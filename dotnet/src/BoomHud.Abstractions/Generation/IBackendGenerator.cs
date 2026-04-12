using System.Collections.Generic;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;

namespace BoomHud.Abstractions.Generation;

/// <summary>
/// Interface for backend code generators.
/// </summary>
public interface IBackendGenerator
{
    /// <summary>
    /// Name of the target framework.
    /// </summary>
    string TargetFramework { get; }

    /// <summary>
    /// Capability manifest for this backend.
    /// </summary>
    Capabilities.ICapabilityManifest Capabilities { get; }

    /// <summary>
    /// Generates code for the given HUD document.
    /// </summary>
    /// <param name="document">The HUD document to generate code for.</param>
    /// <param name="options">Generation options.</param>
    /// <returns>The generation result containing files and diagnostics.</returns>
    GenerationResult Generate(HudDocument document, GenerationOptions options);
}

/// <summary>
/// Options for code generation.
/// </summary>
public sealed record GenerationOptions
{
    /// <summary>
    /// Root namespace for generated code.
    /// </summary>
    public string Namespace { get; init; } = "Generated";
    public string? ViewModelNamespace { get; init; }
    public bool EmitViewModelInterfaces { get; init; } = true;
    public bool EmitCompose { get; init; }

    /// <summary>
    /// Emit Godot scene files (.tscn) when supported by the backend.
    /// </summary>
    public bool EmitTscn { get; init; }
    public bool EmitTscnAttachScript { get; init; } = true;
    public string? ContractId { get; init; }

    /// <summary>
    /// Output directory for generated files.
    /// </summary>
    public string OutputDirectory { get; init; } = ".";

    /// <summary>
    /// Policy for handling missing capabilities.
    /// </summary>
    public MissingCapabilityPolicy MissingCapabilityPolicy { get; init; } = MissingCapabilityPolicy.Warn;

    /// <summary>
    /// Whether to generate comments in output.
    /// </summary>
    public bool IncludeComments { get; init; } = true;

    /// <summary>
    /// Whether to generate nullable annotations.
    /// </summary>
    public bool UseNullableAnnotations { get; init; } = true;

    /// <summary>
    /// Optional design theme (e.g., from Figma variables or design tokens).
    /// Backends may use this to emit shared resources or token-based styling.
    /// </summary>
    public ThemeDocument? Theme { get; init; }

    /// <summary>
    /// Optional motion document for backends that can emit animation artifacts.
    /// </summary>
    public MotionDocument? Motion { get; init; }

    public GeneratorRuleSet? RuleSet { get; init; }

    /// <summary>
    /// Emit a compiler-only Visual IR artifact for diagnostics and future planning work.
    /// </summary>
    public bool EmitVisualIrArtifact { get; init; }

    /// <summary>
    /// Emit a compiler-only Visual synthesis artifact derived from the Visual IR planner.
    /// </summary>
    public bool EmitVisualSynthesisArtifact { get; init; }

    /// <summary>
    /// Emit a compiler-only Visual refinement artifact derived from recursive fidelity planning.
    /// </summary>
    public bool EmitVisualRefinementArtifact { get; init; }

    /// <summary>
    /// Maximum number of refinement actions to plan in one bounded Visual refinement pass.
    /// </summary>
    public int VisualRefinementIterationBudget { get; init; } = 4;

    public IReadOnlyDictionary<string, string> DescriptionReplacements { get; init; }
        = new Dictionary<string, string>();
}

/// <summary>
/// Policy for handling missing capabilities.
/// </summary>
public enum MissingCapabilityPolicy
{
    /// <summary>
    /// Fail generation with an error.
    /// </summary>
    Error,

    /// <summary>
    /// Emit a warning and continue with fallback.
    /// </summary>
    Warn,

    /// <summary>
    /// Silently use fallback behavior.
    /// </summary>
    Silent,

    /// <summary>
    /// Skip the unsupported feature entirely.
    /// </summary>
    Skip
}
