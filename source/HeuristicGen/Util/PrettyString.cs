namespace HeuristicGen.Util;

public readonly struct PrettyString
{
    private readonly string[] _lines;

    public PrettyString(string[] lines) => _lines = lines;

    public override string ToString()
    {
        return (uint)_lines.Length switch
        {
            0 => string.Empty,
            1 => _lines[0],
            > 1 => string.Join(Environment.NewLine, _lines)
        };
    }

    public string ToString(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return ToString();
        }

        return (uint)_lines.Length switch
        {
            0 => prefix,
            1 => prefix + _lines[0],
            > 1 => string.Join(Environment.NewLine, _lines.Select(s => prefix + s))
        };
    }

    public static PrettyString Combine(int maxWidth, string prologue, string separator, string epilogue,
        params PrettyString[] strings)
    {
        // We assume prologue, epilogue, and separator do not contain new line characters.
        if (strings.All(s => s._lines.Length <= 1))
        {
            var combinedSingleLine = prologue + string.Join(separator, strings.Select(s => s.ToString())) + epilogue;
            if (combinedSingleLine.Length <= maxWidth)
            {
                return new PrettyString([combinedSingleLine]);
            }
        }

        var needsIndent = false;
        var totalLines = strings.Sum(s => s._lines.Length);
        if (!string.IsNullOrEmpty(prologue))
        {
            totalLines++;
            needsIndent = true;
        }

        if (!string.IsNullOrEmpty(epilogue))
        {
            totalLines++;
            needsIndent = true;
        }

        var newLines = new string[totalLines];
        var j = 0;
        if (!string.IsNullOrEmpty(prologue))
        {
            newLines[j++] = prologue;
        }

        if (!string.IsNullOrEmpty(epilogue))
        {
            newLines[^1] = epilogue;
        }

        for (var stringIndex = 0; stringIndex < strings.Length; stringIndex++)
        {
            var prettyString = strings[stringIndex];
            foreach (var line in prettyString._lines)
            {
                newLines[j++] = needsIndent ? "    " + line : line;
            }

            if (stringIndex != strings.Length - 1)
            {
                newLines[j - 1] += separator;
            }
        }

        return new PrettyString(newLines);
    }
}