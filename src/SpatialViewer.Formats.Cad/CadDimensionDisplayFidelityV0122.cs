using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

public sealed partial class CadSceneTranslator
{
    private static void AddArrow(
        List<SceneNode> children,
        ObjectId id,
        Point2D tip,
        Point2D toward,
        double size,
        SceneStyle style,
        Dictionary<string, string> metadata)
    {
        metadata.TryGetValue("DimensionArrowRequestedBlock", out var requested);
        if (string.IsNullOrWhiteSpace(requested) && metadata.TryGetValue("DimensionArrowBlock", out var shared))
        {
            requested = shared;
            metadata["DimensionArrowRequestedBlock"] = shared;
            metadata["DimensionArrowSharedBlockFallback"] = bool.TrueString;
        }
        if (!IsArchitecturalTick(requested))
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

    private static SceneNode TextNode(
        ObjectId id,
        Point2D insertionPoint,
        string text,
        double height,
        double rotationRadians,
        SceneStyle style,
        Dictionary<string, string> metadata)
    {
        var isDimension = metadata.TryGetValue("DimensionSemantic", out var semantic)
            && bool.TryParse(semantic, out var parsedDimension)
            && parsedDimension;
        var resolved = isDimension ? ReadableDimensionRotation(rotationRadians) : rotationRadians;
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        if (resolved != rotationRadians)
        {
            enriched["DimensionTextReadableRotationApplied"] = bool.TrueString;
            enriched["DimensionTextSourceRotation"] = rotationRadians.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            enriched["DimensionTextResolvedRotation"] = resolved.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (!isDimension) return TextNode(id, insertionPoint, text, height, resolved, style, (IReadOnlyDictionary<string, string>)enriched);

        enriched["DimensionTextAnchor"] = "MiddleCenter";
        var geometry = new TextGeometry(insertionPoint, text, height)
        {
            HorizontalAlignment = TextHorizontalAlignment2D.Center,
            VerticalAlignment = TextVerticalAlignment2D.Middle
        };
        var transform = Transform2D.Translation(-insertionPoint.X, -insertionPoint.Y)
            .Then(Transform2D.Rotation(resolved))
            .Then(Transform2D.Translation(insertionPoint.X, insertionPoint.Y));
        return new SceneNode(id, geometry, transform, style, metadata: enriched);
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
