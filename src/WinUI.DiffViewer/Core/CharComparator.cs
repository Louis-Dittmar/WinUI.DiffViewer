// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/ByCharRt.kt
//
// Simplification: the original's two-step comparison (non-whitespace first,
// then re-diffing the gaps) is replaced by a single Myers run with
// policy-filtered code points. Surrogate pairs are kept intact.

using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

public static class CharComparator
{
    private readonly struct CharToken
    {
        public readonly int Offset;
        public readonly int Length;
        public readonly int CodePoint;

        public CharToken(int offset, int length, int codePoint)
        {
            Offset = offset;
            Length = length;
            CodePoint = codePoint;
        }
    }

    /// <summary>Computes character-level differences; offsets are relative to the given texts.</summary>
    public static List<DiffFragment> Compare(string text1, string text2, ComparisonPolicy policy)
    {
        var tokens1 = Tokenize(text1, policy);
        var tokens2 = Tokenize(text2, policy);

        var codePoints1 = new List<int>(tokens1.Count);
        foreach (var t in tokens1) codePoints1.Add(t.CodePoint);
        var codePoints2 = new List<int>(tokens2.Count);
        foreach (var t in tokens2) codePoints2.Add(t.CodePoint);

        var changes = DiffAlgorithm.Diff(codePoints1, codePoints2);

        var fragments = new List<DiffFragment>();
        foreach (var change in changes)
        {
            int start1 = RangeStart(tokens1, change.Start1, change.End1, text1.Length);
            int end1 = RangeEnd(tokens1, change.Start1, change.End1, start1);
            int start2 = RangeStart(tokens2, change.Start2, change.End2, text2.Length);
            int end2 = RangeEnd(tokens2, change.Start2, change.End2, start2);
            fragments.Add(new DiffFragment(start1, end1, start2, end2));
        }
        return fragments;
    }

    private static int RangeStart(List<CharToken> tokens, int startIndex, int endIndex, int textLength)
    {
        if (startIndex < endIndex) return tokens[startIndex].Offset;
        return startIndex < tokens.Count ? tokens[startIndex].Offset : textLength;
    }

    private static int RangeEnd(List<CharToken> tokens, int startIndex, int endIndex, int fallbackStart)
    {
        if (startIndex < endIndex)
        {
            var last = tokens[endIndex - 1];
            return last.Offset + last.Length;
        }
        return fallbackStart;
    }

    private static List<CharToken> Tokenize(string text, ComparisonPolicy policy)
    {
        var tokens = new List<CharToken>(text.Length);
        int offset = 0;
        while (offset < text.Length)
        {
            int cp = char.ConvertToUtf32(text, offset);
            int length = char.IsSurrogatePair(text, offset) ? 2 : 1;

            bool isWhitespace = TextUtil.IsWhiteSpaceCodePoint(cp);
            bool skip = policy switch
            {
                ComparisonPolicy.IgnoreWhitespaces => isWhitespace,
                ComparisonPolicy.TrimWhitespaces => isWhitespace && IsLeadingOrTrailing(text, offset),
                _ => false,
            };

            if (!skip)
            {
                tokens.Add(new CharToken(offset, length, cp));
            }
            offset += length;
        }
        return tokens;
    }

    private static bool IsLeadingOrTrailing(string text, int index)
    {
        // Whitespace run touching a line start or line end.
        int i = index;
        while (i > 0 && TextUtil.IsSpaceEnterOrTab(text[i - 1]) && text[i - 1] != '\n') i--;
        bool leading = i == 0 || text[i - 1] == '\n';
        if (leading) return true;

        i = index;
        while (i < text.Length && TextUtil.IsSpaceEnterOrTab(text[i]) && text[i] != '\n') i++;
        return i == text.Length || text[i] == '\n';
    }
}
