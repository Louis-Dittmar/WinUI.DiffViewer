// Syntax highlighting for the unified view, modeled on IntelliJ's concept of
// a per-language SyntaxHighlighter producing token classes that a color scheme
// maps to attributes (intellij-community: platform/editor-ui-api/src/com/
// intellij/openapi/fileTypes/SyntaxHighlighter.java). IntelliJ detects the
// language via the file type/extension; a diff control only sees text, so
// detection here is a content heuristic. Lexers are deliberately small:
// keywords, strings, comments and numbers — the token classes that carry the
// most reading signal in a diff.

using System;
using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

/// <summary>The language used for syntax highlighting.</summary>
public enum SyntaxLanguage
{
    /// <summary>No syntax highlighting.</summary>
    None,

    /// <summary>Detect the language from the text content.</summary>
    Auto,

    CSharp,
    Xml,
    Json,
}

/// <summary>Token class of a highlighted span (mapped to brushes by the view).</summary>
public enum SyntaxKind
{
    Keyword,
    String,
    Comment,
    Number,
}

/// <summary>A highlighted range inside one line (character index + length).</summary>
public readonly struct SyntaxSpan
{
    public int Start { get; }
    public int Length { get; }
    public SyntaxKind Kind { get; }

    public SyntaxSpan(int start, int length, SyntaxKind kind)
    {
        Start = start;
        Length = length;
        Kind = kind;
    }
}

public static class SyntaxHighlighter
{
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "record", "ref", "return", "sbyte",
        "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
        "ushort", "using", "var", "virtual", "void", "volatile", "while",
        "async", "await", "dynamic", "get", "set", "init", "nameof", "partial", "required",
        "value", "when", "where", "yield",
    };

    // -----------------------------------------------------------------
    // Language detection (content heuristic)
    // -----------------------------------------------------------------

    /// <summary>Detects the language of a text pair: the longer side wins, the other is the fallback.</summary>
    public static SyntaxLanguage Detect(string text1, string text2)
    {
        string primary = text1.Length >= text2.Length ? text1 : text2;
        string secondary = ReferenceEquals(primary, text1) ? text2 : text1;

        var language = DetectText(primary);
        if (language == SyntaxLanguage.None)
        {
            language = DetectText(secondary);
        }
        return language;
    }

    private static SyntaxLanguage DetectText(string text)
    {
        int i = 0;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        if (i >= text.Length) return SyntaxLanguage.None;

        char first = text[i];

        if (first == '<') return SyntaxLanguage.Xml;

        if ((first == '{' || first == '[') && LooksLikeJson(text)) return SyntaxLanguage.Json;

        return CSharpScore(text) >= 3 ? SyntaxLanguage.CSharp : SyntaxLanguage.None;
    }

    private static bool LooksLikeJson(string text)
    {
        // A quoted key followed by a colon — and none of C#'s statement glue.
        return (text.Contains("\":", StringComparison.Ordinal) || text.Contains("\" :", StringComparison.Ordinal))
               && !text.Contains(';');
    }

    private static int CSharpScore(string text)
    {
        int score = 0;
        ReadOnlySpan<string> markers = new[]
        {
            "using ", "namespace ", "public ", "private ", "protected ", "internal ",
            "class ", "struct ", "interface ", "void ", "var ", "return", "static ",
            "new ", "=>", "#region", "#endregion", "foreach",
        };
        foreach (var marker in markers)
        {
            if (text.Contains(marker, StringComparison.Ordinal)) score++;
        }
        if (text.Contains(';')) score++;
        return score;
    }

    // -----------------------------------------------------------------
    // Line tokenization
    // -----------------------------------------------------------------

    private static readonly IReadOnlyList<SyntaxSpan> Empty = Array.Empty<SyntaxSpan>();

    /// <summary>
    /// Tokenizes a whole document line by line. Multi-line constructs (block
    /// comments, verbatim strings) carry their state across lines.
    /// </summary>
    public static IReadOnlyList<SyntaxSpan>[] HighlightLines(IReadOnlyList<string> lines, SyntaxLanguage language)
    {
        var result = new IReadOnlyList<SyntaxSpan>[lines.Count];

        switch (language)
        {
            case SyntaxLanguage.CSharp:
                {
                    var state = CSharpState.Default;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        result[i] = TokenizeCSharpLine(lines[i], ref state);
                    }
                    break;
                }
            case SyntaxLanguage.Xml:
                {
                    var state = XmlState.Text;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        result[i] = TokenizeXmlLine(lines[i], ref state);
                    }
                    break;
                }
            case SyntaxLanguage.Json:
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        result[i] = TokenizeJsonLine(lines[i]);
                    }
                    break;
                }
            default:
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        result[i] = Empty;
                    }
                    break;
                }
        }

        return result;
    }

    // ----------------------------- C# --------------------------------

    private enum CSharpState
    {
        Default,
        BlockComment,
        VerbatimString,
    }

    private static IReadOnlyList<SyntaxSpan> TokenizeCSharpLine(string line, ref CSharpState state)
    {
        if (line.Length == 0) return Empty;

        List<SyntaxSpan>? spans = null;
        int i = 0;

        while (i < line.Length)
        {
            if (state == CSharpState.BlockComment)
            {
                int end = line.IndexOf("*/", i, StringComparison.Ordinal);
                if (end < 0)
                {
                    Add(ref spans, i, line.Length - i, SyntaxKind.Comment);
                    return Done(spans);
                }
                Add(ref spans, i, end + 2 - i, SyntaxKind.Comment);
                i = end + 2;
                state = CSharpState.Default;
                continue;
            }

            if (state == CSharpState.VerbatimString)
            {
                int end = FindVerbatimStringEnd(line, i);
                if (end < 0)
                {
                    Add(ref spans, i, line.Length - i, SyntaxKind.String);
                    return Done(spans);
                }
                Add(ref spans, i, end + 1 - i, SyntaxKind.String);
                i = end + 1;
                state = CSharpState.Default;
                continue;
            }

            char c = line[i];

            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                Add(ref spans, i, line.Length - i, SyntaxKind.Comment);
                return Done(spans);
            }

            if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
            {
                int end = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    Add(ref spans, i, line.Length - i, SyntaxKind.Comment);
                    state = CSharpState.BlockComment;
                    return Done(spans);
                }
                Add(ref spans, i, end + 2 - i, SyntaxKind.Comment);
                i = end + 2;
                continue;
            }

            if (c == '#' && IsLineStartSoFar(line, i))
            {
                // Preprocessor directive (#region, #endregion, #if ...) — the whole line.
                Add(ref spans, i, line.Length - i, SyntaxKind.Keyword);
                return Done(spans);
            }

            if (c == '@' || c == '$')
            {
                // String prefix: 1-2 chars out of @/$ followed by '"'.
                // Any '@' in the prefix makes it a verbatim string ("" escapes).
                int p = i;
                bool hasAt = false;
                while (p < line.Length && (line[p] == '@' || line[p] == '$'))
                {
                    hasAt |= line[p] == '@';
                    p++;
                }
                if (p < line.Length && line[p] == '"' && p - i <= 2)
                {
                    int start = i;
                    if (hasAt)
                    {
                        int end = FindVerbatimStringEnd(line, p + 1);
                        if (end < 0)
                        {
                            Add(ref spans, start, line.Length - start, SyntaxKind.String);
                            state = CSharpState.VerbatimString;
                            return Done(spans);
                        }
                        Add(ref spans, start, end + 1 - start, SyntaxKind.String);
                        i = end + 1;
                        continue;
                    }

                    // $"...": a regular string with the prefix included in the span.
                    i = p + 1;
                    while (i < line.Length && line[i] != '"')
                    {
                        if (line[i] == '\\') i++;
                        i++;
                    }
                    i = Math.Min(i + 1, line.Length);
                    Add(ref spans, start, i - start, SyntaxKind.String);
                    continue;
                }
                i = p == i ? i + 1 : p;
                continue;
            }

            if (c == '"')
            {
                int start = i;
                i++;
                while (i < line.Length && line[i] != '"')
                {
                    if (line[i] == '\\') i++;
                    i++;
                }
                i = Math.Min(i + 1, line.Length);
                Add(ref spans, start, i - start, SyntaxKind.String);
                continue;
            }

            if (c == '\'')
            {
                int start = i;
                i++;
                while (i < line.Length && line[i] != '\'')
                {
                    if (line[i] == '\\') i++;
                    i++;
                }
                i = Math.Min(i + 1, line.Length);
                Add(ref spans, start, i - start, SyntaxKind.String);
                continue;
            }

            if (char.IsDigit(c))
            {
                int start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '.' || line[i] == '_'))
                {
                    i++;
                }
                Add(ref spans, start, i - start, SyntaxKind.Number);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                {
                    i++;
                }
                if (CSharpKeywords.Contains(line[start..i]))
                {
                    Add(ref spans, start, i - start, SyntaxKind.Keyword);
                }
                continue;
            }

            i++;
        }

        return Done(spans);
    }

    /// <summary>Finds the closing '"' of a verbatim string ("" is an escaped quote).</summary>
    private static int FindVerbatimStringEnd(string line, int start)
    {
        int i = start;
        while (i < line.Length)
        {
            if (line[i] == '"')
            {
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }
                return i;
            }
            i++;
        }
        return -1;
    }

    private static bool IsLineStartSoFar(string line, int index)
    {
        for (int i = 0; i < index; i++)
        {
            if (!char.IsWhiteSpace(line[i])) return false;
        }
        return true;
    }

    // ----------------------------- XML -------------------------------

    private enum XmlState
    {
        Text,
        Comment,
        Tag,
        String,
    }

    private static IReadOnlyList<SyntaxSpan> TokenizeXmlLine(string line, ref XmlState state)
    {
        if (line.Length == 0) return Empty;

        List<SyntaxSpan>? spans = null;
        int i = 0;

        while (i < line.Length)
        {
            switch (state)
            {
                case XmlState.Comment:
                    {
                        int end = line.IndexOf("-->", i, StringComparison.Ordinal);
                        if (end < 0)
                        {
                            Add(ref spans, i, line.Length - i, SyntaxKind.Comment);
                            return Done(spans);
                        }
                        Add(ref spans, i, end + 3 - i, SyntaxKind.Comment);
                        i = end + 3;
                        state = XmlState.Text;
                        break;
                    }

                case XmlState.String:
                    {
                        int end = line.IndexOf('"', i);
                        if (end < 0)
                        {
                            Add(ref spans, i, line.Length - i, SyntaxKind.String);
                            return Done(spans);
                        }
                        Add(ref spans, i, end + 1 - i, SyntaxKind.String);
                        i = end + 1;
                        state = XmlState.Tag;
                        break;
                    }

                case XmlState.Tag:
                    {
                        char c = line[i];
                        if (c == '"')
                        {
                            int start = i;
                            int end = line.IndexOf('"', i + 1);
                            if (end < 0)
                            {
                                Add(ref spans, start, line.Length - start, SyntaxKind.String);
                                state = XmlState.String;
                                return Done(spans);
                            }
                            Add(ref spans, start, end + 1 - start, SyntaxKind.String);
                            i = end + 1;
                            break;
                        }
                        if (c == '>')
                        {
                            int start = i > 0 && line[i - 1] == '/' ? i - 1 : i;
                            Add(ref spans, start, i + 1 - start, SyntaxKind.Keyword);
                            i++;
                            state = XmlState.Text;
                            break;
                        }
                        i++;
                        break;
                    }

                default: // Text
                    {
                        int lt = line.IndexOf('<', i);
                        if (lt < 0) return Done(spans);

                        if (string.CompareOrdinal(line, lt, "<!--", 0, 4) == 0)
                        {
                            i = lt;
                            state = XmlState.Comment;
                            break;
                        }

                        // "<name" or "</name" is the keyword part of the tag.
                        int j = lt + 1;
                        if (j < line.Length && (line[j] == '/' || line[j] == '?' || line[j] == '!')) j++;
                        while (j < line.Length && (char.IsLetterOrDigit(line[j]) || line[j] is '.' or ':' or '-' or '_')) j++;
                        Add(ref spans, lt, j - lt, SyntaxKind.Keyword);
                        i = j;
                        state = XmlState.Tag;
                        break;
                    }
            }
        }

        return Done(spans);
    }

    // ----------------------------- JSON ------------------------------

    private static IReadOnlyList<SyntaxSpan> TokenizeJsonLine(string line)
    {
        if (line.Length == 0) return Empty;

        List<SyntaxSpan>? spans = null;
        int i = 0;

        while (i < line.Length)
        {
            char c = line[i];

            if (c == '"')
            {
                int start = i;
                i++;
                while (i < line.Length && line[i] != '"')
                {
                    if (line[i] == '\\') i++;
                    i++;
                }
                i = Math.Min(i + 1, line.Length);
                Add(ref spans, start, i - start, SyntaxKind.String);
                continue;
            }

            if (char.IsDigit(c) || (c == '-' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
            {
                int start = i;
                i++;
                while (i < line.Length && (char.IsDigit(line[i]) || line[i] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    i++;
                }
                Add(ref spans, start, i - start, SyntaxKind.Number);
                continue;
            }

            if (char.IsLetter(c))
            {
                int start = i;
                while (i < line.Length && char.IsLetter(line[i])) i++;
                string word = line[start..i];
                if (word is "true" or "false" or "null")
                {
                    Add(ref spans, start, i - start, SyntaxKind.Keyword);
                }
                continue;
            }

            i++;
        }

        return Done(spans);
    }

    // -----------------------------------------------------------------

    private static void Add(ref List<SyntaxSpan>? spans, int start, int length, SyntaxKind kind)
    {
        if (length <= 0) return;
        (spans ??= new List<SyntaxSpan>()).Add(new SyntaxSpan(start, length, kind));
    }

    private static IReadOnlyList<SyntaxSpan> Done(List<SyntaxSpan>? spans) => spans ?? Empty;
}
