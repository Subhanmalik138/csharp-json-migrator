using Xunit;
using System.Text.Json.Nodes;

namespace JsonMigrator.Tests;

public class MigrationEngineTests
{
    private static MigrationPlan Plan(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"plan-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return PlanLoader.Load(path);
    }

    private static readonly string ShopPlan = """
        {
          "name": "shop",
          "mappings": [
            { "source": "sku", "target": "sku", "transforms": ["trim", "uppercase"] },
            { "source": "price", "target": "price", "transforms": ["coerce:double", "multiply:1.5"] },
            { "source": "tags", "target": "tags", "transforms": ["split:,"] },
            { "source": "missing", "target": "filled", "onMissing": "default", "default": "N/A" },
            { "source": "must", "target": "must", "required": true }
          ],
          "drops": ["internal"]
        }
        """;

    [Fact]
    public void Migrate_AppliesMappingsDefaultsAndDrops()
    {
        var engine = new MigrationEngine(Plan(ShopPlan));
        var input = JsonNode.Parse("""
            {"sku": " ab-1 ", "price": "10", "tags": "a,b", "must": 1, "internal": "x", "extra": "kept"}
            """)!;

        var (output, report) = engine.Migrate(input);

        Assert.Equal("AB-1", output["sku"]!.GetValue<string>());
        Assert.Equal(15.0, output["price"]!.GetValue<double>());
        Assert.Equal(2, output["tags"]!.AsArray().Count);
        Assert.Equal("N/A", output["filled"]!.GetValue<string>());
        Assert.Null(output["internal"]);
        Assert.Equal("kept", output["extra"]!.GetValue<string>()); // unmapped copied through
        Assert.False(report.HasErrors);
        Assert.Equal(1, report.FieldsDefaulted);
        Assert.Equal(1, report.FieldsDropped);
    }

    [Fact]
    public void Migrate_RequiredMissingField_RecordsError()
    {
        var engine = new MigrationEngine(Plan(ShopPlan));
        var input = JsonNode.Parse("""{"sku": "a"}""")!;

        var (output, report) = engine.Migrate(input);

        Assert.True(report.HasErrors);
        Assert.Equal(1, report.Errors);
        Assert.Null(output["must"]);
    }

    [Fact]
    public void Migrate_RootArray_ProcessesEachRecord()
    {
        var engine = new MigrationEngine(Plan("""
            {"mappings": [{"source": "n", "target": "n", "transforms": ["coerce:int"]}], "dropUnmapped": true}
            """));
        var input = JsonNode.Parse("""[{"n": "1", "junk": true}, {"n": "2"}]""")!;

        var (output, report) = engine.Migrate(input);

        Assert.Equal(2, output.AsArray().Count);
        Assert.Equal(1L, output[0]!["n"]!.GetValue<long>());
        Assert.Null(output[0]!["junk"]); // dropUnmapped
        Assert.Equal(2, report.RecordsProcessed);
    }

    [Fact]
    public void Migrate_TransformFailure_RecordsError_AndContinues()
    {
        var engine = new MigrationEngine(Plan("""
            {"mappings": [
              {"source": "bad", "target": "bad", "transforms": ["coerce:int"]},
              {"source": "good", "target": "good"}
            ]}
            """));
        var input = JsonNode.Parse("""{"bad": "nope", "good": "yes"}""")!;

        var (output, report) = engine.Migrate(input);

        Assert.True(report.HasErrors);
        Assert.Equal("yes", output["good"]!.GetValue<string>()); // other fields unaffected
        Assert.Null(output["bad"]);
    }

    [Fact]
    public void Report_Summarize_And_ToJson()
    {
        var engine = new MigrationEngine(Plan(ShopPlan));
        var (_, report) = engine.Migrate(JsonNode.Parse("""{"sku": "a", "must": 1}""")!);

        Assert.Contains("1 record(s)", report.Summarize());
        var json = report.ToJson();
        Assert.Equal("shop", json["plan"]!.GetValue<string>());
        Assert.Equal(1, json["recordsProcessed"]!.GetValue<int>());
        Assert.True(json["changes"]!.AsArray().Count > 0);
    }
}
