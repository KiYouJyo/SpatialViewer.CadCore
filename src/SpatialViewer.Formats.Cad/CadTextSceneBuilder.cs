using System.Globalization;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

internal static class CadTextSceneBuilder
{
    public static SceneNode Create(CadTextEntity text, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
        => Create(text.ObjectId, text.InsertionPoint, text.Text, text.Height, text.RotationRadians, text.IsMText, text.Presentation, style, metadata);

    public static SceneNode Create(CadAttributeEntity attribute, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
        => Create(attribute.ObjectId, attribute.InsertionPoint, attribute.Value, attribute.Height, attribute.RotationRadians, false, attribute.Presentation, style, metadata);

    private static SceneNode Create(ObjectId id, Point2D insertionPoint, string sourceText, double sourceHeight, double sourceRotation, bool isMText, CadTextPresentation presentation, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        CadMTextParseResult? mtext = null;
        var text = CadTextNormalizer.Normalize(sourceText);
        if (isMText)
        {
            var raw = string.IsNullOrEmpty(presentation.RawText) ? sourceText : presentation.RawText;
            mtext = CadMTextParser.Parse(raw);
            text = mtext.PlainText;
        }

        var origin = insertionPoint;
        var height = Math.Max(double.Epsilon, Math.Abs(sourceHeight));
        var rotation = sourceRotation;
        var widthFactor = Positive(presentation.WidthFactor);
        var horizontal = TextHorizontalAlignment2D.Left;
        var vertical = TextVerticalAlignment2D.Top;

        if (isMText)
        {
            (horizontal, vertical) = Attachment(presentation.AttachmentPoint);
        }
        else
        {
            horizontal = Horizontal(presentation.HorizontalAlignment);
            vertical = Vertical(presentation.VerticalAlignment);
            var usesAlignmentPoint = !presentation.HorizontalAlignment.Equals("Left", StringComparison.OrdinalIgnoreCase) || !presentation.VerticalAlignment.Equals("Baseline", StringComparison.OrdinalIgnoreCase);
            if (usesAlignmentPoint && presentation.AlignmentPoint is { } aligned) origin = aligned;

            if ((presentation.HorizontalAlignment.Equals("Fit", StringComparison.OrdinalIgnoreCase) || presentation.HorizontalAlignment.Equals("Aligned", StringComparison.OrdinalIgnoreCase)) && presentation.AlignmentPoint is { } endpoint)
            {
                origin = insertionPoint;
                horizontal = TextHorizontalAlignment2D.Left;
                var distance = insertionPoint.DistanceTo(endpoint);
                if (distance > double.Epsilon)
                {
                    rotation = Math.Atan2(endpoint.Y - insertionPoint.Y, endpoint.X - insertionPoint.X);
                    var natural = EstimateNaturalWidth(text, height, widthFactor);
                    if (natural > double.Epsilon)
                    {
                        if (presentation.HorizontalAlignment.Equals("Fit", StringComparison.OrdinalIgnoreCase)) widthFactor *= distance / natural;
                        else height *= distance / natural;
                    }
                }
            }
            else if (presentation.HorizontalAlignment.Equals("Middle", StringComparison.OrdinalIgnoreCase))
            {
                horizontal = TextHorizontalAlignment2D.Center;
                if (presentation.VerticalAlignment.Equals("Baseline", StringComparison.OrdinalIgnoreCase)) vertical = TextVerticalAlignment2D.Middle;
            }
        }

        var font = CadFontResolver.Resolve(presentation.FontFileName, text, presentation.IsShapeFile);
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["TextStyle"] = presentation.StyleName,
            ["FontFile"] = presentation.FontFileName,
            ["BigFontFile"] = presentation.BigFontFileName,
            ["FontKind"] = font.Kind.ToString(),
            ["FontFamily"] = font.Family,
            ["FontFallbackApplied"] = font.UsesFallback.ToString(),
            ["TextHorizontalAlignment"] = presentation.HorizontalAlignment,
            ["TextVerticalAlignment"] = presentation.VerticalAlignment,
            ["MTextAttachment"] = presentation.AttachmentPoint,
            ["TextWidthFactor"] = widthFactor.ToString("R", CultureInfo.InvariantCulture),
            ["TextObliqueAngle"] = presentation.ObliqueAngleRadians.ToString("R", CultureInfo.InvariantCulture),
            ["TextLayoutWidth"] = presentation.LayoutWidth.ToString("R", CultureInfo.InvariantCulture),
            ["TextLineSpacing"] = presentation.LineSpacingFactor.ToString("R", CultureInfo.InvariantCulture),
            ["TextMirrorBackward"] = presentation.IsBackward.ToString(),
            ["TextMirrorUpsideDown"] = presentation.IsUpsideDown.ToString()
        };
        if (!string.IsNullOrEmpty(presentation.RawText)) enriched["RawText"] = presentation.RawText;
        if (mtext is not null)
        {
            enriched["MTextInlineFormatting"] = mtext.HasInlineFormatting.ToString();
            enriched["MTextStackedText"] = mtext.HasStackedText.ToString();
            enriched["MTextFontOverrides"] = mtext.HasFontOverrides.ToString();
            enriched["MTextColorOverrides"] = mtext.HasColorOverrides.ToString();
            enriched["MTextHeightOverrides"] = mtext.HasHeightOverrides.ToString();
            enriched["MTextWidthOverrides"] = mtext.HasWidthOverrides.ToString();
            enriched["MTextObliqueOverrides"] = mtext.HasObliqueOverrides.ToString();
            enriched["MTextTrackingOverrides"] = mtext.HasTrackingOverrides.ToString();
            enriched["MTextDecorations"] = mtext.HasDecorations.ToString();
        }

        if (font.Kind == CadFontKind.Shx && presentation.VectorFont is { } vectorFont)
        {
            var vector = CreateShxNode(id, origin, text, height, rotation, widthFactor, horizontal, vertical, presentation, vectorFont, style, enriched);
            if (vector is not null) return vector;
        }

        var geometry = new TextGeometry(origin, text, height)
        {
            FontFamily = font.Family,
            WidthFactor = widthFactor,
            ObliqueAngleRadians = presentation.ObliqueAngleRadians,
            LayoutWidth = Math.Max(0, presentation.LayoutWidth),
            LineSpacingFactor = Positive(presentation.LineSpacingFactor),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            IsBackward = presentation.IsBackward,
            IsUpsideDown = presentation.IsUpsideDown,
            IsMultiline = isMText || text.Contains('\n')
        };
        var transform = RotationTransform(origin, rotation);
        return new SceneNode(id, geometry, transform, style, metadata: enriched);
    }

    private static SceneNode? CreateShxNode(ObjectId id, Point2D origin, string text, double height, double rotation, double widthFactor, TextHorizontalAlignment2D horizontal, TextVerticalAlignment2D vertical, CadTextPresentation presentation, CadShxFont vectorFont, SceneStyle style, Dictionary<string, string> metadata)
    {
        metadata["ShxVectorFontAvailable"] = bool.TrueString;
        metadata["ShxFontType"] = vectorFont.FontType.ToString();
        metadata["ShxGlyphCount"] = vectorFont.GlyphCount.ToString(CultureInfo.InvariantCulture);
        metadata["ShxFontInfo"] = vectorFont.Info;

        if (!vectorFont.CanLayoutUnicodeText)
        {
            metadata["ShxVectorGlyphComplete"] = bool.FalseString;
            metadata["ShxFallbackReason"] = vectorFont.FontType == CadShxFontType.BigFont ? "BigFontEncodingMappingRequired" : "PlainShapeFile";
            return null;
        }

        var layout = vectorFont.LayoutText(text, height, widthFactor, Positive(presentation.LineSpacingFactor));
        metadata["ShxVectorGlyphComplete"] = layout.Complete.ToString();
        metadata["ShxMissingGlyphCount"] = layout.MissingGlyphCount.ToString(CultureInfo.InvariantCulture);
        if (!layout.Complete)
        {
            metadata["ShxFallbackReason"] = "MissingGlyph";
            return null;
        }

        if (layout.Strokes.Count == 0)
        {
            metadata["FontFallbackApplied"] = bool.FalseString;
            return new SceneNode(id, style: style, metadata: metadata);
        }

        var bounds = layout.Bounds;
        var offsetX = horizontal switch
        {
            TextHorizontalAlignment2D.Center => -layout.AdvanceWidth / 2,
            TextHorizontalAlignment2D.Right => -layout.AdvanceWidth,
            _ => 0
        };
        var offsetY = vertical switch
        {
            TextVerticalAlignment2D.Top when !bounds.IsEmpty => -bounds.MaxY,
            TextVerticalAlignment2D.Middle when !bounds.IsEmpty => -(bounds.MinY + bounds.MaxY) / 2,
            TextVerticalAlignment2D.Bottom when !bounds.IsEmpty => -bounds.MinY,
            _ => 0
        };

        metadata["FontFallbackApplied"] = bool.FalseString;
        metadata["ShxVectorStrokeCount"] = layout.Strokes.Count.ToString(CultureInfo.InvariantCulture);
        var children = layout.Strokes
            .Where(stroke => stroke.Count >= 2)
            .Select(stroke => new SceneNode(
                id,
                new PolylineGeometry(stroke.Select(point => new Point2D(origin.X + offsetX + point.X, origin.Y + offsetY + point.Y)).ToArray()),
                style: style,
                metadata: metadata))
            .ToArray();

        var transform = PresentationTransform(origin, rotation, presentation.ObliqueAngleRadians, presentation.IsBackward, presentation.IsUpsideDown);
        return new SceneNode(id, transform: transform, style: style, children: children, metadata: metadata);
    }

    private static Transform2D PresentationTransform(Point2D origin, double rotation, double obliqueAngleRadians, bool backward, bool upsideDown)
    {
        var transform = Transform2D.Translation(-origin.X, -origin.Y);
        if (backward || upsideDown) transform = transform.Then(Transform2D.Scale(backward ? -1 : 1, upsideDown ? -1 : 1));
        if (Math.Abs(obliqueAngleRadians) > double.Epsilon)
        {
            var shear = new Transform2D(1, 0, -Math.Tan(obliqueAngleRadians), 1, 0, 0);
            transform = transform.Then(shear);
        }
        return transform.Then(Transform2D.Rotation(rotation)).Then(Transform2D.Translation(origin.X, origin.Y));
    }

    private static Transform2D RotationTransform(Point2D origin, double rotation)
        => Transform2D.Translation(-origin.X, -origin.Y).Then(Transform2D.Rotation(rotation)).Then(Transform2D.Translation(origin.X, origin.Y));

    private static double EstimateNaturalWidth(string text, double height, double widthFactor)
    {
        var geometry = new TextGeometry(Point2D.Origin, text, height) { WidthFactor = widthFactor };
        return geometry.EstimatedWidth;
    }

    private static double Positive(double value) => double.IsFinite(value) && value > double.Epsilon ? value : 1;

    private static TextHorizontalAlignment2D Horizontal(string value) => value.ToLowerInvariant() switch
    {
        "center" or "middle" => TextHorizontalAlignment2D.Center,
        "right" => TextHorizontalAlignment2D.Right,
        _ => TextHorizontalAlignment2D.Left
    };

    private static TextVerticalAlignment2D Vertical(string value) => value.ToLowerInvariant() switch
    {
        "top" => TextVerticalAlignment2D.Top,
        "middle" => TextVerticalAlignment2D.Middle,
        "bottom" => TextVerticalAlignment2D.Bottom,
        _ => TextVerticalAlignment2D.Baseline
    };

    private static (TextHorizontalAlignment2D Horizontal, TextVerticalAlignment2D Vertical) Attachment(string value) => value.ToLowerInvariant() switch
    {
        "topcenter" => (TextHorizontalAlignment2D.Center, TextVerticalAlignment2D.Top),
        "topright" => (TextHorizontalAlignment2D.Right, TextVerticalAlignment2D.Top),
        "middleleft" => (TextHorizontalAlignment2D.Left, TextVerticalAlignment2D.Middle),
        "middlecenter" => (TextHorizontalAlignment2D.Center, TextVerticalAlignment2D.Middle),
        "middleright" => (TextHorizontalAlignment2D.Right, TextVerticalAlignment2D.Middle),
        "bottomleft" => (TextHorizontalAlignment2D.Left, TextVerticalAlignment2D.Bottom),
        "bottomcenter" => (TextHorizontalAlignment2D.Center, TextVerticalAlignment2D.Bottom),
        "bottomright" => (TextHorizontalAlignment2D.Right, TextVerticalAlignment2D.Bottom),
        _ => (TextHorizontalAlignment2D.Left, TextVerticalAlignment2D.Top)
    };
}
