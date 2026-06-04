using System.Text.Json.Nodes;
using BoomHud.Abstractions.Runtime;

namespace BoomHud.Godot.Runtime;

public static class RuntimeJsonPointerResolver
{
    public static bool TryResolve(JsonNode? root, string jsonPointer, out JsonNode? value)
    {
        if (!RuntimeSurfaceValidator.IsValidJsonPointer(jsonPointer) || root is null)
        {
            value = null;
            return false;
        }

        if (jsonPointer.Length == 0)
        {
            value = root;
            return true;
        }

        var current = root;
        var segments = jsonPointer[1..].Split('/');
        foreach (var rawSegment in segments)
        {
            var segment = Unescape(rawSegment);
            switch (current)
            {
                case JsonObject jsonObject when jsonObject.TryGetPropertyValue(segment, out var child):
                    current = child;
                    break;
                case JsonArray jsonArray when TryGetArrayItem(jsonArray, segment, out var child):
                    current = child;
                    break;
                default:
                    value = null;
                    return false;
            }

            if (current is null)
            {
                value = null;
                return true;
            }
        }

        value = current;
        return true;
    }

    private static bool TryGetArrayItem(JsonArray jsonArray, string segment, out JsonNode? value)
    {
        if (!int.TryParse(segment, out var index) || index < 0 || index >= jsonArray.Count)
        {
            value = null;
            return false;
        }

        value = jsonArray[index];
        return true;
    }

    private static string Unescape(string segment)
        => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
}
