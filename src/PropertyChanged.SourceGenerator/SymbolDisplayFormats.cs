using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator;

public static class SymbolDisplayFormats
{
    /// <summary>
    /// The name of a symbol, suitably escaped. E.g. "List" or "MethodName"
    /// </summary>
    public static SymbolDisplayFormat SymbolName { get; }

    /// <summary>
    /// The full name of a namespace, suitably escaped. E.g. "System.Collections.Generic"
    /// </summary>
    public static SymbolDisplayFormat Namespace { get; }

    /// <summary>
    /// A class declaration, but not including accessibility or modifiers, e.g. "class List&lt;T&gt;"
    /// </summary>
    public static SymbolDisplayFormat TypeDeclaration { get; }

    /// <summary>
    /// A string suitable for use as the return type from a property or method, suitably escaped.
    /// E.g. "global::System.Collections.Generic.IEnumerable&lt;T&gt;"
    /// </summary>
    public static SymbolDisplayFormat MethodOrPropertyReturnType { get; }

    /// <summary>
    /// A string suitable for passing as a type parameter (i.e. between the &lt; and &gt; when constructing
    /// a call to a generic methods), suitably escaped. E.g. "global::System.Collections.Generic.List&lt;int&gt;"
    /// </summary>
    public static SymbolDisplayFormat TypeParameter { get; }

    static SymbolDisplayFormats()
    {
        SymbolName = new SymbolDisplayFormat(
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        Namespace = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        TypeDeclaration = new SymbolDisplayFormat(
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            kindOptions: SymbolDisplayKindOptions.IncludeTypeKeyword,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        MethodOrPropertyReturnType = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
                | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        TypeParameter = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
                | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
    }
}
