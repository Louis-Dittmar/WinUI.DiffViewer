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

`samples/WinUI.DiffViewer.SampleApp` is a Gallery app based on the WinUI.TableView.SampleApp: on the left is a NavigationView with one page per feature, and on the right is the live example with an Options panel that directly binds the control properties — plus collapsible source code (XAML/C# with live property values), a theme toggle per example and GitHub links to the respective page.


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

## As a NuGet Package (GitHub Packages)

The workflow `.github/workflows/publish-nuget.yml` packages the library and publishes it to **GitHub Packages** as soon as this folder is pushed as its own GitHub repository and a `v*` tag (e.g., `v0.2.0`) is created. Alternatively, it can be triggered manually via "Run workflow".


Verwenden in einem anderen Projekt (`nuget.config` neben der Solution):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/Louis-Dittmar/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="DEIN_GITHUB_USER" />
      <add key="ClearTextPassword" value="DEIN_PAT_MIT_read:packages" />
    </github>
  </packageSourceCredentials>
</configuration>
```

```xml
<PackageReference Include="WinUI.DiffViewer" Version="0.2.0" />
```

Lokal paketieren: `dotnet pack src/WinUI.DiffViewer/WinUI.DiffViewer.csproj -c Release`

## Bauen

Voraussetzungen: **Windows 10/11, Visual Studio 2022** mit der Workload „WinUI application development“
(bzw. .NET 8 SDK + Windows App SDK 1.5).

```
dotnet build WinUI.DiffViewer.sln
dotnet run --project samples/WinUI.DiffViewer.SampleApp   (oder Start über Visual Studio, Plattform x64)
```

> **Hinweis:** Dieses Projekt wurde in einer Linux-Umgebung erstellt. Die Schichten `Core/` und
> `Model/` wurden dort mit dem .NET-8-SDK **kompiliert (0 Warnungen) und mit einer Testsuite
> verifiziert** — u. a. 1.000 randomisierte Diffs mit LCS-Optimalitätsbeweis gegen eine
> DP-Referenz und 300 randomisierte End-to-End-Rekonstruktionstests. Die WinUI-Schicht
> (`Controls/`, `Themes/`, Sample) benötigt Windows und wurde stattdessen einem detaillierten
> API-Review unterzogen; beim allerersten Build können trotzdem Kleinigkeiten auftreten.

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
