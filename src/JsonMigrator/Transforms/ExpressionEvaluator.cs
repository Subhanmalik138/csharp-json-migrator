using System.Globalization;
using System.Text.Json.Nodes;

namespace JsonMigrator.Transforms;

/// <summary>
/// Small recursive-descent evaluator for the "compute:" transform.
/// Supports numbers, $path field references (dotted paths into the current
/// record), parentheses and + - * / with the usual precedence.
/// </summary>
public static class ExpressionEvaluator
{
    /// <summary>Evaluates <paramref name="expression"/>; field refs resolve via <paramref name="resolve"/>.</summary>
    public static JsonNode Evaluate(string expression, Func<string, JsonNode?> resolve)
    {
        var parser = new Parser(expression, resolve);
        var result = parser.ParseExpression();
        parser.ExpectEnd();
        return result % 1 == 0
            ? JsonValue.Create((long)result)
            : JsonValue.Create(result);
    }

    /// <summary>
    /// Extracts a double from any numeric JSON value, tolerating every CLR
    /// backing (int, long, decimal, double) as well as numeric strings.
    /// Throws <see cref="TransformException"/> when the value is not numeric.
    /// </summary>
    internal static double AsDouble(JsonNode? value)
    {
        switch (value)
        {
            case JsonValue v when v.TryGetValue<double>(out var d):
                return d;
            case JsonValue v when v.TryGetValue<string>(out var s)
                && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                return parsed;
            case JsonValue v
                when double.TryParse(v.ToJsonString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n):
                return n;
        }
        throw new TransformException($"'{value}' is not numeric.");
    }

    private sealed class Parser(string expression, Func<string, JsonNode?> resolve)
    {
        private readonly string _text = expression;
        private int _pos;

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Match('+')) value += ParseTerm();
                else if (Match('-')) value -= ParseTerm();
                else return value;
            }
        }

        private double ParseTerm()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (Match('*')) value *= ParseFactor();
                else if (Match('/'))
                {
                    var divisor = ParseFactor();
                    if (divisor == 0) throw new TransformException($"expression '{_text}': division by zero.");
                    value /= divisor;
                }
                else return value;
            }
        }

        private double ParseFactor()
        {
            SkipWhitespace();
            if (Match('-')) return -ParseFactor();
            if (Match('+')) return ParseFactor();
            if (Match('('))
            {
                var value = ParseExpression();
                SkipWhitespace();
                if (!Match(')')) throw new TransformException($"expression '{_text}': missing ')'.");
                return value;
            }
            if (Peek() == '$') return ResolveReference();
            return ParseNumber();
        }

        private double ResolveReference()
        {
            _pos++; // $
            var start = _pos;
            while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] is '.' or '_' or '[' or ']'))
                _pos++;
            var path = _text[start.._pos];
            var node = resolve(path)
                ?? throw new TransformException($"expression '{_text}': field '${path}' not found.");
            try
            {
                return AsDouble(node);
            }
            catch (TransformException)
            {
                throw new TransformException($"expression '{_text}': field '${path}' is not numeric.");
            }
        }

        private double ParseNumber()
        {
            var start = _pos;
            while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
                _pos++;
            if (start == _pos || !double.TryParse(_text[start.._pos], out var value))
                throw new TransformException($"expression '{_text}': expected a number or $field at position {start}.");
            return value;
        }

        public void ExpectEnd()
        {
            SkipWhitespace();
            if (_pos != _text.Length)
                throw new TransformException($"expression '{_text}': unexpected '{_text[_pos]}' at position {_pos}.");
        }

        private void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
        }

        private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';
        private bool Match(char c)
        {
            if (Peek() == c) { _pos++; return true; }
            return false;
        }
    }
}
