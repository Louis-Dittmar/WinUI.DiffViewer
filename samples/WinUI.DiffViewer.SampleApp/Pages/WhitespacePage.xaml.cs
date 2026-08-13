using Microsoft.UI.Xaml.Controls;

namespace WinUI.DiffViewer.SampleApp.Pages;

public sealed partial class WhitespacePage : Page
{
    public WhitespacePage()
    {
        InitializeComponent();

        viewer.OldText = DemoContent.OldWhitespace;
        viewer.NewText = DemoContent.NewWhitespace;
    }
}
