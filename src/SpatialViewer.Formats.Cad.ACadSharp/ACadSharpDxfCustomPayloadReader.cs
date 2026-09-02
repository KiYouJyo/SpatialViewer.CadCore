using System.Globalization;
using System.Text;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

internal sealed record DxfCustomPayloadScanResult(
    IReadOnlyDictionary<string, CadDxfCustomPayload> Payloads,
    int CapturedRecordCount,
    int TruncatedRecordCount,
    bool IsBinaryDxf)
{
    public static DxfCustomPayloadScanResult Empty { get; } = new(
        new Dictionary<string, CadDxfCustomPayload>(StringComparer.OrdinalIgnoreCase),
        0,
        0,
        false);
}

/// <summary>
/// Preserves raw group-code evidence for application-defined ASCII DXF entities before ACadSharp's
/// UnknownEntity path discards proprietary fields. Values are read using Latin-1 as a one-byte-to-one-char
/// projection, making the original value bytes reconstructable without guessing the source code page.
/// </summary>
internal static class ACadSharpDxfCustomPayloadReader
{
    private const int MaxGroupsPerEntity = 65_536;
    private const int MaxProjectedCharactersPerEntity = 8 * 1024 * 1024;
    private static readonly byte[] BinaryDxfPrefix = Encoding.ASCII.GetBytes("AutoCAD Binary DXF");

    public static DxfCustomPayloadScanResult Scan(
        string filePath,
        IReadOnlyList<CadCustomClassDefinition> customClasses,
        List<Diagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(customClasses);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (!string.Equals(Path.GetExtension(filePath), ".dxf", StringComparison.OrdinalIgnoreCase))
            return DxfCustomPayloadScanResult.Empty;

        try
        {
            if (IsBinaryDxf(filePath))
            {
                if (customClasses.Any(definition => definition.IsEntity))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Warning,
                        "CAD_CUSTOM_RAW_DXF_BINARY_UNAVAILABLE",
                        "Application-defined DXF classes were detected, but raw custom payload capture currently supports text DXF only. Class identity and any reader-provided Proxy Graphics remain preserved."));
                }

                return DxfCustomPayloadScanResult.Empty with { IsBinaryDxf = true };
            }

            var knownClassNames = new HashSet<string>(
                customClasses
                    .Where(definition => definition.IsEntity && !string.IsNullOrWhiteSpace(definition.DxfName))
                    .Select(definition => definition.DxfName),
                StringComparer.OrdinalIgnoreCase);
            var payloads = new Dictionary<string, CadDxfCustomPayload>(StringComparer.OrdinalIgnoreCase);
            var capturedRecords = 0;
            var truncatedRecords = 0;
            string? currentSection = null;
            var awaitingSectionName = false;
            PayloadBuilder? current = null;

            using var reader = new StreamReader(filePath, Encoding.Latin1, detectEncodingFromByteOrderMarks: false);
            while (true)
            {
                var codeLine = reader.ReadLine();
                if (codeLine is null) break;
                var valueLine = reader.ReadLine();
                if (valueLine is null)
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Warning,
                        "CAD_CUSTOM_RAW_DXF_ODD_LINE_COUNT",
                        "Raw custom-payload scan stopped because the text DXF ended between a group code and value line."));
                    break;
                }

                if (!int.TryParse(codeLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
                    continue;

                if (code == 0)
                {
                    FlushCurrent();
                    if (string.Equals(valueLine.Trim(), "SECTION", StringComparison.OrdinalIgnoreCase))
                    {
                        currentSection = null;
                        awaitingSectionName = true;
                        continue;
                    }

                    if (string.Equals(valueLine.Trim(), "ENDSEC", StringComparison.OrdinalIgnoreCase))
                    {
                        currentSection = null;
                        awaitingSectionName = false;
                        continue;
                    }

                    if (IsEntitySection(currentSection) && IsCandidateCustomEntity(valueLine.Trim(), knownClassNames))
                    {
                        current = new PayloadBuilder(valueLine.Trim());
                        current.Add(code, valueLine);
                    }

                    continue;
                }

                if (awaitingSectionName && code == 2)
                {
                    currentSection = valueLine.Trim();
                    awaitingSectionName = false;
                    continue;
                }

                current?.Add(code, valueLine);
            }

            FlushCurrent();
            return new DxfCustomPayloadScanResult(payloads, capturedRecords, truncatedRecords, false);

            void FlushCurrent()
            {
                if (current is null) return;
                if (!string.IsNullOrWhiteSpace(current.Handle))
                {
                    payloads[current.Handle] = new CadDxfCustomPayload(current.Groups.ToArray(), current.IsTruncated);
                    capturedRecords++;
                    if (current.IsTruncated) truncatedRecords++;
                }
                else
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Warning,
                        "CAD_CUSTOM_RAW_DXF_HANDLE_MISSING",
                        $"A custom DXF entity payload could not be attached because no handle (group 5) was present: {current.EntityType}"));
                }

                current = null;
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "CAD_CUSTOM_RAW_DXF_SCAN_FAILED",
                $"Raw custom DXF payload capture failed; normal CAD import will continue: {exception.Message}"));
            return DxfCustomPayloadScanResult.Empty;
        }
    }

    private static bool IsCandidateCustomEntity(string entityType, IReadOnlySet<string> knownClassNames)
        => knownClassNames.Contains(entityType) || CadCustomObjectClassifier.IsTianzheng(entityType);

    private static bool IsEntitySection(string? section)
        => string.Equals(section, "ENTITIES", StringComparison.OrdinalIgnoreCase)
            || string.Equals(section, "BLOCKS", StringComparison.OrdinalIgnoreCase);

    private static bool IsBinaryDxf(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        Span<byte> prefix = stackalloc byte[BinaryDxfPrefix.Length];
        var read = stream.Read(prefix);
        return read == BinaryDxfPrefix.Length && prefix.SequenceEqual(BinaryDxfPrefix);
    }

    private sealed class PayloadBuilder
    {
        private int _projectedCharacters;

        public PayloadBuilder(string entityType) => EntityType = entityType;

        public string EntityType { get; }
        public string Handle { get; private set; } = string.Empty;
        public List<CadRawDxfGroup> Groups { get; } = new();
        public bool IsTruncated { get; private set; }

        public void Add(int code, string rawValue)
        {
            if (code == 5 && string.IsNullOrWhiteSpace(Handle)) Handle = rawValue.Trim();
            if (IsTruncated) return;
            var projectedLength = rawValue.Length;
            if (Groups.Count >= MaxGroupsPerEntity || _projectedCharacters + projectedLength > MaxProjectedCharactersPerEntity)
            {
                IsTruncated = true;
                return;
            }

            Groups.Add(new CadRawDxfGroup(code, rawValue));
            _projectedCharacters += projectedLength;
        }
    }
}
