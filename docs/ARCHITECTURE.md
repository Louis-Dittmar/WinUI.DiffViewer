# Architektur & Herkunft des Codes

Dieses Dokument beschreibt, wie WinUI.DiffViewer aus dem IntelliJ-Diff-Viewer
(`intellij-community`, Apache 2.0) abgeleitet ist: was 1:1 portiert wurde, was
vereinfacht wurde und wie die WinUI-Schicht die IntelliJ-UI nachbildet.

## Schichten

```
DiffViewer (Templated Control)          ← UI, entspricht SimpleDiffViewer/UnifiedDiffViewer
   │  ItemsRepeater-Zeilen, TextHighlighter, Sync-Scroll
DiffRowBuilder / DiffRow                ← Zeilenmodell, entspricht SimpleDiffChange + AlignedDiffModel
   │
DiffComputer (Facade)                   ← entspricht ComparisonManagerImpl.compareLinesInner
   ├── LineComparator  ← ByLineRt.kt
   ├── WordComparator  ← ByWordRt.kt
   ├── CharComparator  ← ByCharRt.kt
   └── DiffAlgorithm   ← Diff.kt + DiffIterableUtil.kt
         └── MyersLcs  ← MyersLCS.kt   (originalgetreue Portierung)
```

## Mapping: Originaldatei → C#-Datei

| Original (intellij-community) | C# | Treue |
|---|---|---|
| `platform/util/diff/.../util/diff/MyersLCS.kt` | `Core/MyersLcs.cs` | **1:1-Portierung** (Linear-Space-Myers, Vorwärts-/Rückwärts-k-Linien, Middle-Snake-Rekursion, identische Schwellwerte) |
| `platform/util/diff/.../util/diff/BitSet.kt` | `Core/DiffBitSet.cs` | Minimal-Subset (Set/Get/Bereichs-Set) |
| `platform/util/diff/.../util/diff/Diff.kt` | `Core/DiffAlgorithm.cs` | Volle Pipeline: Präfix/Suffix-Trim, Enumeration, `Reindexer.discardUnique`, Myers mit Threshold (`executeWithThreshold`) und `PatienceIntLCS`-Fallback, `Reindexer.reindex`. **Abweichungen:** (1) schlägt auch der Patience-Fallback fehl, liefern wir dessen ungeprüftes Ergebnis statt `FilesTooBigForDiffException` (ein Editor-Control darf den Diff nicht verweigern); (2) `NormalizeMatchPositions` schiebt gematchte Läufe an die früheste äquivalente Position (git-Konvention) — der bidirektionale Linear-Space-Myers wählt bei wiederholten Blöcken sonst ein mittleres Vorkommen, wodurch ein duplizierter Block als Riesen-Änderung statt als Einfügung erscheint |
| `.../util/diff/Reindexer.kt`, `LCSBuilder.kt` | `Core/Reindexer.cs` | 1:1 (BitSet-Reindex, `ILcsBuilder`) |
| `.../util/diff/PatienceIntLCS.kt`, `UniqueLCS.kt` | `Core/PatienceIntLcs.cs`, `Core/UniqueLcs.cs` | 1:1 (Unique-Anker + LIS, Rekursion in die Lücken, `checkReduction`) |
| `.../util/diff/DiffConfig.kt` | `Core/DiffConfig.cs` | Konstanten; `UNIMPORTANT_LINE_CHAR_COUNT` liegt weiterhin in `TextUtil` |
| `.../comparison/ComparisonPolicy.kt` | `Core/ComparisonPolicy.cs` | 1:1 |
| `.../comparison/ComparisonUtil.kt` | `Core/TextUtil.cs` | Policy-Equality + Java-kompatible Hashes 1:1 (inkl. der Spezialsemantik von `equalsTrimWhitespaces` für innere Zeilen) |
| `.../comparison/TrimUtil.kt` | `Core/TextUtil.cs` | Zeichenklassen (`isPunctuation`, `isAlpha`, `isContinuousScript`) portiert; Unicode-Bereiche über .NET-`UnicodeCategory` statt eingebetteter UCD-Tabellen |
| `.../comparison/ByLineRt.kt` | `Core/LineComparator.cs` | Volle `doCompare`-Pipeline: `Line`-Klasse, `compareSmart` (Unimportant-Lines-Pass), `correctChangesSecondStep` inkl. `getBestMatchingAlignment` (IW-zuerst-Matching) und `LineChunkOptimizer`. **Weggelassen:** nur die 3-Wege-Merge-Einstiege |
| `.../comparison/ChangeCorrector.kt` | `Core/ChangeCorrector.cs` | `SmartLineChangeCorrector` 1:1 (Basis + Line-Variante); `DefaultCharChangeCorrector` nicht portiert (gehört zu ByCharRt.compareTwoStep) |
| `.../comparison/iterables/DiffIterableUtil.kt` (Builder) | `Core/ChangeBuilder.cs` | `ChangeBuilder`/`ExpandChangeBuilder` über `List<DiffRange>` statt `DiffIterable`; `TrimUtil.expand` für Listen als `DiffTrimUtil.Expand` |
| `.../comparison/ByWordRt.kt` | `Core/WordComparator.cs` | Tokenizer (`getInlineChunks`) 1:1 (Wort-Runs, Continuous-Scripts als Einzel-Codepoints, Newline-Chunks). **Vereinfacht:** statt `AdjustmentPunctuationMatcher` + `*SpacesCorrector` werden Interpunktion und Whitespace-Runs mit-tokenisiert und per Policy gefiltert |
| `.../comparison/ByCharRt.kt` | `Core/CharComparator.cs` | Codepoint-Diff (surrogatsicher). **Vereinfacht:** einstufig statt `compareTwoStep` |
| `.../fragments/DiffFragment.kt`, `LineFragment.kt` | `Core/DiffFragment.cs` | Struktur 1:1 (InnerFragments relativ zum Fragment) |
| `platform/diff-impl/.../ComparisonManagerImpl.java` | `Core/DiffComputer.cs` | `compareLinesInner`-Ablauf: Zeilen-Diff → LineFragments → Inner-Fragments je nach HighlightMode; Größenlimit statt `MAX_BAD_LINES` |
| `platform/diff-impl/.../simple/AlignedDiffModel.kt` | `Model/DiffRowBuilder.cs` | Alignment über Füllzeilen statt Editor-Inlays (WinUI-Ansatz); dadurch ist Sync-Scroll exakt |
| `platform/diff-impl/.../util/DiffDrawUtil.java` | `Controls/DiffPalette.cs` + `Themes/Generic.xaml` | Farb-Rollen (Inserted/Modified/Deleted, Zeile vs. Wort) übernommen; Rendering über Row-Backgrounds + `TextHighlighter` statt RangeHighlighter-Ebenen. Die Brushes werden in `DiffViewer.StyleRow` (ItemsRepeater.ElementPrepared) anhand von `ActualTheme` gesetzt und bei `ActualThemeChanged` neu angewendet — so folgen sie Element-Themes und Laufzeit-Theme-Wechseln; im High-Contrast-Modus werden keine eigenen Hintergründe gemalt |
| `platform/diff-impl/.../util/SyncScrollSupport.java` | `Controls/DiffViewer.cs` (SyncScroll) | Trivialfall genügt: durch Füllzeilen sind beide Panes gleich hoch |

## Verifikation

Die `Core`-Schicht wurde vor der Auslieferung durch eine zeilengetreue
Transliteration nach Python gegen eine Reihe von Testfällen geprüft
(Basis-Diffs, Insert/Delete/Modify, Whitespace-Policies, Wort-Highlights,
Surrogate-Pairs, leere Eingaben). Die Python-Transliteration diente nur der
Verifikation der Übersetzungslogik und ist nicht Teil des Pakets.

## Bekannte Abweichungen vom Original

1. **Chunk-Optimierung fehlt** — IntelliJ verschiebt bei mehrdeutigen Diffs die
   Blockgrenzen an „schöne" Stellen (Leerzeilen, Klammerzeilen). Ohne
   `LineChunkOptimizer` kann die Blockwahl gelegentlich eine andere (gleich
   große, aber weniger hübsche) sein.
2. **IW-zuerst-Matching fehlt** — IntelliJ matcht erst whitespace-ignorierend
   und verfeinert dann. Bei `Default`-Policy kann das Original deshalb Zeilen
   paaren, die sich nur in Einrückung unterscheiden; die Portierung zeigt sie
   als gelöscht+eingefügt.
3. **Wort-Diff-Nachbearbeitung vereinfacht** — Interpunktions-Matching über
   `ByChar` auf den Lücken sowie die drei Whitespace-Korrektoren sind durch
   Mit-Tokenisierung ersetzt. Die Highlights sind sehr ähnlich, im Detail aber
   nicht immer identisch mit IntelliJ.
4. **Kein Patience-Fallback** — irrelevant für UI-Größen; `ExecuteLinear`
   begrenzt D auf `20000 + 10·√N` und liefert dann ein gröberes, aber gültiges
   Ergebnis statt einer Exception.
5. **Kein Merge, kein Folding, kein Editieren** — bewusst außerhalb des Scopes
   von v0.1.
