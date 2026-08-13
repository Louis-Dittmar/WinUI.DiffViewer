using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.DiffViewer.SampleApp.Helpers;

namespace WinUI.DiffViewer.SampleApp;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public string Version => $"Version {typeof(SettingsPage).Assembly.GetName().Version?.ToString(3)}";

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        var currentTheme = ThemeHelper.RootTheme.ToString();

        for (int i = 0; i < themeMode.Items.Count; i++)
        {
            if (themeMode.Items[i] is ComboBoxItem item && (string?)item.Tag == currentTheme)
            {
                themeMode.SelectedIndex = i;
                return;
            }
        }

        themeMode.SelectedIndex = themeMode.Items.Count - 1;
    }

    private void OnThemeModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (themeMode.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            ThemeHelper.RootTheme = tag switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }
    }
}
