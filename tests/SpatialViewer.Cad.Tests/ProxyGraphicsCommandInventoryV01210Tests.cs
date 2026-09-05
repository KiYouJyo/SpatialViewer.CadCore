using System.Buffers.Binary;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyGraphicsCommandInventoryV01210Tests
{
    private static readonly int[] UnknownType51 = [51];
    private static readonly int[] UnknownGapTypes = [15, 17, 21];
    [Fact]
    public void ScannerRetainsOnlyStructuralCommandFraming()
    {
        var bytes = Stream(
            (7, new byte[] { 1, 2, 3, 4 }),
            (51, new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 }),
            (7, new byte[] { 0, 0, 0, 0 }));

        var inventory = CadProxyGraphicsCommandScanner.Scan(bytes);

        Assert.Equal(3, inventory.DeclaredCommandCount);
        Assert.Equal(3, inventory.ScannedCommandCount);
        Assert.False(inventory.IsMalformed);
        Assert.False(inventory.IsTruncated);
        Assert.Equal(2, inventory.KnownCommandCount);
        Assert.Equal(1, inventory.UnknownCommandCount);
        Assert.Equal(UnknownType51, inventory.UnknownTypeIds);
        Assert.Equal("7@12x2;51@16x1", inventory.TypeSignature);
        Assert.Collection(
            inventory.Commands,
            command =>
            {
                Assert.Equal(7, command.TypeId);
                Assert.Equal(12, command.RecordSize);
                Assert.Equal(1, command.SequenceIndex);
                Assert.Equal(1, command.TypeOccurrence);
                Assert.True(command.KnownByAcAdSharp);
            },
            command =>
            {
                Assert.Equal(51, command.TypeId);
                Assert.Equal(16, command.RecordSize);
                Assert.Equal(2, command.SequenceIndex);
                Assert.Equal(1, command.TypeOccurrence);
                Assert.False(command.KnownByAcAdSharp);
            },
            command =>
            {
                Assert.Equal(7, command.TypeId);
                Assert.Equal(2, command.TypeOccurrence);
            });
    }

    [Fact]
    public void ScannerDoesNotRetainPayloadBytes()
    {
        const byte privateValue = 0xE7;
        var bytes = Stream((51, Enumerable.Repeat(privateValue, 24).ToArray()));

        var inventory = CadProxyGraphicsCommandScanner.Scan(bytes);
        var json = System.Text.Json.JsonSerializer.Serialize(inventory);

        Assert.DoesNotContain(privateValue.ToString(System.Globalization.CultureInfo.InvariantCulture), json, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(Enumerable.Repeat(privateValue, 24).ToArray()), json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("51@32x1", inventory.TypeSignature);
    }

    [Fact]
    public void ScannerFailsClosedOnTruncatedRecord()
    {
        var bytes = Stream((51, new byte[] { 1, 2, 3, 4 }));
        Array.Resize(ref bytes, bytes.Length - 2);

        var inventory = CadProxyGraphicsCommandScanner.Scan(bytes);

        Assert.True(inventory.IsTruncated);
        Assert.False(inventory.IsMalformed);
        Assert.Equal(0, inventory.ScannedCommandCount);
        Assert.Empty(inventory.Commands);
    }

    [Fact]
    public void ScannerFailsClosedOnInvalidRecordSize()
    {
        var bytes = Stream((7, Array.Empty<byte>()));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), 4);

        var inventory = CadProxyGraphicsCommandScanner.Scan(bytes);

        Assert.True(inventory.IsMalformed);
        Assert.False(inventory.IsTruncated);
        Assert.Empty(inventory.Commands);
    }

    [Fact]
    public void ScannerTreatsEnumGapsAsUnknownCommands()
    {
        var bytes = Stream(
            (15, Array.Empty<byte>()),
            (17, Array.Empty<byte>()),
            (21, Array.Empty<byte>()),
            (37, Array.Empty<byte>()),
            (38, Array.Empty<byte>()));

        var inventory = CadProxyGraphicsCommandScanner.Scan(bytes);

        Assert.Equal(UnknownGapTypes, inventory.UnknownTypeIds);
        Assert.Equal(3, inventory.UnknownCommandCount);
        Assert.Equal(2, inventory.KnownCommandCount);
    }

    private static byte[] Stream(params (int TypeId, byte[] Payload)[] commands)
    {
        var length = 8 + commands.Sum(command => 8 + command.Payload.Length);
        var bytes = new byte[length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), commands.Length);
        var offset = 8;
        foreach (var command in commands)
        {
            var recordSize = 8 + command.Payload.Length;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), recordSize);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 4, 4), command.TypeId);
            command.Payload.CopyTo(bytes, offset + 8);
            offset += recordSize;
        }
        return bytes;
    }
}
