using ACadSharp.Entities;
using ACadSharp.Objects;
using ACadSharp.Tables;
using CSMath;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class SourceContentProfilerV0121Tests
{
    [Fact]
    public void AnalyzeSeparatesPaperAnonymousTableAndExternalReferenceContentWithoutPaths()
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();

        var anonymous = new BlockRecord("*U900") { IsAnonymous = true };
        anonymous.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(10, 0, 0)));
        var tableCache = new BlockRecord("*T900") { IsAnonymous = true };
        tableCache.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(20, 0, 0)));
        var xref = new BlockRecord("XREF_A", "private-title-frame.dwg") { IsUnloaded = true };

        var anonymousInsert = new Insert(anonymous) { InsertPoint = new XYZ(10, 20, 0) };
        var xrefInsert = new Insert(xref) { InsertPoint = new XYZ(30, 40, 0) };
        var table = new TableEntity(tableCache) { InsertPoint = new XYZ(50, 60, 0) };
        var paperInsert = new Insert(anonymous) { InsertPoint = new XYZ(5, 5, 0) };

        source.BlockRecords.Add(anonymous);
        source.BlockRecords.Add(tableCache);
        source.BlockRecords.Add(xref);
        source.Entities.Add(anonymousInsert);
        source.Entities.Add(xrefInsert);
        source.Entities.Add(table);

        var layout = new Layout("Sheet-A");
        layout.UpdatePaperViewport();
        layout.AssociatedBlock.Entities.Add(paperInsert);
        source.Layouts.Add(layout);

        var profile = CadSourceContentProfiler.Analyze(source);

        Assert.Equal(3, profile.ModelSpaceEntityCount);
        Assert.Equal(1, profile.PaperSpaceEntityCount);
        Assert.True(profile.PaperViewportCount >= 1);
        Assert.Equal(2, profile.ModelSpaceBlockReferenceCount + profile.TableEntityCount - 1);
        Assert.Equal(1, profile.PaperSpaceBlockReferenceCount);
        Assert.True(profile.AnonymousBlockDefinitionCount >= 2);
        Assert.True(profile.AnonymousBlockReferenceCount >= 2);
        Assert.Equal(1, profile.TableEntityCount);
        Assert.Equal(1, profile.TableCacheBlockDefinitionCount);
        Assert.Equal(1, profile.ExternalReferenceDefinitionCount);
        Assert.Equal(1, profile.ExternalReferenceReferenceCount);
        Assert.Equal(1, profile.UnloadedExternalReferenceDefinitionCount);
        Assert.Equal(1, profile.EmptyExternalReferenceDefinitionCount);
        Assert.True(profile.HasPaperSpaceContent);
        Assert.True(profile.HasAnonymousBlockContent);
        Assert.True(profile.HasTableContent);
        Assert.True(profile.HasExternalReferenceDependency);

        var serialized = System.Text.Json.JsonSerializer.Serialize(profile);
        Assert.DoesNotContain("private-title-frame.dwg", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("XREF_A", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeFileRecognizesReferencedAnonymousBlockThroughRealDxfReader()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "cad", "dxf", "anonymous-block-v0121.dxf");
        var profile = CadSourceContentProfiler.AnalyzeFile(path);

        Assert.Equal(1, profile.ModelSpaceBlockReferenceCount);
        Assert.True(profile.AnonymousBlockDefinitionCount >= 1);
        Assert.Equal(1, profile.AnonymousBlockReferenceCount);
        Assert.False(profile.HasExternalReferenceDependency);
    }
}
