using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinUI.DiffViewer.Model;

namespace WinUI.DiffViewer.Controls;

/// <summary>
/// Resolves the row-background and word-highlight brushes for a
/// <see cref="DiffRowKind"/>. The palette mirrors IntelliJ's DiffColors
/// (DIFF_INSERTED = green, DIFF_MODIFIED = blue, DIFF_DELETED = gray).
///
/// An application can override individual colors by defining brushes with the
/// keys "DiffInsertedRowBrush", "DiffInsertedWordBrush", "DiffDeletedRowBrush",
/// "DiffDeletedWordBrush", "DiffModifiedRowBrush", "DiffModifiedWordBrush" or
/// "DiffFillerRowBrush" in Application.Resources.
///
/// Brushes are applied from code-behind (DiffViewer.StyleRow) using the
/// control's ActualTheme, so per-window/per-element theme overrides and
/// runtime theme switches are honored. In high-contrast mode no custom
/// backgrounds are painted at all, so system colors stay legible.
/// </summary>
internal static class DiffPalette
{
    internal static Brush? GetRowBrush(DiffRowKind kind, ElementTheme theme, bool highContrast)
        => Resolve(kind, theme, highContrast, word: false);

    internal static Brush? GetWordBrush(DiffRowKind kind, ElementTheme theme, bool highContrast)
        => Resolve(kind, theme, highContrast, word: true);

    /// <summary>
    /// Row color for the line-number gutter: 15% darker than the pane row color,
    /// so the band stays readable behind the numbers (IntelliJ does the same).
    /// </summary>
    internal static Brush? GetGutterBrush(DiffRowKind kind, ElementTheme theme, bool highContrast)
    {
        if (highContrast) return null;
        if (kind == DiffRowKind.Unchanged) return null;

        bool dark = theme == ElementTheme.Dark;
        Color color = dark ? DarkColor(kind, word: false) : LightColor(kind, word: false);
        if (color.A == 0) return null;
        return new SolidColorBrush(Darken(color, 0.85));
    }

    private static Color Darken(Color color, double factor) => Color.FromArgb(
        color.A,
        (byte)(color.R * factor),
        (byte)(color.G * factor),
        (byte)(color.B * factor));

    private static Brush? Resolve(DiffRowKind kind, ElementTheme theme, bool highContrast, bool word)
    {
        string? key = kind switch
        {
            DiffRowKind.Inserted => word ? "DiffInsertedWordBrush" : "DiffInsertedRowBrush",
            DiffRowKind.Deleted => word ? "DiffDeletedWordBrush" : "DiffDeletedRowBrush",
            DiffRowKind.Modified => word ? "DiffModifiedWordBrush" : "DiffModifiedRowBrush",
            DiffRowKind.Filler => word ? null : "DiffFillerRowBrush",
            _ => null,
        };

        if (key == null) return null;

        var app = Application.Current;
        if (app != null && app.Resources.TryGetValue(key, out object resource) && resource is Brush brush)
        {
            return brush;
        }

        if (highContrast) return null;

        bool dark = theme == ElementTheme.Dark;
        return new SolidColorBrush(dark ? DarkColor(kind, word) : LightColor(kind, word));
    }

    // Light palette — close to IntelliJ's default light scheme.
    private static Color LightColor(DiffRowKind kind, bool word) => kind switch
    {
        DiffRowKind.Inserted => word ? Rgb(0xA7, 0xE1, 0xA7) : Rgb(0xD4, 0xF2, 0xD4),
        DiffRowKind.Deleted => word ? Rgb(0xCE, 0xCE, 0xCE) : Rgb(0xE9, 0xE9, 0xE9),
        DiffRowKind.Modified => word ? Rgb(0xA6, 0xCB, 0xEC) : Rgb(0xD7, 0xE7, 0xF5),
        DiffRowKind.Filler => Color.FromArgb(0x14, 0x80, 0x80, 0x80),
        _ => Color.FromArgb(0, 0, 0, 0),
    };

    // Dark palette — close to IntelliJ's Darcula scheme.
    private static Color DarkColor(DiffRowKind kind, bool word) => kind switch
    {
        DiffRowKind.Inserted => word ? Rgb(0x36, 0x59, 0x3F) : Rgb(0x29, 0x44, 0x36),
        DiffRowKind.Deleted => word ? Rgb(0x4C, 0x4C, 0x4C) : Rgb(0x3A, 0x3A, 0x3A),
        DiffRowKind.Modified => word ? Rgb(0x38, 0x54, 0x6E) : Rgb(0x2B, 0x3B, 0x50),
        DiffRowKind.Filler => Color.FromArgb(0x14, 0x80, 0x80, 0x80),
        _ => Color.FromArgb(0, 0, 0, 0),
    };

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(0xFF, r, g, b);
}
