using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyClipRotationV0121Tests
{
    [Fact]
    public void RotatedModelTransformKeepsPolygonClipRotatedWithMatchingInverse()
    {
        var model = CSMath.Matrix4.CreateRotationMatrix(0, 0, Math.PI / 4);
        var inverse = CSMath.Matrix4.CreateRotationMatrix(0, 0, -Math.PI / 4);
        var clip = new ProxyPushClip
        {
            Extrusion = CSMath.XYZ.AxisZ,
            ClipBoundaryOrigin = new CSMath.XYZ(0, 0, 0),
            ClipBoundary = new() { new CSMath.XY(0, 0), new CSMath.XY(10, 10) },
            PointCount = 2,
            ClipBoundaryTransformMatrix = CSMath.Matrix4.Identity,
            InverseBlockTransformMatrix = inverse,
            FrontClip = 0,
            BackClip = 0
        };
        var graphics = new IProxyGeometry[]
        {
            new ProxyPushModelTransform { TransformationMatrix = model },
            clip,
            new ProxyPolyline
            {
                Points = new()
                {
                    new CSMath.XYZ(-5, 5, 0),
                    new CSMath.XYZ(15, 5, 0)
                }
            },
            new ProxyPopModelTransform(),
            new ProxyPopClip()
        };

        var mapped = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var unsupported, out _);
        Assert.Equal(0, unsupported);
        var group = Assert.IsType<CadProxyClipGroup>(Assert.Single(mapped));
        var rootTwo = group.ClipPolygon[1];
        Assert.Equal(10 / Math.Sqrt(2), rootTwo.X, 12);
        Assert.Equal(10 / Math.Sqrt(2), rootTwo.Y, 12);

        var custom = new CadCustomEntity("PX-CLIP-ROT", "TCH_PROXY_CLIP")
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = mapped
        };
        var document = new CadDocument(
            "proxy-clip-rot.dwg",
            "DWG",
            "AC1032",
            CadUnits.Unitless,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });
        var scene = document.Scene;
        var item = Assert.Single(scene.GetItems());
        Assert.Single(item.ClipPolygons);

        var inside = new Point2D(0, 10 / Math.Sqrt(2));
        var outside = new Point2D(7 / Math.Sqrt(2), 17 / Math.Sqrt(2));
        Assert.Equal(custom.ObjectId, HitTesting.HitTest(scene, inside, 1e-9)?.Id);
        Assert.Null(HitTesting.HitTest(scene, outside, 1e-9));
    }
}
