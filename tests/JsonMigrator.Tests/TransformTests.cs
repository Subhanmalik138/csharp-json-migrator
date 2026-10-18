using Xunit;
using System.Text.Json.Nodes;
using JsonMigrator.Transforms;

namespace JsonMigrator.Tests;

public class TransformTests
{
    private static readonly TransformContext Ctx = new()
    {
        Record = JsonNode.Parse("""{"price": 10, "qty": 3, "code": "us"}"""),
        Lookups = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["region"] = new Dictionary<string, string> { ["us"] = "United States" }
        }
    };

    private static string Apply(string spec, string json)
    {
        var result = TransformRegistry.Parse(spec).Apply(JsonNode.Parse(json), Ctx);
        return result?.ToJsonString() ?? "null";
    }

    [Theory]
    [InlineData("trim", "\"  hi  \"", "\"hi\"")]
    [InlineData("lowercase", "\"AbC\"", "\"abc\"")]
    [InlineData("uppercase", "\"AbC\"", "\"ABC\"")]
    [InlineData("coerce:int", "\"42\"", "42")]
    [InlineData("coerce:int", "42.9", "42")]
    [InlineData("coerce:int", "true", "1")]
    [InlineData("coerce:double", "\"3.5\"", "3.5")]
    [InlineData("coerce:bool", "\"yes\"", "true")]
    [InlineData("coerce:string", "42", "\"42\"")]
    [InlineData("add:5", "10", "15")]
    [InlineData("multiply:2.5", "4", "10")]
    [InlineData("prefix:pre-", "\"fix\"", "\"pre-fix\"")]
    [InlineData("suffix:-post", "\"pre\"", "\"pre-post\"")]
    [InlineData("replace:-=_", "\"a-b\"", "\"a_b\"")]
    [InlineData("lookup:region", "\"us\"", "\"United States\"")]
    [InlineData("compute:$price * $qty", "0", "30")]
    [InlineData("compute:($price + 2) * $qty", "0", "36")]
    public void BuiltIns_ProduceExpectedValues(string spec, string input, string expected)
    {
        Assert.Equal(expected, Apply(spec, input));
    }

    [Fact]
    public void Split_ProducesArray_And_Join_ReversesIt()
    {
        var arr = TransformRegistry.Parse("split:,").Apply(JsonNode.Parse("\"a, b,c\""), Ctx);
        Assert.Equal("""["a","b","c"]""", arr?.ToJsonString());
        var joined = TransformRegistry.Parse("join:|").Apply(arr, Ctx);
        Assert.Equal("\"a|b|c\"", joined?.ToJsonString());
    }

    [Fact]
    public void CoerceDateTime_ThenFormat_ProducesDateOnly()
    {
        var coerced = TransformRegistry.Parse("coerce:datetime")
            .Apply(JsonNode.Parse("\"2023-04-12T09:30:00Z\""), Ctx);
        var formatted = TransformRegistry.Parse("format:yyyy-MM-dd").Apply(coerced, Ctx);
        Assert.Equal("\"2023-04-12\"", formatted?.ToJsonString());
    }

    [Fact]
    public void UnknownSpec_ThrowsTransformException()
    {
        Assert.Throws<TransformException>(() => TransformRegistry.Parse("frobnicate"));
    }

    [Fact]
    public void Coerce_Failure_ThrowsTransformException()
    {
        Assert.Throws<TransformException>(() => Apply("coerce:int", "\"not-a-number\""));
    }

    [Fact]
    public void Lookup_MissingKey_ThrowsTransformException()
    {
        Assert.Throws<TransformException>(() => Apply("lookup:region", "\"xx\""));
    }

    [Fact]
    public void ParseAll_CollectsEveryError()
    {
        var ex = Assert.Throws<PlanValidationException>(
            () => TransformRegistry.ParseAll(new[] { "trim", "bogus", "coerce" }));
        Assert.Equal(2, ex.Errors.Count);
    }
}
