using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public class ProperyChangingInterfaceAnalyser : InterfaceAnalyser
{
    public const string OnAnyPropertyChangingMethodName = "OnAnyPropertyChanging";

    public ProperyChangingInterfaceAnalyser(
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

    protected override bool TryFindCallableOverload(List<IMethodSymbol> methods, out IMethodSymbol method, out RaisePropertyChangedMethodSignature? signature, INamedTypeSymbol typeSymbol)
    {
        methods.RemoveAll(x => !IsAccessibleNormalMethod(x, typeSymbol, this.Compilation));

        // We care about the order in which we choose an overload, which unfortunately means we're quadratic
        if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.EventArgsSymbol) &&
            IsNormalParameter(x.Parameters[0]))) != null)
        {
            signature = new RaisePropertyChangedMethodSignature(
                RaisePropertyChangedNameType.PropertyChangedEventArgs,
                hasOld: false,
                hasNew: false,
                method.DeclaredAccessibility);
            return true;
        }

        if ((method = methods.FirstOrDefault(x => x.Parameters.Length == 1 &&
            x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            IsNormalParameter(x.Parameters[0]))) != null)
        {
            signature = new RaisePropertyChangedMethodSignature(
                RaisePropertyChangedNameType.String,
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
            signature = new RaisePropertyChangedMethodSignature(
                RaisePropertyChangedNameType.PropertyChangedEventArgs,
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
            signature = new RaisePropertyChangedMethodSignature(
                RaisePropertyChangedNameType.String,
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
                interfaceAnalysis.OnAnyPropertyChangedInfo = result;
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
}
