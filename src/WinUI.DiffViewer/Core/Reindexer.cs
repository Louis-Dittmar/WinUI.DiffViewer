// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/util/diff/Reindexer.kt
// platform/util/diff/src/com/intellij/util/diff/LCSBuilder.kt
//
// Pre-pass of Diff.buildChanges: elements that occur in only one of the two
// sequences can never be matched, so they are discarded before the LCS runs
// and re-inserted (as changed) afterwards. Besides being faster, this is what
// keeps repeated blocks aligned sanely — the LCS then operates on the shared
// elements only.

using System;
using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

/// <summary>Receives the reconstructed edit script (port of LCSBuilder).</summary>
internal interface ILcsBuilder
{
    void AddEqual(int length);
    void AddChange(int first, int second);
}

internal sealed class Reindexer
{
    private readonly int[]?[] _oldIndices = new int[]?[2];
    private readonly int[] _originalLengths = { -1, -1 };
    private readonly int[] _discardedLengths = { -1, -1 };

    public int[][] DiscardUnique(int[] ints1, int[] ints2)
    {
        var discarded1 = Discard(ints2, ints1, 0);
        return new[] { discarded1, Discard(discarded1, ints2, 1) };
    }

    private int[] Discard(int[] needed, int[] toDiscard, int arrayIndex)
    {
        _originalLengths[arrayIndex] = toDiscard.Length;
        var sorted1 = CreateSorted(needed);
        var discarded = new List<int>(toDiscard.Length);
        var oldIndices = new List<int>(toDiscard.Length);
        for (int i = 0; i < toDiscard.Length; i++)
        {
            int index = toDiscard[i];
            if (Array.BinarySearch(sorted1, index) >= 0)
            {
                discarded.Add(index);
                oldIndices.Add(i);
            }
        }
        _oldIndices[arrayIndex] = oldIndices.ToArray();
        _discardedLengths[arrayIndex] = discarded.Count;
        return discarded.ToArray();
    }

    public void Reindex(DiffBitSet[] discardedChanges, ILcsBuilder builder)
    {
        DiffBitSet changes1;
        DiffBitSet changes2;

        if (_discardedLengths[0] == _originalLengths[0] && _discardedLengths[1] == _originalLengths[1])
        {
            changes1 = discardedChanges[0];
            changes2 = discardedChanges[1];
        }
        else
        {
            changes1 = new DiffBitSet(_originalLengths[0]);
            changes2 = new DiffBitSet(_originalLengths[1]);
            int x = 0;
            int y = 0;
            while (x < _discardedLengths[0] || y < _discardedLengths[1])
            {
                if ((x < _discardedLengths[0] && y < _discardedLengths[1]) && !discardedChanges[0].Get(x) && !discardedChanges[1].Get(y))
                {
                    x = Increment(_oldIndices[0]!, x, changes1, _originalLengths[0]);
                    y = Increment(_oldIndices[1]!, y, changes2, _originalLengths[1]);
                }
                else if (discardedChanges[0].Get(x))
                {
                    changes1.Set(GetOriginal(_oldIndices[0]!, x), true);
                    x = Increment(_oldIndices[0]!, x, changes1, _originalLengths[0]);
                }
                else if (discardedChanges[1].Get(y))
                {
                    changes2.Set(GetOriginal(_oldIndices[1]!, y), true);
                    y = Increment(_oldIndices[1]!, y, changes2, _originalLengths[1]);
                }
            }
            if (_discardedLengths[0] == 0)
            {
                changes1.Set(0, _originalLengths[0]);
            }
            else
            {
                changes1.Set(0, _oldIndices[0]![0]);
            }
            if (_discardedLengths[1] == 0)
            {
                changes2.Set(0, _originalLengths[1]);
            }
            else
            {
                changes2.Set(0, _oldIndices[1]![0]);
            }
        }

        {
            int x = 0;
            int y = 0;
            while (x < _originalLengths[0] && y < _originalLengths[1])
            {
                int startX = x;
                while (x < _originalLengths[0] && y < _originalLengths[1] && !changes1.Get(x) && !changes2.Get(y))
                {
                    x++;
                    y++;
                }
                if (x > startX) builder.AddEqual(x - startX);
                int dx = 0;
                int dy = 0;
                while (x < _originalLengths[0] && changes1.Get(x))
                {
                    dx++;
                    x++;
                }
                while (y < _originalLengths[1] && changes2.Get(y))
                {
                    dy++;
                    y++;
                }
                if (dx != 0 || dy != 0) builder.AddChange(dx, dy);
            }
            if (x != _originalLengths[0] || y != _originalLengths[1]) builder.AddChange(_originalLengths[0] - x, _originalLengths[1] - y);
        }
    }

    private static int[] CreateSorted(int[] ints1)
    {
        var sorted1 = (int[])ints1.Clone();
        Array.Sort(sorted1);
        return sorted1;
    }

    private static int GetOriginal(int[] indexes, int i) => indexes[i];

    private static int Increment(int[] indexes, int i, DiffBitSet set, int length)
    {
        if (i + 1 < indexes.Length)
        {
            set.Set(indexes[i] + 1, indexes[i + 1]);
        }
        else
        {
            set.Set(indexes[i] + 1, length);
        }
        return i + 1;
    }
}
