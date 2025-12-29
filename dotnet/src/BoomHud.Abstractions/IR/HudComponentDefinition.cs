namespace BoomHud.Abstractions.IR;

public sealed record HudComponentDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public ComponentMetadata? Metadata { get; init; }

    public required ComponentNode Root { get; init; }
}
