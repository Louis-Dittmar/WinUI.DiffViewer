using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUI.DiffViewer.SampleApp.Pages;

public sealed partial class LiveEditingPage : Page
{
    private readonly ObservableCollection<string> _log = [];

    public LiveEditingPage()
    {
        InitializeComponent();

        eventLog.ItemsSource = _log;
        viewer.DiffComputed += OnDiffComputed;
        viewer.OldText = DemoContent.OldCode;
        viewer.NewText = DemoContent.NewCode;
    }

    private void OnDiffComputed(object? sender, EventArgs e)
    {
        _log.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  DiffComputed");

        while (_log.Count > 20)
        {
            _log.RemoveAt(_log.Count - 1);
        }
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        viewer.OldText = DemoContent.OldCode;
        viewer.NewText = DemoContent.NewCode;
    }
}
