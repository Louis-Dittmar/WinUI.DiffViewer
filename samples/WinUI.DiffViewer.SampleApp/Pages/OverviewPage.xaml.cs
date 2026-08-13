using Microsoft.UI.Xaml.Controls;

namespace WinUI.DiffViewer.SampleApp.Pages;

public sealed partial class OverviewPage : Page
{
    public OverviewPage()
    {
        InitializeComponent();

        EditorSettings.Apply(viewer);
        viewer.OldText = DemoContent.OldCode;
        viewer.NewText = DemoContent.NewCode;
    }
}
