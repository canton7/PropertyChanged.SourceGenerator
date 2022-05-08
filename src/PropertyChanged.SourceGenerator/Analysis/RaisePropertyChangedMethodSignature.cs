using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public enum RaisePropertyChangedNameType
{
    PropertyChangedEventArgs,
    String,
}

public struct RaisePropertyChangedMethodSignature
{
    public RaisePropertyChangedNameType NameType { get; }
    public bool HasOld { get; }
    public bool HasNew { get; }
    public Accessibility Accessibility { get; }

    public RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType nameType, bool hasOld, bool hasNew, Accessibility accessibility)
    {
        this.NameType = nameType;
        this.HasOld = hasOld;
        this.HasNew = hasNew;
        this.Accessibility = accessibility;
    }
}
