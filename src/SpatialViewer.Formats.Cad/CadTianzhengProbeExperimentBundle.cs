namespace SpatialViewer.Formats.Cad;

/// <summary>
/// One atomic controlled experiment transcript containing the structural signature of the baseline
/// and the case-bound A/B diff produced from the same validated pair. No raw drawing value is retained.
/// </summary>
public sealed record CadTianzhengProbeExperimentBundle(
    CadTianzhengProbeSignature Signature,
    CadTianzhengProbeExperimentObservation Observation);

/// <summary>
/// Parses the combined protocol emitted by TCHRUN. The existing narrow TCHSIG and TCHDIFF parsers are
/// reused and the changed slots are additionally checked against the signature from the same transcript.
/// </summary>
public static class CadTianzhengProbeExperimentBundleParser
{
    private const int MaxInputChars = 1_048_576;

    public static CadTianzhengProbeExperimentBundle Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaxInputChars)
            throw new ArgumentException("Probe bundle exceeds the 1 MiB input limit.", nameof(text));

        var signature = CadTianzhengProbeOutputParser.ParseSignature(text);
        var observation = CadTianzhengProbeExperimentParser.Parse(text);

        if (!string.Equals(signature.DxfName, observation.ExperimentCase.DxfName, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("TCHRUN signature does not match the declared experiment case.");
        if (!string.Equals(signature.DxfName, observation.Diff.DxfName, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("TCHRUN signature and diff object identities differ.");

        ValidateChangesAgainstSignature(signature, observation.Diff.ValueChanges);
        return new CadTianzhengProbeExperimentBundle(signature, observation);
    }

    private static void ValidateChangesAgainstSignature(
        CadTianzhengProbeSignature signature,
        IReadOnlyList<CadDxfCustomPayloadValueChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var seen = new HashSet<CadDxfCustomPayloadValueChange>();
        foreach (var change in changes)
        {
            if (!seen.Add(change))
                throw new FormatException("TCHRUN diff contains a duplicate changed slot.");
            if (change.GroupIndex < 0 || change.GroupIndex >= signature.GroupCodes.Count)
                throw new FormatException("TCHRUN changed slot is outside the emitted signature.");
            if (signature.GroupCodes[change.GroupIndex] != change.Code)
                throw new FormatException("TCHRUN changed-slot group code does not match the emitted signature.");

            var expectedOccurrence = 1;
            for (var index = 0; index < change.GroupIndex; index++)
                if (signature.GroupCodes[index] == change.Code) expectedOccurrence++;
            if (change.CodeOccurrence != expectedOccurrence)
                throw new FormatException("TCHRUN changed-slot occurrence does not match the emitted signature.");
        }
    }
}
