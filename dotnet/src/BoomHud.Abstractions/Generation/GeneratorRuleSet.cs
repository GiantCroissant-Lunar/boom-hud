using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;

namespace BoomHud.Abstractions.Generation;

public sealed record GeneratorRuleSet
{
    public string Version { get; init; } = "1.0";

    public IReadOnlyList<GeneratorRule> Rules { get; init; } = [];

    public static GeneratorRuleSet LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Generator rule set not found: {filePath}", filePath);
        }

        return LoadFromJson(File.ReadAllText(filePath));
    }

    public static GeneratorRuleSet LoadFromJson(string json)
        => JsonSerializer.Deserialize<GeneratorRuleSet>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize generator rule set.");

    public string ToJson()
        => JsonSerializer.Serialize(this, WriteOptions);

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions WriteOptions = new(JsonOptions)
    {
        WriteIndented = true
    };
}

public sealed record GeneratorRule
{
    public string? Name { get; init; }

    public GeneratorRulePhase? Phase { get; init; }

    public double? Cost { get; init; }

    public IReadOnlyList<GeneratorRuleFact> Preconditions { get; init; } = [];

    public IReadOnlyList<GeneratorRuleFact> Effects { get; init; } = [];

    public GeneratorActionTemplate? Template { get; init; }

    public GeneratorRuleSelector Selector { get; init; } = new();

    public GeneratorRuleAction Action { get; init; } = new();
}

public sealed record GeneratorRuleFact
{
    public string Key { get; init; } = string.Empty;

    public string? Value { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GeneratorRulePhase
{
    Normalize,
    Structure,
    Layout,
    Text,
    Icon,
    Motion,
    Finalize
}

public sealed record GeneratorRuleSelector
{
    public string? Backend { get; init; }

    public string? DocumentName { get; init; }

    public string? NodeId { get; init; }

    public string? SourceNodeId { get; init; }

    public ComponentType? ComponentType { get; init; }

    public string? FontFamily { get; init; }

    public string? TextGrowth { get; init; }

    public string? SemanticClass { get; init; }

    public string? SizeBand { get; init; }

    public string? MetadataKey { get; init; }

    public string? MetadataValue { get; init; }

    public string? ClipId { get; init; }

    public string? TrackId { get; init; }

    public string? TargetId { get; init; }

    public MotionProperty? MotionProperty { get; init; }

    public string? SequenceId { get; init; }
}

public sealed record GeneratorRuleAction
{
    public string? ControlType { get; init; }

    public GeneratorTextRuleAction? Text { get; init; }

    public GeneratorIconRuleAction? Icon { get; init; }

    public GeneratorLayoutRuleAction? Layout { get; init; }

    public GeneratorMotionRuleAction? Motion { get; init; }
}

public sealed record GeneratorTextRuleAction
{
    public double? LineHeight { get; init; }

    public bool? WrapText { get; init; }

    public string? FontFamily { get; init; }

    public double? FontSize { get; init; }

    public double? FontSizeDelta { get; init; }

    public double? LetterSpacing { get; init; }

    public double? LetterSpacingDelta { get; init; }

    public string? TextGrowth { get; init; }
}

public sealed record GeneratorIconRuleAction
{
    public double? BaselineOffset { get; init; }

    public bool? OpticalCentering { get; init; }

    public string? SizeMode { get; init; }

    public double? FontSize { get; init; }

    public double? FontSizeDelta { get; init; }

    public double? BaselineOffsetDelta { get; init; }
}

public sealed record GeneratorLayoutRuleAction
{
    public bool? ForceAbsolutePositioning { get; init; }

    public bool? StretchWidth { get; init; }

    public bool? StretchHeight { get; init; }

    public bool? PreferContentWidth { get; init; }

    public bool? PreferContentHeight { get; init; }

    public double? PreferredWidthDelta { get; init; }

    public double? PreferredHeightDelta { get; init; }

    public string? EdgeAlignment { get; init; }

    public double? Gap { get; init; }

    public double? GapDelta { get; init; }

    public double? Padding { get; init; }

    public double? PaddingDelta { get; init; }

    public double? PaddingTop { get; init; }

    public double? PaddingTopDelta { get; init; }

    public double? PaddingRight { get; init; }

    public double? PaddingRightDelta { get; init; }

    public double? PaddingBottom { get; init; }

    public double? PaddingBottomDelta { get; init; }

    public double? PaddingLeft { get; init; }

    public double? PaddingLeftDelta { get; init; }

    public double? OffsetX { get; init; }

    public double? OffsetXDelta { get; init; }

    public double? OffsetY { get; init; }

    public double? OffsetYDelta { get; init; }

    public double? InsetTop { get; init; }

    public double? InsetTopDelta { get; init; }

    public double? InsetRight { get; init; }

    public double? InsetRightDelta { get; init; }

    public double? InsetBottom { get; init; }

    public double? InsetBottomDelta { get; init; }

    public double? InsetLeft { get; init; }

    public double? InsetLeftDelta { get; init; }

    public string? AnchorPreset { get; init; }

    public string? PivotPreset { get; init; }

    public string? EdgeInsetPolicy { get; init; }

    public string? RectTransformMode { get; init; }

    public string? PositionMode { get; init; }

    public string? FlexAlignmentPreset { get; init; }
}

public sealed record GeneratorMotionRuleAction
{
    public int? DurationQuantizationFrames { get; init; }

    public MotionEasing? EasingRemapTo { get; init; }

    public MotionSequenceFillMode? SequenceFillMode { get; init; }

    public string? DefaultSequenceId { get; init; }

    public int? ClipStartOffsetFrames { get; init; }

    public bool? ForceStepText { get; init; }

    public bool? ForceStepVisibility { get; init; }

    public string? RuntimePropertySupportFallback { get; init; }

    public string? TargetResolutionPolicy { get; init; }

    public string? SequenceGroupingPolicy { get; init; }
}

public sealed record GeneratorActionTemplate
{
    public string? Kind { get; init; }

    public double? NumberValue { get; init; }

    public string? StringValue { get; init; }

    public bool? BoolValue { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record MotionRuleContext
{
    public string? ClipId { get; init; }

    public string? TrackId { get; init; }

    public string? TargetId { get; init; }

    public MotionProperty? MotionProperty { get; init; }

    public string? SequenceId { get; init; }
}

public sealed record ResolvedGeneratorPolicy
{
    public string? ControlType { get; init; }

    public ResolvedGeneratorTextPolicy Text { get; init; } = new();

    public ResolvedGeneratorIconPolicy Icon { get; init; } = new();

    public ResolvedGeneratorLayoutPolicy Layout { get; init; } = new();

    public ResolvedGeneratorMotionPolicy Motion { get; init; } = new();

    public ResolvedGeneratorPolicy Apply(GeneratorRuleAction action)
        => this with
        {
            ControlType = action.ControlType ?? ControlType,
            Text = Text.Apply(action.Text),
            Icon = Icon.Apply(action.Icon),
            Layout = Layout.Apply(action.Layout),
            Motion = Motion.Apply(action.Motion)
        };
}

public sealed record ResolvedGeneratorTextPolicy
{
    public double? LineHeight { get; init; }

    public bool? WrapText { get; init; }

    public string? FontFamily { get; init; }

    public double? FontSize { get; init; }

    public double? FontSizeDelta { get; init; }

    public double? LetterSpacing { get; init; }

    public double? LetterSpacingDelta { get; init; }

    public string? TextGrowth { get; init; }

    public ResolvedGeneratorTextPolicy Apply(GeneratorTextRuleAction? action)
        => action == null
            ? this
            : this with
            {
                LineHeight = action.LineHeight ?? LineHeight,
                WrapText = action.WrapText ?? WrapText,
                FontFamily = action.FontFamily ?? FontFamily,
                FontSize = action.FontSize ?? FontSize,
                FontSizeDelta = action.FontSize is { } ? null : AddDelta(FontSizeDelta, action.FontSizeDelta),
                LetterSpacing = action.LetterSpacing ?? LetterSpacing,
                LetterSpacingDelta = action.LetterSpacing is { } ? null : AddDelta(LetterSpacingDelta, action.LetterSpacingDelta),
                TextGrowth = action.TextGrowth ?? TextGrowth
            };

    private static double? AddDelta(double? baseline, double? delta)
        => delta is { } value
            ? (baseline ?? 0d) + value
            : baseline;
}

public sealed record ResolvedGeneratorIconPolicy
{
    public double? BaselineOffset { get; init; }

    public bool? OpticalCentering { get; init; }

    public string? SizeMode { get; init; }

    public double? FontSize { get; init; }

    public double? FontSizeDelta { get; init; }

    public ResolvedGeneratorIconPolicy Apply(GeneratorIconRuleAction? action)
        => action == null
            ? this
            : this with
            {
                BaselineOffset = action.BaselineOffset ?? AddDelta(BaselineOffset, action.BaselineOffsetDelta),
                OpticalCentering = action.OpticalCentering ?? OpticalCentering,
                SizeMode = action.SizeMode ?? SizeMode,
                FontSize = action.FontSize ?? FontSize,
                FontSizeDelta = action.FontSize is { } ? null : AddDelta(FontSizeDelta, action.FontSizeDelta)
            };

    private static double? AddDelta(double? baseline, double? delta)
        => delta is { } value
            ? (baseline ?? 0d) + value
            : baseline;
}

public sealed record ResolvedGeneratorLayoutPolicy
{
    public bool? ForceAbsolutePositioning { get; init; }

    public bool? StretchWidth { get; init; }

    public bool? StretchHeight { get; init; }

    public bool? PreferContentWidth { get; init; }

    public bool? PreferContentHeight { get; init; }

    public double? PreferredWidthDelta { get; init; }

    public double? PreferredHeightDelta { get; init; }

    public string? EdgeAlignment { get; init; }

    public double? Gap { get; init; }

    public double? GapDelta { get; init; }

    public double? Padding { get; init; }

    public double? PaddingDelta { get; init; }

    public double? PaddingTop { get; init; }

    public double? PaddingTopDelta { get; init; }

    public double? PaddingRight { get; init; }

    public double? PaddingRightDelta { get; init; }

    public double? PaddingBottom { get; init; }

    public double? PaddingBottomDelta { get; init; }

    public double? PaddingLeft { get; init; }

    public double? PaddingLeftDelta { get; init; }

    public double? OffsetX { get; init; }

    public double? OffsetXDelta { get; init; }

    public double? OffsetY { get; init; }

    public double? OffsetYDelta { get; init; }

    public double? InsetTop { get; init; }

    public double? InsetTopDelta { get; init; }

    public double? InsetRight { get; init; }

    public double? InsetRightDelta { get; init; }

    public double? InsetBottom { get; init; }

    public double? InsetBottomDelta { get; init; }

    public double? InsetLeft { get; init; }

    public double? InsetLeftDelta { get; init; }

    public ResolvedGeneratorLayoutPolicy Apply(GeneratorLayoutRuleAction? action)
        => action == null
            ? this
            : this with
            {
                ForceAbsolutePositioning = action.ForceAbsolutePositioning ?? ForceAbsolutePositioning,
                StretchWidth = action.StretchWidth ?? StretchWidth,
                StretchHeight = action.StretchHeight ?? StretchHeight,
                PreferContentWidth = action.PreferContentWidth ?? PreferContentWidth,
                PreferContentHeight = action.PreferContentHeight ?? PreferContentHeight,
                PreferredWidthDelta = AddDelta(PreferredWidthDelta, action.PreferredWidthDelta),
                PreferredHeightDelta = AddDelta(PreferredHeightDelta, action.PreferredHeightDelta),
                EdgeAlignment = action.EdgeAlignment ?? EdgeAlignment,
                Gap = action.Gap ?? Gap,
                GapDelta = AddDelta(GapDelta, action.GapDelta),
                Padding = action.Padding ?? Padding,
                PaddingDelta = AddDelta(PaddingDelta, action.PaddingDelta),
                PaddingTop = action.PaddingTop ?? PaddingTop,
                PaddingTopDelta = AddDelta(PaddingTopDelta, action.PaddingTopDelta),
                PaddingRight = action.PaddingRight ?? PaddingRight,
                PaddingRightDelta = AddDelta(PaddingRightDelta, action.PaddingRightDelta),
                PaddingBottom = action.PaddingBottom ?? PaddingBottom,
                PaddingBottomDelta = AddDelta(PaddingBottomDelta, action.PaddingBottomDelta),
                PaddingLeft = action.PaddingLeft ?? PaddingLeft,
                PaddingLeftDelta = AddDelta(PaddingLeftDelta, action.PaddingLeftDelta),
                OffsetX = action.OffsetX ?? OffsetX,
                OffsetXDelta = AddDelta(OffsetXDelta, action.OffsetXDelta),
                OffsetY = action.OffsetY ?? OffsetY,
                OffsetYDelta = AddDelta(OffsetYDelta, action.OffsetYDelta),
                InsetTop = action.InsetTop ?? InsetTop,
                InsetTopDelta = AddDelta(InsetTopDelta, action.InsetTopDelta),
                InsetRight = action.InsetRight ?? InsetRight,
                InsetRightDelta = AddDelta(InsetRightDelta, action.InsetRightDelta),
                InsetBottom = action.InsetBottom ?? InsetBottom,
                InsetBottomDelta = AddDelta(InsetBottomDelta, action.InsetBottomDelta),
                InsetLeft = action.InsetLeft ?? InsetLeft,
                InsetLeftDelta = AddDelta(InsetLeftDelta, action.InsetLeftDelta),
                AnchorPreset = action.AnchorPreset ?? AnchorPreset,
                PivotPreset = action.PivotPreset ?? PivotPreset,
                EdgeInsetPolicy = action.EdgeInsetPolicy ?? EdgeInsetPolicy,
                RectTransformMode = action.RectTransformMode ?? RectTransformMode,
                PositionMode = action.PositionMode ?? PositionMode,
                FlexAlignmentPreset = action.FlexAlignmentPreset ?? FlexAlignmentPreset
            };

    public string? AnchorPreset { get; init; }

    public string? PivotPreset { get; init; }

    public string? EdgeInsetPolicy { get; init; }

    public string? RectTransformMode { get; init; }

    public string? PositionMode { get; init; }

    public string? FlexAlignmentPreset { get; init; }

    private static double? AddDelta(double? baseline, double? delta)
        => delta is { } value
            ? (baseline ?? 0d) + value
            : baseline;
}

public sealed record ResolvedGeneratorMotionPolicy
{
    public int? DurationQuantizationFrames { get; init; }

    public MotionEasing? EasingRemapTo { get; init; }

    public MotionSequenceFillMode? SequenceFillMode { get; init; }

    public string? DefaultSequenceId { get; init; }

    public int? ClipStartOffsetFrames { get; init; }

    public bool? ForceStepText { get; init; }

    public bool? ForceStepVisibility { get; init; }

    public string? RuntimePropertySupportFallback { get; init; }

    public string? TargetResolutionPolicy { get; init; }

    public string? SequenceGroupingPolicy { get; init; }

    public ResolvedGeneratorMotionPolicy Apply(GeneratorMotionRuleAction? action)
        => action == null
            ? this
            : this with
            {
                DurationQuantizationFrames = action.DurationQuantizationFrames ?? DurationQuantizationFrames,
                EasingRemapTo = action.EasingRemapTo ?? EasingRemapTo,
                SequenceFillMode = action.SequenceFillMode ?? SequenceFillMode,
                DefaultSequenceId = action.DefaultSequenceId ?? DefaultSequenceId,
                ClipStartOffsetFrames = action.ClipStartOffsetFrames ?? ClipStartOffsetFrames,
                ForceStepText = action.ForceStepText ?? ForceStepText,
                ForceStepVisibility = action.ForceStepVisibility ?? ForceStepVisibility,
                RuntimePropertySupportFallback = action.RuntimePropertySupportFallback ?? RuntimePropertySupportFallback,
                TargetResolutionPolicy = action.TargetResolutionPolicy ?? TargetResolutionPolicy,
                SequenceGroupingPolicy = action.SequenceGroupingPolicy ?? SequenceGroupingPolicy
            };
}

public static class GeneratorRuleMetadata
{
    public static string? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            bool boolean => boolean.ToString().ToLowerInvariant(),
            float number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }
}
