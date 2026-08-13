// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/ChunkOptimizer.kt
//
// Post-Myers boundary correction: where a change is ambiguous (the same
// element run could be marked at either end), the boundary is shifted to the
// "nicer" position — lines snap to empty/unimportant lines, words snap to
// whitespace-separated sentence boundaries. Without this, deletions tend to
// get glued onto neighboring modifications.

using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

internal abstract class ChunkOptimizer<T>
{
    protected readonly IReadOnlyList<T> Data1;
    protected readonly IReadOnlyList<T> Data2;
    private readonly IEqualityComparer<T> _comparer;
    private readonly List<DiffRange> _ranges = new();

    protected ChunkOptimizer(IReadOnlyList<T> data1, IReadOnlyList<T> data2, IEqualityComparer<T> comparer)
    {
        Data1 = data1;
        Data2 = data2;
        _comparer = comparer;
    }

    /// <summary>Takes the changed ranges of a fair diff and returns corrected changed ranges.</summary>
    public List<DiffRange> Build(List<DiffRange> changes)
    {
        foreach (var range in DiffAlgorithm.IterateUnchanged(changes, Data1.Count, Data2.Count))
        {
            _ranges.Add(range);
            ProcessLastRanges();
        }

        // Invert the corrected unchanged ranges back into changed ranges.
        return DiffAlgorithm.IterateUnchanged(_ranges, Data1.Count, Data2.Count);
    }

    private void ProcessLastRanges()
    {
        if (_ranges.Count < 2) return; // nothing to do

        var range1 = _ranges[^2];
        var range2 = _ranges[^1];
        if (range1.End1 != range2.Start1 && range1.End2 != range2.Start2)
        {
            // if changes do not touch and we still can perform one of these optimisations,
            // it means that given DiffIterable is not LCS (because we can build a smaller one). This should not happen.
            return;
        }

        int count1 = range1.End1 - range1.Start1;
        int count2 = range2.End1 - range2.Start1;

        int equalForward = ExpandForward(range1.End1, range1.End2, range1.End1 + count2, range1.End2 + count2);
        int equalBackward = ExpandBackward(range2.Start1 - count1, range2.Start2 - count1, range2.Start1, range2.Start2);

        // nothing to do
        if (equalForward == 0 && equalBackward == 0) return;

        // merge chunks left [A]B[B] -> [AB]B
        if (equalForward == count2)
        {
            _ranges.RemoveAt(_ranges.Count - 1);
            _ranges.RemoveAt(_ranges.Count - 1);
            _ranges.Add(new DiffRange(range1.Start1, range1.End1 + count2, range1.Start2, range1.End2 + count2));
            ProcessLastRanges();
            return;
        }

        // merge chunks right [A]A[B] -> A[AB]
        if (equalBackward == count1)
        {
            _ranges.RemoveAt(_ranges.Count - 1);
            _ranges.RemoveAt(_ranges.Count - 1);
            _ranges.Add(new DiffRange(range2.Start1 - count1, range2.End1, range2.Start2 - count1, range2.End2));
            ProcessLastRanges();
            return;
        }

        bool touchLeft = range1.End1 == range2.Start1;

        int shift = GetShift(touchLeft, equalForward, equalBackward, range1, range2);
        if (shift != 0)
        {
            _ranges.RemoveAt(_ranges.Count - 1);
            _ranges.RemoveAt(_ranges.Count - 1);
            _ranges.Add(new DiffRange(range1.Start1, range1.End1 + shift, range1.Start2, range1.End2 + shift));
            _ranges.Add(new DiffRange(range2.Start1 + shift, range2.End1, range2.Start2 + shift, range2.End2));
        }
    }

    /// <summary>0 — do nothing; &gt;0 — shift forward; &lt;0 — shift backward.</summary>
    protected abstract int GetShift(bool touchLeft, int equalForward, int equalBackward, DiffRange range1, DiffRange range2);

    /// <summary>Counts pairwise-equal elements walking forward from (start1, start2).</summary>
    private int ExpandForward(int start1, int start2, int end1, int end2)
    {
        int count = 0;
        while (start1 + count < end1 && start2 + count < end2 &&
               _comparer.Equals(Data1[start1 + count], Data2[start2 + count]))
        {
            count++;
        }
        return count;
    }

    /// <summary>Counts pairwise-equal elements walking backward from (end1, end2).</summary>
    private int ExpandBackward(int start1, int start2, int end1, int end2)
    {
        int count = 0;
        while (end1 - 1 - count >= start1 && end2 - 1 - count >= start2 &&
               _comparer.Equals(Data1[end1 - 1 - count], Data2[end2 - 1 - count]))
        {
            count++;
        }
        return count;
    }
}

/*
 * 1. Minimise amount of chunks
 *      good: "AX[AB]" - "[AB]"
 *      bad: "[A]XA[B]" - "[A][B]"
 *
 * 2. Prefer insertions/deletions, that are bounded by empty (or 'unimportant') line
 *      good: "ABooYZ [ABuuYZ ]ABzzYZ" - "ABooYZ []ABzzYZ"
 *      bad: "ABooYZ AB[uuYZ AB]zzYZ" - "ABooYZ AB[]zzYZ"
 */
internal sealed class LineChunkOptimizer : ChunkOptimizer<DiffLine>
{
    public LineChunkOptimizer(IReadOnlyList<DiffLine> lines1, IReadOnlyList<DiffLine> lines2)
        : base(lines1, lines2, EqualityComparer<DiffLine>.Default)
    {
    }

    protected override int GetShift(bool touchLeft, int equalForward, int equalBackward, DiffRange range1, DiffRange range2)
    {
        const int threshold = TextUtil.UnimportantLineCharCount;

        int? shift;

        shift = GetUnchangedBoundaryShift(touchLeft, equalForward, equalBackward, range1, range2, 0);
        if (shift != null) return shift.Value;

        shift = GetChangedBoundaryShift(touchLeft, equalForward, equalBackward, range1, range2, 0);
        if (shift != null) return shift.Value;

        shift = GetUnchangedBoundaryShift(touchLeft, equalForward, equalBackward, range1, range2, threshold);
        if (shift != null) return shift.Value;

        shift = GetChangedBoundaryShift(touchLeft, equalForward, equalBackward, range1, range2, threshold);
        if (shift != null) return shift.Value;

        return 0;
    }

    /// <summary>
    /// Search for an empty line boundary in unchanged lines,
    /// i.e. we want insertion/deletion to go right before/after of an empty line.
    /// </summary>
    private int? GetUnchangedBoundaryShift(bool touchLeft, int equalForward, int equalBackward, DiffRange range1, DiffRange range2, int threshold)
    {
        var touchLines = touchLeft ? Data1 : Data2;
        int touchStart = touchLeft ? range2.Start1 : range2.Start2;

        int shiftForward = FindNextUnimportantLine(touchLines, touchStart, equalForward + 1, threshold);
        int shiftBackward = FindPrevUnimportantLine(touchLines, touchStart - 1, equalBackward + 1, threshold);

        return CombineShift(shiftForward, shiftBackward);
    }

    /// <summary>
    /// Search for an empty line boundary in changed lines,
    /// i.e. we want insertion/deletion to start/end with an empty line.
    /// </summary>
    private int? GetChangedBoundaryShift(bool touchLeft, int equalForward, int equalBackward, DiffRange range1, DiffRange range2, int threshold)
    {
        bool nonTouchLeft = !touchLeft;
        var nonTouchLines = nonTouchLeft ? Data1 : Data2;
        int changeStart = nonTouchLeft ? range1.End1 : range1.End2;
        int changeEnd = nonTouchLeft ? range2.Start1 : range2.Start2;

        int shiftForward = FindNextUnimportantLine(nonTouchLines, changeStart, equalForward + 1, threshold);
        int shiftBackward = FindPrevUnimportantLine(nonTouchLines, changeEnd - 1, equalBackward + 1, threshold);

        return CombineShift(shiftForward, shiftBackward);
    }

    private static int FindNextUnimportantLine(IReadOnlyList<DiffLine> lines, int offset, int count, int threshold)
    {
        for (int i = 0; i < count; i++)
        {
            if (lines[offset + i].NonSpaceChars <= threshold) return i;
        }
        return -1;
    }

    private static int FindPrevUnimportantLine(IReadOnlyList<DiffLine> lines, int offset, int count, int threshold)
    {
        for (int i = 0; i < count; i++)
        {
            if (lines[offset - i].NonSpaceChars <= threshold) return i;
        }
        return -1;
    }

    private static int? CombineShift(int shiftForward, int shiftBackward)
    {
        if (shiftForward == -1 && shiftBackward == -1) return null;
        if (shiftForward == 0 || shiftBackward == 0) return 0;

        return shiftForward != -1 ? shiftForward : -shiftBackward;
    }
}

/*
 * 1. Minimise amount of chunks
 *      good: "AX[AB]" - "[AB]"
 *      bad: "[A]XA[B]" - "[A][B]"
 *
 * 2. Minimise amount of modified 'sentences', where sentence is a sequence of words, that are not separated by whitespace
 *      good: "[AX] [AZ]" - "[AX] AY [AZ]"
 *      bad: "[AX A][Z]" - "[AX A]Y A[Z]"
 *      ex: "1.0.123 1.0.155" vs "1.0.123 1.0.134 1.0.155"
 */
internal sealed class WordChunkOptimizer : ChunkOptimizer<ChunkRef>
{
    public WordChunkOptimizer(IReadOnlyList<ChunkRef> words1, IReadOnlyList<ChunkRef> words2)
        : base(words1, words2, ChunkRefComparer.Instance)
    {
    }

    protected override int GetShift(bool touchLeft, int equalForward, int equalBackward, DiffRange range1, DiffRange range2)
    {
        var touchWords = touchLeft ? Data1 : Data2;
        int touchStart = touchLeft ? range2.Start1 : range2.Start2;

        // check if chunks are already separated by whitespaces
        if (IsSeparatedWithWhitespace(touchWords[touchStart - 1], touchWords[touchStart])) return 0;

        // shift chunks left [X]A Y[A ZA] -> [XA] YA [ZA]
        //                   [X][A ZA] -> [XA] [ZA]
        int leftShift = FindSequenceEdgeShift(touchWords, touchStart, equalForward, leftToRight: true);
        if (leftShift > 0) return leftShift;

        // shift chunks right [AX A]Y A[Z] -> [AX] AY [AZ]
        //                    [AX A][Z] -> [AX] [AZ]
        int rightShift = FindSequenceEdgeShift(touchWords, touchStart - 1, equalBackward, leftToRight: false);
        if (rightShift > 0) return -rightShift;

        // nothing to do
        return 0;
    }

    private static int FindSequenceEdgeShift(IReadOnlyList<ChunkRef> words, int offset, int count, bool leftToRight)
    {
        for (int i = 0; i < count; i++)
        {
            ChunkRef word1;
            ChunkRef word2;
            if (leftToRight)
            {
                word1 = words[offset + i];
                word2 = words[offset + i + 1];
            }
            else
            {
                word1 = words[offset - i - 1];
                word2 = words[offset - i];
            }
            if (IsSeparatedWithWhitespace(word1, word2)) return i + 1;
        }
        return -1;
    }

    private static bool IsSeparatedWithWhitespace(ChunkRef word1, ChunkRef word2)
    {
        if (word1.Chunk.IsNewline || word2.Chunk.IsNewline) return true;

        for (int i = word1.Chunk.End; i < word2.Chunk.Start; i++)
        {
            if (TextUtil.IsSpaceEnterOrTab(word1.Text[i])) return true;
        }
        return false;
    }
}
