using System.Text;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

public enum CadShxFontType { Shapes, BigFont, UniFont }

public sealed record CadShxGlyph(IReadOnlyList<IReadOnlyList<Point2D>> Strokes, Point2D Advance, bool HasExplicitAdvance)
{
    public BoundingBox2D Bounds
    {
        get
        {
            var bounds = BoundingBox2D.Empty;
            foreach (var stroke in Strokes) bounds = bounds.Union(BoundingBox2D.FromPoints(stroke));
            return bounds;
        }
    }
}

public sealed record CadShxTextLayout(IReadOnlyList<IReadOnlyList<Point2D>> Strokes, double AdvanceWidth, int MissingGlyphCount)
{
    public bool Complete => MissingGlyphCount == 0;
    public BoundingBox2D Bounds
    {
        get
        {
            var bounds = BoundingBox2D.Empty;
            foreach (var stroke in Strokes) bounds = bounds.Union(BoundingBox2D.FromPoints(stroke));
            return bounds;
        }
    }
}

/// <summary>Reader-independent parser and vectorizer for compiled AutoCAD SHX fonts.</summary>
public sealed class CadShxFont
{
    private const int MaxSubshapeDepth = 16;
    private readonly Dictionary<int, byte[]> _glyphData;
    private readonly Dictionary<int, CadShxGlyph> _glyphCache = new();

    private CadShxFont(string fileName, string header, string version, CadShxFontType fontType, Dictionary<int, byte[]> glyphData, string info, double baseUp, double baseDown, bool dualOrientation)
    {
        FileName = fileName;
        Header = header;
        Version = version;
        FontType = fontType;
        _glyphData = glyphData;
        Info = info;
        BaseUp = Math.Max(1, baseUp);
        BaseDown = Math.Max(0, baseDown);
        DualOrientation = dualOrientation;
    }

    public string FileName { get; }
    public string Header { get; }
    public string Version { get; }
    public CadShxFontType FontType { get; }
    public string Info { get; }
    public double BaseUp { get; }
    public double BaseDown { get; }
    public double DesignHeight => Math.Max(1, BaseUp + BaseDown);
    public bool DualOrientation { get; }
    public bool IsPlainShapeFile => FontType == CadShxFontType.Shapes && !_glyphData.ContainsKey(0);
    public bool CanLayoutUnicodeText => FontType == CadShxFontType.UniFont || (FontType == CadShxFontType.Shapes && !IsPlainShapeFile);
    public int GlyphCount => _glyphData.Count(code => code.Key != 0);

    public static CadShxFont Parse(ReadOnlySpan<byte> bytes, string fileName = "")
    {
        if (bytes.Length < 8) throw new InvalidDataException("SHX file is too short.");
        var data = bytes.ToArray();
        var terminator = FindHeaderTerminator(data);
        if (terminator < 0) throw new InvalidDataException("SHX header terminator was not found.");

        var headerText = Encoding.ASCII.GetString(data, 0, terminator).Trim();
        var parts = headerText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3) throw new InvalidDataException($"Invalid SHX header: {headerText}");
        var fontType = parts[1].ToLowerInvariant() switch
        {
            "shapes" => CadShxFontType.Shapes,
            "bigfont" => CadShxFontType.BigFont,
            "unifont" => CadShxFontType.UniFont,
            _ => throw new InvalidDataException($"Unsupported SHX font type: {parts[1]}")
        };

        var reader = new ShxReader(data, terminator + 3);
        var glyphs = new Dictionary<int, byte[]>();
        var infoBytes = Array.Empty<byte>();
        switch (fontType)
        {
            case CadShxFontType.Shapes:
                ParseShapes(reader, glyphs, out infoBytes);
                break;
            case CadShxFontType.UniFont:
                ParseUniFont(reader, glyphs, out infoBytes);
                break;
            case CadShxFontType.BigFont:
                ParseBigFont(reader, glyphs, out infoBytes);
                break;
        }

        var (info, baseUp, baseDown, dualOrientation) = ParseMetrics(infoBytes, fontType);
        return new CadShxFont(fileName, parts[0], parts[2], fontType, glyphs, info, baseUp, baseDown, dualOrientation);
    }

    public bool TryGetGlyph(Rune rune, out CadShxGlyph glyph) => TryGetGlyph(rune.Value, out glyph);

    public bool TryGetGlyph(int code, out CadShxGlyph glyph)
    {
        if (code <= 0 || !_glyphData.ContainsKey(code))
        {
            glyph = null!;
            return false;
        }
        if (_glyphCache.TryGetValue(code, out glyph!)) return true;
        try
        {
            var parsed = ParseGlyph(code, 0);
            if (parsed is null)
            {
                glyph = null!;
                return false;
            }
            _glyphCache[code] = parsed;
            glyph = parsed;
            return true;
        }
        catch (InvalidDataException)
        {
            glyph = null!;
            return false;
        }
    }

    public CadShxTextLayout LayoutText(string text, double height, double widthFactor = 1, double lineSpacingFactor = 1)
    {
        ArgumentNullException.ThrowIfNull(text);
        var safeHeight = double.IsFinite(height) && height > double.Epsilon ? height : 1;
        var safeWidth = double.IsFinite(widthFactor) && Math.Abs(widthFactor) > double.Epsilon ? widthFactor : 1;
        var safeLineSpacing = double.IsFinite(lineSpacingFactor) && lineSpacingFactor > double.Epsilon ? lineSpacingFactor : 1;
        var scaleY = safeHeight / DesignHeight;
        var scaleX = scaleY * safeWidth;
        var strokes = new List<IReadOnlyList<Point2D>>();
        var cursorX = 0d;
        var cursorY = 0d;
        var maxAdvance = 0d;
        var missing = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\r') continue;
            if (rune.Value == '\n')
            {
                maxAdvance = Math.Max(maxAdvance, cursorX);
                cursorX = 0;
                cursorY -= safeHeight * safeLineSpacing;
                continue;
            }
            if (rune.Value == ' ')
            {
                cursorX += safeHeight * .5 * Math.Abs(safeWidth);
                continue;
            }
            if (!CanLayoutUnicodeText || rune.Value > ushort.MaxValue || !TryGetGlyph(rune, out var glyph))
            {
                missing++;
                cursorX += safeHeight * .6 * Math.Abs(safeWidth);
                continue;
            }

            foreach (var stroke in glyph.Strokes)
            {
                strokes.Add(stroke.Select(point => new Point2D(cursorX + (point.X * scaleX), cursorY + (point.Y * scaleY))).ToArray());
            }

            var advance = glyph.Advance.X;
            if (!double.IsFinite(advance) || Math.Abs(advance) <= 1e-9)
            {
                var bounds = glyph.Bounds;
                advance = bounds.IsEmpty ? DesignHeight * .6 : Math.Max(DesignHeight * .2, bounds.MaxX + (DesignHeight * .15));
            }
            cursorX += advance * scaleX;
        }

        maxAdvance = Math.Max(maxAdvance, cursorX);
        return new CadShxTextLayout(strokes, maxAdvance, missing);
    }

    private CadShxGlyph? ParseGlyph(int code, int depth)
    {
        if (depth > MaxSubshapeDepth) throw new InvalidDataException("SHX subshape nesting exceeds the supported depth.");
        if (!_glyphData.TryGetValue(code, out var data)) return null;
        if (FontType == CadShxFontType.BigFont && code > 0xff && data.Length >= 2 && data[0] == ((code >> 8) & 0xff) && data[1] == (code & 0xff))
        {
            var start = data.Length > 2 && data[2] == 0 ? 3 : 2;
            data = data[start..];
        }

        var state = new GlyphState(FontType != CadShxFontType.BigFont);
        for (var index = 0; index < data.Length; index++)
        {
            var command = data[index];
            if (command > 0x0f)
            {
                MoveVector(command, state);
                continue;
            }

            switch (command)
            {
                case 0:
                    state.Flush();
                    index = data.Length;
                    break;
                case 1:
                    state.PenDown();
                    break;
                case 2:
                    state.PenUp();
                    break;
                case 3:
                    Ensure(data, index, 1);
                    if (data[++index] == 0) throw new InvalidDataException("SHX divide command uses zero divisor.");
                    state.Scale /= data[index];
                    break;
                case 4:
                    Ensure(data, index, 1);
                    state.Scale *= data[++index];
                    break;
                case 5:
                    if (state.Stack.Count >= 4) throw new InvalidDataException("SHX position stack exceeds four entries.");
                    state.Stack.Push(state.Current);
                    break;
                case 6:
                    if (state.Stack.Count > 0) state.Restore(state.Stack.Pop());
                    break;
                case 7:
                    index = DrawSubshape(data, index, state, depth);
                    break;
                case 8:
                    Ensure(data, index, 2);
                    state.Move(ToSByte(data[++index]), ToSByte(data[++index]));
                    break;
                case 9:
                    index = DrawMultipleDisplacements(data, index, state);
                    break;
                case 10:
                    index = DrawOctantArc(data, index, state);
                    break;
                case 11:
                    index = DrawFractionalArc(data, index, state);
                    break;
                case 12:
                    index = DrawBulgeArc(data, index, state);
                    break;
                case 13:
                    index = DrawMultipleBulgeArcs(data, index, state);
                    break;
                case 14:
                    index = SkipNextCommand(data, index);
                    break;
            }
        }

        state.Flush();
        var explicitAdvance = state.HadPenUpMove || Math.Abs(state.Current.X) > 1e-9;
        return new CadShxGlyph(state.Strokes.Select(stroke => (IReadOnlyList<Point2D>)stroke.ToArray()).ToArray(), state.Current, explicitAdvance);
    }

    private int DrawSubshape(byte[] data, int index, GlyphState state, int depth)
    {
        int subCode;
        switch (FontType)
        {
            case CadShxFontType.Shapes:
                Ensure(data, index, 1);
                subCode = data[++index];
                break;
            case CadShxFontType.UniFont:
                Ensure(data, index, 2);
                subCode = (data[++index] << 8) | data[++index];
                break;
            default:
                return SkipNextCommand(data, index - 1);
        }

        var subshape = ParseGlyph(subCode, depth + 1);
        if (subshape is null) return index;
        state.Flush();
        var origin = state.Current;
        foreach (var stroke in subshape.Strokes)
        {
            state.Strokes.Add(stroke.Select(point => new Point2D(origin.X + (point.X * state.Scale), origin.Y + (point.Y * state.Scale))).ToList());
        }
        state.Restore(new Point2D(origin.X + (subshape.Advance.X * state.Scale), origin.Y + (subshape.Advance.Y * state.Scale)));
        return index;
    }

    private static int DrawMultipleDisplacements(byte[] data, int index, GlyphState state)
    {
        while (index + 2 < data.Length)
        {
            var x = ToSByte(data[++index]);
            var y = ToSByte(data[++index]);
            if (x == 0 && y == 0) break;
            state.Move(x, y);
        }
        return index;
    }

    private static int DrawOctantArc(byte[] data, int index, GlyphState state)
    {
        Ensure(data, index, 2);
        var radius = data[++index] * state.Scale;
        var flag = ToSByte(data[++index]);
        var startOctant = (flag & 0x70) >> 4;
        var octantCount = flag & 0x07;
        if (octantCount == 0) octantCount = 8;
        var sweep = (Math.PI / 4) * octantCount * (flag < 0 ? -1 : 1);
        var start = (Math.PI / 4) * startOctant;
        var center = new Point2D(state.Current.X - (Math.Cos(start) * radius), state.Current.Y - (Math.Sin(start) * radius));
        state.DrawArc(center, radius, start, sweep);
        return index;
    }

    private static int DrawFractionalArc(byte[] data, int index, GlyphState state)
    {
        Ensure(data, index, 5);
        var startOffset = data[++index];
        var endOffset = data[++index];
        var radius = ((data[++index] * 256) + data[++index]) * state.Scale;
        var flag = ToSByte(data[++index]);
        var startOctant = (flag & 0x70) >> 4;
        var octants = flag & 0x07;
        if (octants == 0) octants = 8;
        if (endOffset != 0) octants--;
        var direction = flag < 0 ? -1d : 1d;
        var start = ((Math.PI / 4) * startOctant) + (((Math.PI / 4) * startOffset / 256d) * direction);
        var end = ((Math.PI / 4) * (startOctant + (octants * direction))) + (((Math.PI / 4) * endOffset / 256d) * direction);
        var sweep = end - start;
        var center = new Point2D(state.Current.X - (Math.Cos(start) * radius), state.Current.Y - (Math.Sin(start) * radius));
        state.DrawArc(center, radius, start, sweep);
        return index;
    }

    private static int DrawBulgeArc(byte[] data, int index, GlyphState state)
    {
        Ensure(data, index, 3);
        var x = ToSByte(data[++index]);
        var y = ToSByte(data[++index]);
        var bulge = ToSByte(data[++index]) / 127d;
        state.DrawBulge(x, y, bulge);
        return index;
    }

    private static int DrawMultipleBulgeArcs(byte[] data, int index, GlyphState state)
    {
        while (index + 2 < data.Length)
        {
            var x = ToSByte(data[++index]);
            var y = ToSByte(data[++index]);
            if (x == 0 && y == 0) break;
            Ensure(data, index, 1);
            var bulge = ToSByte(data[++index]) / 127d;
            state.DrawBulge(x, y, bulge);
        }
        return index;
    }

    private static void MoveVector(byte command, GlyphState state)
    {
        var length = (command & 0xf0) >> 4;
        var direction = command & 0x0f;
        var (x, y) = direction switch
        {
            0 => (1d, 0d), 1 => (1d, .5d), 2 => (1d, 1d), 3 => (.5d, 1d),
            4 => (0d, 1d), 5 => (-.5d, 1d), 6 => (-1d, 1d), 7 => (-1d, .5d),
            8 => (-1d, 0d), 9 => (-1d, -.5d), 10 => (-1d, -1d), 11 => (-.5d, -1d),
            12 => (0d, -1d), 13 => (.5d, -1d), 14 => (1d, -1d), _ => (1d, -.5d)
        };
        state.Move(x * length, y * length);
    }

    private int SkipNextCommand(byte[] data, int index)
    {
        if (++index >= data.Length) return data.Length;
        var command = data[index];
        if (command > 0x0f) return index;
        return command switch
        {
            3 or 4 => Math.Min(data.Length - 1, index + 1),
            7 when FontType == CadShxFontType.UniFont => Math.Min(data.Length - 1, index + 2),
            7 => Math.Min(data.Length - 1, index + 1),
            8 or 10 => Math.Min(data.Length - 1, index + 2),
            11 => Math.Min(data.Length - 1, index + 5),
            12 => Math.Min(data.Length - 1, index + 3),
            9 => SkipPairs(data, index),
            13 => SkipBulgeTriples(data, index),
            _ => index
        };
    }

    private static int SkipPairs(byte[] data, int index)
    {
        while (index + 2 < data.Length)
        {
            var x = data[++index];
            var y = data[++index];
            if (x == 0 && y == 0) break;
        }
        return index;
    }

    private static int SkipBulgeTriples(byte[] data, int index)
    {
        while (index + 2 < data.Length)
        {
            var x = data[++index];
            var y = data[++index];
            if (x == 0 && y == 0) break;
            if (++index >= data.Length) break;
        }
        return index;
    }

    private static void ParseShapes(ShxReader reader, Dictionary<int, byte[]> glyphs, out byte[] infoBytes)
    {
        reader.Skip(4);
        var count = reader.ReadInt16();
        if (count <= 0) throw new InvalidDataException("SHX shape table is empty.");
        var entries = new (int Code, int Length)[count];
        for (var i = 0; i < count; i++) entries[i] = (reader.ReadUInt16(), reader.ReadUInt16());
        foreach (var entry in entries)
        {
            var raw = reader.ReadBytes(entry.Length);
            glyphs[entry.Code] = entry.Code == 0 ? raw : StripShapeName(raw);
        }
        infoBytes = glyphs.TryGetValue(0, out var info) ? info : Array.Empty<byte>();
    }

    private static void ParseUniFont(ShxReader reader, Dictionary<int, byte[]> glyphs, out byte[] infoBytes)
    {
        var count = reader.ReadInt32();
        if (count <= 0) throw new InvalidDataException("SHX unifont table is empty.");
        var infoLength = reader.ReadInt16();
        infoBytes = reader.ReadBytes(infoLength);
        for (var i = 0; i < count - 1; i++)
        {
            var code = reader.ReadUInt16();
            var length = reader.ReadUInt16();
            glyphs[code] = StripShapeName(reader.ReadBytes(length));
        }
    }

    private static void ParseBigFont(ShxReader reader, Dictionary<int, byte[]> glyphs, out byte[] infoBytes)
    {
        _ = reader.ReadInt16();
        var count = reader.ReadInt16();
        var changeCount = reader.ReadInt16();
        if (count <= 0) throw new InvalidDataException("SHX bigfont table is empty.");
        reader.Skip(changeCount * 4);
        var entries = new (int Code, int Length, int Offset)[count];
        for (var i = 0; i < count; i++) entries[i] = (reader.ReadUInt16(), reader.ReadUInt16(), checked((int)reader.ReadUInt32()));
        foreach (var entry in entries)
        {
            if (entry.Length <= 0) continue;
            var position = reader.Position;
            reader.Position = entry.Offset;
            glyphs[entry.Code] = reader.ReadBytes(entry.Length);
            reader.Position = position;
        }
        infoBytes = glyphs.TryGetValue(0, out var info) ? info : Array.Empty<byte>();
    }

    private static (string Info, double BaseUp, double BaseDown, bool DualOrientation) ParseMetrics(byte[] infoBytes, CadShxFontType type)
    {
        if (infoBytes.Length == 0) return (string.Empty, 8, 2, false);
        var terminator = Array.FindIndex(infoBytes, value => value is 0 or 0x0d or 0x0a);
        if (terminator < 0) return (Encoding.ASCII.GetString(infoBytes), 8, 2, false);
        var info = Encoding.ASCII.GetString(infoBytes, 0, terminator).TrimEnd('\0');
        if (type == CadShxFontType.BigFont)
        {
            var index = terminator + 1;
            while (index < infoBytes.Length && infoBytes[index] == 0) index++;
            if (index + 2 < infoBytes.Length)
            {
                var up = infoBytes[index];
                var down = infoBytes[index + 1];
                var mode = infoBytes[index + 2];
                return (info, up, down, mode == 2);
            }
            return (info, 8, 2, false);
        }
        if (terminator + 3 < infoBytes.Length)
        {
            var up = infoBytes[terminator + 1];
            var down = infoBytes[terminator + 2];
            var mode = infoBytes[terminator + 3];
            return (info, up, down, mode == 2);
        }
        return (info, 8, 2, false);
    }

    private static byte[] StripShapeName(byte[] raw)
    {
        var index = Array.IndexOf(raw, (byte)0);
        return index < 0 ? raw : raw[(index + 1)..];
    }

    private static int FindHeaderTerminator(byte[] data)
    {
        for (var index = 0; index + 2 < data.Length; index++)
        {
            if (data[index] == 0x0d && data[index + 1] == 0x0a && data[index + 2] == 0x1a) return index;
        }
        return -1;
    }

    private static sbyte ToSByte(byte value) => unchecked((sbyte)value);

    private static void Ensure(byte[] data, int index, int followingBytes)
    {
        if (index + followingBytes >= data.Length) throw new InvalidDataException("Unexpected end of SHX glyph bytecode.");
    }

    private sealed class GlyphState
    {
        public GlyphState(bool penDown)
        {
            PenIsDown = penDown;
            if (penDown) CurrentStroke.Add(Current);
        }

        public Point2D Current { get; private set; } = Point2D.Origin;
        public List<List<Point2D>> Strokes { get; } = new();
        public List<Point2D> CurrentStroke { get; private set; } = new();
        public Stack<Point2D> Stack { get; } = new();
        public bool PenIsDown { get; private set; }
        public double Scale { get; set; } = 1;
        public bool HadPenUpMove { get; private set; }

        public void PenDown()
        {
            if (!PenIsDown && CurrentStroke.Count == 0) CurrentStroke.Add(Current);
            PenIsDown = true;
        }

        public void PenUp()
        {
            Flush();
            PenIsDown = false;
        }

        public void Restore(Point2D point)
        {
            Flush();
            Current = point;
            if (PenIsDown) CurrentStroke.Add(Current);
        }

        public void Move(double x, double y)
        {
            var previous = Current;
            Current = new Point2D(Current.X + (x * Scale), Current.Y + (y * Scale));
            if (PenIsDown)
            {
                if (CurrentStroke.Count == 0) CurrentStroke.Add(previous);
                CurrentStroke.Add(Current);
            }
            else HadPenUpMove = true;
        }

        public void DrawArc(Point2D center, double radius, double start, double sweep)
        {
            var end = new Point2D(center.X + (Math.Cos(start + sweep) * radius), center.Y + (Math.Sin(start + sweep) * radius));
            if (PenIsDown)
            {
                if (CurrentStroke.Count == 0) CurrentStroke.Add(Current);
                var segments = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 18)));
                for (var segment = 1; segment <= segments; segment++)
                {
                    var angle = start + (sweep * segment / segments);
                    CurrentStroke.Add(new Point2D(center.X + (Math.Cos(angle) * radius), center.Y + (Math.Sin(angle) * radius)));
                }
            }
            Current = end;
        }

        public void DrawBulge(double x, double y, double bulge)
        {
            var displacement = new Vector2D(x * Scale, y * Scale);
            var end = Current + displacement;
            if (!PenIsDown || Math.Abs(bulge) <= 1e-12)
            {
                Move(x, y);
                return;
            }
            var chord = Current.DistanceTo(end);
            if (chord <= double.Epsilon)
            {
                Current = end;
                return;
            }
            var midpoint = new Point2D((Current.X + end.X) / 2, (Current.Y + end.Y) / 2);
            var offset = chord * (1 - (bulge * bulge)) / (4 * bulge);
            var dx = end.X - Current.X;
            var dy = end.Y - Current.Y;
            var center = new Point2D(midpoint.X - ((dy / chord) * offset), midpoint.Y + ((dx / chord) * offset));
            var radius = center.DistanceTo(Current);
            var start = Math.Atan2(Current.Y - center.Y, Current.X - center.X);
            DrawArc(center, radius, start, 4 * Math.Atan(bulge));
            Current = end;
        }

        public void Flush()
        {
            if (CurrentStroke.Count > 1) Strokes.Add(CurrentStroke);
            CurrentStroke = new List<Point2D>();
        }
    }

    private sealed class ShxReader
    {
        private readonly byte[] _data;
        public ShxReader(byte[] data, int position) { _data = data; Position = position; }
        public int Position { get; set; }
        public int Remaining => _data.Length - Position;
        public void Skip(int count) { Require(count); Position += count; }
        public short ReadInt16() { Require(2); var value = (short)(_data[Position] | (_data[Position + 1] << 8)); Position += 2; return value; }
        public ushort ReadUInt16() { Require(2); var value = (ushort)(_data[Position] | (_data[Position + 1] << 8)); Position += 2; return value; }
        public int ReadInt32() => unchecked((int)ReadUInt32());
        public uint ReadUInt32() { Require(4); var value = (uint)(_data[Position] | (_data[Position + 1] << 8) | (_data[Position + 2] << 16) | (_data[Position + 3] << 24)); Position += 4; return value; }
        public byte[] ReadBytes(int count) { Require(count); var result = _data.AsSpan(Position, count).ToArray(); Position += count; return result; }
        private void Require(int count) { if (count < 0 || Position < 0 || Position + count > _data.Length) throw new InvalidDataException("Unexpected end of SHX file."); }
    }
}
