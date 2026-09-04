using System.Text;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

return await XiangyuanCorpusProbe.RunAsync(args);

internal enum XiangyuanProbeMode
{
    StrictCorpus,
    Discovery,
    ConversionDiff,
    ConversionConsensus
}

internal static class XiangyuanCorpusProbe
{
    private const string Usage = "Usage: XiangyuanProbe [--discovery | --conversion-diff | --conversion-consensus] --out <report.json> <inputs...>; conversion diff requires <native.dwg> <converted.dwg>, conversion consensus requires 2+ conversion-diff JSON reports";

    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!TryParseArguments(args, out var outputPath, out var inputs, out var mode))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var prefix = mode switch
        {
            XiangyuanProbeMode.Discovery => "XYDISCOVERY",
            XiangyuanProbeMode.ConversionDiff => "XYCONVERSION",
            XiangyuanProbeMode.ConversionConsensus => "XYCONSENSUS",
            _ => "XYCORPUS"
        };

        string json;
        string summary;
        if (mode == XiangyuanProbeMode.ConversionConsensus)
        {
            var reports = new List<CadXiangyuanConversionDiffReport>(inputs.Count);
            for (var index = 0; index < inputs.Count; index++)
            {
                try
                {
                    var reportJson = await File.ReadAllTextAsync(inputs[index]);
                    reports.Add(CadXiangyuanConversionDiffer.FromJson(reportJson));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or FormatException or NotSupportedException)
                {
                    Console.Error.WriteLine($"[{prefix}] Input={index + 1} Status=ReadFailed Type={exception.GetType().Name}");
                    return 3;
                }
            }

            CadXiangyuanConversionConsensusReport consensus;
            try
            {
                consensus = CadXiangyuanConversionConsensus.Build(reports);
                json = CadXiangyuanConversionConsensus.ToJson(consensus);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                Console.Error.WriteLine($"[{prefix}] Status=ConsensusFailed Type={exception.GetType().Name}");
                return 3;
            }
            summary = $"Pairs={consensus.PairCount} Classes={consensus.Classes.Count} Profiles={consensus.Profiles.Count} RepeatedUnknownEntityCandidates={consensus.RepeatedRemovedUnknownEntityCandidateCount} RepeatedUnknownProfileCandidates={consensus.RepeatedRemovedUnknownProfileCandidateCount}";
        }
        else
        {
            var importer = new ACadSharpCadImporter();
            var strictReports = mode == XiangyuanProbeMode.StrictCorpus
                ? new List<CadXiangyuanSchemaCorpusReport>(inputs.Count)
                : null;
            var discoveryReports = mode == XiangyuanProbeMode.StrictCorpus
                ? null
                : new List<CadXiangyuanDiscoveryReport>(inputs.Count);

            for (var index = 0; index < inputs.Count; index++)
            {
                var input = inputs[index];
                if (!importer.CanImport(input))
                {
                    Console.Error.WriteLine($"[{prefix}] Input={index + 1} Status=UnsupportedExtension");
                    return 2;
                }

                ImportResult result;
                try
                {
                    result = await importer.ImportAsync(new ImportRequest(input));
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    Console.Error.WriteLine($"[{prefix}] Input={index + 1} Status=ImportException Type={exception.GetType().Name}");
                    return 3;
                }

                if (result.Document is not CadDocument document)
                {
                    var codes = string.Join(',', result.Diagnostics
                        .Select(diagnostic => diagnostic.Code)
                        .Where(code => !string.IsNullOrWhiteSpace(code))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(code => code, StringComparer.Ordinal));
                    Console.Error.WriteLine($"[{prefix}] Input={index + 1} Status=ImportFailed DiagnosticCodes={codes}");
                    return 3;
                }

                if (mode == XiangyuanProbeMode.StrictCorpus)
                    strictReports!.Add(CadXiangyuanSchemaCorpus.Build(document));
                else
                    discoveryReports!.Add(CadXiangyuanDiscoveryCorpus.Build(document));
            }

            switch (mode)
            {
                case XiangyuanProbeMode.Discovery:
                {
                    var merged = CadXiangyuanDiscoveryCorpus.Merge(discoveryReports!);
                    json = CadXiangyuanDiscoveryCorpus.ToJson(merged);
                    summary = $"Samples={merged.SampleCount} Classes={merged.Classes.Count} Profiles={merged.Profiles.Count} CustomEntities={merged.CustomEntityCount} KnownXiangyuanEntities={merged.KnownXiangyuanEntityCount} UnknownVendorEntities={merged.UnknownVendorEntityCount}";
                    break;
                }
                case XiangyuanProbeMode.ConversionDiff:
                {
                    var diff = CadXiangyuanConversionDiffer.Compare(discoveryReports![0], discoveryReports[1]);
                    json = CadXiangyuanConversionDiffer.ToJson(diff);
                    summary = $"RemovedClasses={diff.RemovedClassCount} RemovedProfiles={diff.RemovedProfileCount} RetainedClasses={diff.RetainedClassCount} RetainedProfiles={diff.RetainedProfileCount}";
                    break;
                }
                default:
                {
                    var merged = CadXiangyuanSchemaCorpus.Merge(strictReports!);
                    json = CadXiangyuanSchemaCorpus.ToJson(merged);
                    summary = $"Samples={merged.SampleCount} Profiles={merged.Entries.Count} Entities={merged.EntityCount} XiangyuanDetected={(merged.EntityCount > 0)}";
                    break;
                }
            }
        }

        try
        {
            WriteReport(outputPath, json, inputs);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"[{prefix}] Status=WriteFailed Type={exception.GetType().Name}");
            return 4;
        }

        Console.WriteLine($"[{prefix}] Status=OK {summary}");
        return 0;
    }

    private static bool TryParseArguments(
        string[] args,
        out string outputPath,
        out List<string> inputs,
        out XiangyuanProbeMode mode)
    {
        outputPath = string.Empty;
        inputs = new List<string>();
        mode = XiangyuanProbeMode.StrictCorpus;
        var modeSeen = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(argument, "--discovery", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--conversion-diff", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--conversion-consensus", StringComparison.OrdinalIgnoreCase))
            {
                if (modeSeen) return false;
                mode = argument.ToLowerInvariant() switch
                {
                    "--discovery" => XiangyuanProbeMode.Discovery,
                    "--conversion-diff" => XiangyuanProbeMode.ConversionDiff,
                    _ => XiangyuanProbeMode.ConversionConsensus
                };
                modeSeen = true;
                continue;
            }

            if (string.Equals(argument, "--out", StringComparison.OrdinalIgnoreCase))
            {
                if (outputPath.Length > 0 || index + 1 >= args.Length) return false;
                outputPath = args[++index];
                continue;
            }

            if (argument.StartsWith('-')) return false;
            inputs.Add(argument);
        }

        if (outputPath.Length == 0 || inputs.Count == 0) return false;
        if (mode == XiangyuanProbeMode.ConversionDiff) return inputs.Count == 2;
        if (mode == XiangyuanProbeMode.ConversionConsensus) return inputs.Count >= 2;
        return true;
    }

    private static void WriteReport(
        string outputPath,
        string json,
        List<string> inputs)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        foreach (var input in inputs)
        {
            if (string.Equals(Path.GetFullPath(input), fullOutputPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output path cannot overwrite an input file.", nameof(outputPath));
        }

        var directory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException("Output directory does not exist.");

        File.WriteAllText(fullOutputPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
