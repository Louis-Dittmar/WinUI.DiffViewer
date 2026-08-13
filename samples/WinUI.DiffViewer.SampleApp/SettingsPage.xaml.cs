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

        themeMode.SelectedIndex = themeMode.Items.Count - 1;
        for (int i = 0; i < themeMode.Items.Count; i++)
        {
            if (themeMode.Items[i] is ComboBoxItem item && (string?)item.Tag == currentTheme)
            {
                themeMode.SelectedIndex = i;
                break;
            }
        }

        editorFont.SelectedIndex = 0;
        for (int i = 0; i < editorFont.Items.Count; i++)
        {
            if (editorFont.Items[i] is ComboBoxItem item && (string?)item.Tag == EditorSettings.FontSource)
            {
                editorFont.SelectedIndex = i;
                break;
            }
        }

        editorFontSize.Value = EditorSettings.FontSize;
    }

    private void OnEditorFontSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (editorFont.SelectedItem is ComboBoxItem { Tag: string source })
        {
            EditorSettings.FontSource = source;
        }
    }

    private void OnEditorFontSizeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!double.IsNaN(args.NewValue))
        {
            EditorSettings.FontSize = args.NewValue;
        }
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
