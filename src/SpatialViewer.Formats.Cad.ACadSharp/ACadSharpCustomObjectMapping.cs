using System.Globalization;
using System.Security.Cryptography;
using ACadSharp.Classes;
using ACadSharp.Entities;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

public sealed partial class ACadSharpCadImporter
{
    private static bool IsCustomEntity(Entity entity)
        => entity is ProxyEntity or UnknownEntity || entity.ProxyGeometries.Count > 0;

    private static CadCustomEntity MapCustomEntity(Entity entity, CommonEntity common)
    {
        var sourceClass = entity switch
        {
            ProxyEntity proxy => proxy.DxfClass,
            UnknownEntity unknown => unknown.DxfClass,
            _ => null
        };
        var definition = sourceClass is null ? null : MapCustomClass(sourceClass);
        var graphicKinds = entity.ProxyGeometries
            .Select(graphic => graphic.GraphicsType.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var mappedProxyPrimitives = ACadSharpProxyGraphicsClipMapping.Map(
            entity.ProxyGeometries,
            out var unsupportedProxyGraphicCount,
            out var statefulGeometryCommandsPresent);
        var proxyPrimitives = ACadSharpProxyLayerProvenance.Apply(
            entity.ProxyGeometries,
            mappedProxyPrimitives,
            out var handledProxyLayerCommandCount);
        unsupportedProxyGraphicCount = Math.Max(0, unsupportedProxyGraphicCount - handledProxyLayerCommandCount);
        var proxyLayerIndices = ACadSharpProxyLayerProvenance.CollectLayerIndices(proxyPrimitives)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
        var proxyGraphicTraitsApplied = CadProxyTraitInspector.HasOverrides(proxyPrimitives);
        var representation = proxyPrimitives.Count > 0
            ? CadCustomEntityRepresentation.ProxyGraphics
            : CadCustomEntityRepresentation.Opaque;
        var rawDxfPayload = ACadSharpCustomPayloadContext.FindDxfPayload(common.Handle);
        var rawDxfProfile = CadDxfCustomPayloadProfiler.Create(rawDxfPayload);
        var handleReferences = CadDxfCustomPayloadProfiler.ExtractHandleReferences(rawDxfPayload);
        var rawDwgObjectRecord = ACadSharpCustomPayloadContext.FindDwgObjectRecord(common.Handle);
        var rawScan = ACadSharpCustomPayloadContext.Snapshot();
        var rawDwgCapture = ACadSharpCustomPayloadContext.SnapshotDwg();
        var proxyCommandInventory = ACadSharpCustomPayloadContext.FindProxyCommandInventory(common.Handle);
        var proxyCommandCapture = ACadSharpCustomPayloadContext.SnapshotProxyCommands();
        var nativeSemantics = CadTianzhengSemanticDecoder.Decode(entity.ObjectName, definition, rawDxfPayload)
            ?? CadTianzhengStairSemanticDecoder.Decode(entity.ObjectName, definition, rawDxfPayload);
        var customVendor = definition?.Vendor ?? CadCustomObjectClassifier.Classify(entity.ObjectName);
        var metadata = new Dictionary<string, string>(common.Metadata, StringComparer.Ordinal)
        {
            ["CustomEntity"] = bool.TrueString,
            ["CustomEntityType"] = entity.ObjectName,
            ["CustomVendor"] = customVendor.ToString(),
            ["TianzhengObject"] = (customVendor == CadCustomObjectVendor.Tianzheng).ToString(),
            ["XiangyuanObject"] = (customVendor == CadCustomObjectVendor.Xiangyuan).ToString(),
            ["CustomRepresentation"] = representation.ToString(),
            ["ProxyGraphicCount"] = entity.ProxyGeometries.Count.ToString(CultureInfo.InvariantCulture),
            ["ProxyGraphicKinds"] = string.Join(';', graphicKinds),
            ["ProxyGraphicTranslatedCount"] = proxyPrimitives.Count.ToString(CultureInfo.InvariantCulture),
            ["ProxyGraphicUnsupportedCount"] = unsupportedProxyGraphicCount.ToString(CultureInfo.InvariantCulture),
            ["ProxyGraphicStatefulGeometryCommandsPresent"] = statefulGeometryCommandsPresent.ToString(),
            ["ProxyGraphicTraitsApplied"] = proxyGraphicTraitsApplied.ToString(),
            ["ProxyGraphicLayerCommandCount"] = handledProxyLayerCommandCount.ToString(CultureInfo.InvariantCulture),
            ["ProxyGraphicLayerProvenanceApplied"] = (handledProxyLayerCommandCount > 0).ToString(),
            ["ProxyGraphicLayerIndices"] = string.Join(';', proxyLayerIndices.Select(index => index.ToString(CultureInfo.InvariantCulture))),
            ["RawDxfPayloadAvailable"] = (rawDxfPayload is not null).ToString(),
            ["RawDxfScanBinary"] = (rawScan?.IsBinaryDxf == true).ToString(),
            ["RawDxfScanFailed"] = (rawScan?.ScanFailed == true).ToString(),
            ["RawDwgObjectRecordAvailable"] = (rawDwgObjectRecord is not null).ToString(),
            ["RawDwgCaptureSupported"] = (rawDwgCapture?.Supported == true).ToString(),
            ["RawDwgCaptureFailed"] = (rawDwgCapture?.CaptureFailed == true).ToString(),
            ["RawProxyCommandInventoryAvailable"] = (proxyCommandInventory is not null).ToString(),
            ["RawProxyCommandCaptureSupported"] = (proxyCommandCapture?.Supported == true).ToString(),
            ["RawProxyCommandCaptureFailed"] = (proxyCommandCapture?.CaptureFailed == true).ToString(),
            ["CustomHandleReferenceCount"] = handleReferences.Count.ToString(CultureInfo.InvariantCulture),
            ["NativeSemanticsDecoded"] = (nativeSemantics is not null).ToString(),
            ["NativeSemanticEvidenceDecoded"] = (nativeSemantics is not null).ToString(),
            ["NativeSemanticCoverage"] = nativeSemantics?.Coverage.ToString() ?? "None",
            ["NativeSemanticDrawable2D"] = (nativeSemantics?.IsDrawable2D == true).ToString()
        };
        if (proxyCommandInventory is not null)
        {
            metadata["RawProxyCommandDeclaredByteCount"] = proxyCommandInventory.DeclaredByteSize.ToString(CultureInfo.InvariantCulture);
            metadata["RawProxyCommandDeclaredCount"] = proxyCommandInventory.DeclaredCommandCount.ToString(CultureInfo.InvariantCulture);
            metadata["RawProxyCommandScannedCount"] = proxyCommandInventory.ScannedCommandCount.ToString(CultureInfo.InvariantCulture);
            metadata["RawProxyCommandKnownCount"] = proxyCommandInventory.KnownCommandCount.ToString(CultureInfo.InvariantCulture);
            metadata["RawProxyCommandUnknownCount"] = proxyCommandInventory.UnknownCommandCount.ToString(CultureInfo.InvariantCulture);
            metadata["RawProxyCommandMalformed"] = proxyCommandInventory.IsMalformed.ToString();
            metadata["RawProxyCommandTruncated"] = proxyCommandInventory.IsTruncated.ToString();
            metadata["RawProxyCommandTypeSignature"] = proxyCommandInventory.TypeSignature;
            metadata["RawProxyUnknownTypeIds"] = string.Join(
                ';',
                proxyCommandInventory.UnknownTypeIds.Select(typeId => typeId.ToString(CultureInfo.InvariantCulture)));
        }

        if (rawDxfPayload is not null)
        {
            metadata["RawDxfGroupCount"] = rawDxfPayload.Groups.Count.ToString(CultureInfo.InvariantCulture);
            metadata["RawDxfPayloadTruncated"] = rawDxfPayload.IsTruncated.ToString();
            metadata["RawDxfByteProjection"] = rawDxfPayload.ByteProjection;
        }
        if (rawDxfProfile is not null)
        {
            metadata["RawDxfSchemaFingerprint"] = rawDxfProfile.Fingerprint;
            metadata["RawDxfGroupCodeSignature"] = rawDxfProfile.GroupCodeSignature;
            metadata["RawDxfSubclassMarkers"] = string.Join(';', rawDxfProfile.SubclassMarkers);
        }
        if (rawDwgObjectRecord is not null)
        {
            metadata["RawDwgObjectRecordByteCount"] = rawDwgObjectRecord.ByteCount.ToString(CultureInfo.InvariantCulture);
            metadata["RawDwgObjectRecordOffset"] = rawDwgObjectRecord.ObjectSectionOffset.ToString(CultureInfo.InvariantCulture);
            metadata["RawDwgObjectRecordTruncated"] = rawDwgObjectRecord.IsTruncated.ToString();
            metadata["RawDwgCaptureMethod"] = rawDwgObjectRecord.CaptureMethod;
            metadata["RawDwgObjectRecordSha256"] = Convert.ToHexString(SHA256.HashData(rawDwgObjectRecord.Bytes.Span)).ToLowerInvariant();
        }
        if (handleReferences.Count > 0)
        {
            metadata["CustomHandleReferenceCodes"] = string.Join(';', handleReferences.Select(reference => reference.GroupCode.ToString(CultureInfo.InvariantCulture)).Distinct(StringComparer.Ordinal));
        }
        if (definition is not null)
        {
            metadata["CustomDxfClass"] = definition.DxfName;
            metadata["CustomCppClass"] = definition.CppClassName;
            metadata["CustomApplication"] = definition.ApplicationName;
            metadata["CustomClassNumber"] = definition.ClassNumber.ToString(CultureInfo.InvariantCulture);
            metadata["CustomProxyFlags"] = definition.ProxyFlags;
        }
        if (nativeSemantics is not null)
        {
            metadata["NativeSemanticType"] = nativeSemantics.GetType().Name;
            metadata["NativeDecoderProfile"] = nativeSemantics.DecoderProfile;
        }

        return new CadCustomEntity(common.Handle, entity.ObjectName, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, metadata)
        {
            ClassDefinition = definition,
            Representation = representation,
            ProxyGraphicKinds = graphicKinds,
            ProxyPrimitives = proxyPrimitives,
            RawDxfPayload = rawDxfPayload,
            RawDxfProfile = rawDxfProfile,
            HandleReferences = handleReferences,
            RawDwgObjectRecord = rawDwgObjectRecord,
            NativeSemantics = nativeSemantics
        };
    }

    private static CadCustomClassDefinition[] MapCustomClasses(global::ACadSharp.CadDocument source)
        => source.Classes
            .Select(MapCustomClass)
            .OrderBy(definition => definition.ClassNumber)
            .ToArray();

    private static CadCustomClassDefinition MapCustomClass(DxfClass source)
        => new(
            source.DxfName ?? string.Empty,
            source.CppClassName ?? string.Empty,
            source.ApplicationName ?? string.Empty,
            source.ClassNumber,
            source.InstanceCount,
            source.IsAnEntity,
            source.ProxyFlags.ToString(),
            source.WasZombie);
}
