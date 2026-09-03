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
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ImportProgress("Reader", .1, "Reading CAD file"));
            using var reader = CadReaderFactory.CreateReader(request.FilePath);
            reader.OnNotification += (_, args) => diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_READER_WARNING", args.Message));
            var source = reader.Read();
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ImportProgress("Adapter", .55, "Copying reader data into CAD model"));
            var globalLineTypeScale = source.Header.LineTypeScale;
            var customClasses = MapCustomClasses(source);
            var layers = source.Layers.Select(layer => new CadLayer(layer.Name, MapColor(layer.Color), layer.IsOn, false, NameOf(layer.LineType), ParseLineWeight(layer.LineWeight))).ToArray();
            var entities = source.Entities.Select(entity => MapEntity(entity, diagnostics, globalLineTypeScale)).ToArray();
            var blocks = MapBlocks(source, diagnostics, globalLineTypeScale);
            var layouts = MapLayouts(source, diagnostics, globalLineTypeScale);

            var shxFonts = new ACadSharpShxFontLoading(request, diagnostics);
            entities = entities.Select(shxFonts.Apply).ToArray();
            blocks = blocks.Select(shxFonts.Apply).ToArray();
            layouts = layouts.Select(shxFonts.Apply).ToArray();

            var allEntities = entities
                .Concat(blocks.SelectMany(block => block.Entities))
                .Concat(layouts.Where(layout => layout.IsPaperSpace).SelectMany(layout => layout.Entities))
                .ToArray();
            ValidateBlockReferences(allEntities, blocks, diagnostics);
            var customEntities = allEntities.OfType<CadCustomEntity>().ToArray();
            var tianzhengClasses = customClasses.Where(definition => definition.IsTianzheng).ToArray();
            var tianzhengEntities = customEntities.Where(entity => entity.IsTianzheng).ToArray();
            var rawScan = ACadSharpCustomPayloadContext.Snapshot() ?? DxfCustomPayloadScanResult.Empty;
            var paperLayouts = layouts.Where(layout => layout.IsPaperSpace).ToArray();
            var metadata = new Dictionary<string, string>
            {
                ["Reader"] = "ACadSharp",
                ["ReaderVersion"] = typeof(CadDocument).Assembly.GetName().Version?.ToString() ?? "unknown",
                ["EntityCount"] = entities.Length.ToString(CultureInfo.InvariantCulture),
                ["BlockCount"] = blocks.Length.ToString(CultureInfo.InvariantCulture),
                ["AnonymousBlockDefinitionCount"] = blocks.Count(block => block.Name.StartsWith('*')).ToString(CultureInfo.InvariantCulture),
                ["LayoutCount"] = layouts.Length.ToString(CultureInfo.InvariantCulture),
                ["PaperLayoutCount"] = paperLayouts.Length.ToString(CultureInfo.InvariantCulture),
                ["PaperSpaceEntityCount"] = paperLayouts.Sum(layout => layout.Entities.Count).ToString(CultureInfo.InvariantCulture),
                ["PaperViewportCount"] = paperLayouts.Sum(layout => layout.Viewports.Count).ToString(CultureInfo.InvariantCulture),
                ["LineTypeScale"] = globalLineTypeScale.ToString(CultureInfo.InvariantCulture),
                ["ShxSearchDirectoryCount"] = shxFonts.SearchDirectoryCount.ToString(CultureInfo.InvariantCulture),
                ["ShxRequestedFontCount"] = shxFonts.RequestedFontCount.ToString(CultureInfo.InvariantCulture),
                ["ShxLoadedFontCount"] = shxFonts.LoadedFontCount.ToString(CultureInfo.InvariantCulture),
                ["CustomClassCount"] = customClasses.Length.ToString(CultureInfo.InvariantCulture),
                ["CustomEntityCount"] = customEntities.Length.ToString(CultureInfo.InvariantCulture),
                ["CustomProxyGraphicEntityCount"] = customEntities.Count(entity => entity.Representation == CadCustomEntityRepresentation.ProxyGraphics).ToString(CultureInfo.InvariantCulture),
                ["TianzhengDetected"] = (tianzhengClasses.Length > 0 || tianzhengEntities.Length > 0).ToString(),
                ["TianzhengClassCount"] = tianzhengClasses.Length.ToString(CultureInfo.InvariantCulture),
                ["TianzhengEntityCount"] = tianzhengEntities.Length.ToString(CultureInfo.InvariantCulture),
                ["RawDxfCapturedCustomRecordCount"] = rawScan.CapturedRecordCount.ToString(CultureInfo.InvariantCulture),
                ["RawDxfTruncatedCustomRecordCount"] = rawScan.TruncatedRecordCount.ToString(CultureInfo.InvariantCulture),
                ["RawDxfScanBinary"] = rawScan.IsBinaryDxf.ToString(),
                ["RawDxfScanFailed"] = rawScan.ScanFailed.ToString()
            };
            if (customEntities.Length > 0)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "CAD_CUSTOM_ENTITY_PRESERVED",
                    $"Preserved {customEntities.Length} application-defined CAD entity or entities for compatibility processing; native semantics are not decoded yet.",
                    new Dictionary<string, string>
                    {
                        ["CustomEntityCount"] = customEntities.Length.ToString(CultureInfo.InvariantCulture),
                        ["ProxyGraphicEntityCount"] = customEntities.Count(entity => entity.Representation == CadCustomEntityRepresentation.ProxyGraphics).ToString(CultureInfo.InvariantCulture),
                        ["TianzhengEntityCount"] = tianzhengEntities.Length.ToString(CultureInfo.InvariantCulture)
                    }));

                if (extension == ".dxf" && rawScan.IsBinaryDxf)
                    diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_CUSTOM_RAW_DXF_BINARY_UNAVAILABLE", "Application-defined entities were preserved, but raw proprietary group capture currently supports text DXF only."));
                else if (extension == ".dxf" && rawScan.ScanFailed)
                    diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_CUSTOM_RAW_DXF_SCAN_FAILED", "Application-defined entities were preserved, but the raw proprietary group pre-scan failed. Native decoding must not assume that missing raw fields were absent from the source."));
            }
            if (entities.OfType<CadUnsupportedEntity>().Any()) diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_PARTIAL_IMPORT", $"Skipped {entities.OfType<CadUnsupportedEntity>().Count()} unsupported entity or entities."));
            var document = new SpatialViewer.Formats.Cad.CadDocument(Path.GetFileName(request.FilePath), extension.TrimStart('.').ToUpperInvariant(), source.Header.Version.ToString(), MapUnits(source.Header.InsUnits.ToString()), layers, blocks, entities, diagnostics, metadata, layouts)
            {
                CustomClasses = customClasses
            };
            progress?.Report(new ImportProgress("Scene", 1, "CAD scene ready"));
            return new ImportResult(document, diagnostics);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "CAD_READER_FAILURE", $"Unable to import CAD file: {exception.Message}", Exception: exception));
            return new ImportResult(null, diagnostics);
        }
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
            MText text => MapMText(text, common),
            TextEntity text => MapText(text, common),
            Insert insert => MapInsert(insert, common, diagnostics, globalLineTypeScale),
            _ when IsCustomEntity(entity) => MapCustomEntity(entity, common),
            _ => Unsupported(entity, common, diagnostics)
        };
    }

    private static CadTextEntity MapText(TextEntity text, CommonEntity common)
    {
        var raw = text.Value ?? string.Empty;
        return new CadTextEntity(common.Handle, Point(text.InsertPoint), NormalizeText(raw), text.Height, Degrees(text.Rotation), 0, false, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata)
        {
            Presentation = MapTextPresentation(text, raw, false)
        };
    }

    private static CadTextEntity MapMText(MText text, CommonEntity common)
    {
        var raw = text.Value ?? string.Empty;
        return new CadTextEntity(common.Handle, Point(text.InsertPoint), NormalizeText(text.PlainText), text.Height, Degrees(text.Rotation), text.HorizontalWidth, true, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, common.Metadata)
        {
            Presentation = MapTextPresentation(text, raw, true)
        };
    }

    private static CadTextPresentation MapTextPresentation(object text, string rawText, bool isMText)
    {
        var style = Property(text, "Style");
        var styleName = StringProperty(style, "Name");
        if (string.IsNullOrWhiteSpace(styleName)) styleName = "Standard";
        var styleWidth = Positive(DoubleProperty(style, "Width", 1));
        var entityWidth = isMText ? 1 : Positive(DoubleProperty(text, "WidthFactor", 1));
        var oblique = isMText ? DoubleProperty(style, "ObliqueAngle") : DoubleProperty(text, "ObliqueAngle");
        if (Math.Abs(oblique) <= double.Epsilon) oblique = DoubleProperty(style, "ObliqueAngle");
        var mirror = $"{StringProperty(text, "Mirror")} {StringProperty(style, "MirrorFlag")}";
        Point2D? alignmentPoint = null;
        if (!isMText && Property(text, "AlignmentPoint") is { } point) alignmentPoint = Point(point);
        var horizontal = isMText ? "Left" : StringProperty(text, "HorizontalAlignment");
        var vertical = isMText ? "Top" : StringProperty(text, "VerticalAlignment");
        if (string.IsNullOrWhiteSpace(horizontal)) horizontal = "Left";
        if (string.IsNullOrWhiteSpace(vertical)) vertical = "Baseline";
        var attachment = isMText ? StringProperty(text, "AttachmentPoint") : "TopLeft";
        if (string.IsNullOrWhiteSpace(attachment)) attachment = "TopLeft";
        return new CadTextPresentation(
            styleName,
            StringProperty(style, "Filename"),
            StringProperty(style, "BigFontFilename"),
            styleWidth * entityWidth,
            oblique,
            horizontal,
            vertical,
            attachment,
            alignmentPoint,
            isMText ? DoubleProperty(text, "RectangleWidth") : 0,
            isMText ? Positive(DoubleProperty(text, "LineSpacing", 1)) : 1,
            mirror.Contains("Backward", StringComparison.OrdinalIgnoreCase),
            mirror.Contains("UpsideDown", StringComparison.OrdinalIgnoreCase),
            BoolProperty(style, "IsShapeFile"),
            rawText);
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
        return new CadHatchEntity(common.Handle, loops, BoolProperty(hatch, "IsSolid"), NameOf(Property(hatch, "Pattern")), Degrees(DoubleProperty(hatch, "PatternAngle")), DoubleProperty(hatch, "PatternScale", 1), common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, metadata)
        {
            PatternLines = MapHatchPatternLines(hatch)
        };
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
        var hasMText = attribute.MText is { } mtext && !string.IsNullOrWhiteSpace(mtext.PlainText);
        var raw = hasMText ? attribute.MText.Value : attribute.Value;
        var value = hasMText ? attribute.MText.PlainText : attribute.Value;
        var prompt = isDefinition && attribute is AttributeDefinition definition ? definition.Prompt : string.Empty;
        var metadata = new Dictionary<string, string>(common.Metadata, StringComparer.Ordinal) { ["AttributeFlags"] = flags };
        return new CadAttributeEntity(common.Handle, Point(attribute.InsertPoint), attribute.Tag ?? string.Empty, NormalizeText(value), attribute.Height, Degrees(attribute.Rotation), isDefinition, NormalizeText(prompt), flags.Contains("Constant", StringComparison.OrdinalIgnoreCase), common.Layer, common.Color, common.Visible && !flags.Contains("Invisible", StringComparison.OrdinalIgnoreCase), common.LineType, common.LineWeight, metadata)
        {
            Presentation = MapTextPresentation(attribute, raw ?? string.Empty, false)
        };
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

    private static CadUnsupportedEntity Unsupported(Entity entity, CommonEntity common, List<Diagnostic> diagnostics)
    {
        var name = entity.ObjectName;
        diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "CAD_UNSUPPORTED_ENTITY", $"Unsupported CAD entity skipped: {name}", new Dictionary<string, string> { ["Handle"] = common.Handle, ["Layer"] = common.Layer }));
        return new CadUnsupportedEntity(common.Handle, name, common.Layer, common.Metadata);
    }

    private static CadBlockDefinition[] MapBlocks(global::ACadSharp.CadDocument source, List<Diagnostic> diagnostics, double globalLineTypeScale)
    {
        var definitions = new List<CadBlockDefinition>();
        foreach (var record in EnumerableProperty(source, "BlockRecords"))
        {
            var name = StringProperty(record, "Name");
            if (string.IsNullOrWhiteSpace(name) || IsSpaceBlockRecordName(name)) continue;
            var block = Property(record, "Block") ?? record;
            var basePoint = Point(Property(block, "BasePoint"));
            var entities = EnumerableProperty(record, "Entities").OfType<Entity>().Select(entity => MapEntity(entity, diagnostics, globalLineTypeScale)).ToArray();
            definitions.Add(new CadBlockDefinition(name, basePoint, entities));
        }
        return definitions.ToArray();
    }

    private static CadLayoutDefinition[] MapLayouts(global::ACadSharp.CadDocument source, List<Diagnostic> diagnostics, double globalLineTypeScale)
    {
        var layouts = new List<CadLayoutDefinition>();
        foreach (var layout in EnumerableProperty(source, "Layouts"))
        {
            var name = StringProperty(layout, "Name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var paperSize = new Size2D(DoubleProperty(layout, "PaperWidth"), DoubleProperty(layout, "PaperHeight"));
            var minLimits = Point(Property(layout, "MinLimits"));
            var maxLimits = Point(Property(layout, "MaxLimits"));
            var minExtents = Point(Property(layout, "MinExtents"));
            var maxExtents = Point(Property(layout, "MaxExtents"));
            var block = Property(layout, "AssociatedBlock");
            var paperEntities = block is null
                ? Array.Empty<CadEntity>()
                : EnumerableProperty(block, "Entities").OfType<Entity>().Where(entity => entity is not Viewport).Select(entity => MapEntity(entity, diagnostics, globalLineTypeScale)).ToArray();
            var viewports = EnumerableProperty(layout, "Viewports").OfType<Viewport>().Select(MapViewport).ToArray();
            var metadata = new Dictionary<string, string>
            {
                ["LayoutFlags"] = StringProperty(layout, "LayoutFlags"),
                ["AssociatedBlock"] = StringProperty(block, "Name")
            };
            layouts.Add(new CadLayoutDefinition(name, IntProperty(layout, "TabOrder"), BoolProperty(layout, "IsPaperSpace"), paperSize, Bounds(minLimits, maxLimits), Bounds(minExtents, maxExtents), paperEntities, viewports, metadata));
        }
        return layouts.OrderBy(layout => layout.TabOrder).ToArray();
    }

    private static CadViewportDefinition MapViewport(Viewport viewport)
    {
        var boundary = viewport.Boundary;
        var boundaryPoints = boundary switch
        {
            LwPolyline polyline => polyline.Vertices.Select(vertex => Point(vertex.Location)).ToArray(),
            Polyline2D polyline => EnumerableProperty(polyline, "Vertices").Select(vertex => Point(Property(vertex, "Location") ?? Property(vertex, "Position"))).ToArray(),
            _ => Array.Empty<Point2D>()
        };
        var metadata = new Dictionary<string, string>
        {
            ["ActiveStatus"] = viewport.ActiveStatus.ToString(CultureInfo.InvariantCulture),
            ["ViewDirection"] = viewport.ViewDirection.ToString(CultureInfo.InvariantCulture),
            ["ViewHeight"] = viewport.ViewHeight.ToString("R", CultureInfo.InvariantCulture)
        };
        return new CadViewportDefinition(
            viewport.Handle.ToString(CultureInfo.InvariantCulture),
            Point(viewport.Center),
            new Size2D(viewport.Width, viewport.Height),
            Point(viewport.ViewCenter),
            Point(viewport.ViewTarget),
            viewport.ViewHeight,
            Degrees(viewport.TwistAngle),
            viewport.ActiveStatus != 0,
            viewport.RepresentsPaper,
            viewport.FrozenLayers.Select(layer => layer.Name).ToArray(),
            boundary?.Handle.ToString(CultureInfo.InvariantCulture),
            boundaryPoints,
            metadata);
    }

    private static BoundingBox2D Bounds(Point2D first, Point2D second) => new(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Max(first.X, second.X), Math.Max(first.Y, second.Y));

    private static bool IsSpaceBlockRecordName(string name)
        => name.StartsWith("*Model_Space", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase);

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

    private static CadColor MapColor(global::ACadSharp.Color color)
    {
        if (color.IsByBlock) return CadColor.ByBlock;
        if (color.IsByLayer) return CadColor.ByLayer;
        if (color.IsTrueColor) return CadColor.FromRgb(color.R, color.G, color.B);
        return CadColor.FromAci(color.Index);
    }

    private static CadUnits MapUnits(string value) => value.ToLowerInvariant() switch
    {
        var x when x.Contains("millimeter") => CadUnits.Millimetres,
        var x when x.Contains("centimeter") => CadUnits.Centimetres,
        var x when x.Contains("meter") => CadUnits.Metres,
        var x when x.Contains("inch") => CadUnits.Inches,
        var x when x.Contains("foot") || x.Contains("feet") => CadUnits.Feet,
        _ => CadUnits.Unitless
    };

    private static string NormalizeText(string? value) => CadTextNormalizer.Normalize(value);
    private static double Positive(double value) => double.IsFinite(value) && value > double.Epsilon ? value : 1;
    // ACadSharp converts every DXF/DWG IsAngle field to radians at the reader boundary.
    // Keep this adapter helper as an identity function so legacy call sites cannot apply a second deg->rad conversion.
    private static double Degrees(double value) => value;
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
