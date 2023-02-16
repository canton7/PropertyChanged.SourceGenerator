using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using PropertyChanged.SourceGenerator.Pipeline;

namespace PropertyChanged.SourceGenerator.Analysis;

public class MemberAnalysisBuilder
{
    public ISymbol BackingMember { get; set; } = null!;
    public string Name { get; set; } = null!;
    public ITypeSymbol Type { get; set; } = null!;
    public bool IsVirtual { get; set; }
    public IReadOnlyList<AttributeData> Attributes { get; set; } = null!;
    public NullableContextOptions? NullableContextOverride { get; set; }
    public Accessibility GetterAccessibility { get; set; }
    public Accessibility SetterAccessibility { get; set; }
    public OnPropertyNameChangedInfo? OnPropertyNameChanged { get; set; }
    public OnPropertyNameChangedInfo? OnPropertyNameChanging { get; set; }
    public List<string>? AttributesForGeneratedProperty { get; set; }

    public string[]? DocComment { get; set; }

    private HashSet<AlsoNotifyMember>? alsoNotify;

    public void AddAlsoNotify(AlsoNotifyMember alsoNotify)
    {
        this.alsoNotify ??= new HashSet<AlsoNotifyMember>(AlsoNotifyMemberNameOnlyComparer.Instance);
        this.alsoNotify.Add(alsoNotify);
    }

    public MemberAnalysis Build()
    {
        return new MemberAnalysis()
        {
            Name = this.Name,
            FullyQualifiedTypeName = this.Type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedTypeName),
            BackingMemberSymbolName = this.BackingMember.ToDisplayString(SymbolDisplayFormats.SymbolName),
            IsVirtual = this.IsVirtual,
            NullableContextOverride = this.NullableContextOverride,
            GetterAccessibility = this.GetterAccessibility,
            SetterAccessibility = this.SetterAccessibility,
            OnPropertyNameChanged = this.OnPropertyNameChanged,
            OnPropertyNameChanging = this.OnPropertyNameChanging,
            AttributesForGeneratedProperty = this.AttributesForGeneratedProperty == null ? ReadOnlyEquatableList<string>.Empty : new ReadOnlyEquatableList<string>(this.AttributesForGeneratedProperty),
            DocComment = this.DocComment == null ? ReadOnlyEquatableList<string>.Empty : new ReadOnlyEquatableList<string>(this.DocComment),
            AlsoNotify = this.alsoNotify == null
                 ? ReadOnlyEquatableList<AlsoNotifyMember>.Empty
                 : new ReadOnlyEquatableList<AlsoNotifyMember>(this.alsoNotify.OrderBy(x => x.Name).ToList()),
        };
    }
}

public class MemberAnalysis : IMember
{
    public required string Name { get; init; }
    public required string FullyQualifiedTypeName { get; init; }
    public required string BackingMemberSymbolName { get; init; }
    public required bool IsVirtual { get; init; }
    public required NullableContextOptions? NullableContextOverride { get; init; }
    public required Accessibility GetterAccessibility { get; init; }
    public required Accessibility SetterAccessibility { get; init; }
    public required OnPropertyNameChangedInfo? OnPropertyNameChanged { get; init; }
    public required OnPropertyNameChangedInfo? OnPropertyNameChanging { get; init; }
    public required ReadOnlyEquatableList<string> AttributesForGeneratedProperty { get; init; }

    public required ReadOnlyEquatableList<string> DocComment { get; init; }

    public required ReadOnlyEquatableList<AlsoNotifyMember> AlsoNotify { get; init; }
}
