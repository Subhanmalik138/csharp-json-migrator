namespace JsonMigrator;

/// <summary>Raised when a migration plan fails validation, carrying every problem found.</summary>
public sealed class PlanValidationException : Exception
{
    /// <summary>The raw error messages collected during validation.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Creates a validation exception from the collected error messages.</summary>
    public PlanValidationException(IEnumerable<string> errors)
        : base("Invalid migration plan: " + string.Join("; ", errors))
    {
        Errors = errors.ToList().AsReadOnly();
    }
}

/// <summary>Raised when a transform spec is unknown or malformed.</summary>
public sealed class TransformException : Exception
{
    /// <summary>Creates a transform exception with the given message.</summary>
    public TransformException(string message) : base(message) { }
}
