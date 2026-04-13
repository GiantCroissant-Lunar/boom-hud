using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class SyntheticComponentizationTests
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = true
    };

    [Fact]
    public void Prepare_ExactRepeatedStaticSubtrees_LiftsSyntheticComponent()
    {
        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateStaticCard("card-alpha", "title-alpha", "icon-alpha", 12, 32),
                    CreateStaticCard("card-bravo", "title-bravo", "icon-bravo", 312, 64)
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());

        prepared.SyntheticComponentization.Should().NotBeNull();
        prepared.SyntheticComponentization!.ChosenGroupCount.Should().Be(1);
        prepared.SyntheticComponentization.RewrittenOccurrenceCount.Should().Be(2);
        prepared.Document.Components.Should().ContainSingle();

        var syntheticId = prepared.Document.Components.Single().Key;
        prepared.Document.Root.Children.Should().HaveCount(2);
        prepared.Document.Root.Children.Should().OnlyContain(child => child.ComponentRefId == syntheticId && child.Children.Count == 0);

        var syntheticRoot = prepared.Document.Components.Single().Value.Root;
        syntheticRoot.Children.Should().HaveCount(2);
        syntheticRoot.Children[0].Properties["Text"].Value.Should().Be("QUEST");
        syntheticRoot.Children[1].Properties["Text"].Value.Should().Be("shield");
    }

    [Fact]
    public void Prepare_DifferentTextLiteral_LiftsSyntheticComponentWithOverrides()
    {
        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateStaticCard("card-alpha", "title-alpha", "icon-alpha", 0, 0),
                    CreateStaticCard("card-bravo", "title-bravo", "icon-bravo", 40, 0, titleText: "BONUS")
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());

        prepared.SyntheticComponentization.Should().NotBeNull();
        prepared.Document.Components.Should().ContainSingle();
        prepared.Document.Root.Children[0].ComponentRefId.Should().NotBeNull();
        prepared.Document.Root.Children[1].ComponentRefId.Should().Be(prepared.Document.Root.Children[0].ComponentRefId);

        var overrides = ComponentInstanceOverrideSupport.GetPropertyOverrides(prepared.Document.Root.Children[1]);
        overrides.Should().ContainKey("$/0");
        overrides["$/0"].Should().ContainKey("Text");
        overrides["$/0"]["Text"].Should().Be("BONUS", "the second card title should become a path-based override");
    }

    [Fact]
    public void Prepare_DifferentIconGlyph_LiftsSyntheticComponentWithOverrides()
    {
        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateStaticCard("card-alpha", "title-alpha", "icon-alpha", 12, 32),
                    CreateStaticCard("card-bravo", "title-bravo", "icon-bravo", 312, 64, iconText: "sparkles")
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());

        prepared.SyntheticComponentization.Should().NotBeNull();
        prepared.Document.Components.Should().ContainSingle();

        var overrides = ComponentInstanceOverrideSupport.GetPropertyOverrides(prepared.Document.Root.Children[1]);
        overrides.Should().ContainKey("$/1");
        overrides["$/1"]["Text"].Should().Be("sparkles");
    }

    [Fact]
    public void Prepare_DifferentLayoutValue_DoesNotLiftSyntheticComponent()
    {
        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateStaticCard("card-alpha", "title-alpha", "icon-alpha", 12, 32),
                    CreateStaticCard("card-bravo", "title-bravo", "icon-bravo", 312, 64, gap: 10)
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());

        prepared.SyntheticComponentization.Should().BeNull();
        prepared.Document.Components.Should().BeEmpty();
    }

    [Fact]
    public void Prepare_DifferentBindingPath_DoesNotLiftSyntheticComponent()
    {
        var document = new HudDocument
        {
            Name = "StatusHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateBoundCard("card-alpha", "Vm.PlayerOne"),
                    CreateBoundCard("card-bravo", "Vm.PlayerTwo")
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());

        prepared.SyntheticComponentization.Should().BeNull();
        prepared.Document.Components.Should().BeEmpty();
    }

    [Fact]
    public void Prepare_ExistingExplicitComponentInstances_ArePreserved()
    {
        var badge = new HudComponentDefinition
        {
            Id = "badge",
            Name = "Badge",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "label",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "READY"
                        }
                    }
                ]
            }
        };

        var document = new HudDocument
        {
            Name = "StatusHud",
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["badge"] = badge
            },
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode { Id = "badge-one", Type = ComponentType.Container, ComponentRefId = "badge" },
                    new ComponentNode { Id = "badge-two", Type = ComponentType.Container, ComponentRefId = "badge" }
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());

        prepared.SyntheticComponentization.Should().BeNull();
        prepared.Document.Components.Should().ContainSingle().Which.Key.Should().Be("badge");
        prepared.Document.Root.Children.Should().OnlyContain(child => child.ComponentRefId == "badge");
    }

    [Fact]
    public void Prepare_OverlappingMatches_SelectsHighestRankedNonOverlappingGroup()
    {
        var rowOne = CreateStaticRow("row-a1", "label-a1", "icon-a1");
        var rowTwo = CreateStaticRow("row-a2", "label-a2", "icon-a2");
        var rowThree = CreateStaticRow("row-b1", "label-b1", "icon-b1");
        var rowFour = CreateStaticRow("row-b2", "label-b2", "icon-b2");

        var document = new HudDocument
        {
            Name = "PartyHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "panel-alpha",
                        Type = ComponentType.Container,
                        Children = [rowOne, rowTwo]
                    },
                    new ComponentNode
                    {
                        Id = "panel-bravo",
                        Type = ComponentType.Container,
                        Children = [rowThree, rowFour]
                    }
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());

        prepared.SyntheticComponentization.Should().NotBeNull();
        prepared.SyntheticComponentization!.ChosenGroupCount.Should().Be(1);
        prepared.SyntheticComponentization.RewrittenOccurrenceCount.Should().Be(4);
        prepared.Document.Root.Children.Should().OnlyContain(child => child.ComponentRefId == null);
        prepared.Document.Root.Children.SelectMany(child => child.Children).Should().OnlyContain(child => child.ComponentRefId != null);
    }

    [Fact]
    public void Prepare_EquivalentPropertyOrdering_ProducesTheSameSyntheticMatch()
    {
        var first = CreatePropertyOrderedCard("card-alpha", propertyOrder: ["Text", "Role"]);
        var second = CreatePropertyOrderedCard("card-bravo", propertyOrder: ["Role", "Text"]);

        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children = [first, second]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());

        prepared.SyntheticComponentization.Should().NotBeNull();
        prepared.SyntheticComponentization!.ChosenGroupCount.Should().Be(1);
        prepared.Document.Components.Should().ContainSingle();
    }

    [Fact]
    public void Prepare_RepeatedRuns_AreDeterministic()
    {
        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateStaticCard("card-alpha", "title-alpha", "icon-alpha", 12, 32),
                    CreateStaticCard("card-bravo", "title-bravo", "icon-bravo", 312, 64)
                ]
            }
        };

        var first = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());
        var second = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());

        GenerationDocumentPreprocessor.ToJson(first.SyntheticComponentization!).Should().Be(
            GenerationDocumentPreprocessor.ToJson(second.SyntheticComponentization!));
    }

    [Fact]
    public void Prepare_ExactRepeatedStaticSubtrees_ProducesDeterministicIrSnapshot()
    {
        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateStaticCard("card-alpha", "title-alpha", "icon-alpha", 12, 32),
                    CreateStaticCard("card-bravo", "title-bravo", "icon-bravo", 312, 64)
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions());
        var snapshot = CreateSnapshot(prepared.Document, prepared.SyntheticComponentization);

        snapshot.ReplaceLineEndings("\n").Should().Be(
            """
            {
              "documentName": "QuestHud",
              "summary": {
                "candidateGroups": 1,
                "chosenGroups": 1,
                "rewrittenOccurrences": 2,
                "components": [
                  {
                    "id": "component-1",
                    "name": "component-1",
                    "rootType": "Container",
                    "occurrences": 2,
                    "nodeCount": 3,
                    "depth": 2
                  }
                ]
              },
              "components": [
                {
                  "id": "component-1",
                  "name": "component-1",
                  "root": {
                    "id": "card-alpha",
                    "type": "Container",
                    "componentRefId": null,
                    "propertyKeys": [],
                    "instanceOverrideKeys": [
                      "boomhud:originalPencilId",
                      "boomhud:pencilLeft",
                      "boomhud:pencilTop"
                    ],
                    "children": [
                      {
                        "id": "title-alpha",
                        "type": "Label",
                        "componentRefId": null,
                        "propertyKeys": [
                          "Text"
                        ],
                        "instanceOverrideKeys": [
                          "boomhud:originalPencilId",
                          "boomhud:pencilLeft",
                          "boomhud:pencilTop"
                        ],
                        "children": []
                      },
                      {
                        "id": "icon-alpha",
                        "type": "Icon",
                        "componentRefId": null,
                        "propertyKeys": [
                          "Text"
                        ],
                        "instanceOverrideKeys": [
                          "boomhud:originalPencilId",
                          "boomhud:pencilLeft",
                          "boomhud:pencilTop"
                        ],
                        "children": []
                      }
                    ]
                  }
                }
              ],
              "root": {
                "id": null,
                "type": "Container",
                "componentRefId": null,
                "propertyKeys": [],
                "instanceOverrideKeys": [],
                "children": [
                  {
                    "id": "card-alpha",
                    "type": "Container",
                    "componentRefId": "component-1",
                    "propertyKeys": [],
                    "instanceOverrideKeys": [
                      "boomhud:originalPencilId",
                      "boomhud:pencilLeft",
                      "boomhud:pencilTop",
                      "boomhud:syntheticComponentInstance"
                    ],
                    "children": []
                  },
                  {
                    "id": "card-bravo",
                    "type": "Container",
                    "componentRefId": "component-1",
                    "propertyKeys": [],
                    "instanceOverrideKeys": [
                      "boomhud:originalPencilId",
                      "boomhud:pencilLeft",
                      "boomhud:pencilTop",
                      "boomhud:syntheticComponentInstance"
                    ],
                    "children": []
                  }
                ]
              }
            }
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Prepare_WithVisualIrArtifact_BuildsFromPostSynthesisDocument()
    {
        var document = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateStaticCard("card-alpha", "title-alpha", "icon-alpha", 12, 32),
                    CreateStaticCard("card-bravo", "title-bravo", "icon-bravo", 312, 64)
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(document, new GenerationOptions { EmitVisualIrArtifact = true }, "react");

        prepared.VisualDocument.Should().NotBeNull();
        var synthesizedComponentId = prepared.Document.Components.Single().Key;
        prepared.VisualDocument!.Components.Should().ContainSingle(component => component.Id == synthesizedComponentId);
        prepared.VisualDocument.Root.Children.Should().OnlyContain(child => child.ComponentRefId == synthesizedComponentId);
    }

    private static ComponentNode CreateStaticCard(
        string cardId,
        string titleId,
        string iconId,
        double left,
        double top,
        string titleText = "QUEST",
        string iconText = "shield",
        double gap = 8)
    {
        return new ComponentNode
        {
            Id = cardId,
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Horizontal,
                Gap = new Spacing(gap),
                Padding = new Spacing(6),
                Width = Dimension.Pixels(220),
                Height = Dimension.Pixels(60)
            },
            Style = new StyleSpec
            {
                Background = Color.Parse("#101820"),
                Border = new BorderSpec { Style = BorderStyle.Solid, Color = Color.Parse("#F5E6A8"), Width = 1 }
            },
            InstanceOverrides = new Dictionary<string, object?>
            {
                [BoomHudMetadataKeys.OriginalPencilId] = cardId,
                [BoomHudMetadataKeys.PencilLeft] = left,
                [BoomHudMetadataKeys.PencilTop] = top
            },
            Children =
            [
                new ComponentNode
                {
                    Id = titleId,
                    Type = ComponentType.Label,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = titleText
                    },
                    Style = new StyleSpec
                    {
                        FontFamily = "Press Start 2P",
                        FontSize = 12
                    },
                    InstanceOverrides = new Dictionary<string, object?>
                    {
                        [BoomHudMetadataKeys.OriginalPencilId] = titleId,
                        [BoomHudMetadataKeys.PencilLeft] = left + 12,
                        [BoomHudMetadataKeys.PencilTop] = top + 12
                    }
                },
                new ComponentNode
                {
                    Id = iconId,
                    Type = ComponentType.Icon,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = iconText
                    },
                    Style = new StyleSpec
                    {
                        FontFamily = "Lucide",
                        FontSize = 18
                    },
                    InstanceOverrides = new Dictionary<string, object?>
                    {
                        [BoomHudMetadataKeys.OriginalPencilId] = iconId,
                        [BoomHudMetadataKeys.PencilLeft] = left + 172,
                        [BoomHudMetadataKeys.PencilTop] = top + 12
                    }
                }
            ]
        };
    }

    private static ComponentNode CreatePropertyOrderedCard(string cardId, IReadOnlyList<string> propertyOrder)
    {
        var properties = new Dictionary<string, BindableValue<object?>>();
        foreach (var key in propertyOrder)
        {
            properties[key] = key switch
            {
                "Text" => "QUEST",
                "Role" => "primary",
                _ => throw new InvalidOperationException("Unexpected property key.")
            };
        }

        return new ComponentNode
        {
            Id = cardId,
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Horizontal,
                Gap = new Spacing(8),
                Padding = new Spacing(6)
            },
            Children =
            [
                new ComponentNode
                {
                    Id = cardId + "-label",
                    Type = ComponentType.Label,
                    Properties = properties
                },
                new ComponentNode
                {
                    Id = cardId + "-icon",
                    Type = ComponentType.Icon,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = "shield"
                    }
                }
            ]
        };
    }

    private static ComponentNode CreateBoundCard(string cardId, string bindingPath)
    {
        return new ComponentNode
        {
            Id = cardId,
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Horizontal,
                Gap = new Spacing(8),
                Padding = new Spacing(6)
            },
            Children =
            [
                new ComponentNode
                {
                    Id = cardId + "-label",
                    Type = ComponentType.Label,
                    Bindings =
                    [
                        new BindingSpec
                        {
                            Property = "text",
                            Path = bindingPath
                        }
                    ]
                },
                new ComponentNode
                {
                    Id = cardId + "-icon",
                    Type = ComponentType.Icon,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = "shield"
                    }
                }
            ]
        };
    }

    private static ComponentNode CreateStaticRow(string rowId, string labelId, string iconId)
    {
        return new ComponentNode
        {
            Id = rowId,
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Horizontal,
                Gap = new Spacing(4),
                Padding = new Spacing(2)
            },
            Children =
            [
                new ComponentNode
                {
                    Id = labelId,
                    Type = ComponentType.Label,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = "ALLY"
                    }
                },
                new ComponentNode
                {
                    Id = iconId,
                    Type = ComponentType.Icon,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = "shield"
                    }
                }
            ]
        };
    }

    private static string CreateSnapshot(HudDocument document, SyntheticComponentizationSummary? summary)
    {
        var componentOrder = document.Components.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToList();
        var componentAliasById = componentOrder
            .Select((key, index) => new { key, alias = $"component-{index + 1}" })
            .ToDictionary(static pair => pair.key, static pair => pair.alias, StringComparer.Ordinal);

        var snapshot = new
        {
            documentName = document.Name,
            summary = summary == null
                ? null
                : new
                {
                    candidateGroups = summary.CandidateGroupCount,
                    chosenGroups = summary.ChosenGroupCount,
                    rewrittenOccurrences = summary.RewrittenOccurrenceCount,
                    components = summary.Components.Select(component => new
                    {
                        id = componentAliasById[component.ComponentId],
                        name = componentAliasById[component.ComponentId],
                        rootType = component.RootType,
                        occurrences = component.OccurrenceCount,
                        nodeCount = component.NodeCount,
                        depth = component.Depth
                    }).ToList()
                },
            components = document.Components
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new
                {
                    id = componentAliasById[pair.Key],
                    name = componentAliasById[pair.Key],
                    root = ProjectNode(pair.Value.Root, componentAliasById)
                })
                .ToList(),
            root = ProjectNode(document.Root, componentAliasById)
        };

        return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
    }

    private static object ProjectNode(ComponentNode node, IReadOnlyDictionary<string, string> componentAliasById)
    {
        return new
        {
            id = node.Id,
            type = node.Type.ToString(),
            componentRefId = node.ComponentRefId == null ? null : componentAliasById[node.ComponentRefId],
            propertyKeys = node.Properties.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToList(),
            instanceOverrideKeys = node.InstanceOverrides.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToList(),
            children = node.Children.Select(child => ProjectNode(child, componentAliasById)).ToList()
        };
    }
}
