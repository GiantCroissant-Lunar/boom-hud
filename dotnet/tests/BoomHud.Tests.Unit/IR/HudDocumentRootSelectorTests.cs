using BoomHud.Abstractions.IR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.IR;

public sealed class HudDocumentRootSelectorTests
{
    [Fact]
    public void SelectRoot_WhenRootMatchesDocument_ReturnsOriginalDocument()
    {
        var document = new HudDocument
        {
            Name = "ExploreHud",
            Root = new ComponentNode { Id = "root", Type = ComponentType.Container }
        };

        var selected = HudDocumentRootSelector.SelectRoot(document, "ExploreHud");

        selected.Should().BeSameAs(document);
    }

    [Fact]
    public void SelectRoot_WhenSelectingReusableComponent_PromotesItAndRemovesDuplicateDefinition()
    {
        var charPortrait = new HudComponentDefinition
        {
            Id = "charPortrait",
            Name = "CharPortrait",
            Root = new ComponentNode { Id = "j8BT0", Type = ComponentType.Container }
        };

        var minimap = new HudComponentDefinition
        {
            Id = "minimap",
            Name = "Minimap",
            Root = new ComponentNode { Id = "hrbtA", Type = ComponentType.Container }
        };

        var document = new HudDocument
        {
            Name = "ExploreHud",
            Root = new ComponentNode { Id = "PYS9B", Type = ComponentType.Container },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                [charPortrait.Id] = charPortrait,
                [minimap.Id] = minimap
            }
        };

        var selected = HudDocumentRootSelector.SelectRoot(document, "CharPortrait");

        selected.Name.Should().Be("CharPortrait");
        selected.Root.Id.Should().Be("j8BT0");
        selected.Components.Should().NotContainKey("charPortrait");
        selected.Components.Should().ContainKey("minimap");
    }

    [Fact]
    public void SelectRoot_WhenRequestedComponentDoesNotExist_ThrowsWithAvailableRoots()
    {
        var document = new HudDocument
        {
            Name = "ExploreHud",
            Root = new ComponentNode { Id = "root", Type = ComponentType.Container },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["charPortrait"] = new()
                {
                    Id = "charPortrait",
                    Name = "CharPortrait",
                    Root = new ComponentNode { Id = "j8BT0", Type = ComponentType.Container }
                }
            }
        };

        var action = () => HudDocumentRootSelector.SelectRoot(document, "StatusIcon");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Available: ExploreHud, CharPortrait");
    }
}