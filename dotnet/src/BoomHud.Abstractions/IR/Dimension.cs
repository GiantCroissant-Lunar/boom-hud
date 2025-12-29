using System.Globalization;

namespace BoomHud.Abstractions.IR;

/// <summary>
/// Represents a dimension value that can be pixels, percentage, cells, or special values.
/// </summary>
public readonly record struct Dimension
{
    public double Value { get; init; }
    public DimensionUnit Unit { get; init; }

    public Dimension(double value, DimensionUnit unit)
    {
        Value = value;
        Unit = unit;
    }

    /// <summary>
    /// Creates a pixel dimension.
    /// </summary>
    public static Dimension Pixels(double value) => new(value, DimensionUnit.Pixels);

    /// <summary>
    /// Creates a percentage dimension.
    /// </summary>
    public static Dimension Percent(double value) => new(value, DimensionUnit.Percent);

    /// <summary>
    /// Creates a cell dimension (for TUI).
    /// </summary>
    public static Dimension Cells(double value) => new(value, DimensionUnit.Cells);

    /// <summary>
    /// Creates a star (proportional) dimension.
    /// </summary>
    public static Dimension Star(double value = 1) => new(value, DimensionUnit.Star);

    /// <summary>
    /// Auto-size dimension.
    /// </summary>
    public static Dimension Auto => new(0, DimensionUnit.Auto);

    /// <summary>
    /// Fill available space dimension.
    /// </summary>
    public static Dimension Fill => new(0, DimensionUnit.Fill);

    /// <summary>
    /// Parses a dimension string like "100px", "50%", "1cell", "auto", "fill", "2*".
    /// </summary>
    public static Dimension Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Dimension value cannot be empty", nameof(value));

        value = value.Trim().ToLowerInvariant();

        if (value == "auto")
            return Auto;
        if (value == "fill")
            return Fill;

        if (value.EndsWith("px", StringComparison.Ordinal))
            return Pixels(double.Parse(value[..^2], CultureInfo.InvariantCulture));
        if (value.EndsWith('%'))
            return Percent(double.Parse(value[..^1], CultureInfo.InvariantCulture));
        if (value.EndsWith("cell", StringComparison.Ordinal))
            return Cells(double.Parse(value[..^4], CultureInfo.InvariantCulture));
        if (value.EndsWith('*'))
        {
            var starValue = value.Length == 1 ? 1 : double.Parse(value[..^1], CultureInfo.InvariantCulture);
            return Star(starValue);
        }

        // Default to pixels if no unit specified
        return Pixels(double.Parse(value, CultureInfo.InvariantCulture));
    }

    public override string ToString() => Unit switch
    {
        DimensionUnit.Pixels => $"{Value.ToString(CultureInfo.InvariantCulture)}px",
        DimensionUnit.Percent => $"{Value.ToString(CultureInfo.InvariantCulture)}%",
        DimensionUnit.Cells => $"{Value.ToString(CultureInfo.InvariantCulture)}cell",
        DimensionUnit.Star => Value == 1 ? "*" : $"{Value.ToString(CultureInfo.InvariantCulture)}*",
        DimensionUnit.Auto => "auto",
        DimensionUnit.Fill => "fill",
        _ => Value.ToString(CultureInfo.InvariantCulture)
    };
}

/// <summary>
/// Units for dimension values.
/// </summary>
public enum DimensionUnit
{
    Pixels,
    Percent,
    Cells,
    Star,
    Auto,
    Fill
}

/// <summary>
/// Represents spacing values (padding, margin, gap).
/// </summary>
public readonly record struct Spacing
{
    public double Top { get; init; }
    public double Right { get; init; }
    public double Bottom { get; init; }
    public double Left { get; init; }

    public Spacing(double all)
    {
        Top = Right = Bottom = Left = all;
    }

    public Spacing(double vertical, double horizontal)
    {
        Top = Bottom = vertical;
        Left = Right = horizontal;
    }

    public Spacing(double top, double right, double bottom, double left)
    {
        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }

    public static Spacing Zero => new(0);
    public static Spacing Uniform(double value) => new(value);
    public static Spacing Horizontal(double value) => new(0, value);
    public static Spacing Vertical(double value) => new(value, 0);

    public override string ToString() =>
        Top == Right && Right == Bottom && Bottom == Left
            ? $"{Top}"
            : Top == Bottom && Left == Right
                ? $"{Top} {Left}"
                : $"{Top} {Right} {Bottom} {Left}";
}
