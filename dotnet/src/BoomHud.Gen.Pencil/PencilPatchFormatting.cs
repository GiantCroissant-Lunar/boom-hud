using System.Globalization;
using System.Text.Json;

namespace BoomHud.Gen.Pencil;

internal static class PencilPatchFormatting
{
    public static string SerializeUpdate(string targetPenId, IEnumerable<KeyValuePair<string, object?>> properties)
        => "U("
            + SerializeString(targetPenId)
            + ", "
            + SerializeObject(properties)
            + ")";

    public static string SerializeValue(object? value)
    {
        return value switch
        {
            null => "null",
            string stringValue => SerializeString(stringValue),
            bool boolValue => boolValue ? "true" : "false",
            byte byteValue => byteValue.ToString(CultureInfo.InvariantCulture),
            sbyte sbyteValue => sbyteValue.ToString(CultureInfo.InvariantCulture),
            short shortValue => shortValue.ToString(CultureInfo.InvariantCulture),
            ushort ushortValue => ushortValue.ToString(CultureInfo.InvariantCulture),
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            uint uintValue => uintValue.ToString(CultureInfo.InvariantCulture),
            long longValue => longValue.ToString(CultureInfo.InvariantCulture),
            ulong ulongValue => ulongValue.ToString(CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("0.####", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("0.####", CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            IReadOnlyDictionary<string, object?> dictionary => SerializeObject(dictionary),
            IDictionary<string, object?> dictionary => SerializeObject(dictionary),
            IEnumerable<object?> list => "[" + string.Join(", ", list.Select(SerializeValue)) + "]",
            JsonElement jsonElement => jsonElement.GetRawText(),
            _ => JsonSerializer.Serialize(value)
        };
    }

    public static string SerializeObject(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        return "{"
            + string.Join(", ", properties.Select(static pair => $"{SerializeString(pair.Key)}: {SerializeValue(pair.Value)}"))
            + "}";
    }

    public static string SerializeString(string value)
    {
        return "\""
            + value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
            + "\"";
    }
}
