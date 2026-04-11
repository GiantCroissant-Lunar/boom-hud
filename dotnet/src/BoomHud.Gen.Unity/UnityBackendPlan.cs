using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Generation;

namespace BoomHud.Gen.Unity;

internal sealed record UnityBackendPlan
{
    public required string Namespace { get; init; }

    public required string ViewModelNamespace { get; init; }

    public required UnityPlannedNode Root { get; init; }

    public required IReadOnlyList<UnityViewModelProperty> ViewModelProperties { get; init; }
}

internal sealed record UnityPlannedNode
{
    public required ComponentNode Source { get; init; }

    public required string Name { get; init; }

    public required string ElementType { get; init; }

    public required string UxmlTag { get; init; }

    public required string CssClass { get; init; }

    public required bool IsFallback { get; init; }

    public required ResolvedGeneratorPolicy Policy { get; init; }

    public required IReadOnlyList<UnityPlannedNode> Children { get; init; }
}

internal sealed record UnityViewModelProperty
{
    public required string Identifier { get; init; }

    public required string Path { get; init; }
}
