// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/util/diff/DiffConfig.kt
//
// UNIMPORTANT_LINE_CHAR_COUNT lives in TextUtil.UnimportantLineCharCount
// (kept there because the word/char comparators already reference it).

namespace WinUI.DiffViewer.Core;

internal static class DiffConfig
{
    public const bool UsePatienceAlg = false;
    public const int DeltaThresholdSize = 20000;
    public const int MaxBadLines = 3; // do not try to compare lines by-word after that many prior failures
}
