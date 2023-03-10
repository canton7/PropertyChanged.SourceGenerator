using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace PropertyChanged.SourceGenerator;

// Adapted from https://github.com/CommunityToolkit/dotnet/blob/63ba418b6fc86c9c9098ad119bd5e1d82e82ea4b/src/CommunityToolkit.Mvvm.SourceGenerators/Helpers/EquatableArray%7BT%7D.cs

public static class EquatableArray
{
    public static EquatableArray<T> AsEquatableArray<T>(this ImmutableArray<T> array) where T : IEquatable<T> => new(array);
}

public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[]? array;

    public static EquatableArray<T> Empty { get; } = new(ImmutableArray<T>.Empty);

    public EquatableArray(ImmutableArray<T> array)
    {
        this.array = Unsafe.As<ImmutableArray<T>, T[]?>(ref array);
    }

    public bool Equals(EquatableArray<T> array)
    {
        return this.AsSpan().SequenceEqual(array.AsSpan());
    }

    public override bool Equals(object obj) => obj is EquatableArray<T> other && this.Equals(other);

    public override int GetHashCode()
    {
        if (this.array == null)
        {
            return 0;
        }

        int hashCode = -658986029;

        foreach (var item in this.array)
        {
            hashCode = hashCode * -1521134295 + item.GetHashCode();
        }

        return hashCode;
    }

    /// <returns>The <see cref="ImmutableArray{T}"/> from the current <see cref="EquatableArray{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArray<T> AsImmutableArray()
    {
        return Unsafe.As<T[]?, ImmutableArray<T>>(ref Unsafe.AsRef(in this.array));
    }

    public ReadOnlySpan<T> AsSpan()
    {
        return this.AsImmutableArray().AsSpan();
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => this.AsImmutableArray().GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)this.AsImmutableArray()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.AsImmutableArray()).GetEnumerator();
}