using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public class PropertyChangingInterfaceAnalyser : InterfaceAnalyser
{
    public const string OnAnyPropertyChangingMethodName = "OnAnyPropertyChanging";

    public PropertyChangingInterfaceAnalyser(
        INamedTypeSymbol interfaceSymbol,
        string eventHandlerMetadataName,
        INamedTypeSymbol eventArgsSymbol,
        DiagnosticReporter diagnostics,
        Compilation compilation)
        : base(interfaceSymbol, eventHandlerMetadataName, eventArgsSymbol, "PropertyChanging", diagnostics, compilation, x => x.INotifyPropertyChanging!)
    {
    }

    protected override bool ShouldGenerateIfInterfaceNotPresent() => false;

    protected override ImmutableArray<string> GetRaisePropertyChangedOrChangingEventNames(Configuration config) =>
        config.RaisePropertyChangingMethodNames;

    protected override RaisePropertyChangedOrChangingMethodSignature? TryClassifyRaisePropertyChangedOrChangingMethod(IMethodSymbol method, INamedTypeSymbol typeSymbol)
    {
        if (!IsAccessibleNormalMethod(method, typeSymbol, this.Compilation))
        {
            return null;
        }

        // We care about the order in which we choose an overload, which unfortunately means we're quadratic
        if (method.Parameters.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, this.EventArgsSymbol) &&
            IsNormalParameter(method.Parameters[0]))
        {
            return new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs,
                HasOld: false,
                HasNew: false,
                method.DeclaredAccessibility);
        }

        if (method.Parameters.Length == 1 &&
            method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            IsNormalParameter(method.Parameters[0]))
        {
            return new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.String,
                HasOld: false,
                HasNew: false,
                method.DeclaredAccessibility);
        }

        if (method.Parameters.Length == 2 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, this.EventArgsSymbol) &&
            IsNormalParameter(method.Parameters[0]) &&
            method.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(method.Parameters[1]))
        {
            return new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs,
                HasOld: true,
                HasNew: false,
                method.DeclaredAccessibility);
        }

        if (method.Parameters.Length == 2 &&
            method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            IsNormalParameter(method.Parameters[0]) &&
            method.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(method.Parameters[1]))
        {
            return new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.String,
                HasOld: true,
                HasNew: false,
                method.DeclaredAccessibility);
        }

        return null;
    }

    protected override OnPropertyNameChangedInfo? FindOnAnyPropertyChangedOrChangingMethod(INamedTypeSymbol typeSymbol, out IMethodSymbol? method)
    {
        method = null;

        var methods = typeSymbol.GetMembers(OnAnyPropertyChangingMethodName)
            .OfType<IMethodSymbol>()
            .Where(x => !x.IsOverride && !x.IsStatic)
            .ToList();

        OnPropertyNameChangedInfo? result = null;
        if (methods.Count > 0)
        {
            // FindCallableOverload might remove some...
            var firstMethod = methods[0];
            if (FindCallableOverload(methods, out method) is { } found)
            {
                result = found;
            }
            else
            {
                this.Diagnostics.ReportInvalidOnAnyPropertyChangedChangedSignature(firstMethod);
            }
        }

        return result;

        OnPropertyNameChangedInfo? FindCallableOverload(List<IMethodSymbol> methods, out IMethodSymbol method)
        {
            methods.RemoveAll(x => !IsAccessibleNormalMethod(x, typeSymbol, this.Compilation));

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 2 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                IsNormalParameter(x.Parameters[1]) &&
                x.Parameters[1].Type.SpecialType == SpecialType.System_Object)) != null)
            {
                return new OnPropertyNameChangedInfo(OnAnyPropertyChangingMethodName, HasOld: true, HasNew: false);
            }

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String)) != null)
            {
                return new OnPropertyNameChangedInfo(OnAnyPropertyChangingMethodName, HasOld: false, HasNew: false);
            }

            return null;
        }
    }

    protected override void ReportCouldNotFindRaisePropertyChangingOrChangedMethod(INamedTypeSymbol typeSymbol, string eventName) =>
        this.Diagnostics.ReportCouldNotFindRaisePropertyChangingMethod(typeSymbol, eventName);

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
            return new OnPropertyNameChangedInfo(onChangedMethodName, HasOld: true, HasNew: false);
        }

        if (methods.Any(x => x.Parameters.Length == 0))
        {
            return new OnPropertyNameChangedInfo(onChangedMethodName, HasOld: false, HasNew: false);
        }

        return null;
    }

    protected override void ReportInvalidOnPropertyNameChangedOrChangingSignature(string name, string onChangedMethodName, IMethodSymbol method) =>
        this.Diagnostics.ReportInvalidOnPropertyNameChangingSignature(name, onChangedMethodName, method);
}
