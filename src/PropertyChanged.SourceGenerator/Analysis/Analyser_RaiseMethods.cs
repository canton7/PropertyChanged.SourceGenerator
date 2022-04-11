using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public partial class Analyser
{
    public const string OnAnyPropertyChangedMethodName = "OnAnyPropertyChanged";

    private void PopulateInterfaceAnalysis(
        INamedTypeSymbol typeSymbol,
        InterfaceAnalysis interfaceAnalysis,
        INamedTypeSymbol eventHanderSymbol,
        IReadOnlyList<TypeAnalysis> baseTypeAnalyses, 
        Configuration config)
    {
        // If we've got any base types we're generating partial types for, that will have the INPC interface
        // and event on, for sure
        interfaceAnalysis.HasInterface = baseTypeAnalyses.Any(x => x.CanGenerate)
            || typeSymbol.AllInterfaces.Contains(this.inpchangedSymbol, SymbolEqualityComparer.Default);

        // Try and find out how we raise the PropertyChanged event
        // 1. If noone's defined the PropertyChanged event yet, we'll define it ourselves
        // 2. Otherwise, try and find a method to raise the event:
        //   a. If PropertyChanged is in a base class, we'll need to abort if we can't find one
        //   b. If PropertyChanged is in our class, we'll just define one and call it

        // They might have defined the event but not the interface, so we'll just look for the event by its
        // signature
        var eventSymbol = TypeAndBaseTypes(typeSymbol)
            .SelectMany(x => x.GetMembers(eventHanderSymbol.Name))
            .OfType<IEventSymbol>()
            .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Type, eventHanderSymbol) &&
                !x.IsStatic);

        bool isGeneratingAnyParent = baseTypeAnalyses.Any(x => x.CanGenerate);

        // If there's no event, the base type in our hierarchy is defining it
        interfaceAnalysis.RequiresEvent = eventSymbol == null && !isGeneratingAnyParent;

        // Try and find a method with a name we recognise and a signature we know how to call
        // We prioritise the method name over things like the signature or where in the type hierarchy
        // it is. One we've found any method with a name we're looking for, stop: it's most likely they've 
        // just messed up the signature
        RaisePropertyChangedMethodSignature? signature = null;
        IMethodSymbol? method = null;
        foreach (string name in config.RaisePropertyChangedMethodNames)
        {
            // We don't filter on IsOverride. That means if there is an override, we'll pick it up before the
            // base type. This matters, because we check whether we found an override further down.
            var methods = TypeAndBaseTypes(typeSymbol)
                .SelectMany(x => x.GetMembers(name))
                .OfType<IMethodSymbol>()
                .Where(x => !x.IsStatic)
                .ToList();
            if (methods.Count > 0)
            {
                if (!TryFindCallableOverload(methods, out method, out signature))
                {
                    interfaceAnalysis.CanCall = false;
                    this.diagnostics.ReportCouldNotFindCallableRaisePropertyChangedOverload(typeSymbol, name);
                }
                break;
            }
        }

        // Get this populated now -- we'll need to adjust our behaviour based on what we find
        this.FindOnAnyPropertyChangedMethod(typeSymbol, interfaceAnalysis, out var onAnyPropertyChangedMethod);

        if (signature != null)
        {
            // We found a method which we know how to call.

            // Users aren't supposed to define their own overrides of this method: they're supposed to define
            // a hook which we call. So if this method was defined on the type we're currently analysing,
            // raise a warning.
            if (SymbolEqualityComparer.Default.Equals(method!.ContainingType, typeSymbol))
            {
                if (method.IsOverride)
                {
                    this.diagnostics.ReportUserDefinedRaisePropertyChangedMethodOverride(method);
                }
                interfaceAnalysis.RaisePropertyChangedMethod.Type = RaisePropertyChangedMethodType.None;
            }
            else if (method.IsVirtual || method.IsOverride)
            {
                interfaceAnalysis.RaisePropertyChangedMethod.Type = RaisePropertyChangedMethodType.Override;
            }
            else
            {
                this.diagnostics.ReportRaisePropertyMethodIsNonVirtual(method);
                if (interfaceAnalysis.OnAnyPropertyChangedInfo != null)
                {
                    this.diagnostics.ReportCannotCallOnAnyPropertyChangedBecauseRaisePropertyChangedIsNonVirtual(onAnyPropertyChangedMethod!, method.Name);
                }
                interfaceAnalysis.RaisePropertyChangedMethod.Type = RaisePropertyChangedMethodType.None;
            }

            if (interfaceAnalysis.OnAnyPropertyChangedInfo?.Signature == OnPropertyNameChangedSignature.OldAndNew &&
                !signature.Value.HasOldAndNew)
            {
                this.diagnostics.ReportCannotPopulateOnAnyPropertyChangedOldAndNew(onAnyPropertyChangedMethod!, method.Name);
            }

            interfaceAnalysis.RaisePropertyChangedMethod.Name = method!.Name;
            interfaceAnalysis.RaisePropertyChangedMethod.Signature = signature.Value;
        }
        else
        {
            // The base type in our hierarchy is defining its own
            // Make sure that that type can actually access the event, if it's pre-existing
            if (eventSymbol != null && !isGeneratingAnyParent &&
                !SymbolEqualityComparer.Default.Equals(eventSymbol.ContainingType, typeSymbol))
            {
                interfaceAnalysis.CanCall = false;
                this.diagnostics.ReportCouldNotFindRaisePropertyChangedMethod(typeSymbol);
            }

            if (isGeneratingAnyParent)
            {
                interfaceAnalysis.RaisePropertyChangedMethod.Type = interfaceAnalysis.OnAnyPropertyChangedInfo == null
                    ? RaisePropertyChangedMethodType.None
                    : RaisePropertyChangedMethodType.Override;
            }
            else
            {
                interfaceAnalysis.RaisePropertyChangedMethod.Type = typeSymbol.IsSealed
                    ? RaisePropertyChangedMethodType.NonVirtual
                    : RaisePropertyChangedMethodType.Virtual;
            }

            interfaceAnalysis.RaisePropertyChangedMethod.Name = config.RaisePropertyChangedMethodNames[0];
            interfaceAnalysis.RaisePropertyChangedMethod.Signature = new RaisePropertyChangedMethodSignature(
                RaisePropertyChangedNameType.PropertyChangedEventArgs,
                hasOldAndNew: interfaceAnalysis.OnAnyPropertyChangedInfo?.Signature == OnPropertyNameChangedSignature.OldAndNew,
                typeSymbol.IsSealed ? Accessibility.Private : Accessibility.Protected);
        }

        bool TryFindCallableOverload(List<IMethodSymbol> methods, out IMethodSymbol method, out RaisePropertyChangedMethodSignature? signature)
        {
            methods.RemoveAll(x => !this.IsAccessibleNormalMethod(x, typeSymbol));

            // We care about the order in which we choose an overload, which unfortunately means we're quadratic
            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.propertyChangedEventArgsSymbol) &&
                IsNormalParameter(x.Parameters[0]))) != null)
            {
                signature = new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.PropertyChangedEventArgs, hasOldAndNew: false, method.DeclaredAccessibility);
                return true;
            }

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                IsNormalParameter(x.Parameters[0]))) != null)
            {
                signature = new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.String, hasOldAndNew: false, method.DeclaredAccessibility);
                return true;
            }

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 3 &&
                SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.propertyChangedEventArgsSymbol) &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
                IsNormalParameter(x.Parameters[1]) &&
                x.Parameters[2].Type.SpecialType == SpecialType.System_Object &&
                IsNormalParameter(x.Parameters[2]))) != null)
            {
                signature = new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.PropertyChangedEventArgs, hasOldAndNew: true, method.DeclaredAccessibility);
                return true;
            }

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 3 &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
                IsNormalParameter(x.Parameters[1]) &&
                x.Parameters[2].Type.SpecialType == SpecialType.System_Object &&
                IsNormalParameter(x.Parameters[2]))) != null)
            {
                signature = new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.String, hasOldAndNew: true, method.DeclaredAccessibility);
                return true;
            }

            signature = default;
            return false;
        }
    }

    private void FindOnAnyPropertyChangedMethod(INamedTypeSymbol typeSymbol, InterfaceAnalysis interfaceAnalysis, out IMethodSymbol? method)
    {
        method = null;

        var methods = typeSymbol.GetMembers(OnAnyPropertyChangedMethodName)
            .OfType<IMethodSymbol>()
            .Where(x => !x.IsOverride && !x.IsStatic)
            .ToList();

        if (methods.Count > 0)
        {
            // FindCallableOverload might remove some...
            var firstMethod = methods[0];
            if (FindCallableOverload(methods, out method) is { } signature)
            {
                interfaceAnalysis.OnAnyPropertyChangedInfo = new OnPropertyNameChangedInfo(OnAnyPropertyChangedMethodName, signature);
            }
            else
            {
                this.diagnostics.ReportInvalidOnAnyPropertyChangedChangedSignature(firstMethod);
            }
        }

        OnPropertyNameChangedSignature? FindCallableOverload(List<IMethodSymbol> methods, out IMethodSymbol method)
        {
            methods.RemoveAll(x => !this.IsAccessibleNormalMethod(x, typeSymbol));

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 3 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                IsNormalParameter(x.Parameters[1]) &&
                x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
                IsNormalParameter(x.Parameters[2]) &&
                x.Parameters[2].Type.SpecialType == SpecialType.System_Object)) != null)
            {
                return OnPropertyNameChangedSignature.OldAndNew;
            }

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String)) != null)
            {
                return OnPropertyNameChangedSignature.Parameterless;
            }

            return null;
        }
    }

    private OnPropertyNameChangedInfo? FindOnPropertyNameChangedMethod(INamedTypeSymbol typeSymbol, IPropertySymbol property) =>
        this.FindOnPropertyNameChangedMethod(typeSymbol, property.Name, property.Type, property.ContainingType);

    /// <param name="typeSymbol">Type we're currently analysing</param>
    /// <param name="name">Name of the property to find an OnPropertyNameChanged method for</param>
    /// <param name="memberType">Type of the property</param>
    /// <param name="containingType">Type containing the property (may be a base type)</param>
    /// <returns></returns>
    private OnPropertyNameChangedInfo? FindOnPropertyNameChangedMethod(
        INamedTypeSymbol typeSymbol, 
        string name,
        ITypeSymbol memberType,
        INamedTypeSymbol containingType)
    {
        string onChangedMethodName = $"On{name}Changed";
        var methods = containingType.GetMembers(onChangedMethodName)
            .OfType<IMethodSymbol>()
            .Where(x => !x.IsOverride && !x.IsStatic)
            .ToList();

        OnPropertyNameChangedInfo? result = null;
        if (methods.Count > 0)
        {
            // FindCallableOverload might remove some...
            var firstMethod = methods[0];
            if (FindCallableOverload(methods) is { } signature)
            {
                result = new OnPropertyNameChangedInfo(onChangedMethodName, signature);
            }
            else
            {
                this.diagnostics.ReportInvalidOnPropertyNameChangedSignature(name, onChangedMethodName, firstMethod);
            }
        }

        return result;

        OnPropertyNameChangedSignature? FindCallableOverload(List<IMethodSymbol> methods)
        {
            methods.RemoveAll(x => !this.IsAccessibleNormalMethod(x, typeSymbol));

            if (methods.Any(x => x.Parameters.Length == 2 &&
                IsNormalParameter(x.Parameters[0]) &&
                IsNormalParameter(x.Parameters[1]) &&
                SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, x.Parameters[1].Type) &&
                this.compilation.HasImplicitConversion(memberType, x.Parameters[0].Type)))
            {
                return OnPropertyNameChangedSignature.OldAndNew;
            }

            if (methods.Any(x => x.Parameters.Length == 0))
            {
                return OnPropertyNameChangedSignature.Parameterless;
            }

            return null;
        }
    }

    private bool IsAccessibleNormalMethod(IMethodSymbol method, ITypeSymbol typeSymbol) =>
        !method.IsGenericMethod &&
        method.ReturnsVoid &&
        this.compilation.IsSymbolAccessibleWithin(method, typeSymbol);

    private static bool IsNormalParameter(IParameterSymbol parameter) =>
        parameter.RefKind == RefKind.None;
}
