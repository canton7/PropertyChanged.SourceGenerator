using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public class MemberAnalysis
    {
        public ISymbol BackingMember { get; set; } = null!;
        public string Name { get; set; } = null!;
        public ITypeSymbol Type { get; set; } = null!;
        public NullableContextOptions? NullableContextOverride { get; set; }
        public Accessibility GetterAccessibility { get; set; }
        public Accessibility SetterAccessibility { get; set; }
    }
}