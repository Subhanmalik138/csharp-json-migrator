using Xunit;
using System.Text.Json.Nodes;

namespace JsonMigrator.Tests;

public class PlanLoaderTests
{
    private static string WritePlan(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"plan-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_ParsesValidPlan()
    {
        var plan = PlanLoader.Load(WritePlan("""
            {
              "name": "p1",
              "mappings": [
                { "source": "a", "target": "b", "transforms": ["trim", "coerce:int"] }
              ],
              "drops": ["legacy"],
              "lookups": { "t": { "x": "y" } }
            }
            """));

        Assert.Equal("p1", plan.Name);
        Assert.Single(plan.Mappings);
        Assert.Equal("a", plan.Mappings[0].Source);
        Assert.Equal(new[] { "trim", "coerce:int" }, plan.Mappings[0].Transforms);
        Assert.Equal(new[] { "legacy" }, plan.Drops);
        Assert.Equal("y", plan.Lookups["t"]["x"]);
    }

    [Fact]
    public void Load_CollectsAllValidationErrors()
    {
        var ex = Assert.Throws<PlanValidationException>(() => PlanLoader.Load(WritePlan("""
            {
              "mappings": [
                { "source": "a", "target": "b", "transforms": ["bogus"] },
                { "source": "c", "target": "b" },
                { "target": "d" }
              ]
            }
            """)));

        // unknown transform + duplicate target + missing source = 3 errors at once
        Assert.Equal(3, ex.Errors.Count);
    }

    [Fact]
    public void Load_RejectsUnknownOnMissingBehavior()
    {
        Assert.Throws<PlanValidationException>(() => PlanLoader.Load(WritePlan("""
            { "mappings": [{ "source": "a", "target": "b", "onMissing": "explode" }] }
            """)));
    }

    [Fact]
    public void Load_RejectsDefaultBehaviorWithoutDefault()
    {
        Assert.Throws<PlanValidationException>(() => PlanLoader.Load(WritePlan("""
            { "mappings": [{ "source": "a", "target": "b", "onMissing": "default" }] }
            """)));
    }

    [Fact]
    public void Load_RejectsUnreadableFile()
    {
        var ex = Assert.Throws<PlanValidationException>(
            () => PlanLoader.Load(Path.Combine(Path.GetTempPath(), "does-not-exist.json")));
        Assert.Contains("could not read plan file", ex.Message);
    }
}
