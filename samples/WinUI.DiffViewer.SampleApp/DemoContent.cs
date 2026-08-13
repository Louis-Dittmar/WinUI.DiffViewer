using System.Text;

namespace WinUI.DiffViewer.SampleApp;

/// <summary>Deterministic demo text pairs used by the sample pages.</summary>
internal static class DemoContent
{
    /// <summary>A small class evolving: modified lines, an inserted block and a deleted method.</summary>
    public const string OldCode =
"""
using System;

namespace Sample
{
    public class Greeter
    {
        private readonly string _name;

        public Greeter(string name)
        {
            _name = name;
        }

        public void Greet()
        {
            Console.WriteLine("Hello, " + _name + "!");
        }

        public void Farewell()
        {
            Console.WriteLine("Goodbye, " + _name + ".");
        }
    }
}
""";

    public const string NewCode =
"""
using System;

namespace Sample
{
    public sealed class Greeter
    {
        private readonly string _name;
        private readonly bool _formal;

        public Greeter(string name, bool formal = false)
        {
            _name = name;
            _formal = formal;
        }

        public void Greet()
        {
            var greeting = _formal ? "Good day" : "Hello";
            Console.WriteLine($"{greeting}, {_name}!");
        }
    }
}
""";

    /// <summary>Small word- and character-level changes inside otherwise equal lines.</summary>
    public const string OldProse =
"""
The quick brown fox jumps over the lazy dog.
Pack my box with five dozen liquor jugs.
How vexingly quick daft zebras jump!
Sphinx of black quartz, judge my vow.
The five boxing wizards jump quickly.
""";

    public const string NewProse =
"""
The quick red fox leaps over the lazy cat.
Pack my box with six dozen liquor jugs.
How vexingly quick daft zebras jump!
Sphinx of blue quartz, judge my vows.
The five boxing wizards jump quickly.
""";

    /// <summary>The same logic reformatted — plus exactly one real change (the trailing '!').</summary>
    public const string OldWhitespace =
"""
if (value == null)
{
    throw new ArgumentNullException(nameof(value));
}

var result = Compute( value );
return result;
""";

    public const string NewWhitespace =
"""
if (value == null) {
        throw new ArgumentNullException(nameof(value));
}

var result  =  Compute(value);
return result!;
""";

    /// <summary>
    /// A pair of documents with different lengths: a block insertion in the
    /// middle plus scattered changes, so synchronized scrolling has to run the
    /// two panes at different speeds.
    /// </summary>
    public static (string OldText, string NewText) BuildSyncScrollDemo()
    {
        var oldSb = new StringBuilder();
        var newSb = new StringBuilder();

        for (int i = 1; i <= 160; i++)
        {
            string line = $"line {i:D3}: the quick brown fox jumps over the lazy dog";
            oldSb.AppendLine(line);

            if (i == 60)
            {
                // 40 lines only the new document has.
                for (int j = 1; j <= 40; j++)
                {
                    newSb.AppendLine($"inserted {j:D2}: this block exists only on the right side");
                }
            }

            // Every 25th line is modified on the right.
            newSb.AppendLine(i % 25 == 0 ? line.Replace("lazy dog", "sleepy cat") : line);
        }

        return (oldSb.ToString(), newSb.ToString());
    }

    /// <summary>Two large documents with a change every 10th line, a deletion every 47th and an inserted block every 100th.</summary>
    public static (string OldText, string NewText) BuildLargeDemo(int lineCount)
    {
        var oldSb = new StringBuilder(lineCount * 48);
        var newSb = new StringBuilder(lineCount * 48);

        for (int i = 1; i <= lineCount; i++)
        {
            string line = $"line {i:D6}: lorem ipsum dolor sit amet, consetetur sadipscing elitr";

            if (i % 47 != 0)
            {
                oldSb.AppendLine(line);
            }

            newSb.AppendLine(i % 10 == 0 ? line.Replace("lorem ipsum", "LOREM IPSUM") : line);

            if (i % 100 == 0)
            {
                newSb.AppendLine($"extra {i:D6}: this line was inserted after every hundredth line");
            }
        }

        return (oldSb.ToString(), newSb.ToString());
    }
}
