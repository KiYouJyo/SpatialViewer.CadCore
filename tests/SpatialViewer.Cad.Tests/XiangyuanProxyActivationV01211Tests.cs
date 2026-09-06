using ACadSharp.Entities.ProxyGraphics;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanProxyActivationV01211Tests
{
    [Fact]
    public void CadCoreDwgReaderEnablesProxyGraphics()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SpatialViewer-proxy-reader-{Guid.NewGuid():N}.dwg");
        File.WriteAllBytes(path, Array.Empty<byte>());
        try
        {
            var readerType = typeof(ACadSharpCadImporter).Assembly.GetType(
                "SpatialViewer.Formats.Cad.ACadSharp.CadCoreDwgReader",
                throwOnError: true)!;
            using var reader = Assert.IsAssignableFrom<DwgReader>(Activator.CreateInstance(readerType, path));

            Assert.False(reader.Configuration.IgnoreProxyGraphics);
            Assert.True(reader.Configuration.KeepUnknownEntities);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void XiangyuanObservedTraitSequenceReachesLightweightPolyline()
    {
        IProxyGeometry[] source =
        [
            new ProxySubentColor { ColorIndex = 3 },
            new ProxySubentMarker { MarkerIndex = 17 },
            new ProxySubentFillon { IsOn = true },
            new ProxyLwPolyine { Entity = Rectangle() }
        ];

        var mapped = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out var stateful);

        Assert.False(stateful);
        Assert.Equal(0, unsupported);
        var proxy = Assert.IsType<CadProxyLwPolyline>(Assert.Single(mapped));
        Assert.Equal(CadColor.FromAci(3), proxy.Traits.Color);
        Assert.Equal(17, proxy.Traits.MarkerId);
        Assert.True(proxy.Traits.FillOn is true);
        Assert.True(proxy.IsClosed);

        var custom = new CadCustomEntity("LZX-LAND", "LZX_LAND", Color: CadColor.FromAci(7))
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = mapped
        };
        var document = new CadDocument(
            "xiangyuan-proxy-activation.dwg",
            "DWG",
            "AC1032",
            CadUnits.Metres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });

        var item = Assert.Single(document.Scene.GetItems(), item => item.Id == custom.ObjectId);

        Assert.IsType<PolygonGeometry>(item.Geometry);
        Assert.Equal("#00FF00", item.Style.Stroke);
        // ObjectARX explicitly defines polyline primitives as non-fillable even when closed.
        Assert.Null(item.Style.Fill);
        Assert.Equal(bool.TrueString, item.Metadata["ProxyFillOn"]);
        Assert.Equal("17", item.Metadata["ProxyMarkerId"]);
    }

    private static ACadSharp.Entities.LwPolyline Rectangle()
        => new(
            new ACadSharp.Entities.LwPolyline.Vertex(0, 0),
            new ACadSharp.Entities.LwPolyline.Vertex(10, 0),
            new ACadSharp.Entities.LwPolyline.Vertex(10, 10),
            new ACadSharp.Entities.LwPolyline.Vertex(0, 10))
        {
            Normal = CSMath.XYZ.AxisZ,
            IsClosed = true
        };
}
