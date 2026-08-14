// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/ChangeCorrector.kt
//
// Base class for two-step diff algorithms: given a matching between
// sub-sequences of the base sequences, build a matching on the whole
// sequences by diffing the gaps between the matched anchor elements.
// Only the line flavor (SmartLineChangeCorrector) is ported — the char
// flavor belongs to ByCharRt, which this library does not use.

using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

internal abstract class ChangeCorrector
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly List<DiffRange> _changes;   // diff over the sub-sequences
    private readonly int _changesLength1;        // sub-sequence lengths the
    private readonly int _changesLength2;        // change list refers to

    protected readonly ChangeBuilder Builder;

    protected ChangeCorrector(int length1, int length2, List<DiffRange> changes, int changesLength1, int changesLength2)
    {
        _length1 = length1;
        _length2 = length2;
        _changes = changes;
        _changesLength1 = changesLength1;
        _changesLength2 = changesLength2;
        Builder = new ChangeBuilder(length1, length2);
    }

    public List<DiffRange> Build()
    {
        Execute();
        return Builder.Finish();
    }

    private void Execute()
    {
        int last1 = 0;
        int last2 = 0;

        foreach (var ch in DiffAlgorithm.IterateUnchanged(_changes, _changesLength1, _changesLength2))
        {
            int count = ch.End1 - ch.Start1;
            for (int i = 0; i < count; i++)
            {
                var (start1, end1) = GetOriginalRange1(ch.Start1 + i);
                var (start2, end2) = GetOriginalRange2(ch.Start2 + i);

                MatchGap(last1, start1, last2, start2);
                Builder.MarkEqual(start1, start2, end1, end2);

                last1 = end1;
                last2 = end2;
            }
        }
        MatchGap(last1, _length1, last2, _length2);
    }

    /// <summary>Matches elements in the gap [start1, end1) x [start2, end2).</summary>
    protected abstract void MatchGap(int start1, int end1, int start2, int end2);

    protected abstract (int start, int end) GetOriginalRange1(int index);

    protected abstract (int start, int end) GetOriginalRange2(int index);
}

/// <summary>
/// Second pass of ByLineRt.compareSmart: the first pass diffed only the "big"
/// (important) lines; this correction re-diffs each gap between the matched
/// big lines with all lines included.
/// </summary>
internal sealed class SmartLineChangeCorrector : ChangeCorrector
{
    private readonly List<int> _indexes1;
    private readonly List<int> _indexes2;
    private readonly List<DiffLine> _lines1;
    private readonly List<DiffLine> _lines2;

    public SmartLineChangeCorrector(
        List<int> indexes1, List<int> indexes2,
        List<DiffLine> lines1, List<DiffLine> lines2,
        List<DiffRange> changes)
        : base(lines1.Count, lines2.Count, changes, indexes1.Count, indexes2.Count)
    {
        _indexes1 = indexes1;
        _indexes2 = indexes2;
        _lines1 = lines1;
        _lines2 = lines2;
    }

    protected override void MatchGap(int start1, int end1, int start2, int end2)
    {
        var expand = DiffTrimUtil.Expand(_lines1, _lines2, start1, start2, end1, end2);

        var inner1 = _lines1.GetRange(expand.Start1, expand.End1 - expand.Start1);
        var inner2 = _lines2.GetRange(expand.Start2, expand.End2 - expand.Start2);
        var innerChanges = DiffAlgorithm.Diff(inner1, inner2);

        Builder.MarkEqual(start1, start2, expand.Start1, expand.Start2);

        foreach (var chunk in DiffAlgorithm.IterateUnchanged(innerChanges, inner1.Count, inner2.Count))
        {
            Builder.MarkEqual(expand.Start1 + chunk.Start1, expand.Start2 + chunk.Start2, chunk.End1 - chunk.Start1);
        }

        Builder.MarkEqual(expand.End1, expand.End2, end1, end2);
    }

    protected override (int start, int end) GetOriginalRange1(int index)
    {
        int offset = _indexes1[index];
        return (offset, offset + 1);
    }

    protected override (int start, int end) GetOriginalRange2(int index)
    {
        int offset = _indexes2[index];
        return (offset, offset + 1);
    }
}
