using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.EventArgs;

public class EventArgsCache
{
    private readonly SortedDictionary<EventArgsCacheKey, string> propertyNameToCacheName;
    private int? hashCode;

    public bool IsEmpty => this.propertyNameToCacheName.Count == 0;

    internal EventArgsCache(SortedDictionary<EventArgsCacheKey, string> propertyNameToCacheName)
    {
        this.propertyNameToCacheName = propertyNameToCacheName;
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

