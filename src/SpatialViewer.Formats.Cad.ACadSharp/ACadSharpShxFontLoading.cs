using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Resolves source SHX references for one CAD import. File discovery is intentionally host-directed:
/// the drawing directory is implicit and additional support directories arrive through ImportOptions metadata.
/// </summary>
internal sealed class ACadSharpShxFontLoading
{
    private const long MaxShxFileBytes = 32L * 1024 * 1024;
    private readonly string[] _searchDirectories;
    private readonly List<Diagnostic> _diagnostics;
    private readonly Dictionary<string, CadShxFont?> _fontCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reportedFailures = new(StringComparer.OrdinalIgnoreCase);

    public ACadSharpShxFontLoading(ImportRequest request, List<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics;

        var directories = new List<string>();
        var drawingDirectory = Path.GetDirectoryName(Path.GetFullPath(request.FilePath));
        AddDirectory(directories, drawingDirectory);

        if (request.Options?.Metadata is { } metadata &&
            metadata.TryGetValue(CadFontImportMetadata.ShxSearchPaths, out var configured) &&
            !string.IsNullOrWhiteSpace(configured))
        {
            foreach (var candidate in configured.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddDirectory(directories, candidate);
            }
        }

        _searchDirectories = directories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public int SearchDirectoryCount => _searchDirectories.Length;
    public int RequestedFontCount => _fontCache.Count;
    public int LoadedFontCount => _fontCache.Values.Count(font => font is not null);

    public CadEntity Apply(CadEntity entity) => entity switch
    {
        CadTextEntity text => text with { Presentation = Apply(text.Presentation) },
        CadAttributeEntity attribute => attribute with { Presentation = Apply(attribute.Presentation) },
        CadBlockReferenceEntity insert => insert with { Attributes = insert.Attributes.Select(attribute => (CadAttributeEntity)Apply(attribute)).ToArray() },
        _ => entity
    };

    public CadBlockDefinition Apply(CadBlockDefinition block)
        => block with { Entities = block.Entities.Select(Apply).ToArray() };

    public CadLayoutDefinition Apply(CadLayoutDefinition layout)
        => layout with { Entities = layout.Entities.Select(Apply).ToArray() };

    private CadTextPresentation Apply(CadTextPresentation presentation)
    {
        if (!ShouldLoad(presentation)) return presentation;
        var sourceName = presentation.FontFileName.Trim();
        if (sourceName.Length == 0) return presentation;
        var font = Load(sourceName);
        return font is null ? presentation : presentation with { VectorFont = font };
    }

    private CadShxFont? Load(string sourceName)
    {
        if (_fontCache.TryGetValue(sourceName, out var cached)) return cached;

        var path = Resolve(sourceName);
        if (path is null)
        {
            _fontCache[sourceName] = null;
            ReportOnce(
                "CAD_SHX_FONT_NOT_FOUND",
                sourceName,
                $"SHX font was not found: {sourceName}",
                new Dictionary<string, string> { ["Font"] = sourceName });
            return null;
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxShxFileBytes)
            {
                _fontCache[sourceName] = null;
                ReportOnce(
                    "CAD_SHX_FONT_TOO_LARGE",
                    sourceName,
                    $"SHX font exceeds the {MaxShxFileBytes / (1024 * 1024)} MiB safety limit: {sourceName}",
                    new Dictionary<string, string> { ["Font"] = sourceName, ["ResolvedPath"] = path, ["Bytes"] = info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            var font = CadShxFont.Parse(bytes, Path.GetFileName(path));
            _fontCache[sourceName] = font;
            return font;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _fontCache[sourceName] = null;
            ReportOnce(
                "CAD_SHX_FONT_INVALID",
                sourceName,
                $"Unable to load SHX font {sourceName}: {exception.Message}",
                new Dictionary<string, string> { ["Font"] = sourceName, ["ResolvedPath"] = path });
            return null;
        }
    }

    private string? Resolve(string sourceName)
    {
        if (Path.IsPathRooted(sourceName))
        {
            try
            {
                var full = Path.GetFullPath(sourceName);
                if (File.Exists(full)) return full;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Continue with basename recovery below.
            }

            var relocatedName = Path.GetFileName(sourceName);
            return relocatedName.Length == 0 ? null : ResolveFromSearchDirectories(relocatedName);
        }

        var exactRelative = ResolveFromSearchDirectories(sourceName);
        if (exactRelative is not null) return exactRelative;
        var fileName = Path.GetFileName(sourceName);
        return fileName.Length > 0 && !fileName.Equals(sourceName, StringComparison.OrdinalIgnoreCase)
            ? ResolveFromSearchDirectories(fileName)
            : null;
    }

    private string? ResolveFromSearchDirectories(string reference)
    {
        foreach (var directory in _searchDirectories)
        {
            try
            {
                var candidate = Path.GetFullPath(Path.Combine(directory, reference));
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // A malformed source font reference should not abort the CAD import.
            }
        }
        return null;
    }

    private void ReportOnce(string code, string sourceName, string message, Dictionary<string, string> data)
    {
        var key = $"{code}\u001f{sourceName}";
        if (!_reportedFailures.Add(key)) return;
        _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, code, message, data));
    }

    private static bool ShouldLoad(CadTextPresentation presentation)
        => presentation.IsShapeFile || Path.GetExtension(presentation.FontFileName).Equals(".shx", StringComparison.OrdinalIgnoreCase);

    private static void AddDirectory(List<string> directories, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var full = Path.GetFullPath(path);
            if (Directory.Exists(full)) directories.Add(full);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Ignore invalid host search paths; source diagnostics are emitted only when a referenced font cannot resolve.
        }
    }
}
