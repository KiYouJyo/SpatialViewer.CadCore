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
        var proxyPrimitives = ACadSharpProxyGraphicsMapping.Map(
            entity.ProxyGeometries,
            out var unsupportedProxyGraphicCount,
            out var statefulGeometryCommandsPresent);
        var representation = proxyPrimitives.Count > 0
            ? CadCustomEntityRepresentation.ProxyGraphics
            : CadCustomEntityRepresentation.Opaque;
        var rawDxfPayload = ACadSharpCustomPayloadContext.FindDxfPayload(common.Handle);
        var rawScan = ACadSharpCustomPayloadContext.Snapshot();
        var metadata = new Dictionary<string, string>(common.Metadata, StringComparer.Ordinal)
        {
            ["CustomEntity"] = bool.TrueString,
            ["CustomEntityType"] = entity.ObjectName,
            ["CustomRepresentation"] = representation.ToString(),
            ["ProxyGraphicCount"] = entity.ProxyGeometries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ProxyGraphicKinds"] = string.Join(';', graphicKinds),
            ["ProxyGraphicTranslatedCount"] = proxyPrimitives.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ProxyGraphicUnsupportedCount"] = unsupportedProxyGraphicCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ProxyGraphicStatefulGeometryCommandsPresent"] = statefulGeometryCommandsPresent.ToString(),
            ["ProxyGraphicTraitsApplied"] = bool.FalseString,
            ["RawDxfPayloadAvailable"] = (rawDxfPayload is not null).ToString(),
            ["RawDxfScanBinary"] = (rawScan?.IsBinaryDxf == true).ToString(),
            ["RawDxfScanFailed"] = (rawScan?.ScanFailed == true).ToString()
        };
        if (rawDxfPayload is not null)
        {
            metadata["RawDxfGroupCount"] = rawDxfPayload.Groups.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            metadata["RawDxfPayloadTruncated"] = rawDxfPayload.IsTruncated.ToString();
            metadata["RawDxfByteProjection"] = rawDxfPayload.ByteProjection;
        }
        if (definition is not null)
        {
            metadata["CustomDxfClass"] = definition.DxfName;
            metadata["CustomCppClass"] = definition.CppClassName;
            metadata["CustomApplication"] = definition.ApplicationName;
            metadata["CustomClassNumber"] = definition.ClassNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            metadata["CustomProxyFlags"] = definition.ProxyFlags;
            metadata["TianzhengObject"] = definition.IsTianzheng.ToString();
        }

        return new CadCustomEntity(common.Handle, entity.ObjectName, common.Layer, common.Color, common.Visible, common.LineType, common.LineWeight, metadata)
        {
            ClassDefinition = definition,
            Representation = representation,
            ProxyGraphicKinds = graphicKinds,
            ProxyPrimitives = proxyPrimitives,
            RawDxfPayload = rawDxfPayload
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
