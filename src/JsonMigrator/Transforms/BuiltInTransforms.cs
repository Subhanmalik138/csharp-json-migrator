using System.Globalization;
using System.Text.Json.Nodes;

namespace JsonMigrator.Transforms;

/// <summary>Built-in transforms. Each is constructed from its spec argument.</summary>
public static class BuiltInTransforms
{
    private sealed class Trim : ITransform
    {
        public string Name => "trim";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) =>
            value is JsonValue v && v.TryGetValue<string>(out var s) ? JsonValue.Create(s.Trim()) : value;
    }

    private sealed class Lowercase : ITransform
    {
        public string Name => "lowercase";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) =>
            value is JsonValue v && v.TryGetValue<string>(out var s) ? JsonValue.Create(s.ToLowerInvariant()) : value;
    }

    private sealed class Uppercase : ITransform
    {
        public string Name => "uppercase";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) =>
            value is JsonValue v && v.TryGetValue<string>(out var s) ? JsonValue.Create(s.ToUpperInvariant()) : value;
    }

    private sealed class Coerce(string kind) : ITransform
    {
        public string Name => $"coerce:{kind}";

        public JsonNode? Apply(JsonNode? value, TransformContext ctx)
        {
            if (value is null) return null;
            try
            {
                return kind switch
                {
                    "int" => JsonValue.Create(ToLong(value)),
                    "long" => JsonValue.Create(ToLong(value)),
                    "double" => JsonValue.Create(ToDouble(value)),
                    "decimal" => JsonValue.Create(ToDecimal(value)),
                    "bool" => JsonValue.Create(ToBool(value)),
                    "string" => JsonValue.Create(ToStringValue(value)),
                    "datetime" => JsonValue.Create(ToDateTime(value)),
                    _ => throw new TransformException($"unknown coerce target '{kind}'.")
                };
            }
            catch (TransformException) { throw; }
            catch (Exception ex)
            {
                throw new TransformException($"coerce:{kind} failed on '{value}': {ex.Message}");
            }
        }

        private static long ToLong(JsonNode value) => value switch
        {
            JsonValue v when v.TryGetValue<long>(out var l) => l,
            JsonValue v when v.TryGetValue<double>(out var d) => (long)d,
            JsonValue v when v.TryGetValue<string>(out var s) && long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var l) => l,
            JsonValue v when v.TryGetValue<bool>(out var b) => b ? 1 : 0,
            _ => throw new TransformException($"cannot coerce '{value}' to int.")
        };

        private static double ToDouble(JsonNode value) => value switch
        {
            JsonValue v when v.TryGetValue<double>(out var d) => d,
            JsonValue v when v.TryGetValue<long>(out var l) => l,
            JsonValue v when v.TryGetValue<string>(out var s) && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => throw new TransformException($"cannot coerce '{value}' to double.")
        };

        private static decimal ToDecimal(JsonNode value) => value switch
        {
            JsonValue v when v.TryGetValue<decimal>(out var d) => d,
            JsonValue v when v.TryGetValue<long>(out var l) => l,
            JsonValue v when v.TryGetValue<double>(out var d) => (decimal)d,
            JsonValue v when v.TryGetValue<string>(out var s) && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => throw new TransformException($"cannot coerce '{value}' to decimal.")
        };

        private static bool ToBool(JsonNode value) => value switch
        {
            JsonValue v when v.TryGetValue<bool>(out var b) => b,
            JsonValue v when v.TryGetValue<string>(out var s) => s.ToLowerInvariant() switch
            {
                "true" or "1" or "yes" or "y" => true,
                "false" or "0" or "no" or "n" => false,
                _ => throw new TransformException($"cannot coerce '{value}' to bool.")
            },
            JsonValue v when v.TryGetValue<long>(out var l) => l != 0,
            _ => throw new TransformException($"cannot coerce '{value}' to bool.")
        };

        private static string ToStringValue(JsonNode value) => value switch
        {
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonValue v when v.TryGetValue<long>(out var l) => l.ToString(CultureInfo.InvariantCulture),
            JsonValue v when v.TryGetValue<double>(out var d) => d.ToString(CultureInfo.InvariantCulture),
            JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
            _ => value.ToJsonString()
        };

        private static string ToDateTime(JsonNode value)
        {
            if (value is JsonValue v && v.TryGetValue<string>(out var s)
                && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToUniversalTime().ToString("o");
            throw new TransformException($"cannot coerce '{value}' to datetime.");
        }
    }

    private sealed class Format(string format) : ITransform
    {
        public string Name => $"format:{format}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) => value switch
        {
            JsonValue v when v.TryGetValue<double>(out var d) => JsonValue.Create(d.ToString(format, CultureInfo.InvariantCulture)),
            JsonValue v when v.TryGetValue<long>(out var l) => JsonValue.Create(l.ToString(format, CultureInfo.InvariantCulture)),
            JsonValue v when v.TryGetValue<string>(out var s)
                && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                => JsonValue.Create(dt.ToString(format, CultureInfo.InvariantCulture)),
            _ => throw new TransformException($"format:{format} needs a number or date string, got '{value}'.")
        };
    }

    private sealed class Add(double amount) : ITransform
    {
        public string Name => $"add:{amount}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) =>
            JsonValue.Create(NumberOf(value) + amount);
    }

    private sealed class Multiply(double factor) : ITransform
    {
        public string Name => $"multiply:{factor}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) =>
            JsonValue.Create(NumberOf(value) * factor);
    }

    private static double NumberOf(JsonNode? value) => value switch
    {
        JsonValue v when v.TryGetValue<double>(out var d) => d,
        JsonValue v when v.TryGetValue<long>(out var l) => l,
        JsonValue v when v.TryGetValue<string>(out var s) && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
        _ => throw new TransformException($"numeric transform needs a number, got '{value}'.")
    };

    private sealed class Split(char separator) : ITransform
    {
        public string Name => $"split:{separator}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx)
        {
            if (value is JsonValue v && v.TryGetValue<string>(out var s))
            {
                var arr = new JsonArray();
                foreach (var part in s.Split(separator)) arr.Add(part.Trim());
                return arr;
            }
            throw new TransformException($"split needs a string, got '{value}'.");
        }
    }

    private sealed class Join(string separator) : ITransform
    {
        public string Name => $"join:{separator}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx)
        {
            if (value is JsonArray arr)
                return JsonValue.Create(string.Join(separator, arr.Select(n => n?.ToString() ?? "")));
            throw new TransformException($"join needs an array, got '{value}'.");
        }
    }

    private sealed class Lookup(string table) : ITransform
    {
        public string Name => $"lookup:{table}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx)
        {
            if (!ctx.Lookups.TryGetValue(table, out var entries))
                throw new TransformException($"lookup table '{table}' is not defined in the plan.");
            var key = value?.ToString() ?? "";
            // JsonValue.ToString() quotes strings; unwrap for string keys.
            if (value is JsonValue v && v.TryGetValue<string>(out var s)) key = s;
            if (entries.TryGetValue(key, out var mapped))
                return JsonValue.Create(mapped);
            throw new TransformException($"lookup:{table} has no entry for '{key}'.");
        }
    }

    private sealed class Compute(string expression) : ITransform
    {
        public string Name => $"compute:{expression}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) =>
            ExpressionEvaluator.Evaluate(expression, path => PathNavigator.Get(ctx.Record, path));
    }

    private sealed class Prefix(string text) : ITransform
    {
        public string Name => $"prefix:{text}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) =>
            value is JsonValue v && v.TryGetValue<string>(out var s) ? JsonValue.Create(text + s) : value;
    }

    private sealed class Suffix(string text) : ITransform
    {
        public string Name => $"suffix:{text}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) =>
            value is JsonValue v && v.TryGetValue<string>(out var s) ? JsonValue.Create(s + text) : value;
    }

    private sealed class Replace(string oldValue, string newValue) : ITransform
    {
        public string Name => $"replace:{oldValue}={newValue}";
        public JsonNode? Apply(JsonNode? value, TransformContext ctx) =>
            value is JsonValue v && v.TryGetValue<string>(out var s)
                ? JsonValue.Create(s.Replace(oldValue, newValue, StringComparison.Ordinal))
                : value;
    }

    /// <summary>Builds the transform for a spec's argument, throwing <see cref="TransformException"/> when unknown.</summary>
    internal static ITransform Create(string name, string? argument) => name switch
    {
        "trim" => new Trim(),
        "lowercase" => new Lowercase(),
        "uppercase" => new Uppercase(),
        "coerce" => new Coerce(argument ?? throw new TransformException("coerce needs a target type, e.g. coerce:int.")),
        "format" => new Format(argument ?? throw new TransformException("format needs a format string, e.g. format:yyyy-MM-dd.")),
        "add" => new Add(ParseDouble(argument, "add")),
        "multiply" => new Multiply(ParseDouble(argument, "multiply")),
        "split" => new Split(ParseChar(argument, "split")),
        "join" => new Join(argument ?? throw new TransformException("join needs a separator.")),
        "lookup" => new Lookup(argument ?? throw new TransformException("lookup needs a table name.")),
        "compute" => new Compute(argument ?? throw new TransformException("compute needs an expression.")),
        "prefix" => new Prefix(argument ?? ""),
        "suffix" => new Suffix(argument ?? ""),
        "replace" => CreateReplace(argument),
        _ => throw new TransformException($"unknown transform '{name}'.")
    };

    private static Replace CreateReplace(string? argument)
    {
        var parts = (argument ?? "").Split('=', 2);
        if (parts.Length != 2)
            throw new TransformException("replace needs 'old=new', e.g. replace:-=_.");
        return new Replace(parts[0], parts[1]);
    }

    private static double ParseDouble(string? argument, string name) =>
        double.TryParse(argument, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d
            : throw new TransformException($"{name} needs a number argument.");

    private static char ParseChar(string? argument, string name) =>
        argument is { Length: 1 } c ? c[0] : throw new TransformException($"{name} needs a single-character separator.");
}
