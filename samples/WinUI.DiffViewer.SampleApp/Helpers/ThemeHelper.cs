using Microsoft.UI.Xaml;

namespace WinUI.DiffViewer.SampleApp.Helpers;

/// <summary>Switches and reads the app theme via the root element.</summary>
internal static class ThemeHelper
{
    /// <summary>
    /// The current actual theme of the app: the root element's requested theme,
    /// or if that is Default, the application theme.
    /// </summary>
    public static ElementTheme ActualTheme
    {
        get
        {
            if (App.Current.MainWindow.Content is FrameworkElement rootElement
                && rootElement.RequestedTheme != ElementTheme.Default)
            {
                return rootElement.RequestedTheme;
            }

            return App.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        }
    }

    /// <summary>Gets or sets the RequestedTheme of the root element.</summary>
    public static ElementTheme RootTheme
    {
        get => App.Current.MainWindow.Content is FrameworkElement rootElement ? rootElement.RequestedTheme : ElementTheme.Default;
        set
        {
            if (App.Current.MainWindow.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = value;
            }
        }
    }
}
