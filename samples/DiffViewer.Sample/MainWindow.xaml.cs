using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.DiffViewer.Controls;
using WinUI.DiffViewer.Core;

namespace DiffViewer.Sample;

public sealed partial class MainWindow : Window
{
    private const string SampleOld = """
        public class Calculator {
            private int value;

            public int add(int a, int b) {
                return a + b;
            }

            public int subtract(int a, int b) {
                return a - b;
            }

            public void reset() {
                value = 0;
            }
        }
        """;

    private const string SampleNew = """
        public class Calculator {
            private int value;
            private int precision;

            public int add(int a, int b) {
                return a + b + precision;
            }

            public int multiply(int a, int b) {
                return a * b;
            }

            public void reset() {
                value = 0;
                precision = 0;
            }
        }
        """;

    private readonly bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        Title = "WinUI.DiffViewer Sample";

        OldTextBox.Text = SampleOld;
        NewTextBox.Text = SampleNew;
        _initialized = true;

        ApplySettings();
        Compare();
    }

    private void OnCompareClicked(object sender, RoutedEventArgs e) => Compare();

    private void OnSettingsChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged fires during InitializeComponent while other named
        // elements are still null — ignore those early events.
        if (!_initialized) return;
        ApplySettings();
    }

    private void ApplySettings()
    {
        Viewer.ViewMode = ModeBox.SelectedIndex == 1 ? DiffViewMode.Unified : DiffViewMode.SideBySide;

        Viewer.IgnorePolicy = PolicyBox.SelectedIndex switch
        {
            1 => ComparisonPolicy.TrimWhitespaces,
            2 => ComparisonPolicy.IgnoreWhitespaces,
            _ => ComparisonPolicy.Default,
        };

        Viewer.HighlightMode = HighlightBox.SelectedIndex switch
        {
            0 => HighlightMode.Lines,
            2 => HighlightMode.Characters,
            3 => HighlightMode.None,
            _ => HighlightMode.Words,
        };
    }

    private void Compare()
    {
        Viewer.OldText = OldTextBox.Text;
        Viewer.NewText = NewTextBox.Text;
    }
}
