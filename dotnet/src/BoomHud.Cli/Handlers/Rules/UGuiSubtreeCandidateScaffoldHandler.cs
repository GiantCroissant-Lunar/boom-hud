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

        foreach (var child in subtree.Children)
        {
            var solveStage = ClassifySolveStage(child);
            var existingIndex = catalogs.FindIndex(catalog => string.Equals(catalog.StableId, child.StableId, StringComparison.Ordinal));
            if (child.Children.Count == 0)
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

            if (existingIndex >= 0 && !options.OverwriteExistingCatalogs)
            {
                skipped.Add(new UGuiSubtreeCandidateScaffoldEntry
                {
                    StableId = child.StableId,
                    SolveStage = solveStage,
                    Reason = "catalog-exists",
                    CandidateIds = catalogs[existingIndex].Candidates.Select(static candidate => candidate.CandidateId).ToArray()
                });
                continue;
            }

            var catalog = CreateCatalog(child, solveStage);
            if (existingIndex >= 0)
            {
                catalogs[existingIndex] = catalog;
                accepted.RemoveAll(selection => string.Equals(selection.StableId, child.StableId, StringComparison.Ordinal));
                checkpoints.RemoveAll(checkpoint => string.Equals(checkpoint.StableId, child.StableId, StringComparison.Ordinal));
            }
            else
            {
                catalogs.Add(catalog);
            }

            accepted.Add(new UGuiBuildSelection
            {
                StableId = child.StableId,
                CandidateId = catalog.Candidates[0].CandidateId
            });

            if (!checkpoints.Any(checkpoint => string.Equals(checkpoint.StableId, child.StableId, StringComparison.Ordinal)))
            {
                checkpoints.Add(new UGuiBuildCheckpoint
                {
                    Order = checkpoints.Count + 1,
                    StableId = child.StableId,
                    SolveStage = solveStage,
                    LastStepOrder = 0,
                    Purpose = "verify-subtree"
                });
            }

            created.Add(new UGuiSubtreeCandidateScaffoldEntry
            {
                StableId = child.StableId,
                SolveStage = solveStage,
                Reason = existingIndex >= 0 ? "catalog-replaced" : "catalog-created",
                CandidateIds = catalog.Candidates.Select(static candidate => candidate.CandidateId).ToArray()
            });
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

    private static UGuiBuildCandidateCatalog CreateCatalog(VisualNode node, string solveStage)
    {
        if (TryCreateStatBarCatalog(node, solveStage, out var statBarCatalog))
        {
            return statBarCatalog;
        }

        if (TryCreateStatusRowCatalog(node, solveStage, out var statusRowCatalog))
        {
            return statusRowCatalog;
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
