// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/util/diff/Diff.kt (pipeline)
// platform/util/diff/src/com/intellij/diff/comparison/iterables/DiffIterableUtil.kt (range building)
//
// Differences from the original: the "discard unique elements" preprocessing
// (Reindexer) and the Patience fallback are omitted — MyersLcs.ExecuteLinear
// never throws, so no fallback is needed. Results are identical, large inputs
// may take slightly longer.

using System;
using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

public static class DiffAlgorithm
{
    /// <summary>
    /// Computes the changed ranges between two sequences. The result is "fair":
    /// unchanged regions pair elements one-to-one.
    /// </summary>
    public static List<DiffRange> Diff<T>(IReadOnlyList<T> objects1, IReadOnlyList<T> objects2)
        where T : notnull
    {
        return Diff(objects1, objects2, EqualityComparer<T>.Default);
    }

    public static List<DiffRange> Diff<T>(
        IReadOnlyList<T> objects1,
        IReadOnlyList<T> objects2,
        IEqualityComparer<T> comparer)
        where T : notnull
    {
        int n1 = objects1.Count;
        int n2 = objects2.Count;

        // Cut off common prefix (port of Diff.getStartShift).
        int prefix = 0;
        while (prefix < n1 && prefix < n2 && comparer.Equals(objects1[prefix], objects2[prefix]))
        {
            prefix++;
        }

        // Cut off common suffix (port of Diff.getEndCut).
        int suffix = 0;
        while (suffix < n1 - prefix && suffix < n2 - prefix &&
               comparer.Equals(objects1[n1 - 1 - suffix], objects2[n2 - 1 - suffix]))
        {
            suffix++;
        }

        int count1 = n1 - prefix - suffix;
        int count2 = n2 - prefix - suffix;

        var result = new List<DiffRange>();

        // Trivial cases (port of Diff.doBuildChangesFast).
        if (count1 == 0 && count2 == 0) return result;
        if (count1 == 0 || count2 == 0)
        {
            result.Add(new DiffRange(prefix, prefix + count1, prefix, prefix + count2));
            return result;
        }

        // Enumerate objects to ints (port of Enumerator).
        var enumerator = new Dictionary<T, int>(comparer);
        var ints1 = new int[count1];
        var ints2 = new int[count2];
        for (int i = 0; i < count1; i++)
        {
            ints1[i] = GetOrAssign(enumerator, objects1[prefix + i]);
        }
        for (int i = 0; i < count2; i++)
        {
            ints2[i] = GetOrAssign(enumerator, objects2[prefix + i]);
        }

        var lcs = new MyersLcs(ints1, ints2);
        lcs.ExecuteLinear();

        // Reconstruct changed ranges from the two "changed" bit sets.
        // The matching is one-to-one and order-preserving, so unchanged
        // positions can be consumed pairwise.
        var changes1 = lcs.Changes1;
        var changes2 = lcs.Changes2;
        int x = 0, y = 0;
        while (x < count1 || y < count2)
        {
            bool changedX = x < count1 && changes1.Get(x);
            bool changedY = y < count2 && changes2.Get(y);
            if (changedX || changedY)
            {
                int startX = x, startY = y;
                while (x < count1 && changes1.Get(x)) x++;
                while (y < count2 && changes2.Get(y)) y++;
                result.Add(new DiffRange(prefix + startX, prefix + x, prefix + startY, prefix + y));
            }
            else
            {
                x++;
                y++;
            }
        }

        return result;
    }

    private static int GetOrAssign<T>(Dictionary<T, int> enumerator, T value) where T : notnull
    {
        if (!enumerator.TryGetValue(value, out int id))
        {
            id = enumerator.Count;
            enumerator.Add(value, id);
        }
        return id;
    }

    /// <summary>Inverts a change list into the list of unchanged (matched) ranges.</summary>
    public static List<DiffRange> IterateUnchanged(List<DiffRange> changes, int length1, int length2)
    {
        var result = new List<DiffRange>();
        int last1 = 0, last2 = 0;
        foreach (var change in changes)
        {
            if (change.Start1 > last1 || change.Start2 > last2)
            {
                result.Add(new DiffRange(last1, change.Start1, last2, change.Start2));
            }
            last1 = change.End1;
            last2 = change.End2;
        }
        if (length1 > last1 || length2 > last2)
        {
            result.Add(new DiffRange(last1, length1, last2, length2));
        }
        return result;
    }
}
