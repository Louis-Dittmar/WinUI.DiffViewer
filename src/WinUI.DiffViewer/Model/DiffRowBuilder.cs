// Row construction: turns the algorithm result (DiffResult) into aligned
// side-by-side row lists or a unified row list. The filler-row alignment
// corresponds to IntelliJ's "Align Changes in Side-by-Side Diff" mode
// (platform/diff-impl/src/com/intellij/diff/tools/simple/AlignedDiffModel.kt),
// which is used here as the only mode because it allows exact synchronous
// scrolling of both panes.

using System;
using System.Collections.Generic;
using WinUI.DiffViewer.Core;

namespace WinUI.DiffViewer.Model;

public sealed class SideBySideRows
{
    public IReadOnlyList<DiffRow> Left { get; }
    public IReadOnlyList<DiffRow> Right { get; }

    public SideBySideRows(IReadOnlyList<DiffRow> left, IReadOnlyList<DiffRow> right)
    {
        Left = left;
        Right = right;
    }
}

public static class DiffRowBuilder
{
    public static SideBySideRows BuildSideBySide(DiffResult result)
    {
        var left = new List<DiffRow>();
        var right = new List<DiffRow>();

        int line1 = 0;
        int line2 = 0;

        foreach (var fragment in result.Fragments)
        {
            // Unchanged run before this fragment.
            while (line1 < fragment.StartLine1)
            {
                left.Add(UnchangedRow(result.Lines1[line1], line1, line2));
                right.Add(UnchangedRow(result.Lines2[line2], line1, line2));
                line1++;
                line2++;
            }

            var kindLeft = fragment.Type switch
            {
                LineFragmentType.Deleted => DiffRowKind.Deleted,
                LineFragmentType.Modified => DiffRowKind.Modified,
                _ => DiffRowKind.Inserted,
            };
            var kindRight = fragment.Type switch
            {
                LineFragmentType.Inserted => DiffRowKind.Inserted,
                LineFragmentType.Modified => DiffRowKind.Modified,
                _ => DiffRowKind.Deleted,
            };

            var leftHighlights = MapInnerToRows(result, fragment, side1: true);
            var rightHighlights = MapInnerToRows(result, fragment, side1: false);

            int count1 = fragment.EndLine1 - fragment.StartLine1;
            int count2 = fragment.EndLine2 - fragment.StartLine2;

            for (int i = 0; i < count1; i++)
            {
                left.Add(new DiffRow
                {
                    Text = result.Lines1[fragment.StartLine1 + i],
                    Kind = kindLeft,
                    OldLineNumber = (fragment.StartLine1 + i + 1).ToString(),
                    Highlights = leftHighlights[i],
                });
            }
            for (int i = 0; i < count2; i++)
            {
                right.Add(new DiffRow
                {
                    Text = result.Lines2[fragment.StartLine2 + i],
                    Kind = kindRight,
                    NewLineNumber = (fragment.StartLine2 + i + 1).ToString(),
                    Highlights = rightHighlights[i],
                });
            }

            // Pad the shorter side so both panes stay aligned.
            for (int i = count1; i < count2; i++) left.Add(new DiffRow { Kind = DiffRowKind.Filler });
            for (int i = count2; i < count1; i++) right.Add(new DiffRow { Kind = DiffRowKind.Filler });

            line1 = fragment.EndLine1;
            line2 = fragment.EndLine2;
        }

        // Trailing unchanged run.
        while (line1 < result.Lines1.Count && line2 < result.Lines2.Count)
        {
            left.Add(UnchangedRow(result.Lines1[line1], line1, line2));
            right.Add(UnchangedRow(result.Lines2[line2], line1, line2));
            line1++;
            line2++;
        }

        return new SideBySideRows(left, right);
    }

    public static IReadOnlyList<DiffRow> BuildUnified(
        DiffResult result,
        IReadOnlyList<SyntaxSpan>[]? syntax1 = null,
        IReadOnlyList<SyntaxSpan>[]? syntax2 = null)
    {
        var rows = new List<DiffRow>();

        int line1 = 0;
        int line2 = 0;

        foreach (var fragment in result.Fragments)
        {
            while (line1 < fragment.StartLine1)
            {
                rows.Add(UnchangedRow(result.Lines1[line1], line1, line2, syntax1));
                line1++;
                line2++;
            }

            var leftHighlights = MapInnerToRows(result, fragment, side1: true);
            var rightHighlights = MapInnerToRows(result, fragment, side1: false);

            for (int i = fragment.StartLine1; i < fragment.EndLine1; i++)
            {
                rows.Add(new DiffRow
                {
                    Text = result.Lines1[i],
                    Kind = DiffRowKind.Deleted,
                    OldLineNumber = (i + 1).ToString(),
                    Highlights = leftHighlights[i - fragment.StartLine1],
                    Syntax = SyntaxFor(syntax1, i),
                });
            }
            for (int i = fragment.StartLine2; i < fragment.EndLine2; i++)
            {
                rows.Add(new DiffRow
                {
                    Text = result.Lines2[i],
                    Kind = DiffRowKind.Inserted,
                    NewLineNumber = (i + 1).ToString(),
                    Highlights = rightHighlights[i - fragment.StartLine2],
                    Syntax = SyntaxFor(syntax2, i),
                });
            }

            line1 = fragment.EndLine1;
            line2 = fragment.EndLine2;
        }

        while (line1 < result.Lines1.Count && line2 < result.Lines2.Count)
        {
            rows.Add(UnchangedRow(result.Lines1[line1], line1, line2, syntax1));
            line1++;
            line2++;
        }

        return rows;
    }

    private static IReadOnlyList<SyntaxSpan> SyntaxFor(IReadOnlyList<SyntaxSpan>[]? syntax, int line)
        => syntax != null && line < syntax.Length ? syntax[line] : Array.Empty<SyntaxSpan>();

    private static DiffRow UnchangedRow(string text, int line1, int line2, IReadOnlyList<SyntaxSpan>[]? syntax1 = null) => new()
    {
        Text = text,
        Kind = DiffRowKind.Unchanged,
        OldLineNumber = (line1 + 1).ToString(),
        NewLineNumber = (line2 + 1).ToString(),
        Syntax = SyntaxFor(syntax1, line1),
    };

    /// <summary>
    /// Distributes a fragment's inner word/char fragments onto its individual
    /// rows. Inner offsets are relative to the fragment start of each side.
    /// </summary>
    private static List<IReadOnlyList<HighlightSpan>> MapInnerToRows(DiffResult result, LineFragment fragment, bool side1)
    {
        int startLine = side1 ? fragment.StartLine1 : fragment.StartLine2;
        int endLine = side1 ? fragment.EndLine1 : fragment.EndLine2;
        var lines = side1 ? result.Lines1 : result.Lines2;

        int lineCount = endLine - startLine;
        var perRow = new List<IReadOnlyList<HighlightSpan>>(lineCount);

        if (fragment.InnerFragments == null || fragment.InnerFragments.Count == 0)
        {
            for (int i = 0; i < lineCount; i++) perRow.Add(Array.Empty<HighlightSpan>());
            return perRow;
        }

        // Row start offsets relative to the fragment ("\n" separated).
        var rowStarts = new int[lineCount + 1];
        int acc = 0;
        for (int i = 0; i < lineCount; i++)
        {
            rowStarts[i] = acc;
            acc += lines[startLine + i].Length + 1; // +1 for '\n'
        }
        rowStarts[lineCount] = acc;

        var listPerRow = new List<HighlightSpan>?[lineCount];
        foreach (var inner in fragment.InnerFragments)
        {
            int start = side1 ? inner.StartOffset1 : inner.StartOffset2;
            int end = side1 ? inner.EndOffset1 : inner.EndOffset2;
            if (end < start) continue;

            for (int row = 0; row < lineCount; row++)
            {
                int rowStart = rowStarts[row];
                int rowTextEnd = rowStart + lines[startLine + row].Length;

                int overlapStart = Math.Max(start, rowStart);
                int overlapEnd = Math.Min(end, rowTextEnd);
                if (overlapStart >= overlapEnd) continue;

                (listPerRow[row] ??= new List<HighlightSpan>())
                    .Add(new HighlightSpan(overlapStart - rowStart, overlapEnd - overlapStart));
            }
        }

        for (int i = 0; i < lineCount; i++)
        {
            perRow.Add((IReadOnlyList<HighlightSpan>?)listPerRow[i] ?? Array.Empty<HighlightSpan>());
        }
        return perRow;
    }
}
