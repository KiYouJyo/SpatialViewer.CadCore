using System.Diagnostics;
using SpatialViewer.Core;

var count = args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed > 0 ? parsed : 100_000;
var columns = Math.Max(1, (int)Math.Sqrt(count));
var layer = new Layer("benchmark", "Benchmark");
var nodes = Enumerable.Range(0, count)
    .Select(index =>
    {
        var x = index % columns;
        var y = index / columns;
        return new SceneNode(ObjectId.New(), new LineGeometry(new Point2D(x, y), new Point2D(x + .75, y + .25)));
    })
    .ToArray();

var sceneBuild = Stopwatch.StartNew();
var scene = new Scene2D(new[] { new SceneLayer(layer, nodes) });
sceneBuild.Stop();

_ = scene.GetItems().Count();
_ = scene.GetBounds();
_ = HitTesting.HitTest(scene, new Point2D(columns / 2d, columns / 2d), .5);

var enumeration = Measure(() => _ = scene.GetItems().Count(), 50);
var bounds = Measure(() => _ = scene.GetBounds(), 50);
var hitPoint = new Point2D(columns / 2d, columns / 2d);
var hitTest = Measure(() => _ = HitTesting.HitTest(scene, hitPoint, .5), 20);

Console.WriteLine("SpatialViewer.CadCore core scene baseline");
Console.WriteLine($"Items: {count:N0}");
Console.WriteLine($"Scene construction: {sceneBuild.Elapsed.TotalMilliseconds:F3} ms");
Console.WriteLine($"Enumerate visible items: {enumeration:F3} ms/op");
Console.WriteLine($"Bounds query: {bounds:F3} ms/op");
Console.WriteLine($"Hit test: {hitTest:F3} ms/op");

static double Measure(Action action, int iterations)
{
    var stopwatch = Stopwatch.StartNew();
    for (var index = 0; index < iterations; index++) action();
    stopwatch.Stop();
    return stopwatch.Elapsed.TotalMilliseconds / iterations;
}
