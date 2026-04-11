using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.Motion;

namespace BoomHud.Generators;

public static class MotionPolicyService
{
    public const string DefaultFallbackPolicy = "warn-skip";

    public static MotionDocument Apply(MotionDocument motion, RuleResolver resolver, string documentName)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(resolver);

        var documentPolicy = resolver.ResolveMotion(documentName, new MotionRuleContext());
        var transformedClips = motion.Clips
            .Select(clip => ApplyClip(documentName, resolver, clip))
            .ToList();

        var transformedSequences = motion.Sequences
            .Select(sequence => ApplySequence(documentName, resolver, transformedClips, sequence))
            .ToList();

        var defaultSequenceId = ResolveDefaultSequenceId(motion, transformedSequences, documentPolicy);

        return motion with
        {
            DefaultSequenceId = defaultSequenceId,
            Clips = transformedClips,
            Sequences = transformedSequences
        };
    }

    public static string ResolveTargetResolutionPolicy(RuleResolver resolver, string documentName, MotionTrack track, string? clipId = null)
        => NormalizeFallbackPolicy(resolver.ResolveMotion(documentName, new MotionRuleContext
        {
            ClipId = clipId,
            TrackId = track.Id,
            TargetId = track.TargetId
        }).TargetResolutionPolicy);

    public static string ResolveRuntimePropertySupportFallback(
        RuleResolver resolver,
        string documentName,
        MotionTrack track,
        MotionProperty property,
        string? clipId = null)
        => NormalizeFallbackPolicy(resolver.ResolveMotion(documentName, new MotionRuleContext
        {
            ClipId = clipId,
            TrackId = track.Id,
            TargetId = track.TargetId,
            MotionProperty = property
        }).RuntimePropertySupportFallback);

    public static bool IsErrorPolicy(string? policy)
        => string.Equals(NormalizeFallbackPolicy(policy), "error", StringComparison.OrdinalIgnoreCase);

    private static MotionClip ApplyClip(string documentName, RuleResolver resolver, MotionClip clip)
    {
        var clipPolicy = resolver.ResolveMotion(documentName, new MotionRuleContext
        {
            ClipId = clip.Id
        });

        var adjustedDuration = QuantizeDuration(clip.DurationFrames, clipPolicy.DurationQuantizationFrames);
        return clip with
        {
            StartFrame = clip.StartFrame + (clipPolicy.ClipStartOffsetFrames ?? 0),
            DurationFrames = adjustedDuration,
            Tracks = clip.Tracks
                .Select(track => ApplyTrack(documentName, resolver, clip.Id, track))
                .ToList()
        };
    }

    private static MotionTrack ApplyTrack(string documentName, RuleResolver resolver, string clipId, MotionTrack track)
    {
        return track with
        {
            Channels = track.Channels
                .Select(channel => ApplyChannel(documentName, resolver, clipId, track, channel))
                .ToList()
        };
    }

    private static MotionChannel ApplyChannel(
        string documentName,
        RuleResolver resolver,
        string clipId,
        MotionTrack track,
        MotionChannel channel)
    {
        var policy = resolver.ResolveMotion(documentName, new MotionRuleContext
        {
            ClipId = clipId,
            TrackId = track.Id,
            TargetId = track.TargetId,
            MotionProperty = channel.Property
        });

        var forceStep = channel.Property switch
        {
            MotionProperty.Text => policy.ForceStepText == true,
            MotionProperty.Visibility => policy.ForceStepVisibility == true,
            _ => false
        };

        return channel with
        {
            Keyframes = channel.Keyframes
                .Select(keyframe => keyframe with
                {
                    Easing = forceStep
                        ? MotionEasing.Step
                        : policy.EasingRemapTo ?? keyframe.Easing
                })
                .ToList()
        };
    }

    private static MotionSequence ApplySequence(
        string documentName,
        RuleResolver resolver,
        IReadOnlyList<MotionClip> clips,
        MotionSequence sequence)
    {
        var sequencePolicy = resolver.ResolveMotion(documentName, new MotionRuleContext
        {
            SequenceId = sequence.Id
        });

        return sequence with
        {
            Items = sequence.Items
                .Select(item =>
                {
                    var itemPolicy = resolver.ResolveMotion(documentName, new MotionRuleContext
                    {
                        SequenceId = sequence.Id,
                        ClipId = item.ClipId
                    });

                    var sourceClip = clips.FirstOrDefault(clip => string.Equals(clip.Id, item.ClipId, StringComparison.Ordinal));
                    var baseDuration = item.DurationFrames ?? sourceClip?.DurationFrames ?? 0;
                    return item with
                    {
                        StartFrame = (item.StartFrame ?? 0) + (itemPolicy.ClipStartOffsetFrames ?? sequencePolicy.ClipStartOffsetFrames ?? 0),
                        DurationFrames = QuantizeDuration(baseDuration, itemPolicy.DurationQuantizationFrames ?? sequencePolicy.DurationQuantizationFrames),
                        FillMode = itemPolicy.SequenceFillMode ?? sequencePolicy.SequenceFillMode ?? item.FillMode
                    };
                })
                .ToList()
        };
    }

    private static string? ResolveDefaultSequenceId(
        MotionDocument motion,
        List<MotionSequence> sequences,
        ResolvedGeneratorMotionPolicy documentPolicy)
    {
        if (!string.IsNullOrWhiteSpace(documentPolicy.DefaultSequenceId)
            && sequences.Any(sequence => string.Equals(sequence.Id, documentPolicy.DefaultSequenceId, StringComparison.Ordinal)))
        {
            return documentPolicy.DefaultSequenceId;
        }

        if (!string.IsNullOrWhiteSpace(motion.DefaultSequenceId)
            && sequences.Any(sequence => string.Equals(sequence.Id, motion.DefaultSequenceId, StringComparison.Ordinal)))
        {
            return motion.DefaultSequenceId;
        }

        return sequences.Count > 0 ? sequences[0].Id : null;
    }

    private static int QuantizeDuration(int durationFrames, int? quantizationFrames)
    {
        if (!quantizationFrames.HasValue || quantizationFrames.Value <= 1 || durationFrames <= 0)
        {
            return durationFrames;
        }

        var step = quantizationFrames.Value;
        var quantized = (int)Math.Round(durationFrames / (double)step, MidpointRounding.AwayFromZero) * step;
        return Math.Max(step, quantized);
    }

    private static string NormalizeFallbackPolicy(string? policy)
        => string.IsNullOrWhiteSpace(policy) ? DefaultFallbackPolicy : policy.Trim().ToLowerInvariant();
}
