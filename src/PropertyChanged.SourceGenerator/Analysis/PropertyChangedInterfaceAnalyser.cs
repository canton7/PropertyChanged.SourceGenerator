using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public class PropertyChangedInterfaceAnalyser : InterfaceAnalyser
{
    public const string OnAnyPropertyChangedMethodName = "OnAnyPropertyChanged";

    public PropertyChangedInterfaceAnalyser(
        INamedTypeSymbol interfaceSymbol,
        INamedTypeSymbol eventHandlerSymbol,
        INamedTypeSymbol eventArgsSymbol,
        DiagnosticReporter diagnostics,
        Compilation compilation)
        : base(interfaceSymbol, eventHandlerSymbol, eventArgsSymbol, "PropertyChanged", diagnostics, compilation, x => x.INotifyPropertyChanged)
    {
    }

    protected override bool ShouldGenerateIfInterfaceNotPresent() => true;

    protected override string[] GetRaisePropertyChangedOrChangingEventNames(Configuration config) =>
        config.RaisePropertyChangedMethodNames;

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

        if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 3 &&
            SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.EventArgsSymbol) &&
            IsNormalParameter(x.Parameters[0]) &&
            x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(x.Parameters[1]) &&
            x.Parameters[2].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(x.Parameters[2]))) != null)
        {
            signature = new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs,
                hasOld: true,
                hasNew: true,
                method.DeclaredAccessibility);
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
            signature = new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.String,
                hasOld: true,
                hasNew: true,
                method.DeclaredAccessibility);
            return true;
        }

        signature = default;
        return false;
    }

    protected override void FindOnAnyPropertyChangedOrChangingMethod(
        INamedTypeSymbol typeSymbol,
        InterfaceAnalysis interfaceAnalysis,
        out IMethodSymbol? method)
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

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 3 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                IsNormalParameter(x.Parameters[1]) &&
                x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
                IsNormalParameter(x.Parameters[2]) &&
                x.Parameters[2].Type.SpecialType == SpecialType.System_Object)) != null)
            {
                return new OnPropertyNameChangedInfo(OnAnyPropertyChangedMethodName, hasOld: true, hasNew: true);
            }

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String)) != null)
            {
                return new OnPropertyNameChangedInfo(OnAnyPropertyChangedMethodName, hasOld: false, hasNew: false);
            }

            return null;
        }
    }

    protected override void ReportCouldNotFindRaisePropertyChangingOrChangedMethod(INamedTypeSymbol typeSymbol) =>
        this.Diagnostics.ReportCouldNotFindRaisePropertyChangedMethod(typeSymbol);
    protected override void ReportCouldNotFindCallableRaisePropertyChangedOrChangingOverload(INamedTypeSymbol typeSymbol, string name) =>
        this.Diagnostics.ReportCouldNotFindCallableRaisePropertyChangedOverload(typeSymbol, name);

    protected override void ReportUserDefinedRaisePropertyChangedOrChangingMethodOverride(IMethodSymbol method) =>
        this.Diagnostics.ReportUserDefinedRaisePropertyChangedMethodOverride(method);

    protected override void ReportCannotCallOnAnyPropertyChangedOrChangingBecauseRaisePropertyChangedOrChangingIsNonVirtual(IMethodSymbol method, string raisePropertyChangedMethodName) =>
        this.Diagnostics.ReportCannotCallOnAnyPropertyChangedBecauseRaisePropertyChangedIsNonVirtual(method, raisePropertyChangedMethodName);

    protected override void ReportCannotPopulateOnAnyPropertyChangedOrChangingOldAndNew(IMethodSymbol method, string raisePropertyChangedMethodName) =>
        this.Diagnostics.ReportCannotPopulateOnAnyPropertyChangedOldAndNew(method, raisePropertyChangedMethodName);

    protected override void ReportRaisePropertyChangedOrChangingMethodIsNonVirtual(IMethodSymbol method) =>
        this.Diagnostics.ReportRaisePropertyChangedMethodIsNonVirtual(method);

    protected override string GetOnPropertyNameChangedOrChangingMethodName(string name) => $"On{name}Changed";

    protected override OnPropertyNameChangedInfo? FindCallableOnPropertyNameChangedOrChangingOverload(
        INamedTypeSymbol typeSymbol,
        List<IMethodSymbol> methods,
        string onChangedMethodName,
        ITypeSymbol memberType)
    {
        methods.RemoveAll(x => !IsAccessibleNormalMethod(x, typeSymbol, this.Compilation));

        if (methods.Any(x => x.Parameters.Length == 2 &&
            IsNormalParameter(x.Parameters[0]) &&
            IsNormalParameter(x.Parameters[1]) &&
            SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, x.Parameters[1].Type) &&
            this.Compilation.HasImplicitConversion(memberType, x.Parameters[0].Type)))
        {
            return new OnPropertyNameChangedInfo(onChangedMethodName, hasOld: true, hasNew: true);
        }

        if (methods.Any(x => x.Parameters.Length == 0))
        {
            return new OnPropertyNameChangedInfo(onChangedMethodName, hasOld: false, hasNew: false);
        }

        return null;
    }

    protected override void ReportInvalidOnPropertyNameChangedOrChangingSignature(string name, string onChangedMethodName, IMethodSymbol method) =>
        this.Diagnostics.ReportInvalidOnPropertyNameChangedSignature(name, onChangedMethodName, method);
}
