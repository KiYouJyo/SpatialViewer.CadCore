using System.Collections;
using System.Globalization;
using System.Reflection;
using ACadSharp.IO;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

internal sealed record ProxyGraphicsCommandCaptureSnapshot(
    bool Supported,
    bool CaptureFailed,
    int CapturedEntityCount,
    int MalformedEntityCount,
    int UnknownCommandEntityCount,
    int UnknownCommandCount,
    IReadOnlyList<int> UnknownTypeIds,
    string CaptureMethod,
    string StatusReason);

internal sealed class ProxyGraphicsCommandCaptureState
{
    private readonly Dictionary<ulong, CadProxyGraphicsCommandInventory> _profiles;

    private ProxyGraphicsCommandCaptureState(
        bool supported,
        bool captureFailed,
        Dictionary<ulong, CadProxyGraphicsCommandInventory>? profiles,
        string statusReason)
    {
        Supported = supported;
        CaptureFailed = captureFailed;
        _profiles = profiles ?? new();
        StatusReason = statusReason;
    }

    public bool Supported { get; }
    public bool CaptureFailed { get; }
    public string StatusReason { get; }

    public static ProxyGraphicsCommandCaptureState Success(Dictionary<ulong, CadProxyGraphicsCommandInventory> profiles)
        => new(true, false, profiles, string.Empty);

    public static ProxyGraphicsCommandCaptureState Failed(string reason)
        => new(true, true, null, reason);

    public static ProxyGraphicsCommandCaptureState Unsupported(string reason)
        => new(false, false, null, reason);

    public CadProxyGraphicsCommandInventory? Find(string handle)
    {
        if (!Supported || CaptureFailed || !TryHandle(handle, out var numericHandle)) return null;
        return _profiles.TryGetValue(numericHandle, out var profile) ? profile : null;
    }

    public ProxyGraphicsCommandCaptureSnapshot Snapshot()
    {
        var profiles = _profiles.Values.ToArray();
        var unknownTypeIds = profiles
            .SelectMany(profile => profile.UnknownTypeIds)
            .Distinct()
            .OrderBy(typeId => typeId)
            .ToArray();
        return new(
            Supported,
            CaptureFailed,
            profiles.Length,
            profiles.Count(profile => profile.IsMalformed || profile.IsTruncated),
            profiles.Count(profile => profile.UnknownCommandCount > 0),
            profiles.Sum(profile => profile.UnknownCommandCount),
            unknownTypeIds,
            ACadSharpProxyGraphicsCommandCapture.CaptureMethod,
            StatusReason);
    }

    private static bool TryHandle(string handle, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(handle)) return false;
        var trimmed = handle.Trim();
        return ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            || ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}

internal static class ACadSharpProxyGraphicsCommandCapture
{
    internal const string CaptureMethod = "ACadSharp-3.7.1-reflection-template-proxy-stream-v1";
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

    public static ProxyGraphicsCommandCaptureState Capture(DwgReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        try
        {
            var builderField = FindField(reader.GetType(), "_builder");
            if (builderField?.GetValue(reader) is not { } builder)
                return ProxyGraphicsCommandCaptureState.Failed("ACadSharp DWG builder was not available after read.");

            var templatesField = FindField(builder.GetType(), "cadObjectsTemplates");
            if (templatesField?.GetValue(builder) is not IDictionary templates)
                return ProxyGraphicsCommandCaptureState.Failed("ACadSharp entity-template map was not available.");

            var profiles = new Dictionary<ulong, CadProxyGraphicsCommandInventory>();
            foreach (DictionaryEntry entry in templates)
            {
                if (entry.Key is not ulong handle || entry.Value is null) continue;
                var proxyProperty = entry.Value.GetType().GetProperty("ProxyGraphics", PublicInstance);
                if (proxyProperty?.GetValue(entry.Value) is not byte[] bytes || bytes.Length == 0) continue;
                profiles[handle] = CadProxyGraphicsCommandScanner.Scan(bytes);
            }
            return ProxyGraphicsCommandCaptureState.Success(profiles);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return ProxyGraphicsCommandCaptureState.Failed($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static FieldInfo? FindField(Type? type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(name, PrivateInstance);
            if (field is not null) return field;
        }
        return null;
    }
}
