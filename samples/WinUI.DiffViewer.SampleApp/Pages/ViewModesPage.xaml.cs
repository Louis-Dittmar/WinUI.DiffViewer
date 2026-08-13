using Microsoft.UI.Xaml.Controls;
using WinUI.DiffViewer.Controls;

namespace WinUI.DiffViewer.SampleApp.Pages;

public sealed partial class ViewModesPage : Page
{
    public ViewModesPage()
    {
        InitializeComponent();

        EditorSettings.Apply(viewer);
        viewer.OldText = DemoContent.OldCode;
        viewer.NewText = DemoContent.NewCode;
        modeButtons.SelectedIndex = viewer.ViewMode == DiffViewMode.SideBySide ? 0 : 1;
    }

    private void OnModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        viewer.ViewMode = modeButtons.SelectedIndex == 1 ? DiffViewMode.Unified : DiffViewMode.SideBySide;
    }
}
