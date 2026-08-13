using Microsoft.UI.Xaml.Controls;

namespace WinUI.DiffViewer.SampleApp.Pages;

public sealed partial class HighlightingPage : Page
{
    public HighlightingPage()
    {
        InitializeComponent();

        EditorSettings.Apply(viewer);
        viewer.OldText = DemoContent.OldProse;
        viewer.NewText = DemoContent.NewProse;
    }
}
