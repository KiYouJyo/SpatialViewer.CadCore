using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using CSMath;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class AngleUnitsV0121ReaderTests
{
    [Fact]
    public async Task ArcTextAndViewportAnglesSurviveRealDxfReaderInRadians()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-angle-units-{Guid.NewGuid():N}.dxf");
        try
        {
            var source = new global::ACadSharp.CadDocument();
            source.CreateDefaults();
            source.Entities.Add(new Arc
            {
                Center = XYZ.Zero,
                Radius = 10,
                StartAngle = Math.PI / 2,
                EndAngle = Math.PI
            });
            source.Entities.Add(new TextEntity
            {
                Value = "ANGLE",
                InsertPoint = new XYZ(20, 0, 0),
                Height = 2.5,
                Rotation = Math.PI / 3
            });

            var layout = new Layout("AngleSheet")
            {
                TabOrder = 2,
                PaperWidth = 100,
                PaperHeight = 100,
                MinLimits = new XY(0, 0),
                MaxLimits = new XY(100, 100),
                MinExtents = new XYZ(0, 0, 0),
                MaxExtents = new XYZ(100, 100, 0)
            };
            layout.UpdatePaperViewport();
            layout.AddViewport(new Viewport
            {
                ActiveStatus = 2,
                Center = new XYZ(50, 50, 0),
                Width = 50,
                Height = 50,
                ViewCenter = XY.Zero,
                ViewTarget = XYZ.Zero,
                ViewHeight = 50,
                TwistAngle = Math.PI / 6
            });
            source.Layouts.Add(layout);

            using (var writer = new DxfWriter(path, source, false)) writer.Write();

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
            var document = Assert.IsType<CadDocument>(result.Document);

            var arc = Assert.Single(document.ModelSpace.OfType<CadArcEntity>());
            Assert.Equal(Math.PI / 2, arc.StartRadians, 10);
            Assert.Equal(Math.PI / 2, arc.SweepRadians, 10);

            var text = Assert.Single(document.ModelSpace.OfType<CadTextEntity>());
            Assert.Equal(Math.PI / 3, text.RotationRadians, 10);

            var sheet = Assert.Single(document.Layouts, candidate => candidate.Name == "AngleSheet");
            var viewport = Assert.Single(sheet.Viewports, candidate => candidate.IsOn && !candidate.RepresentsPaper);
            Assert.Equal(Math.PI / 6, viewport.TwistRadians, 10);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
