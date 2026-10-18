using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonMigrator;

/// <summary>
/// Parses a migration plan from its JSON file format into a validated <see cref="MigrationPlan"/>.
/// </summary>
/// <example>
/// {
///   "version": 1,
///   "name": "users-v1-to-v2",
///   "mappings": [
///     { "source": "first_name", "target": "profile.firstName", "transforms": ["trim"] },
///     { "source": "age", "target": "profile.age", "transforms": ["coerce:int"], "onMissing": "default", "default": 0 }
///   ],
///   "drops": ["legacy_id"],
///   "lookups": { "country": { "US": "United States" } }
/// }
/// </example>
public static class PlanLoader
{
    /// <summary>Loads and validates the plan at <paramref name="path"/>.</summary>
    public static MigrationPlan Load(string path)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path), new JsonNodeOptions(), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            throw new PlanValidationException(new[] { $"could not read plan file '{path}': {ex.Message}" });
        }

        if (root is not JsonObject obj)
            throw new PlanValidationException(new[] { "plan root must be a JSON object." });

        var plan = new MigrationPlan
        {
            Name = obj["name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(path),
            Version = obj["version"]?.GetValue<int>() ?? 1,
            Mappings = ParseMappings(obj["mappings"]),
            Drops = obj["drops"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList()
                    ?? new List<string>(),
            DropUnmapped = obj["dropUnmapped"]?.GetValue<bool>() ?? false,
            Lookups = ParseLookups(obj["lookups"])
        };

        plan.Validate();
        return plan;
    }

    private static List<MappingRule> ParseMappings(JsonNode? node)
    {
        var rules = new List<MappingRule>();
        if (node is not JsonArray arr)
            return rules;

        foreach (var item in arr)
        {
            if (item is not JsonObject m)
                throw new PlanValidationException(new[] { "each entry in 'mappings' must be an object." });

            rules.Add(new MappingRule
            {
                Source = m["source"]?.GetValue<string>() ?? "",
                Target = m["target"]?.GetValue<string>() ?? "",
                Transforms = m["transforms"]?.AsArray().Select(t => t?.GetValue<string>() ?? "").ToList()
                             ?? new List<string>(),
                Required = m["required"]?.GetValue<bool>() ?? false,
                OnMissing = ParseMissingBehavior(m["onMissing"]?.GetValue<string>()),
                DefaultValue = m["default"]?.DeepClone()
            });
        }
        return rules;
    }

    private static MissingFieldBehavior ParseMissingBehavior(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "default" => MissingFieldBehavior.UseDefault,
            "error" => MissingFieldBehavior.Error,
            null or "skip" => MissingFieldBehavior.Skip,
            _ => throw new PlanValidationException(new[] { $"unknown onMissing behavior '{value}' (expected skip, default or error)." })
        };

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ParseLookups(JsonNode? node)
    {
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (node is not JsonObject obj)
            return tables;

        foreach (var (name, table) in obj)
        {
            if (table is not JsonObject entries)
                throw new PlanValidationException(new[] { $"lookup table '{name}' must be an object." });
            tables[name] = entries.ToDictionary(
                kv => kv.Key,
                kv => kv.Value?.GetValue<string>() ?? "",
                StringComparer.OrdinalIgnoreCase);
        }
        return tables;
    }
}
