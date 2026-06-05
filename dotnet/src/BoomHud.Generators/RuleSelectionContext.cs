using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

public readonly record struct RuleSelectionContext(
    ComponentNode? Parent,
    ComponentNode? Grandparent,
    int SiblingIndex)
{
    public static RuleSelectionContext Root => new(null, null, 0);

    public RuleSelectionContext ForChild(ComponentNode parent, int siblingIndex)
        => new(parent, Parent, siblingIndex);
}
