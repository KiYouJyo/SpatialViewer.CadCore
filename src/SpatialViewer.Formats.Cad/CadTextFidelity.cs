using System.Globalization;
using System.Text;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

public sealed record CadTextPresentation(
    string StyleName = "Standard",
    string FontFileName = "",
    string BigFontFileName = "",
    double WidthFactor = 1,
    double ObliqueAngleRadians = 0,
    string HorizontalAlignment = "Left",
    string VerticalAlignment = "Baseline",
    string AttachmentPoint = "TopLeft",
    Point2D? AlignmentPoint = null,
    double LayoutWidth = 0,
    double LineSpacingFactor = 1,
    bool IsBackward = false,
    bool IsUpsideDown = false,
    bool IsShapeFile = false,
    string RawText = "",
    CadShxFont? VectorFont = null);

public enum CadFontKind { Default, TrueType, Shx }
public readonly record struct CadFontResolution(CadFontKind Kind, string Family, bool UsesFallback);

public static class CadFontResolver
{
    public static CadFontResolution Resolve(string? fontFileName, string text, bool isShapeFile = false)
    {
        var source = fontFileName?.Trim() ?? string.Empty;
        var extension = Path.GetExtension(source);
        if (isShapeFile || extension.Equals(".shx", StringComparison.OrdinalIgnoreCase))
        {
            // When the actual SHX glyph stream cannot be used (most commonly a legacy paired
            // BigFont for CJK text), prefer print-oriented CJK families whose full-em metrics are
            // much closer to AutoCAD's legacy architectural text than UI sans-serif fallbacks.
            return new CadFontResolution(CadFontKind.Shx, ShxFallbackFamily(text), true);
        }
        if (extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase))
        {
            var family = FriendlyFamily(Path.GetFileNameWithoutExtension(source));
            return new CadFontResolution(CadFontKind.TrueType, string.IsNullOrWhiteSpace(family) ? FallbackFamily(text) : family, string.IsNullOrWhiteSpace(family));
        }
        return new CadFontResolution(CadFontKind.Default, FallbackFamily(text), true);
    }

    private static string FriendlyFamily(string value) => value.ToLowerInvariant() switch
    {
        "arial" => "Arial",
        "times" or "timesnewroman" or "times new roman" => "Times New Roman",
        "calibri" => "Calibri",
        "simhei" => "SimHei",
        "simsun" => "SimSun",
        "msyh" or "msyh.ttc" => "Microsoft YaHei",
        "msgothic" => "MS Gothic",
        "yugothic" => "Yu Gothic",
        _ => value
    };

    private static string ShxFallbackFamily(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            if (value is >= 0x3040 and <= 0x30FF or >= 0x31F0 and <= 0x31FF) return "MS Gothic";
            if (value is >= 0x3400 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF) return "SimSun";
        }
        return "Segoe UI";
    }

    private static string FallbackFamily(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            if (value is >= 0x3040 and <= 0x30FF or >= 0x31F0 and <= 0x31FF) return "Yu Gothic UI";
            if (value is >= 0x3400 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF) return "Microsoft YaHei UI";
        }
        return "Segoe UI";
    }
}

public static class CadTextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var output = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\\' && index + 1 < value.Length)
            {
                var marker = value[index + 1];
                if (marker is 'P' or 'p') { output.Append('\n'); index++; continue; }
                if (marker == '~') { output.Append(' '); index++; continue; }
                if ((marker is 'U' or 'u') && index + 6 < value.Length && value[index + 2] == '+' && int.TryParse(value.AsSpan(index + 3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint))
                {
                    output.Append(char.ConvertFromUtf32(codePoint)); index += 6; continue;
                }
            }
            if (value[index] == '%' && index + 2 < value.Length && value[index + 1] == '%')
            {
                switch (char.ToLowerInvariant(value[index + 2]))
                {
                    case 'd': output.Append('°'); index += 2; continue;
                    case 'p': output.Append('±'); index += 2; continue;
                    case 'c': output.Append('⌀'); index += 2; continue;
                    case 'u':
                    case 'o': index += 2; continue;
                }
            }
            output.Append(value[index]);
        }
        return output.ToString();
    }
}
