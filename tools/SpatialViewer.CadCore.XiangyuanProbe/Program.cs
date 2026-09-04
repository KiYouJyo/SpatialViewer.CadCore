using System.Text;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

return await XiangyuanCorpusProbe.RunAsync(args);

internal static class XiangyuanCorpusProbe
{
    private const string Usage = "Usage: XiangyuanProbe --out <report.json> <drawing1.dwg|dxf> [drawing2.dwg|dxf ...]";

    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!TryParseArguments(args, out var outputPath, out var inputs))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var importer = new ACadSharpCadImporter();
        var reports = new List<CadXiangyuanSchemaCorpusReport>(inputs.Count);
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            if (!importer.CanImport(input))
            {
                Console.Error.WriteLine($"[XYCORPUS] Input={index + 1} Status=UnsupportedExtension");
                return 2;
            }

            ImportResult result;
            try
            {
                result = await importer.ImportAsync(new ImportRequest(input));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"[XYCORPUS] Input={index + 1} Status=ImportException Type={exception.GetType().Name}");
                return 3;
            }

            if (result.Document is not CadDocument document)
            {
                var codes = string.Join(',', result.Diagnostics
                    .Select(diagnostic => diagnostic.Code)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(code => code, StringComparer.Ordinal));
                Console.Error.WriteLine($"[XYCORPUS] Input={index + 1} Status=ImportFailed DiagnosticCodes={codes}");
                return 3;
            }

            reports.Add(CadXiangyuanSchemaCorpus.Build(document));
        }

        var merged = CadXiangyuanSchemaCorpus.Merge(reports);
        var json = CadXiangyuanSchemaCorpus.ToJson(merged);
        try
        {
            WriteReport(outputPath, json, inputs);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"[XYCORPUS] Status=WriteFailed Type={exception.GetType().Name}");
            return 4;
        }

        Console.WriteLine(
            $"[XYCORPUS] Status=OK Samples={merged.SampleCount} Profiles={merged.Entries.Count} Entities={merged.EntityCount} XiangyuanDetected={(merged.EntityCount > 0)}");
        return 0;
    }

    private static bool TryParseArguments(
        string[] args,
        out string outputPath,
        out List<string> inputs)
    {
        outputPath = string.Empty;
        inputs = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(argument, "--out", StringComparison.OrdinalIgnoreCase))
            {
                if (outputPath.Length > 0 || index + 1 >= args.Length)
                {
                    return false;
                }

                outputPath = args[++index];
                continue;
            }

            if (argument.StartsWith('-'))
            {
                return false;
            }

            inputs.Add(argument);
        }

        return outputPath.Length > 0 && inputs.Count > 0;
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
                throw new ArgumentException("Output path cannot overwrite an input drawing.", nameof(outputPath));
        }

        var directory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException("Output directory does not exist.");

        File.WriteAllText(fullOutputPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
