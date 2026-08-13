// Ported from JetBrains intellij-community (Apache 2.0):
// platform/util/diff/src/com/intellij/diff/comparison/ComparisonPolicy.kt

namespace WinUI.DiffViewer.Core;

/// <summary>How whitespace is treated while comparing texts.</summary>
public enum ComparisonPolicy
{
    /// <summary>Whitespace matters everywhere ("None" in the IntelliJ UI).</summary>
    Default,

    /// <summary>Leading/trailing whitespace of every line is ignored ("Trim whitespaces").</summary>
    TrimWhitespaces,

    /// <summary>All whitespace is ignored ("Ignore whitespaces").</summary>
    IgnoreWhitespaces,
}
