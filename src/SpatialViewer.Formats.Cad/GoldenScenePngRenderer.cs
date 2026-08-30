using System.IO.Compression;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>Deterministic software PNG snapshot writer for fixture-level visual regression inputs.</summary>
public static class GoldenScenePngRenderer
{
    public static void Render(Scene2D scene, string path, int width = 800, int height = 600)
    {
        ArgumentNullException.ThrowIfNull(scene); ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var pixels = new byte[width * height * 3]; Array.Fill(pixels, (byte)250);
        var camera = new Camera2D(scene.GetBounds().Center); camera.Fit(scene.GetBounds(), new(width, height));
        foreach (var item in scene.GetItems()) DrawItem(pixels, width, height, item, camera);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "."); WritePng(path, pixels, width, height);
    }
    private static void DrawItem(byte[] pixels, int width, int height, SceneItem item, Camera2D camera)
    {
        var color = Color(item.Style.Stroke); Point2D Map(Point2D point) => camera.WorldToScreen(item.Transform.Apply(point), new(width, height));
        void Segment(Point2D a, Point2D b) => Line(pixels, width, height, Map(a), Map(b), color);
        switch (item.Geometry)
        {
            case LineGeometry line: Segment(line.Start, line.End); break;
            case PolylineGeometry polyline: Segments(polyline.Points, polyline.IsClosed, Segment); break;
            case PolygonGeometry polygon: Segments(polygon.Points, true, Segment); break;
            case PathGeometry path: Segments(path.Points, path.IsClosed, Segment); break;
            case CircleGeometry circle: Ellipse(circle.Center, circle.Radius, circle.Radius, Map, pixels, width, height, color); break;
            case EllipseGeometry ellipse: Ellipse(ellipse.Center, ellipse.RadiusX, ellipse.RadiusY, Map, pixels, width, height, color); break;
            case ArcGeometry arc: Arc(arc, Map, pixels, width, height, color); break;
            default: var b = item.Geometry.GetBounds(); Segment(new(b.MinX, b.MinY), new(b.MaxX, b.MaxY)); Segment(new(b.MaxX, b.MinY), new(b.MinX, b.MaxY)); break;
        }
    }
    private static void Segments(IReadOnlyList<Point2D> points, bool closed, Action<Point2D, Point2D> draw) { for (var i = 1; i < points.Count; i++) draw(points[i - 1], points[i]); if (closed && points.Count > 2) draw(points[^1], points[0]); }
    private static void Ellipse(Point2D center, double rx, double ry, Func<Point2D, Point2D> map, byte[] pixels, int width, int height, byte[] color) { const int steps = 96; var previous = map(new(center.X + rx, center.Y)); for (var i = 1; i <= steps; i++) { var angle = Math.PI * 2 * i / steps; var current = map(new(center.X + Math.Cos(angle) * rx, center.Y + Math.Sin(angle) * ry)); Line(pixels, width, height, previous, current, color); previous = current; } }
    private static void Arc(ArcGeometry arc, Func<Point2D, Point2D> map, byte[] pixels, int width, int height, byte[] color) { var steps = Math.Max(2, (int)(Math.Abs(arc.SweepRadians) * 32)); var previous = map(new(arc.Center.X + Math.Cos(arc.StartRadians) * arc.Radius, arc.Center.Y + Math.Sin(arc.StartRadians) * arc.Radius)); for (var i = 1; i <= steps; i++) { var angle = arc.StartRadians + (arc.SweepRadians * i / steps); var current = map(new(arc.Center.X + Math.Cos(angle) * arc.Radius, arc.Center.Y + Math.Sin(angle) * arc.Radius)); Line(pixels, width, height, previous, current, color); previous = current; } }
    private static void Line(byte[] pixels, int width, int height, Point2D a, Point2D b, byte[] color) { var steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)))); for (var i = 0; i <= steps; i++) { var x = (int)Math.Round(a.X + ((b.X - a.X) * i / steps)); var y = (int)Math.Round(a.Y + ((b.Y - a.Y) * i / steps)); if (x < 0 || y < 0 || x >= width || y >= height) continue; var index = ((y * width) + x) * 3; pixels[index] = color[0]; pixels[index + 1] = color[1]; pixels[index + 2] = color[2]; } }
    private static byte[] Color(string hex) => hex.Length == 7 && uint.TryParse(hex[1..], System.Globalization.NumberStyles.HexNumber, null, out var rgb) ? new[] { (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb } : new byte[] { 32, 32, 32 };
    private static void WritePng(string path, byte[] pixels, int width, int height)
    {
        using var stream = File.Create(path); stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }); Chunk(stream, "IHDR", Header(width, height));
        using var data = new MemoryStream(); using (var zlib = new ZLibStream(data, CompressionLevel.SmallestSize, true)) for (var y = 0; y < height; y++) { zlib.WriteByte(0); zlib.Write(pixels, y * width * 3, width * 3); }
        Chunk(stream, "IDAT", data.ToArray()); Chunk(stream, "IEND", Array.Empty<byte>());
    }
    private static byte[] Header(int width, int height) { var bytes = new byte[13]; WriteUInt(bytes, 0, (uint)width); WriteUInt(bytes, 4, (uint)height); bytes[8] = 8; bytes[9] = 2; return bytes; }
    private static void Chunk(Stream stream, string name, byte[] data) { var type = System.Text.Encoding.ASCII.GetBytes(name); var length = new byte[4]; WriteUInt(length, 0, (uint)data.Length); stream.Write(length); stream.Write(type); stream.Write(data); var crc = Crc(type.Concat(data).ToArray()); var checksum = new byte[4]; WriteUInt(checksum, 0, crc); stream.Write(checksum); }
    private static void WriteUInt(byte[] buffer, int index, uint value) { buffer[index] = (byte)(value >> 24); buffer[index + 1] = (byte)(value >> 16); buffer[index + 2] = (byte)(value >> 8); buffer[index + 3] = (byte)value; }
    private static uint Crc(byte[] bytes) { uint crc = 0xffffffff; foreach (var value in bytes) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0); } return ~crc; }
}
