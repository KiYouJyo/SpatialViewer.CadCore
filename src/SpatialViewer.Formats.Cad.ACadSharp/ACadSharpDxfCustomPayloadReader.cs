using System.Globalization;
using System.Text;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

internal sealed record DxfCustomPayloadScanResult(
    IReadOnlyDictionary<string, CadDxfCustomPayload> Payloads,
    int CapturedRecordCount,
    int TruncatedRecordCount,
    bool IsBinaryDxf,
    bool ScanFailed = false)
{
    public static DxfCustomPayloadScanResult Empty { get; } = new(
        new Dictionary<string, CadDxfCustomPayload>(StringComparer.OrdinalIgnoreCase),
        0,
        0,
        false);
}

/// <summary>
/// Preserves raw group-code evidence for application-defined text-DXF entities before ACadSharp's
/// UnknownEntity path discards proprietary fields. Values are read using Latin-1 as a one-byte-to-one-char
/// projection, making the original value-line bytes reconstructable without guessing the source code page.
/// </summary>
internal static class ACadSharpDxfCustomPayloadReader
{
    private const int MaxGroupsPerEntity = 65_536;
    private const int MaxProjectedCharactersPerEntity = 8 * 1024 * 1024;
    private static readonly byte[] BinaryDxfPrefix = Encoding.ASCII.GetBytes("AutoCAD Binary DXF");

    public static DxfCustomPayloadScanResult Scan(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!string.Equals(Path.GetExtension(filePath), ".dxf", StringComparison.OrdinalIgnoreCase))
            return DxfCustomPayloadScanResult.Empty;

        try
        {
            if (IsBinaryDxf(filePath)) return DxfCustomPayloadScanResult.Empty with { IsBinaryDxf = true };

            var knownClassNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var payloads = new Dictionary<string, CadDxfCustomPayload>(StringComparer.OrdinalIgnoreCase);
            var capturedRecords = 0;
            var truncatedRecords = 0;
            string? currentSection = null;
            var awaitingSectionName = false;
            var inClassRecord = false;
            PayloadBuilder? current = null;

            using var reader = new StreamReader(filePath, Encoding.Latin1, detectEncodingFromByteOrderMarks: false);
            while (true)
            {
                var codeLine = reader.ReadLine();
                if (codeLine is null) break;
                var valueLine = reader.ReadLine();
                if (valueLine is null) break;
                if (!int.TryParse(codeLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)) continue;

                if (code == 0)
                {
                    FlushCurrent();
                    var token = valueLine.Trim();
                    if (string.Equals(token, "SECTION", StringComparison.OrdinalIgnoreCase))
                    {
                        currentSection = null;
                        awaitingSectionName = true;
                        inClassRecord = false;
                        continue;
                    }

                    if (string.Equals(token, "ENDSEC", StringComparison.OrdinalIgnoreCase))
                    {
                        currentSection = null;
                        awaitingSectionName = false;
                        inClassRecord = false;
                        continue;
                    }

                    inClassRecord = string.Equals(currentSection, "CLASSES", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(token, "CLASS", StringComparison.OrdinalIgnoreCase);

                    if (IsEntitySection(currentSection) && IsCandidateCustomEntity(token, knownClassNames))
                    {
                        current = new PayloadBuilder(token);
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

                if (inClassRecord && code == 1 && !string.IsNullOrWhiteSpace(valueLine))
                {
                    knownClassNames.Add(valueLine.Trim());
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

                current = null;
            }
        }
        catch
        {
            return DxfCustomPayloadScanResult.Empty with { ScanFailed = true };
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
            if (Groups.Count >= MaxGroupsPerEntity || _projectedCharacters + rawValue.Length > MaxProjectedCharactersPerEntity)
            {
                IsTruncated = true;
                return;
            }

            Groups.Add(new CadRawDxfGroup(code, rawValue));
            _projectedCharacters += rawValue.Length;
        }
    }
}
