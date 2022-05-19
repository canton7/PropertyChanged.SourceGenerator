using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyChanged.SourceGenerator.Analysis;

public struct OnPropertyNameChangedInfo
{
    public string Name { get; }
    public bool HasOld { get; }
    public bool HasNew { get; }

    public OnPropertyNameChangedInfo(string name, bool hasOld, bool hasNew)
    {
        this.Name = name;
        this.HasOld = hasOld;
        this.HasNew = hasNew;
    }
}
