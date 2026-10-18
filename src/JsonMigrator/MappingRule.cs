using System.Text.Json.Nodes;
using JsonMigrator.Transforms;

namespace JsonMigrator;

/// <summary>
/// What to do when the source field of a mapping is absent from the input document.
/// </summary>
public enum MissingFieldBehavior
{
    /// <summary>Omit the target field from the output.</summary>
    Skip,
    /// <summary>Use the rule's <see cref="MappingRule.DefaultValue"/> instead.</summary>
    UseDefault,
    /// <summary>Record a migration error and omit the field.</summary>
    Error
}

/// <summary>
/// One field mapping: read a value at <see cref="Source"/>, run it through
/// <see cref="Transforms"/>, and write the result at <see cref="Target"/>.
/// </summary>
public sealed class MappingRule
{
    /// <summary>Dotted path of the source field, e.g. "user.first_name".</summary>
    public required string Source { get; init; }
    /// <summary>Dotted path of the target field, e.g. "profile.firstName".</summary>
    public required string Target { get; init; }

    /// <summary>Transform specs applied in order, e.g. "trim", "coerce:int", "lookup:country".</summary>
    public IReadOnlyList<string> Transforms { get; init; } = Array.Empty<string>();

    /// <summary>When true, a missing source field is always a migration error.</summary>
    public bool Required { get; init; }

    /// <summary>Behavior when the source field is missing from the input.</summary>
    public MissingFieldBehavior OnMissing { get; init; } = MissingFieldBehavior.Skip;

    /// <summary>Literal JSON used when <see cref="OnMissing"/> is <see cref="MissingFieldBehavior.UseDefault"/>.</summary>
    public JsonNode? DefaultValue { get; init; }

    /// <summary>
    /// Validates the structural parts of this rule plus every transform spec,
    /// throwing <see cref="PlanValidationException"/> with every problem found.
    /// </summary>
    public void Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Source))
            errors.Add("mapping is missing 'source'.");
        if (string.IsNullOrWhiteSpace(Target))
            errors.Add($"mapping '{Source}' is missing 'target'.");
        if (OnMissing == MissingFieldBehavior.UseDefault && DefaultValue is null)
            errors.Add($"mapping '{Source}': onMissing is 'default' but no 'default' value is set.");
        try { TransformRegistry.ParseAll(Transforms); }
        catch (PlanValidationException ex)
        {
            errors.AddRange(ex.Errors.Select(e => $"mapping '{Source}': {e}"));
        }
        if (errors.Count > 0)
            throw new PlanValidationException(errors);
    }
}
