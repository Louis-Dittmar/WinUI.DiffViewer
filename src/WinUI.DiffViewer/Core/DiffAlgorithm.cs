// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/util/diff/Diff.kt (full pipeline)
// platform/util/diff/src/com/intellij/diff/comparison/iterables/DiffIterableUtil.kt (range building)
//
// Complete doBuildChanges cascade: prefix/suffix trim, enumeration,
// Reindexer.discardUnique, bounded Myers (executeWithThreshold) with the
// PatienceIntLCS fallback, and Reindexer.reindex to restore the discarded
// elements. Deliberate deviation: when even the patience fallback exceeds its
// reduction threshold (IntelliJ throws FilesTooBigForDiffException and shows a
// "too big" notice), we rerun patience without the check and return the
// approximate result — an interactive editor control must not fail the diff.

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

        var collector = new RangeCollector(prefix, result);
        DoBuildChanges(ints1, ints2, collector);

        NormalizeMatchPositions(result, objects1, objects2, comparer);
        return result;
    }

    /// <summary>
    /// Deliberate addition over IntelliJ's pipeline: when the same content
    /// occurs several times, the (bidirectional, linear-space) Myers run is
    /// free to match ANY of the equivalent occurrences and tends to pick one
    /// in the middle. For "block was modified and duplicated N times" inputs
    /// that turns the duplicates into one giant modification instead of an
    /// insertion. This pass slides every matched run to the leftmost position
    /// that yields the same pairing (the convention git follows), which keeps
    /// the head compact and pushes duplicated tails out as insertions. The
    /// matched-pair count is unchanged, so the result is still a fair LCS.
    /// </summary>
    private static void NormalizeMatchPositions<T>(
        List<DiffRange> changes,
        IReadOnlyList<T> objects1,
        IReadOnlyList<T> objects2,
        IEqualityComparer<T> comparer)
        where T : notnull
    {
        for (int i = 0; i < changes.Count; i++)
        {
            var prev = changes[i];

            // The matched run between this change and the next one (or the
            // trailing matched run after the last change).
            int gapStart1 = prev.End1;
            int gapStart2 = prev.End2;
            int gapEnd1 = i + 1 < changes.Count ? changes[i + 1].Start1 : objects1.Count;
            int gapEnd2 = i + 1 < changes.Count ? changes[i + 1].Start2 : objects2.Count;
            int n = gapEnd1 - gapStart1;
            if (n <= 0 || n != gapEnd2 - gapStart2) continue;

            // Slide the run's side-2 window as far left as possible.
            int target2 = FindLeftmostWindow(objects2, prev.Start2, gapStart2, objects1, gapStart1, n, comparer);
            // Slide the run's side-1 window as far left as possible.
            int target1 = FindLeftmostWindow(objects1, prev.Start1, gapStart1, objects2, gapStart2, n, comparer);

            if (target1 == gapStart1 && target2 == gapStart2) continue;

            changes[i] = new DiffRange(prev.Start1, target1, prev.Start2, target2);

            int newGapEnd1 = target1 + n;
            int newGapEnd2 = target2 + n;
            if (i + 1 < changes.Count)
            {
                var next = changes[i + 1];
                changes[i + 1] = new DiffRange(newGapEnd1, next.End1, newGapEnd2, next.End2);
            }
            else if (newGapEnd1 < objects1.Count || newGapEnd2 < objects2.Count)
            {
                changes.Add(new DiffRange(newGapEnd1, objects1.Count, newGapEnd2, objects2.Count));
            }

            if (changes[i].IsEmpty)
            {
                changes.RemoveAt(i);
                i--; // the grown successor may now slide further
            }
        }
    }

    /// <summary>
    /// Finds the leftmost start position in [lowerBound, current] where
    /// <paramref name="side"/> contains the same run that <paramref name="other"/>
    /// holds at [otherStart, otherStart + n). Returns current when the run
    /// cannot move.
    /// </summary>
    private static int FindLeftmostWindow<T>(
        IReadOnlyList<T> side, int lowerBound, int current,
        IReadOnlyList<T> other, int otherStart, int n,
        IEqualityComparer<T> comparer)
        where T : notnull
    {
        for (int p = lowerBound; p < current; p++)
        {
            bool matches = true;
            for (int k = 0; k < n; k++)
            {
                if (!comparer.Equals(side[p + k], other[otherStart + k]))
                {
                    matches = false;
                    break;
                }
            }
            if (matches) return p;
        }
        return current;
    }

    // Port of Diff.doBuildChanges.
    private static void DoBuildChanges(int[] ints1, int[] ints2, RangeCollector builder)
    {
        var reindexer = new Reindexer(); // discard unique elements, that have no chance to be matched
        int[][] discarded = reindexer.DiscardUnique(ints1, ints2);

        if (discarded[0].Length == 0 && discarded[1].Length == 0)
        {
            builder.AddChange(ints1.Length, ints2.Length);
            return;
        }

        DiffBitSet[] changes;
#pragma warning disable CS0162 // UsePatienceAlg is a compile-time constant (as in DiffConfig.kt)
        if (DiffConfig.UsePatienceAlg)
        {
            var patienceIntLcs = new PatienceIntLcs(discarded[0], discarded[1]);
            patienceIntLcs.Execute();
            changes = patienceIntLcs.Changes;
        }
        else
        {
            try
            {
                var intLcs = new MyersLcs(discarded[0], discarded[1]);
                intLcs.ExecuteWithThreshold();
                changes = new[] { intLcs.Changes1, intLcs.Changes2 };
            }
            catch (DiffTooBigException)
            {
                try
                {
                    var patienceIntLcs = new PatienceIntLcs(discarded[0], discarded[1]);
                    patienceIntLcs.Execute(failOnSmallReduction: true);
                    changes = patienceIntLcs.Changes;
                }
                catch (DiffTooBigException)
                {
                    // Deviation from IntelliJ (which surfaces "diff too big"):
                    // accept the approximate patience result instead of failing.
                    var patienceIntLcs = new PatienceIntLcs(discarded[0], discarded[1]);
                    patienceIntLcs.Execute(failOnSmallReduction: false);
                    changes = patienceIntLcs.Changes;
                }
            }
        }

#pragma warning restore CS0162
        reindexer.Reindex(changes, builder);
    }

    /// <summary>
    /// Collects the edit script as DiffRanges, shifted by the trimmed common
    /// prefix (port of Diff.ChangeBuilder).
    /// </summary>
    private sealed class RangeCollector : ILcsBuilder
    {
        private readonly List<DiffRange> _ranges;
        private int _index1;
        private int _index2;

        public RangeCollector(int startShift, List<DiffRange> ranges)
        {
            _ranges = ranges;
            _index1 = startShift;
            _index2 = startShift;
        }

        public void AddEqual(int length)
        {
            _index1 += length;
            _index2 += length;
        }

        public void AddChange(int first, int second)
        {
            _ranges.Add(new DiffRange(_index1, _index1 + first, _index2, _index2 + second));
            _index1 += first;
            _index2 += second;
        }
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
