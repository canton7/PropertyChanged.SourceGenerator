using System.Collections.Generic;
using System.Linq;

namespace PropertyChanged.SourceGenerator.Analysis;

public struct RaisePropertyChangedMethodInfo
{
    public RaisePropertyChangedMethodType Type { get; set; }
    public string Name { get; set; }
    public RaisePropertyChangedMethodSignature Signature { get; set; }

    private HashSet<(string baseProperty, AlsoNotifyMember notifyProperty)> baseDependsOn;
    public void AddDependsOn(string baseProperty, AlsoNotifyMember notifyProperty)
    {
        this.baseDependsOn ??= new();
        this.baseDependsOn.Add((baseProperty, notifyProperty));
    }
    public IEnumerable<(string baseProperty, AlsoNotifyMember notifyProperty)> BaseDependsOn =>
        this.baseDependsOn ?? Enumerable.Empty<(string, AlsoNotifyMember)>();
}
