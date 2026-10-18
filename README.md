# csharp-json-migrator

A declarative JSON data migration tool for .NET 8. You describe *what* the target schema looks like in a JSON plan file — field mappings, transforms, defaults, drops — and the engine migrates your documents, with a per-field change report so you can dry-run before committing.

Built as a clean class library (`JsonMigrator`) plus a thin CLI (`jsonmigrate`).

## Why this exists

Schema migrations on JSON data (config files, exported records, API payloads between service versions) are usually one-off scripts: brittle, untested, and impossible to review. A declarative plan turns the migration into a reviewable artifact — and `--dry-run` shows exactly what would change before anything is written.

## Quick start

```bash
# Build
dotnet build src/JsonMigrator.Cli

# Dry-run: print every field change without writing output
dotnet run --project src/JsonMigrator.Cli -- \
  --map samples/users-plan.json --input samples/users-v1.json --dry-run

# Run for real, with a JSON report
dotnet run --project src/JsonMigrator.Cli -- \
  --map samples/users-plan.json --input samples/users-v1.json \
  --output users-v2.json --report report.json --strict
```

## The plan format

```jsonc
{
  "name": "users-v1-to-v2",
  "mappings": [
    { "source": "first_name", "target": "profile.firstName", "transforms": ["trim"] },
    { "source": "age", "target": "profile.age", "transforms": ["coerce:int"],
      "onMissing": "default", "default": 0 },
    { "source": "email", "target": "contact.email",
      "transforms": ["trim", "lowercase"], "required": true }
  ],
  "drops": ["legacy_id"],
  "dropUnmapped": false,
  "lookups": { "country": { "US": "United States" } }
}
```

- **Paths** are dotted (`profile.firstName`); array indexes work too (`tags[0]`).
- **Missing sources**: `onMissing` is `skip` (default), `default` (use `default`), or `error`. `required: true` always errors.
- **Copy-through**: unmapped top-level fields are carried over unless `dropUnmapped` is true. A mapping consumes its source's top-level key (mapping `address.city` consumes `address`).
- **Root arrays** are migrated record-by-record.

## Transforms

Applied in order, left to right:

| Spec | Effect |
|---|---|
| `trim`, `lowercase`, `uppercase` | String casing/whitespace |
| `coerce:int\|long\|double\|decimal\|bool\|string\|datetime` | Type coercion (`"42"` → `42`, `"yes"` → `true`) |
| `format:<fmt>` | .NET format string for numbers/dates (`format:yyyy-MM-dd`) |
| `add:<n>`, `multiply:<n>` | Numeric arithmetic |
| `split:<sep>`, `join:<sep>` | String ↔ array |
| `lookup:<table>` | Map through a plan-level lookup table |
| `compute:<expr>` | Arithmetic over `$field` references, e.g. `compute:$price * $qty` |
| `prefix:<t>`, `suffix:<t>`, `replace:<old>=<new>` | String building blocks |

`compute:` uses a small recursive-descent evaluator (`+ - * /`, parentheses, unary minus) resolving `$dotted.path` against the current record.

## Design decisions

1. **Plans are data, validated up front.** `PlanLoader` collects *every* validation problem (unknown transforms, duplicate targets, missing defaults) into one `PlanValidationException` instead of failing on the first — a plan with five typos shouldn't take five runs to fix.
2. **Transforms are pure functions over `JsonNode`.** Each `ITransform` takes a value and a context and returns a new value; the engine never mutates the input document, so dry-run and real runs can't diverge.
3. **The engine compiles the plan once.** Transform specs are parsed at `MigrationEngine` construction, so a bad spec fails before the first record is touched, not halfway through a 100k-record file.
4. **Reports are first-class.** Every run produces per-field `FieldChange` records (mapped / defaulted / dropped / error) — the CLI's `--dry-run` and `--report` are just two views over the same `MigrationReport`.
5. **Zero third-party dependencies** in the library — only `System.Text.Json`. The test project is the only thing that pulls NuGet packages.

## Running the tests

```bash
dotnet test
```

47 tests cover path navigation, every built-in transform, the expression evaluator, plan validation, and end-to-end migrations (defaults, drops, required-field errors, root arrays, error recovery).

## Project layout

```
src/JsonMigrator/            # the library (no external dependencies)
  PathNavigator.cs           # dotted-path get/set over JsonNode trees
  MigrationPlan.cs           # plan model + cross-rule validation
  MappingRule.cs             # one field mapping
  PlanLoader.cs             # JSON plan file -> validated MigrationPlan
  MigrationEngine.cs         # applies a plan to documents
  MigrationReport.cs         # per-field changes + counters
  Transforms/
    TransformRegistry.cs     # spec string -> ITransform
    BuiltInTransforms.cs     # all built-in transforms
    ExpressionEvaluator.cs   # recursive-descent evaluator for compute:
src/JsonMigrator.Cli/        # jsonmigrate CLI (dry-run, reports, exit codes)
tests/JsonMigrator.Tests/    # xUnit suite
samples/                     # example plan + v1 data
```

## License

MIT — see [LICENSE](LICENSE).
