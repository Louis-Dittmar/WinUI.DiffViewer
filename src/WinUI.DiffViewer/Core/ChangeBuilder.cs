// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/iterables/DiffIterableUtil.kt
//   (ChangeBuilderBase, ChangeBuilder, ExpandChangeBuilder)
// platform/util/diff/src/com/intellij/diff/comparison/TrimUtil.kt (expand for lists)
//
// Instead of the original's DiffIterable, builders collect changed ranges into
// a plain List&lt;DiffRange&gt; — the same representation DiffAlgorithm.Diff returns.

using System;
using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

/// <summary>
/// Builds a change list from a stream of "these elements match" calls.
/// Everything between two matched regions becomes a changed range.
/// </summary>
internal class ChangeBuilder
{
    private readonly List<DiffRange> _changes = new();

    public int Length1 { get; }
    public int Length2 { get; }
    public int Index1 { get; private set; }
    public int Index2 { get; private set; }

    public ChangeBuilder(int length1, int length2)
    {
        Length1 = length1;
        Length2 = length2;
    }

    public void MarkEqual(int index1, int index2) => MarkEqual(index1, index2, 1);

    public void MarkEqual(int index1, int index2, int count) => MarkEqual(index1, index2, index1 + count, index2 + count);

    public void MarkEqual(int index1, int index2, int end1, int end2)
    {
        if (index1 == end1 && index2 == end2) return;

        if (Index1 > index1 || Index2 > index2 || index1 > end1 || index2 > end2)
        {
            throw new InvalidOperationException($"Equal ranges must be non-overlapping and ordered: at ({Index1}, {Index2}), got [{index1}, {end1}) - [{index2}, {end2})");
        }

        if (Index1 != index1 || Index2 != index2)
        {
            AddChange(Index1, Index2, index1, index2);
        }
        Index1 = end1;
        Index2 = end2;
    }

    protected virtual void AddChange(int start1, int start2, int end1, int end2)
    {
        _changes.Add(new DiffRange(start1, end1, start2, end2));
    }

    public List<DiffRange> Finish()
    {
        if (Length1 != Index1 || Length2 != Index2)
        {
            AddChange(Index1, Index2, Length1, Length2);
            Index1 = Length1;
            Index2 = Length2;
        }
        return _changes;
    }
}

/// <summary>
/// A ChangeBuilder that trims pairwise-equal elements off both ends of every
/// changed range before recording it (port of ExpandChangeBuilder).
/// </summary>
internal sealed class ExpandChangeBuilder : ChangeBuilder
{
    private readonly IReadOnlyList<DiffLine> _objects1;
    private readonly IReadOnlyList<DiffLine> _objects2;

    public ExpandChangeBuilder(IReadOnlyList<DiffLine> objects1, IReadOnlyList<DiffLine> objects2)
        : base(objects1.Count, objects2.Count)
    {
        _objects1 = objects1;
        _objects2 = objects2;
    }

    protected override void AddChange(int start1, int start2, int end1, int end2)
    {
        var range = DiffTrimUtil.Expand(_objects1, _objects2, start1, start2, end1, end2);
        if (!range.IsEmpty) base.AddChange(range.Start1, range.Start2, range.End1, range.End2);
    }
}

internal static class DiffTrimUtil
{
    /// <summary>
    /// Shrinks the changed range [start1, end1) x [start2, end2) by walking
    /// pairwise-equal elements inward from both ends (port of TrimUtil.expand).
    /// </summary>
    public static DiffRange Expand<T>(IReadOnlyList<T> list1, IReadOnlyList<T> list2, int start1, int start2, int end1, int end2)
        where T : notnull
    {
        while (start1 < end1 && start2 < end2 && list1[start1].Equals(list2[start2]))
        {
            start1++;
            start2++;
        }
        while (end1 > start1 && end2 > start2 && list1[end1 - 1].Equals(list2[end2 - 1]))
        {
            end1--;
            end2--;
        }
        return new DiffRange(start1, end1, start2, end2);
    }
}
