// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/util/Range.kt

namespace WinUI.DiffViewer.Core;

/// <summary>
/// A pair of half-open intervals: [Start1, End1) in the first sequence
/// matched with [Start2, End2) in the second sequence.
/// </summary>
public readonly struct DiffRange
{
    public int Start1 { get; }
    public int End1 { get; }
    public int Start2 { get; }
    public int End2 { get; }

    public DiffRange(int start1, int end1, int start2, int end2)
    {
        Start1 = start1;
        End1 = end1;
        Start2 = start2;
        End2 = end2;
    }

    public bool IsEmpty => Start1 == End1 && Start2 == End2;

    public int Length1 => End1 - Start1;
    public int Length2 => End2 - Start2;

    public override string ToString() => $"[{Start1}, {End1}) - [{Start2}, {End2})";
}
