// Modeled after JetBrains intellij-community (Apache 2.0):
// platform/diff-impl/src/com/intellij/diff/tools/util/base/HighlightPolicy.java

namespace WinUI.DiffViewer.Core;

/// <summary>Granularity of difference highlighting inside changed lines.</summary>
public enum HighlightMode
{
    /// <summary>Only whole lines are highlighted.</summary>
    Lines,

    /// <summary>Changed words are additionally highlighted inside modified lines (default).</summary>
    Words,

    /// <summary>Changed characters are additionally highlighted inside modified lines.</summary>
    Characters,

    /// <summary>No highlighting at all.</summary>
    None,
}
