// Modeled after JetBrains intellij-community (Apache 2.0):
// platform/diff-impl/src/com/intellij/diff/comparison/ComparisonManagerImpl.java
// (compareLines / compareLinesInner / createInnerFragments)

using System;
using System.Collections.Generic;
using System.Text;

namespace WinUI.DiffViewer.Core;

public sealed class DiffOptions
{
    public ComparisonPolicy Policy { get; set; } = ComparisonPolicy.Default;
    public HighlightMode HighlightMode { get; set; } = HighlightMode.Words;

    /// <summary>Fragments longer than this (chars, per side) skip inner word/char highlighting.</summary>
    public int MaxInnerFragmentLength { get; set; } = 100_000;
}

/// <summary>The result of comparing two texts: split lines plus changed blocks.</summary>
public sealed class DiffResult
{
    public IReadOnlyList<string> Lines1 { get; }
    public IReadOnlyList<string> Lines2 { get; }
    public IReadOnlyList<LineFragment> Fragments { get; }

    public DiffResult(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2, IReadOnlyList<LineFragment> fragments)
    {
        Lines1 = lines1;
        Lines2 = lines2;
        Fragments = fragments;
    }
}

public static class DiffComputer
{
    /// <summary>
    /// The high-level entry point: compares two texts line by line and — depending on
    /// <see cref="DiffOptions.HighlightMode"/> — computes word/char-level inner fragments.
    /// Cancellation is intentionally not exception-based: callers that recompute on
    /// every keystroke use a latest-wins version check instead (no debugger-visible
    /// OperationCanceledException storms).
    /// </summary>
    public static DiffResult Compare(string text1, string text2, DiffOptions? options = null)
    {
        options ??= new DiffOptions();

        // Normalize all line separators to '\n'. WinUI TextBox delivers '\r',
        // files may contain "\r\n" — without this, texts collapse into one
        // "line" and everything is reported as changed. All offsets in the
        // result refer to the normalized texts.
        text1 = NormalizeLineSeparators(text1 ?? string.Empty);
        text2 = NormalizeLineSeparators(text2 ?? string.Empty);

        var (lines1, lineStarts1) = SplitLines(text1);
        var (lines2, lineStarts2) = SplitLines(text2);

        // One side is completely empty: mark the whole other side as pure
        // deletion/insertion. (Strict IntelliJ semantics would report Modified
        // when the non-empty side lacks a trailing newline, because the empty
        // document still counts as one empty line — for an interactive editor
        // "everything was deleted" gray is the expected picture.)
        if (text1.Length > 0 && text2.Length == 0)
        {
            var deletedAll = new LineFragment(0, lines1.Count, 0, 0, 0, text1.Length, 0, 0);
            return new DiffResult(lines1, lines2, new List<LineFragment> { deletedAll });
        }
        if (text1.Length == 0 && text2.Length > 0)
        {
            var insertedAll = new LineFragment(0, 0, 0, lines2.Count, 0, 0, 0, text2.Length);
            return new DiffResult(lines1, lines2, new List<LineFragment> { insertedAll });
        }

        var changes = LineComparator.Compare(lines1, lines2, options.Policy);

        var fragments = new List<LineFragment>(changes.Count);
        foreach (var change in changes)
        {
            var fragment = CreateLineFragment(change, lines1, lines2, lineStarts1, lineStarts2);

            bool wantInner = options.HighlightMode is HighlightMode.Words or HighlightMode.Characters
                             && fragment.Type == LineFragmentType.Modified;
            if (wantInner)
            {
                int length1 = fragment.EndOffset1 - fragment.StartOffset1;
                int length2 = fragment.EndOffset2 - fragment.StartOffset2;
                if (length1 <= options.MaxInnerFragmentLength && length2 <= options.MaxInnerFragmentLength)
                {
                    string chunk1 = text1.Substring(fragment.StartOffset1, length1);
                    string chunk2 = text2.Substring(fragment.StartOffset2, length2);

                    // Word-correlation split (port of ByWordRt.compareAndSplit):
                    // the Modified block may fall apart into several correctly
                    // typed fragments (Deleted/Inserted/Modified) with inner
                    // highlights per Modified sub-fragment.
                    fragments.AddRange(ByWordSplitter.CompareAndSplit(
                        chunk1, chunk2, fragment, options.Policy, options.HighlightMode));
                    continue;
                }
            }

            fragments.Add(fragment);
        }

        return new DiffResult(lines1, lines2, fragments);
    }

    private static LineFragment CreateLineFragment(
        DiffRange change,
        List<string> lines1, List<string> lines2,
        List<int> lineStarts1, List<int> lineStarts2)
    {
        int startOffset1 = OffsetOfLineStart(lines1, lineStarts1, change.Start1);
        int endOffset1 = OffsetOfLineEnd(lines1, lineStarts1, change.Start1, change.End1);
        int startOffset2 = OffsetOfLineStart(lines2, lineStarts2, change.Start2);
        int endOffset2 = OffsetOfLineEnd(lines2, lineStarts2, change.Start2, change.End2);

        return new LineFragment(
            change.Start1, change.End1, change.Start2, change.End2,
            startOffset1, endOffset1, startOffset2, endOffset2);
    }

    private static int OffsetOfLineStart(List<string> lines, List<int> lineStarts, int line)
    {
        if (line < lineStarts.Count) return lineStarts[line];
        // Position past the last line: end of text.
        int last = lines.Count - 1;
        return lineStarts[last] + lines[last].Length;
    }

    private static int OffsetOfLineEnd(List<string> lines, List<int> lineStarts, int startLine, int endLine)
    {
        if (endLine == startLine) return OffsetOfLineStart(lines, lineStarts, startLine);
        int lastLine = endLine - 1;
        int end = lineStarts[lastLine] + lines[lastLine].Length;
        // Include the closing '\n' (IntelliJ getLineEnd(line, includeNewline)) —
        // the word-correlation splitter needs the newline chunks to find line
        // boundaries; only the very last line has none to include.
        if (lastLine != lines.Count - 1) end += 1;
        return end;
    }

    /// <summary>Converts "\r\n" and lone '\r' to '\n' so the line model sees every break.</summary>
    internal static string NormalizeLineSeparators(string text)
    {
        if (text.IndexOf('\r') < 0) return text;

        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') continue; // CRLF: keep only the LF
                sb.Append('\n'); // lone CR (WinUI TextBox)
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>Splits text into lines (without '\n') and records each line's start offset.</summary>
    internal static (List<string> lines, List<int> lineStarts) SplitLines(string text)
    {
        var lines = new List<string>();
        var starts = new List<int>();

        if (text.Length == 0)
        {
            lines.Add(string.Empty);
            starts.Add(0);
            return (lines, starts);
        }

        int index = 0;
        while (index <= text.Length)
        {
            int newline = text.IndexOf('\n', Math.Min(index, text.Length));
            if (newline < 0)
            {
                lines.Add(text.Substring(index));
                starts.Add(index);
                break;
            }
            lines.Add(text.Substring(index, newline - index));
            starts.Add(index);
            index = newline + 1;
            if (index == text.Length)
            {
                // Trailing newline produces a final empty line (same as the IntelliJ line model).
                lines.Add(string.Empty);
                starts.Add(index);
                break;
            }
        }

        return (lines, starts);
    }
}
