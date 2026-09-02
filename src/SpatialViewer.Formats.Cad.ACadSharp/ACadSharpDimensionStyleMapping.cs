using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

public sealed partial class ACadSharpCadImporter
{
    private static CadDimensionPresentation MapDimensionPresentation(object? style)
    {
        var scale = Positive(DoubleProperty(style, "ScaleFactor", 1));
        return new CadDimensionPresentation(
            ExtensionLineOffset: DoubleProperty(style, "ExtensionLineOffset") * scale,
            ExtensionLineExtension: DoubleProperty(style, "ExtensionLineExtension") * scale,
            DimensionLineExtension: DoubleProperty(style, "DimensionLineExtension") * scale,
            DimensionLineGap: Math.Abs(DoubleProperty(style, "DimensionLineGap")) * scale,
            SuppressFirstExtensionLine: BoolProperty(style, "SuppressFirstExtensionLine"),
            SuppressSecondExtensionLine: BoolProperty(style, "SuppressSecondExtensionLine"),
            SuppressFirstDimensionLine: BoolProperty(style, "SuppressFirstDimensionLine"),
            SuppressSecondDimensionLine: BoolProperty(style, "SuppressSecondDimensionLine"),
            ArrowBlockName: TableEntryName(Property(style, "ArrowBlock")),
            FirstArrowBlockName: TableEntryName(Property(style, "DimArrow1")),
            SecondArrowBlockName: TableEntryName(Property(style, "DimArrow2")),
            SeparateArrowBlocks: BoolProperty(style, "SeparateArrowBlocks"),
            DecimalPlaces: IntProperty(style, "DecimalPlaces", 2),
            DecimalSeparator: CharProperty(style, "DecimalSeparator", '.'),
            Rounding: DoubleProperty(style, "Rounding"),
            Prefix: StringProperty(style, "Prefix"),
            Suffix: StringProperty(style, "Suffix"),
            GenerateTolerances: BoolProperty(style, "GenerateTolerances"),
            LimitsGeneration: BoolProperty(style, "LimitsGeneration"),
            PlusTolerance: DoubleProperty(style, "PlusTolerance"),
            MinusTolerance: DoubleProperty(style, "MinusTolerance"),
            ToleranceDecimalPlaces: IntProperty(style, "ToleranceDecimalPlaces", 2),
            ToleranceScaleFactor: Positive(DoubleProperty(style, "ToleranceScaleFactor", 1)),
            AlternateUnitsEnabled: BoolProperty(style, "AlternateUnitDimensioning"),
            AlternateUnitScaleFactor: DoubleProperty(style, "AlternateUnitScaleFactor", 25.4),
            AlternateUnitDecimalPlaces: IntProperty(style, "AlternateUnitDecimalPlaces", 3),
            AlternateUnitSuffix: StringProperty(style, "AlternateDimensioningSuffix"),
            LinearUnitFormat: StringProperty(style, "LinearUnitFormat"),
            AngularUnitFormat: StringProperty(style, "AngularUnit"));
    }

    private static string TableEntryName(object? entry) => entry is null ? string.Empty : StringProperty(entry, "Name");

    private static char CharProperty(object? source, string name, char fallback)
    {
        var value = Property(source, name);
        return value switch
        {
            char character => character,
            string text when text.Length > 0 => text[0],
            _ => fallback
        };
    }
}
