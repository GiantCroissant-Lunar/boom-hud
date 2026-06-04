using System.Globalization;
using System.Text.Json.Nodes;
using BoomHud.Abstractions.Runtime;

namespace BoomHud.Godot.Runtime;

public static class RuntimeValueResolver
{
    public static JsonNode? Resolve(RuntimeValue value, JsonObject? dataModel)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Literal is not null)
        {
            return value.Literal;
        }

        return value.Binding is not null
            ? Resolve(value.Binding, dataModel)
            : null;
    }

    public static JsonNode? Resolve(RuntimeBinding binding, JsonObject? dataModel)
    {
        ArgumentNullException.ThrowIfNull(binding);

        return RuntimeJsonPointerResolver.TryResolve(dataModel, binding.Path, out var value)
            ? value
            : binding.Fallback;
    }

    public static string ResolveText(
        IReadOnlyDictionary<string, RuntimeValue> properties,
        string propertyName,
        JsonObject? dataModel,
        string fallback = "")
    {
        ArgumentNullException.ThrowIfNull(properties);

        return properties.TryGetValue(propertyName, out var value)
            ? ToText(Resolve(value, dataModel), fallback, value.Binding?.Format)
            : fallback;
    }

    public static double ResolveNumber(
        IReadOnlyDictionary<string, RuntimeValue> properties,
        string propertyName,
        JsonObject? dataModel,
        double fallback = 0)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return properties.TryGetValue(propertyName, out var value)
            ? ToNumber(Resolve(value, dataModel), fallback)
            : fallback;
    }

    public static bool ResolveBoolean(
        IReadOnlyDictionary<string, RuntimeValue> properties,
        string propertyName,
        JsonObject? dataModel,
        bool fallback = true)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return properties.TryGetValue(propertyName, out var value)
            ? ToBoolean(Resolve(value, dataModel), fallback)
            : fallback;
    }

    public static IReadOnlyList<string> ResolveStringList(
        IReadOnlyDictionary<string, RuntimeValue> properties,
        string propertyName,
        JsonObject? dataModel)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (!properties.TryGetValue(propertyName, out var value))
        {
            return [];
        }

        var node = Resolve(value, dataModel);
        if (node is JsonArray jsonArray)
        {
            return jsonArray.Select(item => ToText(item, fallback: string.Empty)).ToArray();
        }

        var text = ToText(node, fallback: string.Empty);
        return string.IsNullOrEmpty(text) ? [] : [text];
    }

    private static string ToText(JsonNode? node, string fallback, string? format = null)
    {
        if (node is null)
        {
            return fallback;
        }

        object? scalar = node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value when value.TryGetValue<double>(out var number) => number,
            JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean,
            _ => node.ToJsonString()
        };

        if (format is not null)
        {
            return string.Format(CultureInfo.InvariantCulture, format, scalar);
        }

        return scalar switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => scalar?.ToString() ?? fallback
        };
    }

    private static double ToNumber(JsonNode? node, double fallback)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<double>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<string>(out var text) &&
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return fallback;
    }

    private static bool ToBoolean(JsonNode? node, bool fallback)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolean))
            {
                return boolean;
            }

            if (value.TryGetValue<string>(out var text) &&
                bool.TryParse(text, out boolean))
            {
                return boolean;
            }
        }

        return fallback;
    }
}

