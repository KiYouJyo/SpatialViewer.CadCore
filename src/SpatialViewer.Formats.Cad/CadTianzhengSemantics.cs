using System.Globalization;
using System.Text;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>Reader-independent native meaning recovered from an application-defined CAD object.</summary>
public abstract record CadCustomSemantic(string DecoderProfile);

/// <summary>
/// Native 2D semantics for a straight Tianzheng wall. Widths are measured from the wall centerline
/// toward its left/right side relative to Start -> End. Vertical values remain optional because this
/// milestone only needs trustworthy 2D viewing geometry.
/// </summary>
public sealed record CadTianzhengWallSemantic(
    Point2D Start,
    Point2D End,
    double LeftWidth,
    double RightWidth,
    double? BaseElevation,
    double? Height,
    string DecoderProfile)
    : CadCustomSemantic(DecoderProfile)
{
    public double Length => Start.DistanceTo(End);
    public double TotalWidth => LeftWidth + RightWidth;
}

/// <summary>
/// Evidence-gated Tianzheng native decoder. A profile succeeds only when the raw payload matches a
/// known layout. Unknown or malformed payloads remain preserved without speculative semantics.
/// </summary>
public static class CadTianzhengSemanticDecoder
{
    public const string WallDirectProfile = "TCH_WALL_DIRECT_10_11";
    public const string WallPacked300Profile = "TCH_WALL_PACKED_300_UTF16LE";

    public static CadCustomSemantic? Decode(
        string sourceEntityType,
        CadCustomClassDefinition? classDefinition,
        CadDxfCustomPayload? payload)
    {
        if (payload is null || payload.IsTruncated || !IsTianzhengWall(sourceEntityType, classDefinition, payload)) return null;
        return TryDecodePackedWall(payload) ?? TryDecodeDirectWall(payload);
    }

    private static bool IsTianzhengWall(
        string sourceEntityType,
        CadCustomClassDefinition? classDefinition,
        CadDxfCustomPayload payload)
    {
        var isWallName = string.Equals(sourceEntityType, "TCH_WALL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classDefinition?.DxfName, "TCH_WALL", StringComparison.OrdinalIgnoreCase);
        if (!isWallName) return false;

        // TDbWall is a strong schema guard. Some exported/custom fixtures may omit subclass markers,
        // but a matching CLASSES-table C++ class is still acceptable evidence.
        return payload.Groups.Any(group => group.Code == 100 && string.Equals(group.RawValue.Trim(), "TDbWall", StringComparison.OrdinalIgnoreCase))
            || string.Equals(classDefinition?.CppClassName, "TDbWall", StringComparison.OrdinalIgnoreCase);
    }

    private static CadTianzhengWallSemantic? TryDecodeDirectWall(CadDxfCustomPayload payload)
    {
        if (!TryNumber(payload, 10, out var startX)
            || !TryNumber(payload, 20, out var startY)
            || !TryNumber(payload, 11, out var endX)
            || !TryNumber(payload, 21, out var endY)
            || !TryNumber(payload, 40, out var leftWidth)
            || !TryNumber(payload, 41, out var rightWidth))
            return null;

        return CreateWall(
            new Point2D(startX, startY),
            new Point2D(endX, endY),
            leftWidth,
            rightWidth,
            OptionalNumber(payload, 38),
            PositiveOptionalNumber(payload, 39),
            WallDirectProfile);
    }

    private static CadTianzhengWallSemantic? TryDecodePackedWall(CadDxfCustomPayload payload)
    {
        foreach (var group in payload.Groups.Where(group => group.Code == 300))
        {
            if (!TryDecodePackedWallValues(group.RawValue, out var values)) continue;
            var wall = CreateWall(
                new Point2D(values[0], values[2]),
                new Point2D(values[1], values[3]),
                values[6],
                values[7],
                OptionalNumber(payload, 38),
                PositiveOptionalNumber(payload, 39),
                WallPacked300Profile);
            if (wall is not null) return wall;
        }

        return null;
    }

    private static bool TryDecodePackedWallValues(string rawValue, out double[] values)
    {
        values = Array.Empty<double>();
        var encoded = rawValue.Trim();
        if (encoded.Length == 0) return false;
        try
        {
            var bytes = Convert.FromBase64String(encoded);
            if (bytes.Length == 0 || (bytes.Length & 1) != 0) return false;
            var decoded = Encoding.Unicode.GetString(bytes).TrimEnd('\0').Trim();
            var tokens = decoded.Split(',', StringSplitOptions.TrimEntries);
            if (tokens.Length != 8) return false;

            values = new double[8];
            for (var index = 0; index < tokens.Length; index++)
            {
                // Do not repair malformed proprietary notation such as "%+006". Returning no native
                // semantics is safer than generating plausible geometry at the wrong coordinates.
                if (tokens[index].Contains('%', StringComparison.Ordinal)
                    || !double.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index])
                    || !double.IsFinite(values[index]))
                {
                    values = Array.Empty<double>();
                    return false;
                }
            }

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static CadTianzhengWallSemantic? CreateWall(
        Point2D start,
        Point2D end,
        double leftWidth,
        double rightWidth,
        double? baseElevation,
        double? height,
        string profile)
    {
        if (!Finite(start) || !Finite(end) || start.DistanceTo(end) <= 1e-9) return null;
        if (!double.IsFinite(leftWidth) || !double.IsFinite(rightWidth) || leftWidth < 0 || rightWidth < 0) return null;
        if (leftWidth + rightWidth <= 1e-9) return null;
        return new CadTianzhengWallSemantic(start, end, leftWidth, rightWidth, baseElevation, height, profile);
    }

    private static bool TryNumber(CadDxfCustomPayload payload, int code, out double value)
    {
        value = 0;
        var raw = payload.Groups.FirstOrDefault(group => group.Code == code)?.RawValue;
        return raw is not null
            && double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }

    private static double? OptionalNumber(CadDxfCustomPayload payload, int code)
        => TryNumber(payload, code, out var value) ? value : null;

    private static double? PositiveOptionalNumber(CadDxfCustomPayload payload, int code)
        => TryNumber(payload, code, out var value) && value > 0 ? value : null;

    private static bool Finite(Point2D point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
