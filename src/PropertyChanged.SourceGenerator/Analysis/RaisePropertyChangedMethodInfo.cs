using System.Collections.Generic;
using System.Linq;

namespace PropertyChanged.SourceGenerator.Analysis;

public struct RaisePropertyChangedMethodInfo
{
    public RaisePropertyChangedMethodType Type { get; set; }
    public string Name { get; set; }
    public RaisePropertyChangedMethodSignature Signature { get; set; }
}
