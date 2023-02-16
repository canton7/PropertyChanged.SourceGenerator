using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator;

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

public class EventArgsCacheBuilder
{
    // propertyName: The string passed to PropertyChangedEventArgs being cached
    // cacheName: The name of the generated property on the EventArgs cache

    private static readonly Regex invalidCharsRegex = new(@"(^[^a-zA-Z])|([^a-zA-Z0-9])");
    // We use a sorted dictionary to make equality comparisons easier
    private readonly SortedDictionary<EventArgsCacheKey, string> propertyNameToCacheName = new();
    private readonly HashSet<string> cacheNames = new(StringComparer.Ordinal);

    public void Add(string? propertyName, string typeName, string eventArgsTypeName)
    {
        var key = new EventArgsCacheKey(propertyName, eventArgsTypeName);
        if (this.propertyNameToCacheName.ContainsKey(key))
        {
            return;
        }

        string safeName = $"{typeName}_" + propertyName switch
        {
            null => "Null",
            "" => "Empty",
            // Reserve the names 'Null' and 'Empty' for actual null/empty strings
            "Null" => "Null1",
            "Empty" => "Empty1",
            _ => invalidCharsRegex.Replace(propertyName, "_"),
        };

        string cacheName = safeName;
        for (int i = 1; this.cacheNames.Contains(cacheName); i++)
        {
            cacheName = safeName + i;
        }

        this.propertyNameToCacheName.Add(key, cacheName);
        this.cacheNames.Add(cacheName);
    }

    public EventArgsCache ToCache() => new(this.propertyNameToCacheName);
}

public class EventArgsCache
{
    private readonly SortedDictionary<EventArgsCacheKey, string> propertyNameToCacheName;
    private int? hashCode;

    public bool IsEmpty => this.propertyNameToCacheName.Count == 0;

    internal EventArgsCache(SortedDictionary<EventArgsCacheKey, string> propertyNameToCacheName)
    {
        this.propertyNameToCacheName = propertyNameToCacheName;
    }

    public string Get(string? propertyName, string eventArgsTypeName)
    {
        var key = new EventArgsCacheKey(propertyName, eventArgsTypeName);
        // TODO: Error Handling?
        return this.propertyNameToCacheName[key];
    }

    public IEnumerable<(string cacheName, string eventArgsTypeName, string? propertyName)> GetEntries()
    {
        foreach (var kvp in this.propertyNameToCacheName)
        {
            yield return (kvp.Value, kvp.Key.EventArgsTypeName, kvp.Key.PropertyName);
        }
    }

    public override bool Equals(object obj)
    {
        var other = obj as EventArgsCache;
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;

        // We cache hash codes, so this is cheap
        if (this.GetHashCode() != other.GetHashCode())
            return false;

        if (this.propertyNameToCacheName.Count != other.propertyNameToCacheName.Count)
            return false;

        foreach (var (a, b) in this.propertyNameToCacheName.Zip(other.propertyNameToCacheName, (a, b) => (a, b)))
        {
            if (a.Key != b.Key || a.Value != b.Value)
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        if (this.hashCode == null)
        {
            int hash = 17;
            foreach (var kvp in this.propertyNameToCacheName)
            {
                unchecked
                {
                    hash = hash * 23 + kvp.Key.GetHashCode();
                    hash = hash * 23 + kvp.Value.GetHashCode();
                }
            }

            this.hashCode = hash;
        }

        return this.hashCode.Value;
    }
}

