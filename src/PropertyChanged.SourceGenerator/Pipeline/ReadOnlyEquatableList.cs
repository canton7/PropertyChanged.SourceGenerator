using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PropertyChanged.SourceGenerator.Pipeline;

public  class ReadOnlyEquatableList<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> inner;
    private readonly IEqualityComparer<T> comparer;

    public ReadOnlyEquatableList(IReadOnlyList<T> inner, IEqualityComparer<T>? comparer = null)
    {
        this.inner = inner;
        this.comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public override bool Equals(object obj)
    {
        var other = obj as ReadOnlyEquatableList<T>;
        if (this == other)
            return true;
        if (other == null)
            return false;
        return this.inner.SequenceEqual(other.inner, this.comparer);
    }

    public override int GetHashCode()
    {
        int hashCode = -658986029;

        foreach (var item in this.inner)
        {
            hashCode = hashCode * -1521134295 + this.comparer.GetHashCode(item);
        }
        
        return hashCode;
    }

    public T this[int index] => this.inner[index];

    public int Count => this.inner.Count;

    public IEnumerator<T> GetEnumerator() => this.inner.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.inner).GetEnumerator();
}
