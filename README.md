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
samples/DiffViewer.Sample/   Beispiel-App (zwei TextBoxen + Viewer + Einstellungen)
docs/ARCHITECTURE.md         Mapping Original → Portierung, Designentscheidungen
```

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
dotnet run --project samples/DiffViewer.Sample   (oder Start über Visual Studio, Plattform x64)
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

## Bewusste Einschränkungen (v0.1)

- **Read-only**: kein Editieren, keine Accept/Append-Chevrons, kein Merge-Modus.
- Kein Folding („Collapse Unchanged Fragments"), keine Kurven im Divider.
- Kein Syntax-Highlighting (IntelliJ bekommt das gratis durch echte Editor-Instanzen).
- Vereinfachungen in der Engine gegenüber dem Original sind in `docs/ARCHITECTURE.md` dokumentiert.

## Lizenz

Apache License 2.0 — siehe `LICENSE`. Enthält portierten Code aus JetBrains intellij-community, siehe `NOTICE`.
