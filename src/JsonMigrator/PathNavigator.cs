using System.Text.Json.Nodes;

namespace JsonMigrator;

/// <summary>
/// Reads and writes values at dotted paths ("address.city", "items") inside a
/// JSON document tree built from mutable <see cref="JsonNode"/> values.
/// Array indexes are supported with the "[n]" suffix, e.g. "tags[0]".
/// </summary>
public static class PathNavigator
{
    /// <summary>Returns the node at <paramref name="path"/>, or null when any segment is missing.</summary>
    public static JsonNode? Get(JsonNode? root, string path)
    {
        if (root is null || string.IsNullOrWhiteSpace(path))
            return null;

        JsonNode? current = root;
        foreach (var segment in Split(path))
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(segment.Name, out var next))
                current = next;
            else if (current is JsonArray arr && segment.Index is int i && i >= 0 && i < arr.Count)
                current = arr[i];
            else
                return null;
            if (current is null)
                return null;
        }
        return current;
    }

    /// <summary>
    /// Sets <paramref name="value"/> at <paramref name="path"/>, creating any
    /// missing intermediate objects. A null value removes the property.
    /// </summary>
    public static void Set(JsonObject root, string path, JsonNode? value)
    {
        var segments = Split(path).ToList();
        JsonNode current = root;

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var last = i == segments.Count - 1;

            if (current is JsonObject obj)
            {
                if (last)
                {
                    if (value is null)
                        obj.Remove(segment.Name);
                    else
                        obj[segment.Name] = value.DeepClone();
                    return;
                }

                if (!obj.TryGetPropertyValue(segment.Name, out var next) || next is not JsonObject)
                {
                    next = new JsonObject();
                    obj[segment.Name] = next;
                }
                current = next!;
            }
            else if (current is JsonArray arr && segment.Index is int idx)
            {
                while (arr.Count <= idx)
                    arr.Add(new JsonObject());
                if (last)
                {
                    arr[idx] = value?.DeepClone();
                    return;
                }
                if (arr[idx] is not JsonObject)
                    arr[idx] = new JsonObject();
                current = arr[idx]!;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot navigate path '{path}': segment '{segment.Name}' is not an object or array.");
            }
        }
    }

    private readonly record struct Segment(string Name, int? Index);

    private static IEnumerable<Segment> Split(string path)
    {
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bracket = part.IndexOf('[');
            if (bracket < 0)
            {
                yield return new Segment(part, null);
                continue;
            }
            var name = part[..bracket];
            var indexText = part[(bracket + 1)..].TrimEnd(']');
            yield return new Segment(name, int.TryParse(indexText, out var i) ? i : null);
        }
    }
}
