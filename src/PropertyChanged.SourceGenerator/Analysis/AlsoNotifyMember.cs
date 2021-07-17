using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public struct AlsoNotifyMember : IMember, IEquatable<AlsoNotifyMember>
    {
        public string? Name { get; }
        public ITypeSymbol? Type { get; }
        [MemberNotNullWhen(true, nameof(Type))]
        public bool IsCallable => this.Type != null;

        public OnPropertyNameChangedInfo? OnPropertyNameChanged { get; }

        private AlsoNotifyMember(string? name, ITypeSymbol? type, OnPropertyNameChangedInfo? onPropertyNameChanged)
        {
            this.Name = name;
            this.Type = type;
            this.OnPropertyNameChanged = onPropertyNameChanged;
        }

        public static AlsoNotifyMember NonCallable(string? name) =>
            new(name, null, null);
        public static AlsoNotifyMember FromMemberAnalysis(MemberAnalysis memberAnalysis) =>
            new(memberAnalysis.Name, memberAnalysis.Type, memberAnalysis.OnPropertyNameChanged);
        public static AlsoNotifyMember FromProperty(IPropertySymbol property, OnPropertyNameChangedInfo? onPropertyNameChanged) =>
            new(property.Name, property.Type, onPropertyNameChanged);

        public override bool Equals(object obj) => obj is AlsoNotifyMember other && this.Equals(other);
        public bool Equals(AlsoNotifyMember other) => string.Equals(this.Name, other.Name, StringComparison.Ordinal);
        public override int GetHashCode() => this.Name == null ? 0 : StringComparer.Ordinal.GetHashCode(this.Name);

        public static bool operator ==(AlsoNotifyMember left, AlsoNotifyMember right) => left.Equals(right);
        public static bool operator !=(AlsoNotifyMember left, AlsoNotifyMember right) => !left.Equals(right);
    }
}
