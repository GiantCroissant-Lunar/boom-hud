using System.Collections.Generic;

namespace BoomHud.Godot.Runtime;

/// <summary>
/// Visual style for a runtime-surface component variant. All fields are optional, so a variant can
/// style only what it needs (e.g. a label sets just <see cref="FontColor"/>; a card sets fills + border).
/// Colors are hex strings (<c>#RRGGBB</c> / <c>#RRGGBBAA</c>); the renderer turns them into Godot
/// <c>StyleBoxFlat</c>es and theme overrides. The renderer applies a style ONLY when a node carries a
/// matching <c>variant</c> property and the active <see cref="RuntimeSurfaceTheme"/> defines it — so a
/// surface with no variants (or a renderer with no theme) renders exactly as before. Engine-agnostic data
/// (no Godot types) so a theme can be authored in plain C#.
/// </summary>
public sealed record RuntimeComponentStyle
{
    /// <summary>Background fill (panel/badge/button box). Null = transparent.</summary>
    public string? Fill { get; init; }

    /// <summary>Border color. Null = no border.</summary>
    public string? BorderColor { get; init; }

    /// <summary>Uniform border thickness in px (ignored when <see cref="BorderColor"/> is null).</summary>
    public int BorderWidth { get; init; }

    /// <summary>Draw the border on the bottom edge only (for header / section underlines).</summary>
    public bool BorderBottomOnly { get; init; }

    /// <summary>Corner radius in px (all corners).</summary>
    public int CornerRadius { get; init; }

    /// <summary>Content inset [top, right, bottom, left] in px — becomes the box's content margins.</summary>
    public IReadOnlyList<int>? Padding { get; init; }

    /// <summary>Text color.</summary>
    public string? FontColor { get; init; }

    /// <summary>Font size in px (0 = inherit).</summary>
    public int FontSize { get; init; }

    /// <summary>System font family (e.g. "Inter", "JetBrains Mono"); falls back to the theme font if absent.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Font weight 100..900 (0 = inherit). Applied via the system font when <see cref="FontFamily"/> is set.</summary>
    public int FontWeight { get; init; }

    /// <summary>Overall opacity 0..1 (1 = opaque) — modulates the whole control (e.g. dimmed "available" cards).</summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>True when this style carries any box geometry (fill/border/radius/padding) worth a StyleBox.</summary>
    public bool HasBox =>
        Fill is not null
        || (BorderColor is not null && (BorderWidth > 0 || BorderBottomOnly))
        || CornerRadius > 0
        || (Padding is { Count: 4 });
}

/// <summary>
/// A named set of component-variant styles. The renderer looks a node's <c>variant</c> property up here;
/// a miss (or a null theme) means no styling — the default look. Keyed case-insensitively.
/// </summary>
public sealed record RuntimeSurfaceTheme
{
    public IReadOnlyDictionary<string, RuntimeComponentStyle> Variants { get; init; }
        = new Dictionary<string, RuntimeComponentStyle>(System.StringComparer.OrdinalIgnoreCase);

    public RuntimeComponentStyle? Get(string? variant)
        => !string.IsNullOrEmpty(variant) && Variants.TryGetValue(variant, out var style) ? style : null;
}
