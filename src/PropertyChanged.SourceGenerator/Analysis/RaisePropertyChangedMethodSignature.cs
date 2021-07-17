using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public enum RaisePropertyChangedNameType
    {
        PropertyChangedEventArgs,
        String,
    }

    public struct RaisePropertyChangedMethodSignature
    {
        public RaisePropertyChangedNameType NameType { get; }
        public bool HasOldAndNew { get; }

        public static RaisePropertyChangedMethodSignature Default =>
            new(RaisePropertyChangedNameType.PropertyChangedEventArgs, hasOldAndNew: false);

        public RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType nameType, bool hasOldAndNew)
        {
            this.NameType = nameType;
            this.HasOldAndNew = hasOldAndNew;
        }
    }
}
