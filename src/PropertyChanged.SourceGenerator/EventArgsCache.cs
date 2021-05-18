using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PropertyChanged.SourceGenerator
{
    public class EventArgsCache
    {
        // propertyName: The string passed to PropertyChangedEventArgs being cached
        // cacheName: The name of the generated property on the EventArgs cache

        private static readonly Regex invalidCharsRegex = new(@"(^[^a-zA-Z])|([^a-zA-Z0-9])");
        private readonly Dictionary<Key, string> propertyNameToCacheName = new();
        private readonly HashSet<string> cacheNames = new(StringComparer.Ordinal);

        public string GetOrAdd(string? propertyName)
        {
            if (this.propertyNameToCacheName.TryGetValue(propertyName, out string cacheName))
            {
                return cacheName;
            }

            string safeName = propertyName switch
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

            this.propertyNameToCacheName.Add(propertyName, cacheName);
            this.cacheNames.Add(cacheName);
            return cacheName;
        }

        public IEnumerable<(string cacheName, string? propertyName)> GetEntries()
        {
            foreach (var kvp in this.propertyNameToCacheName)
            {
                yield return (kvp.Value, kvp.Key);
            }
        }

        public bool ContainsCacheName(string cacheName)
        {
            return this.cacheNames.Contains(cacheName);
        }

        private struct Key : IEquatable<Key>
        {
            private readonly string? key;
            public Key(string? key) => this.key = key;

            public bool Equals(Key other) => string.Equals(this.key, other.key, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is Key other && this.Equals(other);
            public override int GetHashCode() => this.key?.GetHashCode() ?? 0;

            public static implicit operator Key(string? key) => new(key);
            public static implicit operator string?(Key key) => key.key;
        }
    }
}
