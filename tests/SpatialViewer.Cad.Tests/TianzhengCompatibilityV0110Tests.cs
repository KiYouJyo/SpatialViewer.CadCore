using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengCompatibilityV0110Tests
{
    [Theory]
    [InlineData("TCH_WALL", null, null)]
    [InlineData("TCH_DIMENSION", null, null)]
    [InlineData("CUSTOM", "TDbWall", "Tianzheng Architecture")]
    [InlineData("CUSTOM", "SomeClass", "TArch")]
    [InlineData("CUSTOM", "SomeClass", "Beijing Tangent Technology")]
    [InlineData("CUSTOM", "SomeClass", "天正建筑")]
    public void ClassifierRecognizesConservativeTianzhengIdentities(string dxfName, string? cppClass, string? application)
    {
        Assert.True(CadCustomObjectClassifier.IsTianzheng(dxfName, cppClass, application));
    }

    [Theory]
    [InlineData("LINE", "AcDbLine", "ObjectDBX Classes")]
    [InlineData("AEC_WALL", "AecWall", "Autodesk AEC")]
    [InlineData("CUSTOM", "ThirdPartyWall", "OtherVendor")]
    [InlineData("CUSTOM", "TangentCurve", "OtherVendor")]
    public void ClassifierDoesNotClaimUnrelatedCustomObjects(string dxfName, string cppClass, string application)
    {
        Assert.False(CadCustomObjectClassifier.IsTianzheng(dxfName, cppClass, application));
    }

    [Fact]
    public void CustomClassPreservesClassesTableIdentity()
    {
        var definition = new CadCustomClassDefinition(
            "TCH_WALL",
            "TDbWall",
            "Tianzheng Architecture",
            501,
            24,
            true,
            "EraseAllowed, TransformAllowed",
            true);

        Assert.True(definition.IsTianzheng);
        Assert.Equal(501, definition.ClassNumber);
        Assert.Equal(24, definition.InstanceCount);
        Assert.True(definition.IsEntity);
        Assert.True(definition.WasProxy);
    }

    [Fact]
    public void CustomEntityIsPreservedWithoutPretendingNativeGeometryExists()
    {
        var definition = new CadCustomClassDefinition("TCH_WALL", "TDbWall", "Tianzheng Architecture", 501, 1, true, "None", false);
        var custom = new CadCustomEntity("C001", "TCH_WALL")
        {
            ClassDefinition = definition,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline", "Circle" }
        };
        var ordinary = new CadLineEntity("L001", Point2D.Origin, new Point2D(10, 0));
        var document = new CadDocument(
            "tianzheng-foundation.dwg",
            "DWG",
            "AC1032",
            CadUnits.Unitless,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom, ordinary })
        {
            CustomClasses = new[] { definition }
        };

        Assert.Single(document.CustomClasses);
        Assert.Contains(custom, document.ModelSpace);
        Assert.True(custom.IsTianzheng);
        Assert.Equal(CadCustomEntityRepresentation.ProxyGraphics, custom.Representation);
        Assert.Equal(2, custom.ProxyGraphicKinds.Count);
        Assert.Equal("Polyline", custom.ProxyGraphicKinds[0]);
        Assert.Equal("Circle", custom.ProxyGraphicKinds[1]);

        var sceneItems = document.Scene.GetItems().ToArray();
        Assert.Single(sceneItems);
        Assert.Equal(ordinary.ObjectId, sceneItems[0].Id);
        Assert.DoesNotContain(sceneItems, item => item.Id == custom.ObjectId);
    }

    [Fact]
    public void OpaqueCustomEntityRemainsDistinctFromOrdinaryUnsupportedEntity()
    {
        CadEntity custom = new CadCustomEntity("C002", "TCH_DOOR")
        {
            Representation = CadCustomEntityRepresentation.Opaque
        };
        CadEntity unsupported = new CadUnsupportedEntity("U002", "UNSUPPORTED");

        Assert.IsType<CadCustomEntity>(custom);
        Assert.IsType<CadUnsupportedEntity>(unsupported);
        Assert.True(((CadCustomEntity)custom).IsTianzheng);
        Assert.Equal(CadCustomEntityRepresentation.Opaque, ((CadCustomEntity)custom).Representation);
    }
}
