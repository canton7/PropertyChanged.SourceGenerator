using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public class TypeAnalysis
    {
        public INamedTypeSymbol TypeSymbol { get; set; } = null!;
        public bool HasInpcInterface { get; set; }
        public bool HasEvent { get; set; }
        public bool HasOnPropertyChangedMethod { get; set; }
        public List<MemberAnalysis> Members { get; } = new();
    }
}
