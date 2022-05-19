using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public class PropertyChangingInterfaceAnalyser : InterfaceAnalyser
{
    public const string OnAnyPropertyChangingMethodName = "OnAnyPropertyChanging";

    public PropertyChangingInterfaceAnalyser(
        INamedTypeSymbol interfaceSymbol,
        INamedTypeSymbol eventHandlerSymbol,
        INamedTypeSymbol eventArgsSymbol,
        DiagnosticReporter diagnostics,
        Compilation compilation)
        : base(interfaceSymbol, eventHandlerSymbol, eventArgsSymbol, "PropertyChanging", diagnostics, compilation)
    {
    }

    protected override bool ShouldGenerateIfInterfaceNotPresent() => false;

    protected override string[] GetRaisePropertyChangedOrChangingEventNames(Configuration config) =>
        config.RaisePropertyChangingMethodNames;

    protected override bool TryFindCallableRaisePropertyChangedOrChangingOverload(List<IMethodSymbol> methods, out IMethodSymbol method, out RaisePropertyChangedOrChangingMethodSignature? signature, INamedTypeSymbol typeSymbol)
    {
        methods.RemoveAll(x => !IsAccessibleNormalMethod(x, typeSymbol, this.Compilation));

        // We care about the order in which we choose an overload, which unfortunately means we're quadratic
        if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.EventArgsSymbol) &&
            IsNormalParameter(x.Parameters[0]))) != null)
        {
            signature = new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs,
                hasOld: false,
                hasNew: false,
                method.DeclaredAccessibility);
            return true;
        }

        if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
            x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            IsNormalParameter(x.Parameters[0]))) != null)
        {
            signature = new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.String,
                hasOld: false,
                hasNew: false,
                method.DeclaredAccessibility);
            return true;
        }

        if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 2 &&
            SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.EventArgsSymbol) &&
            IsNormalParameter(x.Parameters[0]) &&
            x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(x.Parameters[1]))) != null)
        {
            signature = new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs,
                hasOld: true,
                hasNew: false,
                method.DeclaredAccessibility);
            return true;
        }

        if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 2 &&
            x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            IsNormalParameter(x.Parameters[0]) &&
            x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(x.Parameters[1]))) != null)
        {
            signature = new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.String,
                hasOld: true,
                hasNew: false,
                method.DeclaredAccessibility);
            return true;
        }

        signature = default;
        return false;
    }

    protected override void FindOnAnyPropertyChangedOrChangingMethod(INamedTypeSymbol typeSymbol, InterfaceAnalysis interfaceAnalysis, out IMethodSymbol? method)
    {
        method = null;

        var methods = typeSymbol.GetMembers(OnAnyPropertyChangingMethodName)
            .OfType<IMethodSymbol>()
            .Where(x => !x.IsOverride && !x.IsStatic)
            .ToList();

        if (methods.Count > 0)
        {
            // FindCallableOverload might remove some...
            var firstMethod = methods[0];
            if (FindCallableOverload(methods, out method) is { } result)
            {
                interfaceAnalysis.OnAnyPropertyChangedOrChangingInfo = result;
            }
            else
            {
                this.Diagnostics.ReportInvalidOnAnyPropertyChangedChangedSignature(firstMethod);
            }
        }

        OnPropertyNameChangedInfo? FindCallableOverload(List<IMethodSymbol> methods, out IMethodSymbol method)
        {
            methods.RemoveAll(x => !IsAccessibleNormalMethod(x, typeSymbol, this.Compilation));

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 2 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                IsNormalParameter(x.Parameters[1]) &&
                x.Parameters[1].Type.SpecialType == SpecialType.System_Object)) != null)
            {
                return new OnPropertyNameChangedInfo(OnAnyPropertyChangingMethodName, hasOld: true, hasNew: false);
            }

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String)) != null)
            {
                return new OnPropertyNameChangedInfo(OnAnyPropertyChangingMethodName, hasOld: false, hasNew: false);
            }

            return null;
        }
    }

    protected override void ReportCouldNotFindRaisePropertyChangingOrChangedMethod(INamedTypeSymbol typeSymbol) =>
        this.Diagnostics.ReportCouldNotFindRaisePropertyChangingMethod(typeSymbol);

    protected override void ReportCouldNotFindCallableRaisePropertyChangedOrChangingOverload(INamedTypeSymbol typeSymbol, string name) =>
        this.Diagnostics.ReportCouldNotFindCallableRaisePropertyChangingOverload(typeSymbol, name);

    protected override void ReportUserDefinedRaisePropertyChangedOrChangingMethodOverride(IMethodSymbol method) =>
        this.Diagnostics.ReportUserDefinedRaisePropertyChangingMethodOverride(method);

    protected override void ReportCannotCallOnAnyPropertyChangedOrChangingBecauseRaisePropertyChangedOrChangingIsNonVirtual(IMethodSymbol method, string raisePropertyChangedMethodName) =>
        this.Diagnostics.ReportCannotCallOnAnyPropertyChangingBecauseRaisePropertyChangingIsNonVirtual(method, raisePropertyChangedMethodName);

    protected override void ReportCannotPopulateOnAnyPropertyChangedOrChangingOldAndNew(IMethodSymbol method, string raisePropertyChangedMethodName) =>
        this.Diagnostics.ReportCannotPopulateOnAnyPropertyChangingOldAndNew(method, raisePropertyChangedMethodName);

    protected override void ReportRaisePropertyChangedOrChangingMethodIsNonVirtual(IMethodSymbol method) =>
        this.Diagnostics.ReportRaisePropertyChangingMethodIsNonVirtual(method);

    protected override string GetOnPropertyNameChangedOrChangingMethodName(string name) => $"On{name}Changing";

    protected override OnPropertyNameChangedInfo? FindCallableOnPropertyNameChangedOrChangingOverload(
        INamedTypeSymbol typeSymbol,
        List<IMethodSymbol> methods,
        string onChangedMethodName,
        ITypeSymbol memberType)
    {
        methods.RemoveAll(x => !IsAccessibleNormalMethod(x, typeSymbol, this.Compilation));

        if (methods.Any(x => x.Parameters.Length == 1 &&
            IsNormalParameter(x.Parameters[0]) &&
            this.Compilation.HasImplicitConversion(memberType, x.Parameters[0].Type)))
        {
            return new OnPropertyNameChangedInfo(onChangedMethodName, hasOld: true, hasNew: false);
        }

        if (methods.Any(x => x.Parameters.Length == 0))
        {
            return new OnPropertyNameChangedInfo(onChangedMethodName, hasOld: false, hasNew: false);
        }

        return null;
    }

    protected override void ReportInvalidOnPropertyNameChangedOrChangingSignature(string name, string onChangedMethodName, IMethodSymbol method) =>
        this.Diagnostics.ReportInvalidOnPropertyNameChangingSignature(name, onChangedMethodName, method);
}
