// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/ByWordRt.kt
//
// The tokenizer (getInlineChunks) is a faithful port: maximal runs of
// non-punctuation/non-whitespace form word chunks; "continuous script" code
// points (CJK, Kana, Thai, ...) become single-code-point words; every '\n' is
// its own chunk. In addition — replacing the original's separate punctuation/
// whitespace correction passes (AdjustmentPunctuationMatcher and the
// *SpacesCorrector family) — punctuation characters and whitespace runs are
// tokenized as well and handled by policy filtering. The visual result is
// close to the original at a fraction of the complexity.

using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

public static class WordComparator
{
    private readonly struct Token
    {
        public readonly int Start;
        public readonly int End;
        public readonly int Hash;
        public readonly TokenKind Kind;

        public Token(int start, int end, int hash, TokenKind kind)
        {
            Start = start;
            End = end;
            Hash = hash;
            Kind = kind;
        }
    }

    private enum TokenKind
    {
        Word,
        Newline,
        Punctuation,
        Whitespace,
    }

    private sealed class TokenComparer : IEqualityComparer<TokenRef>
    {
        public static readonly TokenComparer Instance = new();

        public bool Equals(TokenRef a, TokenRef b)
        {
            if (a.Token.Hash != b.Token.Hash) return false;
            if (a.Token.Kind != b.Token.Kind) return false;
            int len1 = a.Token.End - a.Token.Start;
            int len2 = b.Token.End - b.Token.Start;
            if (a.Token.Kind == TokenKind.Newline) return true;
            if (len1 != len2) return false;
            for (int i = 0; i < len1; i++)
            {
                if (a.Text[a.Token.Start + i] != b.Text[b.Token.Start + i]) return false;
            }
            return true;
        }

        public int GetHashCode(TokenRef t) => t.Token.Hash * 31 + (int)t.Token.Kind;
    }

    private readonly struct TokenRef
    {
        public readonly string Text;
        public readonly Token Token;

        public TokenRef(string text, Token token)
        {
            Text = text;
            Token = token;
        }
    }

    /// <summary>
    /// Computes word-level differences between two texts (typically the two
    /// sides of one changed line block). Returned offsets are relative to the
    /// given texts.
    /// </summary>
    public static List<DiffFragment> Compare(string text1, string text2, ComparisonPolicy policy)
    {
        var tokens1 = Filter(Tokenize(text1), text1, policy);
        var tokens2 = Filter(Tokenize(text2), text2, policy);

        var refs1 = ToRefs(text1, tokens1);
        var refs2 = ToRefs(text2, tokens2);

        var changes = DiffAlgorithm.Diff(refs1, refs2, TokenComparer.Instance);

        var fragments = new List<DiffFragment>();
        foreach (var change in changes)
        {
            int start1 = TokenRangeStart(tokens1, change.Start1, change.End1, text1.Length);
            int end1 = TokenRangeEnd(tokens1, change.Start1, change.End1, start1);
            int start2 = TokenRangeStart(tokens2, change.Start2, change.End2, text2.Length);
            int end2 = TokenRangeEnd(tokens2, change.Start2, change.End2, start2);
            fragments.Add(new DiffFragment(start1, end1, start2, end2));
        }

        return MergeAdjacent(fragments);
    }

    private static List<TokenRef> ToRefs(string text, List<Token> tokens)
    {
        var refs = new List<TokenRef>(tokens.Count);
        foreach (var token in tokens)
        {
            refs.Add(new TokenRef(text, token));
        }
        return refs;
    }

    private static int TokenRangeStart(List<Token> tokens, int startIndex, int endIndex, int textLength)
    {
        if (startIndex < endIndex) return tokens[startIndex].Start;
        // Empty side: anchor the insertion point at the next token (or end of text).
        return startIndex < tokens.Count ? tokens[startIndex].Start : textLength;
    }

    private static int TokenRangeEnd(List<Token> tokens, int startIndex, int endIndex, int fallbackStart)
    {
        if (startIndex < endIndex) return tokens[endIndex - 1].End;
        return fallbackStart;
    }

    private static List<DiffFragment> MergeAdjacent(List<DiffFragment> fragments)
    {
        if (fragments.Count < 2) return fragments;

        var result = new List<DiffFragment>(fragments.Count);
        var current = fragments[0];
        for (int i = 1; i < fragments.Count; i++)
        {
            var next = fragments[i];
            if (next.StartOffset1 <= current.EndOffset1 && next.StartOffset2 <= current.EndOffset2)
            {
                current = new DiffFragment(
                    current.StartOffset1,
                    next.EndOffset1 > current.EndOffset1 ? next.EndOffset1 : current.EndOffset1,
                    current.StartOffset2,
                    next.EndOffset2 > current.EndOffset2 ? next.EndOffset2 : current.EndOffset2);
            }
            else
            {
                result.Add(current);
                current = next;
            }
        }
        result.Add(current);
        return result;
    }

    // -----------------------------------------------------------------
    // Tokenizer — port of ByWordRt.getInlineChunks
    // -----------------------------------------------------------------

    private static List<Token> Tokenize(string text)
    {
        var chunks = new List<Token>();

        int len = text.Length;
        int offset = 0;

        int wordStart = -1;
        int wordHash = 0;

        int wsStart = -1;

        while (offset < len)
        {
            int ch = char.ConvertToUtf32(text, offset);
            int charCount = char.IsSurrogatePair(text, offset) ? 2 : 1;

            bool isAlpha = TextUtil.IsAlpha(ch);
            bool isWordPart = isAlpha && !TextUtil.IsContinuousScript(ch);

            if (isWordPart)
            {
                FlushWhitespace(chunks, text, ref wsStart, offset);
                if (wordStart == -1)
                {
                    wordStart = offset;
                    wordHash = 0;
                }
                unchecked { wordHash = wordHash * 31 + ch; }
            }
            else
            {
                if (wordStart != -1)
                {
                    chunks.Add(new Token(wordStart, offset, wordHash, TokenKind.Word));
                    wordStart = -1;
                }

                if (isAlpha)
                {
                    // continuous script: each code point is its own word
                    FlushWhitespace(chunks, text, ref wsStart, offset);
                    chunks.Add(new Token(offset, offset + charCount, ch, TokenKind.Word));
                }
                else if (ch == '\n')
                {
                    FlushWhitespace(chunks, text, ref wsStart, offset);
                    chunks.Add(new Token(offset, offset + 1, '\n', TokenKind.Newline));
                }
                else if (TextUtil.IsWhiteSpaceCodePoint(ch))
                {
                    if (wsStart == -1) wsStart = offset;
                }
                else
                {
                    // punctuation: each character is its own token
                    FlushWhitespace(chunks, text, ref wsStart, offset);
                    chunks.Add(new Token(offset, offset + charCount, ch, TokenKind.Punctuation));
                }
            }

            offset += charCount;
        }

        if (wordStart != -1)
        {
            chunks.Add(new Token(wordStart, len, wordHash, TokenKind.Word));
        }
        FlushWhitespace(chunks, text, ref wsStart, len);

        return chunks;
    }

    private static void FlushWhitespace(List<Token> chunks, string text, ref int wsStart, int offset)
    {
        if (wsStart == -1) return;
        chunks.Add(new Token(wsStart, offset, TextUtil.StringHashCode(text, wsStart, offset), TokenKind.Whitespace));
        wsStart = -1;
    }

    // -----------------------------------------------------------------
    // Policy filtering — replaces the original's whitespace correctors
    // -----------------------------------------------------------------

    private static List<Token> Filter(List<Token> tokens, string text, ComparisonPolicy policy)
    {
        switch (policy)
        {
            case ComparisonPolicy.Default:
                return tokens;

            case ComparisonPolicy.IgnoreWhitespaces:
                {
                    var result = new List<Token>(tokens.Count);
                    foreach (var token in tokens)
                    {
                        if (token.Kind != TokenKind.Whitespace) result.Add(token);
                    }
                    return result;
                }

            case ComparisonPolicy.TrimWhitespaces:
                {
                    // Drop whitespace tokens that touch a line boundary.
                    var result = new List<Token>(tokens.Count);
                    for (int i = 0; i < tokens.Count; i++)
                    {
                        var token = tokens[i];
                        if (token.Kind == TokenKind.Whitespace)
                        {
                            bool atLineStart = token.Start == 0 || text[token.Start - 1] == '\n';
                            bool atLineEnd = token.End == text.Length || text[token.End] == '\n';
                            if (atLineStart || atLineEnd) continue;
                        }
                        result.Add(token);
                    }
                    return result;
                }

            default:
                return tokens;
        }
    }
}
