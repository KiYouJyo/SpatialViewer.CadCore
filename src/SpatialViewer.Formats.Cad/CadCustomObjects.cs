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
/// Proxy graphics are intentionally identified here but translated in a later compatibility layer.
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
