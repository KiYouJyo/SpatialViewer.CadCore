using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyTransformV0121Tests
{
    [Fact]
    public void BalancedNestedPlanarTransformsApplyInObjectArxStackOrder()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxyPushModelTransform
            {
                TransformationMatrix = CSMath.Matrix4.CreateTranslation(new CSMath.XYZ(100, 200, 0))
            },
            new ProxyPushModelTransform2
            {
                TransformationMatrix = CSMath.Matrix4.CreateRotationMatrix(0, 0, Math.PI / 2)
            },
            new ProxyPolyline
            {
                Points = new()
                {
                    new CSMath.XYZ(1, 0, 0),
                    new CSMath.XYZ(2, 0, 0)
                }
            },
            new ProxyUnicodeText
            {
                Normal = CSMath.XYZ.AxisZ,
                StartPoint = new CSMath.XYZ(1, 0, 0),
                Text = "轴1",
                Height = 2,
                TextDirection = new CSMath.XYZ(1, 0, 0),
                WidthFactor = 1
            },
            new ProxyPopModelTransform(),
            new ProxyPopModelTransform()
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(graphics, out var unsupported, out var stateful);

        Assert.True(stateful);
        Assert.Equal(0, unsupported);
        Assert.Equal(2, mapped.Count);

        var polyline = Assert.IsType<CadProxyPolyline>(mapped[0]);
        Assert.Equal(100, polyline.Points[0].X, 12);
        Assert.Equal(201, polyline.Points[0].Y, 12);
        Assert.Equal(100, polyline.Points[1].X, 12);
        Assert.Equal(202, polyline.Points[1].Y, 12);

        var text = Assert.IsType<CadProxyText>(mapped[1]);
        Assert.Equal(100, text.Origin.X, 12);
        Assert.Equal(201, text.Origin.Y, 12);
        Assert.Equal(Math.PI / 2, text.RotationRadians, 12);
        Assert.Equal(2, text.Height, 12);
    }

    [Fact]
    public void UniformScaleTransformsCircleArcAndTextWithoutFlatteningSemantics()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxyPushModelTransform
            {
                TransformationMatrix = CSMath.Matrix4.CreateScalingMatrix(2, 2, 1)
            },
            new ProxyCircle
            {
                Center = new CSMath.XYZ(5, 6, 0),
                Radius = 3,
                Normal = CSMath.XYZ.AxisZ
            },
            new ProxyCircularArc
            {
                Center = new CSMath.XYZ(10, 0, 0),
                Radius = 4,
                Normal = CSMath.XYZ.AxisZ,
                StartVectorDirection = new CSMath.XYZ(1, 0, 0),
                SweepAngle = Math.PI / 2
            },
            new ProxyText
            {
                Normal = CSMath.XYZ.AxisZ,
                StartPoint = new CSMath.XYZ(1, 2, 0),
                Text = "A",
                Height = 2.5,
                TextDirection = new CSMath.XYZ(1, 0, 0),
                WidthFactor = 0.8
            },
            new ProxyPopModelTransform()
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(graphics, out var unsupported, out var stateful);

        Assert.True(stateful);
        Assert.Equal(0, unsupported);
        var circle = Assert.IsType<CadProxyCircle>(mapped[0]);
        Assert.Equal(10, circle.Center.X, 12);
        Assert.Equal(12, circle.Center.Y, 12);
        Assert.Equal(6, circle.Radius, 12);
        var arc = Assert.IsType<CadProxyArc>(mapped[1]);
        Assert.Equal(8, arc.Radius, 12);
        Assert.Equal(Math.PI / 2, arc.SweepRadians, 12);
        var text = Assert.IsType<CadProxyText>(mapped[2]);
        Assert.Equal(5, text.Height, 12);
        Assert.Equal(0.8, text.WidthFactor, 12);
    }

    [Fact]
    public void NonUniformOrUnbalancedModelTransformsFailClosedForWholeStream()
    {
        var nonUniform = new IProxyGeometry[]
        {
            new ProxyPushModelTransform
            {
                TransformationMatrix = CSMath.Matrix4.CreateScalingMatrix(2, 1, 1)
            },
            new ProxyPolyline
            {
                Points = new() { new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(10, 0, 0) }
            },
            new ProxyPopModelTransform()
        };
        var unbalanced = new IProxyGeometry[]
        {
            new ProxyPushModelTransform
            {
                TransformationMatrix = CSMath.Matrix4.CreateTranslation(new CSMath.XYZ(1, 2, 0))
            },
            new ProxyPolyline
            {
                Points = new() { new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(10, 0, 0) }
            }
        };

        var nonUniformMapped = ACadSharpProxyGraphicsMapping.Map(nonUniform, out var nonUniformUnsupported, out var nonUniformStateful);
        var unbalancedMapped = ACadSharpProxyGraphicsMapping.Map(unbalanced, out var unbalancedUnsupported, out var unbalancedStateful);

        Assert.True(nonUniformStateful);
        Assert.Empty(nonUniformMapped);
        Assert.Equal(nonUniform.Length, nonUniformUnsupported);
        Assert.True(unbalancedStateful);
        Assert.Empty(unbalancedMapped);
        Assert.Equal(unbalanced.Length, unbalancedUnsupported);
    }

    [Fact]
    public void ClipCommandsRemainFailClosedEvenWhenOtherGeometryIsSafe()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxyPushClip(),
            new ProxyPolyline
            {
                Points = new() { new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(10, 0, 0) }
            },
            new ProxyPopClip()
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(graphics, out var unsupported, out var stateful);

        Assert.True(stateful);
        Assert.Empty(mapped);
        Assert.Equal(graphics.Length, unsupported);
    }
}
