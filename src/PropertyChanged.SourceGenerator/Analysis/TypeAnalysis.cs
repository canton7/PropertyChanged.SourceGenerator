using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public class TypeAnalysis
    {
        public bool CanGenerate { get; set; }
        public INamedTypeSymbol TypeSymbol { get; set; } = null!;
        public bool HasInpcInterface { get; set; }
        public bool RequiresEvent { get; set; }
        public bool RequiresRaisePropertyChangedMethod { get; set; }
        public string RaisePropertyChangedMethodName { get; set; } = null!;
        public string? IsChangedPropertyName { get; set; }
        public bool IsChangedSetterIsPrivate { get; set; }
        public RaisePropertyChangedMethodSignature RaisePropertyChangedMethodSignature { get; set; }
        public List<MemberAnalysis> Members { get; } = new();
        public NullableContextOptions NullableContext { get; set; }
        public bool IsSealed { get; set; }

    }
}
