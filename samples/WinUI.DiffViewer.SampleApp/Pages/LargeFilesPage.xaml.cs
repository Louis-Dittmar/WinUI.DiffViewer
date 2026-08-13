using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUI.DiffViewer.SampleApp.Pages;

public sealed partial class LargeFilesPage : Page
{
    private readonly Stopwatch _stopwatch = new();

    public LargeFilesPage()
    {
        InitializeComponent();

        viewer.DiffComputed += OnDiffComputed;
        viewer.DiffFailed += OnDiffFailed;
        GenerateAndCompare(1000);
    }

    private void OnGenerateClicked(object sender, RoutedEventArgs e)
    {
        var lineCount = double.IsNaN(lineCountBox.Value) ? 5000 : (int)lineCountBox.Value;
        GenerateAndCompare(lineCount);
    }

    private void GenerateAndCompare(int lineCount)
    {
        var (oldText, newText) = DemoContent.BuildLargeDemo(lineCount);

        timingText.Text = $"comparing 2 × {lineCount:N0} lines…";
        _stopwatch.Restart();
        viewer.OldText = oldText;
        viewer.NewText = newText;
    }

    private void OnDiffComputed(object? sender, EventArgs e)
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            timingText.Text = $"computed in {_stopwatch.ElapsedMilliseconds} ms (incl. ~250 ms debounce)";
        }
    }

    private void OnDiffFailed(object? sender, Exception ex)
    {
        _stopwatch.Stop();
        timingText.Text = $"failed: {ex.Message}";
    }
}
