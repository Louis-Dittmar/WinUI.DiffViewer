// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/util/diff/MyersLCS.kt
//
// Algorithm for finding the longest common subsequence of two sequences.
// Based on E.W. Myers / "An O(ND) Difference Algorithm and Its Variations" / 1986.
// O(ND) runtime, O(N) memory (linear-space bidirectional middle-snake variant).

using System;

namespace WinUI.DiffViewer.Core;

public sealed class MyersLcs
{
    private readonly int[] _first;
    private readonly int[] _second;
    private readonly int _start1;
    private readonly int _count1;
    private readonly int _start2;
    private readonly int _count2;
    private readonly DiffBitSet _changes1;
    private readonly DiffBitSet _changes2;

    private readonly int[] _vForward;
    private readonly int[] _vBackward;

    public MyersLcs(int[] first, int[] second)
        : this(first, second, 0, first.Length, 0, second.Length,
               new DiffBitSet(first.Length), new DiffBitSet(second.Length))
    {
    }

    public MyersLcs(
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

        _changes1.Set(start1, start1 + count1);
        _changes2.Set(start2, start2 + count2);

        int totalSequenceLength = count1 + count2;
        _vForward = new int[totalSequenceLength + 1];
        _vBackward = new int[totalSequenceLength + 1];
    }

    /// <summary>Changed bits per side; matched elements have their bit cleared.</summary>
    public DiffBitSet Changes1 => _changes1;

    public DiffBitSet Changes2 => _changes2;

    /// <summary>
    /// Runs the O(ND) Myers algorithm where D is bound by A + B * sqrt(N).
    /// Under typical input distributions the expected running time reduces to O(N).
    /// </summary>
    public void ExecuteLinear()
    {
        int threshold = 20000 + 10 * (int)Math.Sqrt(_count1 + _count2);
        Execute(threshold, false);
    }

    /// <summary>Runs the exact algorithm (no difference bound).</summary>
    public void Execute()
    {
        Execute(_count1 + _count2, false);
    }

    private void Execute(int threshold, bool throwException)
    {
        if (_count1 == 0 || _count2 == 0) return;
        Execute(0, _count1, 0, _count2, Math.Min(threshold, _count1 + _count2), throwException);
    }

    // LCS( old[oldStart, oldEnd), new[newStart, newEnd) )
    private void Execute(int oldStart, int oldEnd, int newStart, int newEnd, int differenceEstimate, bool throwException)
    {
        if (oldStart > oldEnd || newStart > newEnd) throw new InvalidOperationException("Invalid range");

        if (oldStart < oldEnd && newStart < newEnd)
        {
            int oldLength = oldEnd - oldStart;
            int newLength = newEnd - newStart;
            _vForward[newLength + 1] = 0;
            _vBackward[newLength + 1] = 0;
            int halfD = (differenceEstimate + 1) / 2;
            int xx = -1, kk = -1, td = -1;

            for (int d = 0; d <= halfD; d++)
            {
                int L = newLength + Math.Max(-d, -newLength + ((d ^ newLength) & 1));
                int R = newLength + Math.Min(d, oldLength - ((d ^ oldLength) & 1));

                for (int k = L; k <= R; k += 2)
                {
                    int x = (k == L || (k != R && _vForward[k - 1] < _vForward[k + 1]))
                        ? _vForward[k + 1]
                        : _vForward[k - 1] + 1;
                    int y = x - k + newLength;
                    x += CommonSubsequenceLengthForward(oldStart + x, newStart + y,
                        Math.Min(oldEnd - oldStart - x, newEnd - newStart - y));
                    _vForward[k] = x;
                }

                if ((oldLength - newLength) % 2 != 0)
                {
                    for (int k = L; k <= R; k += 2)
                    {
                        if (oldLength - (d - 1) <= k && k <= oldLength + (d - 1))
                        {
                            if (_vForward[k] + _vBackward[newLength + oldLength - k] >= oldLength)
                            {
                                xx = _vForward[k];
                                kk = k;
                                td = 2 * d - 1;
                                goto done;
                            }
                        }
                    }
                }

                for (int k = L; k <= R; k += 2)
                {
                    int x = (k == L || (k != R && _vBackward[k - 1] < _vBackward[k + 1]))
                        ? _vBackward[k + 1]
                        : _vBackward[k - 1] + 1;
                    int y = x - k + newLength;
                    x += CommonSubsequenceLengthBackward(oldEnd - 1 - x, newEnd - 1 - y,
                        Math.Min(oldEnd - oldStart - x, newEnd - newStart - y));
                    _vBackward[k] = x;
                }

                if ((oldLength - newLength) % 2 == 0)
                {
                    for (int k = L; k <= R; k += 2)
                    {
                        if (oldLength - d <= k && k <= oldLength + d)
                        {
                            if (_vForward[oldLength + newLength - k] + _vBackward[k] >= oldLength)
                            {
                                xx = oldLength - _vBackward[k];
                                kk = oldLength + newLength - k;
                                td = 2 * d;
                                goto done;
                            }
                        }
                    }
                }
            }

        done:
            if (td > 1)
            {
                int yy = xx - kk + newLength;
                int oldDiff = (td + 1) / 2;
                if (0 < xx && 0 < yy)
                    Execute(oldStart, oldStart + xx, newStart, newStart + yy, oldDiff, throwException);
                if (oldStart + xx < oldEnd && newStart + yy < newEnd)
                    Execute(oldStart + xx, oldEnd, newStart + yy, newEnd, td - oldDiff, throwException);
            }
            else if (td >= 0)
            {
                int x = oldStart;
                int y = newStart;
                while (x < oldEnd && y < newEnd)
                {
                    int commonLength = CommonSubsequenceLengthForward(x, y, Math.Min(oldEnd - x, newEnd - y));
                    if (commonLength > 0)
                    {
                        AddUnchanged(x, y, commonLength);
                        x += commonLength;
                        y += commonLength;
                    }
                    else if (oldEnd - oldStart > newEnd - newStart)
                    {
                        ++x;
                    }
                    else
                    {
                        ++y;
                    }
                }
            }
            else
            {
                // The difference is more than the given estimate.
                if (throwException) throw new DiffTooBigException();
            }
        }
    }

    private void AddUnchanged(int start1, int start2, int count)
    {
        _changes1.Set(_start1 + start1, _start1 + start1 + count, false);
        _changes2.Set(_start2 + start2, _start2 + start2 + count, false);
    }

    private int CommonSubsequenceLengthForward(int oldIndex, int newIndex, int maxLength)
    {
        int x = oldIndex;
        int y = newIndex;

        maxLength = Math.Min(maxLength, Math.Min(_count1 - oldIndex, _count2 - newIndex));
        while (x - oldIndex < maxLength && _first[_start1 + x] == _second[_start2 + y])
        {
            ++x;
            ++y;
        }
        return x - oldIndex;
    }

    private int CommonSubsequenceLengthBackward(int oldIndex, int newIndex, int maxLength)
    {
        int x = oldIndex;
        int y = newIndex;

        maxLength = Math.Min(maxLength, Math.Min(oldIndex, newIndex) + 1);
        while (oldIndex - x < maxLength && _first[_start1 + x] == _second[_start2 + y])
        {
            --x;
            --y;
        }
        return oldIndex - x;
    }
}

/// <summary>Thrown when the difference exceeds the configured estimate (port of FilesTooBigForDiffException).</summary>
public sealed class DiffTooBigException : Exception
{
    public DiffTooBigException() : base("Unable to compute diff: input difference is too large")
    {
    }
}
