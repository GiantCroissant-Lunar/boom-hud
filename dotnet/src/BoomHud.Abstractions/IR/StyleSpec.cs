using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoomHud.Abstractions.IR;

/// <summary>
/// Style specification for a component.
/// </summary>
public sealed record StyleSpec
{
    /// <summary>
    /// Foreground/text color.
    /// </summary>
    public Color? Foreground { get; init; }

    /// <summary>
    /// Optional theme token key for the foreground/text color.
    /// </summary>
    public string? ForegroundToken { get; init; }

    /// <summary>
    /// Background color.
    /// </summary>
    public Color? Background { get; init; }

    /// <summary>
    /// Background image fill.
    /// </summary>
    public BackgroundImageSpec? BackgroundImage { get; init; }

    /// <summary>
    /// Optional theme token key for the background color.
    /// </summary>
    public string? BackgroundToken { get; init; }

    /// <summary>
    /// Font size in points.
    /// </summary>
    public double? FontSize { get; init; }

    /// <summary>
    /// Preferred line height from the source design.
    /// </summary>
    public double? LineHeight { get; init; }

    /// <summary>
    /// Font family name.
    /// </summary>
    public string? FontFamily { get; init; }

    /// <summary>
    /// Optional theme token key for the font size.
    /// </summary>
    public string? FontSizeToken { get; init; }

    /// <summary>
    /// Font weight.
    /// </summary>
    public FontWeight? FontWeight { get; init; }

    /// <summary>
    /// Font style (normal, italic).
    /// </summary>
    public FontStyle? FontStyle { get; init; }

    /// <summary>
    /// Additional spacing between characters in pixels.
    /// </summary>
    public double? LetterSpacing { get; init; }

    /// <summary>
    /// Border specification.
    /// </summary>
    public BorderSpec? Border { get; init; }

    /// <summary>
    /// Optional theme token key for the border color.
    /// </summary>
    public string? BorderColorToken { get; init; }

    /// <summary>
    /// Border radius for rounded corners.
    /// </summary>
    public double? BorderRadius { get; init; }

    /// <summary>
    /// Opacity (0-1).
    /// </summary>
    public double? Opacity { get; init; }

    /// <summary>
    /// Named style class(es) to apply.
    /// </summary>
    public IReadOnlyList<string>? Classes { get; init; }

    /// <summary>
    /// Width override (can also be in layout).
    /// </summary>
    public Dimension? Width { get; init; }

    /// <summary>
    /// Height override (can also be in layout).
    /// </summary>
    public Dimension? Height { get; init; }
}

/// <summary>
/// Background image fill specification.
/// </summary>
public sealed record BackgroundImageSpec
{
    /// <summary>
    /// Image URL or asset path.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Fill mode for the image.
    /// </summary>
    public BackgroundImageMode Mode { get; init; } = BackgroundImageMode.Fill;
}

/// <summary>
/// Background image sizing modes.
/// </summary>
public enum BackgroundImageMode
{
    Fill,
    Contain,
    Stretch,
    Tile,
    Original
}

/// <summary>
/// Font weight options.
/// </summary>
public enum FontWeight
{
    Light,
    Normal,
    Bold
}

/// <summary>
/// Font style options.
/// </summary>
public enum FontStyle
{
    Normal,
    Italic
}

/// <summary>
/// Border specification.
/// </summary>
public sealed record BorderSpec
{
    /// <summary>
    /// Border style.
    /// </summary>
    public BorderStyle Style { get; init; } = BorderStyle.None;

    /// <summary>
    /// Border color.
    /// </summary>
    public Color? Color { get; init; }

    /// <summary>
    /// Border width in pixels.
    /// </summary>
    public double Width { get; init; } = 1;
}

/// <summary>
/// Border style options.
/// </summary>
public enum BorderStyle
{
    None,
    SingleLine,
    DoubleLine,
    Rounded,
    Thick,
    Solid,
    Dashed
}

/// <summary>
/// Color representation supporting named colors, hex, and RGB.
/// </summary>
[JsonConverter(typeof(ColorJsonConverter))]
public readonly record struct Color
{
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }
    public byte A { get; init; }

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    // Common named colors (TUI-compatible)
    public static Color Black => new(0, 0, 0);
    public static Color White => new(255, 255, 255);
    public static Color Red => new(255, 0, 0);
    public static Color Green => new(0, 255, 0);
    public static Color Blue => new(0, 0, 255);
    public static Color Yellow => new(255, 255, 0);
    public static Color Cyan => new(0, 255, 255);
    public static Color Magenta => new(255, 0, 255);
    public static Color Gray => new(128, 128, 128);
    public static Color DarkGray => new(64, 64, 64);
    public static Color LightGray => new(192, 192, 192);
    public static Color Transparent => new(0, 0, 0, 0);

    /// <summary>
    /// Parses a color from hex string (#RGB, #RRGGBB, #RRGGBBAA) or named color.
    /// </summary>
    public static Color Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Color value cannot be empty", nameof(value));

        value = value.Trim().ToLowerInvariant();

        if (value.StartsWith("rgba(", StringComparison.Ordinal) || value.StartsWith("rgb(", StringComparison.Ordinal))
        {
            return ParseRgbFunction(value);
        }

        // Named colors
        return value switch
        {
            "black" => Black,
            "white" => White,
            "red" => Red,
            "green" => Green,
            "blue" => Blue,
            "yellow" => Yellow,
            "cyan" => Cyan,
            "magenta" => Magenta,
            "gray" or "grey" => Gray,
            "darkgray" or "darkgrey" => DarkGray,
            "lightgray" or "lightgrey" => LightGray,
            "transparent" => Transparent,
            _ => ParseHex(value)
        };
    }

    private static Color ParseRgbFunction(string value)
    {
        var openParen = value.IndexOf('(');
        var closeParen = value.LastIndexOf(')');
        if (openParen < 0 || closeParen <= openParen)
        {
            throw new ArgumentException($"Invalid color format: {value}", nameof(value));
        }

        var functionName = value[..openParen];
        var parts = value[(openParen + 1)..closeParen]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (string.Equals(functionName, "rgb", StringComparison.Ordinal))
        {
            if (parts.Length != 3)
            {
                throw new ArgumentException($"Invalid rgb() color format: {value}", nameof(value));
            }

            return new Color(
                ParseRgbChannel(parts[0]),
                ParseRgbChannel(parts[1]),
                ParseRgbChannel(parts[2]));
        }

        if (string.Equals(functionName, "rgba", StringComparison.Ordinal))
        {
            if (parts.Length != 4)
            {
                throw new ArgumentException($"Invalid rgba() color format: {value}", nameof(value));
            }

            return new Color(
                ParseRgbChannel(parts[0]),
                ParseRgbChannel(parts[1]),
                ParseRgbChannel(parts[2]),
                ParseAlphaChannel(parts[3]));
        }

        throw new ArgumentException($"Invalid color format: {value}", nameof(value));
    }

    private static byte ParseRgbChannel(string value)
    {
        if (value.EndsWith('%'))
        {
            var percent = double.Parse(value[..^1], CultureInfo.InvariantCulture);
            return ClampToByte(Math.Round(percent / 100d * 255d, MidpointRounding.AwayFromZero));
        }

        return ClampToByte(double.Parse(value, CultureInfo.InvariantCulture));
    }

    private static byte ParseAlphaChannel(string value)
    {
        if (value.EndsWith('%'))
        {
            var percent = double.Parse(value[..^1], CultureInfo.InvariantCulture);
            return ClampToByte(Math.Round(percent / 100d * 255d, MidpointRounding.AwayFromZero));
        }

        var alpha = double.Parse(value, CultureInfo.InvariantCulture);
        return alpha <= 1d
            ? ClampToByte(Math.Round(alpha * 255d, MidpointRounding.AwayFromZero))
            : ClampToByte(alpha);
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
    }

    private static Color ParseHex(string hex)
    {
        if (!hex.StartsWith('#'))
            throw new ArgumentException($"Invalid color format: {hex}", nameof(hex));

        hex = hex[1..];

        return hex.Length switch
        {
            3 => new Color(
                (byte)(Convert.ToByte(hex[0..1], 16) * 17),
                (byte)(Convert.ToByte(hex[1..2], 16) * 17),
                (byte)(Convert.ToByte(hex[2..3], 16) * 17)),
            6 => new Color(
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16)),
            8 => new Color(
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16)),
            _ => throw new ArgumentException($"Invalid hex color length: {hex}", nameof(hex))
        };
    }

    public string ToHex() => A == 255
        ? $"#{R:X2}{G:X2}{B:X2}"
        : $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    public override string ToString() => ToHex();
}

public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (value != null)
            {
                return Color.Parse(value);
            }
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            // Handle object format { "r": 255, "g": 0, "b": 0, "a": 255 } if needed
            // For now, we mainly need string parsing for the sample JSON
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            byte r = 0, g = 0, b = 0, a = 255;

            if (root.TryGetProperty("r", out var rProp) || root.TryGetProperty("R", out rProp)) r = rProp.GetByte();
            if (root.TryGetProperty("g", out var gProp) || root.TryGetProperty("G", out gProp)) g = gProp.GetByte();
            if (root.TryGetProperty("b", out var bProp) || root.TryGetProperty("B", out bProp)) b = bProp.GetByte();
            if (root.TryGetProperty("a", out var aProp) || root.TryGetProperty("A", out aProp)) a = aProp.GetByte();

            return new Color(r, g, b, a);
        }

        throw new JsonException("Expected string or object for Color");
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToHex());
    }
}
