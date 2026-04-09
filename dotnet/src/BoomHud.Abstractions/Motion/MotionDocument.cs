using BoomHud.Abstractions.Diagnostics;
using DiagnosticFactory = BoomHud.Abstractions.Diagnostics.Diagnostics;
using GeneratedEasing = BoomHud.Abstractions.Motion.Generated.Easing;
using GeneratedKind = BoomHud.Abstractions.Motion.Generated.Kind;
using GeneratedMotionChannel = BoomHud.Abstractions.Motion.Generated.MotionChannel;
using GeneratedMotionClip = BoomHud.Abstractions.Motion.Generated.MotionClip;
using GeneratedMotionDocument = BoomHud.Abstractions.Motion.Generated.MotionDocumentDto;
using GeneratedMotionKeyframe = BoomHud.Abstractions.Motion.Generated.MotionKeyframe;
using GeneratedMotionSequence = BoomHud.Abstractions.Motion.Generated.MotionSequence;
using GeneratedMotionSequenceFillMode = BoomHud.Abstractions.Motion.Generated.MotionSequenceFillMode;
using GeneratedMotionSequenceItem = BoomHud.Abstractions.Motion.Generated.MotionSequenceItem;
using GeneratedMotionTrack = BoomHud.Abstractions.Motion.Generated.MotionTrack;
using GeneratedMotionValue = BoomHud.Abstractions.Motion.Generated.MotionValue;
using GeneratedProperty = BoomHud.Abstractions.Motion.Generated.Property;
using GeneratedSerialize = BoomHud.Abstractions.Motion.Generated.Serialize;
using GeneratedTargetKind = BoomHud.Abstractions.Motion.Generated.TargetKind;

namespace BoomHud.Abstractions.Motion;

/// <summary>
/// Framework-neutral motion document used as the canonical JSON-backed animation contract.
/// </summary>
public sealed record MotionDocument
{
    /// <summary>
    /// Schema version. Currently "1.0".
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Human-readable document name.
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

    /// <summary>
    /// Optional default sequence id to use for staged playback.
    /// </summary>
    public string? DefaultSequenceId { get; init; }

    /// <summary>
    /// Optional composed motion sequences built from clips in this document.
    /// </summary>
    public IReadOnlyList<MotionSequence> Sequences { get; init; } = [];

    /// <summary>
    /// Diagnostics emitted during loading.
    /// </summary>
    public IReadOnlyList<BoomHudDiagnostic> LoadDiagnostics { get; init; } = [];

    private static readonly HashSet<string> SupportedVersions = ["1.0"];

    public static MotionDocument LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Motion document not found: {filePath}", filePath);
        }

        var json = File.ReadAllText(filePath);
        return LoadFromJson(json, filePath);
    }

    public static MotionDocument LoadFromJson(string json, string? sourcePath = null)
    {
        var dto = GeneratedMotionDocument.FromJson(json)
            ?? throw new InvalidOperationException("Failed to deserialize motion document");

        var diagnostics = new List<BoomHudDiagnostic>();
        var version = dto.Version ?? "1.0";

        if (!SupportedVersions.Contains(version))
        {
            diagnostics.Add(DiagnosticFactory.UnknownSchemaVersion("motion document", version, sourcePath));
        }

        return new MotionDocument
        {
            Version = version,
            Name = dto.Name ?? throw new InvalidOperationException("Motion document name is required"),
            FramesPerSecond = dto.FramesPerSecond.HasValue ? checked((int)dto.FramesPerSecond.Value) : 30,
            Clips = (dto.Clips ?? []).Select(MapClip).ToList().AsReadOnly(),
            DefaultSequenceId = string.IsNullOrWhiteSpace(dto.DefaultSequenceId) ? null : dto.DefaultSequenceId,
            Sequences = (dto.Sequences ?? []).Select(MapSequence).ToList().AsReadOnly(),
            LoadDiagnostics = diagnostics.AsReadOnly()
        };
    }

    public string ToJson(bool indented = true)
    {
        var dto = ToDto();
        var json = GeneratedSerialize.ToJson(dto);
        if (!indented)
        {
            return json;
        }

        using var document = System.Text.Json.JsonDocument.Parse(json);
        return System.Text.Json.JsonSerializer.Serialize(document.RootElement, IndentedJsonOptions);
    }

    private GeneratedMotionDocument ToDto()
        => new()
        {
            Schema = "https://boom-hud.dev/schemas/motion.schema.json",
            Version = Version,
            Name = Name,
            FramesPerSecond = FramesPerSecond,
            DefaultSequenceId = DefaultSequenceId,
            Clips = Clips.Select(MapClip).ToList(),
            Sequences = Sequences.Select(MapSequence).ToList()
        };

    private static MotionSequence MapSequence(GeneratedMotionSequence dto)
        => new()
        {
            Id = dto.Id ?? throw new InvalidOperationException("Motion sequence id is required"),
            Name = dto.Name ?? throw new InvalidOperationException("Motion sequence name is required"),
            Items = (dto.Items ?? []).Select(MapSequenceItem).ToList().AsReadOnly()
        };

    private static GeneratedMotionSequence MapSequence(MotionSequence sequence)
        => new()
        {
            Id = sequence.Id,
            Name = sequence.Name,
            Items = sequence.Items.Select(MapSequenceItem).ToList()
        };

    private static MotionSequenceItem MapSequenceItem(GeneratedMotionSequenceItem dto)
        => new()
        {
            ClipId = dto.ClipId ?? throw new InvalidOperationException("Motion sequence item clipId is required"),
            StartFrame = dto.StartFrame.HasValue ? checked((int)dto.StartFrame.Value) : null,
            DurationFrames = dto.DurationFrames.HasValue ? checked((int)dto.DurationFrames.Value) : null,
            FillMode = ParseSequenceFillMode(dto.FillMode)
        };

    private static GeneratedMotionSequenceItem MapSequenceItem(MotionSequenceItem item)
        => new()
        {
            ClipId = item.ClipId,
            StartFrame = item.StartFrame,
            DurationFrames = item.DurationFrames,
            FillMode = item.FillMode switch
            {
                MotionSequenceFillMode.None => GeneratedMotionSequenceFillMode.None,
                MotionSequenceFillMode.HoldStart => GeneratedMotionSequenceFillMode.HoldStart,
                MotionSequenceFillMode.HoldEnd => GeneratedMotionSequenceFillMode.HoldEnd,
                MotionSequenceFillMode.HoldBoth => GeneratedMotionSequenceFillMode.HoldBoth,
                _ => GeneratedMotionSequenceFillMode.None
            }
        };

    private static MotionClip MapClip(GeneratedMotionClip dto)
        => new()
        {
            Id = dto.Id ?? throw new InvalidOperationException("Motion clip id is required"),
            Name = dto.Name ?? throw new InvalidOperationException("Motion clip name is required"),
            StartFrame = dto.StartFrame.HasValue ? checked((int)dto.StartFrame.Value) : 0,
            DurationFrames = checked((int)dto.DurationFrames),
            Tracks = (dto.Tracks ?? []).Select(MapTrack).ToList().AsReadOnly()
        };

    private static GeneratedMotionClip MapClip(MotionClip clip)
        => new()
        {
            Id = clip.Id,
            Name = clip.Name,
            StartFrame = clip.StartFrame,
            DurationFrames = clip.DurationFrames,
            Tracks = clip.Tracks.Select(MapTrack).ToList()
        };

    private static MotionTrack MapTrack(GeneratedMotionTrack dto)
        => new()
        {
            Id = dto.Id ?? throw new InvalidOperationException("Motion track id is required"),
            TargetId = dto.TargetId ?? throw new InvalidOperationException("Motion track targetId is required"),
            TargetKind = ParseTargetKind(dto.TargetKind),
            Channels = (dto.Channels ?? []).Select(MapChannel).ToList().AsReadOnly()
        };

    private static GeneratedMotionTrack MapTrack(MotionTrack track)
        => new()
        {
            Id = track.Id,
            TargetId = track.TargetId,
            TargetKind = track.TargetKind switch
            {
                MotionTargetKind.Element => GeneratedTargetKind.Element,
                MotionTargetKind.Component => GeneratedTargetKind.Component,
                MotionTargetKind.Root => GeneratedTargetKind.Root,
                _ => GeneratedTargetKind.Element
            },
            Channels = track.Channels.Select(MapChannel).ToList()
        };

    private static MotionChannel MapChannel(GeneratedMotionChannel dto)
        => new()
        {
            Property = ParseProperty(dto.Property),
            Keyframes = (dto.Keyframes ?? []).Select(MapKeyframe).ToList().AsReadOnly()
        };

    private static GeneratedMotionChannel MapChannel(MotionChannel channel)
        => new()
        {
            Property = channel.Property switch
            {
                MotionProperty.Opacity => GeneratedProperty.Opacity,
                MotionProperty.PositionX => GeneratedProperty.PositionX,
                MotionProperty.PositionY => GeneratedProperty.PositionY,
                MotionProperty.PositionZ => GeneratedProperty.PositionZ,
                MotionProperty.ScaleX => GeneratedProperty.ScaleX,
                MotionProperty.ScaleY => GeneratedProperty.ScaleY,
                MotionProperty.ScaleZ => GeneratedProperty.ScaleZ,
                MotionProperty.Rotation => GeneratedProperty.Rotation,
                MotionProperty.RotationX => GeneratedProperty.RotationX,
                MotionProperty.RotationY => GeneratedProperty.RotationY,
                MotionProperty.Width => GeneratedProperty.Width,
                MotionProperty.Height => GeneratedProperty.Height,
                MotionProperty.Visibility => GeneratedProperty.Visibility,
                MotionProperty.Text => GeneratedProperty.Text,
                MotionProperty.SpriteFrame => GeneratedProperty.SpriteFrame,
                MotionProperty.Color => GeneratedProperty.Color,
                _ => GeneratedProperty.Opacity
            },
            Keyframes = channel.Keyframes.Select(MapKeyframe).ToList()
        };

    private static MotionKeyframe MapKeyframe(GeneratedMotionKeyframe dto)
        => new()
        {
            Frame = checked((int)dto.Frame),
            Value = MapValue(dto.Value ?? throw new InvalidOperationException("Motion keyframe value is required")),
            Easing = ParseEasing(dto.Easing)
        };

    private static GeneratedMotionKeyframe MapKeyframe(MotionKeyframe keyframe)
        => new()
        {
            Frame = keyframe.Frame,
            Value = MapValue(keyframe.Value),
            Easing = keyframe.Easing switch
            {
                MotionEasing.Linear => GeneratedEasing.Linear,
                MotionEasing.EaseIn => GeneratedEasing.EaseIn,
                MotionEasing.EaseOut => GeneratedEasing.EaseOut,
                MotionEasing.EaseInOut => GeneratedEasing.EaseInOut,
                MotionEasing.Step => GeneratedEasing.Step,
                _ => GeneratedEasing.Linear
            }
        };

    private static MotionValue MapValue(GeneratedMotionValue dto)
        => dto.Kind switch
        {
            GeneratedKind.Number when dto.Number.HasValue => MotionValue.FromNumber(dto.Number.Value),
            GeneratedKind.Boolean when dto.Boolean.HasValue => MotionValue.FromBoolean(dto.Boolean.Value),
            GeneratedKind.Text when dto.Text is not null => MotionValue.FromText(dto.Text),
            GeneratedKind.Vector when dto.Vector is not null => MotionValue.FromVector(dto.Vector.ToArray()),
            _ => throw new InvalidOperationException($"Motion value payload does not match kind '{dto.Kind}'")
        };

    private static GeneratedMotionValue MapValue(MotionValue value)
        => value.Kind switch
        {
            MotionValueKind.Number => new GeneratedMotionValue
            {
                Kind = GeneratedKind.Number,
                Number = value.Number
            },
            MotionValueKind.Boolean => new GeneratedMotionValue
            {
                Kind = GeneratedKind.Boolean,
                Boolean = value.Boolean
            },
            MotionValueKind.Text => new GeneratedMotionValue
            {
                Kind = GeneratedKind.Text,
                Text = value.Text ?? throw new InvalidOperationException("Text motion values require a text payload")
            },
            MotionValueKind.Vector => new GeneratedMotionValue
            {
                Kind = GeneratedKind.Vector,
                Vector = value.Vector?.ToList() ?? throw new InvalidOperationException("Vector motion values require a vector payload")
            },
            _ => throw new InvalidOperationException($"Unsupported motion value kind '{value.Kind}'")
        };

    private static MotionTargetKind ParseTargetKind(GeneratedTargetKind? targetKind)
        => targetKind switch
        {
            GeneratedTargetKind.Element => MotionTargetKind.Element,
            GeneratedTargetKind.Component => MotionTargetKind.Component,
            GeneratedTargetKind.Root => MotionTargetKind.Root,
            _ => MotionTargetKind.Element
        };

    private static MotionProperty ParseProperty(GeneratedProperty property)
        => property switch
        {
            GeneratedProperty.Opacity => MotionProperty.Opacity,
            GeneratedProperty.PositionX => MotionProperty.PositionX,
            GeneratedProperty.PositionY => MotionProperty.PositionY,
            GeneratedProperty.PositionZ => MotionProperty.PositionZ,
            GeneratedProperty.ScaleX => MotionProperty.ScaleX,
            GeneratedProperty.ScaleY => MotionProperty.ScaleY,
            GeneratedProperty.ScaleZ => MotionProperty.ScaleZ,
            GeneratedProperty.Rotation => MotionProperty.Rotation,
            GeneratedProperty.RotationX => MotionProperty.RotationX,
            GeneratedProperty.RotationY => MotionProperty.RotationY,
            GeneratedProperty.Width => MotionProperty.Width,
            GeneratedProperty.Height => MotionProperty.Height,
            GeneratedProperty.Visibility => MotionProperty.Visibility,
            GeneratedProperty.Text => MotionProperty.Text,
            GeneratedProperty.SpriteFrame => MotionProperty.SpriteFrame,
            GeneratedProperty.Color => MotionProperty.Color,
            _ => throw new InvalidOperationException($"Unsupported motion property '{property}'")
        };

    private static MotionEasing ParseEasing(GeneratedEasing? easing)
        => easing switch
        {
            GeneratedEasing.Linear => MotionEasing.Linear,
            GeneratedEasing.EaseIn => MotionEasing.EaseIn,
            GeneratedEasing.EaseOut => MotionEasing.EaseOut,
            GeneratedEasing.EaseInOut => MotionEasing.EaseInOut,
            GeneratedEasing.Step => MotionEasing.Step,
            _ => MotionEasing.Linear
        };

    private static MotionSequenceFillMode ParseSequenceFillMode(GeneratedMotionSequenceFillMode? fillMode)
        => fillMode switch
        {
            GeneratedMotionSequenceFillMode.HoldStart => MotionSequenceFillMode.HoldStart,
            GeneratedMotionSequenceFillMode.HoldEnd => MotionSequenceFillMode.HoldEnd,
            GeneratedMotionSequenceFillMode.HoldBoth => MotionSequenceFillMode.HoldBoth,
            _ => MotionSequenceFillMode.None
        };

    private static readonly System.Text.Json.JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };
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
/// A named staged sequence composed from motion clips in the same document.
/// </summary>
public sealed record MotionSequence
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<MotionSequenceItem> Items { get; init; } = [];
}

/// <summary>
/// Placement of a clip within a staged motion sequence.
/// </summary>
public sealed record MotionSequenceItem
{
    public required string ClipId { get; init; }
    public int? StartFrame { get; init; }
    public int? DurationFrames { get; init; }
    public MotionSequenceFillMode FillMode { get; init; } = MotionSequenceFillMode.None;
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
/// Sequence fill behavior for staged motion playback.
/// </summary>
public enum MotionSequenceFillMode
{
    None,
    HoldStart,
    HoldEnd,
    HoldBoth
}

/// <summary>
/// Tagged motion value union for portable, schema-backed keyframe payloads.
/// </summary>
public sealed record MotionValue
{
    public static MotionValue Empty { get; } = new();

    public MotionValueKind Kind { get; init; }
    public double? Number { get; init; }
    public bool? Boolean { get; init; }
    public string? Text { get; init; }
    public IReadOnlyList<double>? Vector { get; init; }

    public static MotionValue FromNumber(double value) => new() { Kind = MotionValueKind.Number, Number = value };
    public static MotionValue FromBoolean(bool value) => new() { Kind = MotionValueKind.Boolean, Boolean = value };
    public static MotionValue FromText(string value) => new() { Kind = MotionValueKind.Text, Text = value };
    public static MotionValue FromVector(params double[] values) => new() { Kind = MotionValueKind.Vector, Vector = values };
}

/// <summary>
/// Tagged motion value kinds supported by the JSON contract.
/// </summary>
public enum MotionValueKind
{
    None,
    Number,
    Boolean,
    Text,
    Vector
}
