using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public class PropertyChangedInterfaceAnalyser : InterfaceAnalyser
{
    public const string OnAnyPropertyChangedMethodName = "OnAnyPropertyChanged";

    public PropertyChangedInterfaceAnalyser(
        INamedTypeSymbol interfaceSymbol,
        string eventHandlerMetadataName,
        INamedTypeSymbol eventArgsSymbol,
        DiagnosticReporter diagnostics,
        Compilation compilation)
        : base(interfaceSymbol, eventHandlerMetadataName, eventArgsSymbol, "PropertyChanged", diagnostics, compilation, x => x.INotifyPropertyChanged!)
    {
    }

    protected override bool ShouldGenerateIfInterfaceNotPresent() => true;

    protected override ImmutableArray<string> GetRaisePropertyChangedOrChangingEventNames(Configuration config) =>
        config.RaisePropertyChangedMethodNames;

    protected override RaisePropertyChangedOrChangingMethodSignature? TryClassifyRaisePropertyChangedOrChangingMethod(IMethodSymbol method, INamedTypeSymbol typeSymbol)
    {
        if (!IsAccessibleNormalMethod(method, typeSymbol, this.Compilation))
        {
            return null;
        }

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

        if (method.Parameters.Length == 3 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, this.EventArgsSymbol) &&
            IsNormalParameter(method.Parameters[0]) &&
            method.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(method.Parameters[1]) &&
            method.Parameters[2].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(method.Parameters[2]))
        {
            return new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs,
                HasOld: true,
                HasNew: true,
                method.DeclaredAccessibility);
        }

        if (method.Parameters.Length == 3 &&
            method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            IsNormalParameter(method.Parameters[0]) &&
            method.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(method.Parameters[1]) &&
            method.Parameters[2].Type.SpecialType == SpecialType.System_Object &&
            IsNormalParameter(method.Parameters[2]))
        {
            return new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.String,
                HasOld: true,
                HasNew: true,
                method.DeclaredAccessibility);
        }

        return null;
    }

    protected override OnPropertyNameChangedInfo? FindOnAnyPropertyChangedOrChangingMethod(
        INamedTypeSymbol typeSymbol,
        out IMethodSymbol? method)
    {
        method = null;

        var methods = typeSymbol.GetMembers(OnAnyPropertyChangedMethodName)
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

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 3 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                IsNormalParameter(x.Parameters[1]) &&
                x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
                IsNormalParameter(x.Parameters[2]) &&
                x.Parameters[2].Type.SpecialType == SpecialType.System_Object)) != null)
            {
                return new OnPropertyNameChangedInfo(OnAnyPropertyChangedMethodName, HasOld: true, HasNew: true);
            }

            if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
                IsNormalParameter(x.Parameters[0]) &&
                x.Parameters[0].Type.SpecialType == SpecialType.System_String)) != null)
            {
                return new OnPropertyNameChangedInfo(OnAnyPropertyChangedMethodName, HasOld: false, HasNew: false);
            }

            return null;
        }
    }

    protected override void ReportCouldNotFindRaisePropertyChangingOrChangedMethod(INamedTypeSymbol typeSymbol, string eventName) =>
        this.Diagnostics.ReportCouldNotFindRaisePropertyChangedMethod(typeSymbol, eventName);
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
            return new OnPropertyNameChangedInfo(onChangedMethodName, HasOld: true, HasNew: true);
        }

        if (methods.Any(x => x.Parameters.Length == 0))
        {
            return new OnPropertyNameChangedInfo(onChangedMethodName, HasOld: false, HasNew: false);
        }

        return null;
    }

    protected override void ReportInvalidOnPropertyNameChangedOrChangingSignature(string name, string onChangedMethodName, IMethodSymbol method) =>
        this.Diagnostics.ReportInvalidOnPropertyNameChangedSignature(name, onChangedMethodName, method);
}
