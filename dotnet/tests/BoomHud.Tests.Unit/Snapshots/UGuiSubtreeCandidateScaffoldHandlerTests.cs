using System.Text.Json;
using BoomHud.Abstractions.IR;
using BoomHud.Abstractions.Generation;
using BoomHud.Cli.Handlers.Rules;
using BoomHud.Generators.VisualIR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Snapshots;

public sealed class UGuiSubtreeCandidateScaffoldHandlerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _tempDir;

    public UGuiSubtreeCandidateScaffoldHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"boomhud-ugui-scaffold-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Scaffold_CreatesDirectChildMotifCatalogsAndSkipsLeaves()
    {
        var visualDocument = CreateVisualDocument();
        var buildProgram = new UGuiBuildProgram
        {
            DocumentName = visualDocument.DocumentName,
            BackendFamily = visualDocument.BackendFamily,
            SourceGenerationMode = visualDocument.SourceGenerationMode,
            RootStableId = visualDocument.Root.StableId,
            CandidateCatalogs =
            [
                new UGuiBuildCandidateCatalog
                {
                    StableId = "root/1/0",
                    SolveStage = "surface",
                    Candidates =
                    [
                        new UGuiBuildCandidate
                        {
                            CandidateId = "membera-baseline-shell",
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
                    StableId = "root/1/0",
                    CandidateId = "membera-baseline-shell"
                }
            ],
            Checkpoints =
            [
                new UGuiBuildCheckpoint
                {
                    Order = 1,
                    StableId = "root/1/0",
                    SolveStage = "surface",
                    LastStepOrder = 0,
                    Purpose = "verify-subtree"
                }
            ]
        };

        var updated = UGuiSubtreeCandidateScaffoldHandler.Scaffold(
            new UGuiSubtreeCandidateScaffoldOptions
            {
                SubtreeStableId = "root/1/0"
            },
            visualDocument,
            buildProgram,
            out var report);

        report.Created.Should().HaveCount(2);
        report.Skipped.Should().ContainSingle(entry => entry.StableId == "root/1/0/2" && entry.Reason == "leaf-node");

        updated.CandidateCatalogs.Should().Contain(catalog => catalog.StableId == "root/1/0/0");
        updated.CandidateCatalogs.Should().Contain(catalog => catalog.StableId == "root/1/0/1");
        updated.CandidateCatalogs.Should().NotContain(catalog => catalog.StableId == "root/1/0/2");
        updated.AcceptedCandidates.Should().Contain(selection => selection.StableId == "root/1/0/0");
        updated.AcceptedCandidates.Should().Contain(selection => selection.StableId == "root/1/0/1");
        updated.Checkpoints.Should().Contain(checkpoint => checkpoint.StableId == "root/1/0/0");
        updated.Checkpoints.Should().Contain(checkpoint => checkpoint.StableId == "root/1/0/1");
    }

    [Fact]
    public void Scaffold_CreatesStatBarAndStatusRowSpecializedCatalogs()
    {
        var visualDocument = CreateSpecializedVisualDocument();
        var buildProgram = new UGuiBuildProgram
        {
            DocumentName = visualDocument.DocumentName,
            BackendFamily = visualDocument.BackendFamily,
            SourceGenerationMode = visualDocument.SourceGenerationMode,
            RootStableId = visualDocument.Root.StableId
        };

        var updated = UGuiSubtreeCandidateScaffoldHandler.Scaffold(
            new UGuiSubtreeCandidateScaffoldOptions
            {
                SubtreeStableId = "root/0"
            },
            visualDocument,
            buildProgram,
            out var report);

        report.Created.Should().HaveCount(2);

        var hpBarCatalog = updated.CandidateCatalogs.Single(catalog => catalog.StableId == "root/0/0");
        hpBarCatalog.Candidates.Select(candidate => candidate.CandidateId).Should().Contain([
            "hpbar-baseline",
            "hpbar-fill-text-nudge",
            "hpbar-text-tight"
        ]);
        hpBarCatalog.Candidates.Single(candidate => candidate.CandidateId == "hpbar-fill-text-nudge")
            .DescendantActions.Select(action => action.StableId).Should().Contain(["root/0/0/0", "root/0/0/1"]);

        var statusRowCatalog = updated.CandidateCatalogs.Single(catalog => catalog.StableId == "root/0/1");
        statusRowCatalog.Candidates.Select(candidate => candidate.CandidateId).Should().Contain([
            "statusrow-baseline",
            "statusrow-tight-gap",
            "statusrow-icon-baseline-up",
            "statusrow-compact-icons"
        ]);
        statusRowCatalog.Candidates.Single(candidate => candidate.CandidateId == "statusrow-icon-baseline-up")
            .DescendantActions.Select(action => action.StableId).Should().Contain(["root/0/1/0/0", "root/0/1/1/0"]);
    }

    [Fact]
    public void Scaffold_CreatesWrapperBasedStatusRowCatalogWhenIconsLiveBehindComponentRefs()
    {
        var visualDocument = CreateWrapperStatusRowVisualDocument();
        var buildProgram = new UGuiBuildProgram
        {
            DocumentName = visualDocument.DocumentName,
            BackendFamily = visualDocument.BackendFamily,
            SourceGenerationMode = visualDocument.SourceGenerationMode,
            RootStableId = visualDocument.Root.StableId
        };

        var updated = UGuiSubtreeCandidateScaffoldHandler.Scaffold(
            new UGuiSubtreeCandidateScaffoldOptions
            {
                SubtreeStableId = "root"
            },
            visualDocument,
            buildProgram,
            out _);

        var statusRowCatalog = updated.CandidateCatalogs.Single(catalog => catalog.StableId == "root/0");
        statusRowCatalog.Candidates.Select(candidate => candidate.CandidateId).Should().Contain([
            "statusrow-baseline",
            "statusrow-tight-gap",
            "statusrow-compact-cells",
            "statusrow-loose-cells"
        ]);
        statusRowCatalog.Candidates.Single(candidate => candidate.CandidateId == "statusrow-compact-cells")
            .DescendantActions.Select(action => action.StableId).Should().Contain(["root/0/0", "root/0/1"]);
    }

    [Fact]
    public void Scaffold_OnLeafRightAlignedQuantity_CreatesBoundedTextRepairCatalog()
    {
        var visualDocument = CreateLeafQuantityVisualDocument();
        var buildProgram = new UGuiBuildProgram
        {
            DocumentName = visualDocument.DocumentName,
            BackendFamily = visualDocument.BackendFamily,
            SourceGenerationMode = visualDocument.SourceGenerationMode,
            RootStableId = visualDocument.Root.StableId
        };

        var updated = UGuiSubtreeCandidateScaffoldHandler.Scaffold(
            new UGuiSubtreeCandidateScaffoldOptions
            {
                SubtreeStableId = "root/0"
            },
            visualDocument,
            buildProgram,
            out var report);

        report.DirectChildCount.Should().Be(0);
        report.Created.Should().ContainSingle(entry => entry.StableId == "root/0");

        var catalog = updated.CandidateCatalogs.Single(candidateCatalog => candidateCatalog.StableId == "root/0");
        catalog.Candidates.Select(candidate => candidate.CandidateId).Should().Contain([
            "value-baseline",
            "value-nowrap",
            "value-font-down",
            "value-font-up",
            "value-letter-tight",
            "value-letter-loose",
            "value-right-edge-hug"
        ]);
    }

    [Fact]
    public void Scaffold_CreatesValueRowCatalogWithRightEdgeAndMetricRepairCandidates()
    {
        var visualDocument = CreateValueRowVisualDocument();
        var buildProgram = new UGuiBuildProgram
        {
            DocumentName = visualDocument.DocumentName,
            BackendFamily = visualDocument.BackendFamily,
            SourceGenerationMode = visualDocument.SourceGenerationMode,
            RootStableId = visualDocument.Root.StableId
        };

        var updated = UGuiSubtreeCandidateScaffoldHandler.Scaffold(
            new UGuiSubtreeCandidateScaffoldOptions
            {
                SubtreeStableId = "root"
            },
            visualDocument,
            buildProgram,
            out _);

        var catalog = updated.CandidateCatalogs.Single(candidateCatalog => candidateCatalog.StableId == "root/0");
        catalog.Candidates.Select(candidate => candidate.CandidateId).Should().Contain([
            "ingred-row-baseline",
            "ingred-row-tight-gap",
            "ingred-row-row-end-hug",
            "ingred-row-metric-balance"
        ]);
        catalog.Candidates.Single(candidate => candidate.CandidateId == "ingred-row-row-end-hug")
            .DescendantActions.Select(action => action.StableId).Should().Contain("root/0/2");
        catalog.Candidates.Single(candidate => candidate.CandidateId == "ingred-row-metric-balance")
            .DescendantActions.Select(action => action.StableId).Should().Contain(["root/0/0", "root/0/1"]);
    }

    private static VisualDocument CreateVisualDocument()
        => new()
        {
            DocumentName = "PartyStatusStrip",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "root",
                SourceNodeId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                SemanticClass = "surface",
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Vertical
                },
                EdgeContract = DefaultEdgeContract(),
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/0",
                        SourceId = "header",
                        SourceNodeId = "header",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Panel,
                        SemanticClass = "component",
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Panel
                        },
                        EdgeContract = DefaultEdgeContract()
                    },
                    new VisualNode
                    {
                        StableId = "root/1",
                        SourceId = "member-row",
                        SourceNodeId = "member-row",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        SemanticClass = "component",
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container,
                            LayoutType = LayoutType.Horizontal
                        },
                        EdgeContract = DefaultEdgeContract(),
                        Children =
                        [
                            new VisualNode
                            {
                                StableId = "root/1/0",
                                SourceId = "member-a",
                                SourceNodeId = "member-a",
                                Kind = VisualNodeKind.Container,
                                SourceType = ComponentType.Container,
                                SemanticClass = "component",
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Container,
                                    LayoutType = LayoutType.Vertical
                                },
                                EdgeContract = DefaultEdgeContract(),
                                Children =
                                [
                                    new VisualNode
                                    {
                                        StableId = "root/1/0/0",
                                        SourceId = "hero-row",
                                        SourceNodeId = "hero-row",
                                        Kind = VisualNodeKind.Container,
                                        SourceType = ComponentType.Container,
                                        SemanticClass = "motif",
                                        Box = new VisualBox
                                        {
                                            SourceType = ComponentType.Container,
                                            LayoutType = LayoutType.Horizontal
                                        },
                                        EdgeContract = DefaultEdgeContract(),
                                        Children =
                                        [
                                            new VisualNode
                                            {
                                                StableId = "root/1/0/0/0",
                                                SourceId = "portrait",
                                                SourceNodeId = "portrait",
                                                Kind = VisualNodeKind.Container,
                                                SourceType = ComponentType.Panel,
                                                SemanticClass = "atom",
                                                Box = new VisualBox
                                                {
                                                    SourceType = ComponentType.Panel
                                                },
                                                EdgeContract = DefaultEdgeContract()
                                            }
                                        ]
                                    },
                                    new VisualNode
                                    {
                                        StableId = "root/1/0/1",
                                        SourceId = "status-row",
                                        SourceNodeId = "status-row",
                                        Kind = VisualNodeKind.Container,
                                        SourceType = ComponentType.Container,
                                        SemanticClass = "motif",
                                        Box = new VisualBox
                                        {
                                            SourceType = ComponentType.Container,
                                            LayoutType = LayoutType.Horizontal
                                        },
                                        EdgeContract = DefaultEdgeContract(),
                                        Children =
                                        [
                                            new VisualNode
                                            {
                                                StableId = "root/1/0/1/0",
                                                SourceId = "buff-a",
                                                SourceNodeId = "buff-a",
                                                Kind = VisualNodeKind.Container,
                                                SourceType = ComponentType.Panel,
                                                SemanticClass = "atom",
                                                Box = new VisualBox
                                                {
                                                    SourceType = ComponentType.Panel
                                                },
                                                EdgeContract = DefaultEdgeContract()
                                            }
                                        ]
                                    },
                                    new VisualNode
                                    {
                                        StableId = "root/1/0/2",
                                        SourceId = "leaf-label",
                                        SourceNodeId = "leaf-label",
                                        Kind = VisualNodeKind.Text,
                                        SourceType = ComponentType.Label,
                                        SemanticClass = "atom",
                                        Box = new VisualBox
                                        {
                                            SourceType = ComponentType.Label
                                        },
                                        EdgeContract = DefaultEdgeContract()
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

    private static VisualDocument CreateSpecializedVisualDocument()
        => new()
        {
            DocumentName = "PartyStatusStrip",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "root",
                SourceNodeId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                SemanticClass = "surface",
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Vertical
                },
                EdgeContract = DefaultEdgeContract(),
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/0",
                        SourceId = "member-a",
                        SourceNodeId = "member-a",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        SemanticClass = "component",
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container,
                            LayoutType = LayoutType.Vertical
                        },
                        EdgeContract = DefaultEdgeContract(),
                        Children =
                        [
                            new VisualNode
                            {
                                StableId = "root/0/0",
                                SourceId = "HpBar",
                                SourceNodeId = "hp-bar",
                                Kind = VisualNodeKind.Container,
                                SourceType = ComponentType.Container,
                                SemanticClass = "motif",
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Container,
                                    LayoutType = LayoutType.Horizontal
                                },
                                EdgeContract = DefaultEdgeContract(),
                                Children =
                                [
                                    new VisualNode
                                    {
                                        StableId = "root/0/0/0",
                                        SourceId = "HpFill",
                                        SourceNodeId = "hp-fill",
                                        Kind = VisualNodeKind.Container,
                                        SourceType = ComponentType.Panel,
                                        SemanticClass = "atom",
                                        Box = new VisualBox
                                        {
                                            SourceType = ComponentType.Panel
                                        },
                                        EdgeContract = DefaultEdgeContract()
                                    },
                                    new VisualNode
                                    {
                                        StableId = "root/0/0/1",
                                        SourceId = "HpText",
                                        SourceNodeId = "hp-text",
                                        Kind = VisualNodeKind.Text,
                                        SourceType = ComponentType.Label,
                                        SemanticClass = "atom",
                                        Typography = new TypographyContract
                                        {
                                            SemanticClass = "pixel-text",
                                            WrapText = false,
                                            ResolvedFontSize = 9
                                        },
                                        Box = new VisualBox
                                        {
                                            SourceType = ComponentType.Label
                                        },
                                        EdgeContract = DefaultEdgeContract()
                                    }
                                ]
                            },
                            new VisualNode
                            {
                                StableId = "root/0/1",
                                SourceId = "StatusRow",
                                SourceNodeId = "status-row",
                                Kind = VisualNodeKind.Container,
                                SourceType = ComponentType.Container,
                                SemanticClass = "component",
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Container,
                                    LayoutType = LayoutType.Horizontal
                                },
                                EdgeContract = DefaultEdgeContract(),
                                Children =
                                [
                                    new VisualNode
                                    {
                                        StableId = "root/0/1/0",
                                        SourceId = "BuffIcon1",
                                        SourceNodeId = "buff-icon-1",
                                        Kind = VisualNodeKind.Container,
                                        SourceType = ComponentType.Container,
                                        SemanticClass = "motif",
                                        Box = new VisualBox
                                        {
                                            SourceType = ComponentType.Container
                                        },
                                        EdgeContract = DefaultEdgeContract(),
                                        Children =
                                        [
                                            new VisualNode
                                            {
                                                StableId = "root/0/1/0/0",
                                                SourceId = "BuffGlyph1",
                                                SourceNodeId = "buff-glyph-1",
                                                Kind = VisualNodeKind.Icon,
                                                SourceType = ComponentType.Icon,
                                                SemanticClass = "icon-glyph",
                                                Icon = new IconContract
                                                {
                                                    SemanticClass = "icon-glyph",
                                                    BaselineOffset = 0,
                                                    OpticalCentering = true,
                                                    SizeMode = "fit-box",
                                                    ResolvedFontSize = 24
                                                },
                                                Box = new VisualBox
                                                {
                                                    SourceType = ComponentType.Icon
                                                },
                                                EdgeContract = DefaultEdgeContract()
                                            }
                                        ]
                                    },
                                    new VisualNode
                                    {
                                        StableId = "root/0/1/1",
                                        SourceId = "BuffIcon2",
                                        SourceNodeId = "buff-icon-2",
                                        Kind = VisualNodeKind.Container,
                                        SourceType = ComponentType.Container,
                                        SemanticClass = "motif",
                                        Box = new VisualBox
                                        {
                                            SourceType = ComponentType.Container
                                        },
                                        EdgeContract = DefaultEdgeContract(),
                                        Children =
                                        [
                                            new VisualNode
                                            {
                                                StableId = "root/0/1/1/0",
                                                SourceId = "BuffGlyph2",
                                                SourceNodeId = "buff-glyph-2",
                                                Kind = VisualNodeKind.Icon,
                                                SourceType = ComponentType.Icon,
                                                SemanticClass = "icon-glyph",
                                                Icon = new IconContract
                                                {
                                                    SemanticClass = "icon-glyph",
                                                    BaselineOffset = 0,
                                                    OpticalCentering = true,
                                                    SizeMode = "fit-box",
                                                    ResolvedFontSize = 24
                                                },
                                                Box = new VisualBox
                                                {
                                                    SourceType = ComponentType.Icon
                                                },
                                                EdgeContract = DefaultEdgeContract()
                                            }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

    private static VisualDocument CreateWrapperStatusRowVisualDocument()
        => new()
        {
            DocumentName = "PartyStatusStrip",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "root",
                SourceNodeId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                SemanticClass = "surface",
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Horizontal
                },
                EdgeContract = DefaultEdgeContract(),
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/0",
                        SourceId = "StatusRow",
                        SourceNodeId = "status-row",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        SemanticClass = "component",
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container,
                            LayoutType = LayoutType.Horizontal
                        },
                        EdgeContract = DefaultEdgeContract(),
                        Children =
                        [
                            new VisualNode
                            {
                                StableId = "root/0/0",
                                SourceId = "StatusBuff1",
                                SourceNodeId = "status-buff-1",
                                Kind = VisualNodeKind.Container,
                                SourceType = ComponentType.Container,
                                ComponentRefId = "synthetic:buff",
                                SemanticClass = "container",
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Container
                                },
                                EdgeContract = DefaultEdgeContract()
                            },
                            new VisualNode
                            {
                                StableId = "root/0/1",
                                SourceId = "StatusBuff2",
                                SourceNodeId = "status-buff-2",
                                Kind = VisualNodeKind.Container,
                                SourceType = ComponentType.Container,
                                ComponentRefId = "synthetic:buff",
                                SemanticClass = "container",
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Container
                                },
                                EdgeContract = DefaultEdgeContract()
                            }
                        ]
                    }
                ]
            }
        };

    private static VisualDocument CreateLeafQuantityVisualDocument()
        => new()
        {
            DocumentName = "CompactValue",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "root",
                SourceNodeId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                SemanticClass = "surface",
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Horizontal
                },
                EdgeContract = DefaultEdgeContract(),
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/0",
                        SourceId = "value",
                        SourceNodeId = "value",
                        Kind = VisualNodeKind.Text,
                        SourceType = ComponentType.Label,
                        SemanticClass = "right-aligned-quantity",
                        Typography = new TypographyContract
                        {
                            SemanticClass = "right-aligned-quantity",
                            WrapText = true,
                            ResolvedFontSize = 11,
                            ResolvedLetterSpacing = 0
                        },
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Label
                        },
                        EdgeContract = DefaultEdgeContract()
                    }
                ]
            }
        };

    private static VisualDocument CreateValueRowVisualDocument()
        => new()
        {
            DocumentName = "IngredientRow",
            BackendFamily = "ugui",
            SourceGenerationMode = "test",
            Root = new VisualNode
            {
                StableId = "root",
                SourceId = "root",
                SourceNodeId = "root",
                Kind = VisualNodeKind.Container,
                SourceType = ComponentType.Container,
                SemanticClass = "surface",
                Box = new VisualBox
                {
                    SourceType = ComponentType.Container,
                    LayoutType = LayoutType.Vertical
                },
                EdgeContract = DefaultEdgeContract(),
                Children =
                [
                    new VisualNode
                    {
                        StableId = "root/0",
                        SourceId = "ingred-row",
                        SourceNodeId = "ingred-row",
                        Kind = VisualNodeKind.Container,
                        SourceType = ComponentType.Container,
                        SemanticClass = "value-row",
                        Box = new VisualBox
                        {
                            SourceType = ComponentType.Container,
                            LayoutType = LayoutType.Horizontal
                        },
                        EdgeContract = DefaultEdgeContract(),
                        Children =
                        [
                            new VisualNode
                            {
                                StableId = "root/0/0",
                                SourceId = "item-icon",
                                SourceNodeId = "item-icon",
                                Kind = VisualNodeKind.Icon,
                                SourceType = ComponentType.Icon,
                                SemanticClass = "leading-icon",
                                Icon = new IconContract
                                {
                                    SemanticClass = "leading-icon",
                                    BaselineOffset = 0,
                                    OpticalCentering = true,
                                    SizeMode = "fit-box",
                                    ResolvedFontSize = 18
                                },
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Icon
                                },
                                EdgeContract = DefaultEdgeContract()
                            },
                            new VisualNode
                            {
                                StableId = "root/0/1",
                                SourceId = "item-label",
                                SourceNodeId = "item-label",
                                Kind = VisualNodeKind.Text,
                                SourceType = ComponentType.Label,
                                SemanticClass = "compact-label",
                                Typography = new TypographyContract
                                {
                                    SemanticClass = "compact-label",
                                    WrapText = false,
                                    ResolvedFontSize = 11
                                },
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Label
                                },
                                EdgeContract = DefaultEdgeContract()
                            },
                            new VisualNode
                            {
                                StableId = "root/0/2",
                                SourceId = "item-value",
                                SourceNodeId = "item-value",
                                Kind = VisualNodeKind.Text,
                                SourceType = ComponentType.Label,
                                SemanticClass = "right-aligned-quantity",
                                Typography = new TypographyContract
                                {
                                    SemanticClass = "right-aligned-quantity",
                                    WrapText = false,
                                    ResolvedFontSize = 10
                                },
                                Box = new VisualBox
                                {
                                    SourceType = ComponentType.Label
                                },
                                EdgeContract = DefaultEdgeContract()
                            }
                        ]
                    }
                ]
            }
        };

    private static EdgeContract DefaultEdgeContract()
        => new()
        {
            Participation = LayoutParticipation.NormalFlow,
            WidthSizing = AxisSizing.Fixed,
            HeightSizing = AxisSizing.Fixed,
            HorizontalPin = EdgePin.Start,
            VerticalPin = EdgePin.Start,
            OverflowX = OverflowBehavior.Visible,
            OverflowY = OverflowBehavior.Visible,
            WrapPressure = WrapPressurePolicy.Allow
        };
}
