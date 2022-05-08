using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;
public static class Utils
{
    public static IEnumerable<INamedTypeSymbol> TypeAndBaseTypes(INamedTypeSymbol type)
    {
        // Stop at 'object': no point in analysing that
        for (var t = type; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
        {
            yield return t;
        }
    }

    public static bool IsAccessibleNormalMethod(IMethodSymbol method, ITypeSymbol typeSymbol, Compilation compilation) =>
        !method.IsGenericMethod &&
        method.ReturnsVoid &&
        compilation.IsSymbolAccessibleWithin(method, typeSymbol);

    public static bool IsNormalParameter(IParameterSymbol parameter) =>
        parameter.RefKind == RefKind.None;
}
