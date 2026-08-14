// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/ByLineRt.kt
//
// Full doCompare pipeline: lines are first matched ignoring whitespace
// (compareSmart — a two-step diff that anchors on "important" lines only),
// boundaries are corrected by the LineChunkOptimizer, and for exact policies
// a second step (correctChangesSecondStep) re-establishes exact matching
// without losing the whitespace-insensitive alignment. This is what separates
// "a new line was inserted" from "the neighboring lines changed" the way
// IntelliJ does. Not ported: the merge (3-way) entry points.

using System;
using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

/// <summary>A single line prepared for comparison under a policy (port of ByLineRt.Line).</summary>
public sealed class DiffLine
{
    public string Content { get; }
    public ComparisonPolicy Policy { get; }
    public int NonSpaceChars { get; }

    private readonly int _hash;

    public DiffLine(string content, ComparisonPolicy policy)
    {
        Content = content;
        Policy = policy;
        _hash = TextUtil.GetHashCode(content, policy);
        NonSpaceChars = CountNonSpaceChars(content);
    }

    public override int GetHashCode() => _hash;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not DiffLine other) return false;
        if (_hash != other._hash) return false;
        return TextUtil.IsEqual(Content, other.Content, Policy);
    }

    private static int CountNonSpaceChars(string text)
    {
        int nonSpace = 0;
        foreach (char c in text)
        {
            if (!TextUtil.IsSpaceEnterOrTab(c)) nonSpace++;
        }
        return nonSpace;
    }
}

public static class LineComparator
{
    /// <summary>Compares two lists of lines; returns changed line ranges (port of ByLineRt.doCompare).</summary>
    public static List<DiffRange> Compare(
        IReadOnlyList<string> lines1,
        IReadOnlyList<string> lines2,
        ComparisonPolicy policy)
    {
        var diffLines1 = GetLines(lines1, policy);
        var diffLines2 = GetLines(lines2, policy);

        if (policy == ComparisonPolicy.IgnoreWhitespaces)
        {
            var changes = CompareSmart(diffLines1, diffLines2);
            changes = new LineChunkOptimizer(diffLines1, diffLines2).Build(changes);
            return ExpandRanges(diffLines1, diffLines2, changes);
        }
        else
        {
            // Match ignoring whitespace first: an inserted line then pairs the
            // surviving lines correctly even when their indentation changed.
            var iwLines1 = ConvertMode(diffLines1, ComparisonPolicy.IgnoreWhitespaces);
            var iwLines2 = ConvertMode(diffLines2, ComparisonPolicy.IgnoreWhitespaces);

            var iwChanges = CompareSmart(iwLines1, iwLines2);
            iwChanges = new LineChunkOptimizer(diffLines1, diffLines2).Build(iwChanges);
            return CorrectChangesSecondStep(diffLines1, diffLines2, iwChanges);
        }
    }

    /*
     * We want to fix invalid matching here:
     *
     * .{        ..{
     * ..{   vs  ...{
     * ...{
     *
     * The first step will return the matching (0,2)-(0,2), and we should adjust it to (1,3)-(0,2).
     *
     * From the other hand, we don't want to reduce the number of IW-matched lines:
     *
     * .{         ...{
     * ..{    vs  ..{
     * ...{       .{
     *
     * Here the first step returns (0,3)-(0,3) and 'correcting' it to (0,1)-(2,3)
     * would be wrong (and would break the ByWord highlighting).
     *
     * Idea:
     * 1. lines are matched at the first step and equal -> match them
     * 2. lines are not matched at the first step -> do not match them
     * 3. lines are matched at the first step and not equal ->
     *   a. find all IW-equal lines in the same unmatched block
     *   b. find a maximum matching between them, maximising the amount of equal pairs in it
     *   c. match equal lines using the result of the previous step
     */
    private static List<DiffRange> CorrectChangesSecondStep(
        List<DiffLine> lines1, List<DiffLine> lines2, List<DiffRange> iwChanges)
    {
        var builder = new ExpandChangeBuilder(lines1, lines2);
        string? sample = null;
        int last1 = 0;
        int last2 = 0;

        void Flush(int line1, int line2)
        {
            if (sample == null) return;

            int start1 = Math.Max(last1, builder.Index1);
            int start2 = Math.Max(last2, builder.Index2);

            var subLines1 = new List<int>();
            var subLines2 = new List<int>();
            for (int i = start1; i < line1; i++)
            {
                if (TextUtil.IsEqual(sample, lines1[i].Content, ComparisonPolicy.IgnoreWhitespaces))
                {
                    subLines1.Add(i);
                    last1 = i + 1;
                }
            }
            for (int i = start2; i < line2; i++)
            {
                if (TextUtil.IsEqual(sample, lines2[i].Content, ComparisonPolicy.IgnoreWhitespaces))
                {
                    subLines2.Add(i);
                    last2 = i + 1;
                }
            }

            // The original asserts both lists are non-empty (the sample stems
            // from an IW-matched pair, so each side contains it at least once).
            if (subLines1.Count > 0 && subLines2.Count > 0)
            {
                AlignExactMatching(subLines1, subLines2, lines1, lines2, builder);
            }

            sample = null;
        }

        foreach (var range in DiffAlgorithm.IterateUnchanged(iwChanges, lines1.Count, lines2.Count))
        {
            int count = range.End1 - range.Start1;
            for (int i = 0; i < count; i++)
            {
                int index1 = range.Start1 + i;
                int index2 = range.Start2 + i;
                var line1 = lines1[index1];
                var line2 = lines2[index2];

                if (sample == null || !TextUtil.IsEqual(sample, line1.Content, ComparisonPolicy.IgnoreWhitespaces))
                {
                    if (line1.Equals(line2))
                    {
                        Flush(index1, index2);
                        builder.MarkEqual(index1, index2);
                    }
                    else
                    {
                        Flush(index1, index2);
                        sample = line1.Content;
                    }
                }
            }
        }
        Flush(lines1.Count, lines2.Count);

        return builder.Finish();
    }

    private static void AlignExactMatching(
        List<int> subLines1, List<int> subLines2,
        List<DiffLine> lines1, List<DiffLine> lines2,
        ExpandChangeBuilder builder)
    {
        int n = Math.Max(subLines1.Count, subLines2.Count);
        bool skipAligning = n > 10 ||  // brute-force C(n,k) — this limits the search space to ~250 cases
                            subLines1.Count == subLines2.Count; // nothing to do

        if (skipAligning)
        {
            int count = Math.Min(subLines1.Count, subLines2.Count);
            for (int i = 0; i < count; i++)
            {
                int index1 = subLines1[i];
                int index2 = subLines2[i];
                if (lines1[index1].Equals(lines2[index2]))
                {
                    builder.MarkEqual(index1, index2);
                }
            }
            return;
        }

        if (subLines1.Count < subLines2.Count)
        {
            var matching = GetBestMatchingAlignment(subLines1, subLines2, lines1, lines2);
            for (int i = 0; i < subLines1.Count; i++)
            {
                int index1 = subLines1[i];
                int index2 = subLines2[matching[i]];
                if (lines1[index1].Equals(lines2[index2]))
                {
                    builder.MarkEqual(index1, index2);
                }
            }
        }
        else
        {
            var matching = GetBestMatchingAlignment(subLines2, subLines1, lines2, lines1);
            for (int i = 0; i < subLines2.Count; i++)
            {
                int index1 = subLines1[matching[i]];
                int index2 = subLines2[i];
                if (lines1[index1].Equals(lines2[index2]))
                {
                    builder.MarkEqual(index1, index2);
                }
            }
        }
    }

    /// <summary>
    /// Finds the order-preserving injection of subLines1 into subLines2 that
    /// maximises the number of exactly-equal pairs (brute force, n &lt;= 10).
    /// </summary>
    private static int[] GetBestMatchingAlignment(
        List<int> subLines1, List<int> subLines2,
        List<DiffLine> lines1, List<DiffLine> lines2)
    {
        int size = subLines1.Count;

        var comb = new int[size];
        var best = new int[size];
        for (int i = 0; i < size; i++) best[i] = i;
        int bestWeight = 0;

        void Combinations(int start, int n, int k)
        {
            if (k == size)
            {
                int weight = 0;
                for (int i = 0; i < size; i++)
                {
                    if (lines1[subLines1[i]].Equals(lines2[subLines2[comb[i]]])) weight++;
                }
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    Array.Copy(comb, best, comb.Length);
                }
                return;
            }

            for (int i = start; i <= n; i++)
            {
                comb[k] = i;
                Combinations(i + 1, n, k + 1);
            }
        }

        Combinations(0, subLines2.Count - 1, 0);
        return best;
    }

    /*
     * Compare lines in two steps:
     *  - compare ignoring "unimportant" lines
     *  - correct changes (compare all lines in the gaps between matched chunks)
     */
    private static List<DiffRange> CompareSmart(List<DiffLine> lines1, List<DiffLine> lines2)
    {
        const int threshold = TextUtil.UnimportantLineCharCount;

        var (bigLines1, indexes1) = GetBigLines(lines1, threshold);
        var (bigLines2, indexes2) = GetBigLines(lines2, threshold);

        var changes = DiffAlgorithm.Diff(bigLines1, bigLines2);
        return new SmartLineChangeCorrector(indexes1, indexes2, lines1, lines2, changes).Build();
    }

    private static (List<DiffLine> lines, List<int> indexes) GetBigLines(List<DiffLine> lines, int threshold)
    {
        var bigLines = new List<DiffLine>(lines.Count);
        var indexes = new List<int>(lines.Count);

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].NonSpaceChars > threshold)
            {
                bigLines.Add(lines[i]);
                indexes.Add(i);
            }
        }
        return (bigLines, indexes);
    }

    private static List<DiffRange> ExpandRanges(
        List<DiffLine> lines1, List<DiffLine> lines2, List<DiffRange> changes)
    {
        var result = new List<DiffRange>(changes.Count);
        foreach (var ch in changes)
        {
            var expanded = DiffTrimUtil.Expand(lines1, lines2, ch.Start1, ch.Start2, ch.End1, ch.End2);
            if (!expanded.IsEmpty) result.Add(expanded);
        }
        return result;
    }

    private static List<DiffLine> GetLines(IReadOnlyList<string> lines, ComparisonPolicy policy)
    {
        var result = new List<DiffLine>(lines.Count);
        foreach (var line in lines)
        {
            result.Add(new DiffLine(line, policy));
        }
        return result;
    }

    private static List<DiffLine> ConvertMode(List<DiffLine> original, ComparisonPolicy policy)
    {
        var result = new List<DiffLine>(original.Count);
        foreach (var line in original)
        {
            result.Add(line.Policy == policy ? line : new DiffLine(line.Content, policy));
        }
        return result;
    }
}
