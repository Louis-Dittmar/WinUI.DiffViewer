using Microsoft.UI.Xaml.Media;

namespace WinUI.DiffViewer.SampleApp;

/// <summary>
/// App-wide editor settings, applied to the DiffViewer of every sample page
/// when the page is created. Ctrl+mouse wheel additionally zooms each viewer
/// individually (built into the control).
/// </summary>
public static class EditorSettings
{
    public const string JetBrainsMonoSource = "ms-appx:///Assets/Fonts/JetBrainsMono-Regular.ttf#JetBrains Mono";

    public static string FontSource { get; set; } = "Consolas";

    public static double FontSize { get; set; } = 13;

    public static void Apply(WinUI.DiffViewer.Controls.DiffViewer viewer)
    {
        viewer.FontFamily = new FontFamily(FontSource);
        viewer.FontSize = FontSize;
    }
}
