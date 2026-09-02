using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class DimStyleV0100ReaderTests
{
    [Fact]
    public async Task GeneratedDxfCarriesActiveDimStyleIntoReaderIndependentPresentation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatial-viewer-dimstyle-v0100-{Guid.NewGuid():N}.dxf");
        try
        {
            ACadSharpFixtureTranscoder.WriteAnnotationDxf(path);
            var result = await new ACadSharpCadImporter().ImportAsync(new SpatialViewer.Core.ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var dimension = Assert.Single(document.ModelSpace.OfType<CadDimensionEntity>());
            var presentation = dimension.Presentation;

            Assert.True(result.IsSuccess);
            Assert.Equal(0.625, presentation.ExtensionLineOffset, 6);
            Assert.Equal(1.25, presentation.ExtensionLineExtension, 6);
            Assert.Equal(0.625, presentation.DimensionLineGap, 6);
            Assert.Equal(2, presentation.DecimalPlaces);
            Assert.Equal('.', presentation.DecimalSeparator);
            Assert.False(presentation.SuppressFirstExtensionLine);
            Assert.False(presentation.SuppressSecondExtensionLine);
            Assert.False(presentation.SuppressFirstDimensionLine);
            Assert.False(presentation.SuppressSecondDimensionLine);

            var item = Assert.Single(document.Scene.GetItems(), candidate =>
                candidate.Id == dimension.ObjectId && candidate.Geometry is SpatialViewer.Core.TextGeometry);
            Assert.Equal("0.625", item.Metadata["DimensionExtensionLineOffset"]);
            Assert.Equal("1.25", item.Metadata["DimensionExtensionLineExtension"]);
            Assert.Equal("2", item.Metadata["DimensionDecimalPlaces"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
