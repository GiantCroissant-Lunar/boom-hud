namespace BoomHud.Abstractions.IR;

/// <summary>
/// Layout specification for a component.
/// </summary>
public sealed record LayoutSpec
{
    /// <summary>
    /// Type of layout (horizontal, vertical, grid, etc.).
    /// </summary>
    public LayoutType Type { get; init; } = LayoutType.Vertical;

    /// <summary>
    /// Width of the component.
    /// </summary>
    public Dimension? Width { get; init; }

    /// <summary>
    /// Height of the component.
    /// </summary>
    public Dimension? Height { get; init; }

    /// <summary>
    /// Minimum width constraint.
    /// </summary>
    public Dimension? MinWidth { get; init; }

    /// <summary>
    /// Minimum height constraint.
    /// </summary>
    public Dimension? MinHeight { get; init; }

    /// <summary>
    /// Maximum width constraint.
    /// </summary>
    public Dimension? MaxWidth { get; init; }

    /// <summary>
    /// Maximum height constraint.
    /// </summary>
    public Dimension? MaxHeight { get; init; }

    /// <summary>
    /// Gap between children.
    /// </summary>
    public Spacing? Gap { get; init; }

    /// <summary>
    /// Padding inside the component.
    /// </summary>
    public Spacing? Padding { get; init; }

    /// <summary>
    /// Margin outside the component.
    /// </summary>
    public Spacing? Margin { get; init; }

    /// <summary>
    /// Cross-axis alignment.
    /// </summary>
    public Alignment? Align { get; init; }

    /// <summary>
    /// Main-axis justification.
    /// </summary>
    public Justification? Justify { get; init; }

    /// <summary>
    /// Flex weight for proportional sizing.
    /// </summary>
    public double? Weight { get; init; }

    /// <summary>
    /// Grid column index (0-based).
    /// </summary>
    public int? GridColumn { get; init; }

    /// <summary>
    /// Grid row index (0-based).
    /// </summary>
    public int? GridRow { get; init; }

    /// <summary>
    /// Grid column span.
    /// </summary>
    public int GridColumnSpan { get; init; } = 1;

    /// <summary>
    /// Grid row span.
    /// </summary>
    public int GridRowSpan { get; init; } = 1;

    /// <summary>
    /// Column definitions for grid layout.
    /// </summary>
    public IReadOnlyList<Dimension>? ColumnDefinitions { get; init; }

    /// <summary>
    /// Row definitions for grid layout.
    /// </summary>
    public IReadOnlyList<Dimension>? RowDefinitions { get; init; }

    /// <summary>
    /// Dock position for dock layout.
    /// </summary>
    public DockPosition? Dock { get; init; }
}

/// <summary>
/// Types of layout arrangement.
/// </summary>
public enum LayoutType
{
    Horizontal,
    Vertical,
    Stack,
    Grid,
    Dock,
    Absolute
}

/// <summary>
/// Alignment options for cross-axis positioning.
/// </summary>
public enum Alignment
{
    Start,
    Center,
    End,
    Stretch
}

/// <summary>
/// Justification options for main-axis distribution.
/// </summary>
public enum Justification
{
    Start,
    Center,
    End,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly
}

/// <summary>
/// Dock positions for dock layout.
/// </summary>
public enum DockPosition
{
    Top,
    Bottom,
    Left,
    Right,
    Fill
}
