// The control itself — the WinUI counterpart of IntelliJ's SimpleDiffViewer /
// UnifiedDiffViewer (platform/diff-impl/src/com/intellij/diff/tools/...).
//
// Side-by-side mode is laid out like the IntelliJ diff window:
//   [scrollbar] [left editor] [left line numbers | connector curves | right line numbers] [right editor] [scrollbar]
// Both panes are real, editable TextBoxes; the diff highlighting is painted on
// canvases behind the editors and re-computed (debounced, latest-wins) while
// typing. Unified mode stays a read-only row list.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using WinUI.DiffViewer.Core;
using WinUI.DiffViewer.Model;

namespace WinUI.DiffViewer.Controls;

public enum DiffViewMode
{
    /// <summary>Two editable panes with both gutters in the middle (IntelliJ "Side-by-side viewer").</summary>
    SideBySide,

    /// <summary>One read-only pane; deleted lines followed by inserted lines (IntelliJ "Unified viewer").</summary>
    Unified,
}

/// <summary>
/// A diff viewer control for WinUI 3. Set <see cref="OldText"/> and
/// <see cref="NewText"/>; the control computes the diff on a background thread
/// (Myers O(ND) algorithm ported from IntelliJ) and renders side-by-side or
/// unified with line- and word-level highlighting. In side-by-side mode both
/// panes are editable; edits update <see cref="OldText"/>/<see cref="NewText"/>
/// and re-diff live (debounced).
/// </summary>
public sealed class DiffViewer : Control
{
    private const string SideBySideRootPart = "PART_SideBySideRoot";
    private const string UnifiedScrollViewerPart = "PART_UnifiedScrollViewer";
    private const string UnifiedRepeaterPart = "PART_UnifiedRepeater";

    private const string LeftEditorPart = "PART_LeftEditor";
    private const string RightEditorPart = "PART_RightEditor";
    private const string LeftHighlightsPart = "PART_LeftHighlights";
    private const string RightHighlightsPart = "PART_RightHighlights";
    private const string LeftHighlightHostPart = "PART_LeftHighlightHost";
    private const string RightHighlightHostPart = "PART_RightHighlightHost";
    private const string LeftNumbersPart = "PART_LeftNumbers";
    private const string RightNumbersPart = "PART_RightNumbers";
    private const string LeftNumbersHostPart = "PART_LeftNumbersHost";
    private const string RightNumbersHostPart = "PART_RightNumbersHost";
    private const string LeftGutterHighlightsPart = "PART_LeftGutterHighlights";
    private const string RightGutterHighlightsPart = "PART_RightGutterHighlights";
    private const string ConnectorPart = "PART_Connector";
    private const string ConnectorHostPart = "PART_ConnectorHost";
    private const string LeftScrollBarPart = "PART_LeftScrollBar";
    private const string RightScrollBarPart = "PART_RightScrollBar";

    /// <summary>Everything that belongs to one editable pane.</summary>
    private sealed class Pane
    {
        public TextBox? Editor;
        public ScrollViewer? Scroller;          // the TextBox-internal ScrollViewer
        public Canvas? Highlights;
        public FrameworkElement? HighlightHost;
        public TextBlock? Numbers;
        public FrameworkElement? NumbersHost;
        public Canvas? GutterHighlights;
        public ScrollBar? Bar;
        public int GutterLineCount = -1;
    }

    private readonly Pane _left = new();
    private readonly Pane _right = new();

    private FrameworkElement? _sideBySideRoot;
    private ScrollViewer? _unifiedScrollViewer;
    private ItemsRepeater? _unifiedRepeater;
    private Canvas? _connector;
    private FrameworkElement? _connectorHost;

    private readonly AccessibilitySettings _accessibilitySettings = new();

    // Latest-wins recomputation without exceptions: every recompute bumps the
    // version; a finished computation only applies if it is still the newest.
    private int _computeVersion;
    private DispatcherQueueTimer? _debounceTimer;

    private DiffResult? _result;
    private int[]? _lineStarts1;
    private int[]? _lineStarts2;

    // Piecewise-linear mapping between left and right line space, built from
    // the changed blocks — the WinUI counterpart of IntelliJ's SyncScrollSupport:
    // panes with different lengths scroll at different speeds so corresponding
    // changes stay next to each other.
    private List<(double L, double R)>? _syncPoints;

    private bool _pushingToEditors;    // control writes editor.Text (suppress TextChanged feedback)
    private bool _updatingFromEditor;  // editor writes OldText/NewText (suppress push-back)

    private double _measuredLineHeight = 17.0;
    private double _measuredCharWidth = 7.0;
    private bool _metricsMeasured;

    public DiffViewer()
    {
        DefaultStyleKey = typeof(DiffViewer);
        ActualThemeChanged += (_, _) =>
        {
            RestyleUnifiedRows();
            RedrawSideBySide();
        };

        // Font changes invalidate the measured char/line metrics that all
        // highlight geometry is based on.
        RegisterPropertyChangedCallback(FontFamilyProperty, (_, _) => OnFontChanged());
        RegisterPropertyChangedCallback(FontSizeProperty, (_, _) => OnFontChanged());

        // IDE-style zoom: Ctrl + mouse wheel changes the editor font size.
        // handledEventsToo — the editors' inner ScrollViewers swallow wheel
        // events before they would bubble here.
        AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelZoom), handledEventsToo: true);
    }

    private void OnFontChanged()
    {
        _metricsMeasured = false;
        MeasureMetrics();
        RestyleUnifiedRows();
        RedrawSideBySide();
    }

    private void OnPointerWheelZoom(object sender, PointerRoutedEventArgs e)
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if (!ctrl.HasFlag(CoreVirtualKeyStates.Down)) return;

        int delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        if (delta == 0) return;

        double newSize = Math.Clamp(FontSize + (delta > 0 ? 1 : -1), 8, 40);
        if (Math.Abs(newSize - FontSize) > 0.01)
        {
            FontSize = newSize;
        }
        e.Handled = true;
    }

    // -----------------------------------------------------------------
    // Dependency properties
    // -----------------------------------------------------------------

    public static readonly DependencyProperty OldTextProperty = DependencyProperty.Register(
        nameof(OldText), typeof(string), typeof(DiffViewer),
        new PropertyMetadata(string.Empty, OnDiffInputChanged));

    /// <summary>The "before" content (left pane). Updated live when the left pane is edited.</summary>
    public string OldText
    {
        get => (string)GetValue(OldTextProperty);
        set => SetValue(OldTextProperty, value);
    }

    public static readonly DependencyProperty NewTextProperty = DependencyProperty.Register(
        nameof(NewText), typeof(string), typeof(DiffViewer),
        new PropertyMetadata(string.Empty, OnDiffInputChanged));

    /// <summary>The "after" content (right pane). Updated live when the right pane is edited.</summary>
    public string NewText
    {
        get => (string)GetValue(NewTextProperty);
        set => SetValue(NewTextProperty, value);
    }

    public static readonly DependencyProperty ViewModeProperty = DependencyProperty.Register(
        nameof(ViewMode), typeof(DiffViewMode), typeof(DiffViewer),
        new PropertyMetadata(DiffViewMode.SideBySide, OnViewModeChanged));

    public DiffViewMode ViewMode
    {
        get => (DiffViewMode)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    public static readonly DependencyProperty IgnorePolicyProperty = DependencyProperty.Register(
        nameof(IgnorePolicy), typeof(ComparisonPolicy), typeof(DiffViewer),
        new PropertyMetadata(ComparisonPolicy.Default, OnDiffInputChanged));

    /// <summary>Whitespace handling — IntelliJ's "Ignore Differences" setting.</summary>
    public ComparisonPolicy IgnorePolicy
    {
        get => (ComparisonPolicy)GetValue(IgnorePolicyProperty);
        set => SetValue(IgnorePolicyProperty, value);
    }

    public static readonly DependencyProperty HighlightModeProperty = DependencyProperty.Register(
        nameof(HighlightMode), typeof(HighlightMode), typeof(DiffViewer),
        new PropertyMetadata(HighlightMode.Words, OnDiffInputChanged));

    /// <summary>Granularity of inner highlighting — IntelliJ's "Highlighting Differences" setting.</summary>
    public HighlightMode HighlightMode
    {
        get => (HighlightMode)GetValue(HighlightModeProperty);
        set => SetValue(HighlightModeProperty, value);
    }

    public static readonly DependencyProperty SynchronizeScrollingProperty = DependencyProperty.Register(
        nameof(SynchronizeScrolling), typeof(bool), typeof(DiffViewer),
        new PropertyMetadata(true));

    /// <summary>Scroll both panes together (IntelliJ's "Synchronize Scrolling").</summary>
    public bool SynchronizeScrolling
    {
        get => (bool)GetValue(SynchronizeScrollingProperty);
        set => SetValue(SynchronizeScrollingProperty, value);
    }

    /// <summary>Raised after a recomputation finished and the view was updated.</summary>
    public event EventHandler? DiffComputed;

    /// <summary>Raised when the diff computation failed with an exception.</summary>
    public event EventHandler<Exception>? DiffFailed;

    // -----------------------------------------------------------------
    // Template lifecycle
    // -----------------------------------------------------------------

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        DetachPane(_left);
        DetachPane(_right);

        _sideBySideRoot = GetTemplateChild(SideBySideRootPart) as FrameworkElement;
        _unifiedScrollViewer = GetTemplateChild(UnifiedScrollViewerPart) as ScrollViewer;
        _unifiedRepeater = GetTemplateChild(UnifiedRepeaterPart) as ItemsRepeater;
        _connector = GetTemplateChild(ConnectorPart) as Canvas;
        _connectorHost = GetTemplateChild(ConnectorHostPart) as FrameworkElement;

        _left.Editor = GetTemplateChild(LeftEditorPart) as TextBox;
        _left.Highlights = GetTemplateChild(LeftHighlightsPart) as Canvas;
        _left.HighlightHost = GetTemplateChild(LeftHighlightHostPart) as FrameworkElement;
        _left.Numbers = GetTemplateChild(LeftNumbersPart) as TextBlock;
        _left.NumbersHost = GetTemplateChild(LeftNumbersHostPart) as FrameworkElement;
        _left.GutterHighlights = GetTemplateChild(LeftGutterHighlightsPart) as Canvas;
        _left.Bar = GetTemplateChild(LeftScrollBarPart) as ScrollBar;

        _right.Editor = GetTemplateChild(RightEditorPart) as TextBox;
        _right.Highlights = GetTemplateChild(RightHighlightsPart) as Canvas;
        _right.HighlightHost = GetTemplateChild(RightHighlightHostPart) as FrameworkElement;
        _right.Numbers = GetTemplateChild(RightNumbersPart) as TextBlock;
        _right.NumbersHost = GetTemplateChild(RightNumbersHostPart) as FrameworkElement;
        _right.GutterHighlights = GetTemplateChild(RightGutterHighlightsPart) as Canvas;
        _right.Bar = GetTemplateChild(RightScrollBarPart) as ScrollBar;

        if (_unifiedRepeater != null)
        {
            _unifiedRepeater.ElementPrepared -= OnUnifiedRowPrepared;
            _unifiedRepeater.ElementPrepared += OnUnifiedRowPrepared;
        }

        AttachPane(_left, isLeft: true);
        AttachPane(_right, isLeft: false);

        if (_connectorHost != null)
        {
            _connectorHost.SizeChanged -= OnLayoutSizeChanged;
            _connectorHost.SizeChanged += OnLayoutSizeChanged;
        }

        MeasureMetrics();
        PushTextsToEditors();
        ApplyMode(ViewMode);
        RecomputeNow();
    }

    private void AttachPane(Pane pane, bool isLeft)
    {
        if (pane.Editor != null)
        {
            pane.Editor.Loaded += OnEditorLoaded;
            pane.Editor.TextChanged += isLeft ? OnLeftEditorTextChanged : OnRightEditorTextChanged;
        }
        if (pane.HighlightHost != null)
        {
            pane.HighlightHost.SizeChanged += OnLayoutSizeChanged;
        }
        if (pane.NumbersHost != null)
        {
            pane.NumbersHost.SizeChanged += OnLayoutSizeChanged;
        }
        if (pane.Numbers != null)
        {
            pane.Numbers.SizeChanged += OnNumbersSizeChanged;
        }
        if (pane.Bar != null)
        {
            pane.Bar.Scroll += isLeft ? OnLeftBarScroll : OnRightBarScroll;
        }
    }

    private void DetachPane(Pane pane)
    {
        if (pane.Editor != null)
        {
            pane.Editor.Loaded -= OnEditorLoaded;
            pane.Editor.TextChanged -= OnLeftEditorTextChanged;
            pane.Editor.TextChanged -= OnRightEditorTextChanged;
        }
        if (pane.Scroller != null)
        {
            pane.Scroller.ViewChanged -= OnPaneViewChanged;
        }
        if (pane.HighlightHost != null)
        {
            pane.HighlightHost.SizeChanged -= OnLayoutSizeChanged;
        }
        if (pane.NumbersHost != null)
        {
            pane.NumbersHost.SizeChanged -= OnLayoutSizeChanged;
        }
        if (pane.Numbers != null)
        {
            pane.Numbers.SizeChanged -= OnNumbersSizeChanged;
        }
        if (pane.Bar != null)
        {
            pane.Bar.Scroll -= OnLeftBarScroll;
            pane.Bar.Scroll -= OnRightBarScroll;
        }
        pane.Scroller = null;
        pane.GutterLineCount = -1;
    }

    private void OnNumbersSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // The rendered gutter height is the calibration source for the line
        // height — redraw once the new layout is in.
        RedrawSideBySide();
    }

    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        // The TextBox template exists now — grab its internal ScrollViewer.
        var pane = ReferenceEquals(sender, _left.Editor) ? _left : _right;
        if (pane.Scroller != null) pane.Scroller.ViewChanged -= OnPaneViewChanged;

        pane.Scroller = FindDescendantScrollViewer((DependencyObject)sender);
        if (pane.Scroller != null)
        {
            pane.Scroller.ViewChanged += OnPaneViewChanged;
        }
        RedrawSideBySide();
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            var nested = FindDescendantScrollViewer(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private void OnLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement host)
        {
            host.Clip = new RectangleGeometry { Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
        }
        RedrawSideBySide();
    }

    private static void OnDiffInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (DiffViewer)d;
        if (!viewer._updatingFromEditor)
        {
            viewer.PushTextsToEditors();
        }
        viewer.ScheduleRecompute();
    }

    private static void OnViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (DiffViewer)d;
        viewer.ApplyMode(viewer.ViewMode);
        viewer.RecomputeNow();
    }

    private void ApplyMode(DiffViewMode mode)
    {
        bool sideBySide = mode == DiffViewMode.SideBySide;

        if (_sideBySideRoot != null)
        {
            _sideBySideRoot.Visibility = sideBySide ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_unifiedScrollViewer != null)
        {
            _unifiedScrollViewer.Visibility = sideBySide ? Visibility.Collapsed : Visibility.Visible;
        }
        if (!sideBySide && _unifiedRepeater != null)
        {
            // Will be filled by the next recompute.
            _unifiedRepeater.ItemsSource = null;
        }
    }

    // -----------------------------------------------------------------
    // Editing: editor <-> OldText/NewText
    // -----------------------------------------------------------------

    private void OnLeftEditorTextChanged(object sender, TextChangedEventArgs e) => OnEditorTextChanged(_left, isLeft: true);

    private void OnRightEditorTextChanged(object sender, TextChangedEventArgs e) => OnEditorTextChanged(_right, isLeft: false);

    private void OnEditorTextChanged(Pane pane, bool isLeft)
    {
        if (_pushingToEditors || pane.Editor == null) return;

        _updatingFromEditor = true;
        try
        {
            if (isLeft) OldText = pane.Editor.Text;
            else NewText = pane.Editor.Text;
        }
        finally
        {
            _updatingFromEditor = false;
        }
        // ScheduleRecompute already ran via OnDiffInputChanged; nothing else to do.
    }

    private void PushTextsToEditors()
    {
        if (_left.Editor == null && _right.Editor == null) return;

        _pushingToEditors = true;
        try
        {
            PushTextToEditor(_left.Editor, OldText ?? string.Empty);
            PushTextToEditor(_right.Editor, NewText ?? string.Empty);
        }
        finally
        {
            _pushingToEditors = false;
        }
    }

    private static void PushTextToEditor(TextBox? editor, string text)
    {
        if (editor == null) return;

        // TextBox normalizes line breaks to '\r'; compare normalized so we do
        // not reset the caret when the content is effectively unchanged.
        string current = DiffComputer.NormalizeLineSeparators(editor.Text);
        string target = DiffComputer.NormalizeLineSeparators(text);
        if (!string.Equals(current, target, StringComparison.Ordinal))
        {
            editor.Text = text;
        }
    }

    // -----------------------------------------------------------------
    // Diff computation (background thread, debounced, latest-wins — no
    // exception-based cancellation, so nothing for a debugger to break on)
    // -----------------------------------------------------------------

    private void ScheduleRecompute()
    {
        if (_debounceTimer == null)
        {
            var queue = DispatcherQueue;
            if (queue == null)
            {
                RecomputeNow();
                return;
            }
            _debounceTimer = queue.CreateTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(250);
            _debounceTimer.IsRepeating = false;
            _debounceTimer.Tick += (_, _) => RecomputeNow();
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private async void RecomputeNow()
    {
        _debounceTimer?.Stop();

        int version = ++_computeVersion;

        string oldText = OldText ?? string.Empty;
        string newText = NewText ?? string.Empty;
        var options = new DiffOptions
        {
            Policy = IgnorePolicy,
            HighlightMode = HighlightMode,
        };
        var mode = ViewMode;

        DiffResult? result = null;
        int[]? starts1 = null;
        int[]? starts2 = null;
        IReadOnlyList<DiffRow>? unifiedRows = null;

        try
        {
            await Task.Run(() =>
            {
                result = DiffComputer.Compare(oldText, newText, options);
                starts1 = BuildLineStarts(result.Lines1);
                starts2 = BuildLineStarts(result.Lines2);
                if (mode == DiffViewMode.Unified)
                {
                    unifiedRows = DiffRowBuilder.BuildUnified(result);
                }
            });
        }
        catch (Exception ex)
        {
            if (version == _computeVersion)
            {
                DiffFailed?.Invoke(this, ex);
            }
            return;
        }

        // A newer computation was started while this one ran — discard quietly.
        if (version != _computeVersion || result == null) return;

        _result = result;
        _lineStarts1 = starts1;
        _lineStarts2 = starts2;

        // Anchor pairs for different-speed scrolling: document start, both
        // edges of every changed block, document end. Unchanged runs map 1:1,
        // changed blocks map n:m (proportionally).
        var points = new List<(double L, double R)>(result.Fragments.Count * 2 + 2) { (0, 0) };
        foreach (var fragment in result.Fragments)
        {
            points.Add((fragment.StartLine1, fragment.StartLine2));
            points.Add((fragment.EndLine1, fragment.EndLine2));
        }
        points.Add((result.Lines1.Count, result.Lines2.Count));
        _syncPoints = points;

        if (mode == DiffViewMode.Unified)
        {
            if (_unifiedRepeater != null) _unifiedRepeater.ItemsSource = unifiedRows;
        }
        else
        {
            RedrawSideBySide();
        }

        DiffComputed?.Invoke(this, EventArgs.Empty);
    }

    private static int[] BuildLineStarts(IReadOnlyList<string> lines)
    {
        var starts = new int[lines.Count + 1];
        int acc = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            starts[i] = acc;
            acc += lines[i].Length + 1; // +1 for '\n'
        }
        starts[lines.Count] = acc;
        return starts;
    }

    // -----------------------------------------------------------------
    // Scrolling: inner ScrollViewers <-> outer ScrollBars, pane sync
    // -----------------------------------------------------------------

    private void OnPaneViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        var source = sender as ScrollViewer;
        if (source == null) return;

        bool sourceIsLeft = ReferenceEquals(source, _left.Scroller);
        var (pane, other) = sourceIsLeft ? (_left, _right) : (_right, _left);

        UpdateScrollBar(pane);

        if (SynchronizeScrolling && other.Scroller != null)
        {
            // IntelliJ-SyncScrollSupport behavior: the panes scroll at
            // different speeds. The viewport-top line is mapped through the
            // change anchors (piecewise linear), so corresponding blocks stay
            // side by side even when the documents have different lengths.
            double sourceLine = source.VerticalOffset / PaneLineHeight(pane);
            double targetLine = MapLine(sourceLine, leftToRight: sourceIsLeft);
            double target = targetLine * PaneLineHeight(other);
            target = Math.Max(0, Math.Min(target, other.Scroller.ScrollableHeight));

            // The echo ViewChanged from the target pane is suppressed by the
            // value check — the mapping is exactly invertible, so the echo
            // computes the original offset back and stops.
            if (Math.Abs(other.Scroller.VerticalOffset - target) >= 0.75)
            {
                other.Scroller.ChangeView(null, target, null, disableAnimation: true);
            }
        }

        RedrawSideBySide();
    }

    /// <summary>Maps a (fractional) line number from one side to the other via the anchor points.</summary>
    private double MapLine(double line, bool leftToRight)
    {
        var points = _syncPoints;
        if (points == null || points.Count < 2) return line;

        if (line <= 0) return 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            double srcA = leftToRight ? points[i].L : points[i].R;
            double srcB = leftToRight ? points[i + 1].L : points[i + 1].R;
            double dstA = leftToRight ? points[i].R : points[i].L;
            double dstB = leftToRight ? points[i + 1].R : points[i + 1].L;

            bool lastSegment = i == points.Count - 2;
            if (line > srcB && !lastSegment) continue;

            if (srcB - srcA < 1e-9) return dstA;
            double t = (line - srcA) / (srcB - srcA);
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return dstA + t * (dstB - dstA);
        }

        return line;
    }

    /// <summary>
    /// Line height calibrated against the rendered gutter: the numbers block
    /// holds exactly one line per document line, so its actual height divided
    /// by the line count is the real rendered line height. This keeps long
    /// files from accumulating drift between text and highlight bands.
    /// </summary>
    private double PaneLineHeight(Pane pane)
    {
        if (pane.Numbers != null && pane.GutterLineCount > 0 && pane.Numbers.ActualHeight > 1)
        {
            return pane.Numbers.ActualHeight / pane.GutterLineCount;
        }
        return _measuredLineHeight;
    }

    private void OnLeftBarScroll(object sender, ScrollEventArgs e) => _left.Scroller?.ChangeView(null, e.NewValue, null, true);

    private void OnRightBarScroll(object sender, ScrollEventArgs e) => _right.Scroller?.ChangeView(null, e.NewValue, null, true);

    private void UpdateScrollBar(Pane pane)
    {
        if (pane.Bar == null || pane.Scroller == null) return;

        double scrollable = Math.Max(0, pane.Scroller.ScrollableHeight);
        pane.Bar.Maximum = scrollable;
        pane.Bar.ViewportSize = pane.Scroller.ViewportHeight;
        pane.Bar.Value = pane.Scroller.VerticalOffset;
        pane.Bar.IsEnabled = scrollable > 0;
        pane.Bar.SmallChange = 3 * _measuredLineHeight;
        pane.Bar.LargeChange = Math.Max(pane.Scroller.ViewportHeight, 1);
    }

    // -----------------------------------------------------------------
    // Rendering: gutters, line/word highlights, connector curves
    // -----------------------------------------------------------------

    private void MeasureMetrics()
    {
        if (_metricsMeasured) return;

        var probe = new TextBlock
        {
            FontFamily = FontFamily,
            FontSize = FontSize,
            Text = "0000000000",
        };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double oneLine = probe.DesiredSize.Height;
        _measuredCharWidth = probe.DesiredSize.Width / 10.0;

        // Measure across 100 lines so the per-line rounding error shrinks by
        // a factor of 100 — over very long files even 0.1px/line would add up.
        probe.Text = BuildProbeText(101);
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double manyLines = probe.DesiredSize.Height;

        double lh = (manyLines - oneLine) / 100.0;
        if (lh > 1) _measuredLineHeight = lh;
        _metricsMeasured = true;
    }

    private static string BuildProbeText(int lines)
    {
        var sb = new System.Text.StringBuilder(lines * 2);
        for (int i = 0; i < lines; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append('0');
        }
        return sb.ToString();
    }

    private void RedrawSideBySide()
    {
        if (ViewMode != DiffViewMode.SideBySide) return;
        if (_result == null) return;
        if (_left.Highlights == null || _right.Highlights == null) return;

        bool highContrast = IsHighContrast();

        UpdateGutter(_left, _result.Lines1.Count);
        UpdateGutter(_right, _result.Lines2.Count);

        // Per-pane line height, calibrated against the rendered gutter height
        // (see PaneLineHeight) — this keeps very long files from accumulating
        // a visible drift between text, numbers and highlight bands.
        double lhLeft = PaneLineHeight(_left);
        double lhRight = PaneLineHeight(_right);

        DrawPane(_left, side1: true, lhLeft, highContrast);
        DrawPane(_right, side1: false, lhRight, highContrast);
        DrawConnectors(lhLeft, lhRight, highContrast);

        UpdateScrollBar(_left);
        UpdateScrollBar(_right);
    }

    private void UpdateGutter(Pane pane, int lineCount)
    {
        if (pane.Numbers == null) return;

        if (pane.GutterLineCount != lineCount)
        {
            var sb = new System.Text.StringBuilder(lineCount * 4);
            for (int i = 1; i <= lineCount; i++)
            {
                if (i > 1) sb.Append('\n');
                sb.Append(i);
            }
            pane.Numbers.Text = sb.ToString();
            pane.GutterLineCount = lineCount;
        }

        // Gutter width follows the widest line number at the current font
        // metrics, so zooming never clips the numbers.
        int digits = Math.Max(2, CountDigits(lineCount));
        double numbersWidth = Math.Ceiling(digits * _measuredCharWidth) + 4;
        double hostWidth = numbersWidth + 8; // 2px canvas offset + breathing room
        if (pane.NumbersHost != null && (double.IsNaN(pane.NumbersHost.Width) || Math.Abs(pane.NumbersHost.Width - hostWidth) > 0.5))
        {
            pane.NumbersHost.Width = hostWidth;
        }
        if (double.IsNaN(pane.Numbers.Width) || Math.Abs(pane.Numbers.Width - numbersWidth) > 0.5)
        {
            pane.Numbers.Width = numbersWidth;
        }

        // No LineHeight override: the gutter uses the same font as the editor,
        // so its natural line spacing matches the editor's exactly.

        double top = (pane.Editor?.Padding.Top ?? 0) - (pane.Scroller?.VerticalOffset ?? 0);
        Canvas.SetTop(pane.Numbers, top);
    }

    private static int CountDigits(int value)
    {
        int digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }
        return digits;
    }

    private void DrawPane(Pane pane, bool side1, double lh, bool highContrast)
    {
        var canvas = pane.Highlights;
        var gutter = pane.GutterHighlights;
        if (canvas == null) return;
        canvas.Children.Clear();
        gutter?.Children.Clear();

        if (highContrast || _result == null) return;
        if (pane.Scroller == null || pane.Editor == null || pane.HighlightHost == null) return;

        double vOff = pane.Scroller.VerticalOffset;
        double hOff = pane.Scroller.HorizontalOffset;
        double padTop = pane.Editor.Padding.Top;
        double padLeft = pane.Editor.Padding.Left;
        double viewportH = pane.HighlightHost.ActualHeight;
        double width = pane.HighlightHost.ActualWidth;
        double gutterWidth = pane.NumbersHost?.ActualWidth ?? 0;
        var theme = ActualTheme;

        var lines = side1 ? _result.Lines1 : _result.Lines2;
        var starts = side1 ? _lineStarts1 : _lineStarts2;

        foreach (var fragment in _result.Fragments)
        {
            int startLine = side1 ? fragment.StartLine1 : fragment.StartLine2;
            int endLine = side1 ? fragment.EndLine1 : fragment.EndLine2;
            var kind = RowKindFor(fragment.Type, side1);

            double y = padTop + startLine * lh - vOff;
            double h = (endLine - startLine) * lh;

            if (y + h < -64 || y > viewportH + 64) continue;

            if (endLine > startLine)
            {
                var rect = new Rectangle
                {
                    Width = Math.Max(width, 0),
                    Height = h,
                    Fill = DiffPalette.GetRowBrush(kind, theme, highContrast: false),
                };
                Canvas.SetLeft(rect, 0);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);

                // The same band continues across the number gutter, 5% darker
                // (IntelliJ look: the numbers sit on the tinted band).
                if (gutter != null)
                {
                    var gutterRect = new Rectangle
                    {
                        Width = Math.Max(gutterWidth, 0),
                        Height = h,
                        Fill = DiffPalette.GetGutterBrush(kind, theme, highContrast: false),
                    };
                    Canvas.SetLeft(gutterRect, 0);
                    Canvas.SetTop(gutterRect, y);
                    gutter.Children.Add(gutterRect);
                }

                DrawWordHighlights(canvas, fragment, side1, lines!, starts!, lh, padTop, padLeft, vOff, hOff, theme, kind);
            }
            else
            {
                // Empty side of an insertion/deletion: a 2px anchor line,
                // drawn across the text pane and its gutter.
                var markerBrush = DiffPalette.GetWordBrush(ConnectorKind(fragment.Type), theme, highContrast: false);
                var marker = new Rectangle
                {
                    Width = Math.Max(width, 0),
                    Height = 2,
                    Fill = markerBrush,
                };
                Canvas.SetLeft(marker, 0);
                Canvas.SetTop(marker, y - 1);
                canvas.Children.Add(marker);

                if (gutter != null)
                {
                    var gutterMarker = new Rectangle
                    {
                        Width = Math.Max(gutterWidth, 0),
                        Height = 2,
                        Fill = markerBrush,
                    };
                    Canvas.SetLeft(gutterMarker, 0);
                    Canvas.SetTop(gutterMarker, y - 1);
                    gutter.Children.Add(gutterMarker);
                }
            }
        }
    }

    private void DrawWordHighlights(
        Canvas canvas, LineFragment fragment, bool side1,
        IReadOnlyList<string> lines, int[] starts,
        double lh, double padTop, double padLeft, double vOff, double hOff,
        ElementTheme theme, DiffRowKind kind)
    {
        if (fragment.InnerFragments == null || fragment.InnerFragments.Count == 0) return;

        int fragStartLine = side1 ? fragment.StartLine1 : fragment.StartLine2;
        int fragEndLine = side1 ? fragment.EndLine1 : fragment.EndLine2;
        int fragStartOffset = side1 ? fragment.StartOffset1 : fragment.StartOffset2;
        double cw = _measuredCharWidth;

        Brush? wordBrush = DiffPalette.GetWordBrush(kind, theme, highContrast: false);
        if (wordBrush == null) return;

        foreach (var inner in fragment.InnerFragments)
        {
            int s = fragStartOffset + (side1 ? inner.StartOffset1 : inner.StartOffset2);
            int e = fragStartOffset + (side1 ? inner.EndOffset1 : inner.EndOffset2);
            if (e <= s) continue;

            for (int line = fragStartLine; line < fragEndLine && line < lines.Count; line++)
            {
                int lineStart = starts[line];
                int lineEnd = lineStart + lines[line].Length;

                int ovS = Math.Max(s, lineStart);
                int ovE = Math.Min(e, lineEnd);
                if (ovS >= ovE) continue;

                // Monospace math does not hold for tabs — line highlight only.
                if (lines[line].IndexOf('\t') >= 0) continue;

                var rect = new Rectangle
                {
                    Width = (ovE - ovS) * cw,
                    Height = lh,
                    Fill = wordBrush,
                };
                Canvas.SetLeft(rect, padLeft + (ovS - lineStart) * cw - hOff);
                Canvas.SetTop(rect, padTop + line * lh - vOff);
                canvas.Children.Add(rect);
            }
        }
    }

    private void DrawConnectors(double lhLeft, double lhRight, bool highContrast)
    {
        var canvas = _connector;
        if (canvas == null) return;
        canvas.Children.Clear();

        if (highContrast || _result == null || _connectorHost == null) return;

        double width = _connectorHost.ActualWidth;
        double height = _connectorHost.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double leftOff = _left.Scroller?.VerticalOffset ?? 0;
        double rightOff = _right.Scroller?.VerticalOffset ?? 0;
        double padTopLeft = _left.Editor?.Padding.Top ?? 0;
        double padTopRight = _right.Editor?.Padding.Top ?? 0;

        // IntelliJ's DiffDrawUtil.CTRL_PROXIMITY_X: both bezier control points
        // are horizontal (same y as their endpoint) at 30% / 70% of the width,
        // so the curve leaves and enters the editors tangentially flat.
        double ctrl1 = width * 0.3;
        double ctrl2 = width * 0.7;
        var theme = ActualTheme;

        foreach (var fragment in _result.Fragments)
        {
            double lTop = padTopLeft + fragment.StartLine1 * lhLeft - leftOff;
            double lBottom = padTopLeft + fragment.EndLine1 * lhLeft - leftOff;
            double rTop = padTopRight + fragment.StartLine2 * lhRight - rightOff;
            double rBottom = padTopRight + fragment.EndLine2 * lhRight - rightOff;

            if (Math.Max(lBottom, rBottom) < -64 || Math.Min(lTop, rTop) > height + 64) continue;

            // Zero-height side: a 2px band centered on the anchor line — the
            // same geometry as the anchor markers drawn in the panes/gutters
            // (IntelliJ's DividerPolygon uses [start-1, start+1] here), so the
            // dark-strip band meets the pane marker at exactly the same height.
            if (lBottom - lTop < 2) { lTop -= 1; lBottom = lTop + 2; }
            if (rBottom - rTop < 2) { rTop -= 1; rBottom = rTop + 2; }

            var kind = ConnectorKind(fragment.Type);
            var fill = DiffPalette.GetRowBrush(kind, theme, highContrast: false);
            if (fill == null) continue;

            var figure = new PathFigure { StartPoint = new Point(0, lTop), IsClosed = true, IsFilled = true };
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(ctrl1, lTop),
                Point2 = new Point(ctrl2, rTop),
                Point3 = new Point(width, rTop),
            });
            figure.Segments.Add(new LineSegment { Point = new Point(width, rBottom) });
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(ctrl2, rBottom),
                Point2 = new Point(ctrl1, lBottom),
                Point3 = new Point(0, lBottom),
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            canvas.Children.Add(new Path { Data = geometry, Fill = fill });
        }
    }

    private static DiffRowKind RowKindFor(LineFragmentType type, bool side1) => type switch
    {
        LineFragmentType.Modified => DiffRowKind.Modified,
        LineFragmentType.Deleted => DiffRowKind.Deleted,
        _ => DiffRowKind.Inserted,
    };

    private static DiffRowKind ConnectorKind(LineFragmentType type) => type switch
    {
        LineFragmentType.Modified => DiffRowKind.Modified,
        LineFragmentType.Deleted => DiffRowKind.Deleted,
        _ => DiffRowKind.Inserted,
    };

    // -----------------------------------------------------------------
    // Unified mode row styling
    // -----------------------------------------------------------------

    private void OnUnifiedRowPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        var row = sender.ItemsSourceView?.GetAt(args.Index) as DiffRow;
        StyleUnifiedRow(args.Element as FrameworkElement, row);
    }

    private void StyleUnifiedRow(FrameworkElement? element, DiffRow? row)
    {
        if (element is not Grid grid || row == null) return;

        bool highContrast = IsHighContrast();
        grid.Background = DiffPalette.GetRowBrush(row.Kind, ActualTheme, highContrast);

        // Number column width follows the widest line number at the current
        // font metrics (same reasoning as the side-by-side gutters).
        if (_result != null && grid.ColumnDefinitions.Count > 0)
        {
            int digits = Math.Max(2, CountDigits(Math.Max(_result.Lines1.Count, _result.Lines2.Count)));
            double width = Math.Ceiling(digits * _measuredCharWidth) + 14; // 10px right margin + padding
            if (Math.Abs(grid.ColumnDefinitions[0].Width.Value - width) > 0.5)
            {
                grid.ColumnDefinitions[0].Width = new GridLength(width);
            }
        }

        foreach (var child in grid.Children)
        {
            if (child is HighlightableTextBlock highlightable)
            {
                highlightable.HighlightBrush = DiffPalette.GetWordBrush(row.Kind, ActualTheme, highContrast);
                highlightable.FontFamily = FontFamily;
                highlightable.FontSize = FontSize;
            }
            else if (child is TextBlock textBlock)
            {
                textBlock.FontFamily = FontFamily;
                textBlock.FontSize = FontSize;
            }
        }
    }

    private bool IsHighContrast()
    {
        // AccessibilitySettings can throw in desktop apps without a CoreWindow.
        try { return _accessibilitySettings.HighContrast; }
        catch { return false; }
    }

    private void RestyleUnifiedRows()
    {
        var view = _unifiedRepeater?.ItemsSourceView;
        if (_unifiedRepeater == null || view == null) return;

        for (int i = 0; i < view.Count; i++)
        {
            var element = _unifiedRepeater.TryGetElement(i);
            if (element is FrameworkElement fe)
            {
                StyleUnifiedRow(fe, view.GetAt(i) as DiffRow);
            }
        }
    }
}
