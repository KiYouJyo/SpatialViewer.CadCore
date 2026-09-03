using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TableCacheV0121Tests
{
    [Fact]
    public async Task ACadSharpTableCacheRoundTripsThroughReaderImporterAndScene()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-table-cache-{Guid.NewGuid():N}.dxf");
        try
        {
            ACadSharpFixtureTranscoder.WriteTableCacheDxf(path);

            var profile = CadSourceContentProfiler.AnalyzeFile(path);
            Assert.Equal(1, profile.TableEntityCount);
            Assert.Equal(1, profile.TableCacheBlockDefinitionCount);
            Assert.True(profile.AnonymousBlockDefinitionCount >= 1);
            Assert.True(profile.AnonymousBlockReferenceCount >= 1);
            Assert.False(profile.HasExternalReferenceDependency);

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            Assert.True(result.IsSuccess);

            var tableReference = Assert.Single(document.ModelSpace.OfType<CadBlockReferenceEntity>());
            Assert.StartsWith("*T", tableReference.BlockName, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(new Point2D(100, 50), tableReference.InsertionPoint);

            var cache = Assert.Single(document.Blocks, block => string.Equals(block.Name, tableReference.BlockName, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(cache.Entities, entity => entity is CadLineEntity);

            Assert.Contains(
                document.Scene.GetItems(),
                item => item.Bounds == new BoundingBox2D(100, 50, 120, 50));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
