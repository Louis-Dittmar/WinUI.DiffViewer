// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/util/diff/UniqueLCS.kt
//
// Patience-style matching: pairs elements that occur exactly once in BOTH
// sequences and computes the longest increasing subsequence over those pairs.
// Returns null when there are no such unique pairs.

using System;
using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

internal sealed class UniqueLcs
{
    private readonly int[] _first;
    private readonly int[] _second;
    private readonly int _start1;
    private readonly int _count1;
    private readonly int _start2;
    private readonly int _count2;

    public UniqueLcs(int[] first, int[] second)
        : this(first, second, 0, first.Length, 0, second.Length)
    {
    }

    public UniqueLcs(int[] first, int[] second, int start1, int count1, int start2, int count2)
    {
        _first = first;
        _second = second;
        _start1 = start1;
        _count1 = count1;
        _start2 = start2;
        _count2 = count2;
    }

    public int[][]? Execute()
    {
        // map: key -> (offset1 + 1); -1 marks non-unique keys
        // match: offset1 -> (offset2 + 1)
        var map = new Dictionary<int, int>(_count1 + _count2);
        var match = new int[_count1];

        for (int i = 0; i < _count1; i++)
        {
            int index = _start1 + i;
            map.TryGetValue(_first[index], out int value);

            if (value == -1) continue;
            if (value == 0)
            {
                map[_first[index]] = i + 1;
            }
            else
            {
                map[_first[index]] = -1;
            }
        }

        int count = 0;
        for (int i = 0; i < _count2; i++)
        {
            int index = _start2 + i;
            map.TryGetValue(_second[index], out int value);

            if (value == 0 || value == -1) continue;
            if (match[value - 1] == 0)
            {
                match[value - 1] = i + 1;
                count++;
            }
            else
            {
                match[value - 1] = 0;
                map[_second[index]] = -1;
                count--;
            }
        }

        if (count == 0)
        {
            return null;
        }

        // Longest increasing subsequence on the unique element pairs.
        var sequence = new int[count];
        var lastElement = new int[count];
        var predecessor = new int[_count1];

        int length = 0;
        for (int i = 0; i < _count1; i++)
        {
            if (match[i] == 0) continue;

            int j = BinarySearch(sequence, match[i], length);
            if (j == length || match[i] < sequence[j])
            {
                sequence[j] = match[i];
                lastElement[j] = i;
                predecessor[i] = j > 0 ? lastElement[j - 1] : -1;
                if (j == length)
                {
                    length++;
                }
            }
        }

        var ret = new[] { new int[length], new int[length] };

        int pos = length - 1;
        int curr = lastElement[length - 1];
        while (curr != -1)
        {
            ret[0][pos] = curr;
            ret[1][pos] = match[curr] - 1;
            pos--;
            curr = predecessor[curr];
        }

        return ret;
    }

    // find max i: a[i] < value; return i + 1 (the value itself is never present)
    private static int BinarySearch(int[] sequence, int value, int length)
    {
        int i = Array.BinarySearch(sequence, 0, length, value);
        if (i >= 0) throw new InvalidOperationException("Unique element matched twice");
        return ~i;
    }
}
