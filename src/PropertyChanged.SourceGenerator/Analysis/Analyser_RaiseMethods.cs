using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public partial class Analyser
{
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
            if ((result = FindCallableOverload(methods, onChangedMethodName)) == null)
            {
                this.diagnostics.ReportInvalidOnPropertyNameChangedSignature(name, onChangedMethodName, firstMethod);
            }
        }

        return result;

        OnPropertyNameChangedInfo? FindCallableOverload(List<IMethodSymbol> methods, string onChangedMethodName)
        {
            methods.RemoveAll(x => !IsAccessibleNormalMethod(x, typeSymbol, this.compilation));

            if (methods.Any(x => x.Parameters.Length == 2 &&
                IsNormalParameter(x.Parameters[0]) &&
                IsNormalParameter(x.Parameters[1]) &&
                SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, x.Parameters[1].Type) &&
                this.compilation.HasImplicitConversion(memberType, x.Parameters[0].Type)))
            {
                // TODO: Needs to support OnPropertyNameChanging as well
                return new OnPropertyNameChangedInfo(onChangedMethodName, hasOld: true, hasNew: true);
            }

            if (methods.Any(x => x.Parameters.Length == 0))
            {
                return new OnPropertyNameChangedInfo(onChangedMethodName, hasOld: false, hasNew: false);
            }

            return null;
        }
    }
}
