using SpatialViewer.Core;

namespace SpatialViewer.Rendering;

/// <summary>Rendering-time color policies that depend on the viewer surface rather than source format.</summary>
public static class RenderColorPolicy
{
    public const string BackgroundAdaptiveStrokeKey = "BackgroundAdaptiveStroke";

    public static string ResolveStroke(SceneStyle style, IReadOnlyDictionary<string, string> metadata, string canvasColor)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(metadata);
        if (!metadata.TryGetValue(BackgroundAdaptiveStrokeKey, out var value) || !bool.TryParse(value, out var adaptive) || !adaptive) return style.Stroke;
        return TryGetLuminance(canvasColor, out var luminance) ? luminance < 0.5 ? "#FFFFFF" : "#000000" : style.Stroke;
    }

    public static bool IsDark(string color) => TryGetLuminance(color, out var luminance) && luminance < 0.5;

    private static bool TryGetLuminance(string color, out double luminance)
    {
        luminance = 0;
        if (string.IsNullOrWhiteSpace(color)) return false;
        var text = color.Trim().TrimStart('#');
        if (text.Length == 3) text = string.Concat(text.Select(character => new string(character, 2)));
        if (text.Length != 6 || !uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rgb)) return false;
        var red = (rgb >> 16) & 0xff;
        var green = (rgb >> 8) & 0xff;
        var blue = rgb & 0xff;
        luminance = ((0.2126 * red) + (0.7152 * green) + (0.0722 * blue)) / 255d;
        return true;
    }
}
