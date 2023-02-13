using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public record struct AlsoNotifyMember : IMember, IEquatable<AlsoNotifyMember>
{
    public string? Name { get; }
    public string? FullyQualifiedTypeName { get; }
    [MemberNotNullWhen(true, nameof(FullyQualifiedTypeName))]
    public bool IsCallable => this.FullyQualifiedTypeName != null;

    public OnPropertyNameChangedInfo? OnPropertyNameChanged { get; }
    public OnPropertyNameChangedInfo? OnPropertyNameChanging { get; }

    private AlsoNotifyMember(
        string? name,
        ITypeSymbol? type,
        OnPropertyNameChangedInfo? onPropertyNameChanged,
        OnPropertyNameChangedInfo? onPropertyNameChanging)
    {
        this.Name = name;
        this.FullyQualifiedTypeName = type?.ToDisplayString(SymbolDisplayFormats.FullyQualifiedTypeName);
        this.OnPropertyNameChanged = onPropertyNameChanged;
        this.OnPropertyNameChanging = onPropertyNameChanging;
    }

    public static AlsoNotifyMember NonCallable(string? name) =>
        new(name, null, null, null);

    public static AlsoNotifyMember FromMemberAnalysis(MemberAnalysisBuilder memberAnalysis) =>
        new(memberAnalysis.Name, memberAnalysis.Type, memberAnalysis.OnPropertyNameChanged, memberAnalysis.OnPropertyNameChanging);
    
    public static AlsoNotifyMember FromProperty(
        IPropertySymbol property,
        (OnPropertyNameChangedInfo? onPropertyNameChanged, OnPropertyNameChangedInfo? onPropertyNameChanging) namedChangedInfo)
    {
        // If we have an explicitly-implemented property, use e.g. 'Foo' as the name, not ISomeInterface.Foo
        return new(
            property.ToDisplayString(SymbolDisplayFormats.SymbolName),
            property.Type,
            namedChangedInfo.onPropertyNameChanged,
            namedChangedInfo.onPropertyNameChanging);
    }
}

public class AlsoNotifyMemberNameOnlyComparer : IEqualityComparer<AlsoNotifyMember>
{
    public static AlsoNotifyMemberNameOnlyComparer Instance { get; } = new();

    public bool Equals(AlsoNotifyMember x, AlsoNotifyMember y) => string.Equals(x.Name, y.Name, StringComparison.Ordinal);
    public int GetHashCode(AlsoNotifyMember obj) => obj.Name == null ? 0 : StringComparer.Ordinal.GetHashCode(obj.Name);
}