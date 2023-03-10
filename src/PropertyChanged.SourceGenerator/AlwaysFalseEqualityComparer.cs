using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace PropertyChanged.SourceGenerator;

public class AlwaysFalseEqualityComparer<T> : IEqualityComparer<T>
{
    public static AlwaysFalseEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T x, T y) => false;
    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
