using System.Globalization;
using System.Reflection;
using ACadSharp;
using ACadSharp.IO;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

internal sealed record DwgRawObjectCaptureSnapshot(
    bool Supported,
    bool CaptureFailed,
    bool BudgetExhausted,
    int CapturedRecordCount,
    int TruncatedRecordCount,
    long CapturedByteCount,
    string CaptureMethod,
    string StatusReason);

/// <summary>
/// Lazily copies bounded custom-object records from the already-decompressed modern DWG AcDbObjects section.
/// This deliberately retains the complete DWG object record rather than pretending the proprietary Databits region
/// has already been separated from common entity framing or the handle stream.
/// </summary>
internal sealed class DwgRawObjectCaptureState
{
    private const int MaxObjectRecordBytes = 8 * 1024 * 1024;
    private const int MaxTotalCaptureBytes = 128 * 1024 * 1024;
    private readonly Dictionary<ulong, CadDwgCustomObjectRecord?> _cache = new();
    private readonly Dictionary<ulong, long>? _handleOffsets;
    private readonly long[] _orderedOffsets;
    private readonly Stream? _objectStream;
    private long _capturedBytes;
    private int _truncatedRecords;

    internal DwgRawObjectCaptureState(
        Stream objectStream,
        Dictionary<ulong, long> handleOffsets,
        string captureMethod)
    {
        _objectStream = objectStream;
        _handleOffsets = handleOffsets;
        _orderedOffsets = handleOffsets.Values
            .Where(offset => offset >= 0)
            .Distinct()
            .OrderBy(offset => offset)
            .ToArray();
        CaptureMethod = captureMethod;
        Supported = true;
        StatusReason = string.Empty;
    }

    private DwgRawObjectCaptureState(bool supported, bool captureFailed, string captureMethod, string statusReason)
    {
        _orderedOffsets = Array.Empty<long>();
        Supported = supported;
        CaptureFailed = captureFailed;
        CaptureMethod = captureMethod;
        StatusReason = statusReason;
    }

    public bool Supported { get; }
    public bool CaptureFailed { get; }
    public bool BudgetExhausted { get; private set; }
    public string CaptureMethod { get; }
    public string StatusReason { get; }

    public static DwgRawObjectCaptureState Unsupported(string reason)
        => new(false, false, ACadSharpDwgRawObjectReader.CaptureMethod, reason);

    public static DwgRawObjectCaptureState Failed(string reason)
        => new(true, true, ACadSharpDwgRawObjectReader.CaptureMethod, reason);

    public CadDwgCustomObjectRecord? Find(string handle)
    {
        if (!Supported || CaptureFailed || _objectStream is null || _handleOffsets is null) return null;
        if (!TryHandle(handle, out var numericHandle)) return null;
        if (_cache.TryGetValue(numericHandle, out var cached)) return cached;
        if (!_handleOffsets.TryGetValue(numericHandle, out var start) || start < 0 || start >= _objectStream.Length)
        {
            _cache[numericHandle] = null;
            return null;
        }

        var index = Array.BinarySearch(_orderedOffsets, start);
        if (index < 0)
        {
            _cache[numericHandle] = null;
            return null;
        }

        long end = _objectStream.Length;
        for (var next = index + 1; next < _orderedOffsets.Length; next++)
        {
            if (_orderedOffsets[next] > start)
            {
                end = Math.Min(_orderedOffsets[next], _objectStream.Length);
                break;
            }
        }

        var recordLength = end - start;
        if (recordLength <= 0)
        {
            _cache[numericHandle] = null;
            return null;
        }

        var remainingBudget = MaxTotalCaptureBytes - _capturedBytes;
        if (remainingBudget <= 0)
        {
            BudgetExhausted = true;
            _cache[numericHandle] = null;
            return null;
        }

        var bytesToCopy = (int)Math.Min(recordLength, Math.Min(MaxObjectRecordBytes, remainingBudget));
        if (bytesToCopy <= 0)
        {
            BudgetExhausted = true;
            _cache[numericHandle] = null;
            return null;
        }

        var bytes = new byte[bytesToCopy];
        lock (_objectStream)
        {
            _objectStream.Position = start;
            _objectStream.ReadExactly(bytes);
        }

        var truncated = bytesToCopy < recordLength;
        if (truncated) _truncatedRecords++;
        _capturedBytes += bytesToCopy;
        if (_capturedBytes >= MaxTotalCaptureBytes) BudgetExhausted = true;
        var payload = new CadDwgCustomObjectRecord(bytes, start, truncated, CaptureMethod);
        _cache[numericHandle] = payload;
        return payload;
    }

    public DwgRawObjectCaptureSnapshot Snapshot()
        => new(
            Supported,
            CaptureFailed,
            BudgetExhausted,
            _cache.Count(pair => pair.Value is not null),
            _truncatedRecords,
            _capturedBytes,
            CaptureMethod,
            StatusReason);

    private static bool TryHandle(string handle, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(handle)) return false;
        var trimmed = handle.Trim();
        return ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            || ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}

internal static class ACadSharpDwgRawObjectReader
{
    internal const string CaptureMethod = "ACadSharp-3.7.1-reflection-object-section-v1";
    private const string ObjectsSectionName = "AcDb:AcDbObjects";
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static DwgRawObjectCaptureState Initialize(DwgReader reader, global::ACadSharp.CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(document);
        if (document.Header.Version <= ACadVersion.AC1015)
            return DwgRawObjectCaptureState.Unsupported("DWG object-record evidence currently supports AC1018/R2004 and newer section-relative handle maps only.");

        try
        {
            var readerType = typeof(DwgReader);
            var readHandles = readerType.GetMethod("readHandles", PrivateInstance);
            var getSectionStream = readerType.GetMethod("getSectionStream", PrivateInstance, null, new[] { typeof(string) }, null);
            if (readHandles is null || getSectionStream is null)
                return DwgRawObjectCaptureState.Failed("Required ACadSharp DWG section hooks were not found.");

            if (readHandles.Invoke(reader, null) is not Dictionary<ulong, long> handles)
                return DwgRawObjectCaptureState.Failed("ACadSharp handle-map hook returned an unexpected type.");
            var sectionReader = getSectionStream.Invoke(reader, new object[] { ObjectsSectionName });
            if (sectionReader is null)
                return DwgRawObjectCaptureState.Failed("ACadSharp did not expose the decompressed AcDbObjects section.");

            var streamProperty = sectionReader.GetType().GetProperty("Stream", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (streamProperty?.GetValue(sectionReader) is not Stream stream || !stream.CanRead || !stream.CanSeek)
                return DwgRawObjectCaptureState.Failed("ACadSharp AcDbObjects section stream is not seekable/readable.");

            return new DwgRawObjectCaptureState(stream, handles, CaptureMethod);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return DwgRawObjectCaptureState.Failed($"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
