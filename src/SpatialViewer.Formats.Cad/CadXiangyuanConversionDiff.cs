using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace SpatialViewer.Formats.Cad;

public enum CadXiangyuanConversionDiffStatus
{
    RemovedAfterConversion,
    RetainedAfterConversion,
    AddedAfterConversion
}

/// <summary>
/// CLASSES-table identity delta between one native known-Xiangyuan drawing and its controlled
/// "all explode / result output" conversion. A removed class is a research candidate only;
/// conversion disappearance does not by itself prove native Xiangyuan ownership or parcel semantics.
/// </summary>
public sealed record CadXiangyuanConversionClassDelta(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    CadCustomObjectVendor ClassifiedVendor,
    bool IsEntity,
    bool WasProxy,
    string ProxyFlags,
    bool PresentInNative,
    bool PresentInConverted,
    int NativeDeclaredInstanceCount,
    int ConvertedDeclaredInstanceCount,
    CadXiangyuanConversionDiffStatus Status);

/// <summary>Structural custom-entity profile delta without raw drawing values.</summary>
public sealed record CadXiangyuanConversionProfileDelta(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    CadCustomObjectVendor ClassifiedVendor,
    string SchemaFingerprint,
    string GroupCodeSignature,
    string SubclassMarkerSignature,
    string ReferenceCodeSignature,
    string ProxyGraphicKindSignature,
    int NativeEntityCount,
    int ConvertedEntityCount,
    CadXiangyuanConversionDiffStatus Status);

/// <summary>Privacy-safe controlled conversion diff for exactly one native/converted drawing pair.</summary>
public sealed record CadXiangyuanConversionDiffReport(
    int SchemaVersion,
    int NativeSampleCount,
    int ConvertedSampleCount,
    IReadOnlyList<CadXiangyuanConversionClassDelta> Classes,
    IReadOnlyList<CadXiangyuanConversionProfileDelta> Profiles)
{
    public int RemovedClassCount => Classes.Count(item => item.Status == CadXiangyuanConversionDiffStatus.RemovedAfterConversion);
    public int RemovedProfileCount => Profiles.Count(item => item.Status == CadXiangyuanConversionDiffStatus.RemovedAfterConversion);
    public int RetainedClassCount => Classes.Count(item => item.Status == CadXiangyuanConversionDiffStatus.RetainedAfterConversion);
    public int RetainedProfileCount => Profiles.Count(item => item.Status == CadXiangyuanConversionDiffStatus.RetainedAfterConversion);
}

public static class CadXiangyuanConversionDiffer
{
    public const int CurrentSchemaVersion = 1;
    private const int MaxJsonBytes = 16 * 1024 * 1024;
    private const int MaxEntries = 100_000;
    private const int MaxIdentityLength = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static CadXiangyuanConversionDiffReport Compare(
        CadXiangyuanDiscoveryReport native,
        CadXiangyuanDiscoveryReport converted)
    {
        ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(converted);
        if (native.SampleCount != 1 || converted.SampleCount != 1)
            throw new ArgumentException("Xiangyuan conversion diff requires exactly one native and one converted sample.");

        var nativeClasses = native.Classes.ToDictionary(ClassKey.Create);
        var convertedClasses = converted.Classes.ToDictionary(ClassKey.Create);
        var classKeys = nativeClasses.Keys.Concat(convertedClasses.Keys).Distinct().ToArray();
        var classes = classKeys
            .Select(key => CreateClassDelta(
                key,
                nativeClasses.GetValueOrDefault(key),
                convertedClasses.GetValueOrDefault(key)))
            .OrderBy(item => item.Status)
            .ThenBy(item => item.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CppClassName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var nativeProfiles = native.Profiles.ToDictionary(ProfileKey.Create);
        var convertedProfiles = converted.Profiles.ToDictionary(ProfileKey.Create);
        var profileKeys = nativeProfiles.Keys.Concat(convertedProfiles.Keys).Distinct().ToArray();
        var profiles = profileKeys
            .Select(key => CreateProfileDelta(
                key,
                nativeProfiles.GetValueOrDefault(key),
                convertedProfiles.GetValueOrDefault(key)))
            .OrderBy(item => item.Status)
            .ThenBy(item => item.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SchemaFingerprint, StringComparer.Ordinal)
            .ToArray();

        var report = new CadXiangyuanConversionDiffReport(
            CurrentSchemaVersion,
            native.SampleCount,
            converted.SampleCount,
            new ReadOnlyCollection<CadXiangyuanConversionClassDelta>(classes),
            new ReadOnlyCollection<CadXiangyuanConversionProfileDelta>(profiles));
        ValidateReport(report, nameof(native));
        return report;
    }

    public static string ToJson(CadXiangyuanConversionDiffReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ValidateReport(report, nameof(report));
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public static CadXiangyuanConversionDiffReport FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new FormatException($"Xiangyuan conversion-diff JSON exceeds the {MaxJsonBytes} byte safety limit.");
        try
        {
            var report = JsonSerializer.Deserialize<CadXiangyuanConversionDiffReport>(json, JsonOptions)
                ?? throw new FormatException("Xiangyuan conversion-diff JSON did not contain a report.");
            ValidateReport(report, nameof(json));
            return new CadXiangyuanConversionDiffReport(
                report.SchemaVersion,
                report.NativeSampleCount,
                report.ConvertedSampleCount,
                new ReadOnlyCollection<CadXiangyuanConversionClassDelta>(report.Classes.ToArray()),
                new ReadOnlyCollection<CadXiangyuanConversionProfileDelta>(report.Profiles.ToArray()));
        }
        catch (JsonException exception)
        {
            throw new FormatException("Invalid Xiangyuan conversion-diff JSON.", exception);
        }
    }

    private static CadXiangyuanConversionClassDelta CreateClassDelta(
        ClassKey key,
        CadXiangyuanDiscoveryClassEntry? native,
        CadXiangyuanDiscoveryClassEntry? converted)
    {
        var nativeCount = native?.DeclaredInstanceCount ?? 0;
        var convertedCount = converted?.DeclaredInstanceCount ?? 0;
        return new CadXiangyuanConversionClassDelta(
            key.DxfName,
            key.CppClassName,
            key.ApplicationName,
            key.ClassifiedVendor,
            key.IsEntity,
            key.WasProxy,
            key.ProxyFlags,
            native is not null,
            converted is not null,
            nativeCount,
            convertedCount,
            Status(native is not null, converted is not null));
    }

    private static CadXiangyuanConversionProfileDelta CreateProfileDelta(
        ProfileKey key,
        CadXiangyuanDiscoveryProfileEntry? native,
        CadXiangyuanDiscoveryProfileEntry? converted)
        => new(
            key.DxfName,
            key.CppClassName,
            key.ApplicationName,
            key.ClassifiedVendor,
            key.SchemaFingerprint,
            key.GroupCodeSignature,
            key.SubclassMarkerSignature,
            key.ReferenceCodeSignature,
            key.ProxyGraphicKindSignature,
            native?.EntityCount ?? 0,
            converted?.EntityCount ?? 0,
            Status(native is not null, converted is not null));

    private static CadXiangyuanConversionDiffStatus Status(bool native, bool converted)
        => (native, converted) switch
        {
            (true, false) => CadXiangyuanConversionDiffStatus.RemovedAfterConversion,
            (true, true) => CadXiangyuanConversionDiffStatus.RetainedAfterConversion,
            (false, true) => CadXiangyuanConversionDiffStatus.AddedAfterConversion,
            _ => throw new InvalidOperationException("A conversion diff entry must exist in at least one side.")
        };

    internal static void ValidateReport(CadXiangyuanConversionDiffReport report, string parameterName)
    {
        if (report.SchemaVersion != CurrentSchemaVersion)
            throw new ArgumentException($"Unsupported Xiangyuan conversion-diff version: {report.SchemaVersion}.", parameterName);
        if (report.NativeSampleCount != 1 || report.ConvertedSampleCount != 1)
            throw new ArgumentException("Xiangyuan conversion diff must represent exactly one native and one converted sample.", parameterName);
        if (report.Classes is null || report.Profiles is null)
            throw new ArgumentException("Xiangyuan conversion diff classes/profiles cannot be null.", parameterName);
        if (report.Classes.Count > MaxEntries || report.Profiles.Count > MaxEntries)
            throw new ArgumentException($"Xiangyuan conversion diff contains more than {MaxEntries} entries.", parameterName);

        foreach (var item in report.Classes)
        {
            ValidateIdentity(item.DxfName, nameof(item.DxfName), parameterName, true);
            ValidateIdentity(item.CppClassName, nameof(item.CppClassName), parameterName, false);
            ValidateIdentity(item.ApplicationName, nameof(item.ApplicationName), parameterName, false);
            ValidateIdentity(item.ProxyFlags, nameof(item.ProxyFlags), parameterName, false);
            ValidateVendor(item.ClassifiedVendor, parameterName);
            ValidateClassStatus(
                item.Status,
                item.PresentInNative,
                item.PresentInConverted,
                item.NativeDeclaredInstanceCount,
                item.ConvertedDeclaredInstanceCount,
                parameterName);
        }
        foreach (var item in report.Profiles)
        {
            ValidateIdentity(item.DxfName, nameof(item.DxfName), parameterName, true);
            ValidateIdentity(item.CppClassName, nameof(item.CppClassName), parameterName, false);
            ValidateIdentity(item.ApplicationName, nameof(item.ApplicationName), parameterName, false);
            ValidateIdentity(item.SchemaFingerprint, nameof(item.SchemaFingerprint), parameterName, true);
            ValidateIdentity(item.GroupCodeSignature, nameof(item.GroupCodeSignature), parameterName, true);
            ValidateIdentity(item.SubclassMarkerSignature, nameof(item.SubclassMarkerSignature), parameterName, true);
            ValidateIdentity(item.ReferenceCodeSignature, nameof(item.ReferenceCodeSignature), parameterName, false);
            ValidateIdentity(item.ProxyGraphicKindSignature, nameof(item.ProxyGraphicKindSignature), parameterName, true);
            ValidateVendor(item.ClassifiedVendor, parameterName);
            ValidateProfileStatus(item.Status, item.NativeEntityCount, item.ConvertedEntityCount, parameterName);
        }
    }

    private static void ValidateClassStatus(
        CadXiangyuanConversionDiffStatus status,
        bool presentInNative,
        bool presentInConverted,
        int nativeCount,
        int convertedCount,
        string parameterName)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentException($"Unsupported Xiangyuan conversion-diff status: {(int)status}.", parameterName);
        if (nativeCount < 0 || convertedCount < 0)
            throw new ArgumentException("Xiangyuan conversion-diff class instance counts cannot be negative.", parameterName);
        if (!presentInNative && nativeCount != 0)
            throw new ArgumentException("A class absent from the native report cannot have native instances.", parameterName);
        if (!presentInConverted && convertedCount != 0)
            throw new ArgumentException("A class absent from the converted report cannot have converted instances.", parameterName);
        var consistent = status switch
        {
            CadXiangyuanConversionDiffStatus.RemovedAfterConversion => presentInNative && !presentInConverted,
            CadXiangyuanConversionDiffStatus.RetainedAfterConversion => presentInNative && presentInConverted,
            CadXiangyuanConversionDiffStatus.AddedAfterConversion => !presentInNative && presentInConverted,
            _ => false
        };
        if (!consistent)
            throw new ArgumentException("Xiangyuan conversion-diff class status is inconsistent with native/converted presence.", parameterName);
    }

    private static void ValidateProfileStatus(
        CadXiangyuanConversionDiffStatus status,
        int nativeCount,
        int convertedCount,
        string parameterName)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentException($"Unsupported Xiangyuan conversion-diff status: {(int)status}.", parameterName);
        if (nativeCount < 0 || convertedCount < 0 || nativeCount + convertedCount <= 0)
            throw new ArgumentException("Xiangyuan conversion-diff profile counts must be non-negative with at least one non-zero side.", parameterName);
        var consistent = status switch
        {
            CadXiangyuanConversionDiffStatus.RemovedAfterConversion => nativeCount > 0 && convertedCount == 0,
            CadXiangyuanConversionDiffStatus.RetainedAfterConversion => nativeCount > 0 && convertedCount > 0,
            CadXiangyuanConversionDiffStatus.AddedAfterConversion => nativeCount == 0 && convertedCount > 0,
            _ => false
        };
        if (!consistent)
            throw new ArgumentException("Xiangyuan conversion-diff profile status is inconsistent with native/converted counts.", parameterName);
    }

    private static void ValidateIdentity(string? value, string name, string parameterName, bool required)
    {
        if (required && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Xiangyuan conversion-diff field {name} cannot be empty.", parameterName);
        if (value?.Length > MaxIdentityLength)
            throw new ArgumentException($"Xiangyuan conversion-diff field {name} exceeds {MaxIdentityLength} characters.", parameterName);
    }

    private static void ValidateVendor(CadCustomObjectVendor vendor, string parameterName)
    {
        if (!Enum.IsDefined(vendor))
            throw new ArgumentException($"Unsupported custom-object vendor value: {(int)vendor}.", parameterName);
    }

    private readonly record struct ClassKey(
        string DxfName,
        string CppClassName,
        string ApplicationName,
        CadCustomObjectVendor ClassifiedVendor,
        bool IsEntity,
        bool WasProxy,
        string ProxyFlags)
    {
        public static ClassKey Create(CadXiangyuanDiscoveryClassEntry item)
            => new(item.DxfName, item.CppClassName, item.ApplicationName, item.ClassifiedVendor, item.IsEntity, item.WasProxy, item.ProxyFlags);
    }

    private readonly record struct ProfileKey(
        string DxfName,
        string CppClassName,
        string ApplicationName,
        CadCustomObjectVendor ClassifiedVendor,
        string SchemaFingerprint,
        string GroupCodeSignature,
        string SubclassMarkerSignature,
        string ReferenceCodeSignature,
        string ProxyGraphicKindSignature)
    {
        public static ProfileKey Create(CadXiangyuanDiscoveryProfileEntry item)
            => new(
                item.DxfName,
                item.CppClassName,
                item.ApplicationName,
                item.ClassifiedVendor,
                item.SchemaFingerprint,
                item.GroupCodeSignature,
                item.SubclassMarkerSignature,
                item.ReferenceCodeSignature,
                item.ProxyGraphicKindSignature);
    }
}
