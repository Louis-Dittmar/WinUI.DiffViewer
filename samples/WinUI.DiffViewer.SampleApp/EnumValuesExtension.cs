using System;
using Microsoft.UI.Xaml.Markup;

namespace WinUI.DiffViewer.SampleApp;

/// <summary>
/// Returns all values of an enum type — usable as a ComboBox ItemsSource:
/// <c>ItemsSource="{local:EnumValues Type=core:HighlightMode}"</c>.
/// </summary>
[MarkupExtensionReturnType(ReturnType = typeof(Array))]
public sealed partial class EnumValuesExtension : MarkupExtension
{
    public Type? Type { get; set; }

    protected override object ProvideValue()
    {
        return Type is null ? Array.Empty<object>() : Enum.GetValues(Type);
    }
}
