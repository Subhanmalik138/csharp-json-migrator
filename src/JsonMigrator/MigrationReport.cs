using System.Text.Json.Nodes;

namespace JsonMigrator;

/// <summary>What happened to a single field during migration.</summary>
public enum ChangeKind
{
    Mapped,
    Defaulted,
    Dropped,
    Error
}

/// <summary>One field-level change recorded while migrating a record.</summary>
public sealed record FieldChange(
    string Source,
    string Target,
    JsonNode? Before,
    JsonNode? After,
    ChangeKind Kind,
    string? Message = null);

/// <summary>
/// Aggregated result of a migration run: per-field changes plus counters.
/// Serializes to JSON for the CLI's --report output.
/// </summary>
public sealed class MigrationReport
{
    private readonly List<FieldChange> _changes = new();

    /// <summary>Plan name this report belongs to.</summary>
    public string PlanName { get; }

    /// <summary>Number of input records processed.</summary>
    public int RecordsProcessed { get; private set; }

    /// <summary>Every field-level change, in processing order.</summary>
    public IReadOnlyList<FieldChange> Changes => _changes;

    public int FieldsMapped => _changes.Count(c => c.Kind == ChangeKind.Mapped);
    public int FieldsDefaulted => _changes.Count(c => c.Kind == ChangeKind.Defaulted);
    public int FieldsDropped => _changes.Count(c => c.Kind == ChangeKind.Dropped);
    public int Errors => _changes.Count(c => c.Kind == ChangeKind.Error);

    /// <summary>True when any field recorded an error.</summary>
    public bool HasErrors => Errors > 0;

    public MigrationReport(string planName) => PlanName = planName;

    /// <summary>Records one processed input record.</summary>
    public void RecordProcessed() => RecordsProcessed++;

    /// <summary>Appends a field-level change.</summary>
    public void AddChange(FieldChange change) => _changes.Add(change);

    /// <summary>One-line human summary, e.g. "3 records, 12 mapped, 1 defaulted, 2 dropped, 0 errors".</summary>
    public string Summarize() =>
        $"{RecordsProcessed} record(s), {FieldsMapped} mapped, {FieldsDefaulted} defaulted, " +
        $"{FieldsDropped} dropped, {Errors} error(s)";

    /// <summary>Serializes the report (without per-field Before/After payloads) to a JSON object.</summary>
    public JsonObject ToJson()
    {
        var changes = new JsonArray();
        foreach (var c in _changes)
        {
            changes.Add(new JsonObject
            {
                ["source"] = c.Source,
                ["target"] = c.Target,
                ["kind"] = c.Kind.ToString().ToLowerInvariant(),
                ["message"] = c.Message
            });
        }

        return new JsonObject
        {
            ["plan"] = PlanName,
            ["recordsProcessed"] = RecordsProcessed,
            ["fieldsMapped"] = FieldsMapped,
            ["fieldsDefaulted"] = FieldsDefaulted,
            ["fieldsDropped"] = FieldsDropped,
            ["errors"] = Errors,
            ["changes"] = changes
        };
    }
}
