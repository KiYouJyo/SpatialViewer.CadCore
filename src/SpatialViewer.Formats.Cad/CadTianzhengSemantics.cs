using System.Globalization;
using System.Text;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>How far CadCore can safely promote recovered custom-object meaning toward native 2D geometry.</summary>
public enum CadCustomSemanticCoverage
{
    Partial,
    Drawable2D
}

/// <summary>Reader-independent native meaning recovered from an application-defined CAD object.</summary>
public abstract record CadCustomSemantic(string DecoderProfile)
{
    public virtual CadCustomSemanticCoverage Coverage => CadCustomSemanticCoverage.Partial;
    public bool IsDrawable2D => Coverage == CadCustomSemanticCoverage.Drawable2D;
}

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
    public override CadCustomSemanticCoverage Coverage => CadCustomSemanticCoverage.Drawable2D;
    public double Length => Start.DistanceTo(End);
    public double TotalWidth => LeftWidth + RightWidth;
}

/// <summary>
/// Partial native evidence for a Tianzheng opening. Public evidence identifies DXF point 10 as the opening
/// insertion point and group 302 as the opening number when that field is present. Reliable raw mappings for
/// width, height, sill height, opening type, and native geometry are still unavailable. Host-wall identity
/// remains a document-level relationship resolved by <see cref="CadCustomRelationshipResolver"/>.
/// </summary>
public sealed record CadTianzhengOpeningAnchorSemantic(
    Point2D InsertionPoint,
    double? Elevation,
    string DecoderProfile)
    : CadCustomSemantic(DecoderProfile)
{
    /// <summary>Optional Tianzheng opening/door-window number retained from raw DXF group 302.</summary>
    public string? Number { get; init; }
}

/// <summary>
/// Partial native evidence for a Tianzheng column. Published AutoLISP that rounds architectural object
/// coordinates handles *_COLUMN specially and explicitly reads point group 11 as the column insertion point.
/// Section shape/dimensions, rotation, height, material, and native column geometry remain undecoded.
/// </summary>
public sealed record CadTianzhengColumnAnchorSemantic(
    Point2D InsertionPoint,
    double? Elevation,
    string DecoderProfile)
    : CadCustomSemantic(DecoderProfile);

/// <summary>
/// Partial native evidence for a Tianzheng elevation symbol. Public Tianzheng-oriented AutoLISP examples
/// identify point group 10 as the insertion point and group 1 as the elevation text. Group 47 is retained
/// only as an optional plot-scale value because that mapping is explicitly documented by the same examples.
/// Symbol geometry, orientation, text height, and leader/arrow details remain intentionally undecoded.
/// </summary>
public sealed record CadTianzhengElevationSemantic(
    Point2D InsertionPoint,
    double? InsertionZ,
    string Text,
    double? PlotScale,
    string DecoderProfile)
    : CadCustomSemantic(DecoderProfile);

/// <summary>
/// Partial native evidence for a Tianzheng room/space object. Public entget evidence exposes the TDbSpace
/// subclass together with point group 10, group 1 room name, and group 2 room number. Area, volume,
/// perimeter, skirting length, and wall/opening areas remain intentionally undecoded until their raw field
/// mappings are independently verified.
/// </summary>
public sealed record CadTianzhengSpaceSemantic(
    Point2D InsertionPoint,
    double? InsertionZ,
    string Name,
    string Number,
    string DecoderProfile)
    : CadCustomSemantic(DecoderProfile);

/// <summary>
/// Partial native evidence for a Tianzheng drawing-name annotation. Multiple public AutoLISP examples read
/// group 1 from TCH_DRAWINGNAME as the displayed drawing-name text and map the same value to the NameText
/// ActiveX property. No public evidence used by this profile establishes insertion-point, scale, underline,
/// index-pointer, or native symbol geometry fields, so only the text itself is promoted to semantics.
/// </summary>
public sealed record CadTianzhengDrawingNameSemantic(
    string Text,
    string DecoderProfile)
    : CadCustomSemantic(DecoderProfile);

/// <summary>
/// Evidence-gated Tianzheng native decoder. A profile succeeds only when the raw payload matches a
/// known layout. Unknown or malformed payloads remain preserved without speculative semantics.
/// </summary>
public static class CadTianzhengSemanticDecoder
{
    public const string WallDirectProfile = "TCH_WALL_DIRECT_10_11";
    public const string WallPacked300Profile = "TCH_WALL_PACKED_300_UTF16LE";
    public const string OpeningAnchorDirectProfile = "TCH_OPENING_ANCHOR_10";
    public const string ColumnAnchorDirectProfile = "TCH_COLUMN_ANCHOR_11";
    public const string ElevationTextDirectProfile = "TCH_ELEVATION_TEXT_10_1";
    public const string SpaceNameNumberDirectProfile = "TCH_SPACE_NAME_NUMBER_10_1_2";
    public const string DrawingNameTextDirectProfile = "TCH_DRAWINGNAME_TEXT_1";

    public static CadCustomSemantic? Decode(
        string sourceEntityType,
        CadCustomClassDefinition? classDefinition,
        CadDxfCustomPayload? payload)
    {
        if (payload is null || payload.IsTruncated) return null;
        if (IsTianzhengWall(sourceEntityType, classDefinition, payload))
            return TryDecodePackedWall(payload) ?? TryDecodeDirectWall(payload);
        if (IsTianzhengOpening(sourceEntityType, classDefinition, payload))
            return TryDecodeOpeningAnchor(payload);
        if (IsTianzhengColumn(sourceEntityType, classDefinition))
            return TryDecodeColumnAnchor(payload);
        if (IsTianzhengElevation(sourceEntityType, classDefinition, payload))
            return TryDecodeElevation(payload);
        if (IsTianzhengSpace(sourceEntityType, classDefinition, payload))
            return TryDecodeSpace(payload);
        if (IsTianzhengDrawingName(sourceEntityType, classDefinition))
            return TryDecodeDrawingName(payload);
        return null;
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

    private static bool IsTianzhengOpening(
        string sourceEntityType,
        CadCustomClassDefinition? classDefinition,
        CadDxfCustomPayload payload)
    {
        var isOpeningName = string.Equals(sourceEntityType, "TCH_OPENING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classDefinition?.DxfName, "TCH_OPENING", StringComparison.OrdinalIgnoreCase);
        if (!isOpeningName) return false;

        return payload.Groups.Any(group => group.Code == 100 && string.Equals(group.RawValue.Trim(), "TDbOpening", StringComparison.OrdinalIgnoreCase))
            || string.Equals(classDefinition?.CppClassName, "TDbOpening", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTianzhengColumn(
        string sourceEntityType,
        CadCustomClassDefinition? classDefinition)
        => string.Equals(sourceEntityType, "TCH_COLUMN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classDefinition?.DxfName, "TCH_COLUMN", StringComparison.OrdinalIgnoreCase);

    private static bool IsTianzhengElevation(
        string sourceEntityType,
        CadCustomClassDefinition? classDefinition,
        CadDxfCustomPayload payload)
    {
        var isElevationName = string.Equals(sourceEntityType, "TCH_ELEVATION", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classDefinition?.DxfName, "TCH_ELEVATION", StringComparison.OrdinalIgnoreCase);
        if (!isElevationName) return false;

        // Unlike the wall/opening profiles, do not guess a CLASSES-table C++ identity here. Public evidence
        // exposes the concrete TDbSymbElevation subclass marker, so require that marker in the raw payload.
        return payload.Groups.Any(group => group.Code == 100 && string.Equals(group.RawValue.Trim(), "TDbSymbElevation", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTianzhengSpace(
        string sourceEntityType,
        CadCustomClassDefinition? classDefinition,
        CadDxfCustomPayload payload)
    {
        var isSpaceName = string.Equals(sourceEntityType, "TCH_SPACE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classDefinition?.DxfName, "TCH_SPACE", StringComparison.OrdinalIgnoreCase);
        if (!isSpaceName) return false;

        return payload.Groups.Any(group => group.Code == 100 && string.Equals(group.RawValue.Trim(), "TDbSpace", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTianzhengDrawingName(
        string sourceEntityType,
        CadCustomClassDefinition? classDefinition)
        => string.Equals(sourceEntityType, "TCH_DRAWINGNAME", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classDefinition?.DxfName, "TCH_DRAWINGNAME", StringComparison.OrdinalIgnoreCase);

    private static CadTianzhengOpeningAnchorSemantic? TryDecodeOpeningAnchor(CadDxfCustomPayload payload)
    {
        if (!TryNumber(payload, 10, out var x) || !TryNumber(payload, 20, out var y)) return null;
        var insertionPoint = new Point2D(x, y);
        if (!Finite(insertionPoint)) return null;
        var rawNumber = payload.Groups.FirstOrDefault(group => group.Code == 302)?.RawValue;
        return new CadTianzhengOpeningAnchorSemantic(
            insertionPoint,
            OptionalNumber(payload, 30),
            OpeningAnchorDirectProfile)
        {
            Number = string.IsNullOrWhiteSpace(rawNumber) ? null : rawNumber
        };
    }

    private static CadTianzhengColumnAnchorSemantic? TryDecodeColumnAnchor(CadDxfCustomPayload payload)
    {
        if (!TryNumber(payload, 11, out var x) || !TryNumber(payload, 21, out var y)) return null;
        var insertionPoint = new Point2D(x, y);
        if (!Finite(insertionPoint)) return null;
        return new CadTianzhengColumnAnchorSemantic(
            insertionPoint,
            OptionalNumber(payload, 31),
            ColumnAnchorDirectProfile);
    }

    private static CadTianzhengElevationSemantic? TryDecodeElevation(CadDxfCustomPayload payload)
    {
        if (!TryNumber(payload, 10, out var x) || !TryNumber(payload, 20, out var y)) return null;
        var text = payload.Groups.FirstOrDefault(group => group.Code == 1)?.RawValue;
        if (string.IsNullOrWhiteSpace(text)) return null;
        var insertionPoint = new Point2D(x, y);
        if (!Finite(insertionPoint)) return null;
        return new CadTianzhengElevationSemantic(
            insertionPoint,
            OptionalNumber(payload, 30),
            text,
            PositiveOptionalNumber(payload, 47),
            ElevationTextDirectProfile);
    }

    private static CadTianzhengSpaceSemantic? TryDecodeSpace(CadDxfCustomPayload payload)
    {
        if (!TryNumber(payload, 10, out var x) || !TryNumber(payload, 20, out var y)) return null;
        var nameGroup = payload.Groups.FirstOrDefault(group => group.Code == 1);
        var numberGroup = payload.Groups.FirstOrDefault(group => group.Code == 2);
        if (nameGroup is null || numberGroup is null) return null;
        var insertionPoint = new Point2D(x, y);
        if (!Finite(insertionPoint)) return null;
        return new CadTianzhengSpaceSemantic(
            insertionPoint,
            OptionalNumber(payload, 30),
            nameGroup.RawValue,
            numberGroup.RawValue,
            SpaceNameNumberDirectProfile);
    }

    private static CadTianzhengDrawingNameSemantic? TryDecodeDrawingName(CadDxfCustomPayload payload)
    {
        var text = payload.Groups.FirstOrDefault(group => group.Code == 1)?.RawValue;
        return string.IsNullOrWhiteSpace(text)
            ? null
            : new CadTianzhengDrawingNameSemantic(text, DrawingNameTextDirectProfile);
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
                if (tokens[index].Contains('%')
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
