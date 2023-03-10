using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PropertyChanged.SourceGenerator.EventArgs;

public class EventArgsCacheBuilder
{
    // propertyName: The string passed to PropertyChangedEventArgs being cached
    // cacheName: The name of the generated property on the EventArgs cache

    // Collection of all property names we need to add to the cache
    // We use a sorted dictionary to make equality comparisons easier
    private readonly SortedDictionary<EventArgsCacheKey, string> propertyNameToCacheName = new();
    private readonly HashSet<string> cacheNames = new(StringComparer.Ordinal);

    // Collection of just the property names which collided with something else, so we needed to adjust them
    private SortedDictionary<EventArgsCacheKey, string>? collidedPropertyNameToCacheNames;

    public void Add(string? propertyName, string eventName, string eventArgsFullyQualifiedTypeName)
    {
        var key = new EventArgsCacheKey(propertyName, eventArgsFullyQualifiedTypeName);
        if (this.propertyNameToCacheName.ContainsKey(key))
        {
            return;
        }

        string safeName = EventArgsCacheLookup.Transform(propertyName, eventName);

        string cacheName = safeName;
        for (int i = 1; this.cacheNames.Contains(cacheName); i++)
        {
            cacheName = safeName + i;
        }

        if (cacheName != safeName)
        {
            this.collidedPropertyNameToCacheNames ??= new();
            this.collidedPropertyNameToCacheNames.Add(key, cacheName);
        }

        this.propertyNameToCacheName.Add(key, cacheName);
        this.cacheNames.Add(cacheName);
    }

    public (EventArgsCache cache, EventArgsCacheLookup lookup) ToCacheAndLookup() => (new(this.propertyNameToCacheName), new(this.collidedPropertyNameToCacheNames));
}

