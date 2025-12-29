using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl.Figma;

namespace BoomHud.Dsl;

/// <summary>
/// Converts Figma variables JSON into a BoomHud ThemeDocument.
///
/// This is intended for the Figma variables API response (or a compatible mock),
/// not the main /v1/files document JSON.
/// </summary>
public static class FigmaThemeParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Parse a Figma variables JSON payload into a ThemeDocument.
    /// </summary>
    /// <param name="json">JSON from Figma variables API or a compatible mock.</param>
    /// <param name="themeName">Logical theme name to assign to the ThemeDocument.</param>
    /// <param name="collectionName">
    /// Optional collection name to select if multiple collections are present.
    /// If null and only one collection exists, that collection is used.
    /// </param>
    /// <param name="mode">
    /// Optional mode identifier or name (e.g. "Light", or a modeId).
    /// If null, the collection's defaultModeId is used, falling back to the first mode.
    /// </param>
    public static ThemeDocument Parse(string json, string themeName, string? collectionName = null, string? mode = null)
    {
        var data = JsonSerializer.Deserialize<FigmaVariablesResponse>(json, JsonOptions)
                   ?? throw new InvalidOperationException("Failed to parse Figma variables JSON.");

        if (data.VariableCollections == null || data.VariableCollections.Count == 0)
        {
            throw new InvalidOperationException("No variable collections found in Figma variables JSON.");
        }

        if (data.Variables == null || data.Variables.Count == 0)
        {
            throw new InvalidOperationException("No variables found in Figma variables JSON.");
        }

        var collection = SelectCollection(data.VariableCollections.Values, collectionName);
        var modeId = SelectModeId(collection, mode);

        var colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        var dimensions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var fontSizes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (collection.VariableIds != null)
        {
            foreach (var variableId in collection.VariableIds)
            {
                if (!data.Variables.TryGetValue(variableId, out var variable) || variable.ValuesByMode == null)
                {
                    continue;
                }

                if (!variable.ValuesByMode.TryGetValue(modeId, out var valueElement))
                {
                    continue;
                }

                var key = NormalizeVariableKey(collection.Name, variable.Name);
                var resolvedType = variable.ResolvedType?.ToUpperInvariant() ?? string.Empty;

                switch (resolvedType)
                {
                    case "COLOR":
                        if (TryConvertColor(valueElement, out var color))
                        {
                            colors[key] = color;
                        }
                        break;

                    case "FLOAT":
                    case "NUMBER":
                        if (TryConvertNumber(valueElement, out var number))
                        {
                            if (IsFontSizeKey(variable.Name))
                            {
                                fontSizes[key] = number;
                            }
                            else
                            {
                                dimensions[key] = number;
                            }
                        }
                        break;

                    default:
                        // Other types (STRING, BOOLEAN, etc.) are ignored for now.
                        break;
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

    private static FigmaVariableSet SelectCollection(IEnumerable<FigmaVariableSet> collections, string? collectionName)
    {
        FigmaVariableSet? result = null;

        if (!string.IsNullOrWhiteSpace(collectionName))
        {
            result = collections.FirstOrDefault(c =>
                string.Equals(c.Name, collectionName, StringComparison.OrdinalIgnoreCase));
        }

        result ??= collections.FirstOrDefault();

        if (result == null)
        {
            throw new InvalidOperationException("No variable collections available to select.");
        }

        return result;
    }

    private static string SelectModeId(FigmaVariableSet collection, string? mode)
    {
        if (collection.Modes == null || collection.Modes.Count == 0)
        {
            throw new InvalidOperationException($"Variable collection '{collection.Name}' has no modes.");
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            // Try by id first
            var byId = collection.Modes.FirstOrDefault(m =>
                string.Equals(m.ModeId, mode, StringComparison.OrdinalIgnoreCase));

            if (byId != null)
            {
                return byId.ModeId;
            }

            // Then by name
            var byName = collection.Modes.FirstOrDefault(m =>
                string.Equals(m.Name, mode, StringComparison.OrdinalIgnoreCase));

            if (byName != null)
            {
                return byName.ModeId;
            }
        }

        if (!string.IsNullOrEmpty(collection.DefaultModeId))
        {
            return collection.DefaultModeId;
        }

        return collection.Modes[0].ModeId;
    }

    private static string NormalizeVariableKey(string collectionName, string variableName)
    {
        var baseName = (variableName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = "unnamed";
        }

        // "color/surface/default" -&gt; "color.surface.default"
        baseName = baseName
            .Replace(" ", ".")
            .Replace("/", ".")
            .Replace("\\", ".");

        if (!string.IsNullOrWhiteSpace(collectionName))
        {
            var prefix = collectionName.Trim().Replace(" ", ".").ToLowerInvariant();
            return $"{prefix}.{baseName}";
        }

        return baseName;
    }

    private static bool TryConvertColor(JsonElement element, out Color color)
    {
        color = default;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty("r", out var rProp) ||
            !element.TryGetProperty("g", out var gProp) ||
            !element.TryGetProperty("b", out var bProp))
        {
            return false;
        }

        var r = (byte)Math.Clamp((int)Math.Round(rProp.GetDouble() * 255), 0, 255);
        var g = (byte)Math.Clamp((int)Math.Round(gProp.GetDouble() * 255), 0, 255);
        var b = (byte)Math.Clamp((int)Math.Round(bProp.GetDouble() * 255), 0, 255);

        byte a = 255;
        if (element.TryGetProperty("a", out var aProp))
        {
            a = (byte)Math.Clamp((int)Math.Round(aProp.GetDouble() * 255), 0, 255);
        }

        color = new Color(r, g, b, a);
        return true;
    }

    private static bool TryConvertNumber(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            value = element.GetDouble();
            return true;
        }

        // Some payloads may wrap the number in an object { "value": 14 }
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("value", out var inner) &&
            inner.ValueKind == JsonValueKind.Number)
        {
            value = inner.GetDouble();
            return true;
        }

        value = default;
        return false;
    }

    private static bool IsFontSizeKey(string variableName)
    {
        if (string.IsNullOrEmpty(variableName))
        {
            return false;
        }

        var lower = variableName.ToLowerInvariant();
        return lower.Contains("font") && (lower.Contains("size") || lower.Contains("body") || lower.Contains("heading"));
    }
}
