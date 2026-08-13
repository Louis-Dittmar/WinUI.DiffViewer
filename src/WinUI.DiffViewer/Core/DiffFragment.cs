// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/fragments/DiffFragment.kt
// platform/util/diff/src/com/intellij/diff/fragments/LineFragment.kt

using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

/// <summary>An unmatched region, expressed in character offsets of both texts.</summary>
public sealed class DiffFragment
{
    public int StartOffset1 { get; }
    public int EndOffset1 { get; }
    public int StartOffset2 { get; }
    public int EndOffset2 { get; }

    public DiffFragment(int startOffset1, int endOffset1, int startOffset2, int endOffset2)
    {
        StartOffset1 = startOffset1;
        EndOffset1 = endOffset1;
        StartOffset2 = startOffset2;
        EndOffset2 = endOffset2;
    }

    public override string ToString() => $"[{StartOffset1}, {EndOffset1}) - [{StartOffset2}, {EndOffset2})";
}

/// <summary>The kind of a changed block, derived from which side is empty.</summary>
public enum LineFragmentType
{
    Inserted,
    Deleted,
    Modified,
}

/// <summary>
/// A block of changed lines. Line indices and character offsets refer to the
/// complete side-1 / side-2 texts. <see cref="InnerFragments"/> (optional) holds
/// word- or character-level differences with offsets relative to
/// <see cref="StartOffset1"/> / <see cref="StartOffset2"/>.
/// </summary>
public sealed class LineFragment
{
    public int StartLine1 { get; }
    public int EndLine1 { get; }
    public int StartLine2 { get; }
    public int EndLine2 { get; }

    public int StartOffset1 { get; }
    public int EndOffset1 { get; }
    public int StartOffset2 { get; }
    public int EndOffset2 { get; }

    /// <summary>Inner word/char differences, offsets relative to the fragment. Null when not computed.</summary>
    public IReadOnlyList<DiffFragment>? InnerFragments { get; internal set; }

    public LineFragment(
        int startLine1, int endLine1, int startLine2, int endLine2,
        int startOffset1, int endOffset1, int startOffset2, int endOffset2,
        IReadOnlyList<DiffFragment>? innerFragments = null)
    {
        StartLine1 = startLine1;
        EndLine1 = endLine1;
        StartLine2 = startLine2;
        EndLine2 = endLine2;
        StartOffset1 = startOffset1;
        EndOffset1 = endOffset1;
        StartOffset2 = startOffset2;
        EndOffset2 = endOffset2;
        InnerFragments = innerFragments;
    }

    public LineFragmentType Type
    {
        get
        {
            bool empty1 = StartLine1 == EndLine1;
            bool empty2 = StartLine2 == EndLine2;
            if (empty1 && !empty2) return LineFragmentType.Inserted;
            if (!empty1 && empty2) return LineFragmentType.Deleted;
            return LineFragmentType.Modified;
        }
    }

    public override string ToString() =>
        $"lines [{StartLine1}, {EndLine1}) - [{StartLine2}, {EndLine2}), offsets [{StartOffset1}, {EndOffset1}) - [{StartOffset2}, {EndOffset2})";
}
