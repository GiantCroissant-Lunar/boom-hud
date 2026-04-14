using System;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Motion;
using BoomHud.Gen.UGui;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public sealed class UGuiGeneratorTests
{
    private readonly UGuiGenerator _generator = new();
    private readonly GenerationOptions _options = new()
    {
        Namespace = "TestNamespace",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void Generate_MinimalDocument_ProducesUGuiArtifacts()
    {
        var doc = new HudDocument
        {
            Name = "TestComponent",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().Contain(f => f.Path == "TestComponentView.ugui.cs");
        result.Files.Should().Contain(f => f.Path == "ITestComponentViewModel.g.cs");

        var viewFile = result.Files.First(f => f.Path == "TestComponentView.ugui.cs");
        viewFile.Content.Should().Contain("using TMPro;");
        viewFile.Content.Should().Contain("using UnityEngine.UI;");
        viewFile.Content.Should().Contain("public sealed class TestComponentView");
        viewFile.Content.Should().Contain("public RectTransform Root { get; }");
        viewFile.Content.Should().Contain("Root = CreateRect(\"TestComponentRoot\", parent);");
    }

    [Fact]
    public void Generate_DefaultMode_DoesNotEmitVisualIrArtifact()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        result.Files.Should().NotContain(file => file.Path == "VisualHud.visual-ir.json");
    }

    [Fact]
    public void Generate_WhenRequested_EmitsVisualIrArtifact()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Id = "title",
                Type = ComponentType.Label,
                Style = new StyleSpec
                {
                    FontFamily = "Press Start 2P",
                    FontSize = 12
                }
            }
        };

        var result = _generator.Generate(doc, _options with { EmitVisualIrArtifact = true });
        var visualIr = result.Files.Single(file => file.Path == "VisualHud.visual-ir.json").Content;

        visualIr.Should().Contain("\"DocumentName\": \"VisualHud\"");
        visualIr.Should().Contain("\"BackendFamily\": \"ugui\"");
    }

    [Fact]
    public void Generate_WhenRequested_EmitsSourceSemanticArtifact()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Id = "title",
                Type = ComponentType.Label,
                Style = new StyleSpec
                {
                    FontFamily = "Press Start 2P",
                    FontSize = 12
                }
            }
        };

        var result = _generator.Generate(doc, _options with { EmitSourceSemanticArtifact = true });
        var sourceSemantics = result.Files.Single(file => file.Path == "VisualHud.source-semantics.json").Content;

        sourceSemantics.Should().Contain("\"DocumentName\": \"VisualHud\"");
        sourceSemantics.Should().Contain("\"SemanticRole\": \"pixel-text\"");
    }

    [Fact]
    public void Generate_WhenRequested_EmitsVisualPlanningArtifacts()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title-a",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "QUEST"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "title-b",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "BONUS"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options with
        {
            EmitVisualSynthesisArtifact = true,
            EmitVisualRefinementArtifact = true
        });

        result.Files.Should().Contain(file => file.Path == "VisualHud.visual-synthesis.json");
        result.Files.Should().Contain(file => file.Path == "VisualHud.visual-refinement.json");
    }

    [Fact]
    public void Generate_WhenRequested_EmitsUGuiBuildProgramArtifact()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Id = "title",
                Type = ComponentType.Label,
                Properties = new Dictionary<string, BindableValue<object?>>
                {
                    ["Text"] = "QUEST"
                }
            }
        };

        var result = _generator.Generate(doc, _options with { EmitUGuiBuildProgramArtifact = true });

        result.Files.Should().Contain(file => file.Path == "VisualHud.ugui-build-program.json");
        result.Files.Single(file => file.Path == "VisualHud.ugui-build-program.json").Content
            .Should().Contain("\"RootStableId\": \"root\"");
    }

    [Fact]
    public void Generate_WithAcceptedUGuiBuildProgramCandidate_AppliesPolicyOverrideByStableId()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "QUEST"
                        }
                    }
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(doc, _options, "ugui");
        var titleStableId = prepared.VisualDocument.Root.Children.Single().StableId;
        var buildProgram = new UGuiBuildProgram
        {
            DocumentName = prepared.VisualDocument.DocumentName,
            BackendFamily = prepared.VisualDocument.BackendFamily,
            SourceGenerationMode = prepared.VisualDocument.SourceGenerationMode,
            RootStableId = prepared.VisualDocument.Root.StableId,
            CandidateCatalogs =
            [
                new UGuiBuildCandidateCatalog
                {
                    StableId = titleStableId,
                    SolveStage = "atom",
                    Candidates =
                    [
                        new UGuiBuildCandidate
                        {
                            CandidateId = "container-control",
                            Label = "Container control override",
                            Action = new GeneratorRuleAction
                            {
                                ControlType = "Container"
                            }
                        }
                    ]
                }
            ],
            AcceptedCandidates =
            [
                new UGuiBuildSelection
                {
                    StableId = titleStableId,
                    CandidateId = "container-control"
                }
            ]
        };

        var buildProgramPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(buildProgramPath, GenerationDocumentPreprocessor.ToJson(buildProgram));

            var result = _generator.Generate(doc, _options with
            {
                UGuiBuildProgramPath = buildProgramPath
            });

            var viewFile = result.Files.Single(file => file.Path == "VisualHudView.ugui.cs");
            viewFile.Content.Should().Contain("public RectTransform Title { get; }");
            viewFile.Content.Should().NotContain("public TextMeshProUGUI Title { get; }");
        }
        finally
        {
            if (File.Exists(buildProgramPath))
            {
                File.Delete(buildProgramPath);
            }
        }
    }

    [Fact]
    public void Generate_WithAcceptedUGuiBuildProgramTextDelta_AppliesDeltaOnTopOfMetricProfileFontSize()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "QUEST"
                        }
                    }
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(doc, _options, "ugui");
        var titleNode = prepared.VisualDocument.Root.Children.Single();
        titleNode.MetricProfileId.Should().NotBeNullOrWhiteSpace();

        var buildProgram = new UGuiBuildProgram
        {
            DocumentName = prepared.VisualDocument.DocumentName,
            BackendFamily = prepared.VisualDocument.BackendFamily,
            SourceGenerationMode = prepared.VisualDocument.SourceGenerationMode,
            RootStableId = prepared.VisualDocument.Root.StableId,
            CandidateCatalogs =
            [
                new UGuiBuildCandidateCatalog
                {
                    StableId = titleNode.StableId,
                    SolveStage = "atom",
                    Candidates =
                    [
                        new UGuiBuildCandidate
                        {
                            CandidateId = "font-bump",
                            Label = "Increase font size",
                            Action = new GeneratorRuleAction
                            {
                                Text = new GeneratorTextRuleAction
                                {
                                    FontSizeDelta = 1
                                }
                            }
                        }
                    ]
                }
            ],
            AcceptedCandidates =
            [
                new UGuiBuildSelection
                {
                    StableId = titleNode.StableId,
                    CandidateId = "font-bump"
                }
            ]
        };

        var buildProgramPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(buildProgramPath, GenerationDocumentPreprocessor.ToJson(buildProgram));

            var result = _generator.Generate(doc, _options with
            {
                UGuiBuildProgramPath = buildProgramPath
            });

            var viewFile = result.Files.Single(file => file.Path == "VisualHudView.ugui.cs");
            viewFile.Content.Should().Contain("ApplyStyle(Title, fg: null, bg: null, fontFamily: \"Press Start 2P\", fontSize: 10,");
        }
        finally
        {
            if (File.Exists(buildProgramPath))
            {
                File.Delete(buildProgramPath);
            }
        }
    }

    [Fact]
    public void Generate_WithSharedMetricProfile_AppliesTopLevelProfileToMatchedLabel()
    {
        var doc = new HudDocument
        {
            Name = "VisualHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        },
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.PencilTextGrowth] = "fixed-width"
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "QUEST"
                        }
                    }
                ]
            }
        };

        var baseline = _generator.Generate(doc, _options);
        var baselineViewFile = baseline.Files.Single(file => file.Path == "VisualHudView.ugui.cs");

        var result = _generator.Generate(doc, _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                MetricProfiles =
                [
                    new GeneratorMetricProfile
                    {
                        Name = "shared-pixel-small-font-bump",
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            ComponentType = ComponentType.Label,
                            SemanticClass = "pixel-text",
                            SizeBand = "small"
                        },
                        Template = new GeneratorActionTemplate
                        {
                            Kind = "fontSizeDelta",
                            NumberValue = 1
                        }
                    }
                ]
            }
        });

        var viewFile = result.Files.Single(file => file.Path == "VisualHudView.ugui.cs");
        baselineViewFile.Content.Should().Contain("ApplyStyle(Title, fg: null, bg: null, fontFamily: \"Press Start 2P\", fontSize: 9,");
        viewFile.Content.Should().Contain("ApplyStyle(Title, fg: null, bg: null, fontFamily: \"Press Start 2P\", fontSize: 10,");
    }

    [Fact]
    public void Generate_RightAlignedQuantity_UsesContentHugWithoutFlexGrowth()
    {
        var doc = new HudDocument
        {
            Name = "CompactRowHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = Spacing.Uniform(8),
                    Width = Dimension.Pixels(220)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "ingredientLabel",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "COPPER"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    },
                    new ComponentNode
                    {
                        Id = "ingredientQty",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            Align = Alignment.End
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "12 pcs"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "CompactRowHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(IngredientQty), horizontal: true, vertical: false);");
        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(IngredientQty), ignoreLayout: false, preferredWidth:");
        viewFile.Content.Should().NotContain("ApplyLayoutSizing(RectOf(IngredientQty), ignoreLayout: false, preferredWidth: null, preferredHeight: null, flexibleWidth: 1f");
    }

    [Fact]
    public void Generate_ValueRowShell_DisablesMainAxisChildControlToPreserveCompactWidths()
    {
        var doc = new HudDocument
        {
            Name = "ValueRowHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = Spacing.Uniform(8),
                    Width = Dimension.Pixels(220)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "ingredientLabel",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "COPPER"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    },
                    new ComponentNode
                    {
                        Id = "ingredientQty",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            Align = Alignment.End
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "12 pcs"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "ValueRowHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyHorizontalLayout(Root, 8f, 0, 0, 0, 0, null, childControlWidth: false, childControlHeight: true);");
    }

    [Fact]
    public void Generate_SourceImageAsset_AppliesAuthoredImageBehavior()
    {
        var doc = new HudDocument
        {
            Name = "ImageAssetHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "heroPortrait",
                        Type = ComponentType.Image,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(96),
                            Height = Dimension.Pixels(64)
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["source"] = "portraits/hero"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "ImageAssetHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyImageAssetBehavior(HeroPortrait, preserveAspect: true);");
        viewFile.Content.Should().Contain("SetImage(HeroPortrait, \"portraits/hero\");");
        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(HeroPortrait), horizontal: false, vertical: false);");
    }

    [Fact]
    public void Generate_OverflowingValueRow_PreservesSourceGapAndPadding()
    {
        var doc = new HudDocument
        {
            Name = "OverflowValueRowHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Width = Dimension.Pixels(80),
                    Gap = Spacing.Uniform(8),
                    Padding = Spacing.Uniform(4)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "ingredientLabel",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(40)
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "COPPER"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    },
                    new ComponentNode
                    {
                        Id = "ingredientQty",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(32),
                            Align = Alignment.End
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "12 pcs"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "OverflowValueRowHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyHorizontalLayout(Root, 8f, 4, 4, 4, 4, null, childControlWidth: false, childControlHeight: true);");
        viewFile.Content.Should().NotContain("ApplyHorizontalLayout(Root, 4f, 2, 2, 4, 4");
    }

    [Fact]
    public void Generate_UnpinnedInlineIcon_PrefersIntrinsicContentHug()
    {
        var doc = new HudDocument
        {
            Name = "InlineIconHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = Spacing.Uniform(8)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "statusIcon",
                        Type = ComponentType.Icon,
                        Style = new StyleSpec
                        {
                            FontFamily = "Lucide",
                            FontSize = 16
                        }
                    },
                    new ComponentNode
                    {
                        Id = "statusLabel",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "READY"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "InlineIconHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(StatusIcon), horizontal: true, vertical: true);");
        viewFile.Content.Should().NotContain("ApplyLayoutSizing(RectOf(StatusIcon), ignoreLayout: false, preferredWidth: null, preferredHeight: null, flexibleWidth: 1f");
    }

    [Fact]
    public void Generate_IconShellChild_CentersGlyphWithinShell()
    {
        var doc = new HudDocument
        {
            Name = "IconShellHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "badgeShell",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(36),
                            Height = Dimension.Pixels(36)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "badgeIcon",
                                Type = ComponentType.Icon,
                                Style = new StyleSpec
                                {
                                    FontFamily = "Lucide",
                                    FontSize = 16
                                },
                                Layout = new LayoutSpec
                                {
                                    Width = Dimension.Pixels(16),
                                    Height = Dimension.Pixels(16)
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "IconShellHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyRectAnchorPreset(RectOf(BadgeIcon), \"center\");");
        viewFile.Content.Should().Contain("ApplyRectPivotPreset(RectOf(BadgeIcon), \"center\");");
    }

    [Fact]
    public void Generate_CompactRowLabel_ForcesNoWrapFromSourceSemantics()
    {
        var doc = new HudDocument
        {
            Name = "CompactLabelHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = Spacing.Uniform(8)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "ingredientLabel",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "COPPER"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    },
                    new ComponentNode
                    {
                        Id = "ingredientQty",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            Align = Alignment.End
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "12 pcs"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "CompactLabelHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyTextMetrics(IngredientLabel, lineSpacing:");
        viewFile.Content.Should().Contain("wrapText: false);");
    }

    [Fact]
    public void Generate_CompactRowLabel_PrefersIntrinsicWidthWithoutFlexGrowth()
    {
        var doc = new HudDocument
        {
            Name = "CompactLabelWidthHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = Spacing.Uniform(8),
                    Width = Dimension.Pixels(220)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "ingredientLabel",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "COPPER"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    },
                    new ComponentNode
                    {
                        Id = "ingredientQty",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            Align = Alignment.End
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "12 pcs"
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 9
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "CompactLabelWidthHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(IngredientLabel), ignoreLayout: false, preferredWidth:");
        viewFile.Content.Should().NotContain("ApplyLayoutSizing(RectOf(IngredientLabel), ignoreLayout: false, preferredWidth: null, preferredHeight: null, flexibleWidth: 1f");
    }

    [Fact]
    public void Generate_TabButtons_PreferIntrinsicWidthAndNoWrap()
    {
        var doc = new HudDocument
        {
            Name = "TabStripHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = Spacing.Uniform(8),
                    Width = Dimension.Pixels(280)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "inventoryTab",
                        Type = ComponentType.Button,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "INVENTORY"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "craftingTab",
                        Type = ComponentType.Button,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "CRAFTING"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "TabStripHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyTextMetrics(InventoryTab, lineSpacing:");
        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(InventoryTab), ignoreLayout: false, preferredWidth:");
        viewFile.Content.Should().NotContain("ApplyLayoutSizing(RectOf(InventoryTab), ignoreLayout: false, preferredWidth: null, preferredHeight: null, flexibleWidth: 1f");
        viewFile.Content.Should().Contain("wrapText: false);");
    }

    [Fact]
    public void Generate_SingleTextChipShell_StretchesAndCentersTextWithoutShellLayoutGroup()
    {
        var doc = new HudDocument
        {
            Name = "ChipShellHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Gap = Spacing.Uniform(8)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "filterAll",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(138),
                            Height = Dimension.Pixels(42)
                        },
                        Style = new StyleSpec
                        {
                            Background = Color.Parse("#F5BE58")
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "filterAllText",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["Text"] = "ALL"
                                },
                                Style = new StyleSpec
                                {
                                    FontFamily = "Press Start 2P",
                                    FontSize = 10
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "ChipShellHudView.ugui.cs");

        viewFile.Content.Should().NotContain("ApplyVerticalLayout(RectOf(FilterAll)");
        viewFile.Content.Should().Contain("ApplyRectTransformMode(RectOf(FilterAllText), \"center\");");
        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(FilterAllText), horizontal: true, vertical: true);");
        viewFile.Content.Should().Contain("ApplyTextAlignment(FilterAllText, \"center\");");
    }

    [Fact]
    public void Generate_FixedPanelStack_PreservesAuthoredVerticalSpacingWithoutOverflowCompaction()
    {
        var doc = new HudDocument
        {
            Name = "FixedPanelStackHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(836),
                    Height = Dimension.Pixels(860),
                    Gap = Spacing.Uniform(16),
                    Padding = new Spacing(18, 18, 18, 18)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "header",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(800),
                            Height = Dimension.Pixels(56)
                        }
                    },
                    new ComponentNode
                    {
                        Id = "blueprintView",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(800),
                            Height = Dimension.Pixels(372)
                        },
                        Style = new StyleSpec
                        {
                            Background = Color.Parse("#121419")
                        }
                    },
                    new ComponentNode
                    {
                        Id = "resourceRequirements",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(800),
                            Height = Dimension.Pixels(176)
                        }
                    },
                    new ComponentNode
                    {
                        Id = "bottomActionStrip",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(800),
                            Height = Dimension.Pixels(200)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.Single(file => file.Path == "FixedPanelStackHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 16f, 18, 18, 18, 18");
        viewFile.Content.Should().NotContain("ApplyVerticalLayout(Root, 10.667f, 18, 18, 12, 12");
    }

    [Fact]
    public void Generate_WithSemanticIconMetricProfiles_AppliesLeadingAndBadgeIconCalibration()
    {
        var doc = new HudDocument
        {
            Name = "IconMetricHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = Spacing.Uniform(8)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "row",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Horizontal,
                            Gap = Spacing.Uniform(8)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "rowIcon",
                                Type = ComponentType.Icon,
                                Style = new StyleSpec
                                {
                                    FontFamily = "Lucide",
                                    FontSize = 18
                                },
                                Layout = new LayoutSpec
                                {
                                    Width = Dimension.Pixels(18),
                                    Height = Dimension.Pixels(18)
                                }
                            },
                            new ComponentNode
                            {
                                Id = "rowLabel",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["Text"] = "MATERIAL"
                                },
                                Style = new StyleSpec
                                {
                                    FontFamily = "Press Start 2P",
                                    FontSize = 9
                                }
                            }
                        ]
                    },
                    new ComponentNode
                    {
                        Id = "badgeShell",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(36),
                            Height = Dimension.Pixels(36)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "badgeIcon",
                                Type = ComponentType.Icon,
                                Style = new StyleSpec
                                {
                                    FontFamily = "Lucide",
                                    FontSize = 16
                                },
                                Layout = new LayoutSpec
                                {
                                    Width = Dimension.Pixels(16),
                                    Height = Dimension.Pixels(16)
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                MetricProfiles =
                [
                    new GeneratorMetricProfile
                    {
                        Name = "ugui-leading-icon-baseline",
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            ComponentType = ComponentType.Icon,
                            SemanticClass = "leading-icon"
                        },
                        Template = new GeneratorActionTemplate
                        {
                            Kind = "iconBaselineOffsetDelta",
                            NumberValue = 1
                        }
                    },
                    new GeneratorMetricProfile
                    {
                        Name = "ugui-badge-icon-centering",
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            ComponentType = ComponentType.Icon,
                            SemanticClass = "badge-icon"
                        },
                        Template = new GeneratorActionTemplate
                        {
                            Kind = "iconCenteringPolicy",
                            BoolValue = false
                        }
                    }
                ]
            }
        });

        var viewFile = result.Files.Single(file => file.Path == "IconMetricHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyIconMetrics(RowIcon, boxWidth: 18f, boxHeight: 18f, baselineOffset: 1f");
        viewFile.Content.Should().Contain("ApplyIconMetrics(BadgeIcon, boxWidth: 16f, boxHeight: 16f, baselineOffset: 0f, opticalCentering: false");
    }

    [Fact]
    public void Generate_WithRepeatedSourceIds_AppliesBuildProgramOverrideToMatchingStructuralPath()
    {
        var doc = new HudDocument
        {
            Name = "RepeatHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "hpBar",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "FIRST"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "hpBar",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "SECOND"
                        }
                    }
                ]
            }
        };

        var prepared = GenerationDocumentPreprocessor.Prepare(doc, _options, "ugui");
        var hpBarStableIds = Flatten(prepared.VisualDocument.Root)
            .Where(node => string.Equals(node.SourceId, "hpBar", StringComparison.Ordinal))
            .Select(node => node.StableId)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToList();
        hpBarStableIds.Should().HaveCount(2);
        var firstHpBarStableId = hpBarStableIds[0];
        var secondHpBarStableId = hpBarStableIds[1];

        var buildProgram = new UGuiBuildProgram
        {
            DocumentName = prepared.VisualDocument.DocumentName,
            BackendFamily = prepared.VisualDocument.BackendFamily,
            SourceGenerationMode = prepared.VisualDocument.SourceGenerationMode,
            RootStableId = prepared.VisualDocument.Root.StableId,
            CandidateCatalogs =
            [
                new UGuiBuildCandidateCatalog
                {
                    StableId = firstHpBarStableId,
                    SolveStage = "motif",
                    Candidates =
                    [
                        new UGuiBuildCandidate
                        {
                            CandidateId = "force-scrollrect",
                            Action = new GeneratorRuleAction
                            {
                                ControlType = "ScrollRect"
                            }
                        }
                    ]
                }
            ],
            AcceptedCandidates =
            [
                new UGuiBuildSelection
                {
                    StableId = firstHpBarStableId,
                    CandidateId = "force-scrollrect"
                }
            ]
        };

        secondHpBarStableId.Should().NotBe(firstHpBarStableId);

        var buildProgramPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(buildProgramPath, GenerationDocumentPreprocessor.ToJson(buildProgram));

            var result = _generator.Generate(doc, _options with
            {
                UGuiBuildProgramPath = buildProgramPath
            });

            var viewFile = result.Files.Single(file => file.Path == "RepeatHudView.ugui.cs");
            viewFile.Content.Should().Contain("public ScrollRect HpBar { get; }");
            viewFile.Content.Should().Contain("public TextMeshProUGUI HpBar2 { get; }");
        }
        finally
        {
            if (File.Exists(buildProgramPath))
            {
                File.Delete(buildProgramPath);
            }
        }
    }

    private static IEnumerable<VisualNode> Flatten(VisualNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    [Fact]
    public void Generate_WithBindings_EmitsViewModelInterfaceAndRefreshAssignments()
    {
        var doc = new HudDocument
        {
            Name = "StatusHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "statusLabel",
                        Type = ComponentType.Label,
                        Bindings = [new BindingSpec { Property = "text", Path = "Player.HealthText" }],
                        Visible = BindableValue<bool>.Bind("Player.IsVisible")
                    },
                    new ComponentNode
                    {
                        Id = "actionButton",
                        Type = ComponentType.Button,
                        Bindings = [new BindingSpec { Property = "text", Path = "Player.ActionLabel" }],
                        Enabled = BindableValue<bool>.Bind("Player.CanAct")
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path == "StatusHudView.ugui.cs");
        viewFile.Content.Should().Contain("public TextMeshProUGUI StatusLabel { get; }");
        viewFile.Content.Should().Contain("public Button ActionButton { get; }");
        viewFile.Content.Should().Contain("StatusLabel.text = AsString(_viewModel.PlayerHealthText);");
        viewFile.Content.Should().Contain("StatusLabel.gameObject.SetActive(AsBool(_viewModel.PlayerIsVisible, true));");
        viewFile.Content.Should().Contain("SetButtonText(ActionButton, AsString(_viewModel.PlayerActionLabel));");
        viewFile.Content.Should().Contain("ApplyEnabled(ActionButton, AsBool(_viewModel.PlayerCanAct, true));");

        var interfaceFile = result.Files.First(f => f.Path == "IStatusHudViewModel.g.cs");
        interfaceFile.Content.Should().Contain("object? PlayerHealthText { get; }");
        interfaceFile.Content.Should().Contain("object? PlayerIsVisible { get; }");
        interfaceFile.Content.Should().Contain("object? PlayerActionLabel { get; }");
        interfaceFile.Content.Should().Contain("object? PlayerCanAct { get; }");
    }

    [Fact]
    public void Generate_ComponentRef_ComposesReferencedUGuiView()
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
                            ["text"] = "READY"
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
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
                    new ComponentNode
                    {
                        Id = "readyBadge",
                        Type = ComponentType.Container,
                        ComponentRefId = "badge"
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path == "StatusHudView.ugui.cs");
        viewFile.Content.Should().Contain("var readyBadgeView = new BadgeView(Root, null, null);");
        viewFile.Content.Should().Contain("ReadyBadge = readyBadgeView.Root;");
    }

    [Fact]
    public void Generate_ComponentRefWithInstanceOverrides_PassesOverrideBagIntoChildView()
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
                            ["text"] = "READY"
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
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
                    new ComponentNode
                    {
                        Id = "readyBadge",
                        Type = ComponentType.Container,
                        ComponentRefId = "badge",
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.ComponentPropertyOverrides] = new Dictionary<string, object?>
                            {
                                [ComponentInstanceOverrideSupport.ChildPath(ComponentInstanceOverrideSupport.RootPath, 0)] = new Dictionary<string, object?>
                                {
                                    ["text"] = "GO"
                                }
                            }
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "StatusHudView.ugui.cs");
        var badgeFile = result.Files.First(f => f.Path == "BadgeView.ugui.cs");

        viewFile.Content.Should().Contain("new BadgeView(Root, null, new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal)");
        badgeFile.Content.Should().Contain("private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? _componentOverrides;");
        badgeFile.Content.Should().Contain("private void ApplyInstanceOverrides()");
        badgeFile.Content.Should().Contain("TryGetComponentOverrideValue(\"$/0\", \"text\", out var componentOverrideValue0)");
    }

    [Fact]
    public void Generate_WithSyntheticExactReuse_EmitsSyntheticComponentArtifactsForUGui()
    {
        var doc = new HudDocument
        {
            Name = "QuestHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    CreateSyntheticCandidateCard("card-alpha", 12, 32),
                    CreateSyntheticCandidateCard("card-bravo", 312, 64)
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        result.Files.Should().Contain(file => file.Path == "QuestHud.synthetic-components.json");
        result.Files.Should().Contain(file => file.Path.StartsWith("Synthetic", StringComparison.Ordinal) && file.Path.EndsWith("View.ugui.cs", StringComparison.Ordinal));
        result.Files.Should().Contain(file => file.Path.StartsWith("ISynthetic", StringComparison.Ordinal) && file.Path.EndsWith("ViewModel.g.cs", StringComparison.Ordinal));

        var viewFile = result.Files.First(f => f.Path == "QuestHudView.ugui.cs");
        viewFile.Content.Should().Contain("new Synthetic");
        viewFile.Content.Should().Contain("ConfigureRect(RectOf(CardAlpha), width: null, height: null, left: 12f, top: 32f, absolute: true);");
        viewFile.Content.Should().Contain("ConfigureRect(RectOf(CardBravo), width: null, height: null, left: 312f, top: 64f, absolute: true);");
        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(CardAlpha), ignoreLayout: true, preferredWidth: null, preferredHeight: null, flexibleWidth: null, flexibleHeight: null);");
        viewFile.Content.Should().NotContain("ApplyHorizontalLayout(RectOf(CardAlpha)");
        viewFile.Content.Should().NotContain("ApplyHorizontalLayout(RectOf(CardBravo)");
        viewFile.Content.Should().NotContain("ApplyContentSizeFit(RectOf(CardAlpha)");
        viewFile.Content.Should().NotContain("ApplyContentSizeFit(RectOf(CardBravo)");
    }

    [Fact]
    public void Generate_NumericIds_AreSanitizedIntoValidIdentifiers()
    {
        var doc = new HudDocument
        {
            Name = "MinimapHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "00",
                        Type = ComponentType.Container
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "MinimapHudView.ugui.cs");

        viewFile.Content.Should().Contain("public RectTransform Node00 { get; }");
        viewFile.Content.Should().Contain("Node00 = CreateRect(\"Node00\", Root);");
    }

    [Fact]
    public void Generate_IconComponent_NormalizesTokenText()
    {
        var doc = new HudDocument
        {
            Name = "IconHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "classIcon",
                        Type = ComponentType.Icon,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "shield"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "IconHudView.ugui.cs");

        viewFile.Content.Should().Contain("ClassIcon.text = ResolveIconText(\"shield\");");
        viewFile.Content.Should().Contain("\"shield\"=>\"\\uE158\"");
        viewFile.Content.Should().Contain("ApplyStyle(ClassIcon, fg: null, bg: null, fontFamily: null, fontSize: 16");
        viewFile.Content.Should().Contain("treatAsIcon: true");
    }

    [Fact]
    public void Generate_LeafNodes_DoNotEmitLayoutGroups_AndBordersAreApplied()
    {
        var doc = new HudDocument
        {
            Name = "MiniMapTile",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(24),
                    Height = Dimension.Pixels(24)
                },
                Style = new StyleSpec
                {
                    Background = Color.Black,
                    Border = new BorderSpec
                    {
                        Style = BorderStyle.Solid,
                        Color = Color.White,
                        Width = 2
                    }
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Pixels(24),
                            Height = Dimension.Pixels(24)
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "flame"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "MiniMapTileView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyBorder(component.gameObject,ParseColor(borderColor,Color.white),borderWidth.Value);");
        viewFile.Content.Should().Contain("ApplyStyle(Root, fg: null, bg: \"#000000\", fontFamily: null, fontSize: null, borderColor: \"#FFFFFF\", borderWidth: 2f, treatAsIcon: false);");
        viewFile.Content.Should().NotContain("ApplyVerticalLayout(RectOf(Icon)");
    }

    [Fact]
    public void Generate_FixedWidthLabel_WithLineHeight_EmitsTextMetrics()
    {
        var doc = new HudDocument
        {
            Name = "WrappedHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "body",
                        Type = ComponentType.Label,
                        InstanceOverrides = new Dictionary<string, object?>
                        {
                            [BoomHudMetadataKeys.PencilTextGrowth] = "fixed-width"
                        },
                        Style = new StyleSpec
                        {
                            FontSize = 14,
                            LineHeight = 1.4
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Wrapped copy"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "WrappedHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyTextMetrics(Body, lineSpacing: 1.4f, letterSpacing: null, wrapText: true);");
        viewFile.Content.Should().Contain("text.lineSpacing=lineSpacing.Value;");
        viewFile.Content.Should().Contain("text.horizontalOverflow=wrapText?HorizontalWrapMode.Wrap:HorizontalWrapMode.Overflow;");
    }

    [Fact]
    public void Generate_TextWithLetterSpacing_AppliesTmpCharacterSpacing()
    {
        var doc = new HudDocument
        {
            Name = "LetterSpacingHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "body",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 10,
                            LetterSpacing = 1.5
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "TRACKED"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "LetterSpacingHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyTextMetrics(Body, lineSpacing: null, letterSpacing: 1.5f, wrapText: false);");
        viewFile.Content.Should().Contain("text.characterSpacing=letterSpacing.Value/fontSize*100f;");
        viewFile.Content.Should().Contain("var isPixelFont=fontFamily==\"Press Start 2P\";");
        viewFile.Content.Should().Contain("text.useMaxVisibleDescender=true;");
        viewFile.Content.Should().Contain("text.extraPadding=false;");
    }

    [Fact]
    public void Generate_AutoHeightLayoutRoot_UsesContentSizeFitter()
    {
        var doc = new HudDocument
        {
            Name = "AutoHeightHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(130)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Ready"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "AutoHeightHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyContentSizeFit(Root, horizontal: false, vertical: true);");
    }

    [Fact]
    public void Generate_AbsoluteOverlayText_WithAutoSize_UsesContentHugWithoutRejoiningLayoutFlow()
    {
        var doc = new HudDocument
        {
            Name = "OverlayTextHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "progress",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Absolute,
                            Width = Dimension.Pixels(540),
                            Height = Dimension.Pixels(22)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "progressText",
                                Type = ComponentType.Label,
                                Layout = new LayoutSpec
                                {
                                    Left = Dimension.Pixels(12),
                                    Top = Dimension.Pixels(4)
                                },
                                Style = new StyleSpec
                                {
                                    FontFamily = "Press Start 2P",
                                    FontSize = 9
                                },
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["text"] = "RESPONSE WINDOW 58%"
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "OverlayTextHudView.ugui.cs");

        viewFile.Content.Should().Contain("ConfigureRect(RectOf(ProgressText), width: null, height: null, left: 12f, top: 4f, absolute: true);");
        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(ProgressText), ignoreLayout: true, preferredWidth: null, preferredHeight: null, flexibleWidth: null, flexibleHeight: null);");
        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(ProgressText), horizontal: true, vertical: true);");
    }

    [Fact]
    public void Generate_ValueRowTextChildren_UseHorizontalContentHug()
    {
        var doc = new HudDocument
        {
            Name = "TopBarValueRowHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Width = Dimension.Pixels(540),
                    Gap = Spacing.Uniform(12)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "sectionLabel",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 18
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "MODULE FABRICATION"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "tabOne",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 10
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "ASSEMBLY"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "crewCount",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            Align = Alignment.End
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 10
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "CREW 11/16"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "TopBarValueRowHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(SectionLabel), horizontal: true, vertical: false);");
        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(TabOne), horizontal: true, vertical: false);");
        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(CrewCount), horizontal: true, vertical: false);");
    }

    [Fact]
    public void Generate_ValueRow_UsesFlexibleTrailingCarrierForRightEdgeCluster()
    {
        var doc = new HudDocument
        {
            Name = "ValueRowSpacerHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Width = Dimension.Pixels(540),
                    Gap = Spacing.Uniform(12)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "sectionLabel",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 18
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "MODULE FABRICATION"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "tabOne",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 10
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "ASSEMBLY"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "crewCount",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            Align = Alignment.End
                        },
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 10
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "CREW 11/16"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "timeText",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontFamily = "Press Start 2P",
                            FontSize = 10
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["Text"] = "06:20"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "ValueRowSpacerHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(CrewCount), ignoreLayout: false, preferredWidth: null, preferredHeight: null, flexibleWidth: 1f, flexibleHeight: null);");
        viewFile.Content.Should().Contain("ApplyTextAlignment(CrewCount, \"top-right\");");
    }

    [Fact]
    public void Generate_TextStackChildrenWithContentHug_PropagatesVerticalHugToContainer()
    {
        var doc = new HudDocument
        {
            Name = "ObjectiveHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "iconShell",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Pixels(44),
                            Height = Dimension.Pixels(44)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "icon",
                                Type = ComponentType.Icon,
                                Layout = new LayoutSpec
                                {
                                    Width = Dimension.Pixels(20),
                                    Height = Dimension.Pixels(20)
                                }
                            }
                        ]
                    },
                    new ComponentNode
                    {
                        Id = "body",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "title",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["text"] = "Recover the key"
                                }
                            },
                            new ComponentNode
                            {
                                Id = "hint",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["text"] = "Search the chapel."
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Name = "stacked-line-hug",
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "ugui",
                        DocumentName = "ObjectiveHud",
                        SemanticClass = "stacked-text-line"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            PreferContentHeight = true
                        }
                    }
                }
            ]
        };

        var result = _generator.Generate(doc, _options with { RuleSet = ruleSet });
        var viewFile = result.Files.First(f => f.Path == "ObjectiveHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(Body), ignoreLayout: false, preferredWidth: null, preferredHeight: null, flexibleWidth: 1f, flexibleHeight: null);");
        viewFile.Content.Should().Contain("ApplyContentSizeFit(RectOf(Body), horizontal: false, vertical: true);");
    }

    [Fact]
    public void Generate_OverflowingCrossAxisCardShell_PreservesSourceSpacing()
    {
        var doc = new HudDocument
        {
            Name = "PartyStripHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Height = Dimension.Pixels(216)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "memberA",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Pixels(400),
                            Height = Dimension.Fill,
                            Gap = Spacing.Uniform(12),
                            Padding = Spacing.Uniform(12)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "heroRow",
                                Type = ComponentType.Container,
                                Layout = new LayoutSpec
                                {
                                    Type = LayoutType.Horizontal,
                                    Height = Dimension.Pixels(76)
                                }
                            },
                            new ComponentNode
                            {
                                Id = "hpBar",
                                Type = ComponentType.ProgressBar,
                                Layout = new LayoutSpec
                                {
                                    Height = Dimension.Pixels(22)
                                }
                            },
                            new ComponentNode
                            {
                                Id = "mpBar",
                                Type = ComponentType.ProgressBar,
                                Layout = new LayoutSpec
                                {
                                    Height = Dimension.Pixels(22)
                                }
                            },
                            new ComponentNode
                            {
                                Id = "statusRow",
                                Type = ComponentType.Container,
                                Layout = new LayoutSpec
                                {
                                    Type = LayoutType.Horizontal,
                                    Height = Dimension.Pixels(56)
                                },
                                Children =
                                [
                                    new ComponentNode
                                    {
                                        Id = "statusBuff1",
                                        Type = ComponentType.Container,
                                        Layout = new LayoutSpec
                                        {
                                            Width = Dimension.Pixels(56),
                                            Height = Dimension.Pixels(56)
                                        }
                                    },
                                    new ComponentNode
                                    {
                                        Id = "statusBuff2",
                                        Type = ComponentType.Container,
                                        Layout = new LayoutSpec
                                        {
                                            Width = Dimension.Pixels(56),
                                            Height = Dimension.Pixels(56)
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "PartyStripHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyHorizontalLayout(Root, 0f, 0, 0, 0, 0, null, childControlWidth: false, childControlHeight: false);");
        viewFile.Content.Should().Contain("ConfigureRect(Root, width: null, height: 216f, left: null, top: null, absolute: false);");
        viewFile.Content.Should().Contain("ApplyLayoutSizing(Root, ignoreLayout: false, preferredWidth: null, preferredHeight: 216f, flexibleWidth: null, flexibleHeight: null);");
        viewFile.Content.Should().Contain("ConfigureRect(RectOf(MemberA), width: 400f, height: 216f, left: null, top: null, absolute: false);");
        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(MemberA), ignoreLayout: false, preferredWidth: 400f, preferredHeight: 216f, flexibleWidth: null, flexibleHeight: 1f);");
        viewFile.Content.Should().Contain("ApplyVerticalLayout(RectOf(MemberA), 12f, 12, 12, 12, 12, null, childControlWidth: true, childControlHeight: false);");
        viewFile.Content.Should().Contain("ApplyHorizontalLayout(RectOf(StatusRow), 0f, 0, 0, 0, 0, null, childControlWidth: false, childControlHeight: false);");
    }

    [Fact]
    public void Generate_ComponentRefChild_PreservesSourceSpacingInOverflowingCrossAxisShell()
    {
        var heroRowComponent = new HudComponentDefinition
        {
            Id = "hero-row",
            Name = "HeroRow",
            Root = new ComponentNode
            {
                Id = "heroRowRoot",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Height = Dimension.Pixels(76)
                }
            }
        };

        var doc = new HudDocument
        {
            Name = "PartyStripHud",
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["hero-row"] = heroRowComponent
            },
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Horizontal,
                    Height = Dimension.Pixels(216)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "memberA",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Pixels(400),
                            Height = Dimension.Fill,
                            Gap = Spacing.Uniform(12),
                            Padding = Spacing.Uniform(12)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "heroRow",
                                Type = ComponentType.Container,
                                ComponentRefId = "hero-row"
                            },
                            new ComponentNode
                            {
                                Id = "hpBar",
                                Type = ComponentType.ProgressBar,
                                Layout = new LayoutSpec
                                {
                                    Height = Dimension.Pixels(22)
                                }
                            },
                            new ComponentNode
                            {
                                Id = "mpBar",
                                Type = ComponentType.ProgressBar,
                                Layout = new LayoutSpec
                                {
                                    Height = Dimension.Pixels(22)
                                }
                            },
                            new ComponentNode
                            {
                                Id = "statusRow",
                                Type = ComponentType.Container,
                                Layout = new LayoutSpec
                                {
                                    Type = LayoutType.Horizontal,
                                    Height = Dimension.Pixels(56)
                                },
                                Children =
                                [
                                    new ComponentNode
                                    {
                                        Id = "statusBuff1",
                                        Type = ComponentType.Container,
                                        Layout = new LayoutSpec
                                        {
                                            Width = Dimension.Pixels(56),
                                            Height = Dimension.Pixels(56)
                                        }
                                    },
                                    new ComponentNode
                                    {
                                        Id = "statusBuff2",
                                        Type = ComponentType.Container,
                                        Layout = new LayoutSpec
                                        {
                                            Width = Dimension.Pixels(56),
                                            Height = Dimension.Pixels(56)
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "PartyStripHudView.ugui.cs");

        viewFile.Content.Should().Contain("var heroRowView = new HeroRowView(RectOf(MemberA), null, null);");
        viewFile.Content.Should().Contain("ApplyHorizontalLayout(Root, 0f, 0, 0, 0, 0, null, childControlWidth: false, childControlHeight: false);");
        viewFile.Content.Should().Contain("ApplyVerticalLayout(RectOf(MemberA), 12f, 12, 12, 12, 12, null, childControlWidth: true, childControlHeight: false);");
    }

    [Fact]
    public void Generate_NonOverflowingCrossAxisHeaderShell_KeepsMainAxisChildControl()
    {
        var doc = new HudDocument
        {
            Name = "PartyHeaderHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(1280),
                    Padding = Spacing.Uniform(24)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "header",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Horizontal,
                            Width = Dimension.Fill,
                            Height = Dimension.Pixels(40)
                        },
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "areaName",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["Text"] = "PartyStatusStrip"
                                }
                            },
                            new ComponentNode
                            {
                                Id = "encounterState",
                                Type = ComponentType.Label,
                                Properties = new Dictionary<string, BindableValue<object?>>
                                {
                                    ["Text"] = "ENCOUNTER READY"
                                }
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "PartyHeaderHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyHorizontalLayout(RectOf(Header), 0f, 0, 0, 0, 0);");
    }

    [Fact]
    public void Generate_ExplicitMainAxisShell_StillCompactsOverflow()
    {
        var doc = new HudDocument
        {
            Name = "PinnedShellHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Height = Dimension.Pixels(100),
                    Gap = Spacing.Uniform(12),
                    Padding = Spacing.Uniform(12)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "top",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Height = Dimension.Pixels(44)
                        }
                    },
                    new ComponentNode
                    {
                        Id = "bottom",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Height = Dimension.Pixels(44)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "PinnedShellHudView.ugui.cs");

        viewFile.Content.Should().Contain("ConfigureRect(Root, width: null, height: 100f, left: null, top: null, absolute: false);");
        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 4f, 12, 12, 4, 4, null, childControlWidth: true, childControlHeight: false);");
    }

    [Fact]
    public void Generate_LayoutAlignmentPreset_EmitsUguiLayoutGroupAlignment()
    {
        var doc = new HudDocument
        {
            Name = "AlignedHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = Spacing.Uniform(6),
                    Padding = Spacing.Uniform(4)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(20),
                            Height = Dimension.Pixels(20)
                        }
                    }
                ]
            }
        };

        var ruleSet = new GeneratorRuleSet
        {
            Rules =
            [
                new GeneratorRule
                {
                    Name = "center-layout",
                    Selector = new GeneratorRuleSelector
                    {
                        Backend = "ugui",
                        DocumentName = "AlignedHud",
                        NodeId = "root"
                    },
                    Action = new GeneratorRuleAction
                    {
                        Layout = new GeneratorLayoutRuleAction
                        {
                            FlexAlignmentPreset = "center"
                        }
                    }
                }
            ]
        };

        var result = _generator.Generate(doc, _options with { RuleSet = ruleSet });
        var viewFile = result.Files.First(f => f.Path == "AlignedHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 6f, 4, 4, 4, 4, \"center\", childControlWidth: false, childControlHeight: false);");
        viewFile.Content.Should().Contain("ApplyLayoutAlignment(group,alignmentPreset);");
    }

    [Fact]
    public void Generate_LayoutAlignmentFromLayoutSpec_EmitsUguiLayoutGroupAlignment()
    {
        var doc = new HudDocument
        {
            Name = "StatusBuffHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(56),
                    Height = Dimension.Pixels(56),
                    Align = Alignment.Center,
                    Justify = Justification.Center
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(24),
                            Height = Dimension.Pixels(24)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "StatusBuffHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 0f, 0, 0, 0, 0, \"center\", childControlWidth: false, childControlHeight: false);");
    }

    [Fact]
    public void Generate_VerticalTopCenterAlignment_EmitsUguiTopCenterPreset()
    {
        var doc = new HudDocument
        {
            Name = "CharPortraitAlignmentHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Width = Dimension.Pixels(130),
                    Gap = Spacing.Uniform(8),
                    Align = Alignment.Center,
                    Justify = Justification.Start
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "face",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(56),
                            Height = Dimension.Pixels(56)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "CharPortraitAlignmentHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 8f, 0, 0, 0, 0, \"top-center\", childControlWidth: false, childControlHeight: false);");
    }

    [Fact]
    public void Generate_EmitsPrefabBindingApi_ForExistingHierarchy()
    {
        var doc = new HudDocument
        {
            Name = "PrefabHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Ready"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);
        var viewFile = result.Files.First(f => f.Path == "PrefabHudView.ugui.cs");

        viewFile.Content.Should().Contain("public static PrefabHudView Bind(RectTransform root, IPrefabHudViewModel? viewModel = null, IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? componentOverrides = null) => new(root, viewModel, componentOverrides);");
        viewFile.Content.Should().Contain("private PrefabHudView(RectTransform root, IPrefabHudViewModel? viewModel, IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? componentOverrides)");
        viewFile.Content.Should().Contain("Title = RequireComponent<TextMeshProUGUI>(Root, \"Title\");");
        viewFile.Content.Should().Contain("private static RectTransform RequireRect(Transform root,string path)");
        viewFile.Content.Should().Contain("private static T RequireComponent<T>(Transform root,string path) where T : Component");
    }

    [Fact]
    public void Generate_WithMotion_EmitsSharedTimelineArtifactsForUGui()
    {
        var doc = new HudDocument
        {
            Name = "StatusHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label
                    }
                ]
            }
        };

        var motion = new MotionDocument
        {
            Name = "StatusHudMotion",
            FramesPerSecond = 30,
            DefaultSequenceId = "introSequence",
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    DurationFrames = 24,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "rootTrack",
                            TargetId = "root",
                            TargetKind = MotionTargetKind.Root,
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Opacity,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(0) },
                                        new MotionKeyframe { Frame = 12, Value = MotionValue.FromNumber(1), Easing = MotionEasing.EaseOut }
                                    ]
                                }
                            ]
                        },
                        new MotionTrack
                        {
                            Id = "titleTrack",
                            TargetId = "title",
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Text,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromText("BOOT") },
                                        new MotionKeyframe { Frame = 18, Value = MotionValue.FromText("READY") }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ],
            Sequences =
            [
                new MotionSequence
                {
                    Id = "introSequence",
                    Name = "Intro Sequence",
                    Items =
                    [
                        new MotionSequenceItem
                        {
                            ClipId = "intro",
                            StartFrame = 0,
                            FillMode = MotionSequenceFillMode.HoldEnd
                        }
                    ]
                }
            ]
        };

        var result = _generator.Generate(doc, _options with { Motion = motion });

        result.Success.Should().BeTrue();
        result.Files.Should().Contain(f => f.Path == "StatusHudMotion.gen.cs");
        result.Files.Should().Contain(f => f.Path == "StatusHudMotionHost.gen.cs");

        var motionFile = result.Files.First(f => f.Path == "StatusHudMotion.gen.cs");
        motionFile.Content.Should().Contain("public const int FramesPerSecond = 30;");
        motionFile.Content.Should().Contain("public const string DefaultSequenceId = \"introSequence\";");
        motionFile.Content.Should().Contain("public static TimelineSequenceClip[] GetSequenceItems(string sequenceId)");
        motionFile.Content.Should().Contain("ApplyOpacity(view.Root, EvaluateNumber(localFrame, s_IntroRootTrackOpacity, 1f));");
        motionFile.Content.Should().Contain("ApplyText(view.Title, EvaluateString(localFrame, s_IntroTitleTrackText, string.Empty));");
        motionFile.Content.Should().Contain("var canvasGroup = target.GetComponent<CanvasGroup>();");
        motionFile.Content.Should().Contain("canvasGroup = target.gameObject.AddComponent<CanvasGroup>();");
        motionFile.Content.Should().Contain("var state = BoomHudMotionRectState.Capture(rectTransform);");

        var hostFile = result.Files.First(f => f.Path == "StatusHudMotionHost.gen.cs");
        hostFile.Content.Should().Contain("public class StatusHudMotionHost : BoomHudUguiMotionHost");
        hostFile.Content.Should().NotContain("OnMotionApplied(");
        hostFile.Content.Should().Contain("if (root.childCount == 1 && root.GetChild(0) is RectTransform childRoot && TryBindExisting(childRoot, out boundView))");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WithMotionRuleSet_AppliesMotionPoliciesForUGui()
    {
        var doc = new HudDocument
        {
            Name = "StatusHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label
                    }
                ]
            }
        };

        var motion = new MotionDocument
        {
            Name = "StatusHudMotion",
            FramesPerSecond = 30,
            DefaultSequenceId = "introSequence",
            Clips =
            [
                new MotionClip
                {
                    Id = "intro",
                    Name = "Intro",
                    DurationFrames = 60,
                    Tracks =
                    [
                        new MotionTrack
                        {
                            Id = "rootTrack",
                            TargetId = "root",
                            TargetKind = MotionTargetKind.Root,
                            Channels =
                            [
                                new MotionChannel
                                {
                                    Property = MotionProperty.Opacity,
                                    Keyframes =
                                    [
                                        new MotionKeyframe { Frame = 0, Value = MotionValue.FromNumber(0), Easing = MotionEasing.EaseOut },
                                        new MotionKeyframe { Frame = 12, Value = MotionValue.FromNumber(1), Easing = MotionEasing.EaseOut }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ],
            Sequences =
            [
                new MotionSequence
                {
                    Id = "introSequence",
                    Name = "Intro Sequence",
                    Items =
                    [
                        new MotionSequenceItem
                        {
                            ClipId = "intro",
                            StartFrame = 0,
                            FillMode = MotionSequenceFillMode.HoldEnd
                        }
                    ]
                }
            ]
        };

        var result = _generator.Generate(doc, _options with
        {
            Motion = motion,
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Phase = GeneratorRulePhase.Motion,
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            DocumentName = "StatusHud",
                            ClipId = "intro"
                        },
                        Template = new GeneratorActionTemplate
                        {
                            Kind = "durationQuantization",
                            NumberValue = 16
                        }
                    },
                    new GeneratorRule
                    {
                        Phase = GeneratorRulePhase.Motion,
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            DocumentName = "StatusHud",
                            SequenceId = "introSequence"
                        },
                        Template = new GeneratorActionTemplate
                        {
                            Kind = "fillModePolicy",
                            Parameters = new Dictionary<string, string>
                            {
                                ["fillMode"] = "HoldBoth"
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Phase = GeneratorRulePhase.Motion,
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            DocumentName = "StatusHud",
                            TrackId = "rootTrack",
                            MotionProperty = MotionProperty.Opacity
                        },
                        Template = new GeneratorActionTemplate
                        {
                            Kind = "easingRemap",
                            Parameters = new Dictionary<string, string>
                            {
                                ["easing"] = "Linear"
                            }
                        }
                    }
                ]
            }
        });

        var motionFile = result.Files.First(f => f.Path == "StatusHudMotion.gen.cs").Content;
        motionFile.Should().Contain("public const string DefaultSequenceId = \"introSequence\";");
        motionFile.Should().Contain("FillMode = TimelineSequenceFillMode.HoldBoth");
        motionFile.Should().Contain("=> 64,");
        motionFile.Should().Contain("EaseMode.Linear");
    }

    [Fact]
    public void Generate_WithRuleSet_RemapsLabelToInputField()
    {
        var options = _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "title",
                            ComponentType = ComponentType.Label
                        },
                        Action = new GeneratorRuleAction
                        {
                            ControlType = "InputField"
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "RuleHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "title",
                        Type = ComponentType.Label,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Ready"
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "RuleHudView.ugui.cs");

        viewFile.Content.Should().Contain("public InputField Title { get; }");
        viewFile.Content.Should().Contain("Title = CreateInput(\"Title\", Root, false);");
        viewFile.Content.Should().Contain("Title.text = \"Ready\";");
    }

    [Fact]
    public void Generate_WithRuleSet_OverridesTextIconAndLayoutPolicies()
    {
        var options = _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "body"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Text = new GeneratorTextRuleAction
                            {
                                WrapText = true,
                                LineHeight = 1.6,
                                FontSize = 18
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "classIcon"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Icon = new GeneratorIconRuleAction
                            {
                                BaselineOffset = 1.5,
                                OpticalCentering = false,
                                SizeMode = "match-height",
                                FontSize = 20
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "root"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                Gap = 9,
                                Padding = 7
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "badge"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                ForceAbsolutePositioning = true,
                                OffsetX = 3,
                                OffsetY = 4
                            }
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "PolicyHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = new Spacing(2),
                    Padding = new Spacing(1)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "body",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            FontSize = 14
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "Wrapped copy"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "classIcon",
                        Type = ComponentType.Icon,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(24),
                            Height = Dimension.Pixels(24)
                        },
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["text"] = "shield"
                        }
                    },
                    new ComponentNode
                    {
                        Id = "badge",
                        Type = ComponentType.Container
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "PolicyHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyStyle(Body, fg: null, bg: null, fontFamily: null, fontSize: 18");
        viewFile.Content.Should().Contain("ApplyTextMetrics(Body, lineSpacing: 1.6f, letterSpacing: null, wrapText: true);");
        viewFile.Content.Should().Contain("ApplyIconMetrics(ClassIcon, boxWidth: 24f, boxHeight: 24f, baselineOffset: 1.5f, opticalCentering: false, sizeMode: \"match-height\", explicitFontSize: 20f);");
        viewFile.Content.Should().Contain("rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,rect.anchoredPosition.y+baselineOffset);");
        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 9f, 7, 7, 7, 7);");
        viewFile.Content.Should().Contain("ConfigureRect(RectOf(Badge), width: null, height: null, left: 3f, top: 4f, absolute: true);");
    }

    [Fact]
    public void Generate_WithRuleSet_AnchorPivotAndRectTransformMode_EmitsRectTransformOverrides()
    {
        var options = _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "badge"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                AnchorPreset = "top-center",
                                PivotPreset = "center",
                                RectTransformMode = "stretch-parent"
                            }
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "AnchorHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "badge",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(40),
                            Height = Dimension.Pixels(20)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "AnchorHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyRectAnchorPreset(RectOf(Badge), \"top-center\");");
        viewFile.Content.Should().Contain("ApplyRectPivotPreset(RectOf(Badge), \"center\");");
        viewFile.Content.Should().Contain("ApplyRectTransformMode(RectOf(Badge), \"stretch-parent\");");
        viewFile.Content.Should().Contain("private static void ApplyRectAnchorPreset(RectTransform rect,string preset)");
        viewFile.Content.Should().Contain("private static void ApplyRectPivotPreset(RectTransform rect,string preset)");
        viewFile.Content.Should().Contain("private static void ApplyRectTransformMode(RectTransform rect,string mode)");
    }

    [Fact]
    public void Generate_WithRuleSet_LayoutDeltasAndEdgeInsetPolicy_EmitsAdjustedLayoutOverrides()
    {
        var options = _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "root"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                GapDelta = 2,
                                PaddingDelta = 3
                            }
                        }
                    },
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "badge"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                ForceAbsolutePositioning = true,
                                OffsetXDelta = 3,
                                OffsetYDelta = -2,
                                EdgeInsetPolicy = "match-parent"
                            }
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "DeltaHud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical,
                    Gap = new Spacing(4),
                    Padding = new Spacing(1)
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "badge",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Absolute,
                            Left = Dimension.Pixels(5),
                            Top = Dimension.Pixels(10),
                            Width = Dimension.Pixels(40),
                            Height = Dimension.Pixels(20)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "DeltaHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyVerticalLayout(Root, 6f, 4, 4, 4, 4, null, childControlWidth: false, childControlHeight: false);");
        viewFile.Content.Should().Contain("ConfigureRect(RectOf(Badge), width: 40f, height: 20f, left: 8f, top: 8f, absolute: true);");
        viewFile.Content.Should().Contain("ApplyEdgeInsetPolicy(RectOf(Badge), \"match-parent\");");
        viewFile.Content.Should().Contain("private static void ApplyEdgeInsetPolicy(RectTransform rect,string policy)");
    }

    [Fact]
    public void Generate_WithRuleSet_PreferredSizeDeltas_AdjustsLayoutSizing()
    {
        var options = _options with
        {
            RuleSet = new GeneratorRuleSet
            {
                Rules =
                [
                    new GeneratorRule
                    {
                        Selector = new GeneratorRuleSelector
                        {
                            Backend = "ugui",
                            NodeId = "card"
                        },
                        Action = new GeneratorRuleAction
                        {
                            Layout = new GeneratorLayoutRuleAction
                            {
                                PreferredWidthDelta = -20,
                                PreferredHeightDelta = 15
                            }
                        }
                    }
                ]
            }
        };

        var doc = new HudDocument
        {
            Name = "PreferredSizeHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec
                {
                    Type = LayoutType.Vertical
                },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "card",
                        Type = ComponentType.Container,
                        Layout = new LayoutSpec
                        {
                            Type = LayoutType.Vertical,
                            Width = Dimension.Pixels(120),
                            Height = Dimension.Pixels(80)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "PreferredSizeHudView.ugui.cs");

        viewFile.Content.Should().Contain("ApplyLayoutSizing(RectOf(Card), ignoreLayout: false, preferredWidth: 100f, preferredHeight: 95f, flexibleWidth: null, flexibleHeight: null);");
    }

    private static ComponentNode CreateSyntheticCandidateCard(string cardId, double left, double top)
    {
        return new ComponentNode
        {
            Id = cardId,
            Type = ComponentType.Container,
            Layout = new LayoutSpec
            {
                Type = LayoutType.Horizontal,
                Gap = new Spacing(8),
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
                [BoomHudMetadataKeys.PencilTop] = top,
                [BoomHudMetadataKeys.PencilPosition] = "absolute"
            },
            Children =
            [
                new ComponentNode
                {
                    Id = cardId + "-title",
                    Type = ComponentType.Label,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = "QUEST"
                    },
                    Style = new StyleSpec
                    {
                        FontFamily = "Press Start 2P",
                        FontSize = 12
                    }
                },
                new ComponentNode
                {
                    Id = cardId + "-icon",
                    Type = ComponentType.Icon,
                    Properties = new Dictionary<string, BindableValue<object?>>
                    {
                        ["Text"] = "shield"
                    },
                    Style = new StyleSpec
                    {
                        FontFamily = "Lucide",
                        FontSize = 18
                    }
                }
            ]
        };
    }
}
