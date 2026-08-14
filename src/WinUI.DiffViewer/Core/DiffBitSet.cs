// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/util/diff/BitSet.kt (minimal subset)

using System;

namespace WinUI.DiffViewer.Core;

/// <summary>Minimal fixed-size bit set used by <see cref="MyersLcs"/>.</summary>
public sealed class DiffBitSet
{
    private readonly ulong[] _words;

    public int Length { get; }

    public DiffBitSet(int length)
    {
        Length = length;
        _words = new ulong[(length + 63) >> 6];
    }

    public bool Get(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        // Like java.util.BitSet, reads past the logical length yield false
        // (the Reindexer probes one past the end of an exhausted side).
        if (index >= Length) return false;
        return (_words[index >> 6] & (1UL << (index & 63))) != 0;
    }

    public void Set(int index, bool value)
    {
        if ((uint)index >= (uint)Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (value) _words[index >> 6] |= 1UL << (index & 63);
        else _words[index >> 6] &= ~(1UL << (index & 63));
    }

    public void Set(int fromInclusive, int toExclusive) => Set(fromInclusive, toExclusive, true);

    public void Set(int fromInclusive, int toExclusive, bool value)
    {
        if (fromInclusive < 0 || toExclusive > Length || fromInclusive > toExclusive)
            throw new ArgumentOutOfRangeException(nameof(fromInclusive));
        for (int i = fromInclusive; i < toExclusive; i++)
        {
            if (value) _words[i >> 6] |= 1UL << (i & 63);
            else _words[i >> 6] &= ~(1UL << (i & 63));
        }
    }
}
