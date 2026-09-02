using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengCustomPayloadProfileV0120Tests
{
    [Fact]
    public void SameSchemaWithDifferentDrawingValuesProducesSameFingerprint()
    {
        var first = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(0, "TCH_OPENING"),
            new(5, "1A"),
            new(100, "AcDbEntity"),
            new(8, "WINDOW-A"),
            new(100, "TDbOpening"),
            new(10, "1200.25"),
            new(20, "900.5"),
            new(330, "FF")
        });
        var second = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(0, "TCH_OPENING"),
            new(5, "BEEF"),
            new(100, "AcDbEntity"),
            new(8, "PRIVATE-PROJECT-LAYER"),
            new(100, "TDbOpening"),
            new(10, "998877.125"),
            new(20, "-443322.75"),
            new(330, "ABCDEF")
        });

        var firstProfile = Assert.IsType<CadDxfCustomPayloadProfile>(CadDxfCustomPayloadProfiler.Create(first));
        var secondProfile = Assert.IsType<CadDxfCustomPayloadProfile>(CadDxfCustomPayloadProfiler.Create(second));

        Assert.Equal(firstProfile.Fingerprint, secondProfile.Fingerprint);
        Assert.Equal(firstProfile.GroupCodeSignature, secondProfile.GroupCodeSignature);
        Assert.Equal(new[] { "AcDbEntity", "TDbOpening" }, firstProfile.SubclassMarkers);
        Assert.DoesNotContain("1200.25", firstProfile.GroupCodeSignature, StringComparison.Ordinal);
        Assert.DoesNotContain("WINDOW-A", firstProfile.GroupCodeSignature, StringComparison.Ordinal);
        Assert.Equal(64, firstProfile.Fingerprint.Length);
    }

    [Fact]
    public void DifferentSubclassOrGroupLayoutProducesDifferentFingerprint()
    {
        var baseline = Profile(new CadRawDxfGroup[]
        {
            new(0, "TCH_OPENING"),
            new(100, "AcDbEntity"),
            new(100, "TDbOpening"),
            new(10, "1"),
            new(20, "2")
        });
        var differentSubclass = Profile(new CadRawDxfGroup[]
        {
            new(0, "TCH_OPENING"),
            new(100, "AcDbEntity"),
            new(100, "TDbOpeningV2"),
            new(10, "1"),
            new(20, "2")
        });
        var differentLayout = Profile(new CadRawDxfGroup[]
        {
            new(0, "TCH_OPENING"),
            new(100, "AcDbEntity"),
            new(100, "TDbOpening"),
            new(10, "1"),
            new(20, "2"),
            new(40, "900")
        });

        Assert.NotEqual(baseline.Fingerprint, differentSubclass.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, differentLayout.Fingerprint);
    }

    [Fact]
    public void HandleReferencesAreCanonicalizedWithoutAssigningRelationshipMeaning()
    {
        var payload = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(330, "1A"),
            new(340, "000000FF"),
            new(350, "ABCDEF"),
            new(360, "10"),
            new(320, "DEAD"),
            new(10, "123.5")
        });

        var references = CadDxfCustomPayloadProfiler.ExtractHandleReferences(payload);

        Assert.Collection(references,
            reference => { Assert.Equal(330, reference.GroupCode); Assert.Equal("26", reference.TargetHandle); },
            reference => { Assert.Equal(340, reference.GroupCode); Assert.Equal("255", reference.TargetHandle); },
            reference => { Assert.Equal(350, reference.GroupCode); Assert.Equal("11259375", reference.TargetHandle); },
            reference => { Assert.Equal(360, reference.GroupCode); Assert.Equal("16", reference.TargetHandle); });
        Assert.DoesNotContain(references, reference => reference.GroupCode == 320);
    }

    [Fact]
    public void TruncationStateParticipatesInSchemaFingerprint()
    {
        var groups = new CadRawDxfGroup[] { new(100, "TDbWall"), new(300, "opaque") };
        var complete = Profile(groups, false);
        var truncated = Profile(groups, true);

        Assert.NotEqual(complete.Fingerprint, truncated.Fingerprint);
    }

    private static CadDxfCustomPayloadProfile Profile(IReadOnlyList<CadRawDxfGroup> groups, bool truncated = false)
        => Assert.IsType<CadDxfCustomPayloadProfile>(CadDxfCustomPayloadProfiler.Create(new CadDxfCustomPayload(groups, truncated)));
}
