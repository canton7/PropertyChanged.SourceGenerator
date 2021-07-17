using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public enum OnPropertyNameChangedSignature
    {
        Parameterless,
        OldAndNew,
    }

    public struct OnPropertyNameChangedInfo
    {
        public string Name { get; }
        public OnPropertyNameChangedSignature Signature { get; }

        public OnPropertyNameChangedInfo(string name, OnPropertyNameChangedSignature signature)
        {
            this.Name = name;
            this.Signature = signature;
        }
    }
}
