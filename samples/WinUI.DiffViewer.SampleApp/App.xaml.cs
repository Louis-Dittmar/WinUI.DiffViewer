using Microsoft.UI.Xaml;

namespace WinUI.DiffViewer.SampleApp;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    public MainWindow MainWindow { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
