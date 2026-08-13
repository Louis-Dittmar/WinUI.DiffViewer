# WinUI.DiffViewer

Ein **Diff-Viewer-Control für WinUI 3** im Stil von [WinUI.TableView](https://github.com/w-ahmad/WinUI.TableView):
eine wiederverwendbare Klassenbibliothek mit einem Templated Control, das zwei Texte vergleicht und die
Unterschiede wie der JetBrains/IntelliJ-Diff-Viewer darstellt — Side-by-Side oder Unified, mit Zeilen- und
Wort-Highlighting, Zeilennummern-Gutter und synchronem Scrollen.

Die **Diff-Engine ist eine C#-Portierung des IntelliJ-Algorithmus** aus
[intellij-community](https://github.com/JetBrains/intellij-community) (Apache 2.0, siehe `NOTICE`):
Myers-O(ND)-LCS, zeilen-/wort-/zeichenweiser Vergleich und die Whitespace-Ignore-Policies.

## Features

| Feature | IntelliJ-Vorbild |
|---|---|
| Side-by-Side- und Unified-Modus | `SimpleDiffViewer` / `UnifiedDiffViewer` |
| Grün = hinzugefügt, Blau = geändert, Grau = gelöscht | `DiffColors` (DIFF_INSERTED/MODIFIED/DELETED) |
| Wort-/Zeichen-Highlighting in geänderten Zeilen | `ByWordRt` / `ByCharRt` + `InlineHighlighterBuilder` |
| Ignore Differences: None / Trim / Ignore whitespaces | `ComparisonPolicy` |
| Highlighting: Lines / Words / Characters / None | `HighlightPolicy` |
| Ausgerichtete Panes (Füllzeilen) + synchrones Scrollen | `AlignedDiffModel` + `SyncScrollSupport` |
| Berechnung im Hintergrund-Thread, latest-wins | `DiffViewerBase.rediff()` |
| Light/Dark/HighContrast-Paletten | Editor-Farbschemata |
| Block-Splitting: Löschungen/Einfügungen in großen Änderungsblöcken werden eigene Fragmente | `ByWordRt.compareAndSplit` + `LineFragmentSplitter` |
| Grenzen-Korrektur (Änderungen rasten an Leerzeilen/Whitespace ein) | `ChunkOptimizer` (Line + Word) |
| Syntax-Highlighting im Unified-Modus (C#/XML/JSON, Auto-Erkennung) — `LanguageMode` | `SyntaxHighlighter`-Tokenklassen |
| Strg+Mausrad-Zoom, FontFamily/FontSize live umschaltbar | Editor-Zoom |

## Verwendung

```xml
<Page xmlns:dv="using:WinUI.DiffViewer.Controls">
    <dv:DiffViewer x:Name="Viewer"
                   ViewMode="SideBySide"
                   IgnorePolicy="Default"
                   HighlightMode="Words" />
</Page>
```

```csharp
Viewer.OldText = File.ReadAllText("alt.cs");
Viewer.NewText = File.ReadAllText("neu.cs");
// Alle Properties sind DependencyProperties — jede Änderung rechnet den Diff neu.
```

Die Engine ist auch ohne UI nutzbar:

```csharp
using WinUI.DiffViewer.Core;

var result = DiffComputer.Compare(oldText, newText, new DiffOptions {
    Policy = ComparisonPolicy.IgnoreWhitespaces,
    HighlightMode = HighlightMode.Words,
});
foreach (LineFragment f in result.Fragments)
    Console.WriteLine($"{f.Type}: {f}");
```

## Projektstruktur

```
src/WinUI.DiffViewer/
  Core/        Diff-Engine (reines C#, keine UI-Abhängigkeit außer dem Projekt-TFM)
  Model/       Zeilenmodell: DiffRow, DiffRowBuilder (Ausrichtung, Highlight-Spans)
  Controls/    DiffViewer (Templated Control), HighlightableTextBlock, Konverter
  Themes/      Generic.xaml (Template + Farbpaletten)
samples/WinUI.DiffViewer.SampleApp/   Gallery-Sample-App (siehe unten)
docs/ARCHITECTURE.md                  Mapping Original → Portierung, Designentscheidungen
```

## Sample App

`samples/WinUI.DiffViewer.SampleApp` ist eine Gallery-App nach dem Vorbild der
[WinUI.TableView-SampleApp](https://github.com/w-ahmad/WinUI.TableView): links eine
NavigationView mit einer Seite pro Feature, rechts das Live-Beispiel mit einem
Options-Panel, das die Control-Properties direkt bindet — plus aufklappbarem
Quellcode (XAML/C#, mit live eingesetzten Property-Werten), Theme-Toggle pro
Beispiel und GitHub-Links auf die jeweilige Page.

| Seite | Zeigt |
|---|---|
| Overview | Alle Optionen an einem Beispiel (View mode, Highlighting, Ignore, Sync-Scroll) |
| View Modes | `ViewMode`: SideBySide vs. Unified |
| Highlighting | `HighlightMode`: Lines / Words / Characters / None |
| Whitespace Handling | `IgnorePolicy`: Default / TrimWhitespaces / IgnoreWhitespaces |
| Live Editing | Editierbare Panes, `DiffComputed`-Event-Log |
| Synchronized Scrolling | Unterschiedlich lange Dokumente, Scroll-Mapping über Change-Anker |
| Large Files | Generierte Großdokumente + Berechnungszeit |
| Syntax Highlighting | `LanguageMode`: Auto / CSharp / Xml / Json / None (Unified-Modus) |
| Settings | App-Theme, Editor-Font (Consolas/Cascadia/JetBrains Mono) + Schriftgröße, Version, Links |

## Als NuGet-Paket (GitHub Packages)

Der Workflow `.github/workflows/publish-nuget.yml` paketiert die Bibliothek und
veröffentlicht sie in **GitHub Packages**, sobald dieser Ordner als eigenes
GitHub-Repository gepusht und ein Tag `v*` (z. B. `v0.2.0`) gesetzt wird —
alternativ manuell über „Run workflow".

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

## Farben anpassen

Die Zeilen- und Wort-Hintergründe werden per Code-Behind (`DiffPalette`) anhand von
`ActualTheme` gesetzt — sie folgen damit auch Fenster-/Element-Themes, Theme-Wechseln
zur Laufzeit und deaktivieren sich im High-Contrast-Modus (Systemfarben bleiben lesbar).
Eigene Farben definierst du in `Application.Resources`; sie haben immer Vorrang:

```xml
<Application.Resources>
    <SolidColorBrush x:Key="DiffInsertedRowBrush" Color="#E6FFEC" />
    <SolidColorBrush x:Key="DiffInsertedWordBrush" Color="#ABF2BC" />
    <!-- ebenso: DiffDeletedRowBrush/-WordBrush, DiffModifiedRowBrush/-WordBrush, DiffFillerRowBrush -->
</Application.Resources>
```

## Bewusste Einschränkungen

- Keine Accept/Append-Chevrons, kein Merge-Modus (Editieren in den Panes geht seit v0.2).
- Kein Folding („Collapse Unchanged Fragments").
- Syntax-Highlighting nur im Unified-Modus: die editierbaren Side-by-Side-Panes sind
  TextBoxen und können keinen mehrfarbigen Text rendern (IntelliJ bekommt das gratis
  durch echte Editor-Instanzen).
- Vereinfachungen in der Engine gegenüber dem Original sind in `docs/ARCHITECTURE.md` dokumentiert.

## Lizenz

Apache License 2.0 — siehe `LICENSE`. Enthält portierten Code aus JetBrains intellij-community, siehe `NOTICE`.
