using System.Text.Json.Nodes;

namespace JsonMigrator;

/// <summary>
/// A complete migration: an ordered set of field mappings plus global options,
/// loaded from a JSON plan file.
/// </summary>
public sealed class MigrationPlan
{
    /// <summary>Human-readable plan name, defaults to the plan file name.</summary>
    public string Name { get; init; } = "migration";
    /// <summary>Plan format version.</summary>
    public int Version { get; init; } = 1;
    /// <summary>Ordered field mappings applied to every input record.</summary>
    public IReadOnlyList<MappingRule> Mappings { get; init; } = Array.Empty<MappingRule>();

    /// <summary>Source paths copied to no target are removed from the output.</summary>
    public IReadOnlyList<string> Drops { get; init; } = Array.Empty<string>();

    /// <summary>When true, source fields with no mapping are removed instead of copied through.</summary>
    public bool DropUnmapped { get; init; }

    /// <summary>Named lookup tables usable by the "lookup:table" transform.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Lookups { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, string>>();

    /// <summary>Runs every structural check and throws <see cref="PlanValidationException"/> on failure.</summary>
    public void Validate()
    {
        var errors = new List<string>();

        if (Mappings.Count == 0)
            errors.Add("plan has no 'mappings'.");

        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in Mappings)
        {
            try { rule.Validate(); }
            catch (PlanValidationException ex) { errors.AddRange(ex.Errors); }

            if (!string.IsNullOrWhiteSpace(rule.Target) && !targets.Add(rule.Target))
                errors.Add($"duplicate target path '{rule.Target}'.");
        }

        foreach (var drop in Drops)
            if (string.IsNullOrWhiteSpace(drop))
                errors.Add("plan has an empty entry in 'drops'.");

        if (errors.Count > 0)
            throw new PlanValidationException(errors);
    }
}
