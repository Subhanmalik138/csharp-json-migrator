using Xunit;
using System.Globalization;
using System.Text.Json.Nodes;
using JsonMigrator.Transforms;

namespace JsonMigrator.Tests;

public class ExpressionEvaluatorTests
{
    private static double Eval(string expr) =>
        double.Parse(ExpressionEvaluator.Evaluate(expr, path => path switch
        {
            "a" => JsonValue.Create(6),
            "b" => JsonValue.Create(2.5),
            _ => null
        }).ToJsonString(), CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("$a * $b + 1", 16)]
    [InlineData("$a / 4", 1.5)]
    [InlineData("-$a + 10", 4)]
    public void Evaluates_WithPrecedenceAndReferences(string expr, double expected)
    {
        Assert.Equal(expected, Eval(expr));
    }

    [Fact]
    public void MissingField_ThrowsTransformException()
    {
        Assert.Throws<TransformException>(() => Eval("$nope + 1"));
    }

    [Fact]
    public void DivisionByZero_ThrowsTransformException()
    {
        Assert.Throws<TransformException>(() => Eval("1 / 0"));
    }

    [Fact]
    public void TrailingGarbage_ThrowsTransformException()
    {
        Assert.Throws<TransformException>(() => Eval("1 + 2)"));
    }
}
