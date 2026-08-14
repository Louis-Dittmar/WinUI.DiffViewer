// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/util/diff/PatienceIntLCS.kt
//
// Patience diff: anchors on elements unique to both sequences (UniqueLCS),
// recurses into the gaps, and falls back to bounded Myers where no unique
// anchors exist. Serves as the fallback when plain Myers exceeds its
// difference threshold.

using System;

namespace WinUI.DiffViewer.Core;

internal sealed class PatienceIntLcs
{
    private readonly int[] _first;
    private readonly int[] _second;
    private readonly int _start1;
    private readonly int _count1;
    private readonly int _start2;
    private readonly int _count2;
    private readonly DiffBitSet _changes1;
    private readonly DiffBitSet _changes2;

    public PatienceIntLcs(int[] first, int[] second)
        : this(first, second, 0, first.Length, 0, second.Length,
               new DiffBitSet(first.Length), new DiffBitSet(second.Length))
    {
    }

    public PatienceIntLcs(
        int[] first, int[] second,
        int start1, int count1, int start2, int count2,
        DiffBitSet changes1, DiffBitSet changes2)
    {
        _first = first;
        _second = second;
        _start1 = start1;
        _count1 = count1;
        _start2 = start2;
        _count2 = count2;
        _changes1 = changes1;
        _changes2 = changes2;
    }

    public DiffBitSet[] Changes => new[] { _changes1, _changes2 };

    public void Execute(bool failOnSmallReduction = false)
    {
        int thresholdCheckCounter = failOnSmallReduction ? 2 : -1;
        Execute(_start1, _count1, _start2, _count2, thresholdCheckCounter);
    }

    private void Execute(int start1, int count1, int start2, int count2, int thresholdCheckCounter)
    {
        if (count1 == 0 && count2 == 0)
        {
            return;
        }

        if (count1 == 0 || count2 == 0)
        {
            AddChange(start1, count1, start2, count2);
            return;
        }

        int startOffset = MatchForward(start1, count1, start2, count2);
        start1 += startOffset;
        start2 += startOffset;
        count1 -= startOffset;
        count2 -= startOffset;

        int endOffset = MatchBackward(start1, count1, start2, count2);
        count1 -= endOffset;
        count2 -= endOffset;

        if (count1 == 0 || count2 == 0)
        {
            AddChange(start1, count1, start2, count2);
        }
        else
        {
            if (thresholdCheckCounter == 0) CheckReduction(count1, count2);
            thresholdCheckCounter = Math.Max(-1, thresholdCheckCounter - 1);

            var uniqueLcs = new UniqueLcs(_first, _second, start1, count1, start2, count2);
            var matching = uniqueLcs.Execute();

            if (matching == null)
            {
                if (thresholdCheckCounter >= 0) CheckReduction(count1, count2);
                var intLcs = new MyersLcs(_first, _second, start1, count1, start2, count2, _changes1, _changes2);
                intLcs.ExecuteLinear();
            }
            else
            {
                int s1, s2, c1, c2;
                int matched = matching[0].Length;
                if (matched <= 0) throw new InvalidOperationException("Empty unique matching");

                c1 = matching[0][0];
                c2 = matching[1][0];

                Execute(start1, c1, start2, c2, thresholdCheckCounter);

                for (int i = 1; i < matching[0].Length; i++)
                {
                    s1 = matching[0][i - 1] + 1;
                    s2 = matching[1][i - 1] + 1;

                    c1 = matching[0][i] - s1;
                    c2 = matching[1][i] - s2;

                    if (c1 > 0 || c2 > 0)
                    {
                        Execute(start1 + s1, c1, start2 + s2, c2, thresholdCheckCounter);
                    }
                }

                if (matching[0][matched - 1] == count1 - 1)
                {
                    s1 = count1 - 1;
                    c1 = 0;
                }
                else
                {
                    s1 = matching[0][matched - 1] + 1;
                    c1 = count1 - s1;
                }
                if (matching[1][matched - 1] == count2 - 1)
                {
                    s2 = count2 - 1;
                    c2 = 0;
                }
                else
                {
                    s2 = matching[1][matched - 1] + 1;
                    c2 = count2 - s2;
                }

                Execute(start1 + s1, c1, start2 + s2, c2, thresholdCheckCounter);
            }
        }
    }

    private int MatchForward(int start1, int count1, int start2, int count2)
    {
        int size = Math.Min(count1, count2);
        int idx = 0;
        for (int i = 0; i < size; i++)
        {
            if (_first[start1 + i] != _second[start2 + i]) break;
            ++idx;
        }
        return idx;
    }

    private int MatchBackward(int start1, int count1, int start2, int count2)
    {
        int size = Math.Min(count1, count2);
        int idx = 0;
        for (int i = 1; i <= size; i++)
        {
            if (_first[start1 + count1 - i] != _second[start2 + count2 - i]) break;
            ++idx;
        }
        return idx;
    }

    private void AddChange(int start1, int count1, int start2, int count2)
    {
        _changes1.Set(start1, start1 + count1);
        _changes2.Set(start2, start2 + count2);
    }

    private void CheckReduction(int count1, int count2)
    {
        if (count1 * 2 < _count1) return;
        if (count2 * 2 < _count2) return;
        throw new DiffTooBigException();
    }
}
