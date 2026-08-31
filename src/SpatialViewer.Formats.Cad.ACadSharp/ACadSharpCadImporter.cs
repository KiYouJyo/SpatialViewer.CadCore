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
public sealed class ACadSharpCadImporter : IDocumentImporter
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
            MText text => new CadTextEntity(common.Handle, Point(text.InsertPoint), NormalizeText(text.PlainText), text.Height, Degrees(text.Rotation), text.HorizontalWidth, true, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
            TextEntity text => new CadTextEntity(common.Handle, Point(text.InsertPoint), NormalizeText(text.Value), text.Height, Degrees(text.Rotation), 0, false, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
            Insert insert => new CadBlockReferenceEntity(common.Handle, insert.Block?.Name ?? "<missing>", Point(insert.InsertPoint), Degrees(insert.Rotation), insert.XScale, insert.YScale, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata),
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
    private static CadEllipseEntity MapEllipse(Ellipse ellipse, CommonEntity common) { var axis = Point(ellipse.MajorAxis); var rx = Math.Sqrt((axis.X * axis.X) + (axis.Y * axis.Y)); return new CadEllipseEntity(common.Handle, Point(ellipse.Center), rx, rx * ellipse.RadiusRatio, Math.Atan2(axis.Y, axis.X), common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata); }
    private static CadUnsupportedEntity Unsupported(Entity entity, CommonEntity common, List<Diagnostic> diagnostics) { var name = entity.ObjectName; diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_UNSUPPORTED_ENTITY", $"Unsupported CAD entity skipped: {name}", new Dictionary<string, string> { ["Handle"] = common.Handle, ["Layer"] = common.Layer })); return new CadUnsupportedEntity(common.Handle, name, common.Layer, common.Metadata); }
    private static CadBlockDefinition[] MapBlocks(global::ACadSharp.CadDocument source, List<Diagnostic> diagnostics, double globalLineTypeScale)
    {
        var definitions = new List<CadBlockDefinition>();
        foreach (var record in EnumerableProperty(source, "BlockRecords"))
        {
            var name = StringProperty(record, "Name"); if (string.IsNullOrWhiteSpace(name) || name.StartsWith('*')) continue;
            var block = Property(record, "Block") ?? record; var basePoint = Point(Property(block, "BasePoint")); var entities = EnumerableProperty(record, "Entities").OfType<Entity>().Select(entity => MapEntity(entity, diagnostics, globalLineTypeScale)).ToArray();
            if (entities.Length > 0) definitions.Add(new CadBlockDefinition(name, basePoint, entities));
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
    private static double DoubleProperty(object? source, string name) => Convert.ToDouble(Property(source, name) ?? 0d, CultureInfo.InvariantCulture);
    private static int? ParseLineWeight(object? value) => int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : null;
    private sealed record CommonEntity(string Handle, string Layer, CadColor Color, bool Visible, string LineType, int? LineWeight, IReadOnlyDictionary<string, string> Metadata);
}
