using System.Text.Json;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class CadProxyGeometryDiffTests
{
    private static readonly CadCustomClassDefinition CustomClass = new(
        "PRIVATE_PROXY",
        "PrivateProxyObject",
        "PrivateApplication",
        1301,
        1,
        true,
        "None",
        true);

    [Fact]
    public void ComparableGeometryReportsOnlyAnonymousChangedSlots()
    {
        const string privateBeforeText = "PRIVATE_LABEL_ALPHA";
        const string privateAfterText = "PRIVATE_LABEL_BETA";
        var before = Entity(
            "100",
            new CadProxyPolyline(new[] { new Point2D(123456.125, 654321.875), new Point2D(20, 30) }),
            new CadProxyText(new Point2D(5, 6), privateBeforeText, 2, 0, 1, 0, "Text2"));
        var after = Entity(
            "101",
            new CadProxyPolyline(new[] { new Point2D(123457.125, 654321.875), new Point2D(20, 30) }),
            new CadProxyText(new Point2D(5, 6), privateAfterText, 2, 0, 1, 0, "Text2"));

        var report = CadProxyGeometryDiffer.Compare(before, after);
        var json = JsonSerializer.Serialize(report);

        Assert.Equal(CadProxyGeometryDiffStatus.Comparable, report.Status);
        Assert.Equal(report.BeforeLayoutFingerprint, report.AfterLayoutFingerprint);
        Assert.Contains(report.ValueChanges, item => item.PrimitivePath == "0"
            && item.Field == CadProxyGeometryField.PointX
            && item.ElementIndex == 0);
        Assert.Contains(report.ValueChanges, item => item.PrimitivePath == "1"
            && item.Field == CadProxyGeometryField.TextContent);
        Assert.DoesNotContain(privateBeforeText, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateAfterText, json, StringComparison.Ordinal);
        Assert.DoesNotContain("123456.125", json, StringComparison.Ordinal);
        Assert.DoesNotContain("123457.125", json, StringComparison.Ordinal);
    }

    [Fact]
    public void VertexCountChangeFailsClosedAsLayoutMismatch()
    {
        var before = Entity(
            "200",
            new CadProxyPolyline(new[] { new Point2D(0, 0), new Point2D(10, 0) }));
        var after = Entity(
            "201",
            new CadProxyPolyline(new[] { new Point2D(0, 0), new Point2D(5, 0), new Point2D(10, 0) }));

        var report = CadProxyGeometryDiffer.Compare(before, after);

        Assert.Equal(CadProxyGeometryDiffStatus.LayoutMismatch, report.Status);
        Assert.NotEqual(report.BeforeLayoutFingerprint, report.AfterLayoutFingerprint);
        Assert.Empty(report.ValueChanges);
    }

    [Fact]
    public void MissingProxyGraphicsCannotProduceGeometryEvidence()
    {
        var before = Entity("300");
        var after = Entity("301", new CadProxyCircle(new Point2D(0, 0), 10));

        var report = CadProxyGeometryDiffer.Compare(before, after);

        Assert.Equal(CadProxyGeometryDiffStatus.MissingProxyGraphics, report.Status);
        Assert.Empty(report.ValueChanges);
    }

    [Fact]
    public void ConsensusRetainsOnlyRepeatableGeometrySlots()
    {
        var first = CadProxyGeometryExperimentAnalyzer.Observe(
            Entity("400", new CadProxyPolyline(new[] { new Point2D(0, 0), new Point2D(10, 0) })),
            Entity("401", new CadProxyPolyline(new[] { new Point2D(1, 0), new Point2D(10, 1) })));
        var second = CadProxyGeometryExperimentAnalyzer.Observe(
            Entity("500", new CadProxyPolyline(new[] { new Point2D(20, 20), new Point2D(30, 20) })),
            Entity("501", new CadProxyPolyline(new[] { new Point2D(21, 20), new Point2D(30, 20) })));

        var consensus = CadProxyGeometryExperimentAnalyzer.BuildConsensus(new[] { first, second });

        Assert.True(consensus.HasStableCandidate);
        var stable = Assert.Single(consensus.StableValueChanges);
        Assert.Equal("0", stable.PrimitivePath);
        Assert.Equal(CadProxyGeometryField.PointX, stable.Field);
        Assert.Equal(0, stable.ElementIndex);
    }

    [Fact]
    public void NestedClipGroupUsesDeterministicPrimitivePaths()
    {
        var clipBefore = new CadProxyClipGroup(
            new[] { new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10) },
            new CadProxyPrimitive[]
            {
                new CadProxyCircle(new Point2D(5, 5), 2)
            });
        var clipAfter = clipBefore with
        {
            Children = new CadProxyPrimitive[]
            {
                new CadProxyCircle(new Point2D(6, 5), 2)
            }
        };

        var report = CadProxyGeometryDiffer.Compare(Entity("600", clipBefore), Entity("601", clipAfter));

        Assert.Equal(CadProxyGeometryDiffStatus.Comparable, report.Status);
        var change = Assert.Single(report.ValueChanges);
        Assert.Equal("0/0", change.PrimitivePath);
        Assert.Equal(CadProxyGeometryField.CenterX, change.Field);
    }

    [Fact]
    public void DifferentCustomObjectIdentityIsRejectedBeforeGeometryComparison()
    {
        var otherClass = CustomClass with { DxfName = "OTHER_PROXY" };
        var before = Entity("700", new CadProxyCircle(new Point2D(0, 0), 1));
        var after = new CadCustomEntity("701", otherClass.DxfName)
        {
            ClassDefinition = otherClass,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = new CadProxyPrimitive[] { new CadProxyCircle(new Point2D(1, 0), 1) }
        };

        Assert.Throws<ArgumentException>(() => CadProxyGeometryDiffer.Compare(before, after));
    }

    private static CadCustomEntity Entity(string handle, params CadProxyPrimitive[] primitives)
        => new(handle, CustomClass.DxfName)
        {
            ClassDefinition = CustomClass,
            Representation = primitives.Length == 0
                ? CadCustomEntityRepresentation.Opaque
                : CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = primitives,
            ProxyGraphicKinds = primitives.Select(item => item.SourceKind).ToArray()
        };
}
