// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/ByLineRt.kt
//
// Simplifications relative to the original: the two-step "ignore whitespaces
// first, refine afterwards" matching (correctChangesSecondStep), the
// "unimportant lines" pre-pass (compareSmart) and the LineChunkOptimizer are
// not ported. The Myers diff runs directly over policy-hashed lines, which
// produces the same changed blocks in almost all cases.

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
    /// <summary>Compares two lists of lines; returns changed line ranges.</summary>
    public static List<DiffRange> Compare(
        IReadOnlyList<string> lines1,
        IReadOnlyList<string> lines2,
        ComparisonPolicy policy)
    {
        var diffLines1 = GetLines(lines1, policy);
        var diffLines2 = GetLines(lines2, policy);
        return DiffAlgorithm.Diff(diffLines1, diffLines2);
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
}
