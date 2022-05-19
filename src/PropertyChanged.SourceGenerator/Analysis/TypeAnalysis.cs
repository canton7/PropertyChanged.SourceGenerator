using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public class TypeAnalysis
{
    public bool CanGenerate { get; set; }
    public bool HadException { get; set; }
    public INamedTypeSymbol TypeSymbol { get; set; } = null!;

    public InterfaceAnalysis INotifyPropertyChanged { get; set; } = new();
    public InterfaceAnalysis INotifyPropertyChanging { get; set; } = new();

    public string? IsChangedPropertyName { get; set; }
    public bool IsChangedSetterIsPrivate { get; set; }
    public List<MemberAnalysis> Members { get; } = new();
    public NullableContextOptions NullableContext { get; set; }

    private HashSet<(string baseProperty, AlsoNotifyMember notifyProperty)>? baseDependsOn;
    public void AddDependsOn(string baseProperty, AlsoNotifyMember notifyProperty)
    {
        this.baseDependsOn ??= new();
        this.baseDependsOn.Add((baseProperty, notifyProperty));
    }
    public IEnumerable<(string baseProperty, AlsoNotifyMember notifyProperty)> BaseDependsOn =>
        this.baseDependsOn ?? Enumerable.Empty<(string, AlsoNotifyMember)>();
}
