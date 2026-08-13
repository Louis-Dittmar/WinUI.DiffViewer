using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WinUI.DiffViewer.SampleApp.Helpers;

internal static class UIHelper
{
    /// <summary>Walks up the visual tree to the nearest ancestor of type <typeparamref name="T"/>.</summary>
    public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);

        while (current is not null && current is not T)
        {
            current = VisualTreeHelper.GetParent(current);
        }

        return current as T;
    }
}
