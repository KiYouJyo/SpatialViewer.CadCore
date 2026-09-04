using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxySurfaceFillV0129Tests
{
    private static readonly int[] QuadFace = [0, 1, 2, 3];

    [Fact]
    public void ExplicitFillOnMapsPlanarShellFacesAndPreservesFaceColor()
    {
        var faceTraits = new FaceTraits();
        faceTraits.Colors.Add(1);

        var shell = new ProxyShell
        {
            FaceTraits = faceTraits,
            Vertices = new()
            {
                new CSMath.XYZ(0, 0, 0),
                new CSMath.XYZ(10, 0, 0),
                new CSMath.XYZ(10, 5, 0),
                new CSMath.XYZ(0, 5, 0)
            },
            Faces = new() { QuadFace }
        };

        IProxyGeometry[] source =
        [
            new ProxySubentColor { ColorIndex = 3 },
            new ProxySubentFillon { IsOn = true },
            shell
        ];

        var mapped = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out _);

        Assert.Equal(0, unsupported);
        var surface = Assert.IsType<CadProxySurfaceSet>(Assert.Single(mapped));
        Assert.Equal("ShellSurface", surface.ProxySurfaceKind);
        Assert.True(surface.Traits.FillOn);
        var face = Assert.Single(surface.Faces);
        Assert.Equal(CadColor.FromAci(1), face.Evidence.Color);
        Assert.Equal(4, face.Points.Count);
        Assert.Equal(4, surface.Edges.Count);

        var custom = new CadCustomEntity("SHELL-FILL", "PROXY_SHELL", Color: CadColor.FromAci(7))
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = mapped
        };
        var items = Document(custom).Scene.GetItems().Where(item => item.Id == custom.ObjectId).ToArray();
        var polygon = Assert.Single(items, item => item.Geometry is PolygonGeometry);

        Assert.Equal("#FF0000", polygon.Style.Fill);
        Assert.Equal(bool.TrueString, polygon.Metadata["ProxySurfaceFilled"]);
        Assert.Equal(bool.TrueString, polygon.Metadata["ProxyFillOn"]);
        Assert.Equal(bool.TrueString, polygon.Metadata["ProxyFaceColorOverride"]);
    }

    [Fact]
    public void ExplicitFillOffKeepsShellAsEdgeOnlyFallback()
    {
        var faceTraits = new FaceTraits();
        faceTraits.Colors.Add(2);
        var shell = Shell(faceTraits);

        IProxyGeometry[] source =
        [
            new ProxySubentFillon { IsOn = false },
            shell
        ];

        var mapped = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out _);

        Assert.Equal(0, unsupported);
        var edges = Assert.IsType<CadProxyEdgeSet>(Assert.Single(mapped));
        Assert.False(edges.Traits.FillOn);
        Assert.Equal("ShellEdges", edges.ProxyEdgeKind);
    }

    [Fact]
    public void ExplicitFillOnMapsPlanarMeshFacesInRowOrder()
    {
        var faceTraits = new FaceTraits();
        faceTraits.Colors.Add(4);
        faceTraits.Colors.Add(5);

        var mesh = new ProxyMesh
        {
            RowCount = 2,
            ColumnCount = 3,
            FaceTraits = faceTraits,
            Vertices = new()
            {
                new CSMath.XYZ(0, 0, 0),
                new CSMath.XYZ(1, 0, 0),
                new CSMath.XYZ(2, 0, 0),
                new CSMath.XYZ(0, 1, 0),
                new CSMath.XYZ(1, 1, 0),
                new CSMath.XYZ(2, 1, 0)
            }
        };

        IProxyGeometry[] source =
        [
            new ProxySubentFillon { IsOn = true },
            mesh
        ];

        var mapped = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out _);

        Assert.Equal(0, unsupported);
        var surface = Assert.IsType<CadProxySurfaceSet>(Assert.Single(mapped));
        Assert.Equal("MeshSurface", surface.ProxySurfaceKind);
        Assert.Equal(2, surface.Faces.Count);
        Assert.Equal(CadColor.FromAci(4), surface.Faces[0].Evidence.Color);
        Assert.Equal(CadColor.FromAci(5), surface.Faces[1].Evidence.Color);
        Assert.Equal(new Point2D(0, 0), surface.Faces[0].Points[0]);
        Assert.Equal(new Point2D(1, 1), surface.Faces[0].Points[2]);
        Assert.Equal(new Point2D(1, 0), surface.Faces[1].Points[0]);
        Assert.Equal(new Point2D(2, 1), surface.Faces[1].Points[2]);
    }

    [Fact]
    public void MalformedFaceTraitsFallBackToEdgesWithoutInventingFill()
    {
        var faceTraits = new FaceTraits();
        faceTraits.Colors.AddRange([1, 2]);
        var shell = Shell(faceTraits);

        IProxyGeometry[] source =
        [
            new ProxySubentFillon { IsOn = true },
            shell
        ];

        var mapped = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out _);

        Assert.Equal(0, unsupported);
        var edges = Assert.IsType<CadProxyEdgeSet>(Assert.Single(mapped));
        Assert.True(edges.Traits.FillOn);
        Assert.Equal("ShellEdges", edges.ProxyEdgeKind);
    }

    [Fact]
    public void FillOffSuppressesProxyPolygonFillButNeverFillsPolyline()
    {
        IProxyGeometry[] source =
        [
            new ProxySubentFillon { IsOn = false },
            new ProxyPolygon
            {
                Points = new()
                {
                    new CSMath.XYZ(0, 0, 0),
                    new CSMath.XYZ(10, 0, 0),
                    new CSMath.XYZ(10, 10, 0)
                }
            },
            new ProxyPolyline
            {
                Points = new()
                {
                    new CSMath.XYZ(20, 0, 0),
                    new CSMath.XYZ(30, 0, 0),
                    new CSMath.XYZ(30, 10, 0),
                    new CSMath.XYZ(20, 0, 0)
                }
            }
        ];

        var mapped = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out _);
        var custom = new CadCustomEntity("FILL-OFF", "PROXY_FILL", Color: CadColor.FromAci(2))
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = mapped
        };
        var items = Document(custom).Scene.GetItems().Where(item => item.Id == custom.ObjectId).ToArray();

        Assert.Equal(0, unsupported);
        Assert.Equal(2, items.Length);
        var polygon = Assert.Single(items, item => item.Geometry is PolygonGeometry);
        var polyline = Assert.Single(items, item => item.Geometry is PolylineGeometry);
        Assert.Null(polygon.Style.Fill);
        Assert.Null(polyline.Style.Fill);
        Assert.Equal(bool.FalseString, polygon.Metadata["ProxyPolygonFilled"]);
    }

    private static ProxyShell Shell(FaceTraits faceTraits)
        => new()
        {
            FaceTraits = faceTraits,
            Vertices = new()
            {
                new CSMath.XYZ(0, 0, 0),
                new CSMath.XYZ(2, 0, 0),
                new CSMath.XYZ(2, 1, 0),
                new CSMath.XYZ(0, 1, 0)
            },
            Faces = new() { QuadFace }
        };

    private static CadDocument Document(CadCustomEntity custom)
        => new(
            "proxy-surface-fill.dwg",
            "DWG",
            "AC1032",
            CadUnits.Unitless,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });
}
