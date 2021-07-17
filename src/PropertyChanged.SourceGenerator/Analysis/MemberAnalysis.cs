using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public class MemberAnalysis : IMember
    {
        public ISymbol BackingMember { get; set; } = null!;
        public string Name { get; set; } = null!;
        public ITypeSymbol Type { get; set; } = null!;
        bool IMember.IsCallable => true;
        public NullableContextOptions? NullableContextOverride { get; set; }
        public Accessibility GetterAccessibility { get; set; }
        public Accessibility SetterAccessibility { get; set; }
        public OnPropertyNameChangedInfo? OnPropertyNameChanged { get; set; }

        private HashSet<AlsoNotifyMember>? _alsoNotify;
        public IEnumerable<AlsoNotifyMember> AlsoNotify => this._alsoNotify ?? Enumerable.Empty<AlsoNotifyMember>();
        public void AddAlsoNotify(AlsoNotifyMember alsoNotify)
        {
            this._alsoNotify ??= new HashSet<AlsoNotifyMember>();
            this._alsoNotify.Add(alsoNotify);
        }
    }
}