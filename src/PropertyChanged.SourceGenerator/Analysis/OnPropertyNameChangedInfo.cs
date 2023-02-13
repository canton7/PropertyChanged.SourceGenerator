using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyChanged.SourceGenerator.Analysis;

public record struct OnPropertyNameChangedInfo(
    string Name,
    bool HasOld,
    bool HasNew)
{
}
