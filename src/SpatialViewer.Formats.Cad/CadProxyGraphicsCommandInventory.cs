using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// One structural record from the raw ObjectARX proxy-graphics stream.
/// No payload bytes or primitive values are retained.
/// </summary>
public sealed record CadProxyGraphicsCommandEntry(
    int TypeId,
    int RecordSize,
    int SequenceIndex,
    int TypeOccurrence,
    bool KnownByAcAdSharp);

/// <summary>
/// Privacy-safe structural inventory of one raw ObjectARX proxy-graphics stream.
/// </summary>
public sealed record CadProxyGraphicsCommandInventory(
    int DeclaredByteSize,
    int DeclaredCommandCount,
    int ScannedCommandCount,
    bool IsMalformed,
    bool IsTruncated,
    IReadOnlyList<CadProxyGraphicsCommandEntry> Commands)
{
    public int KnownCommandCount => Commands.Count(command => command.KnownByAcAdSharp);
    public int UnknownCommandCount => Commands.Count(command => !command.KnownByAcAdSharp);
    public IReadOnlyList<int> UnknownTypeIds => Commands
        .Where(command => !command.KnownByAcAdSharp)
        .Select(command => command.TypeId)
        .Distinct()
        .OrderBy(typeId => typeId)
        .ToArray();

    public string TypeSignature => string.Join(
        ';',
        Commands
            .GroupBy(command => new { command.TypeId, command.RecordSize })
            .OrderBy(group => group.Key.TypeId)
            .ThenBy(group => group.Key.RecordSize)
            .Select(group => string.Create(
                CultureInfo.InvariantCulture,
                $"{group.Key.TypeId}@{group.Key.RecordSize}x{group.Count()}")));
}

/// <summary>
/// Scans only proxy-graphics record framing: [declaredSize, count] followed by
/// [recordSize, typeId, payload...] records. Payload bytes are skipped, never retained.
/// </summary>
public static class CadProxyGraphicsCommandScanner
{
    private const int MaxProxyGraphicsBytes = 64 * 1024 * 1024;
    private const int MaxCommands = 1_000_000;

    // ACadSharp 3.7.1 GraphicsType enum values. Gaps are deliberately unknown.
    private static readonly HashSet<int> KnownTypeIds = new()
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14,
        16, 18, 19, 20, 22, 23, 24, 25, 26, 27, 28, 29,
        30, 31, 32, 33, 34, 35, 36, 37, 38
    };

    public static CadProxyGraphicsCommandInventory Scan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaxProxyGraphicsBytes)
            throw new ArgumentException($"Proxy-graphics stream exceeds the {MaxProxyGraphicsBytes} byte safety limit.", nameof(bytes));
        if (bytes.Length < 8)
            return EmptyMalformed(bytes.Length);

        var declaredSize = BinaryPrimitives.ReadInt32LittleEndian(bytes[..4]);
        var declaredCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4));
        if (declaredCount < 0 || declaredCount > MaxCommands)
            return new(declaredSize, declaredCount, 0, true, false, Array.Empty<CadProxyGraphicsCommandEntry>());

        var offset = 8;
        var malformed = false;
        var truncated = false;
        var occurrences = new Dictionary<int, int>();
        var commands = new List<CadProxyGraphicsCommandEntry>(Math.Min(declaredCount, 4096));

        for (var index = 0; index < declaredCount; index++)
        {
            if (offset > bytes.Length - 8)
            {
                truncated = true;
                break;
            }

            var recordSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
            var typeId = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4));
            if (recordSize < 8)
            {
                malformed = true;
                break;
            }
            if (recordSize > bytes.Length - offset)
            {
                truncated = true;
                break;
            }

            occurrences.TryGetValue(typeId, out var occurrence);
            occurrence++;
            occurrences[typeId] = occurrence;
            commands.Add(new(
                typeId,
                recordSize,
                index + 1,
                occurrence,
                KnownTypeIds.Contains(typeId)));
            offset += recordSize;
        }

        if (!malformed && !truncated && commands.Count != declaredCount)
            malformed = true;

        // The header size is observed evidence only. Different DWG writers have
        // historically interpreted it differently, so never fail solely on a mismatch.
        return new(
            declaredSize,
            declaredCount,
            commands.Count,
            malformed,
            truncated,
            new ReadOnlyCollection<CadProxyGraphicsCommandEntry>(commands));
    }

    private static CadProxyGraphicsCommandInventory EmptyMalformed(int actualBytes)
        => new(
            actualBytes,
            0,
            0,
            true,
            actualBytes > 0,
            Array.Empty<CadProxyGraphicsCommandEntry>());
}
