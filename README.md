# WinUI.DiffViewer

This is a Diff Viewer Control for WinUI 3. It is a reusable class library with a templated control that compares two texts and displays the differences, similar to the JetBrains/IntelliJ Diff Viewer. It supports side-by-side or unified views with line and word highlighting, line number gutters and synchronous scrolling.

The diff engine is a C# port of the IntelliJ algorithm from IntelliJ Community (Apache 2.0 licence; see NOTICE). It features Myers O(ND) LCS, line/word/character-based comparison and whitespace ignore policies. And Currently has some MAJOR Bugs.

## Features

| Feature | IntelliJ Counterpart |
| --- | --- |
| Side-by-Side and Unified mode | `SimpleDiffViewer` / `UnifiedDiffViewer` |
| Green = added, Blue = modified, Gray = deleted | `DiffColors` (DIFF_INSERTED/MODIFIED/DELETED) |
| Word/Character highlighting in modified lines | `ByWordRt` / `ByCharRt` + `InlineHighlighterBuilder` |
| Ignore Differences: None / Trim / Ignore whitespaces | `ComparisonPolicy` |
| Highlighting: Lines / Words / Characters / None | `HighlightPolicy` |
| Aligned panes (filler lines) + synchronous scrolling | `AlignedDiffModel` + `SyncScrollSupport` |
| Background thread calculation, latest-wins | `DiffViewerBase.rediff()` |
| Light/Dark/HighContrast palettes | Editor color schemes |
| Block splitting: Deletions/Insertions in large modification blocks become separate fragments | `ByWordRt.compareAndSplit` + `LineFragmentSplitter` |
| Boundary correction (changes snap to empty lines/whitespace) | `ChunkOptimizer` (Line + Word) |
| Ctrl+Mouse wheel zoom, FontFamily/FontSize switchable live | Editor zoom |



## Usage

```xml
<Page xmlns:dv="using:WinUI.DiffViewer.Controls">
    <dv:DiffViewer x:Name="Viewer"
                   ViewMode="SideBySide"
                   IgnorePolicy="Default"
                   HighlightMode="Words" />
</Page>

```

```csharp
Viewer.OldText = File.ReadAllText("old.cs");
Viewer.NewText = File.ReadAllText("new.cs");
// All properties are DependencyProperties; every change triggers a diff recalculation.

```

The engine can also be used without a UI:

```csharp
using WinUI.DiffViewer.Core;

var result = DiffComputer.Compare(oldText, newText, new DiffOptions {
    Policy = ComparisonPolicy.IgnoreWhitespaces,
    HighlightMode = HighlightMode.Words,
});
foreach (LineFragment f in result.Fragments)
    Console.WriteLine($"{f.Type}: {f}");

```

## Project Structure

```
src/WinUI.DiffViewer/
  Core/        Diff engine (pure C#, no UI dependency other than the project TFM)
  Model/       Line model: DiffRow, DiffRowBuilder (alignment, highlight spans)
  Controls/    DiffViewer (Templated Control), HighlightableTextBlock, Converters
  Themes/      Generic.xaml (Template + color palettes)
samples/WinUI.DiffViewer.SampleApp/  Gallery sample app (see below)
docs/ARCHITECTURE.md                 Mapping Original -> Port, design decisions

```

## Sample App

`samples/WinUI.DiffViewer.SampleApp` is a gallery where you can test the feature. On the left is a navigation view with one page per feature. On the right is the live example with an options panel that binds the control properties directly.


| Page | Demonstrates |
| --- | --- |
| Overview | All options in one example (View mode, Highlighting, Ignore, Sync-Scroll) |
| View Modes | `ViewMode`: SideBySide vs. Unified |
| Highlighting | `HighlightMode`: Lines / Words / Characters / None |
| Whitespace Handling | `IgnorePolicy`: Default / TrimWhitespaces / IgnoreWhitespaces |
| Live Editing | Editable panes, `DiffComputed` event log |
| Synchronized Scrolling | Documents of different lengths, scroll mapping via change anchors |
| Large Files | Generated large documents + computation time |
| Settings | App theme, editor font (Consolas/Cascadia/JetBrains Mono) + font size, version, links |


## Build

Requirements: Windows 10/11 and Visual Studio 2022 with the "WinUI application development" workload (or .NET 8 SDK + Windows App SDK 1.5).

```
dotnet build WinUI.DiffViewer.sln
dotnet run --project samples/WinUI.DiffViewer.SampleApp  (or start via Visual Studio, platform x64.)
```

## Customizing Colors

The line and word backgrounds are set via code-behind (`DiffPalette`) based on the `ActualTheme`. Thus, they respect window/element themes and runtime theme changes, and they disable themselves in High Contrast mode (system colors remain readable). You can define your own colors in `Application.Resources`; they always take precedence:

```xml
<Application.Resources>
    <SolidColorBrush x:Key="DiffInsertedRowBrush" Color="#E6FFEC" />
    <SolidColorBrush x:Key="DiffInsertedWordBrush" Color="#ABF2BC" />
    <!-- ebenso: DiffDeletedRowBrush/-WordBrush, DiffModifiedRowBrush/-WordBrush, DiffFillerRowBrush -->
</Application.Resources>
```

## Known Limitations

* No Accept/Append chevrons, no merge mode (editing in the panes is supported since v0.2).
* No syntax highlighting (IntelliJ gets this for free through real editor instances).
* Simplifications in the engine compared to the original are documented in `docs/ARCHITECTURE.md`.

## License
Apache License 2.0, see `LICENSE`. Contains ported code from JetBrains intellij-community, see `NOTICE`.
