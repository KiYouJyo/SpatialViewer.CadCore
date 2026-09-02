using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengRelationshipGraphV0120Tests
{
    private static readonly CadCustomClassDefinition OpeningClass = new(
        "TCH_OPENING", "TDbOpening", "Tianzheng Architecture", 602, 1, true, "None", false);
    private static readonly CadCustomClassDefinition WallClass = new(
        "TCH_WALL", "TDbWall", "Tianzheng Architecture", 601, 1, true, "None", false);

    [Fact]
    public void OpeningHostWallIsResolvedByTargetIdentityInsteadOfReferenceOrder()
    {
        var wall = new CadCustomEntity("100", "TCH_WALL")
        {
            ClassDefinition = WallClass,
            NativeSemantics = new CadTianzhengWallSemantic(
                new Point2D(0, 0), new Point2D(1000, 0), 100, 100, 0, 3000,
                CadTianzhengSemanticDecoder.WallDirectProfile)
        };
        var unrelated = new CadLineEntity("999", new Point2D(0, 0), new Point2D(0, 100));
        var opening = new CadCustomEntity("200", "TCH_OPENING")
        {
            ClassDefinition = OpeningClass,
            HandleReferences = new CadCustomHandleReference[]
            {
                new(330, "999"),
                new(330, "100"),
                new(340, "100"),
                new(350, "404")
            }
        };
        var document = Document(wall, unrelated, opening);

        var relationships = CadCustomRelationshipResolver.Resolve(document);

        Assert.Equal(3, relationships.Count);
        var unrelatedEdge = Assert.Single(relationships.Where(relationship => relationship.TargetHandle == "999"));
        Assert.Equal(CadCustomRelationshipKind.ObjectReference, unrelatedEdge.Kind);
        Assert.Equal(330, unrelatedEdge.GroupCode);
        var wallEdges = relationships.Where(relationship => relationship.TargetHandle == "100").OrderBy(relationship => relationship.GroupCode).ToArray();
        Assert.Equal(2, wallEdges.Length);
        Assert.Equal(CadCustomRelationshipKind.TianzhengOpeningHostWall, wallEdges[0].Kind);
        Assert.Equal(CadCustomRelationshipKind.TianzhengOpeningHostWall, wallEdges[1].Kind);
        Assert.Equal(330, wallEdges[0].GroupCode);
        Assert.Equal(340, wallEdges[1].GroupCode);
        Assert.Equal(opening.ObjectId, wallEdges[0].SourceObjectId);
        Assert.Equal(wall.ObjectId, wallEdges[0].TargetObjectId);
        Assert.DoesNotContain(relationships, relationship => relationship.TargetHandle == "404");
    }

    [Fact]
    public void UnrelatedVendorObjectsAreNotPromotedToTianzhengRelationships()
    {
        var vendorWall = new CadCustomEntity("300", "VENDOR_WALL")
        {
            ClassDefinition = new CadCustomClassDefinition(
                "VENDOR_WALL", "VendorWall", "Vendor Application", 700, 1, true, "None", false)
        };
        var vendorOpening = new CadCustomEntity("301", "VENDOR_OPENING")
        {
            ClassDefinition = new CadCustomClassDefinition(
                "VENDOR_OPENING", "VendorOpening", "Vendor Application", 701, 1, true, "None", false),
            HandleReferences = new CadCustomHandleReference[] { new(330, "300") }
        };

        var relationship = Assert.Single(CadCustomRelationshipResolver.Resolve(Document(vendorWall, vendorOpening)));

        Assert.Equal(CadCustomRelationshipKind.ObjectReference, relationship.Kind);
        Assert.Equal("VENDOR_OPENING", relationship.SourceEntityType);
        Assert.Equal("VENDOR_WALL", relationship.TargetEntityType);
    }

    [Fact]
    public void ResolverIncludesCustomEntitiesStoredInBlocksAndPaperSpace()
    {
        var wall = new CadCustomEntity("500", "TCH_WALL") { ClassDefinition = WallClass };
        var opening = new CadCustomEntity("501", "TCH_OPENING")
        {
            ClassDefinition = OpeningClass,
            HandleReferences = new CadCustomHandleReference[] { new(330, "500") }
        };
        var block = new CadBlockDefinition("TchWallBlock", Point2D.Zero, new CadEntity[] { wall });
        var layout = new CadLayoutDefinition(
            "Sheet1",
            1,
            true,
            new Size2D(420, 297),
            new BoundingBox2D(0, 0, 420, 297),
            new BoundingBox2D(0, 0, 420, 297),
            new CadEntity[] { opening },
            Array.Empty<CadViewportDefinition>());
        var document = new CadDocument(
            "relationships.dxf",
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            new[] { block },
            Array.Empty<CadEntity>(),
            layouts: new[] { layout });

        var relationship = Assert.Single(CadCustomRelationshipResolver.Resolve(document));

        Assert.Equal(CadCustomRelationshipKind.TianzhengOpeningHostWall, relationship.Kind);
        Assert.Equal("501", relationship.SourceHandle);
        Assert.Equal("500", relationship.TargetHandle);
    }

    private static CadDocument Document(params CadEntity[] entities)
        => new(
            "relationships.dxf",
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            entities);
}
