namespace SpatialViewer.Formats.Cad;

/// <summary>How much visual information survived reader adaptation for a custom CAD entity.</summary>
public enum CadCustomEntityRepresentation
{
    Opaque,
    ProxyGraphics
}

/// <summary>Reader-independent copy of one entry from the CAD CLASSES table.</summary>
public sealed record CadCustomClassDefinition(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    int ClassNumber,
    int InstanceCount,
    bool IsEntity,
    string ProxyFlags,
    bool WasProxy)
{
    public bool IsTianzheng => CadCustomObjectClassifier.IsTianzheng(DxfName, CppClassName, ApplicationName);
}

/// <summary>
/// Preserves an application-defined CAD entity even when CadCore does not yet understand its native semantics.
/// Decoded proxy primitives are display fallbacks only and do not imply native object semantics.
/// Raw DXF groups and bounded raw DWG object records are retained as evidence for later native decoders rather than interpreted speculatively.
/// </summary>
public sealed record CadCustomEntity(
    string Handle,
    string SourceEntityType,
    string LayerName = "0",
    CadColor Color = default,
    bool IsVisible = true,
    string LineTypeName = "Continuous",
    int? LineWeight = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
    : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value)
{
    public CadCustomClassDefinition? ClassDefinition { get; init; }
    public CadCustomEntityRepresentation Representation { get; init; } = CadCustomEntityRepresentation.Opaque;
    public IReadOnlyList<string> ProxyGraphicKinds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CadProxyPrimitive> ProxyPrimitives { get; init; } = Array.Empty<CadProxyPrimitive>();
    public CadDxfCustomPayload? RawDxfPayload { get; init; }
    public CadDxfCustomPayloadProfile? RawDxfProfile { get; init; }
    public IReadOnlyList<CadCustomHandleReference> HandleReferences { get; init; } = Array.Empty<CadCustomHandleReference>();
    public CadDwgCustomObjectRecord? RawDwgObjectRecord { get; init; }
    public CadCustomSemantic? NativeSemantics { get; init; }
    public bool IsTianzheng => ClassDefinition?.IsTianzheng == true || CadCustomObjectClassifier.IsTianzheng(SourceEntityType);
}

/// <summary>Conservative identification rules for Tianzheng application-defined CAD classes.</summary>
public static class CadCustomObjectClassifier
{
    public static bool IsTianzheng(string? dxfName, string? cppClassName = null, string? applicationName = null)
    {
        if (!string.IsNullOrWhiteSpace(dxfName) && dxfName.StartsWith("TCH_", StringComparison.OrdinalIgnoreCase)) return true;
        return ContainsExplicitIdentity(cppClassName) || ContainsApplicationIdentity(applicationName);
    }

    private static bool ContainsExplicitIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Contains("Tianzheng", StringComparison.OrdinalIgnoreCase)
            || value.Contains("TArch", StringComparison.OrdinalIgnoreCase)
            || value.Contains("天正", StringComparison.Ordinal);
    }

    private static bool ContainsApplicationIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return ContainsExplicitIdentity(value)
            || value.Contains("Tangent", StringComparison.OrdinalIgnoreCase);
    }
}
