// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/TrimUtil.kt
// platform/util/diff/src/com/intellij/diff/comparison/ComparisonUtil.kt
// platform/util/diff/src/com/intellij/diff/comparison/CharacterUtils.kt
//
// Character classification uses .NET's built-in Unicode tables instead of the
// original's embedded UCD range tables; behavior is equivalent for all common input.

using System;
using System.Globalization;

namespace WinUI.DiffViewer.Core;

public static class TextUtil
{
    /// <summary>Space, tab or newline — the whitespace definition used throughout the diff.</summary>
    public static bool IsSpaceEnterOrTab(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r';

    public static bool IsPunctuation(int codePoint)
    {
        int b = codePoint;
        if (b == 95) return false; // exclude '_'
        return (b >= 33 && b <= 47) ||   // !"#$%&'()*+,-./
               (b >= 58 && b <= 64) ||   // :;<=>?@
               (b >= 91 && b <= 96) ||   // [\]^_`
               (b >= 123 && b <= 126);   // {|}~
    }

    public static bool IsWhiteSpaceCodePoint(int codePoint)
        => codePoint < 128 && IsSpaceEnterOrTab((char)codePoint);

    public static bool IsAlpha(int codePoint)
    {
        if (IsWhiteSpaceCodePoint(codePoint)) return false;
        return !IsPunctuation(codePoint);
    }

    /// <summary>
    /// Scripts without word delimiters (CJK, Kana, Thai, ...): every code point
    /// becomes its own "word" so the word-level diff stays meaningful.
    /// </summary>
    public static bool IsContinuousScript(int codePoint)
    {
        if (codePoint < 128) return false;
        if (IsDecimalDigit(codePoint)) return false;

        if (codePoint > 0xFFFF) return true; // non-BMP

        var category = CharUnicodeInfo.GetUnicodeCategory((char)codePoint);
        bool isLetter = category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.LetterNumber
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark;
        if (!isLetter) return true;

        return IsIdeographic(codePoint)
               || (codePoint >= 0x3041 && codePoint <= 0x30FF)   // Hiragana + Katakana
               || (codePoint >= 0x31F0 && codePoint <= 0x31FF)   // Katakana phonetic extensions
               || (codePoint >= 0xFF66 && codePoint <= 0xFF9D)   // Halfwidth Katakana
               || (codePoint >= 0x0E01 && codePoint <= 0x0E5B)   // Thai
               || (codePoint >= 0xA980 && codePoint <= 0xA9DF);  // Javanese
    }

    private static bool IsIdeographic(int c) =>
        (c >= 0x3006 && c <= 0x303A) ||
        (c >= 0x3400 && c <= 0x4DBF) ||
        (c >= 0x4E00 && c <= 0x9FFF) ||
        (c >= 0xF900 && c <= 0xFAD9);

    private static bool IsDecimalDigit(int c)
        => c <= 0xFFFF && CharUnicodeInfo.GetUnicodeCategory((char)c) == UnicodeCategory.DecimalDigitNumber;

    // ---------------------------------------------------------------------
    // Trimming
    // ---------------------------------------------------------------------

    public static int TrimStart(string text, int start, int end)
    {
        while (start < end && IsSpaceEnterOrTab(text[start])) start++;
        return start;
    }

    public static int TrimEnd(string text, int start, int end)
    {
        while (start < end && IsSpaceEnterOrTab(text[end - 1])) end--;
        return end;
    }

    // ---------------------------------------------------------------------
    // Policy-aware equality and hashing (port of ComparisonUtil)
    // ---------------------------------------------------------------------

    public static bool IsEqual(string? text1, string? text2, ComparisonPolicy policy)
    {
        if (ReferenceEquals(text1, text2)) return true;
        if (text1 == null || text2 == null) return false;

        return policy switch
        {
            ComparisonPolicy.Default => string.Equals(text1, text2, StringComparison.Ordinal),
            ComparisonPolicy.TrimWhitespaces => EqualsTrimWhitespaces(text1, text2),
            ComparisonPolicy.IgnoreWhitespaces => EqualsIgnoreWhitespaces(text1, text2),
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
    }

    public static int GetHashCode(string text, ComparisonPolicy policy)
    {
        switch (policy)
        {
            case ComparisonPolicy.Default:
                return StringHashCode(text, 0, text.Length);
            case ComparisonPolicy.TrimWhitespaces:
                int offset1 = TrimStart(text, 0, text.Length);
                int offset2 = TrimEnd(text, offset1, text.Length);
                return StringHashCode(text, offset1, offset2);
            case ComparisonPolicy.IgnoreWhitespaces:
                return StringHashCodeIgnoreWhitespaces(text);
            default:
                throw new ArgumentOutOfRangeException(nameof(policy));
        }
    }

    /// <summary>Java-compatible string hash over [start, end).</summary>
    public static int StringHashCode(string text, int start, int end)
    {
        int h = 0;
        for (int i = start; i < end; i++)
        {
            unchecked { h = 31 * h + text[i]; }
        }
        return h;
    }

    public static int StringHashCodeIgnoreWhitespaces(string text)
    {
        int h = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (IsSpaceEnterOrTab(c)) continue;
            unchecked { h = 31 * h + c; }
        }
        return h;
    }

    /// <summary>
    /// Leading/trailing whitespace of every line (including inner lines) is ignored.
    /// "\nXY\n" equals "\n XY \n"; "\nXY\n" differs from "\nX Y\n".
    /// </summary>
    public static bool EqualsTrimWhitespaces(string s1, string s2)
    {
        int index1 = 0;
        int index2 = 0;

        while (true)
        {
            bool lastLine1 = false;
            bool lastLine2 = false;

            int end1 = s1.IndexOf('\n', index1) + 1;
            int end2 = s2.IndexOf('\n', index2) + 1;
            if (end1 == 0)
            {
                end1 = s1.Length;
                lastLine1 = true;
            }
            if (end2 == 0)
            {
                end2 = s2.Length;
                lastLine2 = true;
            }
            if (lastLine1 != lastLine2) return false;

            if (!EqualsTrimmedRange(s1, index1, end1, s2, index2, end2)) return false;

            index1 = end1;
            index2 = end2;
            if (lastLine1) return true;
        }
    }

    private static bool EqualsTrimmedRange(string s1, int start1, int end1, string s2, int start2, int end2)
    {
        start1 = TrimStart(s1, start1, end1);
        end1 = TrimEnd(s1, start1, end1);
        start2 = TrimStart(s2, start2, end2);
        end2 = TrimEnd(s2, start2, end2);

        if (end1 - start1 != end2 - start2) return false;
        for (int i = 0; i < end1 - start1; i++)
        {
            if (s1[start1 + i] != s2[start2 + i]) return false;
        }
        return true;
    }

    public static bool EqualsIgnoreWhitespaces(string s1, string s2)
    {
        int i1 = 0;
        int i2 = 0;
        while (true)
        {
            while (i1 < s1.Length && IsSpaceEnterOrTab(s1[i1])) i1++;
            while (i2 < s2.Length && IsSpaceEnterOrTab(s2[i2])) i2++;

            if (i1 == s1.Length && i2 == s2.Length) return true;
            if (i1 == s1.Length || i2 == s2.Length) return false;
            if (s1[i1] != s2[i2]) return false;
            i1++;
            i2++;
        }
    }

    /// <summary>Lines with at most this many non-space characters ("{", "}", "") are "unimportant" (port of DiffConfig.UNIMPORTANT_LINE_CHAR_COUNT).</summary>
    public const int UnimportantLineCharCount = 3;
}
