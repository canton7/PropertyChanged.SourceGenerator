using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PropertyChanged.SourceGenerator.EventArgs;

public struct EventArgsCacheLookup : IEquatable<EventArgsCacheLookup>
{
    private static readonly Regex invalidCharsRegex = new(@"(^[^a-zA-Z])|([^a-zA-Z0-9])");


    private readonly SortedDictionary<EventArgsCacheKey, string>? collidedPropertyNameToCacheNames;

    internal EventArgsCacheLookup(SortedDictionary<EventArgsCacheKey, string>? collidedPropertyNameToCacheNames)
    {
        this.collidedPropertyNameToCacheNames = collidedPropertyNameToCacheNames;
    }

    public string Get(string? propertyName, string eventName, string eventArgsFullyQualifiedTypeName)
    {
        if (this.collidedPropertyNameToCacheNames != null &&
            this.collidedPropertyNameToCacheNames.TryGetValue(new EventArgsCacheKey(propertyName, eventArgsFullyQualifiedTypeName), out string cacheName))
        {
            return cacheName;
        }

        // Else, there were no collisions when using the default transformation
        return Transform(propertyName, eventName);
    }

    internal static string Transform(string? propertyName, string eventName)
    {
        return $"{eventName}_" + propertyName switch
        {
            null => "Null",
            "" => "Empty",
            "Item[]" => "Item",
            // Reserve the names 'Null' and 'Empty' for actual null/empty strings, and 'Item' for indexers
            "Null" => "Null1",
            "Empty" => "Empty1",
            "Item" => "Item1",
            _ => invalidCharsRegex.Replace(propertyName, "_"),
        };
    }

    public bool Equals(EventArgsCacheLookup other)
    {
        if (ReferenceEquals(this.collidedPropertyNameToCacheNames, other.collidedPropertyNameToCacheNames))
            return true;
        if ((this.collidedPropertyNameToCacheNames == null) != (other.collidedPropertyNameToCacheNames == null))
            return false;

        // If both or either are null, we'd have returned by now
        if (this.collidedPropertyNameToCacheNames!.Count != other.collidedPropertyNameToCacheNames!.Count)
            return false;

        foreach (var (a, b) in this.collidedPropertyNameToCacheNames.Zip(other.collidedPropertyNameToCacheNames, (a, b) => (a, b)))
        {
            if (a.Key != b.Key || a.Value != b.Value)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EventArgsCacheLookup other && this.Equals(other);

    public override int GetHashCode()
    {
        if (this.collidedPropertyNameToCacheNames == null)
            return 0;

        int hash = 17;
        foreach (var kvp in this.collidedPropertyNameToCacheNames)
        {
            unchecked
            {
                hash = hash * 23 + kvp.Key.GetHashCode();
                hash = hash * 23 + kvp.Value.GetHashCode();
            }
        }
        return hash;
    }
}
