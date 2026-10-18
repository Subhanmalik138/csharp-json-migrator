using System.Text.Json.Nodes;

namespace JsonMigrator.Transforms;

/// <summary>
/// Context a transform runs in: the current input record (for cross-field
/// references) and the plan's lookup tables.
/// </summary>
public sealed class TransformContext
{
    /// <summary>The current input record, for cross-field references.</summary>
    public required JsonNode? Record { get; init; }
    /// <summary>Named lookup tables from the migration plan.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Lookups { get; init; }
}

/// <summary>A single named field transform, e.g. trim, coerce:int, lookup:country.</summary>
public interface ITransform
{
    /// <summary>Canonical spec name, e.g. "coerce:int".</summary>
    string Name { get; }
    /// <summary>Applies the transform to <paramref name="value"/> within <paramref name="ctx"/>.</summary>
    JsonNode? Apply(JsonNode? value, TransformContext ctx);
}
