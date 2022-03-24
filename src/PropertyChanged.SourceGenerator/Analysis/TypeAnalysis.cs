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
        private RaisePropertyChangedMethodInfo _raisePropertyChangedMethod;
        public ref RaisePropertyChangedMethodInfo RaisePropertyChangedMethod => ref this._raisePropertyChangedMethod;
        public OnPropertyNameChangedInfo? OnAnyPropertyChangedInfo { get; set; }
        public string? IsChangedPropertyName { get; set; }
        public bool IsChangedSetterIsPrivate { get; set; }
        public List<MemberAnalysis> Members { get; } = new();
        public NullableContextOptions NullableContext { get; set; }
    }
}
