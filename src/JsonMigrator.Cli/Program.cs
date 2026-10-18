using System.Text.Json;
using System.Text.Json.Nodes;
using JsonMigrator;

namespace JsonMigrator.Cli;

/// <summary>
/// jsonmigrate --map plan.json --input data.json [--output out.json]
///              [--dry-run] [--report report.json] [--compact] [--strict]
///
/// Exit codes: 0 = success, 1 = usage or I/O error, 2 = migration produced errors (--strict).
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var options = ParseArgs(args);
        if (options is null)
        {
            PrintUsage();
            return 1;
        }

        MigrationPlan plan;
        try
        {
            plan = PlanLoader.Load(options.MapPath);
        }
        catch (PlanValidationException ex)
        {
            Console.Error.WriteLine($"Invalid plan: {ex.Message}");
            return 1;
        }

        JsonNode? input;
        try
        {
            input = JsonNode.Parse(File.ReadAllText(options.InputPath));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Console.Error.WriteLine($"Could not read input '{options.InputPath}': {ex.Message}");
            return 1;
        }

        if (input is null)
        {
            Console.Error.WriteLine("Input file is empty.");
            return 1;
        }

        var engine = new MigrationEngine(plan);
        var (output, report) = engine.Migrate(input);

        Console.WriteLine(report.Summarize());

        if (options.DryRun)
        {
            PrintChanges(report);
        }
        else
        {
            var json = output.ToJsonString(new JsonSerializerOptions { WriteIndented = !options.Compact });
            try
            {
                File.WriteAllText(options.OutputPath!, json + Environment.NewLine);
                Console.WriteLine($"Wrote {options.OutputPath}");
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Could not write output: {ex.Message}");
                return 1;
            }
        }

        if (options.ReportPath is not null)
        {
            try
            {
                File.WriteAllText(options.ReportPath,
                    report.ToJson().ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                    + Environment.NewLine);
                Console.WriteLine($"Wrote report {options.ReportPath}");
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Could not write report: {ex.Message}");
                return 1;
            }
        }

        if (report.HasErrors)
        {
            Console.Error.WriteLine($"{report.Errors} field error(s) — see the report for details.");
            return options.Strict ? 2 : 0;
        }

        return 0;
    }

    private static void PrintChanges(MigrationReport report)
    {
        Console.WriteLine("--- dry run: field changes ---");
        foreach (var change in report.Changes)
        {
            var detail = change.Kind switch
            {
                ChangeKind.Mapped => $"{change.Source} -> {change.Target} = {change.After?.ToJsonString()}",
                ChangeKind.Defaulted => $"{change.Target} defaulted to {change.After?.ToJsonString()}",
                ChangeKind.Dropped => $"{change.Source} dropped",
                ChangeKind.Error => $"ERROR {change.Source}: {change.Message}",
                _ => change.Source
            };
            Console.WriteLine($"  [{change.Kind.ToString().ToLowerInvariant()}] {detail}");
        }
    }

    private static Options? ParseArgs(string[] args)
    {
        string? map = null, input = null, output = null, report = null;
        var dryRun = false;
        var compact = false;
        var strict = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--map" when i + 1 < args.Length: map = args[++i]; break;
                case "--input" when i + 1 < args.Length: input = args[++i]; break;
                case "--output" when i + 1 < args.Length: output = args[++i]; break;
                case "--report" when i + 1 < args.Length: report = args[++i]; break;
                case "--dry-run": dryRun = true; break;
                case "--compact": compact = true; break;
                case "--strict": strict = true; break;
                case "--help" or "-h": return null;
                default:
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                    return null;
            }
        }

        if (map is null || input is null)
        {
            Console.Error.WriteLine("Both --map and --input are required.");
            return null;
        }
        if (!dryRun && output is null)
        {
            Console.Error.WriteLine("--output is required unless --dry-run is given.");
            return null;
        }

        return new Options(map, input, output, report, dryRun, compact, strict);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage: jsonmigrate --map plan.json --input data.json [options]

              --output out.json   Write the migrated document here (required unless --dry-run)
              --dry-run           Print the field-level changes without writing output
              --report r.json     Write the migration report as JSON
              --compact           Minified output instead of indented
              --strict            Exit 2 when any field fails to migrate
            """);
    }

    private sealed record Options(
        string MapPath,
        string InputPath,
        string? OutputPath,
        string? ReportPath,
        bool DryRun,
        bool Compact,
        bool Strict);
}
