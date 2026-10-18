using Xunit;
using System.Text.Json.Nodes;
using JsonMigrator;

namespace JsonMigrator.Tests;

public class PathNavigatorTests
{
    private static JsonObject Doc() =>
        (JsonObject)JsonNode.Parse("""{"user": {"name": "Ada", "tags": ["x", "y"]}}""")!;

    [Fact]
    public void Get_ReturnsNestedValue()
    {
        Assert.Equal("Ada", PathNavigator.Get(Doc(), "user.name")?.GetValue<string>());
    }

    [Fact]
    public void Get_SupportsArrayIndex()
    {
        Assert.Equal("y", PathNavigator.Get(Doc(), "user.tags[1]")?.GetValue<string>());
    }

    [Fact]
    public void Get_ReturnsNull_WhenSegmentMissing()
    {
        Assert.Null(PathNavigator.Get(Doc(), "user.address.city"));
        Assert.Null(PathNavigator.Get(Doc(), "user.tags[9]"));
    }

    [Fact]
    public void Set_CreatesIntermediateObjects()
    {
        var doc = new JsonObject();
        PathNavigator.Set(doc, "profile.address.city", JsonValue.Create("Lahore"));
        Assert.Equal("Lahore", doc["profile"]!["address"]!["city"]!.GetValue<string>());
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        var doc = Doc();
        PathNavigator.Set(doc, "user.name", JsonValue.Create("Grace"));
        Assert.Equal("Grace", PathNavigator.Get(doc, "user.name")?.GetValue<string>());
    }

    [Fact]
    public void Set_Null_RemovesProperty()
    {
        var doc = Doc();
        PathNavigator.Set(doc, "user.name", null);
        Assert.Null(PathNavigator.Get(doc, "user.name"));
        Assert.NotNull(PathNavigator.Get(doc, "user.tags")); // siblings untouched
    }
}
