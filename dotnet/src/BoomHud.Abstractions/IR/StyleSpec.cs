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
    /// Optional theme token key for the background color.
    /// </summary>
    public string? BackgroundToken { get; init; }

    /// <summary>
    /// Font size in points.
    /// </summary>
    public double? FontSize { get; init; }

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
