using Microsoft.UI.Xaml.Controls;

namespace WinUI.DiffViewer.SampleApp.Pages;

public sealed partial class SyncScrollingPage : Page
{
    public SyncScrollingPage()
    {
        InitializeComponent();

        EditorSettings.Apply(viewer);
        var (oldText, newText) = DemoContent.BuildSyncScrollDemo();
        viewer.OldText = oldText;
        viewer.NewText = newText;
    }
}
