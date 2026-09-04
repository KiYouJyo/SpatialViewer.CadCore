using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyMeshShellV0126Tests
{
    private static readonly int[] SevenAciColors = [1, 2, 3, 4, 5, 6, 7];
    private static readonly int[] SevenMarkers = [30, 31, 32, 33, 34, 35, 36];
    private static readonly int[] SevenVisibilities = [1, 0, 2, 1, 1, 1, 1];
    private static readonly int[] FourAciColors = [1, 2, 3, 4];
    private static readonly int[] FourVisibilities = [1, 0, 2, 1];
    private static readonly int[] QuadFace = [0, 1, 2, 3];
    private static readonly int[] TriangleFace = [0, 1, 2];

    [Fact]
    public void MeshPreservesDocumentedRowThenColumnEdgeOrderAndEvidence()
    {
        var traits = new EdgeTraits();
        traits.Colors.AddRange(SevenAciColors);
        traits.LayerHandles.AddRange(new ulong[] { 10, 11, 12, 13, 14, 15, 16 });
        traits.LineTypeHandles.AddRange(new ulong[] { 20, 21, 22, 23, 24, 25, 26 });
        traits.MakerIds.AddRange(SevenMarkers);
        traits.VisibilityIndicators.AddRange(SevenVisibilities);

        var mesh = new ProxyMesh
        {
            RowCount = 2,
            ColumnCount = 3,
            EdgeTraits = traits,
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
        IProxyGeometry[] source = [mesh];

        var mapped = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out _);

        Assert.Equal(0, unsupported);
        var edges = Assert.IsType<CadProxyEdgeSet>(Assert.Single(mapped));
        Assert.Equal("MeshEdges", edges.ProxyEdgeKind);
        Assert.Equal(7, edges.Edges.Count);

        Assert.Equal(new Point2D(0, 0), edges.Edges[0].Start);
        Assert.Equal(new Point2D(1, 0), edges.Edges[0].End);
        Assert.Equal(new Point2D(1, 0), edges.Edges[1].Start);
        Assert.Equal(new Point2D(2, 0), edges.Edges[1].End);
        Assert.Equal(new Point2D(0, 1), edges.Edges[2].Start);
        Assert.Equal(new Point2D(1, 1), edges.Edges[2].End);
        Assert.Equal(new Point2D(1, 1), edges.Edges[3].Start);
        Assert.Equal(new Point2D(2, 1), edges.Edges[3].End);
        Assert.Equal(new Point2D(0, 0), edges.Edges[4].Start);
        Assert.Equal(new Point2D(0, 1), edges.Edges[4].End);

        Assert.Equal(CadColor.FromAci(1), edges.Edges[0].Evidence.Color);
        Assert.Equal(10UL, edges.Edges[0].Evidence.LayerReference);
        Assert.Equal(20UL, edges.Edges[0].Evidence.LineTypeReference);
        Assert.Equal(30, edges.Edges[0].Evidence.MarkerId);
        Assert.Equal(1, edges.Edges[0].Evidence.Visibility);
        Assert.Equal(0, edges.Edges[1].Evidence.Visibility);
        Assert.Equal(2, edges.Edges[2].Evidence.Visibility);
    }

    [Fact]
    public void MeshSceneOmitsInvisibleEdgesButKeepsSilhouetteAndAciColors()
    {
        var traits = new EdgeTraits();
        traits.Colors.AddRange(FourAciColors);
        traits.VisibilityIndicators.AddRange(FourVisibilities);

        var mesh = new ProxyMesh
        {
            RowCount = 2,
            ColumnCount = 2,
            EdgeTraits = traits,
            Vertices = new()
            {
                new CSMath.XYZ(0, 0, 0),
                new CSMath.XYZ(1, 0, 0),
                new CSMath.XYZ(0, 1, 0),
                new CSMath.XYZ(1, 1, 0)
            }
        };
        IProxyGeometry[] source = [mesh];
        var primitives = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out _);
        Assert.Equal(0, unsupported);

        var custom = new CadCustomEntity("MESH", "PROXY_MESH", Color: CadColor.FromAci(7))
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = primitives
        };
        var document = new CadDocument(
            "mesh.dwg",
            "DWG",
            "AC1032",
            CadUnits.Unitless,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });

        var lines = document.Scene.GetItems()
            .Where(item => item.Id == custom.ObjectId && item.Geometry is LineGeometry)
            .ToArray();

        Assert.Equal(3, lines.Length);
        Assert.DoesNotContain(lines, item => item.Metadata.TryGetValue("ProxyEdgeIndex", out var index) && index == "1");
        Assert.Contains(lines, item => item.Metadata.TryGetValue("ProxyEdgeIndex", out var index) && index == "2");
        Assert.Contains(lines, item => item.Style.Stroke == "#FF0000");
        Assert.Contains(lines, item => item.Style.Stroke == "#00FF00");
    }

    [Fact]
    public void ShellUsesFaceTraversalForEdgeEvidence()
    {
        var traits = new EdgeTraits();
        traits.Colors.AddRange(FourAciColors);

        var shell = new ProxyShell
        {
            EdgeTraits = traits,
            Vertices = new()
            {
                new CSMath.XYZ(0, 0, 5),
                new CSMath.XYZ(2, 0, 5),
                new CSMath.XYZ(2, 1, 5),
                new CSMath.XYZ(0, 1, 5)
            },
            Faces = new() { QuadFace }
        };
        IProxyGeometry[] source = [shell];

        var mapped = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out _);

        Assert.Equal(0, unsupported);
        var edges = Assert.IsType<CadProxyEdgeSet>(Assert.Single(mapped));
        Assert.Equal("ShellEdges", edges.ProxyEdgeKind);
        Assert.Equal(4, edges.Edges.Count);
        Assert.Equal(new Point2D(0, 0), edges.Edges[0].Start);
        Assert.Equal(new Point2D(2, 0), edges.Edges[0].End);
        Assert.Equal(new Point2D(0, 1), edges.Edges[3].Start);
        Assert.Equal(new Point2D(0, 0), edges.Edges[3].End);
        Assert.Equal(CadColor.FromAci(4), edges.Edges[3].Evidence.Color);
    }

    [Fact]
    public void NonPlanarOrMalformedMeshShellFailClosed()
    {
        var nonPlanar = new ProxyMesh
        {
            RowCount = 2,
            ColumnCount = 2,
            Vertices = new()
            {
                new CSMath.XYZ(0, 0, 0),
                new CSMath.XYZ(1, 0, 0),
                new CSMath.XYZ(0, 1, 0),
                new CSMath.XYZ(1, 1, 1)
            }
        };
        IProxyGeometry[] meshSource = [nonPlanar];
        var mappedMesh = ACadSharpProxyGraphicsMapping.Map(meshSource, out var meshUnsupported, out _);
        Assert.Empty(mappedMesh);
        Assert.Equal(1, meshUnsupported);

        var badTraits = new EdgeTraits();
        badTraits.Colors.Add(1);
        var malformedShell = new ProxyShell
        {
            EdgeTraits = badTraits,
            Vertices = new()
            {
                new CSMath.XYZ(0, 0, 0),
                new CSMath.XYZ(1, 0, 0),
                new CSMath.XYZ(0, 1, 0)
            },
            Faces = new() { TriangleFace }
        };
        IProxyGeometry[] shellSource = [malformedShell];
        var mappedShell = ACadSharpProxyGraphicsMapping.Map(shellSource, out var shellUnsupported, out _);
        Assert.Empty(mappedShell);
        Assert.Equal(1, shellUnsupported);
    }
}
