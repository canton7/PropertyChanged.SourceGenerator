using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public struct AlsoNotifyMember : IEquatable<AlsoNotifyMember>
    {
        public string? Name { get; }
        public ITypeSymbol? Type { get; }
        [MemberNotNullWhen(true, nameof(Type))]
        public bool IsCallable => this.Type != null;

        public AlsoNotifyMember(string? name, ITypeSymbol? type)
        {
            this.Name = name;
            this.Type = type;
        }

        public override bool Equals(object obj) => obj is AlsoNotifyMember other && this.Equals(other);
        public bool Equals(AlsoNotifyMember other) => string.Equals(this.Name, other.Name, StringComparison.Ordinal);
        public override int GetHashCode() => this.Name == null ? 0 : StringComparer.Ordinal.GetHashCode(this.Name);

        public static bool operator ==(AlsoNotifyMember left, AlsoNotifyMember right) => left.Equals(right);
        public static bool operator !=(AlsoNotifyMember left, AlsoNotifyMember right) => !left.Equals(right);
    }
}
