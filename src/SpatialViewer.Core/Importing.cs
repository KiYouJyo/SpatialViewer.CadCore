namespace SpatialViewer.Core;

/// <summary>Immutable request for a format-neutral document import.</summary>
public sealed record ImportRequest(string FilePath, ImportOptions? Options = null);
/// <summary>Controls an import without coupling it to a user interface.</summary>
public sealed record ImportOptions(bool ContinueOnUnsupportedContent = true, IReadOnlyDictionary<string, string>? Metadata = null);
/// <summary>Progress reported by importers at stable pipeline boundaries.</summary>
public sealed record ImportProgress(string Stage, double? Fraction = null, string? Message = null);
/// <summary>Result of an import. Partial imports are represented by a document plus warning/error diagnostics.</summary>
public sealed record ImportResult(IDocument? Document, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsSuccess => Document is not null && !Diagnostics.Any(x => x.Severity is DiagnosticSeverity.Fatal);
}
/// <summary>Format-neutral contract implemented by reader adapters.</summary>
public interface IDocumentImporter
{
    bool CanImport(string filePath);
    Task<ImportResult> ImportAsync(ImportRequest request, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default);
}
