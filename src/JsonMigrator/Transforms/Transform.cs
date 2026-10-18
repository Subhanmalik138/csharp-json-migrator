using System.Text.Json.Nodes;

namespace JsonMigrator.Transforms;

/// <summary>
/// Context a transform runs in: the current input record (for cross-field
/// references) and the plan's lookup tables.
/// </summary>
public sealed class TransformContext
{
    public required JsonNode? Record { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Lookups { get; init; }
}

/// <summary>A single named field transform, e.g. trim, coerce:int, lookup:country.</summary>
public interface ITransform
{
    string Name { get; }
    JsonNode? Apply(JsonNode? value, TransformContext ctx);
}
