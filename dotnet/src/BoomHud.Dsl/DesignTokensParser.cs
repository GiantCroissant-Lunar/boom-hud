using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using BoomHud.Abstractions.IR;

namespace BoomHud.Dsl;

public static class DesignTokensParser
{
    public static ThemeDocument Parse(string json, string themeName, string variant = "light")
    {
        var root = JsonNode.Parse(json) ?? throw new InvalidOperationException("Failed to parse design tokens JSON.");

        var spread = root["spread"] ?? throw new InvalidOperationException("Missing 'spread' property.");

        var variantNode = spread[variant] ?? throw new InvalidOperationException($"Variant '{variant}' not found under 'spread'.");
        var expandedNode = spread["expanded"] ?? variantNode;

        var colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        var dimensions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var fontSizes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var reference = expandedNode["reference"];
        if (reference != null)
        {
            var colorScale = reference["color"]?["scale"] as JsonObject;
            if (colorScale != null)
            {
                foreach (var (scaleName, scaleNode) in colorScale)
                {
                    if (scaleNode is not JsonObject shades)
                    {
                        continue;
                    }

                    foreach (var (shadeName, shadeNode) in shades)
                    {
                        var hex = GetString(shadeNode?["value"]);
                        if (string.IsNullOrWhiteSpace(hex))
                        {
                            continue;
                        }

                        var key = $"reference.color.{scaleName}.{shadeName}";
                        colors[key] = Color.Parse(hex);
                    }
                }
            }

            var dimensionScale = reference["dimension"]?["scale"] as JsonObject;
            if (dimensionScale != null)
            {
                foreach (var (dimensionKey, dimensionNode) in dimensionScale)
                {
                    if (!TryGetDouble(dimensionNode?["value"], out var value))
                    {
                        continue;
                    }

                    var key = $"reference.dimension.{dimensionKey}";
                    dimensions[key] = value;
                }
            }

            var typographyScale = reference["typography"]?["scale"] as JsonObject;
            if (typographyScale != null)
            {
                foreach (var (typographyKey, typographyNode) in typographyScale)
                {
                    if (!TryGetDouble(typographyNode?["value"], out var value))
                    {
                        continue;
                    }

                    var key = $"reference.typography.{typographyKey}";
                    fontSizes[key] = value;
                }
            }
        }

        Color? ResolveColor(JsonNode? valueNode)
        {
            if (valueNode == null)
            {
                return null;
            }

            var s = GetString(valueNode);
            if (s is not null)
            {
                if (s.Length > 1 && s[0] == '{' && s[^1] == '}')
                {
                    var path = s[1..^1];
                    var target = GetNodeByPath(root, path);
                    var hex = GetString(target?["value"]);
                    if (!string.IsNullOrWhiteSpace(hex))
                    {
                        return Color.Parse(hex);
                    }

                    return null;
                }

                if (s.StartsWith('#'))
                {
                    return Color.Parse(s);
                }
            }

            return null;
        }

        double? ResolveNumber(JsonNode? valueNode)
        {
            if (valueNode == null)
            {
                return null;
            }

            if (TryGetDouble(valueNode, out var direct))
            {
                return direct;
            }

            var s = GetString(valueNode);
            if (s is not null && s.Length > 1 && s[0] == '{' && s[^1] == '}')
            {
                var path = s[1..^1];
                var target = GetNodeByPath(root, path);
                if (TryGetDouble(target?["value"], out var resolved))
                {
                    return resolved;
                }
            }

            return null;
        }

        var system = variantNode["system"];
        if (system != null)
        {
            var systemColor = system["color"];
            if (systemColor != null)
            {
                var brand = ResolveColor(systemColor["brand"]?["value"]);
                if (brand is Color brandColor)
                {
                    colors["system.color.brand"] = brandColor;
                }

                if (systemColor["text"] is JsonObject textObj)
                {
                    foreach (var (textKey, textNode) in textObj)
                    {
                        var c = ResolveColor(textNode?["value"]);
                        if (c is Color color)
                        {
                            colors[$"system.color.text.{textKey}"] = color;
                        }
                    }
                }
            }

            var radius = system["dimension"]?["radius"] as JsonObject;
            if (radius != null)
            {
                foreach (var (radiusKey, radiusNode) in radius)
                {
                    var value = ResolveNumber(radiusNode?["value"]);
                    if (value.HasValue)
                    {
                        dimensions[$"system.radius.{radiusKey}"] = value.Value;
                    }
                }
            }

            if (system["typography"] is JsonObject typographySystem)
            {
                foreach (var (groupName, groupNode) in typographySystem)
                {
                    if (groupNode is not JsonObject group)
                    {
                        continue;
                    }

                    foreach (var (entryKey, entryNode) in group)
                    {
                        var value = ResolveNumber(entryNode?["value"]);
                        if (!value.HasValue)
                        {
                            continue;
                        }

                        fontSizes[$"system.{groupName}.{entryKey}"] = value.Value;
                    }
                }
            }
        }

        return new ThemeDocument
        {
            Name = themeName,
            Colors = new ReadOnlyDictionary<string, Color>(colors),
            Dimensions = new ReadOnlyDictionary<string, double>(dimensions),
            FontSizes = new ReadOnlyDictionary<string, double>(fontSizes)
        };
    }

    private static JsonNode? GetNodeByPath(JsonNode root, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        JsonNode? current = root;

        foreach (var segment in segments)
        {
            if (current is not JsonObject obj)
            {
                return null;
            }

            if (!obj.TryGetPropertyValue(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static bool TryGetDouble(JsonNode? node, out double value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<double>(out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonNode? node)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var s))
        {
            return s;
        }

        return null;
    }
}
