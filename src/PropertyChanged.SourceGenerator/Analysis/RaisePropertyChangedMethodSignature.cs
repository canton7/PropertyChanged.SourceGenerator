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
    public bool HasOldAndNew { get; }
    public Accessibility Accessibility { get; }

    public RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType nameType, bool hasOldAndNew, Accessibility accessibility)
    {
        this.NameType = nameType;
        this.HasOldAndNew = hasOldAndNew;
        this.Accessibility = accessibility;
    }
}
