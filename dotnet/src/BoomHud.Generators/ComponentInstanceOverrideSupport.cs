using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

public static class ComponentInstanceOverrideSupport
{
    public const string RootPath = "$";

    public static string NormalizePropertyName(string propertyName)
        => propertyName.Trim().ToLowerInvariant();

    public static string ChildPath(string parentPath, int childIndex)
        => string.Equals(parentPath, RootPath, StringComparison.Ordinal)
            ? RootPath + "/" + childIndex.ToString(CultureInfo.InvariantCulture)
            : parentPath + "/" + childIndex.ToString(CultureInfo.InvariantCulture);

    public static bool CanParameterizeProperty(ComponentNode node, string propertyName, BindableValue<object?> value)
    {
        if (node.Children.Count > 0 || value.IsBound || !IsSerializableOverrideValue(value.Value))
        {
            return false;
        }

        return IsSupportedProperty(node, propertyName);
    }

    public static bool IsSupportedProperty(ComponentNode node, string propertyName)
    {
        var normalized = NormalizePropertyName(propertyName);
        return normalized switch
        {
            "text" or "content"
                => node.Type is ComponentType.Label
                    or ComponentType.Badge
                    or ComponentType.Button
                    or ComponentType.TextInput
                    or ComponentType.TextArea
                    or ComponentType.Checkbox
                    or ComponentType.RadioButton
                    or ComponentType.Icon
                    or ComponentType.ProgressBar,
            "value"
                => node.Type is ComponentType.Label
                    or ComponentType.Badge
                    or ComponentType.Button
                    or ComponentType.TextInput
                    or ComponentType.TextArea
                    or ComponentType.Checkbox
                    or ComponentType.RadioButton
                    or ComponentType.Icon
                    or ComponentType.ProgressBar
                    or ComponentType.Slider
                    or ComponentType.Image,
            "source" or "src"
                => node.Type == ComponentType.Image,
            _ => false
        };
    }

    public static bool IsSerializableOverrideValue(object? value)
        => value is null
            or string
            or bool
            or byte
            or sbyte
            or short
            or ushort
            or int
            or uint
            or long
            or ulong
            or float
            or double
            or decimal;

    public static SortedDictionary<string, SortedDictionary<string, object?>> GetPropertyOverrides(ComponentNode node)
    {
        if (!node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.ComponentPropertyOverrides, out var raw) || raw == null)
        {
            return new SortedDictionary<string, SortedDictionary<string, object?>>(StringComparer.Ordinal);
        }

        return TryConvertOverrideMap(raw)
            ?? new SortedDictionary<string, SortedDictionary<string, object?>>(StringComparer.Ordinal);
    }

    public static object? ToSerializableOverrideValue(object? value)
        => value switch
        {
            JsonElement element => ConvertJsonElement(element),
            _ => value
        };

    private static SortedDictionary<string, SortedDictionary<string, object?>>? TryConvertOverrideMap(object raw)
    {
        if (raw is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Object
                ? ConvertJsonOverrideMap(element)
                : null;
        }

        if (raw is IDictionary dictionary)
        {
            var result = new SortedDictionary<string, SortedDictionary<string, object?>>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string path || entry.Value == null)
                {
                    continue;
                }

                if (TryConvertPropertyMap(entry.Value) is { } propertyMap && propertyMap.Count > 0)
                {
                    result[path] = propertyMap;
                }
            }

            return result;
        }

        return null;
    }

    private static SortedDictionary<string, SortedDictionary<string, object?>> ConvertJsonOverrideMap(JsonElement element)
    {
        var result = new SortedDictionary<string, SortedDictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var propertyMap = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var childProperty in property.Value.EnumerateObject())
            {
                propertyMap[childProperty.Name] = ConvertJsonElement(childProperty.Value);
            }

            if (propertyMap.Count > 0)
            {
                result[property.Name] = propertyMap;
            }
        }

        return result;
    }

    private static SortedDictionary<string, object?>? TryConvertPropertyMap(object raw)
    {
        if (raw is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Object
                ? ConvertJsonPropertyMap(element)
                : null;
        }

        if (raw is IDictionary dictionary)
        {
            var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string propertyName)
                {
                    continue;
                }

                result[propertyName] = ToSerializableOverrideValue(entry.Value);
            }

            return result;
        }

        return null;
    }

    private static SortedDictionary<string, object?> ConvertJsonPropertyMap(JsonElement element)
    {
        var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ConvertJsonElement(property.Value);
        }

        return result;
    }

    private static object? ConvertJsonElement(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
}
