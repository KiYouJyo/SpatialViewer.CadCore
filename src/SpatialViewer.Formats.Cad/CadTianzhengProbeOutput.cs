using System.Collections.ObjectModel;
using System.Globalization;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Privacy-safe structural signature emitted by the in-product TCHSIG probe. It intentionally contains
/// no raw DXF values, handles, subclass names, file paths, or drawing text.
/// </summary>
public sealed record CadTianzhengProbeSignature(
    string DxfName,
    int EntryCount,
    int SubclassMarkerCount,
    IReadOnlyList<int> GroupCodes);

/// <summary>One privacy-safe TCHDIFF observation associated with a Tianzheng object type.</summary>
public sealed record CadTianzhengProbeDiff(
    string DxfName,
    IReadOnlyList<CadDxfCustomPayloadValueChange> ValueChanges);

/// <summary>
/// Stable group slots reported by every independent TCHDIFF observation for one TCHSIG structural signature.
/// This remains research evidence and must not be promoted to a named semantic field without independent proof.
/// </summary>
public sealed record CadTianzhengProbeConsensus(
    CadTianzhengProbeSignature Signature,
    int ObservationCount,
    IReadOnlyList<CadDxfCustomPayloadValueChange> StableValueChanges)
{
    public bool HasStableCandidate => StableValueChanges.Count > 0;
}

/// <summary>
/// Parses the deliberately narrow text protocol produced by tools/tianzheng-probe/TianzhengDiffProbe.lsp.
/// Only structural fields are retained. Other console lines are ignored rather than copied into results.
/// </summary>
public static class CadTianzhengProbeOutputParser
{
    private const int MaxInputChars = 1_048_576;
    private const int MaxEntries = 65_536;
    private const int MaxObservations = 10_000;
    private const int MaxDxfNameLength = 128;
    private const string SigTypePrefix = "[TCHSIG] Object type=";
    private const string SigEntryPrefix = "[TCHSIG] Entry count=";
    private const string SigSubclassPrefix = "[TCHSIG] Subclass marker count=";
    private const string SigCodesPrefix = "[TCHSIG] code-signature=";
    private const string DiffTypePrefix = "[TCHDIFF] Object type=";
    private const string DiffChangePrefix = "[TCHDIFF] changed slot=";

    public static CadTianzhengProbeSignature ParseSignature(string text)
    {
        ValidateInput(text);
        string? dxfName = null;
        int? entryCount = null;
        int? subclassCount = null;
        int[]? groupCodes = null;

        foreach (var rawLine in Lines(text))
        {
            var line = rawLine.Trim();
            if (line.StartsWith(SigTypePrefix, StringComparison.Ordinal))
            {
                RequireUnset(dxfName, "TCHSIG object type");
                dxfName = ParseDxfName(line[SigTypePrefix.Length..]);
            }
            else if (line.StartsWith(SigEntryPrefix, StringComparison.Ordinal))
            {
                if (entryCount is not null) throw Invalid("Duplicate TCHSIG entry count.");
                entryCount = ParseBoundedNonNegativeInt(line[SigEntryPrefix.Length..], MaxEntries, "entry count");
            }
            else if (line.StartsWith(SigSubclassPrefix, StringComparison.Ordinal))
            {
                if (subclassCount is not null) throw Invalid("Duplicate TCHSIG subclass marker count.");
                subclassCount = ParseBoundedNonNegativeInt(line[SigSubclassPrefix.Length..], MaxEntries, "subclass marker count");
            }
            else if (line.StartsWith(SigCodesPrefix, StringComparison.Ordinal))
            {
                if (groupCodes is not null) throw Invalid("Duplicate TCHSIG code signature.");
                groupCodes = ParseCodes(line[SigCodesPrefix.Length..]);
            }
        }

        if (dxfName is null || entryCount is null || subclassCount is null || groupCodes is null)
            throw Invalid("Incomplete TCHSIG output.");
        if (entryCount.Value != groupCodes.Length)
            throw Invalid("TCHSIG entry count does not match the group-code signature length.");
        if (subclassCount.Value > groupCodes.Count(code => code == 100))
            throw Invalid("TCHSIG subclass marker count exceeds group 100 occurrences.");

        return new CadTianzhengProbeSignature(
            dxfName,
            entryCount.Value,
            subclassCount.Value,
            new ReadOnlyCollection<int>(groupCodes));
    }

    public static CadTianzhengProbeDiff ParseDiff(string text)
    {
        ValidateInput(text);
        string? dxfName = null;
        var changes = new List<CadDxfCustomPayloadValueChange>();
        foreach (var rawLine in Lines(text))
        {
            var line = rawLine.Trim();
            if (line.StartsWith(DiffTypePrefix, StringComparison.Ordinal))
            {
                RequireUnset(dxfName, "TCHDIFF object type");
                dxfName = ParseDxfName(line[DiffTypePrefix.Length..]);
            }
            else if (line.StartsWith(DiffChangePrefix, StringComparison.Ordinal))
            {
                if (changes.Count >= MaxEntries) throw Invalid("Too many TCHDIFF changed slots.");
                changes.Add(ParseChange(line[DiffChangePrefix.Length..]));
            }
        }

        if (dxfName is null) throw Invalid("Incomplete TCHDIFF output: object type is missing.");
        var ordered = changes
            .Distinct()
            .OrderBy(change => change.GroupIndex)
            .ThenBy(change => change.Code)
            .ThenBy(change => change.CodeOccurrence)
            .ToArray();
        if (ordered.Length != changes.Count)
            throw Invalid("TCHDIFF output contains duplicate changed slots.");
        return new CadTianzhengProbeDiff(dxfName, new ReadOnlyCollection<CadDxfCustomPayloadValueChange>(ordered));
    }

    public static CadTianzhengProbeConsensus BuildConsensus(
        CadTianzhengProbeSignature signature,
        IEnumerable<CadTianzhengProbeDiff> observations)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(observations);
        ValidateSignature(signature);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent TCHDIFF observations are required.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"At most {MaxObservations} TCHDIFF observations are supported.", nameof(observations));

        HashSet<CadDxfCustomPayloadValueChange>? stable = null;
        foreach (var observation in items)
        {
            ArgumentNullException.ThrowIfNull(observation);
            if (!string.Equals(signature.DxfName, observation.DxfName, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("TCHDIFF object identity does not match the TCHSIG signature.", nameof(observations));
            ValidateChanges(signature, observation.ValueChanges);
            if (stable is null)
                stable = new HashSet<CadDxfCustomPayloadValueChange>(observation.ValueChanges);
            else
                stable.IntersectWith(observation.ValueChanges);
        }

        var ordered = stable!
            .OrderBy(change => change.GroupIndex)
            .ThenBy(change => change.Code)
            .ThenBy(change => change.CodeOccurrence)
            .ToArray();
        return new CadTianzhengProbeConsensus(
            signature,
            items.Count,
            new ReadOnlyCollection<CadDxfCustomPayloadValueChange>(ordered));
    }

    private static void ValidateSignature(CadTianzhengProbeSignature signature)
    {
        _ = ParseDxfName(signature.DxfName);
        if (signature.EntryCount < 0 || signature.EntryCount > MaxEntries)
            throw new ArgumentException("Probe signature entry count is out of range.", nameof(signature));
        if (signature.SubclassMarkerCount < 0 || signature.SubclassMarkerCount > signature.EntryCount)
            throw new ArgumentException("Probe signature subclass marker count is out of range.", nameof(signature));
        if (signature.GroupCodes.Count != signature.EntryCount)
            throw new ArgumentException("Probe signature entry count does not match its group-code signature.", nameof(signature));
        if (signature.GroupCodes.Any(code => code is < -5 or > 1071))
            throw new ArgumentException("Probe signature contains an invalid DXF group code.", nameof(signature));
        if (signature.SubclassMarkerCount > signature.GroupCodes.Count(code => code == 100))
            throw new ArgumentException("Probe signature subclass marker count exceeds group 100 occurrences.", nameof(signature));
    }

    private static void ValidateChanges(
        CadTianzhengProbeSignature signature,
        IReadOnlyList<CadDxfCustomPayloadValueChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var seen = new HashSet<CadDxfCustomPayloadValueChange>();
        foreach (var change in changes)
        {
            if (!seen.Add(change))
                throw new ArgumentException("TCHDIFF observation contains duplicate changed slots.", nameof(changes));
            if (change.GroupIndex < 0 || change.GroupIndex >= signature.GroupCodes.Count)
                throw new ArgumentException("TCHDIFF slot is outside the TCHSIG group-code signature.", nameof(changes));
            if (signature.GroupCodes[change.GroupIndex] != change.Code)
                throw new ArgumentException("TCHDIFF group code does not match the TCHSIG slot.", nameof(changes));
            var expectedOccurrence = 1;
            for (var index = 0; index < change.GroupIndex; index++)
                if (signature.GroupCodes[index] == change.Code) expectedOccurrence++;
            if (change.CodeOccurrence != expectedOccurrence)
                throw new ArgumentException("TCHDIFF group occurrence does not match the TCHSIG signature.", nameof(changes));
        }
    }

    private static CadDxfCustomPayloadValueChange ParseChange(string value)
    {
        const string CodeToken = " code=";
        const string OccurrenceToken = " occurrence=";
        var codeAt = value.IndexOf(CodeToken, StringComparison.Ordinal);
        var occurrenceAt = value.IndexOf(OccurrenceToken, StringComparison.Ordinal);
        if (codeAt <= 0 || occurrenceAt <= codeAt + CodeToken.Length)
            throw Invalid("Malformed TCHDIFF changed-slot line.");

        var index = ParseBoundedNonNegativeInt(value[..codeAt], MaxEntries - 1, "slot");
        var codeTextStart = codeAt + CodeToken.Length;
        var code = ParseInt(value[codeTextStart..occurrenceAt], "group code");
        if (code is < -5 or > 1071) throw Invalid("TCHDIFF group code is out of range.");
        var occurrence = ParseBoundedNonNegativeInt(value[(occurrenceAt + OccurrenceToken.Length)..], MaxEntries, "occurrence");
        if (occurrence == 0) throw Invalid("TCHDIFF occurrence must be at least 1.");
        return new CadDxfCustomPayloadValueChange(index, code, occurrence);
    }

    private static int[] ParseCodes(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<int>();
        var tokens = value.Split(',', StringSplitOptions.None);
        if (tokens.Length > MaxEntries) throw Invalid("TCHSIG code signature is too large.");
        var codes = new int[tokens.Length];
        for (var index = 0; index < tokens.Length; index++)
        {
            if (tokens[index].Length == 0) throw Invalid("TCHSIG code signature contains an empty token.");
            codes[index] = ParseInt(tokens[index], "group code");
            if (codes[index] is < -5 or > 1071) throw Invalid("TCHSIG group code is out of range.");
        }
        return codes;
    }

    private static string ParseDxfName(string value)
    {
        var name = value.Trim();
        if (name.Length is < 5 or > MaxDxfNameLength || !name.StartsWith("TCH_", StringComparison.OrdinalIgnoreCase))
            throw Invalid("Probe object type must be a bounded TCH_* identity.");
        if (name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))
            throw Invalid("Probe object type contains unsupported characters.");
        return name.ToUpperInvariant();
    }

    private static int ParseBoundedNonNegativeInt(string value, int maximum, string field)
    {
        var parsed = ParseInt(value, field);
        if (parsed < 0 || parsed > maximum) throw Invalid($"Probe {field} is out of range.");
        return parsed;
    }

    private static int ParseInt(string value, string field)
        => int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw Invalid($"Probe {field} is not an integer.");

    private static IEnumerable<string> Lines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static void ValidateInput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaxInputChars) throw new ArgumentException("Probe output exceeds the 1 MiB input limit.", nameof(text));
    }

    private static void RequireUnset(string? value, string field)
    {
        if (value is not null) throw Invalid($"Duplicate {field}.");
    }

    private static FormatException Invalid(string message) => new(message);
}
