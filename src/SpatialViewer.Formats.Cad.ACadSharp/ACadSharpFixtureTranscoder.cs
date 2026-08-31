using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using CSMath;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>Test-only helpers that produce legal deterministic CAD fixtures through ACadSharp's own writers.</summary>
public static class ACadSharpFixtureTranscoder
{
    public static void WriteDwgFromDxf(string dxfPath, string dwgPath)
    {
        using var reader = new DxfReader(dxfPath);
        var document = reader.Read();
        document.CreateDefaults();
        using var writer = new DwgWriter(dwgPath, document);
        writer.Write();
    }

    public static void WriteAnnotationDxf(string dxfPath)
    {
        var document = new CadDocument();
        document.CreateDefaults();

        var dimension = new DimensionLinear
        {
            FirstPoint = new XYZ(0, 0, 0),
            SecondPoint = new XYZ(100, 0, 0),
            Offset = 20,
            TextMiddlePoint = new XYZ(50, 23, 0)
        };
        document.Entities.Add(dimension);
        dimension.UpdateBlock();

        var leader = new Leader
        {
            ArrowHeadEnabled = true,
            PathType = LeaderPathType.StraightLineSegments,
            CreationType = LeaderCreationType.CreatedWithoutAnnotation,
            TextHeight = 2.5
        };
        leader.Vertices.Add(new XYZ(0, 40, 0));
        leader.Vertices.Add(new XYZ(20, 50, 0));
        leader.Vertices.Add(new XYZ(40, 50, 0));
        document.Entities.Add(leader);

        var multiLeader = new MultiLeader
        {
            PathType = MultiLeaderPathType.StraightLineSegments,
            EnableDogleg = true,
            LandingDistance = 8,
            ArrowheadSize = 2.5,
            PropertyOverrideFlags = MultiLeaderPropertyOverrideFlags.ContentType | MultiLeaderPropertyOverrideFlags.TextAlignment | MultiLeaderPropertyOverrideFlags.EnableUseDefaultMText
        };
        multiLeader.ContextData.ContentBasePoint = new XYZ(88, 70, 0);
        multiLeader.ContextData.BasePoint = XYZ.Zero;
        multiLeader.ContextData.TextLabel = "CadCore MLeader";
        multiLeader.ContextData.TextLocation = new XYZ(88, 70, 0);
        multiLeader.ContextData.TextHeight = 2.5;
        var root = new MultiLeaderObjectContextData.LeaderRoot
        {
            ConnectionPoint = new XYZ(80, 70, 0),
            ContentValid = true,
            Direction = XYZ.AxisX,
            LandingDistance = 8
        };
        var line = new MultiLeaderObjectContextData.LeaderLine
        {
            PathType = MultiLeaderPathType.StraightLineSegments,
            ArrowheadSize = 2.5
        };
        line.Points.Add(new XYZ(50, 60, 0));
        line.Points.Add(new XYZ(65, 70, 0));
        root.Lines.Add(line);
        multiLeader.ContextData.LeaderRoots.Add(root);
        document.Entities.Add(multiLeader);

        using var writer = new DxfWriter(dxfPath, document, false);
        writer.Write();
    }
}
