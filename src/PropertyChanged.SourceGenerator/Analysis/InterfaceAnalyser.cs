using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public abstract class InterfaceAnalyser
{
    private readonly INamedTypeSymbol interfaceSymbol;
    private readonly INamedTypeSymbol eventHandlerSymbol;
    protected readonly INamedTypeSymbol EventArgsSymbol;
    private readonly string eventName;
    protected readonly DiagnosticReporter Diagnostics;
    protected readonly Compilation Compilation;

    protected InterfaceAnalyser(
        INamedTypeSymbol interfaceSymbol,
        INamedTypeSymbol eventHandlerSymbol,
        INamedTypeSymbol eventArgsSymbol,
        string eventName,
        DiagnosticReporter diagnostics,
        Compilation compilation)
    {
        this.interfaceSymbol = interfaceSymbol;
        this.eventHandlerSymbol = eventHandlerSymbol;
        this.EventArgsSymbol = eventArgsSymbol;
        this.eventName = eventName;
        this.Diagnostics = diagnostics;
        this.Compilation = compilation;
    }

    #region Interface Analysis

    public void PopulateInterfaceAnalysis(
        INamedTypeSymbol typeSymbol,
        InterfaceAnalysis interfaceAnalysis,
        IReadOnlyList<TypeAnalysis> baseTypeAnalyses,
        Configuration config)
    {
        bool hasInterface = typeSymbol.AllInterfaces.Contains(this.interfaceSymbol, SymbolEqualityComparer.Default);
        if (!this.ShouldGenerateIfInterfaceNotPresent() && !hasInterface)
        {
            return;
        }

        // If we've got any base types we're generating partial types for, that will have the INPC interface
        // and event on, for sure
        interfaceAnalysis.RequiresInterface = !hasInterface && !baseTypeAnalyses.Any(x => x.CanGenerate);

        interfaceAnalysis.EventName = this.eventName;
        interfaceAnalysis.EventArgsSymbol = this.EventArgsSymbol;

        // Try and find out how we raise the PropertyChanged event
        // 1. If noone's defined the PropertyChanged event yet, we'll define it ourselves
        // 2. Otherwise, try and find a method to raise the event:
        //   a. If PropertyChanged is in a base class, we'll need to abort if we can't find one
        //   b. If PropertyChanged is in our class, we'll just define one and call it

        // They might have defined the event but not the interface, so we'll just look for the event by its
        // signature
        var eventSymbol = TypeAndBaseTypes(typeSymbol)
            .SelectMany(x => x.GetMembers(this.eventName))
            .OfType<IEventSymbol>()
            .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Type, this.eventHandlerSymbol) &&
                !x.IsStatic);

        bool isGeneratingAnyParent = baseTypeAnalyses.Any(x => x.CanGenerate);

        // If there's no event, the base type in our hierarchy is defining it
        interfaceAnalysis.RequiresEvent = eventSymbol == null && !isGeneratingAnyParent;

        // Try and find a method with a name we recognise and a signature we know how to call
        // We prioritise the method name over things like the signature or where in the type hierarchy
        // it is. One we've found any method with a name we're looking for, stop: it's most likely they've 
        // just messed up the signature
        interfaceAnalysis.CanCallRaiseMethod = true;
        RaisePropertyChangedOrChangingMethodSignature? signature = null;
        IMethodSymbol? method = null;
        foreach (string name in this.GetRaisePropertyChangedOrChangingEventNames(config))
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
                if (!this.TryFindCallableRaisePropertyChangedOrChangingOverload(methods, out method, out signature, typeSymbol))
                {
                    interfaceAnalysis.CanCallRaiseMethod = false;
                    this.ReportCouldNotFindCallableRaisePropertyChangedOrChangingOverload(typeSymbol, name);
                }
                break;
            }
        }

        // Get this populated now -- we'll need to adjust our behaviour based on what we find
        this.FindOnAnyPropertyChangedOrChangingMethod(typeSymbol, interfaceAnalysis, out var onAnyPropertyChangedMethod);

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
                    this.ReportUserDefinedRaisePropertyChangedOrChangingMethodOverride(method);
                }
                interfaceAnalysis.RaiseMethodType = RaisePropertyChangedMethodType.None;
            }
            else if (method.IsVirtual || method.IsOverride)
            {
                interfaceAnalysis.RaiseMethodType = RaisePropertyChangedMethodType.Override;
            }
            else
            {
                this.ReportRaisePropertyChangedOrChangingMethodIsNonVirtual(method);
                if (interfaceAnalysis.OnAnyPropertyChangedOrChangingInfo != null)
                {
                    this.ReportCannotCallOnAnyPropertyChangedOrChangingBecauseRaisePropertyChangedOrChangingIsNonVirtual(onAnyPropertyChangedMethod!, method.Name);
                }
                interfaceAnalysis.RaiseMethodType = RaisePropertyChangedMethodType.None;
            }

            if (interfaceAnalysis.OnAnyPropertyChangedOrChangingInfo is { } info &&
                ((info.HasOld && !signature.Value.HasOld) || (info.HasNew && !signature.Value.HasNew)))
            {
                this.ReportCannotPopulateOnAnyPropertyChangedOrChangingOldAndNew(onAnyPropertyChangedMethod!, method.Name);
            }

            interfaceAnalysis.RaiseMethodName = method!.Name;
            interfaceAnalysis.RaiseMethodSignature = signature.Value;
        }
        else if (interfaceAnalysis.CanCallRaiseMethod)
        {
            // The base type in our hierarchy is defining its own
            // Make sure that that type can actually access the event, if it's pre-existing
            if (eventSymbol != null && !isGeneratingAnyParent &&
                !SymbolEqualityComparer.Default.Equals(eventSymbol.ContainingType, typeSymbol))
            {
                interfaceAnalysis.CanCallRaiseMethod = false;
                interfaceAnalysis.RaiseMethodType = RaisePropertyChangedMethodType.None;
                this.ReportCouldNotFindRaisePropertyChangingOrChangedMethod(typeSymbol);
            }
            else if (isGeneratingAnyParent)
            {
                interfaceAnalysis.RaiseMethodType = interfaceAnalysis.OnAnyPropertyChangedOrChangingInfo == null
                    ? RaisePropertyChangedMethodType.None
                    : RaisePropertyChangedMethodType.Override;
            }
            else
            {
                interfaceAnalysis.RaiseMethodType = typeSymbol.IsSealed
                    ? RaisePropertyChangedMethodType.NonVirtual
                    : RaisePropertyChangedMethodType.Virtual;
            }

            interfaceAnalysis.RaiseMethodName = this.GetRaisePropertyChangedOrChangingEventNames(config)[0];
            interfaceAnalysis.RaiseMethodSignature = new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs,
                hasOld: interfaceAnalysis.OnAnyPropertyChangedOrChangingInfo?.HasOld ?? false,
                hasNew: interfaceAnalysis.OnAnyPropertyChangedOrChangingInfo?.HasNew ?? false,
                typeSymbol.IsSealed ? Accessibility.Private : Accessibility.Protected);
        }
    }

    protected abstract bool ShouldGenerateIfInterfaceNotPresent();

    protected abstract string[] GetRaisePropertyChangedOrChangingEventNames(Configuration config);

    protected abstract bool TryFindCallableRaisePropertyChangedOrChangingOverload(List<IMethodSymbol> methods, out IMethodSymbol method, out RaisePropertyChangedOrChangingMethodSignature? signature, INamedTypeSymbol typeSymbol);

    protected abstract void FindOnAnyPropertyChangedOrChangingMethod(INamedTypeSymbol typeSymbol, InterfaceAnalysis interfaceAnalysis, out IMethodSymbol? method);

    protected abstract void ReportCouldNotFindRaisePropertyChangingOrChangedMethod(INamedTypeSymbol typeSymbol);

    protected abstract void ReportCouldNotFindCallableRaisePropertyChangedOrChangingOverload(INamedTypeSymbol typeSymbol, string name);

    protected abstract void ReportUserDefinedRaisePropertyChangedOrChangingMethodOverride(IMethodSymbol method);

    protected abstract void ReportCannotCallOnAnyPropertyChangedOrChangingBecauseRaisePropertyChangedOrChangingIsNonVirtual(IMethodSymbol method, string raisePropertyChangedMethodName);

    protected abstract void ReportCannotPopulateOnAnyPropertyChangedOrChangingOldAndNew(IMethodSymbol method, string raisePropertyChangedMethodName);

    protected abstract void ReportRaisePropertyChangedOrChangingMethodIsNonVirtual(IMethodSymbol method);

    #endregion

    #region OnPropertyNameChanged / Changing

    /// <param name="typeSymbol">Type we're currently analysing</param>
    /// <param name="name">Name of the property to find an OnPropertyNameChanged method for</param>
    /// <param name="memberType">Type of the property</param>
    /// <param name="containingType">Type containing the property (may be a base type)</param>
    /// <returns></returns>
    public OnPropertyNameChangedInfo? FindOnPropertyNameChangedMethod(
        INamedTypeSymbol typeSymbol,
        string name,
        ITypeSymbol memberType,
        INamedTypeSymbol containingType)
    {
        string onChangedMethodName = this.GetOnPropertyNameChangedOrChangingMethodName(name);
        var methods = containingType.GetMembers(onChangedMethodName)
            .OfType<IMethodSymbol>()
            .Where(x => !x.IsOverride && !x.IsStatic)
            .ToList();

        OnPropertyNameChangedInfo? result = null;
        if (methods.Count > 0)
        {
            // FindCallableOverload might remove some...
            var firstMethod = methods[0];
            if ((result = this.FindCallableOnPropertyNameChangedOrChangingOverload(typeSymbol, methods, onChangedMethodName, memberType)) == null)
            {
                this.ReportInvalidOnPropertyNameChangedOrChangingSignature(name, onChangedMethodName, firstMethod);
            }
        }

        return result;
    }

    protected abstract string GetOnPropertyNameChangedOrChangingMethodName(string name);

    protected abstract OnPropertyNameChangedInfo? FindCallableOnPropertyNameChangedOrChangingOverload(
        INamedTypeSymbol typeSymbol,
        List<IMethodSymbol> methods,
        string onChangedMethodName,
        ITypeSymbol memberType);

    protected abstract void ReportInvalidOnPropertyNameChangedOrChangingSignature(string name, string onChangedMethodName, IMethodSymbol method);

    #endregion
}
