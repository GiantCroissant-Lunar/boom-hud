using BoomHud.Abstractions.IR;
using BoomHud.Generators;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public class GeneratorSourceIdTests
{
    [Fact]
    public void ComputeSourceId_ForKnownDocument_IsStable()
    {
        // Pins the exact drift-detection hash. Consumers persist this source id, so the
        // value MUST NOT change without a deliberate migration. If this fails after a
        // refactor of GeneratorSourceId, the hash computation drifted.
        var doc = new HudDocument
        {
            Name = "X",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        GeneratorSourceId.ComputeSourceId(doc)
            .Should().Be("sha256:a7ec50d1f48241e5988f4600db8e2c71935c0e85b75d95531db845fba4202b73");
    }

    [Fact]
    public void ComputeSourceId_ChangesWithDocumentStructure()
    {
        var a = new HudDocument { Name = "Root", Root = new ComponentNode { Type = ComponentType.Container } };
        var b = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children = [new ComponentNode { Id = "a", Type = ComponentType.Label }]
            }
        };

        GeneratorSourceId.ComputeSourceId(a).Should().NotBe(GeneratorSourceId.ComputeSourceId(b));
    }

    [Fact]
    public void CollectNormalizedPseudoNodes_StampedNode_ProducesPathEntry()
    {
        var doc = new HudDocument
        {
            Name = "Timeline",
            Root = new ComponentNode
            {
                Id = "timeline",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "playButton",
                        Type = ComponentType.Button,
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.OriginalFigmaType] = "BUTTON",
                            [BoomHudMetadataKeys.NormalizedFromPseudoType] = true
                        }
                    }
                ]
            }
        };

        GeneratorSourceId.CollectNormalizedPseudoNodes(doc)
            .Should().ContainSingle().Which.Should().Be("timeline/playButton|BUTTON|Button");
    }
}
