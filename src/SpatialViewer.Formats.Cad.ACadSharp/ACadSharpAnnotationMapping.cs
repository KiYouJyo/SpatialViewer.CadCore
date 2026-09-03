using System.Globalization;
using ACadSharp.Entities;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

public sealed partial class ACadSharpCadImporter
{
    private static readonly string[] DimensionReferencePointNames =
    {
        "FirstPoint", "SecondPoint", "AngleVertex", "DimensionArc", "Center",
        "FirstPointLine1", "SecondPointLine1", "FirstPointLine2", "SecondPointLine2",
        "FeatureLocation", "LeaderEndpoint", "LeaderEndPoint", "Origin", "ReferencePoint", "ChordPoint"
    };

    private static CadDimensionEntity MapDimension(Dimension dimension, CommonEntity common, List<Diagnostic> diagnostics)
    {
        var kind = DimensionKind(dimension.GetType().Name);
        var references = new Dictionary<string, Point2D>(StringComparer.Ordinal);
        foreach (var name in DimensionReferencePointNames)
        {
            try
            {
                if (Property(dimension, name) is { } value) references[name] = Point(value);
            }
            catch (Exception exception)
            {
                diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_DIMENSION_POINT_WARNING", $"Unable to read dimension point {name}: {exception.Message}", new Dictionary<string, string> { ["Handle"] = common.Handle, ["Point"] = name }));
            }
        }

        double measurement;
        try { measurement = dimension.Measurement; }
        catch (Exception exception)
        {
            measurement = 0;
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_DIMENSION_MEASUREMENT_WARNING", $"Unable to evaluate dimension measurement: {exception.Message}", new Dictionary<string, string> { ["Handle"] = common.Handle }));
        }

        string text;
        try { text = NormalizeText(dimension.GetMeasurementText()); }
        catch (Exception exception)
        {
            text = NormalizeText(dimension.Text);
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_DIMENSION_TEXT_WARNING", $"Unable to format dimension text: {exception.Message}", new Dictionary<string, string> { ["Handle"] = common.Handle }));
        }

        var style = dimension.GetActiveDimensionStyle();
        var styleName = NameOf(dimension.Style);
        var styleScale = DoubleProperty(style, "ScaleFactor", 1);
        var textHeight = DoubleProperty(style, "TextHeight", 2.5) * Math.Max(styleScale, double.Epsilon);
        var arrowSize = DoubleProperty(style, "ArrowSize", 2.5) * Math.Max(styleScale, double.Epsilon);
        var rotation = DoubleProperty(dimension, "Rotation", dimension.TextRotation);
        var dimensionLineColor = Property(style, "DimensionLineColor") is global::ACadSharp.Color dimSource ? MapColor(dimSource) : (CadColor?)null;
        var extensionLineColor = Property(style, "ExtensionLineColor") is global::ACadSharp.Color extSource ? MapColor(extSource) : (CadColor?)null;
        var textColor = Property(style, "TextColor") is global::ACadSharp.Color textSource ? MapColor(textSource) : (CadColor?)null;
        var metadata = new Dictionary<string, string>(common.Metadata, StringComparer.Ordinal)
        {
            ["DimensionKind"] = kind.ToString(),
            ["DimensionStyle"] = styleName,
            ["DimensionMeasurement"] = measurement.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionFlags"] = dimension.Flags.ToString(),
            ["DimensionReferencePointCount"] = references.Count.ToString(CultureInfo.InvariantCulture),
            ["DimensionHorizontalDirection"] = DoubleProperty(dimension, "HorizontalDirection").ToString("R", CultureInfo.InvariantCulture),
            ["DimensionOrdinateTypeX"] = BoolProperty(dimension, "IsOrdinateTypeX").ToString(),
            ["DimensionTextHorizontalAlignment"] = StringProperty(style, "TextHorizontalAlignment"),
            ["DimensionTextVerticalAlignment"] = StringProperty(style, "TextVerticalAlignment"),
            ["DimensionTextInsideHorizontal"] = BoolProperty(style, "TextInsideHorizontal").ToString(),
            ["DimensionTextOutsideHorizontal"] = BoolProperty(style, "TextOutsideHorizontal").ToString(),
            ["DimensionTextMovement"] = StringProperty(style, "TextMovement")
        };
        if (dimensionLineColor is { } dimColor) metadata["DimensionLineColor"] = DescribeColor(dimColor);
        if (extensionLineColor is { } extColor) metadata["DimensionExtensionLineColor"] = DescribeColor(extColor);
        if (textColor is { } resolvedTextColor) metadata["DimensionTextColor"] = DescribeColor(resolvedTextColor);

        return new CadDimensionEntity(common.Handle, kind, Point(dimension.DefinitionPoint), Point(dimension.TextMiddlePoint), text, measurement, rotation, textHeight, arrowSize, styleName, references, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, metadata)
        {
            Presentation = MapDimensionPresentation(style),
            DimensionLineColor = dimensionLineColor,
            ExtensionLineColor = extensionLineColor,
            TextColor = textColor
        };
    }

    private static string DescribeColor(CadColor color) => color.Kind switch
    {
        CadColorKind.Aci => $"Aci:{color.Index}",
        CadColorKind.TrueColor => $"Rgb:{color.Red:X2}{color.Green:X2}{color.Blue:X2}",
        CadColorKind.ByBlock => "ByBlock",
        _ => "ByLayer"
    };

    private static CadDimensionKind DimensionKind(string typeName) => typeName switch
    {
        "DimensionLinear" => CadDimensionKind.Linear,
        "DimensionAligned" => CadDimensionKind.Aligned,
        "DimensionAngular2Line" => CadDimensionKind.Angular2Line,
        "DimensionAngular3Pt" => CadDimensionKind.Angular3Point,
        "DimensionRadius" => CadDimensionKind.Radius,
        "DimensionDiameter" => CadDimensionKind.Diameter,
        "DimensionOrdinate" => CadDimensionKind.Ordinate,
        "DimensionArc" => CadDimensionKind.ArcLength,
        _ => CadDimensionKind.Unknown
    };

    private static CadLeaderEntity MapLeader(Leader leader, CommonEntity common)
    {
        var annotation = leader.AssociatedAnnotation;
        var annotationText = annotation switch
        {
            MText mtext => NormalizeText(mtext.PlainText),
            TextEntity text => NormalizeText(text.Value),
            _ => string.Empty
        };
        Point2D? annotationPoint = annotation switch
        {
            MText mtext => Point(mtext.InsertPoint),
            TextEntity text => Point(text.InsertPoint),
            Insert insert => Point(insert.InsertPoint),
            _ => null
        };
        var annotationHandle = annotation is null ? string.Empty : annotation.Handle.ToString(CultureInfo.InvariantCulture);
        var annotationType = annotation?.ObjectName ?? string.Empty;
        var styleName = NameOf(leader.Style);
        var metadata = new Dictionary<string, string>(common.Metadata, StringComparer.Ordinal)
        {
            ["LeaderPathType"] = leader.PathType.ToString(),
            ["LeaderStyle"] = styleName,
            ["LeaderCreationType"] = leader.CreationType.ToString(),
            ["LeaderAssociatedHandle"] = annotationHandle,
            ["LeaderAssociatedType"] = annotationType
        };
        return new CadLeaderEntity(common.Handle, leader.Vertices.Select(vertex => Point(vertex)).ToArray(), leader.ArrowHeadEnabled, leader.PathType.ToString().Contains("Spline", StringComparison.OrdinalIgnoreCase), annotationHandle, annotationType, annotationText, annotationPoint, leader.TextHeight, styleName, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, metadata);
    }

    private static CadMultiLeaderEntity MapMultiLeader(MultiLeader multiLeader, CommonEntity common, List<Diagnostic> diagnostics)
    {
        var context = multiLeader.ContextData;
        var paths = new List<CadLeaderPath>();
        if (context is not null)
        {
            foreach (var root in EnumerableProperty(context, "LeaderRoots"))
            {
                var connection = Point(Property(root, "ConnectionPoint"));
                var direction = Point(Property(root, "Direction"));
                var landingDistance = DoubleProperty(root, "LandingDistance", multiLeader.LandingDistance);
                var landingEnd = new Point2D(connection.X + (direction.X * landingDistance), connection.Y + (direction.Y * landingDistance));
                foreach (var line in EnumerableProperty(root, "Lines"))
                {
                    var points = EnumerableProperty(line, "Points").Select(Point).ToList();
                    AppendDistinct(points, connection);
                    if (multiLeader.EnableDogleg && landingDistance > double.Epsilon) AppendDistinct(points, landingEnd);
                    if (points.Count == 0)
                    {
                        diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_MLEADER_EMPTY_PATH", "MLEADER line did not expose any points.", new Dictionary<string, string> { ["Handle"] = common.Handle }));
                        continue;
                    }
                    var pathType = StringProperty(line, "PathType");
                    var arrowSize = DoubleProperty(line, "ArrowheadSize", DoubleProperty(context, "ArrowheadSize", multiLeader.ArrowheadSize));
                    paths.Add(new CadLeaderPath(points, pathType.Contains("Spline", StringComparison.OrdinalIgnoreCase), arrowSize, connection, landingEnd));
                }
            }
        }

        var text = context is null ? string.Empty : NormalizeText(StringProperty(context, "TextLabel"));
        var textLocation = context is null ? Point2D.Origin : Point(Property(context, "TextLocation"));
        var textHeight = context is null ? 0 : DoubleProperty(context, "TextHeight");
        var textRotation = context is null ? 0 : DoubleProperty(context, "TextRotation");
        var contentType = multiLeader.ContentType.ToString();
        var styleName = NameOf(multiLeader.Style);
        var metadata = new Dictionary<string, string>(common.Metadata, StringComparer.Ordinal)
        {
            ["MultiLeaderContentType"] = contentType,
            ["MultiLeaderStyle"] = styleName,
            ["MultiLeaderPathType"] = multiLeader.PathType.ToString(),
            ["MultiLeaderPathCount"] = paths.Count.ToString(CultureInfo.InvariantCulture),
            ["MultiLeaderDogleg"] = multiLeader.EnableDogleg.ToString()
        };
        return new CadMultiLeaderEntity(common.Handle, paths, text, textLocation, textHeight, textRotation, contentType, multiLeader.EnableDogleg, multiLeader.LandingDistance, multiLeader.ArrowheadSize, styleName, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, metadata);
    }

    private static void AppendDistinct(List<Point2D> points, Point2D point)
    {
        if (points.Count == 0 || points[^1].DistanceTo(point) > 1e-9) points.Add(point);
    }
}
