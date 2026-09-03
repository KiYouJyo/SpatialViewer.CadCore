using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

public sealed partial class CadSceneTranslator
{
    /// <summary>
    /// Intercepts dimension/leader arrow calls whose metadata is still mutable. Known AutoCAD built-in
    /// architectural tick identities are rendered as a single oblique stroke; every other request is
    /// delegated to the established conservative arrow fallback.
    /// </summary>
    private static void AddArrow(
        List<SceneNode> children,
        ObjectId id,
        Point2D tip,
        Point2D toward,
        double size,
        SceneStyle style,
        Dictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue("DimensionArrowRequestedBlock", out var requested) || !IsArchitecturalTick(requested))
        {
            AddArrow(children, id, tip, toward, size, style, (IReadOnlyDictionary<string, string>)metadata);
            return;
        }

        var direction = Normalize(new Point2D(toward.X - tip.X, toward.Y - tip.Y));
        if (Math.Abs(direction.X) <= double.Epsilon && Math.Abs(direction.Y) <= double.Epsilon) return;

        var arrowSize = size > double.Epsilon ? size : 2.5;
        var tickDirection = Rotate(direction, Math.PI / 4);
        var halfLength = arrowSize * 0.75;
        var start = new Point2D(tip.X - (tickDirection.X * halfLength), tip.Y - (tickDirection.Y * halfLength));
        var end = new Point2D(tip.X + (tickDirection.X * halfLength), tip.Y + (tickDirection.Y * halfLength));
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["DimensionArrowResolvedKind"] = "ArchitecturalTick",
            ["DimensionArrowFallbackApplied"] = bool.FalseString
        };
        children.Add(LineNode(id, start, end, style, enriched));
    }

    /// <summary>
    /// AutoCAD dimension text is conventionally kept readable. Only semantic DIMENSION text uses this
    /// overload; ordinary TEXT/MTEXT retains its authored rotation exactly.
    /// </summary>
    private static SceneNode TextNode(
        ObjectId id,
        Point2D insertionPoint,
        string text,
        double height,
        double rotationRadians,
        SceneStyle style,
        Dictionary<string, string> metadata)
    {
        var resolved = metadata.TryGetValue("DimensionSemantic", out var semantic)
            && bool.TryParse(semantic, out var isDimension)
            && isDimension
                ? ReadableDimensionRotation(rotationRadians)
                : rotationRadians;
        if (resolved != rotationRadians)
        {
            metadata = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
            {
                ["DimensionTextReadableRotationApplied"] = bool.TrueString,
                ["DimensionTextSourceRotation"] = rotationRadians.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ["DimensionTextResolvedRotation"] = resolved.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            };
        }
        return TextNode(id, insertionPoint, text, height, resolved, style, (IReadOnlyDictionary<string, string>)metadata);
    }

    private static bool IsArchitecturalTick(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return false;
        var normalized = requested.Trim().TrimStart('_');
        return normalized.Equals("Oblique", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("ArchTick", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("ArchitecturalTick", StringComparison.OrdinalIgnoreCase);
    }

    private static double ReadableDimensionRotation(double rotationRadians)
    {
        if (!double.IsFinite(rotationRadians)) return rotationRadians;
        var resolved = Math.IEEERemainder(rotationRadians, Math.PI * 2);
        if (resolved > (Math.PI / 2) + 1e-12) resolved -= Math.PI;
        else if (resolved < (-Math.PI / 2) - 1e-12) resolved += Math.PI;
        return Math.Abs(resolved) <= 1e-12 ? 0 : resolved;
    }
}
