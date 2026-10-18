namespace JsonMigrator.Transforms;

/// <summary>
/// Parses transform specs from migration plans ("trim", "coerce:int",
/// "lookup:country", "compute:$price * $qty") into executable transforms.
/// </summary>
public static class TransformRegistry
{
    /// <summary>Parses one spec; throws <see cref="TransformException"/> when unknown or malformed.</summary>
    public static ITransform Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            throw new TransformException("empty transform spec.");

        var colon = spec.IndexOf(':');
        var name = (colon < 0 ? spec : spec[..colon]).Trim().ToLowerInvariant();
        var argument = colon < 0 ? null : spec[(colon + 1)..];

        return BuiltInTransforms.Create(name, argument);
    }

    /// <summary>Parses every spec, collecting all problems before throwing.</summary>
    public static IReadOnlyList<ITransform> ParseAll(IEnumerable<string> specs)
    {
        var transforms = new List<ITransform>();
        var errors = new List<string>();
        foreach (var spec in specs)
        {
            try { transforms.Add(Parse(spec)); }
            catch (TransformException ex) { errors.Add($"transform '{spec}': {ex.Message}"); }
        }
        if (errors.Count > 0)
            throw new PlanValidationException(errors);
        return transforms;
    }
}
