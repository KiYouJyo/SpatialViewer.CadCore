using ACadSharp.Entities;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

public sealed partial class ACadSharpCadImporter
{
    private static CadHatchPatternLine[] MapHatchPatternLines(Hatch hatch)
    {
        if (hatch.IsSolid || hatch.Pattern is null || hatch.Pattern.Lines.Count == 0) return Array.Empty<CadHatchPatternLine>();
        return hatch.Pattern.Lines.Select(line => new CadHatchPatternLine(
            line.Angle,
            new Point2D(line.BasePoint.X, line.BasePoint.Y),
            new Vector2D(line.Offset.X, line.Offset.Y),
            line.DashLengths.Where(double.IsFinite).ToArray())).ToArray();
    }
}
