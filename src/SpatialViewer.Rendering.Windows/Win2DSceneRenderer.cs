using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using SpatialViewer.Core;
using SpatialViewer.Rendering;
using Windows.UI;
using System.Diagnostics;

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
        var stroke = Parse(command.Style.Stroke, command.Style.Opacity); var fill = command.Style.Fill is null ? (Color?)null : Parse(command.Style.Fill, command.Style.Opacity); var width = (float)Math.Max(.5, command.Style.StrokeWidth);
        Point2D Map(Point2D p) => camera.WorldToScreen(command.WorldTransform.Apply(p), viewport);
        System.Numerics.Vector2 V(Point2D p) { var q = Map(p); return new((float)q.X, (float)q.Y); }
        switch (command.Geometry)
        {
            case PointGeometry point: session.FillCircle(V(point.Position), Math.Max(2, width), stroke); break;
            case LineGeometry line: session.DrawLine(V(line.Start), V(line.End), stroke, width); break;
            case PolylineGeometry polyline: DrawPolyline(session, polyline.Points, polyline.IsClosed, V, stroke, width); break;
            case PolygonGeometry polygon: DrawPolygon(session, polygon.Points, V, stroke, fill, width); break;
            case RectangleGeometry rectangle: DrawRectangle(session, rectangle.Rectangle, V, stroke, fill, width); break;
            case CircleGeometry circle: DrawEllipse(session, circle.Center, circle.Radius, circle.Radius, Map, stroke, fill, width); break;
            case EllipseGeometry ellipse: DrawEllipse(session, ellipse.Center, ellipse.RadiusX, ellipse.RadiusY, Map, stroke, fill, width); break;
            case ArcGeometry arc: DrawArc(session, arc, V, stroke, width); break;
            case PathGeometry path: DrawPolyline(session, path.Points, path.IsClosed, V, stroke, width); break;
            case TextGeometry text: { var p = V(text.Origin); session.DrawText(text.Text, p, stroke, new CanvasTextFormat { FontSize = (float)Math.Max(8, text.Height * camera.Zoom) }); break; }
            case ImageGeometry image: DrawRectangle(session, image.GetBounds(), V, stroke, null, width); break;
        }
        if (selected && !command.Bounds.IsEmpty) DrawRectangle(session, command.Bounds, p => { var q = camera.WorldToScreen(p, viewport); return new System.Numerics.Vector2((float)q.X, (float)q.Y); }, Parse(SelectionColor, 1), null, 2);
    }
    private static void DrawPolyline(CanvasDrawingSession s, IReadOnlyList<Point2D> points, bool closed, Func<Point2D, System.Numerics.Vector2> map, Color color, float width) { for (var i = 1; i < points.Count; i++) s.DrawLine(map(points[i - 1]), map(points[i]), color, width); if (closed && points.Count > 2) s.DrawLine(map(points[^1]), map(points[0]), color, width); }
    private static void DrawPolygon(CanvasDrawingSession s, IReadOnlyList<Point2D> points, Func<Point2D, System.Numerics.Vector2> map, Color stroke, Color? fill, float width) { if (points.Count < 3) return; using var path = new CanvasPathBuilder(s); path.BeginFigure(map(points[0])); for (var i = 1; i < points.Count; i++) path.AddLine(map(points[i])); path.EndFigure(CanvasFigureLoop.Closed); using var geometry = CanvasGeometry.CreatePath(path); if (fill is { } f) s.FillGeometry(geometry, f); s.DrawGeometry(geometry, stroke, width); }
    private static void DrawRectangle(CanvasDrawingSession s, BoundingBox2D bounds, Func<Point2D, System.Numerics.Vector2> map, Color stroke, Color? fill, float width) { var a = map(new(bounds.MinX, bounds.MinY)); var b = map(new(bounds.MaxX, bounds.MaxY)); var rect = new global::Windows.Foundation.Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)); if (fill is { } f) s.FillRectangle(rect, f); s.DrawRectangle(rect, stroke, width); }
    private static void DrawEllipse(CanvasDrawingSession s, Point2D center, double rx, double ry, Func<Point2D, Point2D> map, Color stroke, Color? fill, float width) { var c = map(center); var x = map(new(center.X + rx, center.Y)); var y = map(new(center.X, center.Y + ry)); var centerVector = new System.Numerics.Vector2((float)c.X, (float)c.Y); var radiusX = (float)Math.Abs(x.X - c.X); var radiusY = (float)Math.Abs(y.Y - c.Y); if (fill is { } f) s.FillEllipse(centerVector, radiusX, radiusY, f); s.DrawEllipse(centerVector, radiusX, radiusY, stroke, width); }
    private static void DrawArc(CanvasDrawingSession s, ArcGeometry arc, Func<Point2D, System.Numerics.Vector2> map, Color color, float width) { using var path = new CanvasPathBuilder(s); var start = arc.StartRadians; var end = start + arc.SweepRadians; var startPoint = new Point2D(arc.Center.X + Math.Cos(start) * arc.Radius, arc.Center.Y + Math.Sin(start) * arc.Radius); path.BeginFigure(map(startPoint)); var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(arc.SweepRadians) / (Math.PI / 18))); for (var i = 1; i <= steps; i++) { var angle = start + ((end - start) * i / steps); path.AddLine(map(new Point2D(arc.Center.X + Math.Cos(angle) * arc.Radius, arc.Center.Y + Math.Sin(angle) * arc.Radius))); } path.EndFigure(CanvasFigureLoop.Open); using var geometry = CanvasGeometry.CreatePath(path); s.DrawGeometry(geometry, color, width); }
    private static Color Parse(string value, double opacity)
    {
        var text = value.TrimStart('#'); if (text.Length == 3) text = string.Concat(text.Select(c => new string(c, 2))); if (text.Length != 6 || !uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return Color.FromArgb((byte)(255 * opacity), 32, 32, 32);
        return Color.FromArgb((byte)Math.Clamp(255 * opacity, 0, 255), (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }
}
