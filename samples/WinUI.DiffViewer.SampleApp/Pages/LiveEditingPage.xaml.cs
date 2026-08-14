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

        EditorSettings.Apply(viewer);
        eventLog.ItemsSource = _log;
        viewer.DiffComputed += OnDiffComputed;
        viewer.UndoStateChanged += OnUndoStateChanged;
        viewer.OldText = DemoContent.OldLineComparator;
        viewer.NewText = DemoContent.NewLineComparator;
    }

    private void OnUndoStateChanged(object? sender, EventArgs e)
    {
        undoButton.IsEnabled = viewer.CanUndo;
        redoButton.IsEnabled = viewer.CanRedo;
    }

    private void OnUndoClicked(object sender, RoutedEventArgs e) => viewer.Undo();

    private void OnRedoClicked(object sender, RoutedEventArgs e) => viewer.Redo();

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
        viewer.OldText = DemoContent.OldLineComparator;
        viewer.NewText = DemoContent.NewLineComparator;
    }
}
