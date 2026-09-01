using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
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
        var document = new global::ACadSharp.CadDocument();
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

    public static void WriteLayoutDxf(string dxfPath)
    {
        var document = new global::ACadSharp.CadDocument();
        document.CreateDefaults();
        document.Entities.Add(new Line { StartPoint = new XYZ(-100, 0, 0), EndPoint = new XYZ(100, 0, 0) });

        var layout = new Layout("SheetV060")
        {
            TabOrder = 2,
            PaperWidth = 200,
            PaperHeight = 100,
            MinLimits = new XY(0, 0),
            MaxLimits = new XY(200, 100),
            MinExtents = new XYZ(0, 0, 0),
            MaxExtents = new XYZ(200, 100, 0)
        };
        layout.UpdatePaperViewport();
        layout.AssociatedBlock.Entities.Add(new Line { StartPoint = new XYZ(10, 10, 0), EndPoint = new XYZ(40, 10, 0) });
        layout.AddViewport(new Viewport
        {
            ActiveStatus = 2,
            Center = new XYZ(100, 50, 0),
            Width = 100,
            Height = 50,
            ViewCenter = new XY(0, 0),
            ViewTarget = XYZ.Zero,
            ViewHeight = 50,
            TwistAngle = 0
        });
        document.Layouts.Add(layout);

        using var writer = new DxfWriter(dxfPath, document, false);
        writer.Write();
    }

    public static void WriteTextFidelityDxf(string dxfPath)
    {
        var document = new global::ACadSharp.CadDocument();
        document.CreateDefaults();

        var shx = new TextStyle("CadCoreSHX")
        {
            Filename = "simplex.shx",
            Width = .8,
            ObliqueAngle = .05
        };
        var ttf = new TextStyle("CadCoreTTF")
        {
            Filename = "simhei.ttf"
        };
        document.TextStyles.Add(shx);
        document.TextStyles.Add(ttf);

        document.Entities.Add(new TextEntity
        {
            Value = "ROOM%%d",
            InsertPoint = new XYZ(10, 20, 0),
            AlignmentPoint = new XYZ(100, 50, 0),
            Height = 10,
            HorizontalAlignment = TextHorizontalAlignment.Center,
            VerticalAlignment = TextVerticalAlignmentType.Middle,
            WidthFactor = .9,
            ObliqueAngle = .2,
            Mirror = TextMirrorFlag.Backward,
            Style = shx
        });

        document.Entities.Add(new MText("第一行\\P第二行")
        {
            InsertPoint = new XYZ(30, 40, 0),
            AlignmentPoint = XYZ.AxisX,
            Height = 5,
            AttachmentPoint = AttachmentPointType.BottomRight,
            RectangleWidth = 42,
            LineSpacing = 1.5,
            Style = ttf
        });

        using var writer = new DxfWriter(dxfPath, document, false);
        writer.Write();
    }
}
