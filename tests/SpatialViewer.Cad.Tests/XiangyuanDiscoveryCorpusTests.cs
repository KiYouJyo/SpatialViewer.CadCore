using System.Text;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanDiscoveryCorpusTests
{
    private static readonly IReadOnlyList<CadCustomHandleReference> NoReferences = Array.Empty<CadCustomHandleReference>();
    private static readonly string[] PolylineProxyKinds = { "Polyline" };

    private static readonly CadCustomClassDefinition UnknownClass = new(
        "VENDOR_PRIVATE_PARCEL",
        "VendorPrivateParcel",
        "VendorPrivateApp",
        901,
        2,
        true,
        "EraseAllowed",
        true);

    private static readonly CadCustomClassDefinition XiangyuanClass = new(
        "XY_CONFIRMED",
        "XiangyuanConfirmedObject",
        "LzxSoft Control Planning CAD",
        902,
        1,
        true,
        "None",
        true);

    private static readonly CadCustomClassDefinition TianzhengClass = new(
        "TCH_WALL",
        "TDbWall",
        "Tianzheng Architecture",
        903,
        1,
        true,
        "None",
        false);

    private static readonly CadCustomClassDefinition UninstantiatedClass = new(
        "VENDOR_UNUSED",
        "VendorUnusedObject",
        "VendorPrivateApp",
        904,
        0,
        true,
        "None",
        false);

    [Fact]
    public void BuildInventoriesUnknownClassesWithoutPromotingThemToXiangyuan()
    {
        var unknown = Entity("100", UnknownClass, Payload("PRIVATE_UNKNOWN_VALUE")) with
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = PolylineProxyKinds
        };
        var confirmed = Entity("200", XiangyuanClass, Payload("PRIVATE_XY_VALUE"));
        var tianzheng = Entity("300", TianzhengClass, Payload("PRIVATE_TCH_VALUE"));
        var document = Document(
            "known-xiangyuan-private.dxf",
            new[] { UnknownClass, XiangyuanClass, TianzhengClass, UninstantiatedClass },
            unknown,
            confirmed,
            tianzheng);

        var report = CadXiangyuanDiscoveryCorpus.Build(document);

        Assert.Equal(1, report.SchemaVersion);
        Assert.Equal(1, report.SampleCount);
        Assert.Equal(4, report.Classes.Count);
        Assert.Equal(3, report.Profiles.Count);
        Assert.Equal(3, report.CustomEntityCount);
        Assert.Equal(1, report.KnownXiangyuanEntityCount);
        Assert.Equal(1, report.UnknownVendorEntityCount);

        var unknownClass = Assert.Single(report.Classes, entry => entry.DxfName == UnknownClass.DxfName);
        Assert.Equal(CadCustomObjectVendor.Unknown, unknownClass.ClassifiedVendor);
        Assert.Equal(2, unknownClass.DeclaredInstanceCount);

        var unknownProfile = Assert.Single(report.Profiles, entry => entry.DxfName == UnknownClass.DxfName);
        Assert.Equal(CadCustomObjectVendor.Unknown, unknownProfile.ClassifiedVendor);
        Assert.Equal(1, unknownProfile.ProxyGraphicsEntityCount);
        Assert.Equal(0, unknownProfile.OpaqueEntityCount);

        var unused = Assert.Single(report.Classes, entry => entry.DxfName == UninstantiatedClass.DxfName);
        Assert.Equal(0, unused.DeclaredInstanceCount);
        Assert.DoesNotContain(report.Profiles, entry => entry.DxfName == UninstantiatedClass.DxfName);
    }

    [Fact]
    public void JsonDoesNotExposeDrawingValuesHandlesPathsOrRawDwgBytes()
    {
        const string privateRawValue = "SECRET_PARCEL_ATTRIBUTE";
        const string privateHandle = "SECRET_HANDLE_ABC";
        const string privateDrawing = "client-project-secret.dxf";
        var entity = Entity(privateHandle, UnknownClass, Payload(privateRawValue)) with
        {
            RawDwgObjectRecord = new CadDwgCustomObjectRecord(
                Encoding.UTF8.GetBytes("SECRET_DWG_BYTES"),
                1234567,
                false,
                "test"),
            HandleReferences = new[] { new CadCustomHandleReference(330, "SECRET_TARGET_HANDLE") }
        };
        var document = Document(privateDrawing, new[] { UnknownClass }, entity);

        var json = CadXiangyuanDiscoveryCorpus.ToJson(CadXiangyuanDiscoveryCorpus.Build(document));

        Assert.DoesNotContain(privateRawValue, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateHandle, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateDrawing, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET_DWG_BYTES", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_TARGET_HANDLE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567", json, StringComparison.Ordinal);
        Assert.Contains(UnknownClass.DxfName, json, StringComparison.Ordinal);
        Assert.Contains(UnknownClass.CppClassName, json, StringComparison.Ordinal);
        Assert.Contains(UnknownClass.ApplicationName, json, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeTracksCrossSampleClassAndProfileCoverage()
    {
        var firstDocument = Document(
            "one.dxf",
            new[] { UnknownClass },
            Entity("401", UnknownClass, Payload("one")));
        var secondDocument = Document(
            "two.dxf",
            new[] { UnknownClass },
            Entity("501", UnknownClass, Payload("two")));

        var merged = CadXiangyuanDiscoveryCorpus.Build(new[] { firstDocument, secondDocument });
        var classEntry = Assert.Single(merged.Classes);
        var profile = Assert.Single(merged.Profiles);

        Assert.Equal(2, merged.SampleCount);
        Assert.Equal(4, classEntry.DeclaredInstanceCount);
        Assert.Equal(2, classEntry.SamplesContainingClass);
        Assert.Equal(2, profile.EntityCount);
        Assert.Equal(2, profile.SamplesContainingProfile);
        Assert.Equal(CadCustomObjectVendor.Unknown, profile.ClassifiedVendor);

        var roundTrip = CadXiangyuanDiscoveryCorpus.FromJson(CadXiangyuanDiscoveryCorpus.ToJson(merged));
        Assert.Equal(merged.SchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(merged.SampleCount, roundTrip.SampleCount);
        Assert.Equal(merged.Classes.ToArray(), roundTrip.Classes.ToArray());
        Assert.Equal(merged.Profiles.ToArray(), roundTrip.Profiles.ToArray());
    }

    [Fact]
    public void ValidationRejectsInventedVendorEnumValue()
    {
        var invalid = new CadXiangyuanDiscoveryReport(
            CadXiangyuanDiscoveryCorpus.CurrentSchemaVersion,
            1,
            new[]
            {
                new CadXiangyuanDiscoveryClassEntry(
                    "X",
                    "Y",
                    "Z",
                    (CadCustomObjectVendor)999,
                    true,
                    false,
                    "None",
                    1,
                    1)
            },
            Array.Empty<CadXiangyuanDiscoveryProfileEntry>());

        var exception = Assert.Throws<ArgumentException>(() => CadXiangyuanDiscoveryCorpus.ToJson(invalid));

        Assert.Contains("vendor", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CadCustomEntity Entity(
        string handle,
        CadCustomClassDefinition definition,
        CadDxfCustomPayload payload)
        => new(handle, definition.DxfName)
        {
            ClassDefinition = definition,
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload),
            HandleReferences = NoReferences
        };

    private static CadDxfCustomPayload Payload(string privateValue)
        => new(
            new CadRawDxfGroup[]
            {
                new(100, "AcDbEntity"),
                new(100, "VendorSyntheticObject"),
                new(10, privateValue),
                new(40, "2.5")
            });

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
