using System.Collections.Generic;
using System.Linq;
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

        private HashSet<string?>? _alsoNotify;
        public IEnumerable<string?> AlsoNotify => this._alsoNotify ?? Enumerable.Empty<string?>();
        public void AddAlsoNotify(string? alsoNotify)
        {
            this._alsoNotify ??= new HashSet<string?>();
            this._alsoNotify.Add(alsoNotify);
        }
    }
}