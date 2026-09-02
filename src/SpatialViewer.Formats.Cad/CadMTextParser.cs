using System.Globalization;
using System.Text;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Reader-independent result of flattening AutoCAD MTEXT control sequences into display text.
/// Formatting flags are retained so richer backends can progressively render the original semantics
/// without requiring the ACadSharp adapter at render time.
/// </summary>
public sealed record CadMTextParseResult(
    string PlainText,
    bool HasInlineFormatting,
    bool HasStackedText,
    bool HasFontOverrides,
    bool HasColorOverrides,
    bool HasHeightOverrides,
    bool HasWidthOverrides,
    bool HasObliqueOverrides,
    bool HasTrackingOverrides,
    bool HasDecorations);

/// <summary>
/// Normalizes the common AutoCAD MTEXT control language while preserving readable content.
/// This intentionally degrades unsupported per-span presentation to plain text instead of exposing
/// raw formatting codes in the viewer.
/// </summary>
public static class CadMTextParser
{
    public static CadMTextParseResult Parse(string? value)
    {
        if (string.IsNullOrEmpty(value)) return new CadMTextParseResult(string.Empty, false, false, false, false, false, false, false, false, false);

        var output = new StringBuilder(value.Length);
        var inlineFormatting = false;
        var stacked = false;
        var font = false;
        var color = false;
        var height = false;
        var width = false;
        var oblique = false;
        var tracking = false;
        var decorations = false;

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];

            if (current is '{' or '}')
            {
                inlineFormatting = true;
                continue;
            }

            if (current == '\\' && index + 1 < value.Length)
            {
                var marker = value[++index];
                switch (marker)
                {
                    case '\\':
                    case '{':
                    case '}':
                        output.Append(marker);
                        continue;
                    case 'P':
                    case 'p':
                    case 'X':
                    case 'x':
                        output.Append('\n');
                        continue;
                    case '~':
                        output.Append(' ');
                        continue;
                    case 'U':
                    case 'u':
                        if (index + 5 < value.Length && value[index + 1] == '+' && int.TryParse(value.AsSpan(index + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint))
                        {
                            output.Append(char.ConvertFromUtf32(codePoint));
                            index += 5;
                            continue;
                        }
                        output.Append(marker);
                        continue;
                    case 'S':
                    case 's':
                    {
                        stacked = true;
                        inlineFormatting = true;
                        var payload = ReadUntilSemicolon(value, ref index);
                        output.Append(FlattenStack(payload));
                        continue;
                    }
                    case 'F':
                    case 'f':
                        font = true;
                        inlineFormatting = true;
                        SkipUntilSemicolon(value, ref index);
                        continue;
                    case 'C':
                    case 'c':
                        color = true;
                        inlineFormatting = true;
                        SkipUntilSemicolon(value, ref index);
                        continue;
                    case 'H':
                    case 'h':
                        height = true;
                        inlineFormatting = true;
                        SkipUntilSemicolon(value, ref index);
                        continue;
                    case 'W':
                    case 'w':
                        width = true;
                        inlineFormatting = true;
                        SkipUntilSemicolon(value, ref index);
                        continue;
                    case 'Q':
                    case 'q':
                        oblique = true;
                        inlineFormatting = true;
                        SkipUntilSemicolon(value, ref index);
                        continue;
                    case 'T':
                    case 't':
                        tracking = true;
                        inlineFormatting = true;
                        SkipUntilSemicolon(value, ref index);
                        continue;
                    case 'A':
                    case 'a':
                        inlineFormatting = true;
                        SkipUntilSemicolon(value, ref index);
                        continue;
                    case 'L':
                    case 'l':
                    case 'O':
                    case 'o':
                    case 'K':
                    case 'k':
                        decorations = true;
                        inlineFormatting = true;
                        continue;
                    default:
                        // Unknown control codes are kept as readable text instead of silently
                        // discarding a potentially meaningful character.
                        output.Append(marker);
                        continue;
                }
            }

            if (current == '%' && index + 2 < value.Length && value[index + 1] == '%')
            {
                switch (char.ToLowerInvariant(value[index + 2]))
                {
                    case 'd': output.Append('°'); index += 2; continue;
                    case 'p': output.Append('±'); index += 2; continue;
                    case 'c': output.Append('⌀'); index += 2; continue;
                    case 'u':
                    case 'o': decorations = true; inlineFormatting = true; index += 2; continue;
                }
            }

            output.Append(current);
        }

        return new CadMTextParseResult(output.ToString(), inlineFormatting, stacked, font, color, height, width, oblique, tracking, decorations);
    }

    private static string ReadUntilSemicolon(string value, ref int index)
    {
        var start = index + 1;
        var end = start;
        while (end < value.Length && value[end] != ';') end++;
        index = end < value.Length ? end : value.Length - 1;
        return value[start..end];
    }

    private static void SkipUntilSemicolon(string value, ref int index) => _ = ReadUntilSemicolon(value, ref index);

    private static string FlattenStack(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return string.Empty;
        var separatorIndex = payload.IndexOfAny(['#', '/', '^']);
        if (separatorIndex < 0) return payload;

        var numerator = payload[..separatorIndex];
        var denominator = payload[(separatorIndex + 1)..];
        if (numerator.Length == 0) return denominator;
        if (denominator.Length == 0) return numerator;
        return $"{numerator}/{denominator}";
    }
}
