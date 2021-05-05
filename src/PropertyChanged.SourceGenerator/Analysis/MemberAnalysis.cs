using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public class MemberAnalysis
    {
        public ISymbol BackingMember { get; set; } = null!;
        public string Name { get; set; } = null!;
        public ITypeSymbol Type { get; set; } = null!;
    }
}