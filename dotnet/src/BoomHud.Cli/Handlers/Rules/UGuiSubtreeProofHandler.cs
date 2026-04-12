using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Cli.Handlers.Baseline;
using BoomHud.Generators.VisualIR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BoomHud.Cli.Handlers.Rules;

public sealed record UGuiSubtreeProofOptions
{
    public FileInfo? VisualIrFile { get; init; }

    public string SubtreeStableId { get; init; } = string.Empty;

    public IReadOnlyList<FileInfo> ActualLayoutFiles { get; init; } = [];

    public IReadOnlyList<string> Labels { get; init; } = [];

    public IReadOnlyList<string> CandidateKeys { get; init; } = [];

    public FileInfo? BuildProgramFile { get; init; }

    public FileInfo? ReferenceImageFile { get; init; }

    public FileInfo? OutFile { get; init; }

    public double PositionTolerance { get; init; } = 0.01d;

    public double SizeTolerance { get; init; } = 0.01d;

    public double FontTolerance { get; init; } = 0.01d;

    public int ImageTolerance { get; init; } = 8;

    public string ImageNormalizeMode { get; init; } = "stretch";

    public bool PrintSummary { get; init; } = true;
}

public sealed record UGuiSubtreeProofReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public required string Version { get; init; }

    public required string DocumentName { get; init; }

    public required string BackendFamily { get; init; }

    public required string SubtreeStableId { get; init; }

    public string? SubtreeSolveStage { get; init; }

    public required IReadOnlyList<UGuiSubtreeProofRun> Runs { get; init; }

    public required UGuiDeterminismProof Determinism { get; init; }

    public required UGuiLocalScoringProof LocalScoring { get; init; }

    public required UGuiCandidateCatalogProof CandidateCatalog { get; init; }

    public required UGuiEmitterConsumptionProof EmitterConsumption { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed record UGuiSubtreeProofRun
{
    public required string Label { get; init; }

    public required string CandidateKey { get; init; }

    public required string ActualLayoutPath { get; init; }

    public required string BackendFamily { get; init; }

    public required string RootStableId { get; init; }

    public string? ReferenceImagePath { get; init; }

    public string? CandidateImagePath { get; init; }

    public required string ScoreMode { get; init; }

    public required string SubtreeLocalPath { get; init; }

    public required int SubtreeNodeCount { get; init; }

    public required int SubtreeIssueCount { get; init; }

    public required double RootScore { get; init; }

    public required double SubtreeScore { get; init; }

    public double? DirectChildAggregateScore { get; init; }

    public double? OutsideSubtreeScore { get; init; }

    public UGuiShellGeometrySnapshot? SubtreeShell { get; init; }

    public UGuiShellGeometrySnapshot? ParentShell { get; init; }

    public IReadOnlyList<UGuiSubtreeMotifScore> DirectChildScores { get; init; } = [];
}

public sealed record UGuiShellGeometrySnapshot
{
    public required string LocalPath { get; init; }

    public required double ActualX { get; init; }

    public required double ActualY { get; init; }

    public required double ActualWidth { get; init; }

    public required double ActualHeight { get; init; }

    public required double ExpectedX { get; init; }

    public required double ExpectedY { get; init; }

    public required double ExpectedWidth { get; init; }

    public required double ExpectedHeight { get; init; }
}

public sealed record UGuiSubtreeMotifScore
{
    public required string StableId { get; init; }

    public string? LocalPath { get; init; }

    public required double Score { get; init; }

    public required int NodeCount { get; init; }
}

public sealed record UGuiDeterminismProof
{
    public required bool IsSatisfied { get; init; }

    public required int RunCount { get; init; }

    public required int UniqueCandidateCount { get; init; }

    public required int RepeatedCandidateGroupCount { get; init; }

    public required bool StructureMatchesAcrossRuns { get; init; }

    public required double MaxPositionDelta { get; init; }

    public required double MaxSizeDelta { get; init; }

    public required double MaxFontDelta { get; init; }

    public required double MaxSubtreeScoreDelta { get; init; }

    public string? Notes { get; init; }
}

public sealed record UGuiLocalScoringProof
{
    public required bool IsSatisfied { get; init; }

    public required double MaxSubtreeScoreDelta { get; init; }

    public double? MaxDirectChildAggregateScoreDelta { get; init; }

    public string PrimarySignal { get; init; } = "subtree";

    public double? SubtreeToRootScoreCorrelation { get; init; }

    public double? DirectChildAggregateToRootScoreCorrelation { get; init; }

    public double? OutsideSubtreeToRootScoreCorrelation { get; init; }

    public string? Notes { get; init; }
}

public sealed record UGuiCandidateCatalogProof
{
    public required bool IsSatisfied { get; init; }

    public required int CandidateCount { get; init; }

    public required bool HasAcceptedCandidate { get; init; }

    public required IReadOnlyList<string> CandidateIds { get; init; }

    public string? AcceptedCandidateId { get; init; }

    public string? Notes { get; init; }
}

public sealed record UGuiEmitterConsumptionProof
{
    public required bool IsSatisfied { get; init; }

    public string? ConsumedCandidateId { get; init; }

    public string? StableId { get; init; }

    public required IReadOnlyList<string> AppliedOverrideDimensions { get; init; }

    public string? Notes { get; init; }
}

public static class UGuiSubtreeProofHandler
{
    public static int Execute(UGuiSubtreeProofOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var report = BuildReport(options);
        var outputPath = options.OutFile?.FullName ?? ResolveDefaultOutputPath(options);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, report.ToJson());

        if (options.PrintSummary)
        {
            PrintSummary(report, outputPath);
        }

        return 0;
    }

    internal static UGuiSubtreeProofReport BuildReport(UGuiSubtreeProofOptions options)
    {
        if (options.VisualIrFile == null || !options.VisualIrFile.Exists)
        {
            throw new FileNotFoundException("Visual IR artifact is required.", options.VisualIrFile?.FullName);
        }

        if (string.IsNullOrWhiteSpace(options.SubtreeStableId))
        {
            throw new InvalidOperationException("Subtree stable id is required.");
        }

        if (options.ActualLayoutFiles.Count == 0)
        {
            throw new InvalidOperationException("At least one actual-layout snapshot is required.");
        }

        foreach (var actualLayoutFile in options.ActualLayoutFiles)
        {
            if (!actualLayoutFile.Exists)
            {
                throw new FileNotFoundException("Actual-layout snapshot not found.", actualLayoutFile.FullName);
            }
        }

        if (options.BuildProgramFile is { Exists: false })
        {
            throw new FileNotFoundException("uGUI build-program artifact not found.", options.BuildProgramFile.FullName);
        }

        if (options.ReferenceImageFile is { Exists: false })
        {
            throw new FileNotFoundException("Reference image not found.", options.ReferenceImageFile.FullName);
        }

        if (NormalizeModeOrNull(options.ImageNormalizeMode) == null)
        {
            throw new InvalidOperationException("Image normalize mode must be one of: off, stretch, cover.");
        }

        var visualDocument = JsonSerializer.Deserialize<VisualDocument>(File.ReadAllText(options.VisualIrFile.FullName))
            ?? throw new InvalidOperationException($"Failed to deserialize Visual IR artifact '{options.VisualIrFile.FullName}'.");
        var buildProgram = options.BuildProgramFile != null
            ? JsonSerializer.Deserialize<UGuiBuildProgram>(File.ReadAllText(options.BuildProgramFile.FullName))
            : null;
        var labels = NormalizeLabels(options.ActualLayoutFiles.Count, options.Labels);
        var candidateKeys = NormalizeCandidateKeys(options.ActualLayoutFiles.Count, options.CandidateKeys);
        var expectedBoundsByStableId = ComputeExpectedBoundsByStableId(visualDocument.Root);
        var visualNodesByStableId = IndexVisualNodesByStableId(visualDocument.Root);
        if (!visualNodesByStableId.TryGetValue(options.SubtreeStableId, out var subtreeVisualNode))
        {
            throw new InvalidOperationException($"Visual IR artifact does not contain subtree stable id '{options.SubtreeStableId}'.");
        }

        var runModels = options.ActualLayoutFiles
            .Select((file, index) => BuildRunModel(
                visualDocument,
                options,
                file,
                labels[index],
                candidateKeys[index],
                expectedBoundsByStableId,
                subtreeVisualNode))
            .ToList();

        var determinism = EvaluateDeterminism(runModels, options.PositionTolerance, options.SizeTolerance, options.FontTolerance);
        var localScoring = EvaluateLocalScoring(runModels);
        var candidateCatalog = EvaluateCandidateCatalog(buildProgram, options.SubtreeStableId);
        var emitterConsumption = EvaluateEmitterConsumption(buildProgram, options.SubtreeStableId);

        return new UGuiSubtreeProofReport
        {
            Version = "1.0",
            DocumentName = visualDocument.DocumentName,
            BackendFamily = runModels[0].Run.BackendFamily,
            SubtreeStableId = options.SubtreeStableId,
            SubtreeSolveStage = buildProgram?.Checkpoints
                .FirstOrDefault(checkpoint => string.Equals(checkpoint.StableId, options.SubtreeStableId, StringComparison.Ordinal))
                ?.SolveStage,
            Runs = runModels.Select(model => model.Run).ToList(),
            Determinism = determinism,
            LocalScoring = localScoring,
            CandidateCatalog = candidateCatalog,
            EmitterConsumption = emitterConsumption
        };
    }

    internal static UGuiDeterminismProof EvaluateDeterminism(
        IReadOnlyList<RunModel> runModels,
        double positionTolerance,
        double sizeTolerance,
        double fontTolerance)
    {
        if (runModels.Count < 2)
        {
            return new UGuiDeterminismProof
            {
                IsSatisfied = false,
                RunCount = runModels.Count,
                UniqueCandidateCount = runModels.Select(static model => model.Run.CandidateKey).Distinct(StringComparer.Ordinal).Count(),
                RepeatedCandidateGroupCount = 0,
                StructureMatchesAcrossRuns = true,
                MaxPositionDelta = 0,
                MaxSizeDelta = 0,
                MaxFontDelta = 0,
                MaxSubtreeScoreDelta = 0,
                Notes = "At least two runs are required to prove deterministic capture and measured-layout stability."
            };
        }

        var repeatedGroups = runModels
            .GroupBy(static model => model.Run.CandidateKey, StringComparer.Ordinal)
            .Where(static group => group.Count() >= 2)
            .ToList();
        if (repeatedGroups.Count == 0)
        {
            return new UGuiDeterminismProof
            {
                IsSatisfied = false,
                RunCount = runModels.Count,
                UniqueCandidateCount = runModels.Select(static model => model.Run.CandidateKey).Distinct(StringComparer.Ordinal).Count(),
                RepeatedCandidateGroupCount = 0,
                StructureMatchesAcrossRuns = true,
                MaxPositionDelta = 0,
                MaxSizeDelta = 0,
                MaxFontDelta = 0,
                MaxSubtreeScoreDelta = 0,
                Notes = "No repeated candidate groups were provided. Determinism requires at least one candidate key with two or more captures."
            };
        }

        var structureMatches = true;
        var maxPositionDelta = 0d;
        var maxSizeDelta = 0d;
        var maxFontDelta = 0d;
        var maxScoreDelta = 0d;

        foreach (var group in repeatedGroups)
        {
            var baseline = group.First();
            foreach (var candidate in group.Skip(1))
            {
                maxScoreDelta = Math.Max(maxScoreDelta, Math.Abs(candidate.Run.SubtreeScore - baseline.Run.SubtreeScore));

                var baselinePaths = baseline.ComparisonsByPath.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
                var candidatePaths = candidate.ComparisonsByPath.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
                if (!baselinePaths.SequenceEqual(candidatePaths, StringComparer.Ordinal))
                {
                    structureMatches = false;
                    continue;
                }

                foreach (var path in baselinePaths)
                {
                    var left = baseline.ComparisonsByPath[path];
                    var right = candidate.ComparisonsByPath[path];
                    maxPositionDelta = Math.Max(maxPositionDelta, Math.Max(Math.Abs(left.ActualX - right.ActualX), Math.Abs(left.ActualY - right.ActualY)));
                    maxSizeDelta = Math.Max(maxSizeDelta, Math.Max(Math.Abs(left.ActualWidth - right.ActualWidth), Math.Abs(left.ActualHeight - right.ActualHeight)));
                    maxFontDelta = Math.Max(maxFontDelta, Math.Abs(left.ActualFontSize - right.ActualFontSize));
                }
            }
        }

        var satisfied = structureMatches
            && maxPositionDelta <= positionTolerance
            && maxSizeDelta <= sizeTolerance
            && maxFontDelta <= fontTolerance;

        return new UGuiDeterminismProof
        {
            IsSatisfied = satisfied,
            RunCount = runModels.Count,
            UniqueCandidateCount = runModels.Select(static model => model.Run.CandidateKey).Distinct(StringComparer.Ordinal).Count(),
            RepeatedCandidateGroupCount = repeatedGroups.Count,
            StructureMatchesAcrossRuns = structureMatches,
            MaxPositionDelta = Math.Round(maxPositionDelta, 4),
            MaxSizeDelta = Math.Round(maxSizeDelta, 4),
            MaxFontDelta = Math.Round(maxFontDelta, 4),
            MaxSubtreeScoreDelta = Math.Round(maxScoreDelta, 4),
            Notes = satisfied
                ? "Repeated captures agree at the subtree level within the configured layout tolerances."
                : "Capture, crop, or layout realization still drifts across repeated runs, so search would optimize noise."
        };
    }

    internal static UGuiLocalScoringProof EvaluateLocalScoring(IReadOnlyList<RunModel> runModels)
    {
        if (runModels.Count == 0)
        {
            throw new InvalidOperationException("At least one run model is required.");
        }

        var candidateAggregates = BuildCandidateAggregates(runModels);
        if (candidateAggregates.Count < 2)
        {
            return new UGuiLocalScoringProof
            {
                IsSatisfied = false,
                MaxSubtreeScoreDelta = 0,
                PrimarySignal = candidateAggregates.Count == 1 && candidateAggregates[0].DirectChildAggregateScore.HasValue
                    ? "direct-child-aggregate"
                    : "subtree",
                Notes = "At least two candidate groups are required to prove that a local scoring signal ranks subtree variants correctly."
            };
        }

        double? subtreeCorrelation = null;
        double? directChildAggregateCorrelation = null;
        double? outsideCorrelation = null;
        var directChildAggregateScores = candidateAggregates.Select(static aggregate => aggregate.DirectChildAggregateScore).ToList();
        var maxDirectChildAggregateScoreDelta = (double?)null;
        var directChildAggregateAvailable = directChildAggregateScores.All(static score => score.HasValue);
        var maxAggregatedSubtreeScoreDelta = candidateAggregates
            .Skip(1)
            .Select(aggregate => Math.Abs(aggregate.SubtreeScore - candidateAggregates[0].SubtreeScore))
            .DefaultIfEmpty(0d)
            .Max();

        subtreeCorrelation = ComputePearsonCorrelation(
            candidateAggregates.Select(static aggregate => aggregate.SubtreeScore).ToArray(),
            candidateAggregates.Select(static aggregate => aggregate.RootScore).ToArray());

        if (directChildAggregateAvailable)
        {
            var baselineAggregate = directChildAggregateScores[0]!.Value;
            maxDirectChildAggregateScoreDelta = directChildAggregateScores
                .Skip(1)
                .Select(score => Math.Abs(score!.Value - baselineAggregate))
                .DefaultIfEmpty(0d)
                .Max();
            directChildAggregateCorrelation = ComputePearsonCorrelation(
                directChildAggregateScores.Select(static score => score!.Value).ToArray(),
                candidateAggregates.Select(static aggregate => aggregate.RootScore).ToArray());
        }

        if (candidateAggregates.All(static aggregate => aggregate.OutsideSubtreeScore.HasValue))
        {
            outsideCorrelation = ComputePearsonCorrelation(
                candidateAggregates.Select(static aggregate => aggregate.OutsideSubtreeScore!.Value).ToArray(),
                candidateAggregates.Select(static aggregate => aggregate.RootScore).ToArray());
        }

        var shellSensitiveCase = directChildAggregateAvailable
            && (maxDirectChildAggregateScoreDelta ?? maxAggregatedSubtreeScoreDelta) <= 0.25d
            && outsideCorrelation is >= 0.5d
            && subtreeCorrelation is < 0.5d;
        var primarySignal = SelectPrimarySignal(
            directChildAggregateAvailable,
            shellSensitiveCase,
            subtreeCorrelation,
            directChildAggregateCorrelation);
        var primaryDelta = primarySignal == "direct-child-aggregate"
            ? maxDirectChildAggregateScoreDelta ?? maxAggregatedSubtreeScoreDelta
            : maxAggregatedSubtreeScoreDelta;
        var primaryCorrelation = primarySignal == "direct-child-aggregate"
            ? directChildAggregateCorrelation
            : subtreeCorrelation;
        var correlationHealthy = primaryCorrelation is >= 0.5d;

        return new UGuiLocalScoringProof
        {
            IsSatisfied = correlationHealthy || shellSensitiveCase,
            MaxSubtreeScoreDelta = Math.Round(maxAggregatedSubtreeScoreDelta, 4),
            MaxDirectChildAggregateScoreDelta = maxDirectChildAggregateScoreDelta is null ? null : Math.Round(maxDirectChildAggregateScoreDelta.Value, 4),
            PrimarySignal = primarySignal,
            SubtreeToRootScoreCorrelation = subtreeCorrelation is null ? null : Math.Round(subtreeCorrelation.Value, 4),
            DirectChildAggregateToRootScoreCorrelation = directChildAggregateCorrelation is null ? null : Math.Round(directChildAggregateCorrelation.Value, 4),
            OutsideSubtreeToRootScoreCorrelation = outsideCorrelation is null ? null : Math.Round(outsideCorrelation.Value, 4),
            Notes = primarySignal switch
            {
                "direct-child-aggregate" when shellSensitiveCase
                    => "Direct child motif scores stayed stable while outside-subtree fidelity improved, which points to a shell or placement change rather than a motif regression.",
                "direct-child-aggregate" when primaryCorrelation is null
                    => "Direct child motif scores did not vary across candidate groups, so additional variants are required to prove that motif-level improvements predict parent improvements.",
                "direct-child-aggregate" when primaryCorrelation >= 0.5d
                    => "Direct child motif scores track with root fidelity across the provided candidate groups.",
                "direct-child-aggregate"
                    => "Direct child motif scores still do not correlate strongly enough with root fidelity to trust hierarchical search.",
                "subtree" when directChildAggregateAvailable && directChildAggregateCorrelation is not null
                    => "Subtree scores are a healthier local signal here; direct child aggregate scores are present but less predictive for this component.",
                _ when primaryCorrelation is null
                    => "Subtree scores did not vary across candidate groups, so additional variants are required to prove that local improvements predict parent improvements.",
                _ when primaryCorrelation >= 0.5d
                    => "Higher subtree scores track with higher root scores across the provided candidate groups.",
                _
                    => "Subtree scoring does not yet correlate strongly enough with root fidelity to trust hierarchical search."
            }
        };
    }

    private static string SelectPrimarySignal(
        bool directChildAggregateAvailable,
        bool shellSensitiveCase,
        double? subtreeCorrelation,
        double? directChildAggregateCorrelation)
    {
        if (!directChildAggregateAvailable)
        {
            return "subtree";
        }

        if (shellSensitiveCase)
        {
            return "direct-child-aggregate";
        }

        var subtreeStrength = subtreeCorrelation is >= 0d ? subtreeCorrelation.Value : double.NegativeInfinity;
        var directChildStrength = directChildAggregateCorrelation is >= 0d ? directChildAggregateCorrelation.Value : double.NegativeInfinity;
        return directChildStrength > subtreeStrength
            ? "direct-child-aggregate"
            : "subtree";
    }

    internal static UGuiCandidateCatalogProof EvaluateCandidateCatalog(UGuiBuildProgram? buildProgram, string stableId)
    {
        if (buildProgram == null)
        {
            return new UGuiCandidateCatalogProof
            {
                IsSatisfied = false,
                CandidateCount = 0,
                HasAcceptedCandidate = false,
                CandidateIds = [],
                Notes = "No replayable uGUI build-program artifact was provided."
            };
        }

        var catalog = buildProgram.CandidateCatalogs.FirstOrDefault(candidateCatalog => string.Equals(candidateCatalog.StableId, stableId, StringComparison.Ordinal));
        if (catalog == null)
        {
            return new UGuiCandidateCatalogProof
            {
                IsSatisfied = false,
                CandidateCount = 0,
                HasAcceptedCandidate = false,
                CandidateIds = [],
                Notes = "The build program does not define a curated candidate catalog for this subtree."
            };
        }

        var candidateIds = catalog.Candidates.Select(static candidate => candidate.CandidateId).ToList();
        var uniqueCandidateIds = new HashSet<string>(candidateIds, StringComparer.Ordinal);
        var accepted = buildProgram.AcceptedCandidates.FirstOrDefault(selection => string.Equals(selection.StableId, stableId, StringComparison.Ordinal));
        var hasAcceptedCandidate = accepted != null && uniqueCandidateIds.Contains(accepted.CandidateId);
        var allCandidatesHaveActions = catalog.Candidates.All(static candidate => HasMeaningfulCandidate(candidate));
        var isSatisfied = catalog.Candidates.Count is >= 2 and <= 6
            && uniqueCandidateIds.Count == candidateIds.Count
            && allCandidatesHaveActions
            && hasAcceptedCandidate;

        return new UGuiCandidateCatalogProof
        {
            IsSatisfied = isSatisfied,
            CandidateCount = catalog.Candidates.Count,
            HasAcceptedCandidate = hasAcceptedCandidate,
            CandidateIds = candidateIds,
            AcceptedCandidateId = accepted?.CandidateId,
            Notes = isSatisfied
                ? "Candidate space is small, explicit, and carries a selected subtree realization."
                : "Candidate catalog is missing, duplicated, oversized, actionless, or lacks an accepted subtree choice."
        };
    }

    internal static UGuiEmitterConsumptionProof EvaluateEmitterConsumption(UGuiBuildProgram? buildProgram, string stableId)
    {
        if (buildProgram == null)
        {
            return new UGuiEmitterConsumptionProof
            {
                IsSatisfied = false,
                StableId = stableId,
                AppliedOverrideDimensions = [],
                Notes = "No build program was provided, so the generator cannot consume an accepted subtree choice."
            };
        }

        var selection = buildProgram.AcceptedCandidates.FirstOrDefault(candidate => string.Equals(candidate.StableId, stableId, StringComparison.Ordinal));
        var catalog = buildProgram.CandidateCatalogs.FirstOrDefault(candidateCatalog => string.Equals(candidateCatalog.StableId, stableId, StringComparison.Ordinal));
        var acceptedCandidate = selection == null
            ? null
            : catalog?.Candidates.FirstOrDefault(candidate => string.Equals(candidate.CandidateId, selection.CandidateId, StringComparison.Ordinal));
        var dimensions = acceptedCandidate == null
            ? []
            : DescribeAppliedDimensions(acceptedCandidate);

        return new UGuiEmitterConsumptionProof
        {
            IsSatisfied = acceptedCandidate != null && dimensions.Count > 0,
            ConsumedCandidateId = acceptedCandidate?.CandidateId,
            StableId = stableId,
            AppliedOverrideDimensions = dimensions,
            Notes = acceptedCandidate == null
                ? "No accepted candidate was found for this subtree."
                : "The experimental uGUI emitter can now consume the accepted subtree candidate by stable id."
        };
    }

    private static RunModel BuildRunModel(
        VisualDocument visualDocument,
        UGuiSubtreeProofOptions options,
        FileInfo actualLayoutFile,
        string label,
        string candidateKey,
        IReadOnlyDictionary<string, LogicalBounds> expectedBoundsByStableId,
        VisualNode subtreeVisualNode)
    {
        var actualLayout = JsonSerializer.Deserialize<ActualLayoutSnapshot>(File.ReadAllText(actualLayoutFile.FullName))
            ?? throw new InvalidOperationException($"Failed to deserialize actual layout snapshot '{actualLayoutFile.FullName}'.");
        var measured = ImageSimilarityHandler.BuildMeasuredLayoutReport(visualDocument, actualLayout);
        var subtreeRoot = measured.Comparisons.FirstOrDefault(comparison => string.Equals(comparison.ExpectedStableId, options.SubtreeStableId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Measured layout report for '{actualLayoutFile.FullName}' does not contain subtree stable id '{options.SubtreeStableId}'.");
        var subtreeLocalPath = subtreeRoot.LocalPath;
        var parentLocalPath = ResolveParentLocalPath(subtreeLocalPath);
        var parentComparison = parentLocalPath == null
            ? null
            : measured.Comparisons.FirstOrDefault(comparison => string.Equals(comparison.LocalPath, parentLocalPath, StringComparison.Ordinal));
        var subtreeComparisons = measured.Comparisons
            .Where(comparison => IsWithinSubtree(subtreeLocalPath, comparison.LocalPath))
            .ToList();
        var subtreeIssues = measured.Issues
            .Where(issue => IsWithinSubtree(subtreeLocalPath, issue.LocalPath))
            .ToList();
        var globalActualBoundsByPath = BuildGlobalActualBoundsByPath(measured.Comparisons);
        var rootScore = ComputeMeasuredScore(measured.Comparisons, measured.Issues);
        var subtreeScore = ComputeMeasuredScore(subtreeComparisons, subtreeIssues);
        var scoreMode = "measured-layout";
        string? candidateImagePath = null;

        if (options.ReferenceImageFile != null)
        {
            var candidateImageFile = ResolveCandidateImageFile(actualLayoutFile);
            var imageScores = ComputeImageScores(
                options.ReferenceImageFile,
                candidateImageFile,
                expectedBoundsByStableId,
                subtreeVisualNode,
                measured.Comparisons,
                subtreeComparisons,
                options.ImageNormalizeMode,
                options.ImageTolerance);
            rootScore = imageScores.RootOverallSimilarityPercent;
            subtreeScore = imageScores.SubtreeOverallSimilarityPercent;
            scoreMode = "image";
            candidateImagePath = candidateImageFile.FullName;

            return new RunModel(
                new UGuiSubtreeProofRun
                {
                    Label = label,
                    CandidateKey = candidateKey,
                    ActualLayoutPath = actualLayoutFile.FullName,
                    BackendFamily = actualLayout.BackendFamily,
                    RootStableId = measured.ExpectedRootStableId,
                    ReferenceImagePath = options.ReferenceImageFile?.FullName,
                    CandidateImagePath = candidateImagePath,
                    ScoreMode = scoreMode,
                    SubtreeLocalPath = subtreeLocalPath,
                    SubtreeNodeCount = subtreeComparisons.Count,
                    SubtreeIssueCount = subtreeIssues.Count,
                    RootScore = rootScore,
                    SubtreeScore = subtreeScore,
                    DirectChildAggregateScore = imageScores.DirectChildAggregateOverallSimilarityPercent,
                    OutsideSubtreeScore = imageScores.OutsideSubtreeOverallSimilarityPercent,
                    SubtreeShell = CreateShellGeometrySnapshot(
                        subtreeRoot,
                        globalActualBoundsByPath,
                        ResolveExpectedBounds(options.SubtreeStableId, expectedBoundsByStableId)),
                    ParentShell = CreateShellGeometrySnapshot(
                        parentComparison,
                        globalActualBoundsByPath,
                        parentComparison == null ? null : ResolveExpectedBounds(parentComparison.ExpectedStableId, expectedBoundsByStableId)),
                    DirectChildScores = imageScores.DirectChildScores
                },
                subtreeComparisons.ToDictionary(static comparison => comparison.LocalPath, StringComparer.Ordinal));
        }

        return new RunModel(
            new UGuiSubtreeProofRun
            {
                Label = label,
                CandidateKey = candidateKey,
                ActualLayoutPath = actualLayoutFile.FullName,
                BackendFamily = actualLayout.BackendFamily,
                RootStableId = measured.ExpectedRootStableId,
                ReferenceImagePath = options.ReferenceImageFile?.FullName,
                CandidateImagePath = candidateImagePath,
                ScoreMode = scoreMode,
                SubtreeLocalPath = subtreeLocalPath,
                SubtreeNodeCount = subtreeComparisons.Count,
                SubtreeIssueCount = subtreeIssues.Count,
                RootScore = rootScore,
                SubtreeScore = subtreeScore
                ,
                SubtreeShell = CreateShellGeometrySnapshot(
                    subtreeRoot,
                    globalActualBoundsByPath,
                    ResolveExpectedBounds(options.SubtreeStableId, expectedBoundsByStableId)),
                ParentShell = CreateShellGeometrySnapshot(
                    parentComparison,
                    globalActualBoundsByPath,
                    parentComparison == null ? null : ResolveExpectedBounds(parentComparison.ExpectedStableId, expectedBoundsByStableId))
            },
            subtreeComparisons.ToDictionary(static comparison => comparison.LocalPath, StringComparer.Ordinal));
    }

    private static string? ResolveParentLocalPath(string localPath)
    {
        var separatorIndex = localPath.LastIndexOf('/');
        if (separatorIndex <= 0)
        {
            return null;
        }

        return localPath[..separatorIndex];
    }

    private static UGuiShellGeometrySnapshot? CreateShellGeometrySnapshot(
        MeasuredLayoutComparison? comparison,
        IReadOnlyDictionary<string, LogicalBounds> globalActualBoundsByPath,
        LogicalBounds? expectedBounds)
        => comparison == null
            ? null
            : new UGuiShellGeometrySnapshot
            {
                LocalPath = comparison.LocalPath,
                ActualX = ResolveGlobalActualValue(comparison.LocalPath, globalActualBoundsByPath, static bounds => bounds.X, comparison.ActualX),
                ActualY = ResolveGlobalActualValue(comparison.LocalPath, globalActualBoundsByPath, static bounds => bounds.Y, comparison.ActualY),
                ActualWidth = ResolveGlobalActualValue(comparison.LocalPath, globalActualBoundsByPath, static bounds => bounds.Width, comparison.ActualWidth),
                ActualHeight = ResolveGlobalActualValue(comparison.LocalPath, globalActualBoundsByPath, static bounds => bounds.Height, comparison.ActualHeight),
                ExpectedX = expectedBounds?.X ?? comparison.ActualX,
                ExpectedY = expectedBounds?.Y ?? comparison.ActualY,
                ExpectedWidth = ResolveExpectedShellWidth(comparison, expectedBounds),
                ExpectedHeight = ResolveExpectedShellHeight(comparison, expectedBounds)
            };

    private static LogicalBounds? ResolveExpectedBounds(
        string stableId,
        IReadOnlyDictionary<string, LogicalBounds> expectedBoundsByStableId)
        => expectedBoundsByStableId.TryGetValue(stableId, out var bounds)
            ? bounds
            : null;

    private static double ResolveGlobalActualValue(
        string localPath,
        IReadOnlyDictionary<string, LogicalBounds> globalActualBoundsByPath,
        Func<LogicalBounds, double> selector,
        double fallback)
        => globalActualBoundsByPath.TryGetValue(localPath, out var bounds)
            ? selector(bounds)
            : fallback;

    private static double ResolveExpectedShellWidth(MeasuredLayoutComparison comparison, LogicalBounds? expectedBounds)
    {
        if (expectedBounds is { Width: > 0d })
        {
            return expectedBounds.Width;
        }

        if (comparison.ExpectedWidthSizing == AxisSizing.Fill
            && comparison.ExpectedAvailableWidth.HasValue
            && comparison.ExpectedAvailableWidth.Value > 0d)
        {
            return comparison.ExpectedAvailableWidth.Value;
        }

        return comparison.ActualWidth;
    }

    private static double ResolveExpectedShellHeight(MeasuredLayoutComparison comparison, LogicalBounds? expectedBounds)
    {
        if (expectedBounds is { Height: > 0d })
        {
            return expectedBounds.Height;
        }

        return comparison.ActualHeight;
    }

    private static ImageScoreSummary ComputeImageScores(
        FileInfo referenceImageFile,
        FileInfo candidateImageFile,
        IReadOnlyDictionary<string, LogicalBounds> expectedBoundsByStableId,
        VisualNode subtreeVisualNode,
        IReadOnlyList<MeasuredLayoutComparison> comparisons,
        IReadOnlyList<MeasuredLayoutComparison> subtreeComparisons,
        string normalizeMode,
        int tolerance)
    {
        using var reference = Image.Load<Rgba32>(referenceImageFile.FullName);
        using var candidate = Image.Load<Rgba32>(candidateImageFile.FullName);
        using var normalizedCandidate = NormalizeCandidateToReference(candidate, reference.Width, reference.Height, normalizeMode);

        var rootMetrics = BaselineCompareHandler.ComputeDiffMetrics(reference, normalizedCandidate, tolerance);
        var rootScore = ImageSimilarityHandler.ComputeOverallSimilarityPercent(rootMetrics);

        if (!expectedBoundsByStableId.TryGetValue("root", out var expectedRootBounds))
        {
            throw new InvalidOperationException("Expected visual bounds do not contain the root node.");
        }

        var globalActualBoundsByPath = BuildGlobalActualBoundsByPath(comparisons);
        var actualRootBounds = ResolveActualBounds("root", comparisons, globalActualBoundsByPath)
            ?? throw new InvalidOperationException("Measured layout comparisons do not contain the root node bounds.");
        var actualSubtreeBounds = ResolveActualBounds(subtreeVisualNode.StableId, comparisons, globalActualBoundsByPath);

        if (!expectedBoundsByStableId.TryGetValue(subtreeVisualNode.StableId, out var expectedSubtreeBounds))
        {
            throw new InvalidOperationException($"Expected visual bounds do not contain subtree stable id '{subtreeVisualNode.StableId}'.");
        }

        var subtreeBounds = ResolveImageBounds(
            expectedRootBounds,
            expectedSubtreeBounds,
            actualRootBounds,
            actualSubtreeBounds,
            reference.Width,
            reference.Height);

        using var referenceCrop = reference.Clone(context => context.Crop(subtreeBounds));
        using var candidateCrop = normalizedCandidate.Clone(context => context.Crop(subtreeBounds));
        var subtreeMetrics = BaselineCompareHandler.ComputeDiffMetrics(referenceCrop, candidateCrop, tolerance);
        var subtreeScore = ImageSimilarityHandler.ComputeOverallSimilarityPercent(subtreeMetrics);

        using var referenceOutsideMask = reference.Clone();
        using var candidateOutsideMask = normalizedCandidate.Clone();
        ApplyMask(referenceOutsideMask, subtreeBounds);
        ApplyMask(candidateOutsideMask, subtreeBounds);
        var outsideMetrics = BaselineCompareHandler.ComputeDiffMetrics(referenceOutsideMask, candidateOutsideMask, tolerance);
        var outsideScore = ImageSimilarityHandler.ComputeOverallSimilarityPercent(outsideMetrics);

        var directChildScores = new List<UGuiSubtreeMotifScore>();
        double weightedScoreSum = 0d;
        double weightedAreaSum = 0d;
        foreach (var child in subtreeVisualNode.Children)
        {
            if (!expectedBoundsByStableId.TryGetValue(child.StableId, out var childBounds))
            {
                continue;
            }

            var actualChildBounds = ResolveActualBounds(child.StableId, comparisons, globalActualBoundsByPath);
            var childImageBounds = ResolveImageBounds(
                expectedRootBounds,
                childBounds,
                actualRootBounds,
                actualChildBounds,
                reference.Width,
                reference.Height);
            using var referenceChildCrop = reference.Clone(context => context.Crop(childImageBounds));
            using var candidateChildCrop = normalizedCandidate.Clone(context => context.Crop(childImageBounds));
            var childMetrics = BaselineCompareHandler.ComputeDiffMetrics(referenceChildCrop, candidateChildCrop, tolerance);
            var childScore = ImageSimilarityHandler.ComputeOverallSimilarityPercent(childMetrics);
            var localPath = subtreeComparisons.FirstOrDefault(comparison => string.Equals(comparison.ExpectedStableId, child.StableId, StringComparison.Ordinal))?.LocalPath;
            var nodeCount = subtreeComparisons.Count(comparison =>
                string.Equals(comparison.ExpectedStableId, child.StableId, StringComparison.Ordinal)
                || comparison.ExpectedStableId.StartsWith(child.StableId + "/", StringComparison.Ordinal));

            directChildScores.Add(new UGuiSubtreeMotifScore
            {
                StableId = child.StableId,
                LocalPath = localPath,
                Score = childScore,
                NodeCount = Math.Max(1, nodeCount)
            });

            var areaWeight = Math.Max(1d, childBounds.Width * childBounds.Height);
            weightedScoreSum += childScore * areaWeight;
            weightedAreaSum += areaWeight;
        }

        var directChildAggregateScore = weightedAreaSum > 0d
            ? Math.Round(weightedScoreSum / weightedAreaSum, 4)
            : (double?)null;

        return new ImageScoreSummary(
            rootScore,
            subtreeScore,
            outsideScore,
            directChildAggregateScore,
            directChildScores);
    }

    internal static double ComputeMeasuredScore(
        IReadOnlyList<MeasuredLayoutComparison> comparisons,
        IReadOnlyList<MeasuredLayoutIssue> issues)
    {
        if (comparisons.Count == 0)
        {
            return 0;
        }

        var issuePenalty = issues.Sum(static issue => issue.Severity switch
        {
            "error" => 18d,
            "warning" => 10d,
            "info" => 4d,
            _ => 6d
        });
        var structurePenalty = comparisons.Sum(static comparison => Math.Abs(comparison.ExpectedChildCount - comparison.ActualChildCount) * 6d);
        var wrapPenalty = comparisons.Sum(static comparison => comparison.ExpectedWrapText == comparison.ActualWrapText ? 0d : 4d);
        var clipPenalty = comparisons.Sum(static comparison => comparison.ExpectedClipContent == comparison.ActualClipContent ? 0d : 4d);
        var scalePenalty = comparisons.Sum(static comparison => (Math.Abs(comparison.ActualScaleX - 1d) + Math.Abs(comparison.ActualScaleY - 1d)) * 8d);
        var fontPenalty = comparisons.Sum(static comparison => comparison.ExpectedFontSize.HasValue && comparison.ActualFontSize > 0
            ? Math.Min(8d, Math.Abs(comparison.ExpectedFontSize.Value - comparison.ActualFontSize) * 2d)
            : 0d);

        var normalizedPenalty = (issuePenalty + structurePenalty + wrapPenalty + clipPenalty + scalePenalty + fontPenalty) / comparisons.Count;
        return Math.Round(Math.Clamp(100d - normalizedPenalty, 0d, 100d), 4);
    }

    private static bool HasMeaningfulCandidate(UGuiBuildCandidate candidate)
        => HasMeaningfulAction(candidate.Action)
           || candidate.DescendantActions.Any(static descendant => HasMeaningfulAction(descendant.Action));

    private static bool HasMeaningfulAction(GeneratorRuleAction action)
        => !string.IsNullOrWhiteSpace(action.ControlType)
           || action.Text is not null
           || action.Icon is not null
           || action.Layout is not null
           || action.Motion is not null;

    private static List<string> DescribeAppliedDimensions(UGuiBuildCandidate candidate)
    {
        var dimensions = new List<string>();
        AppendAppliedDimensions(dimensions, candidate.Action, prefix: null);
        if (candidate.DescendantActions.Count > 0)
        {
            dimensions.Add("descendants");
        }

        return dimensions;
    }

    private static void AppendAppliedDimensions(List<string> dimensions, GeneratorRuleAction action, string? prefix)
    {
        var leading = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix + ":";
        if (!string.IsNullOrWhiteSpace(action.ControlType))
        {
            dimensions.Add(leading + "control-type");
        }

        if (action.Layout is not null)
        {
            dimensions.Add(leading + "layout");
        }

        if (action.Text is not null)
        {
            dimensions.Add(leading + "text");
        }

        if (action.Icon is not null)
        {
            dimensions.Add(leading + "icon");
        }

        if (action.Motion is not null)
        {
            dimensions.Add(leading + "motion");
        }
    }

    private static bool IsWithinSubtree(string subtreeLocalPath, string candidateLocalPath)
        => string.Equals(subtreeLocalPath, candidateLocalPath, StringComparison.Ordinal)
           || candidateLocalPath.StartsWith(subtreeLocalPath + "/", StringComparison.Ordinal);

    private static FileInfo ResolveCandidateImageFile(FileInfo actualLayoutFile)
    {
        var fullPath = actualLayoutFile.FullName;
        string candidatePath;
        if (fullPath.EndsWith(".layout.actual.json", StringComparison.OrdinalIgnoreCase))
        {
            candidatePath = fullPath[..^".layout.actual.json".Length] + ".png";
        }
        else
        {
            candidatePath = Path.ChangeExtension(fullPath, ".png");
        }

        if (!File.Exists(candidatePath))
        {
            throw new FileNotFoundException("Candidate image inferred from actual-layout snapshot was not found.", candidatePath);
        }

        return new FileInfo(candidatePath);
    }

    private static string? NormalizeModeOrNull(string mode)
        => mode.ToLowerInvariant() switch
        {
            "off" => "off",
            "stretch" => "stretch",
            "cover" => "cover",
            _ => null
        };

    private static Image<Rgba32> NormalizeCandidateToReference(Image<Rgba32> candidate, int referenceWidth, int referenceHeight, string mode)
    {
        var normalizedMode = NormalizeModeOrNull(mode)
            ?? throw new InvalidOperationException($"Unsupported image normalize mode '{mode}'.");

        if (string.Equals(normalizedMode, "off", StringComparison.Ordinal))
        {
            if (candidate.Width != referenceWidth || candidate.Height != referenceHeight)
            {
                throw new InvalidOperationException(
                    $"Image normalize mode 'off' requires matching dimensions, but candidate is {candidate.Width}x{candidate.Height} and reference is {referenceWidth}x{referenceHeight}.");
            }

            return candidate.Clone();
        }

        var resizeMode = string.Equals(normalizedMode, "cover", StringComparison.Ordinal)
            ? ResizeMode.Crop
            : ResizeMode.Stretch;
        return candidate.Clone(context => context.Resize(new ResizeOptions
        {
            Size = new Size(referenceWidth, referenceHeight),
            Mode = resizeMode
        }));
    }

    private static Rectangle ResolveImageBounds(
        LogicalBounds expectedRootBounds,
        LogicalBounds expectedNodeBounds,
        LogicalBounds actualRootBounds,
        LogicalBounds? actualNodeBounds,
        int imageWidth,
        int imageHeight)
    {
        var expectedRect = ScaleBoundsToImage(expectedRootBounds, expectedNodeBounds, imageWidth, imageHeight);
        if (actualNodeBounds == null)
        {
            return expectedRect;
        }

        var actualRect = ScaleBoundsToImage(actualRootBounds, actualNodeBounds, imageWidth, imageHeight);
        return UnionRectangles(expectedRect, actualRect, imageWidth, imageHeight);
    }

    private static Rectangle ScaleBoundsToImage(LogicalBounds rootBounds, LogicalBounds subtreeBounds, int imageWidth, int imageHeight)
    {
        var rootWidth = Math.Max(1d, rootBounds.Width);
        var rootHeight = Math.Max(1d, rootBounds.Height);
        var scaleX = imageWidth / rootWidth;
        var scaleY = imageHeight / rootHeight;

        var x = Math.Clamp((int)Math.Round((subtreeBounds.X - rootBounds.X) * scaleX), 0, Math.Max(0, imageWidth - 1));
        var y = Math.Clamp((int)Math.Round((subtreeBounds.Y - rootBounds.Y) * scaleY), 0, Math.Max(0, imageHeight - 1));
        var width = Math.Max(1, (int)Math.Round(subtreeBounds.Width * scaleX));
        var height = Math.Max(1, (int)Math.Round(subtreeBounds.Height * scaleY));

        if (x + width > imageWidth)
        {
            width = imageWidth - x;
        }

        if (y + height > imageHeight)
        {
            height = imageHeight - y;
        }

        return new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
    }

    private static Rectangle UnionRectangles(Rectangle left, Rectangle right, int imageWidth, int imageHeight)
    {
        var x = Math.Max(0, Math.Min(left.X, right.X));
        var y = Math.Max(0, Math.Min(left.Y, right.Y));
        var rightEdge = Math.Min(imageWidth, Math.Max(left.Right, right.Right));
        var bottomEdge = Math.Min(imageHeight, Math.Max(left.Bottom, right.Bottom));
        return new Rectangle(
            x,
            y,
            Math.Max(1, rightEdge - x),
            Math.Max(1, bottomEdge - y));
    }

    private static void ApplyMask(Image<Rgba32> image, Rectangle bounds)
    {
        var maxX = Math.Min(image.Width, bounds.Right);
        var maxY = Math.Min(image.Height, bounds.Bottom);
        for (var y = Math.Max(0, bounds.Y); y < maxY; y++)
        {
            for (var x = Math.Max(0, bounds.X); x < maxX; x++)
            {
                image[x, y] = new Rgba32(0, 0, 0, 0);
            }
        }
    }

    private static Dictionary<string, LogicalBounds> ComputeExpectedBoundsByStableId(VisualNode root)
    {
        var bounds = new Dictionary<string, LogicalBounds>(StringComparer.Ordinal);
        LayoutExpectedNode(root, 0, 0, explicitWidth: null, explicitHeight: null, bounds);
        return bounds;
    }

    private static Dictionary<string, VisualNode> IndexVisualNodesByStableId(VisualNode root)
        => FlattenVisualNodes(root).ToDictionary(static node => node.StableId, StringComparer.Ordinal);

    private static Dictionary<string, LogicalBounds> BuildGlobalActualBoundsByPath(IReadOnlyList<MeasuredLayoutComparison> comparisons)
    {
        var comparisonsByPath = comparisons.ToDictionary(static comparison => comparison.LocalPath, StringComparer.Ordinal);
        var cache = new Dictionary<string, LogicalBounds>(StringComparer.Ordinal);
        foreach (var comparison in comparisons)
        {
            ResolveGlobalActualBounds(comparison.LocalPath, comparisonsByPath, cache);
        }

        return cache;
    }

    private static LogicalBounds? ResolveActualBounds(
        string stableId,
        IReadOnlyList<MeasuredLayoutComparison> comparisons,
        IReadOnlyDictionary<string, LogicalBounds> globalActualBoundsByPath)
    {
        var matches = comparisons
            .Where(comparison =>
                (string.Equals(comparison.ExpectedStableId, stableId, StringComparison.Ordinal)
                || comparison.ExpectedStableId.StartsWith(stableId + "/", StringComparison.Ordinal))
                && globalActualBoundsByPath.ContainsKey(comparison.LocalPath))
            .Select(comparison => globalActualBoundsByPath[comparison.LocalPath])
            .Where(static bounds => bounds.Width > 0d && bounds.Height > 0d)
            .ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        var minX = matches.Min(static bounds => bounds.X);
        var minY = matches.Min(static bounds => bounds.Y);
        var maxX = matches.Max(static bounds => bounds.X + bounds.Width);
        var maxY = matches.Max(static bounds => bounds.Y + bounds.Height);
        return new LogicalBounds(minX, minY, Math.Max(0d, maxX - minX), Math.Max(0d, maxY - minY));
    }

    private static LogicalBounds ResolveGlobalActualBounds(
        string localPath,
        IReadOnlyDictionary<string, MeasuredLayoutComparison> comparisonsByPath,
        IDictionary<string, LogicalBounds> cache)
    {
        if (cache.TryGetValue(localPath, out var cached))
        {
            return cached;
        }

        var comparison = comparisonsByPath[localPath];
        var parentPath = TryGetParentLocalPath(localPath);
        LogicalBounds bounds;
        if (parentPath == null || !comparisonsByPath.ContainsKey(parentPath))
        {
            bounds = new LogicalBounds(
                comparison.ActualX,
                comparison.ActualY,
                comparison.ActualWidth,
                comparison.ActualHeight);
        }
        else
        {
            var parentBounds = ResolveGlobalActualBounds(parentPath, comparisonsByPath, cache);
            bounds = new LogicalBounds(
                parentBounds.X + comparison.ActualX,
                parentBounds.Y + comparison.ActualY,
                comparison.ActualWidth,
                comparison.ActualHeight);
        }

        cache[localPath] = bounds;
        return bounds;
    }

    private static string? TryGetParentLocalPath(string localPath)
    {
        var separatorIndex = localPath.LastIndexOf('/');
        return separatorIndex <= 0 ? null : localPath[..separatorIndex];
    }

    private static IEnumerable<VisualNode> FlattenVisualNodes(VisualNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in FlattenVisualNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static LogicalBounds LayoutExpectedNode(
        VisualNode node,
        double originX,
        double originY,
        double? explicitWidth,
        double? explicitHeight,
        IDictionary<string, LogicalBounds> boundsByStableId)
    {
        var width = explicitWidth ?? ResolveDimensionPixels(node.Box.Width);
        var height = explicitHeight ?? ResolveDimensionPixels(node.Box.Height);
        var nodeBounds = new LogicalBounds(originX, originY, width, height);
        boundsByStableId[node.StableId] = nodeBounds;

        if (node.Children.Count == 0)
        {
            return nodeBounds;
        }

        var padding = node.Box.Padding ?? Spacing.Zero;
        var innerX = originX + padding.Left;
        var innerY = originY + padding.Top;
        var innerWidth = Math.Max(0, width - padding.Left - padding.Right);
        var innerHeight = Math.Max(0, height - padding.Top - padding.Bottom);
        var layoutType = node.Box.LayoutType;

        if (layoutType is not LayoutType.Horizontal and not LayoutType.Vertical)
        {
            foreach (var child in node.Children)
            {
                var childX = innerX + ResolveDimensionPixels(child.Box.Left);
                var childY = innerY + ResolveDimensionPixels(child.Box.Top);
                LayoutExpectedNode(child, childX, childY, explicitWidth: null, explicitHeight: null, boundsByStableId);
            }

            nodeBounds = ExpandBoundsToChildrenIfNeeded(nodeBounds, node, boundsByStableId);
            boundsByStableId[node.StableId] = nodeBounds;
            return nodeBounds;
        }

        var flowChildren = node.Children
            .Where(static child => !child.Box.IsAbsolutePositioned && child.EdgeContract.Participation != LayoutParticipation.Overlay)
            .ToList();
        var gap = layoutType == LayoutType.Horizontal
            ? ResolveGap(node.Box.Gap, horizontal: true)
            : ResolveGap(node.Box.Gap, horizontal: false);
        var fillTotalWeight = flowChildren.Sum(child => ResolveFillWeight(child, layoutType.Value));
        var fixedSpace = flowChildren.Sum(child => ResolveFixedSpace(child, layoutType.Value, innerWidth, innerHeight));
        if (flowChildren.Count > 1)
        {
            fixedSpace += gap * (flowChildren.Count - 1);
        }

        var remainingSpace = Math.Max(0, (layoutType == LayoutType.Horizontal ? innerWidth : innerHeight) - fixedSpace);
        var cursorX = innerX;
        var cursorY = innerY;

        foreach (var child in node.Children)
        {
            if (child.Box.IsAbsolutePositioned || child.EdgeContract.Participation == LayoutParticipation.Overlay)
            {
                var absoluteX = innerX + ResolveDimensionPixels(child.Box.Left);
                var absoluteY = innerY + ResolveDimensionPixels(child.Box.Top);
                LayoutExpectedNode(child, absoluteX, absoluteY, explicitWidth: null, explicitHeight: null, boundsByStableId);
                continue;
            }

            var childWidth = ResolveChildAxisSize(child.Box.Width, layoutType.Value, innerWidth, remainingSpace, fillTotalWeight, child, isWidthAxis: true);
            var childHeight = ResolveChildAxisSize(child.Box.Height, layoutType.Value, innerHeight, remainingSpace, fillTotalWeight, child, isWidthAxis: false);

            if (layoutType == LayoutType.Horizontal)
            {
                childWidth = Math.Max(childWidth, ResolveDimensionPixels(child.Box.Width));
                childHeight = ResolveCrossAxisSize(child.Box.Height, innerHeight);
                if (childHeight <= 0d && child.EdgeContract.HeightSizing == AxisSizing.Hug)
                {
                    childHeight = MeasureContentHeight(child, cursorX, innerY, boundsByStableId);
                }

                LayoutExpectedNode(child, cursorX, innerY, childWidth, childHeight, boundsByStableId);
                cursorX += childWidth + gap;
            }
            else
            {
                childWidth = ResolveCrossAxisSize(child.Box.Width, innerWidth);
                if (childWidth <= 0d && child.EdgeContract.WidthSizing == AxisSizing.Hug)
                {
                    childWidth = MeasureContentWidth(child, innerX, cursorY, boundsByStableId);
                }

                childHeight = Math.Max(childHeight, ResolveDimensionPixels(child.Box.Height));
                if (childHeight <= 0d && child.EdgeContract.HeightSizing == AxisSizing.Hug)
                {
                    childHeight = MeasureContentHeight(child, innerX, cursorY, boundsByStableId);
                }

                LayoutExpectedNode(child, innerX, cursorY, childWidth, childHeight, boundsByStableId);
                cursorY += childHeight + gap;
            }
        }

        nodeBounds = ExpandBoundsToChildrenIfNeeded(nodeBounds, node, boundsByStableId);
        boundsByStableId[node.StableId] = nodeBounds;
        return nodeBounds;
    }

    private static LogicalBounds ExpandBoundsToChildrenIfNeeded(
        LogicalBounds nodeBounds,
        VisualNode node,
        IDictionary<string, LogicalBounds> boundsByStableId)
    {
        var childBounds = node.Children
            .Select(child => boundsByStableId.TryGetValue(child.StableId, out var bounds) ? bounds : null)
            .Where(static bounds => bounds != null)
            .Cast<LogicalBounds>()
            .ToList();
        if (childBounds.Count == 0)
        {
            return nodeBounds;
        }

        var padding = node.Box.Padding ?? Spacing.Zero;
        var maxX = childBounds.Max(static bounds => bounds.X + bounds.Width);
        var maxY = childBounds.Max(static bounds => bounds.Y + bounds.Height);
        var widthFromChildren = Math.Max(0d, maxX - nodeBounds.X + padding.Right);
        var heightFromChildren = Math.Max(0d, maxY - nodeBounds.Y + padding.Bottom);
        var width = ShouldUseContentWidth(node, nodeBounds.Width)
            ? Math.Max(nodeBounds.Width, widthFromChildren)
            : nodeBounds.Width;
        var height = ShouldUseContentHeight(node, nodeBounds.Height)
            ? Math.Max(nodeBounds.Height, heightFromChildren)
            : nodeBounds.Height;

        return nodeBounds with
        {
            Width = width,
            Height = height
        };
    }

    private static bool ShouldUseContentWidth(VisualNode node, double currentWidth)
        => currentWidth <= 0d || node.EdgeContract.WidthSizing == AxisSizing.Hug;

    private static bool ShouldUseContentHeight(VisualNode node, double currentHeight)
        => currentHeight <= 0d || node.EdgeContract.HeightSizing == AxisSizing.Hug;

    private static double MeasureContentWidth(
        VisualNode node,
        double originX,
        double originY,
        IDictionary<string, LogicalBounds> boundsByStableId)
        => MeasureContentBounds(node, originX, originY, boundsByStableId).Width;

    private static double MeasureContentHeight(
        VisualNode node,
        double originX,
        double originY,
        IDictionary<string, LogicalBounds> boundsByStableId)
        => MeasureContentBounds(node, originX, originY, boundsByStableId).Height;

    private static LogicalBounds MeasureContentBounds(
        VisualNode node,
        double originX,
        double originY,
        IDictionary<string, LogicalBounds> boundsByStableId)
    {
        var previewBounds = LayoutExpectedNode(node, originX, originY, explicitWidth: null, explicitHeight: null, boundsByStableId);
        return previewBounds with
        {
            Width = Math.Max(0d, previewBounds.Width),
            Height = Math.Max(0d, previewBounds.Height)
        };
    }

    private static double ResolveChildAxisSize(
        Dimension? dimension,
        LayoutType parentLayoutType,
        double availableAxis,
        double remainingSpace,
        double fillTotalWeight,
        VisualNode child,
        bool isWidthAxis)
    {
        var childLayoutType = isWidthAxis ? LayoutType.Horizontal : LayoutType.Vertical;
        var dimensionPixels = ResolveDimensionPixels(dimension);
        var axisMatchesParent = (parentLayoutType == LayoutType.Horizontal && isWidthAxis)
            || (parentLayoutType == LayoutType.Vertical && !isWidthAxis);
        if (!axisMatchesParent)
        {
            return ResolveCrossAxisSize(dimension, availableAxis);
        }

        return dimension?.Unit switch
        {
            DimensionUnit.Fill or DimensionUnit.Star => fillTotalWeight > 0
                ? remainingSpace * (ResolveFillWeight(child, childLayoutType) / fillTotalWeight)
                : availableAxis,
            _ => dimensionPixels
        };
    }

    private static double ResolveCrossAxisSize(Dimension? dimension, double availableAxis)
        => dimension?.Unit switch
        {
            DimensionUnit.Fill or DimensionUnit.Star => availableAxis,
            _ => ResolveDimensionPixels(dimension)
        };

    private static double ResolveFixedSpace(VisualNode child, LayoutType parentLayoutType, double innerWidth, double innerHeight)
    {
        var dimension = parentLayoutType == LayoutType.Horizontal ? child.Box.Width : child.Box.Height;
        return dimension?.Unit switch
        {
            DimensionUnit.Fill or DimensionUnit.Star => 0,
            _ => ResolveDimensionPixels(dimension)
        };
    }

    private static double ResolveFillWeight(VisualNode child, LayoutType axis)
    {
        var dimension = axis == LayoutType.Horizontal ? child.Box.Width : child.Box.Height;
        var dimensionValue = dimension?.Value ?? 0d;
        return dimension?.Unit switch
        {
            DimensionUnit.Star => Math.Max(1d, dimensionValue),
            DimensionUnit.Fill => Math.Max(1d, child.Box.Weight ?? 1d),
            _ => 0d
        };
    }

    private static double ResolveGap(Spacing? spacing, bool horizontal)
    {
        if (spacing == null)
        {
            return 0;
        }

        return horizontal ? spacing.Value.Left : spacing.Value.Top;
    }

    private static double ResolveDimensionPixels(Dimension? dimension)
    {
        var dimensionValue = dimension?.Value ?? 0d;
        return dimension?.Unit switch
        {
            DimensionUnit.Pixels or DimensionUnit.Cells or DimensionUnit.Percent or DimensionUnit.Star => Math.Max(0d, dimensionValue),
            _ => 0d
        };
    }

    private static string ResolveDefaultOutputPath(UGuiSubtreeProofOptions options)
    {
        var baseDirectory = options.OutFile?.DirectoryName
            ?? options.ActualLayoutFiles[0].DirectoryName
            ?? options.VisualIrFile?.DirectoryName
            ?? Environment.CurrentDirectory;
        return Path.Combine(baseDirectory, $"{options.SubtreeStableId}.ugui-proof.json");
    }

    private static IReadOnlyList<string> NormalizeLabels(int count, IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return Enumerable.Range(1, count)
                .Select(index => $"run-{index}")
                .ToArray();
        }

        if (labels.Count != count)
        {
            throw new InvalidOperationException("When labels are provided, their count must match the number of actual-layout files.");
        }

        return labels;
    }

    private static IReadOnlyList<string> NormalizeCandidateKeys(int count, IReadOnlyList<string> candidateKeys)
    {
        if (candidateKeys.Count == 0)
        {
            return Enumerable.Repeat("candidate-1", count).ToArray();
        }

        if (candidateKeys.Count != count)
        {
            throw new InvalidOperationException("When candidate keys are provided, their count must match the number of actual-layout files.");
        }

        return candidateKeys;
    }

    private static double? ComputePearsonCorrelation(double[] left, double[] right)
    {
        if (left.Length != right.Length || left.Length < 2)
        {
            return null;
        }

        var meanLeft = left.Average();
        var meanRight = right.Average();
        var numerator = 0d;
        var varianceLeft = 0d;
        var varianceRight = 0d;

        for (var index = 0; index < left.Length; index++)
        {
            var centeredLeft = left[index] - meanLeft;
            var centeredRight = right[index] - meanRight;
            numerator += centeredLeft * centeredRight;
            varianceLeft += centeredLeft * centeredLeft;
            varianceRight += centeredRight * centeredRight;
        }

        if (varianceLeft <= 0d || varianceRight <= 0d)
        {
            return null;
        }

        return numerator / Math.Sqrt(varianceLeft * varianceRight);
    }

    private static List<CandidateAggregate> BuildCandidateAggregates(IReadOnlyList<RunModel> runModels)
    {
        var aggregates = new List<CandidateAggregate>();
        foreach (var group in runModels.GroupBy(static model => model.Run.CandidateKey, StringComparer.Ordinal))
        {
            var runs = group.Select(static model => model.Run).ToList();
            var directChildAggregateScore = runs.All(static run => run.DirectChildAggregateScore.HasValue)
                ? runs.Average(static run => run.DirectChildAggregateScore!.Value)
                : (double?)null;
            var outsideSubtreeScore = runs.All(static run => run.OutsideSubtreeScore.HasValue)
                ? runs.Average(static run => run.OutsideSubtreeScore!.Value)
                : (double?)null;

            aggregates.Add(new CandidateAggregate(
                group.Key,
                runs.Average(static run => run.RootScore),
                runs.Average(static run => run.SubtreeScore),
                directChildAggregateScore,
                outsideSubtreeScore));
        }

        return aggregates;
    }

    private static void PrintSummary(UGuiSubtreeProofReport report, string outputPath)
    {
        Console.WriteLine("=== uGUI Subtree Proof ===");
        Console.WriteLine($"Document:             {report.DocumentName}");
        Console.WriteLine($"Subtree stable id:    {report.SubtreeStableId}");
        Console.WriteLine($"Runs:                 {report.Runs.Count}");
        Console.WriteLine($"Candidates:           {report.Determinism.UniqueCandidateCount}");
        Console.WriteLine($"Repeat groups:        {report.Determinism.RepeatedCandidateGroupCount}");
        Console.WriteLine($"Determinism:          {(report.Determinism.IsSatisfied ? "PASS" : "FAIL")}");
        Console.WriteLine($"Local scoring:        {(report.LocalScoring.IsSatisfied ? "PASS" : "FAIL")}");
        Console.WriteLine($"Candidate catalog:    {(report.CandidateCatalog.IsSatisfied ? "PASS" : "FAIL")}");
        Console.WriteLine($"Emitter consumption:  {(report.EmitterConsumption.IsSatisfied ? "PASS" : "FAIL")}");
        Console.WriteLine($"Report:               {outputPath}");
    }

    internal sealed record RunModel(
        UGuiSubtreeProofRun Run,
        IReadOnlyDictionary<string, MeasuredLayoutComparison> ComparisonsByPath);

    private sealed record ImageScoreSummary(
        double RootOverallSimilarityPercent,
        double SubtreeOverallSimilarityPercent,
        double OutsideSubtreeOverallSimilarityPercent,
        double? DirectChildAggregateOverallSimilarityPercent,
        IReadOnlyList<UGuiSubtreeMotifScore> DirectChildScores);

    private sealed record CandidateAggregate(
        string CandidateKey,
        double RootScore,
        double SubtreeScore,
        double? DirectChildAggregateScore,
        double? OutsideSubtreeScore);

    private sealed record LogicalBounds(
        double X,
        double Y,
        double Width,
        double Height);
}
