using System.Globalization;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Partial native evidence shared by Tianzheng straight-flight and double-flight stairs. A published
/// AutoLISP property table maps raw DXF group 40 to 踏步高度 (step/riser height) for both TCH_LINESTAIR
/// and TCH_RECTSTAIR. Other stair dimensions, counts, rotation, floor markers and native geometry remain
/// intentionally undecoded until their raw mappings receive independent validation.
/// </summary>
public sealed record CadTianzhengStairStepSemantic(
    string StairEntityType,
    double StepHeight,
    string DecoderProfile)
    : CadCustomSemantic(DecoderProfile);

/// <summary>
/// Narrow evidence-gated decoder for Tianzheng stair objects. The profile accepts only exact LINESTAIR or
/// RECTSTAIR identity and one unique, positive, finite group-40 value. Conflicting source/CLASSES identities,
/// repeated group 40, malformed values and truncated payloads fail closed.
/// </summary>
public static class CadTianzhengStairSemanticDecoder
{
    public const string LineStairStepHeightProfile = "TCH_LINESTAIR_STEP_HEIGHT_40";
    public const string RectStairStepHeightProfile = "TCH_RECTSTAIR_STEP_HEIGHT_40";

    public static CadTianzhengStairStepSemantic? Decode(
        string sourceEntityType,
        CadCustomClassDefinition? classDefinition,
        CadDxfCustomPayload? payload)
    {
        if (payload is null || payload.IsTruncated) return null;

        var sourceKind = StairKind(sourceEntityType);
        var classKind = StairKind(classDefinition?.DxfName);
        if (sourceKind is null && classKind is null) return null;
        if (sourceKind is not null && classKind is not null
            && !string.Equals(sourceKind, classKind, StringComparison.Ordinal))
        {
            return null;
        }

        var kind = sourceKind ?? classKind!;
        var heightGroups = payload.Groups.Where(group => group.Code == 40).Take(2).ToArray();
        if (heightGroups.Length != 1
            || !double.TryParse(
                heightGroups[0].RawValue.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var stepHeight)
            || !double.IsFinite(stepHeight)
            || stepHeight <= 0)
        {
            return null;
        }

        var profile = string.Equals(kind, "TCH_LINESTAIR", StringComparison.Ordinal)
            ? LineStairStepHeightProfile
            : RectStairStepHeightProfile;
        return new CadTianzhengStairStepSemantic(kind, stepHeight, profile);
    }

    private static string? StairKind(string? value)
    {
        if (string.Equals(value, "TCH_LINESTAIR", StringComparison.OrdinalIgnoreCase))
            return "TCH_LINESTAIR";
        if (string.Equals(value, "TCH_RECTSTAIR", StringComparison.OrdinalIgnoreCase))
            return "TCH_RECTSTAIR";
        return null;
    }
}
