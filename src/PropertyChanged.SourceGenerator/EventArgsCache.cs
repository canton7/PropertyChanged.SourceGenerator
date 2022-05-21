using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator;

public class EventArgsCache
{
    // propertyName: The string passed to PropertyChangedEventArgs being cached
    // cacheName: The name of the generated property on the EventArgs cache

    private static readonly Regex invalidCharsRegex = new(@"(^[^a-zA-Z])|([^a-zA-Z0-9])");
    private readonly Dictionary<Key, string> propertyNameToCacheName = new();
    private readonly HashSet<string> cacheNames = new(StringComparer.Ordinal);

    public bool IsEmpty => this.cacheNames.Count == 0;

    public string GetOrAdd(string? propertyName, INamedTypeSymbol eventArgsTypeSymbol)
    {
        var key = new Key(propertyName, eventArgsTypeSymbol);
        if (this.propertyNameToCacheName.TryGetValue(key, out string cacheName))
        {
            return cacheName;
        }

        string typeName = eventArgsTypeSymbol.Name;
        if (typeName.EndsWith("EventArgs"))
        {
            typeName = typeName.Substring(0, typeName.Length - "EventArgs".Length);
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

        cacheName = safeName;
        for (int i = 1; this.cacheNames.Contains(cacheName); i++)
        {
            cacheName = safeName + i;
        }

        this.propertyNameToCacheName.Add(key, cacheName);
        this.cacheNames.Add(cacheName);
        return cacheName;
    }

    public IEnumerable<(string cacheName, INamedTypeSymbol eventArgsType, string? propertyName)> GetEntries()
    {
        foreach (var kvp in this.propertyNameToCacheName)
        {
            yield return (kvp.Value, kvp.Key.EventArgsTypeSymbol, kvp.Key.PropertyName);
        }
    }

    public bool ContainsCacheName(string cacheName)
    {
        return this.cacheNames.Contains(cacheName);
    }

    private struct Key : IEquatable<Key>
    {
        public string? PropertyName { get; }
        public INamedTypeSymbol EventArgsTypeSymbol { get; }

        public Key(string? propertyName, INamedTypeSymbol eventArgsTypeSymbol)
        {
            this.PropertyName = propertyName;
            this.EventArgsTypeSymbol = eventArgsTypeSymbol;
        }

        public bool Equals(Key other)
        {
            return string.Equals(this.PropertyName, other.PropertyName, StringComparison.Ordinal) &&
                SymbolEqualityComparer.Default.Equals(this.EventArgsTypeSymbol, other.EventArgsTypeSymbol);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (this.PropertyName == null ? 0 : StringComparer.Ordinal.GetHashCode(this.PropertyName));
                hash = hash * 23 + SymbolEqualityComparer.Default.GetHashCode(this.EventArgsTypeSymbol);
                return hash;
            }
        }

        public override bool Equals(object? obj) => obj is Key other && this.Equals(other);
        
        public static bool operator ==(Key left, Key right) => left.Equals(right);
        public static bool operator !=(Key left, Key right) => !left.Equals(right);
    }
}
