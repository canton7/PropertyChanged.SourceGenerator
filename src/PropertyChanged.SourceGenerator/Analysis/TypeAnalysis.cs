using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using PropertyChanged.SourceGenerator.Pipeline;

namespace PropertyChanged.SourceGenerator.Analysis;

public class TypeAnalysisBuilder
{
    public required INamedTypeSymbol TypeSymbol { get; init; }

    public bool CanGenerate { get; set; }
    public bool HadException { get; set; }

    public InterfaceAnalysis? INotifyPropertyChanged { get; set; }
    public InterfaceAnalysis? INotifyPropertyChanging { get; set; }

    public string? IsChangedPropertyName { get; set; }
    public bool IsChangedSetterIsPrivate { get; set; }
    public List<MemberAnalysisBuilder> Members { get; } = new();
    public NullableContextOptions NullableContext { get; set; }

    private HashSet<(string baseProperty, AlsoNotifyMember notifyProperty)>? baseDependsOn;
    public void AddDependsOn(string baseProperty, AlsoNotifyMember notifyProperty)
    {
        this.baseDependsOn ??= new();
        this.baseDependsOn.Add((baseProperty, notifyProperty));
    }

    public TypeAnalysis Build()
    {
        string? containingNamespace = null;
        if (this.TypeSymbol.ContainingNamespace is { IsGlobalNamespace: false } @namespace)
        {
            containingNamespace = @namespace.ToDisplayString(SymbolDisplayFormats.Namespace);
        }

        var outerTypes = new List<string>();
        for (var outerType = this.TypeSymbol.ContainingType; outerType != null; outerType = outerType.ContainingType)
        {
            outerTypes.Add(outerType.ToDisplayString(SymbolDisplayFormats.TypeDeclaration));
        }

        return new()
        {
            CanGenerate = this.CanGenerate,
            TypeDeclaration = this.TypeSymbol.ToDisplayString(SymbolDisplayFormats.TypeDeclaration),
            TypeNameForGeneratedFileName = this.TypeSymbol.ToDisplayString(SymbolDisplayFormats.GeneratedFileName),
            ContainingNamespace = containingNamespace,
            OuterTypes = new ReadOnlyEquatableList<string>(outerTypes),
            INotifyPropertyChanged = this.INotifyPropertyChanged ?? throw new ArgumentNullException(nameof(this.INotifyPropertyChanged)),
            INotifyPropertyChanging = this.INotifyPropertyChanging ?? throw new ArgumentNullException(nameof(this.INotifyPropertyChanging)),
            IsChangedPropertyName = this.IsChangedPropertyName,
            IsChangedSetterIsPrivate = this.IsChangedSetterIsPrivate,
            Members = new ReadOnlyEquatableList<MemberAnalysis>(this.Members.Select(x => x.Build()).ToList()),
            NullableContext = this.NullableContext,
            // TODO: Generator turns this into a lookup straight away -- can we just store as that?
            BaseDependsOn = this.baseDependsOn == null
                ? ReadOnlyEquatableList<(string, AlsoNotifyMember)>.Empty
                : new ReadOnlyEquatableList<(string, AlsoNotifyMember)>(this.baseDependsOn.OrderBy(x => x.baseProperty).ThenBy(x => x.notifyProperty.Name).ToList()),
        };
    }
}

public record TypeAnalysis
{
    public required bool CanGenerate { get; init; }
    public required string TypeDeclaration { get; init; }
    public required string TypeNameForGeneratedFileName { get; init; }
    public required string? ContainingNamespace { get; init; }
    public required ReadOnlyEquatableList<string> OuterTypes { get; init; }

    public required InterfaceAnalysis INotifyPropertyChanged { get; init; }
    public required InterfaceAnalysis INotifyPropertyChanging { get; init; }

    public required string? IsChangedPropertyName { get; init; }
    public required bool IsChangedSetterIsPrivate { get; init; }
    public required ReadOnlyEquatableList<MemberAnalysis> Members { get; init; }
    public required NullableContextOptions NullableContext { get; init; }

    public required ReadOnlyEquatableList<(string baseProperty, AlsoNotifyMember notifyProperty)> BaseDependsOn { get; init; }
}
