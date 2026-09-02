using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// An anonymized structural description of an application-defined text-DXF payload. The fingerprint
/// contains group-code order and subclass marker identity, but deliberately excludes ordinary raw values.
/// This allows Tianzheng layouts to be clustered by schema without collecting drawing coordinates or labels.
/// </summary>
public sealed record CadDxfCustomPayloadProfile(
    string Fingerprint,
    string GroupCodeSignature,
    IReadOnlyList<string> SubclassMarkers,
    IReadOnlyDictionary<int, int> GroupCodeCounts);

/// <summary>One object-handle relationship retained from a custom DXF entity.</summary>
public sealed record CadCustomHandleReference(int GroupCode, string TargetHandle);

/// <summary>Creates privacy-preserving schema fingerprints and generic handle relationships from raw custom payload evidence.</summary>
public static class CadDxfCustomPayloadProfiler
{
    private static readonly HashSet<int> ObjectReferenceCodes = new() { 330, 340, 350, 360 };

    public static CadDxfCustomPayloadProfile? Create(CadDxfCustomPayload? payload)
    {
        if (payload is null) return null;
        var codeSignature = string.Join(',', payload.Groups.Select(group => group.Code.ToString(CultureInfo.InvariantCulture)));
        var subclassMarkers = payload.Groups
            .Where(group => group.Code == 100)
            .Select(group => group.RawValue.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
        var counts = payload.Groups
            .GroupBy(group => group.Code)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());
        var normalized = $"codes:{codeSignature}|subclasses:{string.Join('>', subclassMarkers)}|truncated:{payload.IsTruncated}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return new CadDxfCustomPayloadProfile(
            fingerprint,
            codeSignature,
            subclassMarkers,
            new ReadOnlyDictionary<int, int>(counts));
    }

    public static IReadOnlyList<CadCustomHandleReference> ExtractHandleReferences(CadDxfCustomPayload? payload)
    {
        if (payload is null) return Array.Empty<CadCustomHandleReference>();
        return payload.Groups
            .Where(group => ObjectReferenceCodes.Contains(group.Code))
            .Select(group => new CadCustomHandleReference(group.Code, CanonicalHandle(group.RawValue)))
            .Where(reference => reference.TargetHandle.Length > 0)
            .ToArray();
    }

    public static string CanonicalHandle(string rawHandle)
    {
        if (string.IsNullOrWhiteSpace(rawHandle)) return string.Empty;
        var trimmed = rawHandle.Trim();
        return ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? value.ToString(CultureInfo.InvariantCulture)
            : trimmed;
    }
}
