using System.Text.Json;

namespace BoomHud.Abstractions.Generation;

public sealed record GeneratorRulePlan
{
    private static readonly JsonSerializerOptions WriteOptions = new(GeneratorRuleSet.JsonOptions)
    {
        WriteIndented = true
    };

    public string Version { get; init; } = "1.0";

    public string? Name { get; init; }

    public IReadOnlyList<GeneratorRuleFact> InitialFacts { get; init; } = [];

    public IReadOnlyList<GeneratorRuleFact> FinalFacts { get; init; } = [];

    public IReadOnlyList<GeneratorPlannedRule> AppliedRules { get; init; } = [];

    public IReadOnlyList<GeneratorSkippedRule> SkippedRules { get; init; } = [];

    public double TotalCost { get; init; }

    public string ToJson()
        => JsonSerializer.Serialize(this, WriteOptions);
}

public sealed record GeneratorPlannedRule
{
    public int OriginalIndex { get; init; }

    public string? Name { get; init; }

    public GeneratorRulePhase? Phase { get; init; }

    public double Cost { get; init; }

    public int Specificity { get; init; }

    public GeneratorRuleSelector Selector { get; init; } = new();

    public GeneratorRuleAction Action { get; init; } = new();

    public IReadOnlyList<GeneratorRuleFact> Preconditions { get; init; } = [];

    public IReadOnlyList<GeneratorRuleFact> Effects { get; init; } = [];
}

public sealed record GeneratorSkippedRule
{
    public int OriginalIndex { get; init; }

    public string? Name { get; init; }

    public GeneratorRulePhase? Phase { get; init; }

    public IReadOnlyList<GeneratorRuleFact> MissingPreconditions { get; init; } = [];
}
