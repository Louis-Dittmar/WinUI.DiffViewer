using Microsoft.UI.Xaml.Controls;

namespace WinUI.DiffViewer.SampleApp.Pages;

public sealed partial class HighlightingPage : Page
{
    public HighlightingPage()
    {
        InitializeComponent();

        viewer.OldText = DemoContent.OldProse;
        viewer.NewText = DemoContent.NewProse;
    }
}
