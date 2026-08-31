using System.Collections;
using System.Globalization;
using System.Reflection;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>ACadSharp-only reader adapter. All values are copied into Spatial Viewer CAD records before return.</summary>
public sealed partial class ACadSharpCadImporter : IDocumentImporter
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".dxf", ".dwg" };
    public bool CanImport(string filePath) => Extensions.Contains(Path.GetExtension(filePath));
    public Task<ImportResult> ImportAsync(ImportRequest request, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => ImportCore(request, progress, cancellationToken), cancellationToken);
    }
    private static ImportResult ImportCore(ImportRequest request, IProgress<ImportProgress>? progress, CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>();
        if (!File.Exists(request.FilePath)) return new ImportResult(null, new[] { new Diagnostic(DiagnosticSeverity.Error, "CAD_FILE_NOT_FOUND", $"CAD file does not exist: {request.FilePath}") });
        var extension = Path.GetExtension(request.FilePath).ToLowerInvariant();
        if (!Extensions.Contains(extension)) return new ImportResult(null, new[] { new Diagnostic(DiagnosticSeverity.Error, "CAD_UNSUPPORTED_EXTENSION", $"Unsupported CAD extension: {extension}") });
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); progress?.Report(new ImportProgress("Reader", .1, "Reading CAD file"));
            using var reader = CadReaderFactory.CreateReader(request.FilePath);
            reader.OnNotification += (_, args) => diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_READER_WARNING", args.Message));
            var source = reader.Read(); cancellationToken.ThrowIfCancellationRequested(); progress?.Report(new ImportProgress("Adapter", .55, "Copying reader data into CAD model"));
            var globalLineTypeScale = source.Header.LineTypeScale;
            var layers = source.Layers.Select(layer => new CadLayer(layer.Name, MapColor(layer.Color), layer.IsOn, false, NameOf(layer.LineType), ParseLineWeight(layer.LineWeight))).ToArray();
            var entities = source.Entities.Select(entity => MapEntity(entity, diagnostics, globalLineTypeScale)).ToArray();
            var blocks = MapBlocks(source, diagnostics, globalLineTypeScale);
            ValidateBlockReferences(entities.Concat(blocks.SelectMany(block => block.Entities)), blocks, diagnostics);
            var metadata = new Dictionary<string, string>
            {
                ["Reader"] = "ACadSharp",
                ["ReaderVersion"] = typeof(CadDocument).Assembly.GetName().Version?.ToString() ?? "unknown",
                ["EntityCount"] = entities.Length.ToString(CultureInfo.InvariantCulture),
                ["BlockCount"] = blocks.Length.ToString(CultureInfo.InvariantCulture),
                ["LineTypeScale"] = globalLineTypeScale.ToString(CultureInfo.InvariantCulture)
            };
            if (entities.OfType<CadUnsupportedEntity>().Any()) diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_PARTIAL_IMPORT", $"Skipped {entities.OfType<CadUnsupportedEntity>().Count()} unsupported entity or entities."));
            var document = new SpatialViewer.Formats.Cad.CadDocument(Path.GetFileName(request.FilePath), extension.TrimStart('.').ToUpperInvariant(), source.Header.Version.ToString(), MapUnits(source.Header.InsUnits.ToString()), layers, blocks, entities, diagnostics, metadata);
            progress?.Report(new ImportProgress("Scene", 1, "CAD scene ready"));
            return new ImportResult(document, diagnostics);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "CAD_READER_FAILURE", $"Unable to import CAD file: {exception.Message}", Exception: exception)); return new ImportResult(null, diagnostics); }
    }
    private static CadEntity MapEntity(Entity entity, List<Diagnostic> diagnostics, double globalLineTypeScale)
    {
        var common = Common(entity, globalLineTypeScale);
        return entity switch
        {
            global::ACadSharp.Entities.Point point => new CadPointEntity(common.Handle, Point(point.Location), common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
            Line line => new CadLineEntity(common.Handle, Point(line.StartPoint), Point(line.EndPoint), common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
            Arc arc => new CadArcEntity(common.Handle, Point(arc.Center), arc.Radius, Degrees(arc.StartAngle), NormalizeSweep(Degrees(arc.EndAngle) - Degrees(arc.StartAngle)), common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
            Circle circle => new CadCircleEntity(common.Handle, Point(circle.Center), circle.Radius, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
            Ellipse ellipse => MapEllipse(ellipse, common),
            LwPolyline polyline => MapLwPolyline(polyline, common),
            Polyline2D polyline => MapPolyline2D(polyline, common),
            global::ACadSharp.Entities.Spline spline => new CadSplineEntity(common.Handle, MapSplineDefinition(spline), common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
            Hatch hatch => MapHatch(hatch, common, diagnostics),
            AttributeDefinition attribute => MapAttribute(attribute, common, true),
            AttributeEntity attribute => MapAttribute(attribute, common, false),
            Dimension dimension => MapDimension(dimension, common, diagnostics),
            Leader leader => MapLeader(leader, common),
            MultiLeader multiLeader => MapMultiLeader(multiLeader, common, diagnostics),
            MText text => new CadTextEntity(common.Handle, Point(text.InsertPoint), NormalizeText(text.PlainText), text.Height, Degrees(text.Rotation), text.HorizontalWidth, true, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
            TextEntity text => new CadTextEntity(common.Handle, Point(text.InsertPoint), NormalizeText(text.Value), text.Height, Degrees(text.Rotation), 0, false, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
            Insert insert => MapInsert(insert, common, diagnostics, globalLineTypeScale),
            _ => Unsupported(entity, common, diagnostics)
        };
    }
    private static CadPolylineEntity MapLwPolyline(LwPolyline polyline, CommonEntity common) => new(common.Handle, polyline.Vertices.Select(vertex => Point(vertex.Location)).ToArray(), polyline.IsClosed, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata)
    {
        Bulges = polyline.Vertices.Select(vertex => vertex.Bulge).ToArray()
    };
    private static CadPolylineEntity MapPolyline2D(Polyline2D polyline, CommonEntity common)
    {
        var vertices = EnumerableProperty(polyline, "Vertices").ToArray();
        return new CadPolylineEntity(common.Handle, vertices.Select(vertex => Point(Property(vertex, "Location") ?? Property(vertex, "Position"))).ToArray(), polyline.IsClosed, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata)
        {
            Bulges = vertices.Select(vertex => DoubleProperty(vertex, "Bulge")).ToArray()
        };
    }
    private static CadEllipseEntity MapEllipse(Ellipse ellipse, CommonEntity common)
    {
        var axis = Point(ellipse.MajorAxisEndPoint);
        var radiusX = Math.Sqrt((axis.X * axis.X) + (axis.Y * axis.Y));
        return new CadEllipseEntity(common.Handle, Point(ellipse.Center), radiusX, radiusX * ellipse.RadiusRatio, Math.Atan2(axis.Y, axis.X), common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata);
    }
    private static CadSplineDefinition MapSplineDefinition(object spline)
    {
        var controlObjects = EnumerableProperty(spline, "ControlPoints").ToArray();
        var controlPoints = controlObjects.Select(Point).ToArray();
        var knots = EnumerableProperty(spline, "Knots").Select(DoubleValue).ToArray();
        var weights = EnumerableProperty(spline, "Weights").Select(DoubleValue).ToArray();
        if (weights.Length != controlPoints.Length)
        {
            var zWeights = controlObjects.Select(point => DoubleProperty(point, "Z")).ToArray();
            weights = zWeights.Any(weight => Math.Abs(weight) > double.Epsilon) ? zWeights : Array.Empty<double>();
        }
        var fitPoints = EnumerableProperty(spline, "FitPoints").Select(Point).ToArray();
        return new CadSplineDefinition(IntProperty(spline, "Degree", 3), controlPoints, knots, weights, fitPoints, BoolProperty(spline, "IsClosed"), BoolProperty(spline, "IsPeriodic"));
    }
    private static CadHatchEntity MapHatch(Hatch hatch, CommonEntity common, List<Diagnostic> diagnostics)
    {
        var loops = EnumerableProperty(hatch, "Paths").Select(path => MapHatchLoop(path, diagnostics, common.Handle)).Where(loop => loop.Edges.Count > 0).ToArray();
        var metadata = new Dictionary<string, string>(common.Metadata, StringComparer.Ordinal)
        {
            ["HatchStyle"] = StringProperty(hatch, "Style"),
            ["HatchPatternType"] = StringProperty(hatch, "PatternType")
        };
        return new CadHatchEntity(common.Handle, loops, BoolProperty(hatch, "IsSolid"), NameOf(Property(hatch, "Pattern")), Degrees(DoubleProperty(hatch, "PatternAngle")), DoubleProperty(hatch, "PatternScale", 1), common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, metadata);
    }
    private static CadHatchLoop MapHatchLoop(object path, List<Diagnostic> diagnostics, string hatchHandle)
    {
        var edges = new List<CadHatchEdge>();
        foreach (var edge in EnumerableProperty(path, "Edges"))
        {
            var mapped = MapHatchEdge(edge);
            if (mapped is null)
            {
                diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_HATCH_EDGE_UNSUPPORTED", $"Unsupported hatch boundary edge skipped: {edge.GetType().Name}", new Dictionary<string, string> { ["Handle"] = hatchHandle }));
                continue;
            }
            edges.Add(mapped);
        }
        return new CadHatchLoop(edges, StringProperty(path, "Flags"));
    }
    private static CadHatchEdge? MapHatchEdge(object edge)
    {
        switch (edge.GetType().Name)
        {
            case "Line":
                return new CadHatchLineEdge(Point(Property(edge, "Start")), Point(Property(edge, "End")));
            case "Arc":
            {
                var start = Degrees(DoubleProperty(edge, "StartAngle"));
                var end = Degrees(DoubleProperty(edge, "EndAngle"));
                var counterClockwise = BoolProperty(edge, "CounterClockWise", true);
                var sweep = counterClockwise ? NormalizeSweep(end - start) : -NormalizeSweep(start - end);
                return new CadHatchArcEdge(Point(Property(edge, "Center")), DoubleProperty(edge, "Radius"), start, sweep);
            }
            case "Ellipse":
            {
                var start = Degrees(DoubleProperty(edge, "StartAngle"));
                var end = Degrees(DoubleProperty(edge, "EndAngle"));
                var counterClockwise = BoolProperty(edge, "CounterClockWise", true);
                var sweep = counterClockwise ? NormalizeSweep(end - start) : -NormalizeSweep(start - end);
                return new CadHatchEllipseEdge(Point(Property(edge, "Center")), Point(Property(edge, "MajorAxisEndPoint")), DoubleProperty(edge, "RadiusRatio", 1), start, sweep);
            }
            case "Polyline":
            {
                var vertices = EnumerableProperty(edge, "Vertices").ToArray();
                var points = vertices.Select(Point).ToArray();
                var bulges = EnumerableProperty(edge, "Bulges").Select(DoubleValue).ToArray();
                if (bulges.Length != points.Length) bulges = vertices.Select(vertex => DoubleProperty(vertex, "Z")).ToArray();
                return new CadHatchPolylineEdge(points, bulges, BoolProperty(edge, "IsClosed", true));
            }
            case "Spline":
                return new CadHatchSplineEdge(MapSplineDefinition(edge));
            default:
                return null;
        }
    }
    private static CadAttributeEntity MapAttribute(AttributeBase attribute, CommonEntity common, bool isDefinition)
    {
        var flags = attribute.Flags.ToString();
        var value = attribute.MText is { } mtext && !string.IsNullOrWhiteSpace(mtext.PlainText) ? mtext.PlainText : attribute.Value;
        var prompt = isDefinition && attribute is AttributeDefinition definition ? definition.Prompt : string.Empty;
        var metadata = new Dictionary<string, string>(common.Metadata, StringComparer.Ordinal) { ["AttributeFlags"] = flags };
        return new CadAttributeEntity(common.Handle, Point(attribute.InsertPoint), attribute.Tag ?? string.Empty, NormalizeText(value), attribute.Height, Degrees(attribute.Rotation), isDefinition, NormalizeText(prompt), flags.Contains("Constant", StringComparison.OrdinalIgnoreCase), common.Layer, common.Color, common.Visible && !flags.Contains("Invisible", StringComparison.OrdinalIgnoreCase), common.LineType, common.LineWeight, metadata);
    }
    private static CadBlockReferenceEntity MapInsert(Insert insert, CommonEntity common, List<Diagnostic> diagnostics, double globalLineTypeScale)
    {
        var attributes = new List<CadAttributeEntity>();
        foreach (var attribute in insert.Attributes)
        {
            try { attributes.Add(MapAttribute(attribute, Common(attribute, globalLineTypeScale), false)); }
            catch (Exception exception) { diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_ATTRIBUTE_IMPORT_WARNING", $"Unable to copy block attribute: {exception.Message}", new Dictionary<string, string> { ["InsertHandle"] = common.Handle })); }
        }
        return new CadBlockReferenceEntity(common.Handle, insert.Block?.Name ?? "<missing>", Point(insert.InsertPoint), Degrees(insert.Rotation), insert.XScale, insert.YScale, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata) { Attributes = attributes };
    }
    private static CadUnsupportedEntity Unsupported(Entity entity, CommonEntity common, List<Diagnostic> diagnostics) { var name = entity.ObjectName; diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_UNSUPPORTED_ENTITY", $"Unsupported CAD entity skipped: {name}", new Dictionary<string, string> { ["Handle"] = common.Handle, ["Layer"] = common.Layer })); return new CadUnsupportedEntity(common.Handle, name, common.Layer, common.Metadata); }
    private static CadBlockDefinition[] MapBlocks(global::ACadSharp.CadDocument source, List<Diagnostic> diagnostics, double globalLineTypeScale)
    {
        var definitions = new List<CadBlockDefinition>();
        foreach (var record in EnumerableProperty(source, "BlockRecords"))
        {
            var name = StringProperty(record, "Name"); if (string.IsNullOrWhiteSpace(name) || name.StartsWith('*')) continue;
            var block = Property(record, "Block") ?? record; var basePoint = Point(Property(block, "BasePoint")); var entities = EnumerableProperty(record, "Entities").OfType<Entity>().Select(entity => MapEntity(entity, diagnostics, globalLineTypeScale)).ToArray();
            definitions.Add(new CadBlockDefinition(name, basePoint, entities));
        }
        return definitions.ToArray();
    }
    private static void ValidateBlockReferences(IEnumerable<CadEntity> entities, IReadOnlyList<CadBlockDefinition> blocks, List<Diagnostic> diagnostics)
    {
        var names = new HashSet<string>(blocks.Select(block => block.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var reference in entities.OfType<CadBlockReferenceEntity>().Where(reference => !names.Contains(reference.BlockName))) diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_INVALID_BLOCK_REFERENCE", $"Block reference skipped because definition was not found: {reference.BlockName}", new Dictionary<string, string> { ["Handle"] = reference.Handle }));
    }
    private static CommonEntity Common(Entity entity, double globalLineTypeScale)
    {
        var activeLineType = entity.GetActiveLineType();
        var metadata = new Dictionary<string, string>
        {
            ["Normal"] = Property(entity, "Normal")?.ToString() ?? string.Empty,
            ["LineTypeScale"] = entity.LineTypeScale.ToString(CultureInfo.InvariantCulture),
            ["GlobalLineTypeScale"] = globalLineTypeScale.ToString(CultureInfo.InvariantCulture)
        };
        var pattern = activeLineType.Segments.Select(segment => segment.Length).Where(double.IsFinite).ToArray();
        if (pattern.Length > 0) metadata["LineTypePattern"] = string.Join(';', pattern.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
        return new CommonEntity(entity.Handle.ToString(CultureInfo.InvariantCulture), entity.Layer?.Name ?? "0", MapColor(entity.Color), !entity.IsInvisible, NameOf(activeLineType), ParseLineWeight(entity.LineWeight), metadata);
    }
    private static CadColor MapColor(global::ACadSharp.Color color) { if (color.IsByBlock) return CadColor.ByBlock; if (color.IsByLayer) return CadColor.ByLayer; if (color.IsTrueColor) return CadColor.FromRgb(color.R, color.G, color.B); return CadColor.FromAci(color.Index); }
    private static CadUnits MapUnits(string value) => value.ToLowerInvariant() switch { var x when x.Contains("millimeter") => CadUnits.Millimetres, var x when x.Contains("centimeter") => CadUnits.Centimetres, var x when x.Contains("meter") => CadUnits.Metres, var x when x.Contains("inch") => CadUnits.Inches, var x when x.Contains("foot") || x.Contains("feet") => CadUnits.Feet, _ => CadUnits.Unitless };
    private static string NormalizeText(string? value) => string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\P", "\n", StringComparison.OrdinalIgnoreCase).Replace("\\~", " ", StringComparison.Ordinal);
    private static double Degrees(double value) => value * Math.PI / 180d;
    private static double NormalizeSweep(double sweep) => sweep <= 0 ? sweep + (Math.PI * 2) : sweep;
    private static Point2D Point(object? point) => point is null ? Point2D.Origin : new(DoubleProperty(point, "X"), DoubleProperty(point, "Y"));
    private static object? Property(object? source, string name) => source?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(source);
    private static IEnumerable<object> EnumerableProperty(object source, string name) => Property(source, name) is IEnumerable enumerable ? enumerable.Cast<object>() : Array.Empty<object>();
    private static string StringProperty(object? source, string name) => Property(source, name)?.ToString() ?? string.Empty;
    private static string NameOf(object? source) => StringProperty(source, "Name") is { Length: > 0 } name ? name : "Continuous";
    private static double DoubleProperty(object? source, string name, double fallback = 0) => Property(source, name) is { } value ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : fallback;
    private static double DoubleValue(object? value) => value is null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    private static int IntProperty(object? source, string name, int fallback = 0) => Property(source, name) is { } value ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : fallback;
    private static bool BoolProperty(object? source, string name, bool fallback = false) => Property(source, name) is { } value ? Convert.ToBoolean(value, CultureInfo.InvariantCulture) : fallback;
    private static int? ParseLineWeight(object? value) => int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : null;
    private sealed record CommonEntity(string Handle, string Layer, CadColor Color, bool Visible, string LineType, int? LineWeight, IReadOnlyDictionary<string, string> Metadata);
}
