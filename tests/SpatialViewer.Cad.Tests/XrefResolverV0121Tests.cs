using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class XrefResolverV0121Tests
{
    [Fact]
    public async Task HostSuppliedXrefStreamReachesSceneWithoutOpeningSourceReference()
    {
        var parentPath = TempDxfPath();
        const string privateReference = @"Z:\private\never-present\title-frame.dwg";
        try
        {
            WriteParentXrefDxf(parentPath, privateReference);

            var importer = new ACadSharpCadImporter();
            var local = await importer.ImportAsync(new ImportRequest(parentPath));
            var localDocument = Assert.IsType<CadDocument>(local.Document);
            Assert.True(local.IsSuccess);
            Assert.DoesNotContain(localDocument.Scene.GetItems(), item => item.Bounds == new BoundingBox2D(100, 50, 110, 50));

            var resolver = new RecordingResolver(_ => new CadExternalReferenceResource(BuildChildDxf(), CadExternalReferenceFormat.Dxf));
            var resolved = await importer.ImportWithExternalReferencesAsync(new ImportRequest(parentPath), resolver);
            var document = Assert.IsType<CadDocument>(resolved.Document);

            Assert.True(resolved.IsSuccess);
            Assert.Equal(1, resolver.CallCount);
            var request = Assert.Single(resolver.Requests);
            Assert.Equal(privateReference, request.SourceReference);
            Assert.Equal("TITLE_XREF", request.ReferenceName);
            Assert.False(request.IsOverlay);

            Assert.Equal("1", document.Metadata["XrefResolverRequestCount"]);
            Assert.Equal("1", document.Metadata["XrefResolverResolvedCount"]);
            Assert.Equal("0", document.Metadata["XrefResolverFailedCount"]);
            Assert.Contains(document.Blocks, block => string.Equals(block.Name, "__XREF_0001__::STAMP", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(document.Scene.GetItems(), item => item.Bounds == new BoundingBox2D(100, 50, 110, 50));
            Assert.Contains(document.Scene.GetItems(), item => item.Bounds == new BoundingBox2D(120, 45, 140, 45));

            var serialized = System.Text.Json.JsonSerializer.Serialize(resolved.Diagnostics);
            Assert.DoesNotContain(privateReference, serialized, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(parentPath)) File.Delete(parentPath);
        }
    }

    [Fact]
    public async Task ResolverDeclineAndFailureKeepParentUsableAndDoNotLeakSourceReference()
    {
        var parentPath = TempDxfPath();
        const string privateReference = @"D:\sensitive\title-border.dwg";
        try
        {
            WriteParentXrefDxf(parentPath, privateReference);
            var importer = new ACadSharpCadImporter();

            var declinedResolver = new RecordingResolver(_ => null);
            var declined = await importer.ImportWithExternalReferencesAsync(new ImportRequest(parentPath), declinedResolver);
            var declinedDocument = Assert.IsType<CadDocument>(declined.Document);
            Assert.True(declined.IsSuccess);
            Assert.Equal(1, declinedResolver.CallCount);
            Assert.Equal("1", declinedDocument.Metadata["XrefResolverDeclinedCount"]);
            Assert.Equal("0", declinedDocument.Metadata["XrefResolverResolvedCount"]);

            var failedResolver = new RecordingResolver(_ => throw new InvalidOperationException("resolver-secret-must-not-surface"));
            var failed = await importer.ImportWithExternalReferencesAsync(new ImportRequest(parentPath), failedResolver);
            var failedDocument = Assert.IsType<CadDocument>(failed.Document);
            Assert.True(failed.IsSuccess);
            Assert.Equal(1, failedResolver.CallCount);
            Assert.Equal("1", failedDocument.Metadata["XrefResolverFailedCount"]);
            Assert.Contains(failed.Diagnostics, diagnostic => diagnostic.Code == "CAD_XREF_RESOLVER_FAILED");

            var serialized = System.Text.Json.JsonSerializer.Serialize(failed.Diagnostics);
            Assert.DoesNotContain(privateReference, serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("resolver-secret-must-not-surface", serialized, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(parentPath)) File.Delete(parentPath);
        }
    }

    [Fact]
    public async Task UnloadedXrefNeverCallsHostResolver()
    {
        var parentPath = TempDxfPath();
        try
        {
            WriteParentXrefDxf(parentPath, "unloaded-title.dwg", isUnloaded: true);
            var resolver = new RecordingResolver(_ => throw new InvalidOperationException("must not be called"));

            var result = await new ACadSharpCadImporter().ImportWithExternalReferencesAsync(new ImportRequest(parentPath), resolver);
            var document = Assert.IsType<CadDocument>(result.Document);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, resolver.CallCount);
            Assert.Equal("1", document.Metadata["XrefUnloadedCount"]);
            Assert.Equal("0", document.Metadata["XrefResolverRequestCount"]);
        }
        finally
        {
            if (File.Exists(parentPath)) File.Delete(parentPath);
        }
    }

    [Fact]
    public async Task NestedXrefIsReportedButNotRecursivelyResolved()
    {
        var parentPath = TempDxfPath();
        const string nestedPrivateReference = @"Q:\nested\private-background.dwg";
        try
        {
            WriteParentXrefDxf(parentPath, "root-title.dxf");
            var resolver = new RecordingResolver(_ => new CadExternalReferenceResource(BuildChildDxf(nestedPrivateReference), CadExternalReferenceFormat.Dxf));

            var result = await new ACadSharpCadImporter().ImportWithExternalReferencesAsync(new ImportRequest(parentPath), resolver);
            var document = Assert.IsType<CadDocument>(result.Document);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, resolver.CallCount);
            Assert.Equal("1", document.Metadata["XrefResolverResolvedCount"]);
            Assert.Equal("1", document.Metadata["XrefNestedDependencyCount"]);
            Assert.Contains(document.Blocks, block => block.Name.Contains("NESTED_XREF", StringComparison.OrdinalIgnoreCase));

            var serialized = System.Text.Json.JsonSerializer.Serialize(result.Diagnostics);
            Assert.DoesNotContain(nestedPrivateReference, serialized, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(parentPath)) File.Delete(parentPath);
        }
    }

    [Fact]
    public async Task InvalidHostResourceFailsClosedWithoutDiscardingParentDocument()
    {
        var parentPath = TempDxfPath();
        try
        {
            WriteParentXrefDxf(parentPath, "broken-resource.dxf");
            var resolver = new RecordingResolver(_ => new CadExternalReferenceResource(new MemoryStream(new byte[] { 1, 2, 3, 4 }), CadExternalReferenceFormat.Dxf));

            var result = await new ACadSharpCadImporter().ImportWithExternalReferencesAsync(new ImportRequest(parentPath), resolver);
            var document = Assert.IsType<CadDocument>(result.Document);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, resolver.CallCount);
            Assert.Equal("1", document.Metadata["XrefResolverFailedCount"]);
            Assert.Equal("0", document.Metadata["XrefResolverResolvedCount"]);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CAD_XREF_RESOURCE_READ_FAILED");
        }
        finally
        {
            if (File.Exists(parentPath)) File.Delete(parentPath);
        }
    }

    private static string TempDxfPath() => Path.Combine(Path.GetTempPath(), $"cadcore-xref-{Guid.NewGuid():N}.dxf");

    private static void WriteParentXrefDxf(string path, string sourceReference, bool isUnloaded = false)
    {
        var document = new global::ACadSharp.CadDocument();
        document.CreateDefaults();
        var xref = new BlockRecord("TITLE_XREF", sourceReference) { IsUnloaded = isUnloaded };
        document.BlockRecords.Add(xref);
        document.Entities.Add(new Insert(xref) { InsertPoint = new XYZ(100, 50, 0) });

        using var writer = new DxfWriter(path, document, false);
        writer.Write();
    }

    private static MemoryStream BuildChildDxf(string? nestedReference = null)
    {
        var document = new global::ACadSharp.CadDocument();
        document.CreateDefaults();
        document.Header.ModelSpaceInsertionBase = new XYZ(10, 5, 0);
        document.Entities.Add(new Line(new XYZ(10, 5, 0), new XYZ(20, 5, 0)));

        var stamp = new BlockRecord("STAMP");
        stamp.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(20, 0, 0)));
        document.BlockRecords.Add(stamp);
        document.Entities.Add(new Insert(stamp) { InsertPoint = new XYZ(30, 0, 0) });

        if (!string.IsNullOrWhiteSpace(nestedReference))
        {
            var nested = new BlockRecord("NESTED_XREF", nestedReference);
            document.BlockRecords.Add(nested);
            document.Entities.Add(new Insert(nested) { InsertPoint = new XYZ(0, 100, 0) });
        }

        using var buffer = new MemoryStream();
        using (var writer = new DxfWriter(buffer, document, false))
        {
            writer.Write();
        }
        return new MemoryStream(buffer.ToArray(), writable: false);
    }

    private sealed class RecordingResolver : ICadExternalReferenceResolver
    {
        private readonly Func<CadExternalReferenceRequest, CadExternalReferenceResource?> _resolve;

        public RecordingResolver(Func<CadExternalReferenceRequest, CadExternalReferenceResource?> resolve)
        {
            _resolve = resolve;
        }

        public int CallCount { get; private set; }
        public List<CadExternalReferenceRequest> Requests { get; } = new();

        public CadExternalReferenceResource? Resolve(CadExternalReferenceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Requests.Add(request);
            return _resolve(request);
        }
    }
}
