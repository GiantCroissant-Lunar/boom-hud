using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.Motion;

namespace BoomHud.Generators;

public static class GeneratorRuleExecutionCompiler
{
    public static GeneratorMetricProfile Compile(GeneratorMetricProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Template == null || string.IsNullOrWhiteSpace(profile.Template.Kind))
        {
            return profile;
        }

        var compiledAction = Merge(profile.Action, CompileTemplate(profile.Template));
        return profile with
        {
            Action = compiledAction
        };
    }

    public static GeneratorRule Compile(GeneratorRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (rule.Template == null || string.IsNullOrWhiteSpace(rule.Template.Kind))
        {
            return rule;
        }

        var compiledAction = Merge(rule.Action, CompileTemplate(rule.Template));
        return rule with
        {
            Action = compiledAction
        };
    }

    private static GeneratorRuleAction CompileTemplate(GeneratorActionTemplate template)
    {
        var kind = template.Kind?.Trim();
        if (string.IsNullOrWhiteSpace(kind))
        {
            return new GeneratorRuleAction();
        }

        return kind switch
        {
            "controlRemap" => new GeneratorRuleAction
            {
                ControlType = GetString(template, "controlType")
            },
            "fontSizeDelta" => new GeneratorRuleAction
            {
                Text = new GeneratorTextRuleAction
                {
                    FontSizeDelta = template.NumberValue
                }
            },
            "lineHeightMode" => new GeneratorRuleAction
            {
                Text = new GeneratorTextRuleAction
                {
                    LineHeight = template.NumberValue
                }
            },
            "letterSpacingDelta" => new GeneratorRuleAction
            {
                Text = new GeneratorTextRuleAction
                {
                    LetterSpacingDelta = template.NumberValue
                }
            },
            "wrapPolicy" => new GeneratorRuleAction
            {
                Text = new GeneratorTextRuleAction
                {
                    WrapText = template.BoolValue
                }
            },
            "textGrowthPolicy" => new GeneratorRuleAction
            {
                Text = new GeneratorTextRuleAction
                {
                    TextGrowth = GetString(template, "textGrowth")
                }
            },
            "gapDelta" => new GeneratorRuleAction
            {
                Layout = new GeneratorLayoutRuleAction
                {
                    GapDelta = template.NumberValue
                }
            },
            "paddingDelta" => new GeneratorRuleAction
            {
                Layout = new GeneratorLayoutRuleAction
                {
                    PaddingDelta = template.NumberValue
                }
            },
            "offsetDelta" => CompileOffsetDelta(template),
            "stretchPolicy" => CompileStretchPolicy(template),
            "preferContentPolicy" => CompilePreferContentPolicy(template),
            "absolutePositionPolicy" => new GeneratorRuleAction
            {
                Layout = new GeneratorLayoutRuleAction
                {
                    ForceAbsolutePositioning = template.BoolValue
                }
            },
            "iconFontSizeDelta" => new GeneratorRuleAction
            {
                Icon = new GeneratorIconRuleAction
                {
                    FontSizeDelta = template.NumberValue
                }
            },
            "iconCenteringPolicy" => new GeneratorRuleAction
            {
                Icon = new GeneratorIconRuleAction
                {
                    OpticalCentering = template.BoolValue
                }
            },
            "iconBaselineOffsetDelta" => new GeneratorRuleAction
            {
                Icon = new GeneratorIconRuleAction
                {
                    BaselineOffsetDelta = template.NumberValue
                }
            },
            "anchorPreset" => new GeneratorRuleAction
            {
                Layout = new GeneratorLayoutRuleAction
                {
                    AnchorPreset = GetString(template, "anchorPreset")
                }
            },
            "pivotPreset" => new GeneratorRuleAction
            {
                Layout = new GeneratorLayoutRuleAction
                {
                    PivotPreset = GetString(template, "pivotPreset")
                }
            },
            "edgeInsetPolicy" => new GeneratorRuleAction
            {
                Layout = new GeneratorLayoutRuleAction
                {
                    EdgeInsetPolicy = GetString(template, "edgeInsetPolicy")
                }
            },
            "rectTransformMode" => new GeneratorRuleAction
            {
                Layout = new GeneratorLayoutRuleAction
                {
                    RectTransformMode = GetString(template, "rectTransformMode")
                }
            },
            "positionMode" => new GeneratorRuleAction
            {
                Layout = new GeneratorLayoutRuleAction
                {
                    PositionMode = GetString(template, "positionMode")
                }
            },
            "translateAdjustment" => CompileOffsetDelta(template),
            "flexAlignmentPreset" => new GeneratorRuleAction
            {
                Layout = new GeneratorLayoutRuleAction
                {
                    FlexAlignmentPreset = GetString(template, "flexAlignmentPreset")
                }
            },
            "durationQuantization" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    DurationQuantizationFrames = ToInt(template.NumberValue)
                }
            },
            "easingRemap" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    EasingRemapTo = ParseEnum<MotionEasing>(GetString(template, "easing"))
                }
            },
            "fillModePolicy" or "timelineSequenceFillPreset" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    SequenceFillMode = ParseEnum<MotionSequenceFillMode>(GetString(template, "fillMode"))
                }
            },
            "defaultSequenceSelection" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    DefaultSequenceId = GetString(template, "defaultSequenceId")
                }
            },
            "clipStartAdjustment" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    ClipStartOffsetFrames = ToInt(template.NumberValue)
                }
            },
            "sequenceGroupingPolicy" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    SequenceGroupingPolicy = GetString(template, "sequenceGroupingPolicy")
                }
            },
            "textStepPolicy" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    ForceStepText = template.BoolValue
                }
            },
            "visibilityStepPolicy" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    ForceStepVisibility = template.BoolValue
                }
            },
            "runtimePropertySupportFallback" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    RuntimePropertySupportFallback = GetString(template, "runtimePropertySupportFallback")
                }
            },
            "targetResolutionPolicy" => new GeneratorRuleAction
            {
                Motion = new GeneratorMotionRuleAction
                {
                    TargetResolutionPolicy = GetString(template, "targetResolutionPolicy")
                }
            },
            _ => new GeneratorRuleAction()
        };
    }

    private static GeneratorRuleAction CompileOffsetDelta(GeneratorActionTemplate template)
    {
        var axis = GetString(template, "axis")?.ToLowerInvariant();
        return new GeneratorRuleAction
        {
            Layout = axis switch
            {
                "x" => new GeneratorLayoutRuleAction { OffsetXDelta = template.NumberValue },
                "y" => new GeneratorLayoutRuleAction { OffsetYDelta = template.NumberValue },
                _ => new GeneratorLayoutRuleAction
                {
                    OffsetXDelta = template.NumberValue,
                    OffsetYDelta = template.NumberValue
                }
            }
        };
    }

    private static GeneratorRuleAction CompileStretchPolicy(GeneratorActionTemplate template)
    {
        var axis = GetString(template, "axis")?.ToLowerInvariant();
        return new GeneratorRuleAction
        {
            Layout = axis switch
            {
                "width" => new GeneratorLayoutRuleAction { StretchWidth = template.BoolValue },
                "height" => new GeneratorLayoutRuleAction { StretchHeight = template.BoolValue },
                _ => new GeneratorLayoutRuleAction
                {
                    StretchWidth = template.BoolValue,
                    StretchHeight = template.BoolValue
                }
            }
        };
    }

    private static GeneratorRuleAction CompilePreferContentPolicy(GeneratorActionTemplate template)
    {
        var axis = GetString(template, "axis")?.ToLowerInvariant();
        return new GeneratorRuleAction
        {
            Layout = axis switch
            {
                "width" => new GeneratorLayoutRuleAction { PreferContentWidth = template.BoolValue },
                "height" => new GeneratorLayoutRuleAction { PreferContentHeight = template.BoolValue },
                _ => new GeneratorLayoutRuleAction
                {
                    PreferContentWidth = template.BoolValue,
                    PreferContentHeight = template.BoolValue
                }
            }
        };
    }

    private static GeneratorRuleAction Merge(GeneratorRuleAction existing, GeneratorRuleAction compiled)
        => new()
        {
            ControlType = compiled.ControlType ?? existing.ControlType,
            Text = Merge(existing.Text, compiled.Text),
            Icon = Merge(existing.Icon, compiled.Icon),
            Layout = Merge(existing.Layout, compiled.Layout),
            Motion = Merge(existing.Motion, compiled.Motion)
        };

    private static GeneratorTextRuleAction? Merge(GeneratorTextRuleAction? existing, GeneratorTextRuleAction? compiled)
        => existing == null ? compiled : compiled == null ? existing : new GeneratorTextRuleAction
        {
            LineHeight = compiled.LineHeight ?? existing.LineHeight,
            WrapText = compiled.WrapText ?? existing.WrapText,
            FontFamily = compiled.FontFamily ?? existing.FontFamily,
            FontSize = compiled.FontSize ?? existing.FontSize,
            FontSizeDelta = compiled.FontSizeDelta ?? existing.FontSizeDelta,
            LetterSpacing = compiled.LetterSpacing ?? existing.LetterSpacing,
            LetterSpacingDelta = compiled.LetterSpacingDelta ?? existing.LetterSpacingDelta,
            TextGrowth = compiled.TextGrowth ?? existing.TextGrowth
        };

    private static GeneratorIconRuleAction? Merge(GeneratorIconRuleAction? existing, GeneratorIconRuleAction? compiled)
        => existing == null ? compiled : compiled == null ? existing : new GeneratorIconRuleAction
        {
            BaselineOffset = compiled.BaselineOffset ?? existing.BaselineOffset,
            OpticalCentering = compiled.OpticalCentering ?? existing.OpticalCentering,
            SizeMode = compiled.SizeMode ?? existing.SizeMode,
            FontSize = compiled.FontSize ?? existing.FontSize,
            FontSizeDelta = compiled.FontSizeDelta ?? existing.FontSizeDelta,
            BaselineOffsetDelta = compiled.BaselineOffsetDelta ?? existing.BaselineOffsetDelta
        };

    private static GeneratorLayoutRuleAction? Merge(GeneratorLayoutRuleAction? existing, GeneratorLayoutRuleAction? compiled)
        => existing == null ? compiled : compiled == null ? existing : new GeneratorLayoutRuleAction
        {
            ForceAbsolutePositioning = compiled.ForceAbsolutePositioning ?? existing.ForceAbsolutePositioning,
            StretchWidth = compiled.StretchWidth ?? existing.StretchWidth,
            StretchHeight = compiled.StretchHeight ?? existing.StretchHeight,
            PreferContentWidth = compiled.PreferContentWidth ?? existing.PreferContentWidth,
            PreferContentHeight = compiled.PreferContentHeight ?? existing.PreferContentHeight,
            EdgeAlignment = compiled.EdgeAlignment ?? existing.EdgeAlignment,
            Gap = compiled.Gap ?? existing.Gap,
            GapDelta = compiled.GapDelta ?? existing.GapDelta,
            Padding = compiled.Padding ?? existing.Padding,
            PaddingDelta = compiled.PaddingDelta ?? existing.PaddingDelta,
            OffsetX = compiled.OffsetX ?? existing.OffsetX,
            OffsetXDelta = compiled.OffsetXDelta ?? existing.OffsetXDelta,
            OffsetY = compiled.OffsetY ?? existing.OffsetY,
            OffsetYDelta = compiled.OffsetYDelta ?? existing.OffsetYDelta,
            AnchorPreset = compiled.AnchorPreset ?? existing.AnchorPreset,
            PivotPreset = compiled.PivotPreset ?? existing.PivotPreset,
            EdgeInsetPolicy = compiled.EdgeInsetPolicy ?? existing.EdgeInsetPolicy,
            RectTransformMode = compiled.RectTransformMode ?? existing.RectTransformMode,
            PositionMode = compiled.PositionMode ?? existing.PositionMode,
            FlexAlignmentPreset = compiled.FlexAlignmentPreset ?? existing.FlexAlignmentPreset
        };

    private static GeneratorMotionRuleAction? Merge(GeneratorMotionRuleAction? existing, GeneratorMotionRuleAction? compiled)
        => existing == null ? compiled : compiled == null ? existing : new GeneratorMotionRuleAction
        {
            DurationQuantizationFrames = compiled.DurationQuantizationFrames ?? existing.DurationQuantizationFrames,
            EasingRemapTo = compiled.EasingRemapTo ?? existing.EasingRemapTo,
            SequenceFillMode = compiled.SequenceFillMode ?? existing.SequenceFillMode,
            DefaultSequenceId = compiled.DefaultSequenceId ?? existing.DefaultSequenceId,
            ClipStartOffsetFrames = compiled.ClipStartOffsetFrames ?? existing.ClipStartOffsetFrames,
            ForceStepText = compiled.ForceStepText ?? existing.ForceStepText,
            ForceStepVisibility = compiled.ForceStepVisibility ?? existing.ForceStepVisibility,
            RuntimePropertySupportFallback = compiled.RuntimePropertySupportFallback ?? existing.RuntimePropertySupportFallback,
            TargetResolutionPolicy = compiled.TargetResolutionPolicy ?? existing.TargetResolutionPolicy,
            SequenceGroupingPolicy = compiled.SequenceGroupingPolicy ?? existing.SequenceGroupingPolicy
        };

    private static string? GetString(GeneratorActionTemplate template, string parameterKey)
        => template.Parameters.TryGetValue(parameterKey, out var parameterValue)
            ? parameterValue
            : template.StringValue;

    private static int? ToInt(double? value)
        => value.HasValue ? (int?)Math.Round(value.Value) : null;

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct
        => !string.IsNullOrWhiteSpace(value)
           && Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;
}
