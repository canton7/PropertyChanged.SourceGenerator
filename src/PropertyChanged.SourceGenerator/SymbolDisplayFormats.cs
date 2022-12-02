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
    public static SymbolDisplayFormat FullyQualifiedTypeName { get; }

    /// <summary>
    /// A string suitable for indicating what type an event is defined on, and whether it is implicitly
    /// or explicitly implemented
    /// </summary>
    public static SymbolDisplayFormat EventDefinition { get; }

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

        FullyQualifiedTypeName = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
                | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        EventDefinition = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType
                | SymbolDisplayMemberOptions.IncludeExplicitInterface,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
    }
}
