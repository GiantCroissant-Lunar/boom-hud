namespace BoomHud.Abstractions.IR;

/// <summary>
/// Framework-neutral motion document for a HUD or component.
/// </summary>
public sealed record MotionDocument
{
    /// <summary>
    /// Motion document name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Frames per second used as the canonical timing basis.
    /// </summary>
    public int FramesPerSecond { get; init; } = 30;

    /// <summary>
    /// Motion clips contained in this document.
    /// </summary>
    public IReadOnlyList<MotionClip> Clips { get; init; } = [];
}

/// <summary>
/// A named motion clip, typically exported as one previewable/renderable animation.
/// </summary>
public sealed record MotionClip
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int StartFrame { get; init; }
    public int DurationFrames { get; init; }
    public IReadOnlyList<MotionTrack> Tracks { get; init; } = [];
}

/// <summary>
/// A track animating a specific target over time.
/// </summary>
public sealed record MotionTrack
{
    public required string Id { get; init; }
    public required string TargetId { get; init; }
    public MotionTargetKind TargetKind { get; init; } = MotionTargetKind.Element;
    public IReadOnlyList<MotionChannel> Channels { get; init; } = [];
}

/// <summary>
/// A property channel on a target.
/// </summary>
public sealed record MotionChannel
{
    public MotionProperty Property { get; init; }
    public IReadOnlyList<MotionKeyframe> Keyframes { get; init; } = [];
}

/// <summary>
/// A single keyframe value for a property.
/// </summary>
public sealed record MotionKeyframe
{
    public int Frame { get; init; }
    public MotionValue Value { get; init; } = MotionValue.Empty;
    public MotionEasing Easing { get; init; } = MotionEasing.Linear;
}

/// <summary>
/// Target classification for downstream exporters.
/// </summary>
public enum MotionTargetKind
{
    Element,
    Component,
    Root
}

/// <summary>
/// Exportable motion properties shared across adapters.
/// </summary>
public enum MotionProperty
{
    Opacity,
    PositionX,
    PositionY,
    PositionZ,
    ScaleX,
    ScaleY,
    ScaleZ,
    Rotation,
    RotationX,
    RotationY,
    Width,
    Height,
    Visibility,
    Text,
    SpriteFrame,
    Color
}

/// <summary>
/// Common easing identifiers intended to map cleanly across Remotion, Godot, and Unity.
/// </summary>
public enum MotionEasing
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    Step
}

/// <summary>
/// Simple scalar/vector/bool/string value union for exportable keyframes.
/// </summary>
public sealed record MotionValue
{
    public static MotionValue Empty { get; } = new();

    public double? Number { get; init; }
    public bool? Boolean { get; init; }
    public string? Text { get; init; }
    public IReadOnlyList<double>? Vector { get; init; }

    public static MotionValue FromNumber(double value) => new() { Number = value };
    public static MotionValue FromBoolean(bool value) => new() { Boolean = value };
    public static MotionValue FromText(string value) => new() { Text = value };
    public static MotionValue FromVector(params double[] values) => new() { Vector = values };
}
