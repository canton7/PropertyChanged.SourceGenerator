using System;

namespace PropertyChanged.SourceGenerator.EventArgs;

internal readonly struct EventArgsCacheKey : IEquatable<EventArgsCacheKey>, IComparable<EventArgsCacheKey>
{
    public string? PropertyName { get; }
    public string EventArgsTypeName { get; }

    public EventArgsCacheKey(string? propertyName, string eventArgsTypeName)
    {
        this.PropertyName = propertyName;
        this.EventArgsTypeName = eventArgsTypeName;
    }

    public int CompareTo(EventArgsCacheKey other)
    {
        int result = StringComparer.Ordinal.Compare(this.PropertyName, other.PropertyName);
        if (result != 0)
            return result;

        result = string.Compare(this.EventArgsTypeName, other.EventArgsTypeName);
        return result;
    }

    public bool Equals(EventArgsCacheKey other)
    {
        return string.Equals(this.PropertyName, other.PropertyName, StringComparison.Ordinal) &&
            this.EventArgsTypeName == other.EventArgsTypeName;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + (this.PropertyName == null ? 0 : StringComparer.Ordinal.GetHashCode(this.PropertyName));
            hash = hash * 23 + this.EventArgsTypeName.GetHashCode();
            return hash;
        }
    }

    public override bool Equals(object? obj) => obj is EventArgsCacheKey other && this.Equals(other);

    public static bool operator ==(EventArgsCacheKey left, EventArgsCacheKey right) => left.Equals(right);
    public static bool operator !=(EventArgsCacheKey left, EventArgsCacheKey right) => !left.Equals(right);
    public static bool operator <(EventArgsCacheKey left, EventArgsCacheKey right) => left.CompareTo(right) < 0;
    public static bool operator <=(EventArgsCacheKey left, EventArgsCacheKey right) => left.CompareTo(right) <= 0;
    public static bool operator >(EventArgsCacheKey left, EventArgsCacheKey right) => left.CompareTo(right) > 0;
    public static bool operator >=(EventArgsCacheKey left, EventArgsCacheKey right) => left.CompareTo(right) >= 0;
}

