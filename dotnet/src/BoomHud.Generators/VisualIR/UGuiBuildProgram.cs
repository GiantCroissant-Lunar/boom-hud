namespace BoomHud.Generators.VisualIR;

using BoomHud.Abstractions.Generation;

public sealed record UGuiBuildProgram
{
    public required string DocumentName { get; init; }

    public required string BackendFamily { get; init; }

    public required string SourceGenerationMode { get; init; }

    public required string RootStableId { get; init; }

    public IReadOnlyList<UGuiBuildStep> Steps { get; init; } = [];

    public IReadOnlyList<UGuiBuildCheckpoint> Checkpoints { get; init; } = [];

    public IReadOnlyList<UGuiBuildCandidateCatalog> CandidateCatalogs { get; init; } = [];

    public IReadOnlyList<UGuiBuildSelection> AcceptedCandidates { get; init; } = [];
}

public sealed record UGuiBuildStep
{
    public required int Order { get; init; }

    public required string StableId { get; init; }

    public string? ParentStableId { get; init; }

    public required string SolveStage { get; init; }

    public required string ActionType { get; init; }

    public IReadOnlyDictionary<string, object?> Parameters { get; init; }
        = new Dictionary<string, object?>(StringComparer.Ordinal);
}

public sealed record UGuiBuildCheckpoint
{
    public required int Order { get; init; }

    public required string StableId { get; init; }

    public required string SolveStage { get; init; }

    public required int LastStepOrder { get; init; }

    public required string Purpose { get; init; }
}

public sealed record UGuiBuildCandidateCatalog
{
    public required string StableId { get; init; }

    public string? SolveStage { get; init; }

    public IReadOnlyList<UGuiBuildCandidate> Candidates { get; init; } = [];
}

public sealed record UGuiBuildCandidate
{
    public required string CandidateId { get; init; }

    public string? Label { get; init; }

    public GeneratorRuleAction Action { get; init; } = new();

    public IReadOnlyList<UGuiBuildDescendantAction> DescendantActions { get; init; } = [];
}

public sealed record UGuiBuildDescendantAction
{
    public required string StableId { get; init; }

    public GeneratorRuleAction Action { get; init; } = new();
}

public sealed record UGuiBuildSelection
{
    public required string StableId { get; init; }

    public required string CandidateId { get; init; }
}
