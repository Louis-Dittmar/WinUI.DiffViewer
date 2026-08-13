using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace WinUI.DiffViewer.SampleApp.Controls;

/// <summary>
/// A named placeholder inside a <see cref="SamplePresenter"/> source snippet:
/// occurrences of <c>$(Key)</c> are replaced with <see cref="Value"/>, and the
/// snippet re-renders whenever the value changes.
/// </summary>
public sealed class CodeSubstitution : DependencyObject
{
    public event TypedEventHandler<CodeSubstitution, object?>? ValueChanged;

    public string Key { get; set; } = string.Empty;

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string ValueAsString() => Value?.ToString() ?? string.Empty;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(object), typeof(CodeSubstitution),
        new PropertyMetadata(null, (d, e) => ((CodeSubstitution)d).ValueChanged?.Invoke((CodeSubstitution)d, e.NewValue)));
}
