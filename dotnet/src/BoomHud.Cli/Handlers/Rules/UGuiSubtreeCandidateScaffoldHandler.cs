using System.Text;
using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;

namespace BoomHud.Cli.Handlers.Rules;

public sealed record UGuiSubtreeCandidateScaffoldOptions
{
    public FileInfo? VisualIrFile { get; init; }

    public FileInfo? BuildProgramFile { get; init; }

    public string SubtreeStableId { get; init; } = string.Empty;

    public FileInfo? OutFile { get; init; }

    public FileInfo? ReportOutFile { get; init; }

    public bool OverwriteExistingCatalogs { get; init; }

    public bool PrintSummary { get; init; } = true;
}

public sealed record UGuiSubtreeCandidateScaffoldReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public required string DocumentName { get; init; }

    public required string SubtreeStableId { get; init; }

    public required int DirectChildCount { get; init; }

    public required IReadOnlyList<UGuiSubtreeCandidateScaffoldEntry> Created { get; init; }

    public required IReadOnlyList<UGuiSubtreeCandidateScaffoldEntry> Skipped { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed record UGuiSubtreeCandidateScaffoldEntry
{
    public required string StableId { get; init; }

    public required string SolveStage { get; init; }

    public required string Reason { get; init; }

    public required IReadOnlyList<string> CandidateIds { get; init; }
}

public static class UGuiSubtreeCandidateScaffoldHandler
{
    public static int Execute(UGuiSubtreeCandidateScaffoldOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.VisualIrFile == null || !options.VisualIrFile.Exists)
        {
            throw new FileNotFoundException("Visual IR artifact is required.", options.VisualIrFile?.FullName);
        }

        if (options.BuildProgramFile == null || !options.BuildProgramFile.Exists)
        {
            throw new FileNotFoundException("uGUI build program is required.", options.BuildProgramFile?.FullName);
        }

        if (string.IsNullOrWhiteSpace(options.SubtreeStableId))
        {
            throw new InvalidOperationException("Subtree stable id is required.");
        }

        var visualDocument = JsonSerializer.Deserialize<VisualDocument>(File.ReadAllText(options.VisualIrFile.FullName))
            ?? throw new InvalidOperationException($"Failed to deserialize Visual IR artifact '{options.VisualIrFile.FullName}'.");
        var buildProgram = JsonSerializer.Deserialize<UGuiBuildProgram>(File.ReadAllText(options.BuildProgramFile.FullName))
            ?? throw new InvalidOperationException($"Failed to deserialize build program '{options.BuildProgramFile.FullName}'.");

        var updated = Scaffold(options, visualDocument, buildProgram, out var report);
        var outPath = options.OutFile?.FullName ?? options.BuildProgramFile.FullName;
        EnsureParentDirectory(outPath);
        File.WriteAllText(outPath, GenerationDocumentPreprocessor.ToJson(updated));

        if (options.ReportOutFile != null)
        {
            EnsureParentDirectory(options.ReportOutFile.FullName);
            File.WriteAllText(options.ReportOutFile.FullName, report.ToJson());
        }

        if (options.PrintSummary)
        {
            Console.WriteLine("=== uGUI Subtree Candidate Scaffold ===");
            Console.WriteLine($"Document:             {report.DocumentName}");
            Console.WriteLine($"Subtree stable id:    {report.SubtreeStableId}");
            Console.WriteLine($"Direct children:      {report.DirectChildCount}");
            Console.WriteLine($"Created catalogs:     {report.Created.Count}");
            Console.WriteLine($"Skipped children:     {report.Skipped.Count}");
            Console.WriteLine($"Build program:        {outPath}");
            if (options.ReportOutFile != null)
            {
                Console.WriteLine($"Scaffold report:      {options.ReportOutFile.FullName}");
            }
        }

        return 0;
    }

    internal static UGuiBuildProgram Scaffold(
        UGuiSubtreeCandidateScaffoldOptions options,
        VisualDocument visualDocument,
        UGuiBuildProgram buildProgram,
        out UGuiSubtreeCandidateScaffoldReport report)
    {
        var subtree = FindNodeByStableId(visualDocument.Root, options.SubtreeStableId)
            ?? throw new InvalidOperationException($"Visual IR artifact does not contain subtree stable id '{options.SubtreeStableId}'.");

        var catalogs = buildProgram.CandidateCatalogs.ToList();
        var accepted = buildProgram.AcceptedCandidates.ToList();
        var checkpoints = buildProgram.Checkpoints.ToList();
        var created = new List<UGuiSubtreeCandidateScaffoldEntry>();
        var skipped = new List<UGuiSubtreeCandidateScaffoldEntry>();

        if (subtree.Children.Count == 0 && ShouldCreateCatalogForNode(subtree))
        {
            TryScaffoldNode(options, catalogs, accepted, checkpoints, created, skipped, subtree);
        }

        foreach (var child in subtree.Children)
        {
            var solveStage = ClassifySolveStage(child);
            if (child.Children.Count == 0 && !ShouldCreateCatalogForNode(child))
            {
                skipped.Add(new UGuiSubtreeCandidateScaffoldEntry
                {
                    StableId = child.StableId,
                    SolveStage = solveStage,
                    Reason = "leaf-node",
                    CandidateIds = []
                });
                continue;
            }

            TryScaffoldNode(options, catalogs, accepted, checkpoints, created, skipped, child, solveStage);
        }

        report = new UGuiSubtreeCandidateScaffoldReport
        {
            DocumentName = visualDocument.DocumentName,
            SubtreeStableId = subtree.StableId,
            DirectChildCount = subtree.Children.Count,
            Created = created,
            Skipped = skipped
        };

        return buildProgram with
        {
            CandidateCatalogs = catalogs,
            AcceptedCandidates = accepted,
            Checkpoints = checkpoints
        };
    }

    private static void TryScaffoldNode(
        UGuiSubtreeCandidateScaffoldOptions options,
        List<UGuiBuildCandidateCatalog> catalogs,
        List<UGuiBuildSelection> accepted,
        List<UGuiBuildCheckpoint> checkpoints,
        List<UGuiSubtreeCandidateScaffoldEntry> created,
        List<UGuiSubtreeCandidateScaffoldEntry> skipped,
        VisualNode node,
        string? solveStage = null)
    {
        solveStage ??= ClassifySolveStage(node);
        var existingIndex = catalogs.FindIndex(catalog => string.Equals(catalog.StableId, node.StableId, StringComparison.Ordinal));
        if (existingIndex >= 0 && !options.OverwriteExistingCatalogs)
        {
            skipped.Add(new UGuiSubtreeCandidateScaffoldEntry
            {
                StableId = node.StableId,
                SolveStage = solveStage,
                Reason = "catalog-exists",
                CandidateIds = catalogs[existingIndex].Candidates.Select(static candidate => candidate.CandidateId).ToArray()
            });
            return;
        }

        var catalog = CreateCatalog(node, solveStage);
        if (existingIndex >= 0)
        {
            catalogs[existingIndex] = catalog;
            accepted.RemoveAll(selection => string.Equals(selection.StableId, node.StableId, StringComparison.Ordinal));
            checkpoints.RemoveAll(checkpoint => string.Equals(checkpoint.StableId, node.StableId, StringComparison.Ordinal));
        }
        else
        {
            catalogs.Add(catalog);
        }

        accepted.Add(new UGuiBuildSelection
        {
            StableId = node.StableId,
            CandidateId = catalog.Candidates[0].CandidateId
        });

        checkpoints.Add(new UGuiBuildCheckpoint
        {
            Order = checkpoints.Count + 1,
            StableId = node.StableId,
            SolveStage = solveStage,
            LastStepOrder = 0,
            Purpose = "verify-subtree"
        });

        created.Add(new UGuiSubtreeCandidateScaffoldEntry
        {
            StableId = node.StableId,
            SolveStage = solveStage,
            Reason = existingIndex >= 0 ? "catalog-replaced" : "catalog-created",
            CandidateIds = catalog.Candidates.Select(static candidate => candidate.CandidateId).ToArray()
        });
    }

    private static bool ShouldCreateCatalogForNode(VisualNode node)
        => node.Children.Count > 0
            || node.Typography != null
            || node.Icon != null
            || string.Equals(node.SemanticClass, "value-row", StringComparison.Ordinal)
            || string.Equals(node.SemanticClass, "right-aligned-quantity", StringComparison.Ordinal);

    private static UGuiBuildCandidateCatalog CreateCatalog(VisualNode node, string solveStage)
    {
        if (TryCreateValueRowCatalog(node, solveStage, out var valueRowCatalog))
        {
            return valueRowCatalog;
        }

        if (TryCreateStatBarCatalog(node, solveStage, out var statBarCatalog))
        {
            return statBarCatalog;
        }

        if (TryCreateStatusRowCatalog(node, solveStage, out var statusRowCatalog))
        {
            return statusRowCatalog;
        }

        if (TryCreateCompactTextCatalog(node, solveStage, out var compactTextCatalog))
        {
            return compactTextCatalog;
        }

        if (TryCreateCompactIconCatalog(node, solveStage, out var compactIconCatalog))
        {
            return compactIconCatalog;
        }

        return CreateGenericCatalog(node, solveStage);
    }

    private static UGuiBuildCandidateCatalog CreateGenericCatalog(VisualNode node, string solveStage)
    {
        var prefix = BuildCandidatePrefix(node);
        var controlType = InferControlType(node);
        var candidates = new List<UGuiBuildCandidate>
        {
            new()
            {
                CandidateId = $"{prefix}-baseline",
                Label = "Baseline",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType
                }
            },
            new()
            {
                CandidateId = $"{prefix}-content-hug",
                Label = "Content hug",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType,
                    Layout = new GeneratorLayoutRuleAction
                    {
                        PreferContentWidth = true,
                        PreferContentHeight = true
                    }
                }
            }
        };

        if (node.Box.LayoutType is LayoutType.Horizontal or LayoutType.Vertical or LayoutType.Stack)
        {
            candidates.Add(new UGuiBuildCandidate
            {
                CandidateId = $"{prefix}-tight-layout",
                Label = "Tight layout",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType,
                    Layout = new GeneratorLayoutRuleAction
                    {
                        PaddingDelta = -4,
                        GapDelta = -4
                    }
                }
            });
        }

        if (candidates.Count > 6)
        {
            candidates = candidates.Take(6).ToList();
        }

        return new UGuiBuildCandidateCatalog
        {
            StableId = node.StableId,
            SolveStage = solveStage,
            Candidates = candidates
        };
    }

    private static bool TryCreateStatBarCatalog(VisualNode node, string solveStage, out UGuiBuildCandidateCatalog catalog)
    {
        catalog = null!;
        if (!TryFindStatBarParts(node, out var fillNode, out var textNode))
        {
            return false;
        }

        var prefix = BuildCandidatePrefix(node);
        var controlType = InferControlType(node);
        catalog = new UGuiBuildCandidateCatalog
        {
            StableId = node.StableId,
            SolveStage = solveStage,
            Candidates =
            [
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-baseline",
                    Label = "Baseline",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    }
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-fill-text-nudge",
                    Label = "Fill plus text nudge",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    },
                    DescendantActions =
                    [
                        new UGuiBuildDescendantAction
                        {
                            StableId = fillNode.StableId,
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    PreferredWidthDelta = 24
                                }
                            }
                        },
                        new UGuiBuildDescendantAction
                        {
                            StableId = textNode.StableId,
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    OffsetXDelta = 4,
                                    OffsetYDelta = 1
                                },
                                Text = new GeneratorTextRuleAction
                                {
                                    FontSizeDelta = 1
                                }
                            }
                        }
                    ]
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-text-tight",
                    Label = "Text tight",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    },
                    DescendantActions =
                    [
                        new UGuiBuildDescendantAction
                        {
                            StableId = textNode.StableId,
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    OffsetXDelta = -2
                                },
                                Text = new GeneratorTextRuleAction
                                {
                                    FontSizeDelta = -1
                                }
                            }
                        }
                    ]
                }
            ]
        };

        return true;
    }

    private static bool TryCreateValueRowCatalog(VisualNode node, string solveStage, out UGuiBuildCandidateCatalog catalog)
    {
        catalog = null!;
        if (!string.Equals(node.SemanticClass, "value-row", StringComparison.Ordinal))
        {
            return false;
        }

        var quantityStableIds = CollectSemanticStableIds(node, "right-aligned-quantity").ToArray();
        var compactTextStableIds = CollectCompactTextStableIds(node).ToArray();
        var iconStableIds = CollectIconStableIds(node).ToArray();
        if (quantityStableIds.Length == 0 && compactTextStableIds.Length == 0 && iconStableIds.Length == 0)
        {
            return false;
        }

        var prefix = BuildCandidatePrefix(node);
        var controlType = InferControlType(node);
        catalog = new UGuiBuildCandidateCatalog
        {
            StableId = node.StableId,
            SolveStage = solveStage,
            Candidates =
            [
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-baseline",
                    Label = "Baseline",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    }
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-tight-gap",
                    Label = "Tight gap",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType,
                        Layout = new GeneratorLayoutRuleAction
                        {
                            GapDelta = -2
                        }
                    }
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-row-end-hug",
                    Label = "Row end hug",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    },
                    DescendantActions = quantityStableIds
                        .Select(stableId => new UGuiBuildDescendantAction
                        {
                            StableId = stableId,
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    PreferContentWidth = true,
                                    PreferContentHeight = true
                                },
                                Text = new GeneratorTextRuleAction
                                {
                                    WrapText = false
                                }
                            }
                        })
                        .ToArray()
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-metric-balance",
                    Label = "Metric balance",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType,
                        Layout = new GeneratorLayoutRuleAction
                        {
                            GapDelta = -2
                        }
                    },
                    DescendantActions = compactTextStableIds
                        .Select(stableId => new UGuiBuildDescendantAction
                        {
                            StableId = stableId,
                            Action = new GeneratorRuleAction
                            {
                                Text = new GeneratorTextRuleAction
                                {
                                    FontSizeDelta = -1,
                                    LetterSpacingDelta = -0.25
                                }
                            }
                        })
                        .Concat(iconStableIds.Select(stableId => new UGuiBuildDescendantAction
                        {
                            StableId = stableId,
                            Action = new GeneratorRuleAction
                            {
                                Icon = new GeneratorIconRuleAction
                                {
                                    BaselineOffsetDelta = -1
                                }
                            }
                        }))
                        .ToArray()
                }
            ]
        };

        return true;
    }

    private static bool TryCreateCompactTextCatalog(VisualNode node, string solveStage, out UGuiBuildCandidateCatalog catalog)
    {
        catalog = null!;
        if (node.Kind != VisualNodeKind.Text || node.Typography == null)
        {
            return false;
        }

        var prefix = BuildCandidatePrefix(node);
        var controlType = InferControlType(node);
        var candidates = new List<UGuiBuildCandidate>
        {
            new()
            {
                CandidateId = $"{prefix}-baseline",
                Label = "Baseline",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType
                }
            },
            new()
            {
                CandidateId = $"{prefix}-{(node.Typography.WrapText ? "nowrap" : "wrap")}",
                Label = node.Typography.WrapText ? "No wrap" : "Wrap",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType,
                    Text = new GeneratorTextRuleAction
                    {
                        WrapText = !node.Typography.WrapText
                    }
                }
            },
            new()
            {
                CandidateId = $"{prefix}-font-down",
                Label = "Font down",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType,
                    Text = new GeneratorTextRuleAction
                    {
                        FontSizeDelta = -1
                    }
                }
            },
            new()
            {
                CandidateId = $"{prefix}-font-up",
                Label = "Font up",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType,
                    Text = new GeneratorTextRuleAction
                    {
                        FontSizeDelta = 1
                    }
                }
            },
            new()
            {
                CandidateId = $"{prefix}-letter-tight",
                Label = "Letter tight",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType,
                    Text = new GeneratorTextRuleAction
                    {
                        LetterSpacingDelta = -0.25
                    }
                }
            },
            new()
            {
                CandidateId = $"{prefix}-letter-loose",
                Label = "Letter loose",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType,
                    Text = new GeneratorTextRuleAction
                    {
                        LetterSpacingDelta = 0.25
                    }
                }
            }
        };

        if (string.Equals(node.SemanticClass, "right-aligned-quantity", StringComparison.Ordinal))
        {
            candidates.Add(new UGuiBuildCandidate
            {
                CandidateId = $"{prefix}-right-edge-hug",
                Label = "Right edge hug",
                Action = new GeneratorRuleAction
                {
                    ControlType = controlType,
                    Layout = new GeneratorLayoutRuleAction
                    {
                        PreferContentWidth = true,
                        PreferContentHeight = true
                    },
                    Text = new GeneratorTextRuleAction
                    {
                        WrapText = false
                    }
                }
            });
        }

        catalog = new UGuiBuildCandidateCatalog
        {
            StableId = node.StableId,
            SolveStage = solveStage,
            Candidates = candidates
        };

        return true;
    }

    private static bool TryCreateCompactIconCatalog(VisualNode node, string solveStage, out UGuiBuildCandidateCatalog catalog)
    {
        catalog = null!;
        if (node.Kind != VisualNodeKind.Icon && node.Icon == null)
        {
            return false;
        }

        var prefix = BuildCandidatePrefix(node);
        var controlType = InferControlType(node);
        catalog = new UGuiBuildCandidateCatalog
        {
            StableId = node.StableId,
            SolveStage = solveStage,
            Candidates =
            [
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-baseline",
                    Label = "Baseline",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    }
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-baseline-up",
                    Label = "Baseline up",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType,
                        Icon = new GeneratorIconRuleAction
                        {
                            BaselineOffsetDelta = -1
                        }
                    }
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-baseline-down",
                    Label = "Baseline down",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType,
                        Icon = new GeneratorIconRuleAction
                        {
                            BaselineOffsetDelta = 1
                        }
                    }
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-optical-center",
                    Label = "Optical center",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType,
                        Icon = new GeneratorIconRuleAction
                        {
                            OpticalCentering = true
                        }
                    }
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-compact",
                    Label = "Compact",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType,
                        Icon = new GeneratorIconRuleAction
                        {
                            FontSizeDelta = -1
                        }
                    }
                }
            ]
        };

        return true;
    }

    private static bool TryCreateStatusRowCatalog(VisualNode node, string solveStage, out UGuiBuildCandidateCatalog catalog)
    {
        catalog = null!;
        if (!IsStatusRow(node))
        {
            return false;
        }

        var prefix = BuildCandidatePrefix(node);
        var controlType = InferControlType(node);
        var iconStableIds = CollectIconStableIds(node).ToArray();
        if (iconStableIds.Length == 0)
        {
            var wrapperStableIds = node.Children.Select(static child => child.StableId).ToArray();
            if (wrapperStableIds.Length == 0)
            {
                return false;
            }

            var wrapperCandidates = new List<UGuiBuildCandidate>
            {
                new()
                {
                    CandidateId = $"{prefix}-baseline",
                    Label = "Baseline",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    }
                },
                new()
                {
                    CandidateId = $"{prefix}-tight-gap",
                    Label = "Tight gap",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType,
                        Layout = new GeneratorLayoutRuleAction
                        {
                            GapDelta = -2
                        }
                    }
                },
                new()
                {
                    CandidateId = $"{prefix}-compact-cells",
                    Label = "Compact cells",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    },
                    DescendantActions = wrapperStableIds
                        .Select(stableId => new UGuiBuildDescendantAction
                        {
                            StableId = stableId,
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    PreferredWidthDelta = -4,
                                    PreferredHeightDelta = -4
                                }
                            }
                        })
                        .ToArray()
                },
                new()
                {
                    CandidateId = $"{prefix}-loose-cells",
                    Label = "Loose cells",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    },
                    DescendantActions = wrapperStableIds
                        .Select(stableId => new UGuiBuildDescendantAction
                        {
                            StableId = stableId,
                            Action = new GeneratorRuleAction
                            {
                                Layout = new GeneratorLayoutRuleAction
                                {
                                    PreferredWidthDelta = 4,
                                    PreferredHeightDelta = 4
                                }
                            }
                        })
                        .ToArray()
                }
            };

            catalog = new UGuiBuildCandidateCatalog
            {
                StableId = node.StableId,
                SolveStage = solveStage,
                Candidates = wrapperCandidates
            };

            return true;
        }

        catalog = new UGuiBuildCandidateCatalog
        {
            StableId = node.StableId,
            SolveStage = solveStage,
            Candidates =
            [
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-baseline",
                    Label = "Baseline",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    }
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-tight-gap",
                    Label = "Tight gap",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType,
                        Layout = new GeneratorLayoutRuleAction
                        {
                            GapDelta = -2
                        }
                    }
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-icon-baseline-up",
                    Label = "Icon baseline up",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType
                    },
                    DescendantActions = iconStableIds
                        .Select(stableId => new UGuiBuildDescendantAction
                        {
                            StableId = stableId,
                            Action = new GeneratorRuleAction
                            {
                                Icon = new GeneratorIconRuleAction
                                {
                                    BaselineOffsetDelta = -1
                                }
                            }
                        })
                        .ToArray()
                },
                new UGuiBuildCandidate
                {
                    CandidateId = $"{prefix}-compact-icons",
                    Label = "Compact icons",
                    Action = new GeneratorRuleAction
                    {
                        ControlType = controlType,
                        Layout = new GeneratorLayoutRuleAction
                        {
                            GapDelta = -2
                        }
                    },
                    DescendantActions = iconStableIds
                        .Select(stableId => new UGuiBuildDescendantAction
                        {
                            StableId = stableId,
                            Action = new GeneratorRuleAction
                            {
                                Icon = new GeneratorIconRuleAction
                                {
                                    FontSizeDelta = -1
                                }
                            }
                        })
                        .ToArray()
                }
            ]
        };

        return true;
    }

    private static string BuildCandidatePrefix(VisualNode node)
    {
        var seed = node.SourceId
                   ?? node.SourceNodeId
                   ?? node.StableId.Split('/').LastOrDefault()
                   ?? node.StableId;
        var builder = new StringBuilder(seed.Length);
        var previousDash = false;
        foreach (var ch in seed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                previousDash = false;
            }
            else if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        var value = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(value)
            ? node.StableId.Replace('/', '-')
            : value;
    }

    private static string InferControlType(VisualNode node)
        => node.Kind switch
        {
            VisualNodeKind.Text => "Text",
            VisualNodeKind.Icon => "Text",
            VisualNodeKind.Image => "Image",
            VisualNodeKind.Interactive => "Button",
            VisualNodeKind.Value => "Slider",
            _ => "Container"
        };

    private static bool TryFindStatBarParts(VisualNode node, out VisualNode fillNode, out VisualNode textNode)
    {
        fillNode = null!;
        textNode = null!;

        var sourceId = node.SourceId ?? string.Empty;
        var nameLooksLikeBar = sourceId.Contains("bar", StringComparison.OrdinalIgnoreCase)
            || sourceId.Contains("hp", StringComparison.OrdinalIgnoreCase)
            || sourceId.Contains("mp", StringComparison.OrdinalIgnoreCase);
        if (!nameLooksLikeBar)
        {
            return false;
        }

        fillNode = node.Children.FirstOrDefault(child =>
            child.SourceId?.Contains("fill", StringComparison.OrdinalIgnoreCase) ?? false)
            ?? null!;
        textNode = node.Children.FirstOrDefault(static child => child.Kind == VisualNodeKind.Text)
            ?? null!;

        return fillNode != null && textNode != null;
    }

    private static bool IsStatusRow(VisualNode node)
    {
        var sourceId = node.SourceId ?? string.Empty;
        if (sourceId.Contains("statusrow", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return node.Children.Count >= 3 && node.Children.All(child => CollectIconStableIds(child).Any());
    }

    private static IEnumerable<string> CollectIconStableIds(VisualNode node)
    {
        if (node.Kind == VisualNodeKind.Icon || node.Icon != null || (node.SemanticClass?.Contains("icon", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            yield return node.StableId;
        }

        foreach (var child in node.Children)
        {
            foreach (var stableId in CollectIconStableIds(child))
            {
                yield return stableId;
            }
        }
    }

    private static IEnumerable<string> CollectSemanticStableIds(VisualNode node, string semanticClass)
    {
        if (string.Equals(node.SemanticClass, semanticClass, StringComparison.Ordinal))
        {
            yield return node.StableId;
        }

        foreach (var child in node.Children)
        {
            foreach (var stableId in CollectSemanticStableIds(child, semanticClass))
            {
                yield return stableId;
            }
        }
    }

    private static IEnumerable<string> CollectCompactTextStableIds(VisualNode node)
    {
        if (node.Kind == VisualNodeKind.Text
            && node.SemanticClass is "compact-label" or "compact-numeric-readout" or "tab-label" or "button-label" or "right-aligned-quantity")
        {
            yield return node.StableId;
        }

        foreach (var child in node.Children)
        {
            foreach (var stableId in CollectCompactTextStableIds(child))
            {
                yield return stableId;
            }
        }
    }

    private static string ClassifySolveStage(VisualNode node)
    {
        if (node.Children.Count == 0)
        {
            return "atom";
        }

        var count = CountNodes(node);
        if (count <= 4)
        {
            return "motif";
        }

        if (count <= 12)
        {
            return "component";
        }

        return "surface";
    }

    private static int CountNodes(VisualNode node)
    {
        var count = 1;
        foreach (var child in node.Children)
        {
            count += CountNodes(child);
        }

        return count;
    }

    private static VisualNode? FindNodeByStableId(VisualNode root, string stableId)
    {
        if (string.Equals(root.StableId, stableId, StringComparison.Ordinal))
        {
            return root;
        }

        foreach (var child in root.Children)
        {
            var match = FindNodeByStableId(child, stableId);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
