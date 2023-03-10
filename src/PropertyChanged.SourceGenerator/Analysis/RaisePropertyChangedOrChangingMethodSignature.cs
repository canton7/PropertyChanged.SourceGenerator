using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public enum RaisePropertyChangedOrChangingNameType
{
    // The order here is used by RaisePropertyChangedOrChangingMethodSignature.CompareTo!
    String = 0,
    PropertyChangedEventArgs = 1,
}

public record struct RaisePropertyChangedOrChangingMethodSignature(
    RaisePropertyChangedOrChangingNameType NameType,
    bool HasOld,
    bool HasNew,
    Accessibility Accessibility)
{
}

// This is a separate class because the comparison doesn't strictly align with RaisePropertyChangedOrChangingMethodSignature's Equals implementation
public class RaisePropertyChangedOrChangingMethodSignatureBetternessComparer : IComparer<RaisePropertyChangedOrChangingMethodSignature?>
{
    public static RaisePropertyChangedOrChangingMethodSignatureBetternessComparer Instance { get; } = new();

    public int Compare(RaisePropertyChangedOrChangingMethodSignature? left, RaisePropertyChangedOrChangingMethodSignature? right)
    {
        // Not-null is always better than null
        if (left == null)
            return right == null ? 0 : -1;
        if (right == null)
            return 1;

        var (x, y) = (left.Value, right.Value);

        // Methods which take PropertyChangedEventArgs are always better than those which don't
        int result = Comparer<RaisePropertyChangedOrChangingNameType>.Default.Compare(x.NameType, y.NameType);
        if (result != 0)
        {
            return result;
        }

        // Methods which take old and/or new are better than those that don't
        result = (x.HasOld | x.HasNew).CompareTo(y.HasOld | y.HasNew);
        if (result != 0)
        {
            return result;
        }

        // We don't care about accessibility when testing equality
        return 0;
    }
}
