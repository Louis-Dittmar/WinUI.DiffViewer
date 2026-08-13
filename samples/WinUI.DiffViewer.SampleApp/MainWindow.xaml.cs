using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinUI.DiffViewer.SampleApp.Pages;

namespace WinUI.DiffViewer.SampleApp;

public sealed partial class MainWindow : Window
{
    private bool _canNavigate = true;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
    }

    private void RootGridLoaded(object sender, RoutedEventArgs e)
    {
        navigationView.SelectedItem = overviewNavItem;
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        navigationView.IsPaneOpen = !navigationView.IsPaneOpen;
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!_canNavigate)
        {
            return;
        }

        if (args.IsSettingsSelected)
        {
            rootFrame.Navigate(typeof(SettingsPage), args.SelectedItem);
            return;
        }

        if (args.SelectedItem is NavigationViewItem { Content: string content } selectedItem)
        {
            var pageType = content switch
            {
                "Overview" => typeof(OverviewPage),
                "View Modes" => typeof(ViewModesPage),
                "Highlighting" => typeof(HighlightingPage),
                "Whitespace Handling" => typeof(WhitespacePage),
                "Live Editing" => typeof(LiveEditingPage),
                "Synchronized Scrolling" => typeof(SyncScrollingPage),
                "Large Files" => typeof(LargeFilesPage),
                "Syntax Highlighting" => typeof(SyntaxHighlightingPage),
                _ => typeof(OverviewPage),
            };

            rootFrame.Navigate(pageType, selectedItem);
        }
    }

    private void OnBackRequested(TitleBar sender, object args)
    {
        if (rootFrame.CanGoBack)
        {
            rootFrame.GoBack();
        }
    }

    private void OnRootFrameNavigated(object sender, NavigationEventArgs e)
    {
        _canNavigate = false;

        if (e.NavigationMode == NavigationMode.Back && e.Parameter is NavigationViewItem navItem && !navItem.Equals(navigationView.SelectedItem))
        {
            navigationView.SelectedItem = navItem;
        }

        if (e.Content is Page page)
        {
            page.NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        _canNavigate = true;
    }
}
