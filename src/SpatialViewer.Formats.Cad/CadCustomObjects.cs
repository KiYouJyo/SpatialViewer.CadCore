namespace SpatialViewer.Formats.Cad;

/// <summary>How much visual information survived reader adaptation for a custom CAD entity.</summary>
public enum CadCustomEntityRepresentation
{
    Opaque,
    ProxyGraphics
}

/// <summary>Known application families for reader-independent custom CAD objects.</summary>
public enum CadCustomObjectVendor
{
    Unknown,
    Tianzheng,
    Xiangyuan
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
    public CadCustomObjectVendor Vendor => CadCustomObjectClassifier.Classify(DxfName, CppClassName, ApplicationName);
    public bool IsTianzheng => Vendor == CadCustomObjectVendor.Tianzheng;
    public bool IsXiangyuan => Vendor == CadCustomObjectVendor.Xiangyuan;
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

    public CadCustomObjectVendor Vendor
    {
        get
        {
            var classVendor = ClassDefinition?.Vendor ?? CadCustomObjectVendor.Unknown;
            return classVendor != CadCustomObjectVendor.Unknown
                ? classVendor
                : CadCustomObjectClassifier.Classify(SourceEntityType);
        }
    }

    public bool IsTianzheng => Vendor == CadCustomObjectVendor.Tianzheng;
    public bool IsXiangyuan => Vendor == CadCustomObjectVendor.Xiangyuan;
}

/// <summary>Conservative vendor identification rules for application-defined CAD classes.</summary>
public static class CadCustomObjectClassifier
{
    public static CadCustomObjectVendor Classify(string? dxfName, string? cppClassName = null, string? applicationName = null)
    {
        if (IsTianzhengIdentity(dxfName, cppClassName, applicationName)) return CadCustomObjectVendor.Tianzheng;
        if (IsXiangyuanIdentity(dxfName, cppClassName, applicationName)) return CadCustomObjectVendor.Xiangyuan;
        return CadCustomObjectVendor.Unknown;
    }

    public static bool IsTianzheng(string? dxfName, string? cppClassName = null, string? applicationName = null)
        => Classify(dxfName, cppClassName, applicationName) == CadCustomObjectVendor.Tianzheng;

    public static bool IsXiangyuan(string? dxfName, string? cppClassName = null, string? applicationName = null)
        => Classify(dxfName, cppClassName, applicationName) == CadCustomObjectVendor.Xiangyuan;

    private static bool IsTianzhengIdentity(string? dxfName, string? cppClassName, string? applicationName)
    {
        if (!string.IsNullOrWhiteSpace(dxfName) && dxfName.StartsWith("TCH_", StringComparison.OrdinalIgnoreCase)) return true;
        return ContainsTianzhengExplicitIdentity(cppClassName) || ContainsTianzhengApplicationIdentity(applicationName);
    }

    private static bool IsXiangyuanIdentity(string? dxfName, string? cppClassName, string? applicationName)
    {
        // Do not infer real Xiangyuan DXF class names from the public LZX command/menu prefix.
        // Until real CLASSES-table samples prove a class-name convention, require an explicit
        // application/C++ identity instead of guessing from the entity token alone.
        _ = dxfName;
        return ContainsXiangyuanExplicitIdentity(cppClassName) || ContainsXiangyuanApplicationIdentity(applicationName);
    }

    private static bool ContainsTianzhengExplicitIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Contains("Tianzheng", StringComparison.OrdinalIgnoreCase)
            || value.Contains("TArch", StringComparison.OrdinalIgnoreCase)
            || value.Contains("天正", StringComparison.Ordinal);
    }

    private static bool ContainsTianzhengApplicationIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return ContainsTianzhengExplicitIdentity(value)
            || value.Contains("Tangent", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsXiangyuanExplicitIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Contains("Xiangyuan", StringComparison.OrdinalIgnoreCase)
            || value.Contains("湘源", StringComparison.Ordinal)
            || (value.Length > 3 && value.StartsWith("Lzx", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsXiangyuanApplicationIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return ContainsXiangyuanExplicitIdentity(value)
            || value.Contains("LzxSoft", StringComparison.OrdinalIgnoreCase);
    }
}
