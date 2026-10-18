using System.Text.Json.Nodes;
using JsonMigrator.Transforms;

namespace JsonMigrator;

/// <summary>
/// Applies a validated <see cref="MigrationPlan"/> to JSON documents.
/// The input may be a single object or an array of objects (migrated record by record).
/// </summary>
public sealed class MigrationEngine
{
    private readonly MigrationPlan _plan;
    private readonly IReadOnlyList<(MappingRule Rule, IReadOnlyList<ITransform> Transforms)> _compiled;

    /// <summary>Compiles the plan's transform specs up front so bad specs fail fast.</summary>
    public MigrationEngine(MigrationPlan plan)
    {
        _plan = plan;
        _compiled = plan.Mappings
            .Select(r => (Rule: r, Transforms: TransformRegistry.ParseAll(r.Transforms)))
            .ToList();
    }

    /// <summary>Migrates <paramref name="input"/>, returning the output document and its report.</summary>
    public (JsonNode Output, MigrationReport Report) Migrate(JsonNode input)
    {
        var report = new MigrationReport(_plan.Name);

        if (input is JsonArray array)
        {
            var output = new JsonArray();
            foreach (var item in array)
            {
                output.Add(MigrateRecord(item, report));
                report.RecordProcessed();
            }
            return (output, report);
        }

        var single = MigrateRecord(input, report);
        report.RecordProcessed();
        return (single, report);
    }

    private JsonNode MigrateRecord(JsonNode? input, MigrationReport report)
    {
        var ctx = new TransformContext { Record = input, Lookups = _plan.Lookups };
        var output = new JsonObject();

        foreach (var (rule, transforms) in _compiled)
        {
            var sourceValue = PathNavigator.Get(input, rule.Source);
            if (sourceValue is null)
            {
                HandleMissing(rule, output, report);
                continue;
            }

            try
            {
                var value = sourceValue.DeepClone();
                foreach (var transform in transforms)
                    value = transform.Apply(value, ctx);

                PathNavigator.Set(output, rule.Target, value);
                report.AddChange(new FieldChange(
                    rule.Source, rule.Target, sourceValue.DeepClone(), value?.DeepClone(), ChangeKind.Mapped));
            }
            catch (TransformException ex)
            {
                report.AddChange(new FieldChange(
                    rule.Source, rule.Target, sourceValue.DeepClone(), null, ChangeKind.Error, ex.Message));
            }
        }

        CopyUnmappedFields(input, output, report);
        ApplyDrops(output, report);

        return output;
    }

    private void HandleMissing(MappingRule rule, JsonObject output, MigrationReport report)
    {
        if (rule.Required || rule.OnMissing == MissingFieldBehavior.Error)
        {
            report.AddChange(new FieldChange(
                rule.Source, rule.Target, null, null, ChangeKind.Error,
                $"required source field '{rule.Source}' is missing."));
            return;
        }

        if (rule.OnMissing == MissingFieldBehavior.UseDefault && rule.DefaultValue is not null)
        {
            PathNavigator.Set(output, rule.Target, rule.DefaultValue);
            report.AddChange(new FieldChange(
                rule.Source, rule.Target, null, rule.DefaultValue.DeepClone(), ChangeKind.Defaulted));
        }
        // Skip: omit the target field entirely.
    }

    private void CopyUnmappedFields(JsonNode? input, JsonObject output, MigrationReport report)
    {
        if (_plan.DropUnmapped || input is not JsonObject obj)
            return;

        // A mapping consumes its source's top-level key (e.g. "address.city" consumes "address").
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rule, _) in _compiled)
            consumed.Add(rule.Source.Split('.')[0].Split('[')[0]);

        foreach (var (key, value) in obj)
        {
            if (consumed.Contains(key))
                continue;
            output[key] = value?.DeepClone();
        }
    }

    private void ApplyDrops(JsonObject output, MigrationReport report)
    {
        foreach (var drop in _plan.Drops)
        {
            // Drops apply to the output: explicit drops plus anything copied through unmapped.
            var existing = PathNavigator.Get(output, drop);
            if (existing is not null)
            {
                PathNavigator.Set(output, drop, null);
                report.AddChange(new FieldChange(drop, "", existing.DeepClone(), null, ChangeKind.Dropped));
            }
        }
    }
}
