using System.Collections.Generic;
using WinUI.DiffViewer.Core;

namespace WinUI.DiffViewer.Model;

/// <summary>Visual classification of a rendered diff row.</summary>
public enum DiffRowKind
{
    Unchanged,
    Inserted,
    Deleted,
    Modified,

    /// <summary>Empty padding row used to keep both panes aligned.</summary>
    Filler,
}

/// <summary>A highlighted span inside a row's text (character index + length).</summary>
public readonly struct HighlightSpan
{
    public int Start { get; }
    public int Length { get; }

    public HighlightSpan(int start, int length)
    {
        Start = start;
        Length = length;
    }
}

/// <summary>
/// One rendered row of the diff. In side-by-side mode each pane binds its own
/// line number; in unified mode both numbers are shown.
/// </summary>
public sealed class DiffRow
{
    public string Text { get; init; } = string.Empty;

    public DiffRowKind Kind { get; init; }

    /// <summary>1-based line number in the old text; empty for filler/inserted rows.</summary>
    public string OldLineNumber { get; init; } = string.Empty;

    /// <summary>1-based line number in the new text; empty for filler/deleted rows.</summary>
    public string NewLineNumber { get; init; } = string.Empty;

    /// <summary>Word/char highlight spans inside <see cref="Text"/>.</summary>
    public IReadOnlyList<HighlightSpan> Highlights { get; init; } = System.Array.Empty<HighlightSpan>();

    /// <summary>Syntax token spans inside <see cref="Text"/> (empty when highlighting is off).</summary>
    public IReadOnlyList<SyntaxSpan> Syntax { get; init; } = System.Array.Empty<SyntaxSpan>();

    /// <summary>
    /// The single line number shown in unified mode: deleted rows keep their
    /// old-text number, all other rows show the new-text number.
    /// </summary>
    public string UnifiedLineNumber => Kind == DiffRowKind.Deleted ? OldLineNumber : NewLineNumber;

    public bool IsFiller => Kind == DiffRowKind.Filler;
}
