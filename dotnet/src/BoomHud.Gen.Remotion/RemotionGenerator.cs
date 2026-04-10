using System.Globalization;
using System.Linq;
using System.Text;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Gen.React;

namespace BoomHud.Gen.Remotion;

/// <summary>
/// Code generator for Remotion compositions and composition helpers.
/// </summary>
public sealed class RemotionGenerator : IBackendGenerator
{
    private const string SequenceTypeName = "MotionSequence";
    private const string FillModeNone = "none";
    private const string FillModeHoldStart = "holdStart";
    private const string FillModeHoldEnd = "holdEnd";
    private const string FillModeHoldBoth = "holdBoth";

    public string TargetFramework => "Remotion";

    public ICapabilityManifest Capabilities => RemotionCapabilities.Instance;

    public GenerationResult Generate(HudDocument document, GenerationOptions options)
    {
        var reactResult = new ReactGenerator().Generate(document, options);
        var diagnostics = new List<Diagnostic>(reactResult.Diagnostics);
        var files = new List<GeneratedFile>(reactResult.Files);

        if (options.Motion == null)
        {
            return new GenerationResult
            {
                Files = files,
                Diagnostics = diagnostics
            };
        }

        try
        {
            var motionContent = GenerateMotionComposition(document, options.Motion, diagnostics);
            files.Add(new GeneratedFile
            {
                Path = $"{document.Name}MotionComposition.tsx",
                Content = motionContent,
                Type = GeneratedFileType.SourceCode
            });
        }
        catch (Exception ex)
        {
            diagnostics.Add(Diagnostic.Error($"Remotion motion export failed: {ex.Message}", code: "BHR3000"));
        }

        return new GenerationResult
        {
            Files = files,
            Diagnostics = diagnostics
        };
    }

    private static string GenerateMotionComposition(
        HudDocument document,
        MotionDocument motion,
        List<Diagnostic> diagnostics)
    {
        var sequences = ResolveSequences(document.Name, motion, diagnostics);
        var defaultSequence = ResolveDefaultSequence(document.Name, sequences, motion.DefaultSequenceId, diagnostics);

        var builder = new StringBuilder();

        builder.AppendLine("import React from 'react';");
        builder.AppendLine("import { AbsoluteFill, Sequence } from 'remotion';");
        builder.AppendLine("import { MotionScene, parseMotionDocument, type MotionSequence } from './motion';");
        builder.AppendLine();
        builder.AppendLine($"import {{ {document.Name}View, type {document.Name}ViewModel }} from './{document.Name}View';");
        builder.AppendLine();
        builder.Append("const motionDocument = parseMotionDocument(`");
        builder.AppendLine();
        builder.AppendLine(EscapeTemplateLiteral(motion.ToJson()));
        builder.AppendLine("`);");
        builder.AppendLine();

        foreach (var sequence in sequences)
        {
            builder.Append("const ").Append(sequence.SequenceVariable).Append(": ").Append(SequenceTypeName).AppendLine(" = [");

            foreach (var item in sequence.ResolvedItems)
            {
                builder.AppendLine("  {");
                builder.Append("    clipId: ").Append(ToStringLiteral(item.ClipId)).AppendLine(",");

                if (item.StartFrame.HasValue)
                {
                    builder.Append("    startFrame: ").Append(item.StartFrame.Value.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                }

                if (item.DurationFrames.HasValue)
                {
                    builder.Append("    durationFrames: ").Append(item.DurationFrames.Value.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                }

                if (!string.IsNullOrEmpty(item.FillMode))
                {
                    builder.Append("    fillMode: ").Append(ToStringLiteral(item.FillMode)).AppendLine(",");
                }

                builder.AppendLine("  },");
            }

            builder.AppendLine("];\n");
            builder.Append("const ").Append(sequence.DurationVariable).Append(" = ").Append(sequence.DurationInFrames.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
            builder.AppendLine();
        }

        builder.AppendLine($"export const {document.Name}MotionSequences = [");
        foreach (var sequence in sequences)
        {
            builder.AppendLine("  {");
            builder.Append("    id: ").Append(ToStringLiteral(sequence.Id)).AppendLine(",");
            builder.Append("    name: ").Append(ToStringLiteral(sequence.Name)).AppendLine(",");
            builder.Append("    sequence: ").Append(sequence.SequenceVariable).AppendLine(",");
            builder.Append("    durationInFrames: ").Append(sequence.DurationVariable).AppendLine(",");
            builder.AppendLine("  },");
        }

        builder.AppendLine("];\n");
        builder.Append("export const ").Append(ToSafeIdentifier(document.Name + "DefaultMotionSequenceId")).Append(" = ").Append(ToStringLiteral(defaultSequence.Id)).AppendLine(";");
        builder.Append("export const ").Append(ToSafeIdentifier(document.Name + "DefaultMotionSequence")).Append(" = ").Append(defaultSequence.SequenceVariable).AppendLine(";");
        builder.Append("export const ").Append(ToSafeIdentifier(document.Name + "DefaultMotionDurationInFrames")).Append(" = ").Append(defaultSequence.DurationVariable).AppendLine(";");
        builder.Append("export const ").Append(document.Name).AppendLine("MotionFramesPerSecond = motionDocument.framesPerSecond;");
        builder.Append("export const ").Append(document.Name).AppendLine("FramesPerSecond = motionDocument.framesPerSecond;");
        builder.Append("export const ").Append(document.Name).AppendLine("MotionSequence = ").Append(ToSafeIdentifier(document.Name + "DefaultMotionSequence")).AppendLine(";");
        builder.Append("export const ").Append(document.Name).AppendLine("MotionDurationInFrames = ").Append(ToSafeIdentifier(document.Name + "DefaultMotionDurationInFrames")).AppendLine(";");
        builder.AppendLine();
        builder.AppendLine();

        builder.Append("export const ").Append(document.Name).AppendLine("MotionComposition = (viewModel: ")
            .Append(document.Name).AppendLine("ViewModel): React.JSX.Element =>");
        builder.AppendLine("{");
        builder.AppendLine("  return (");
        builder.AppendLine("    <AbsoluteFill>");
        builder.AppendLine("      <Sequence");
        builder.AppendLine("        from={0}");
        builder.Append("        durationInFrames={");
        builder.Append(ToSafeIdentifier(document.Name + "DefaultMotionDurationInFrames"));
        builder.AppendLine("}>");
        builder.AppendLine("        <MotionScene");
        builder.AppendLine("          document={motionDocument}");
        builder.AppendLine($"          sequence={{ {ToSafeIdentifier(document.Name + "DefaultMotionSequence")} }}");
        builder.AppendLine($"          component={{ {document.Name}View }}");
        builder.AppendLine("          viewModel={viewModel}");
        builder.AppendLine("        />");
        builder.AppendLine("      </Sequence>");
        builder.AppendLine("    </AbsoluteFill>");
        builder.AppendLine("  );");
        builder.AppendLine("};");

        return builder.ToString();
    }

    private static List<RemotionSequence> ResolveSequences(
        string documentName,
        MotionDocument motion,
        List<Diagnostic> diagnostics)
    {
        var clipLookup = motion.Clips.ToDictionary(static clip => clip.Id, static clip => clip);
        if (motion.Sequences.Count == 0)
        {
            diagnostics.Add(Diagnostic.Warning(
                $"Motion document '{motion.Name}' does not define sequences. Using implicit sequence from all clips.",
                code: "BHR4104"));

            var implicitItems = motion.Clips
                .Select(clip => new RemotionSequenceItem
                {
                    ClipId = clip.Id,
                    StartFrame = Math.Max(0, clip.StartFrame),
                    DurationFrames = Math.Max(0, clip.DurationFrames),
                    FillMode = FillModeNone
                })
                .ToList();

            var implicitDuration = motion.Clips.Aggregate(
                0,
                (duration, clip) => Math.Max(duration, Math.Max(0, clip.StartFrame) + Math.Max(0, clip.DurationFrames)));

            return [
                new RemotionSequence
                {
                    Id = "default",
                    Name = "Default",
                    SequenceVariable = ToSafeIdentifier(documentName + "MotionSequence_default"),
                    DurationVariable = ToSafeIdentifier(documentName + "MotionSequenceDurationInFrames_default"),
                    ResolvedItems = implicitItems,
                    DurationInFrames = implicitDuration
                }
            ];
        }

        var resolved = new List<RemotionSequence>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < motion.Sequences.Count; index++)
        {
            var sequence = motion.Sequences[index];
            var items = new List<RemotionSequenceItem>();
            var duration = 0;

            foreach (var sequenceItem in sequence.Items)
            {
                if (!clipLookup.TryGetValue(sequenceItem.ClipId, out var clip))
                {
                    diagnostics.Add(Diagnostic.Warning(
                        $"Motion sequence '{sequence.Id}' references missing clip '{sequenceItem.ClipId}'.",
                        code: "BHR4101"));
                    continue;
                }

                var resolvedStart = sequenceItem.StartFrame ?? clip.StartFrame;
                if (resolvedStart < 0)
                {
                    diagnostics.Add(Diagnostic.Warning(
                        $"Motion sequence '{sequence.Id}' item '{sequenceItem.ClipId}' has negative start frame; clamped to 0.",
                        code: "BHR4102"));
                    resolvedStart = 0;
                }

                var resolvedDuration = sequenceItem.DurationFrames ?? clip.DurationFrames;
                resolvedDuration = Math.Max(0, resolvedDuration);

                if (sequenceItem.DurationFrames.HasValue && resolvedDuration > clip.DurationFrames)
                {
                    diagnostics.Add(Diagnostic.Warning(
                        $"Motion sequence '{sequence.Id}' item '{sequenceItem.ClipId}' duration was clamped from {sequenceItem.DurationFrames} to {clip.DurationFrames}.",
                        code: "BHR4103"));
                    resolvedDuration = clip.DurationFrames;
                }

                var itemDurationForPlaylist = sequenceItem.DurationFrames.HasValue
                    ? resolvedDuration
                    : Math.Max(0, clip.DurationFrames);

                duration = Math.Max(duration, resolvedStart + itemDurationForPlaylist);

                items.Add(new RemotionSequenceItem
                {
                    ClipId = sequenceItem.ClipId,
                    StartFrame = sequenceItem.StartFrame.HasValue ? resolvedStart : null,
                    DurationFrames = sequenceItem.DurationFrames.HasValue ? resolvedDuration : null,
                    FillMode = ToFillMode(sequenceItem.FillMode)
                });
            }

            if (items.Count == 0)
            {
                diagnostics.Add(Diagnostic.Warning(
                    $"Motion sequence '{sequence.Id}' has no valid clips and will be empty.",
                    code: "BHR4103"));
            }

            var sequenceVariable = EnsureUniqueIdentifier(
                usedNames,
                ToSafeIdentifier($"{documentName}MotionSequence_{sequence.Id}_{index}"));
            var durationVariable = EnsureUniqueIdentifier(
                usedNames,
                ToSafeIdentifier($"{documentName}MotionSequenceDurationInFrames_{sequence.Id}_{index}"));

            resolved.Add(new RemotionSequence
            {
                Id = sequence.Id,
                Name = sequence.Name,
                SequenceVariable = sequenceVariable,
                DurationVariable = durationVariable,
                ResolvedItems = items,
                DurationInFrames = duration
            });
        }

        if (resolved.Count == 0)
        {
            diagnostics.Add(Diagnostic.Warning("No valid motion sequences were resolved; created empty fallback.", code: "BHR4105"));
            return [
                new RemotionSequence
                {
                    Id = "empty",
                    Name = "Empty",
                    SequenceVariable = ToSafeIdentifier(documentName + "MotionSequence_empty"),
                    DurationVariable = ToSafeIdentifier(documentName + "MotionSequenceDurationInFrames_empty"),
                    ResolvedItems = [],
                    DurationInFrames = 0
                }
            ];
        }

        return resolved;
    }

    private static RemotionSequence ResolveDefaultSequence(
        string documentName,
        List<RemotionSequence> sequences,
        string? defaultSequenceId,
        List<Diagnostic> diagnostics)
    {
        var resolved = !string.IsNullOrWhiteSpace(defaultSequenceId)
            ? sequences.FirstOrDefault(sequence => string.Equals(sequence.Id, defaultSequenceId, StringComparison.Ordinal))
            : sequences[0];

        if (resolved is not null)
        {
            return resolved;
        }

        diagnostics.Add(Diagnostic.Warning(
            $"Motion document '{documentName}' default sequence '{defaultSequenceId}' was not found. Using '{sequences[0].Id}'.",
            code: "BHR4100"));
        return sequences[0];
    }

    private static string ToSafeIdentifier(string value)
    {
        var parts = value
            .Split([' ', '-', '_', '.', '/', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => new string(part.Where(static ch => char.IsLetterOrDigit(ch)).ToArray()))
            .Where(static part => part.Length > 0)
            .ToList();

        if (parts.Count == 0)
        {
            return "Value";
        }

        var identifier = string.Concat(parts.Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
        return char.IsDigit(identifier[0])
            ? "Seq" + identifier
            : identifier;
    }

    private static string EnsureUniqueIdentifier(HashSet<string> names, string identifier)
    {
        if (names.Add(identifier))
        {
            return identifier;
        }

        var suffix = 2;
        while (true)
        {
            var withSuffix = $"{identifier}{suffix}";
            if (names.Add(withSuffix))
            {
                return withSuffix;
            }

            suffix++;
        }
    }

    private static string ToFillMode(MotionSequenceFillMode fillMode)
        => fillMode switch
        {
            MotionSequenceFillMode.HoldStart => FillModeHoldStart,
            MotionSequenceFillMode.HoldEnd => FillModeHoldEnd,
            MotionSequenceFillMode.HoldBoth => FillModeHoldBoth,
            _ => FillModeNone
        };

    private static string EscapeTemplateLiteral(string value)
        => value
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("${", "\\${", StringComparison.Ordinal);

    private static string ToStringLiteral(string value)
        => '"' + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + '"';

    private sealed record RemotionSequence
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string SequenceVariable { get; init; }

        public required string DurationVariable { get; init; }

        public required IReadOnlyList<RemotionSequenceItem> ResolvedItems { get; init; }

        public int DurationInFrames { get; init; }
    }

    private sealed record RemotionSequenceItem
    {
        public required string ClipId { get; init; }

        public int? StartFrame { get; init; }

        public int? DurationFrames { get; init; }

        public required string FillMode { get; init; }
    }
}
