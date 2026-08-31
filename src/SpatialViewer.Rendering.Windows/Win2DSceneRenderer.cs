using System.Diagnostics;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using SpatialViewer.Core;
using SpatialViewer.Rendering;
using Windows.UI;

namespace SpatialViewer.Rendering.Windows;

/// <summary>Win2D/Direct2D renderer for the Debug Host. It converts to floats only after camera-relative double math.</summary>
public sealed class Win2DSceneRenderer : ISceneRenderer
{
    private readonly CanvasControl _canvas;
    private RenderFrame? _frame;
    private Camera2D? _camera;
    private Size2D _viewport;
    private ObjectId? _selected;
    private bool _disposed;
    /// <summary>Raised on the UI thread after a Win2D frame completes, with elapsed milliseconds.</summary>
    public event Action<double>? FrameRendered;
    /// <summary>Gets or sets the canvas color without recreating the scene or renderer.</summary>
    public string CanvasColor { get; set; } = "#FAFAFA";
    /// <summary>Gets or sets the selected-object highlight color.</summary>
    public string SelectionColor { get; set; } = "#FFC107";

    public Win2DSceneRenderer(CanvasControl canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _canvas.Draw += Draw;
    }
    public void Render(RenderFrame frame, Camera2D camera, Size2D viewport, ObjectId? selectedObject)
    { ObjectDisposedException.ThrowIf(_disposed, this); _frame = frame; _camera = camera; _viewport = viewport; _selected = selectedObject; _canvas.Invalidate(); }
    public void RecreateResources() => _canvas.Invalidate();
    public void Dispose() { if (_disposed) return; _disposed = true; _canvas.Draw -= Draw; }
    private void Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var stopwatch = Stopwatch.StartNew();
        args.DrawingSession.Clear(Parse(CanvasColor, 1));
        if (_frame is { } frame && _camera is { } camera) foreach (var command in frame.Commands) DrawCommand(args.DrawingSession, command, camera, _viewport, command.ObjectId == _selected);
        stopwatch.Stop();
        FrameRendered?.Invoke(stopwatch.Elapsed.TotalMilliseconds);
    }
    private void DrawCommand(CanvasDrawingSession session, RenderCommand command, Camera2D camera, Size2D viewport, bool selected)
    {
        var stroke = Parse(RenderColorPolicy.ResolveStroke(command.Style, command.Metadata, CanvasColor), command.Style.Opacity);
        var fill = command.Style.Fill is null ? (Color?)null : Parse(command.Style.Fill, command.Style.Opacity);
        var width = (float)Math.Max(.5, command.Style.StrokeWidth);
        Point2D Map(Point2D p) => camera.WorldToScreen(command.WorldTransform.Apply(p), viewport);
        Vector2 V(Point2D p) { var q = Map(p); return new((float)q.X, (float)q.Y); }
        var pattern = RenderStrokePattern.ResolvePixels(command.Metadata, ScreenScale(Map));
        switch (command.Geometry)
        {
            case PointGeometry point: session.FillCircle(V(point.Position), Math.Max(2, width), stroke); break;
            case LineGeometry line: DrawScreenPolyline(session, new[] { V(line.Start), V(line.End) }, false, stroke, width, pattern); break;
            case PolylineGeometry polyline: DrawScreenPolyline(session, polyline.Points.Select(V).ToArray(), polyline.IsClosed, stroke, width, pattern); break;
            case PolygonGeometry polygon: DrawPolygon(session, polygon.Points.Select(V).ToArray(), stroke, fill, width, pattern); break;
            case CompoundPathGeometry compound: DrawCompoundPath(session, compound.Loops.Select(loop => loop.Select(V).ToArray()).ToArray(), stroke, fill, width, pattern); break;
            case RectangleGeometry rectangle: DrawPolygon(session, RectanglePoints(rectangle.Rectangle).Select(V).ToArray(), stroke, fill, width, pattern); break;
            case CircleGeometry circle: DrawEllipse(session, new EllipseGeometry(circle.Center, circle.Radius, circle.Radius), Map, stroke, fill, width, pattern); break;
            case EllipseGeometry ellipse: DrawEllipse(session, ellipse, Map, stroke, fill, width, pattern); break;
            case ArcGeometry arc: DrawArc(session, arc, Map, stroke, width, pattern); break;
            case PathGeometry path: DrawScreenPolyline(session, path.Points.Select(V).ToArray(), path.IsClosed, stroke, width, pattern); break;
            case TextGeometry text: DrawText(session, text, Map, stroke); break;
            case ImageGeometry image: DrawPolygon(session, RectanglePoints(image.GetBounds()).Select(V).ToArray(), stroke, null, width, pattern); break;
        }
        if (selected && !command.Bounds.IsEmpty) DrawSelectionRectangle(session, command.Bounds, camera, viewport);
    }
    private void DrawSelectionRectangle(CanvasDrawingSession session, BoundingBox2D bounds, Camera2D camera, Size2D viewport)
    {
        Vector2 Map(Point2D p) { var q = camera.WorldToScreen(p, viewport); return new((float)q.X, (float)q.Y); }
        DrawScreenPolyline(session, RectanglePoints(bounds).Select(Map).ToArray(), true, Parse(SelectionColor, 1), 2, Array.Empty<double>());
    }
    private static void DrawText(CanvasDrawingSession session, TextGeometry text, Func<Point2D, Point2D> map, Color color)
    {
        var placement = TextScreenTransform.Resolve(text, map);
        var origin = new Vector2((float)placement.Origin.X, (float)placement.Origin.Y);
        var previous = session.Transform;
        session.Transform = Matrix3x2.CreateRotation((float)placement.RotationRadians, origin) * previous;
        try
        {
            session.DrawText(text.Text, origin, color, new CanvasTextFormat { FontSize = (float)placement.FontSizePixels });
        }
        finally
        {
            session.Transform = previous;
        }
    }
    private static void DrawEllipse(CanvasDrawingSession session, EllipseGeometry ellipse, Func<Point2D, Point2D> map, Color stroke, Color? fill, float width, IReadOnlyList<double> pattern)
    {
        var points = AdaptiveEllipseTessellator.Tessellate(ellipse, map).Select(point => new Vector2((float)point.X, (float)point.Y)).ToArray();
        if (points.Length < 3) return;
        if (fill is { } color) FillPolygon(session, points, color);
        DrawScreenPolyline(session, points, false, stroke, width, pattern);
    }
    private static void DrawArc(CanvasDrawingSession session, ArcGeometry arc, Func<Point2D, Point2D> map, Color color, float width, IReadOnlyList<double> pattern)
    {
        var points = AdaptiveArcTessellator.Tessellate(arc, map).Select(point => new Vector2((float)point.X, (float)point.Y)).ToArray();
        DrawScreenPolyline(session, points, false, color, width, pattern);
    }
    private static void DrawPolygon(CanvasDrawingSession session, Vector2[] points, Color stroke, Color? fill, float width, IReadOnlyList<double> pattern)
    {
        if (points.Length < 3) return;
        if (fill is { } color) FillPolygon(session, points, color);
        DrawScreenPolyline(session, points, true, stroke, width, pattern);
    }
    private static void DrawCompoundPath(CanvasDrawingSession session, IReadOnlyList<Vector2[]> loops, Color stroke, Color? fill, float width, IReadOnlyList<double> pattern)
    {
        var valid = loops.Where(loop => loop.Length >= 3).ToArray();
        if (valid.Length == 0) return;
        if (fill is { } fillColor)
        {
            using var path = new CanvasPathBuilder(session);
            path.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Alternate);
            foreach (var loop in valid)
            {
                path.BeginFigure(loop[0]);
                for (var index = 1; index < loop.Length; index++) path.AddLine(loop[index]);
                path.EndFigure(CanvasFigureLoop.Closed);
            }
            using var geometry = CanvasGeometry.CreatePath(path);
            session.FillGeometry(geometry, fillColor);
        }
        foreach (var loop in valid) DrawScreenPolyline(session, loop, true, stroke, width, pattern);
    }
    private static void FillPolygon(CanvasDrawingSession session, Vector2[] points, Color fill)
    {
        using var path = new CanvasPathBuilder(session);
        path.BeginFigure(points[0]);
        for (var index = 1; index < points.Length; index++) path.AddLine(points[index]);
        path.EndFigure(CanvasFigureLoop.Closed);
        using var geometry = CanvasGeometry.CreatePath(path);
        session.FillGeometry(geometry, fill);
    }
    private static void DrawScreenPolyline(CanvasDrawingSession session, Vector2[] points, bool closed, Color color, float width, IReadOnlyList<double> pattern)
    {
        if (points.Length < 2) return;
        if (pattern.Count == 0)
        {
            for (var index = 1; index < points.Length; index++) session.DrawLine(points[index - 1], points[index], color, width);
            if (closed && points.Length > 2) session.DrawLine(points[^1], points[0], color, width);
            return;
        }
        var patternIndex = 0;
        var (remaining, drawing) = PatternElement(pattern[0], width);
        var segmentCount = closed ? points.Length : points.Length - 1;
        for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            var a = points[segmentIndex];
            var b = points[(segmentIndex + 1) % points.Length];
            var vector = b - a;
            var length = vector.Length();
            if (length <= float.Epsilon) continue;
            var consumed = 0d;
            while (consumed < length - 1e-6)
            {
                var step = Math.Min(remaining, length - consumed);
                if (drawing && step > 1e-6)
                {
                    var start = a + (vector * (float)(consumed / length));
                    var end = a + (vector * (float)((consumed + step) / length));
                    session.DrawLine(start, end, color, width);
                }
                consumed += step;
                remaining -= step;
                if (remaining <= 1e-6)
                {
                    patternIndex = (patternIndex + 1) % pattern.Count;
                    (remaining, drawing) = PatternElement(pattern[patternIndex], width);
                }
            }
        }
    }
    private static (double Length, bool Drawing) PatternElement(double value, float width) => Math.Abs(value) <= double.Epsilon ? (Math.Max(1, width), true) : (Math.Abs(value), value > 0);
    private static double ScreenScale(Func<Point2D, Point2D> map)
    {
        var origin = map(Point2D.Origin);
        var x = map(new Point2D(1, 0));
        var y = map(new Point2D(0, 1));
        return (origin.DistanceTo(x) + origin.DistanceTo(y)) / 2;
    }
    private static Point2D[] RectanglePoints(BoundingBox2D bounds) => new[] { new Point2D(bounds.MinX, bounds.MinY), new Point2D(bounds.MaxX, bounds.MinY), new Point2D(bounds.MaxX, bounds.MaxY), new Point2D(bounds.MinX, bounds.MaxY) };
    private static Color Parse(string value, double opacity)
    {
        var text = value.TrimStart('#'); if (text.Length == 3) text = string.Concat(text.Select(c => new string(c, 2))); if (text.Length != 6 || !uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return Color.FromArgb((byte)(255 * opacity), 32, 32, 32);
        return Color.FromArgb((byte)Math.Clamp(255 * opacity, 0, 255), (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }
}
