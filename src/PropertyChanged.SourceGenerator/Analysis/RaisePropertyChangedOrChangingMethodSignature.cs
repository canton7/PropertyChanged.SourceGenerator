using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public enum RaisePropertyChangedOrChangingNameType
{
    PropertyChangedEventArgs,
    String,
}

public record struct RaisePropertyChangedOrChangingMethodSignature(
    RaisePropertyChangedOrChangingNameType NameType,
    bool HasOld,
    bool HasNew,
    Accessibility Accessibility)
{
}
