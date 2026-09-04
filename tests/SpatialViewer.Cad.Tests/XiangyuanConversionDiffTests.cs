using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanConversionDiffTests
{
    private static readonly CadCustomClassDefinition CandidateClass = new(
        "VENDOR_UNKNOWN_OBJECT",
        "VendorUnknownObject",
        "VendorPrivateApp",
        1001,
        1,
        true,
        "EraseAllowed",
        true);

    private static readonly CadCustomClassDefinition RetainedClass = new(
        "TCH_WALL",
        "TDbWall",
        "Tianzheng Architecture",
        1002,
        1,
        true,
        "None",
        false);

    private static readonly CadCustomClassDefinition AddedClass = new(
        "OTHER_OUTPUT_OBJECT",
        "OtherOutputObject",
        "OtherOutputApp",
        1003,
        1,
        true,
        "None",
        false);

    [Fact]
    public void CompareMarksDisappearedUnknownProfileAsCandidateWithoutChangingVendor()
    {
        var native = Discovery(Document(
            "native-secret.dxf",
            new[] { CandidateClass, RetainedClass },
            Entity("100", CandidateClass, "PRIVATE_NATIVE"),
            Entity("200", RetainedClass, "PRIVATE_RETAINED")));
        var converted = Discovery(Document(
            "converted-secret.dxf",
            new[] { RetainedClass, AddedClass },
            Entity("300", RetainedClass, "PRIVATE_CONVERTED"),
            Entity("400", AddedClass, "PRIVATE_ADDED")));

        var report = CadXiangyuanConversionDiffer.Compare(native, converted);

        Assert.Equal(1, report.RemovedClassCount);
        Assert.Equal(1, report.RemovedProfileCount);
        Assert.Equal(1, report.RetainedClassCount);
        Assert.Equal(1, report.RetainedProfileCount);

        var removedClass = Assert.Single(report.Classes, item => item.Status == CadXiangyuanConversionDiffStatus.RemovedAfterConversion);
        Assert.Equal(CandidateClass.DxfName, removedClass.DxfName);
        Assert.Equal(CadCustomObjectVendor.Unknown, removedClass.ClassifiedVendor);
        Assert.Equal(1, removedClass.NativeDeclaredInstanceCount);
        Assert.Equal(0, removedClass.ConvertedDeclaredInstanceCount);

        var removedProfile = Assert.Single(report.Profiles, item => item.Status == CadXiangyuanConversionDiffStatus.RemovedAfterConversion);
        Assert.Equal(CandidateClass.DxfName, removedProfile.DxfName);
        Assert.Equal(CadCustomObjectVendor.Unknown, removedProfile.ClassifiedVendor);
        Assert.Equal(1, removedProfile.NativeEntityCount);
        Assert.Equal(0, removedProfile.ConvertedEntityCount);

        var addedClass = Assert.Single(report.Classes, item => item.Status == CadXiangyuanConversionDiffStatus.AddedAfterConversion);
        Assert.Equal(AddedClass.DxfName, addedClass.DxfName);
        Assert.Equal(CadCustomObjectVendor.Unknown, addedClass.ClassifiedVendor);
    }

    [Fact]
    public void JsonRoundTripContainsOnlyStructuralConversionEvidence()
    {
        var native = Discovery(Document(
            "PRIVATE_NATIVE_FILE.dxf",
            new[] { CandidateClass },
            Entity("SECRET_NATIVE_HANDLE", CandidateClass, "SECRET_NATIVE_VALUE")));
        var converted = Discovery(Document(
            "PRIVATE_CONVERTED_FILE.dxf",
            Array.Empty<CadCustomClassDefinition>()));

        var report = CadXiangyuanConversionDiffer.Compare(native, converted);
        var json = CadXiangyuanConversionDiffer.ToJson(report);
        var roundTrip = CadXiangyuanConversionDiffer.FromJson(json);

        Assert.DoesNotContain("PRIVATE_NATIVE_FILE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_CONVERTED_FILE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_NATIVE_HANDLE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_NATIVE_VALUE", json, StringComparison.Ordinal);
        Assert.Contains(CandidateClass.DxfName, json, StringComparison.Ordinal);
        Assert.Equal(report.Classes.ToArray(), roundTrip.Classes.ToArray());
        Assert.Equal(report.Profiles.ToArray(), roundTrip.Profiles.ToArray());
    }

    [Fact]
    public void CompareRejectsMergedDiscoveryReports()
    {
        var single = Discovery(Document(
            "one.dxf",
            new[] { CandidateClass },
            Entity("1", CandidateClass, "one")));
        var merged = CadXiangyuanDiscoveryCorpus.Merge(new[] { single, single });

        var exception = Assert.Throws<ArgumentException>(() => CadXiangyuanConversionDiffer.Compare(merged, single));

        Assert.Contains("exactly one", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CadXiangyuanDiscoveryReport Discovery(CadDocument document)
        => CadXiangyuanDiscoveryCorpus.Build(document);

    private static CadCustomEntity Entity(string handle, CadCustomClassDefinition definition, string privateValue)
    {
        var payload = new CadDxfCustomPayload(
            new CadRawDxfGroup[]
            {
                new(100, "AcDbEntity"),
                new(100, "SyntheticCustomObject"),
                new(10, privateValue),
                new(40, "2.5")
            });
        return new CadCustomEntity(handle, definition.DxfName)
        {
            ClassDefinition = definition,
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload)
        };
    }

    private static CadDocument Document(
        string displayName,
        IReadOnlyList<CadCustomClassDefinition> customClasses,
        params CadEntity[] entities)
        => new CadDocument(
            displayName,
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            entities)
        {
            CustomClasses = customClasses
        };
}
