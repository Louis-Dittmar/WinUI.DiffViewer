// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/ByWordRt.kt (compareAndSplit)
// platform/util/diff/src/com/intellij/diff/comparison/LineFragmentSplitter.kt
//
// Splits one big "Modified" line block into several line fragments by
// correlating the words of both sides: sub-blocks whose words did not match
// anything become their own Deleted/Inserted fragments instead of being glued
// into one Modified block. This is what makes a deleted region inside a larger
// changed run show up gray instead of blue.
//
// Simplification relative to the original: inner fragments are computed by
// re-running the word/char comparator per sub-block instead of slicing the
// global word diff (SubiterableDiffIterable) — the results are equivalent
// because block boundaries are line-aligned.

using System.Collections.Generic;

namespace WinUI.DiffViewer.Core;

/// <summary>A word or newline chunk (port of ByWordRt.InlineChunk). Offsets refer to the source text.</summary>
internal readonly struct InlineChunk
{
    public readonly int Start;
    public readonly int End;
    public readonly int Hash;
    public readonly bool IsNewline;

    public InlineChunk(int start, int end, int hash, bool isNewline)
    {
        Start = start;
        End = end;
        Hash = hash;
        IsNewline = isNewline;
    }
}

/// <summary>An InlineChunk together with its source text, so equality can compare content.</summary>
internal readonly struct ChunkRef
{
    public readonly string Text;
    public readonly InlineChunk Chunk;

    public ChunkRef(string text, InlineChunk chunk)
    {
        Text = text;
        Chunk = chunk;
    }
}

internal sealed class ChunkRefComparer : IEqualityComparer<ChunkRef>
{
    public static readonly ChunkRefComparer Instance = new();

    public bool Equals(ChunkRef a, ChunkRef b)
    {
        // All newline chunks are equal to each other (ByWordRt.NewlineChunk.equals).
        if (a.Chunk.IsNewline || b.Chunk.IsNewline) return a.Chunk.IsNewline == b.Chunk.IsNewline;

        if (a.Chunk.Hash != b.Chunk.Hash) return false;
        int len1 = a.Chunk.End - a.Chunk.Start;
        int len2 = b.Chunk.End - b.Chunk.Start;
        if (len1 != len2) return false;
        for (int i = 0; i < len1; i++)
        {
            if (a.Text[a.Chunk.Start + i] != b.Text[b.Chunk.Start + i]) return false;
        }
        return true;
    }

    public int GetHashCode(ChunkRef r) => r.Chunk.IsNewline ? '\n' : r.Chunk.Hash;
}

internal static class ByWordSplitter
{
    /// <summary>
    /// Port of ByWordRt.compareAndSplit for one Modified fragment: returns the
    /// fragment split into one or more correctly typed line fragments, with
    /// inner word/char highlights per Modified sub-fragment.
    /// </summary>
    public static List<LineFragment> CompareAndSplit(
        string text1, string text2,
        LineFragment fragment,
        ComparisonPolicy policy, HighlightMode mode)
    {
        var words1 = GetInlineChunks(text1);
        var words2 = GetInlineChunks(text2);

        var refs1 = ToRefs(text1, words1);
        var refs2 = ToRefs(text2, words2);

        var wordChanges = DiffAlgorithm.Diff(refs1, refs2, ChunkRefComparer.Instance);
        wordChanges = new WordChunkOptimizer(refs1, refs2).Build(wordChanges);

        var blocks = new Splitter(text1, text2, words1, words2, wordChanges).Run();
        if (blocks.Count == 0) return new List<LineFragment> { fragment };

        // Port of ComparisonManagerImpl.createInnerWordFragments: line ranges
        // accumulate the per-block newline counts; the last block inherits the
        // fragment end lines (special case for the trailing line).
        var result = new List<LineFragment>(blocks.Count);
        int startLine1 = fragment.StartLine1;
        int startLine2 = fragment.StartLine2;

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            bool isLast = i == blocks.Count - 1;

            int endLine1 = isLast ? fragment.EndLine1 : startLine1 + CountNewlines(words1, block.WordsStart1, block.WordsEnd1);
            int endLine2 = isLast ? fragment.EndLine2 : startLine2 + CountNewlines(words2, block.WordsStart2, block.WordsEnd2);

            var lineFragment = new LineFragment(
                startLine1, endLine1, startLine2, endLine2,
                fragment.StartOffset1 + block.OffsetStart1, fragment.StartOffset1 + block.OffsetEnd1,
                fragment.StartOffset2 + block.OffsetStart2, fragment.StartOffset2 + block.OffsetEnd2);

            if (lineFragment.Type == LineFragmentType.Modified)
            {
                string sub1 = text1.Substring(block.OffsetStart1, block.OffsetEnd1 - block.OffsetStart1);
                string sub2 = text2.Substring(block.OffsetStart2, block.OffsetEnd2 - block.OffsetStart2);

                var inner = mode == HighlightMode.Characters
                    ? CharComparator.Compare(sub1, sub2, policy)
                    : WordComparator.Compare(sub1, sub2, policy);

                lineFragment.InnerFragments = DropWholeChanged(inner, sub1.Length, sub2.Length);
            }

            result.Add(lineFragment);
            startLine1 = endLine1;
            startLine2 = endLine2;
        }

        return result;
    }

    /// <summary>
    /// Port of LineFragmentImpl.dropWholeChangedFragments: a single inner
    /// fragment covering both sides completely carries no information — null
    /// means "everything changed, no fine highlighting".
    /// </summary>
    private static IReadOnlyList<DiffFragment>? DropWholeChanged(List<DiffFragment> inner, int length1, int length2)
    {
        if (inner.Count == 1 &&
            inner[0].StartOffset1 == 0 && inner[0].EndOffset1 == length1 &&
            inner[0].StartOffset2 == 0 && inner[0].EndOffset2 == length2)
        {
            return null;
        }
        return inner;
    }

    private static List<ChunkRef> ToRefs(string text, List<InlineChunk> chunks)
    {
        var refs = new List<ChunkRef>(chunks.Count);
        foreach (var chunk in chunks)
        {
            refs.Add(new ChunkRef(text, chunk));
        }
        return refs;
    }

    private static int CountNewlines(List<InlineChunk> words, int start, int end)
    {
        int count = 0;
        for (int i = start; i < end; i++)
        {
            if (words[i].IsNewline) count++;
        }
        return count;
    }

    // -----------------------------------------------------------------
    // Tokenizer — port of ByWordRt.getInlineChunks: only words and newlines
    // are chunks; punctuation and whitespace lie between them.
    // -----------------------------------------------------------------

    internal static List<InlineChunk> GetInlineChunks(string text)
    {
        var chunks = new List<InlineChunk>();

        int len = text.Length;
        int offset = 0;

        int wordStart = -1;
        int wordHash = 0;

        while (offset < len)
        {
            int ch = char.ConvertToUtf32(text, offset);
            int charCount = char.IsSurrogatePair(text, offset) ? 2 : 1;

            bool isAlpha = TextUtil.IsAlpha(ch);
            bool isWordPart = isAlpha && !TextUtil.IsContinuousScript(ch);

            if (isWordPart)
            {
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
                    chunks.Add(new InlineChunk(wordStart, offset, wordHash, isNewline: false));
                    wordStart = -1;
                }

                if (isAlpha)
                {
                    // continuous script: each code point is its own word
                    chunks.Add(new InlineChunk(offset, offset + charCount, ch, isNewline: false));
                }
                else if (ch == '\n')
                {
                    chunks.Add(new InlineChunk(offset, offset + 1, '\n', isNewline: true));
                }
            }

            offset += charCount;
        }

        if (wordStart != -1)
        {
            chunks.Add(new InlineChunk(wordStart, len, wordHash, isNewline: false));
        }

        return chunks;
    }

    // -----------------------------------------------------------------
    // Port of LineFragmentSplitter
    // -----------------------------------------------------------------

    internal readonly struct WordBlock
    {
        public readonly int WordsStart1, WordsEnd1, WordsStart2, WordsEnd2;
        public readonly int OffsetStart1, OffsetEnd1, OffsetStart2, OffsetEnd2;

        public WordBlock(
            int wordsStart1, int wordsEnd1, int wordsStart2, int wordsEnd2,
            int offsetStart1, int offsetEnd1, int offsetStart2, int offsetEnd2)
        {
            WordsStart1 = wordsStart1;
            WordsEnd1 = wordsEnd1;
            WordsStart2 = wordsStart2;
            WordsEnd2 = wordsEnd2;
            OffsetStart1 = offsetStart1;
            OffsetEnd1 = offsetEnd1;
            OffsetStart2 = offsetStart2;
            OffsetEnd2 = offsetEnd2;
        }
    }

    private sealed class PendingChunk
    {
        public WordBlock Block;
        public bool HasEqualWords;
        public bool HasWordsInside;
        public bool IsEqualIgnoreWhitespaces;

        public PendingChunk(WordBlock block, bool hasEqualWords, bool hasWordsInside, bool isEqualIgnoreWhitespaces)
        {
            Block = block;
            HasEqualWords = hasEqualWords;
            HasWordsInside = hasWordsInside;
            IsEqualIgnoreWhitespaces = isEqualIgnoreWhitespaces;
        }
    }

    private sealed class Splitter
    {
        // Index convention (from the original):
        // -1 - the beginning of file, words.Count - end of file,
        // everything in between - InlineChunks (words or newlines).

        private readonly string _text1;
        private readonly string _text2;
        private readonly List<InlineChunk> _words1;
        private readonly List<InlineChunk> _words2;
        private readonly List<DiffRange> _wordChanges;

        private readonly List<WordBlock> _result = new();
        private int _last1 = -1;
        private int _last2 = -1;
        private PendingChunk? _pending;

        public Splitter(string text1, string text2, List<InlineChunk> words1, List<InlineChunk> words2, List<DiffRange> wordChanges)
        {
            _text1 = text1;
            _text2 = text2;
            _words1 = words1;
            _words2 = words2;
            _wordChanges = wordChanges;
        }

        public List<WordBlock> Run()
        {
            bool hasEqualWords = false;
            foreach (var range in DiffAlgorithm.IterateUnchanged(_wordChanges, _words1.Count, _words2.Count))
            {
                int count = range.End1 - range.Start1;
                for (int i = 0; i < count; i++)
                {
                    int index1 = range.Start1 + i;
                    int index2 = range.Start2 + i;

                    if (_words1[index1].IsNewline && _words2[index2].IsNewline)
                    {
                        // A matched newline pair: a line boundary both sides agree on.
                        AddLineChunk(index1, index2, hasEqualWords);
                        hasEqualWords = false;
                    }
                    else
                    {
                        if (IsFirstInLine(_words1, index1) && IsFirstInLine(_words2, index2))
                        {
                            // A matched word pair that starts its line on both
                            // sides: split before this line (at the preceding newlines).
                            AddLineChunk(index1 - 1, index2 - 1, hasEqualWords);
                            hasEqualWords = false;
                        }
                        hasEqualWords = true;
                    }
                }
            }

            AddLineChunk(_words1.Count, _words2.Count, hasEqualWords);

            if (_pending != null) _result.Add(_pending.Block);

            return _result;
        }

        private static bool IsFirstInLine(List<InlineChunk> words, int index)
        {
            return index == 0 || words[index - 1].IsNewline;
        }

        private void AddLineChunk(int end1, int end2, bool hasEqualWords)
        {
            if (_last1 > end1 || _last2 > end2) return;

            var chunk = CreateChunk(_last1, _last2, end1, end2, hasEqualWords);
            if (chunk.Block.OffsetStart1 == chunk.Block.OffsetEnd1 &&
                chunk.Block.OffsetStart2 == chunk.Block.OffsetEnd2)
            {
                return; // empty on both sides — discard
            }

            if (_pending == null)
            {
                _pending = chunk;
            }
            else if (ShouldMergeChunks(_pending, chunk))
            {
                _pending = MergeChunks(_pending, chunk);
            }
            else
            {
                _result.Add(_pending.Block);
                _pending = chunk;
            }

            _last1 = end1;
            _last2 = end2;
        }

        private PendingChunk CreateChunk(int start1, int start2, int end1, int end2, bool hasEqualWords)
        {
            // Start/end chunks are newlines (or file start/end) by construction,
            // so all block offsets are aligned to line starts.
            int startOffset1 = GetOffset(_words1, _text1, start1);
            int startOffset2 = GetOffset(_words2, _text2, start2);
            int endOffset1 = GetOffset(_words1, _text1, end1);
            int endOffset2 = GetOffset(_words2, _text2, end2);

            // The opening newline does not belong to the block, the closing one does.
            int wordsStart1 = System.Math.Max(0, start1 + 1);
            int wordsStart2 = System.Math.Max(0, start2 + 1);
            int wordsEnd1 = System.Math.Min(end1 + 1, _words1.Count);
            int wordsEnd2 = System.Math.Min(end2 + 1, _words2.Count);

            var block = new WordBlock(
                wordsStart1, wordsEnd1, wordsStart2, wordsEnd2,
                startOffset1, endOffset1, startOffset2, endOffset2);

            return new PendingChunk(
                block,
                hasEqualWords,
                HasWordsInside(block),
                IsEqualIgnoreWhitespaces(block));
        }

        private static int GetOffset(List<InlineChunk> words, string text, int index)
        {
            if (index == -1) return 0;
            if (index == words.Count) return text.Length;
            // By construction this chunk is a newline; the offset is right after it.
            return words[index].End;
        }

        private bool HasWordsInside(WordBlock block)
        {
            for (int i = block.WordsStart1; i < block.WordsEnd1; i++)
            {
                if (!_words1[i].IsNewline) return true;
            }
            for (int i = block.WordsStart2; i < block.WordsEnd2; i++)
            {
                if (!_words2[i].IsNewline) return true;
            }
            return false;
        }

        private bool IsEqualIgnoreWhitespaces(WordBlock block)
        {
            string sub1 = _text1.Substring(block.OffsetStart1, block.OffsetEnd1 - block.OffsetStart1);
            string sub2 = _text2.Substring(block.OffsetStart2, block.OffsetEnd2 - block.OffsetStart2);
            return TextUtil.EqualsIgnoreWhitespaces(sub1, sub2);
        }

        private static bool ShouldMergeChunks(PendingChunk chunk1, PendingChunk chunk2)
        {
            if (!chunk1.HasEqualWords && !chunk2.HasEqualWords) return true; // combine lines, that matched only by '\n'

            if (chunk1.IsEqualIgnoreWhitespaces && chunk2.IsEqualIgnoreWhitespaces) return true; // combine whitespace-only changed lines

            if (!chunk1.HasWordsInside || !chunk2.HasWordsInside) return true; // squash block without words in it

            return false;
        }

        private static PendingChunk MergeChunks(PendingChunk chunk1, PendingChunk chunk2)
        {
            var b1 = chunk1.Block;
            var b2 = chunk2.Block;

            var merged = new WordBlock(
                b1.WordsStart1, b2.WordsEnd1, b1.WordsStart2, b2.WordsEnd2,
                b1.OffsetStart1, b2.OffsetEnd1, b1.OffsetStart2, b2.OffsetEnd2);

            return new PendingChunk(
                merged,
                chunk1.HasEqualWords || chunk2.HasEqualWords,
                chunk1.HasWordsInside || chunk2.HasWordsInside,
                chunk1.IsEqualIgnoreWhitespaces && chunk2.IsEqualIgnoreWhitespaces);
        }
    }
}
