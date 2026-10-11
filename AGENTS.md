---
project: WinUI.DiffViewer
languages: [C#, XAML]
framework: WinUI 3 / Windows App SDK
build-system: dotnet (MSBuild)
repository: library + gallery sample app
---

# WinUI.DiffViewer — Agent Guide

An editable side-by-side / unified diff viewer control for WinUI 3 in the
JetBrains style. The diff engine is a C# port of IntelliJ's comparison
pipeline (Myers O(ND), word splitting, chunk optimizers) from
[intellij-community](https://github.com/JetBrains/intellij-community)
(Apache 2.0, see `NOTICE`).

## Build & Run

```
dotnet build WinUI.DiffViewer.sln
dotnet run --project samples/WinUI.DiffViewer.SampleApp
```

- Requires Windows + .NET 8 SDK (+ Windows App SDK NuGet, restored automatically).
- If `dotnet` resolves to an SDK-less x86 install, use `C:\Program Files\dotnet\dotnet.exe`.
- A running SampleApp instance locks the output DLLs — stop it before building.
- CI: `.github/workflows/ci-build.yml` builds the solution; `publish-nuget.yml` packs on `v*` tags.

## Layout

```
src/WinUI.DiffViewer/
  Core/        Diff engine — pure C#, NO UI dependencies (see invariants)
  Model/       Row model: DiffRow, DiffRowBuilder (alignment, highlight spans)
  Controls/    DiffViewer (templated control), HighlightableTextBlock, DiffPalette
  Themes/      Generic.xaml (control template, chrome colors)
samples/WinUI.DiffViewer.SampleApp/   Gallery app (one page per feature, SamplePresenter)
docs/ARCHITECTURE.md                  Original -> port mapping, design decisions
```

## Project Invariants

- **`Core/` and `Model/` stay UI-free.** They must compile with the plain .NET
  SDK on any OS — engine verification runs as a console harness that compiles
  `Core/*.cs` directly (`<Compile Include="...\Core\*.cs" />`). Never add
  WinUI/WinRT usings there.
- **The engine is a port, not an invention.** For algorithm changes, consult
  the originals first (a checkout may exist at `F:\Repository\intellij-community`,
  key paths: `platform/util/diff/src/com/intellij/diff/comparison/`,
  `platform/diff-impl/src/com/intellij/diff/tools/`). Port faithfully; state
  every deliberate simplification in the file header comment and, for larger
  deviations, in `docs/ARCHITECTURE.md`.
- **Offset convention:** a `LineFragment`'s end offsets include the closing
  `'\n'` (IntelliJ `getLineEnd(line, includeNewline)`), except for the last
  line of the text. The word splitter depends on this.
- **No hardcoded pixel sizes for text-dependent layout.** All highlight/gutter
  geometry derives from the measured font metrics (`MeasureMetrics`,
  `PaneLineHeight`); widths follow digit count x char width. FontFamily and
  FontSize can change at runtime (settings, Ctrl+wheel zoom).
- **Colors go through `DiffPalette`.** Apps override via `Application.Resources`
  keys (`Diff*RowBrush`, `Diff*WordBrush`, ...). In high-contrast mode no
  custom colors are painted at all.
- **Empty side = full deletion/insertion.** A completely empty text marks the
  whole other side as Deleted/Inserted (deliberate deviation from IntelliJ's
  "one empty line" model — documented in `DiffComputer.Compare`).

## Sample App Rules

- Every feature page follows the `SamplePresenter` pattern: `Header`,
  `Description`, `Example` (the live control), `Options` (controls bound to the
  viewer via `ElementName`), `Xaml`/`CSharp` source strings with
  `$(Key)` substitutions bound `OneWay` to the live property values.
- New page checklist: `Pages/<Name>Page.xaml(.cs)` + nav item in
  `MainWindow.xaml` + entry in the `OnNavigationSelectionChanged` switch +
  `EditorSettings.Apply(viewer)` in the page constructor.
- Demo texts live in `DemoContent.cs` (deterministic — no randomness).
- JetBrains Mono ships under `Assets/Fonts/` (SIL OFL, keep `OFL.txt`).

## After Code Changes

- `dotnet build WinUI.DiffViewer.sln` must pass with 0 warnings.
- Engine changes: run the console test harness over `Core/*.cs` (recreate per
  the invariant above if missing) — it includes IntelliJ's reference case
  `"a\nx\nb\n" vs "!x\n" => del/mod/del`.
- Launch the sample app once as a smoke test (it must survive startup).
- Commits: conventional prefixes (`feat:`/`fix:`/`docs:`/`revert:`), one
  logical change per commit, English messages. README is written in German.
