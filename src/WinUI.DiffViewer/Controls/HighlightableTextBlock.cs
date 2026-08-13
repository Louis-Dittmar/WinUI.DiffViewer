using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using WinUI.DiffViewer.Core;
using WinUI.DiffViewer.Model;

namespace WinUI.DiffViewer.Controls;

/// <summary>
/// A TextBlock wrapper that renders word/char-level diff highlights using
/// <see cref="TextHighlighter"/> (the WinUI counterpart of the inline
/// RangeHighlighters created by IntelliJ's DiffDrawUtil.InlineHighlighterBuilder).
/// </summary>
[TemplatePart(Name = TextBlockPart, Type = typeof(TextBlock))]
public sealed class HighlightableTextBlock : Control
{
    private const string TextBlockPart = "PART_Text";

    private TextBlock? _textBlock;

    public HighlightableTextBlock()
    {
        DefaultStyleKey = typeof(HighlightableTextBlock);
        ActualThemeChanged += (_, _) => Update();
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(HighlightableTextBlock),
        new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty SpansProperty = DependencyProperty.Register(
        nameof(Spans), typeof(object), typeof(HighlightableTextBlock),
        new PropertyMetadata(null, OnVisualPropertyChanged));

    /// <summary>An IReadOnlyList&lt;HighlightSpan&gt; describing the highlighted ranges.</summary>
    public object? Spans
    {
        get => GetValue(SpansProperty);
        set => SetValue(SpansProperty, value);
    }

    public static readonly DependencyProperty HighlightBrushProperty = DependencyProperty.Register(
        nameof(HighlightBrush), typeof(Brush), typeof(HighlightableTextBlock),
        new PropertyMetadata(null, OnVisualPropertyChanged));

    public Brush? HighlightBrush
    {
        get => (Brush?)GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    public static readonly DependencyProperty SyntaxSpansProperty = DependencyProperty.Register(
        nameof(SyntaxSpans), typeof(object), typeof(HighlightableTextBlock),
        new PropertyMetadata(null, OnVisualPropertyChanged));

    /// <summary>An IReadOnlyList&lt;SyntaxSpan&gt; with syntax token foregrounds.</summary>
    public object? SyntaxSpans
    {
        get => GetValue(SyntaxSpansProperty);
        set => SetValue(SyntaxSpansProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _textBlock = GetTemplateChild(TextBlockPart) as TextBlock;
        Update();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HighlightableTextBlock)d).Update();

    private void Update()
    {
        if (_textBlock == null) return;

        string text = Text ?? string.Empty;
        _textBlock.Text = text;
        _textBlock.TextHighlighters.Clear();

        // Syntax token foregrounds — one highlighter per token class.
        if (SyntaxSpans is IReadOnlyList<SyntaxSpan> { Count: > 0 } syntaxSpans)
        {
            var perKind = new Dictionary<SyntaxKind, TextHighlighter>();
            foreach (var span in syntaxSpans)
            {
                int start = span.Start;
                int length = span.Length;
                if (length <= 0 || start < 0 || start >= text.Length) continue;
                if (start + length > text.Length) length = text.Length - start;

                if (!perKind.TryGetValue(span.Kind, out var kindHighlighter))
                {
                    var foreground = DiffPalette.GetSyntaxBrush(span.Kind, ActualTheme, highContrast: false);
                    if (foreground == null) continue;
                    kindHighlighter = new TextHighlighter { Foreground = foreground };
                    perKind.Add(span.Kind, kindHighlighter);
                }
                kindHighlighter.Ranges.Add(new Microsoft.UI.Xaml.Documents.TextRange { StartIndex = start, Length = length });
            }
            foreach (var kindHighlighter in perKind.Values)
            {
                if (kindHighlighter.Ranges.Count > 0)
                {
                    _textBlock.TextHighlighters.Add(kindHighlighter);
                }
            }
        }

        // Word/char diff highlight backgrounds.
        if (Spans is not IReadOnlyList<HighlightSpan> spans || spans.Count == 0 || HighlightBrush == null)
        {
            return;
        }

        var highlighter = new TextHighlighter { Background = HighlightBrush };
        foreach (var span in spans)
        {
            if (span.Length <= 0) continue;
            int start = span.Start;
            int length = span.Length;
            if (start < 0 || start >= text.Length) continue;
            if (start + length > text.Length) length = text.Length - start;
            highlighter.Ranges.Add(new Microsoft.UI.Xaml.Documents.TextRange { StartIndex = start, Length = length });
        }

        if (highlighter.Ranges.Count > 0)
        {
            _textBlock.TextHighlighters.Add(highlighter);
        }
    }
}
