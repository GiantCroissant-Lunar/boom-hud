using System.Text.Json;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;

namespace BoomHud.Cli.Handlers.Rules;

public sealed record UGuiSubtreeCandidateSelectionOptions
{
    public FileInfo? ProofReportFile { get; init; }

    public FileInfo? BuildProgramFile { get; init; }

    public FileInfo? OutFile { get; init; }

    public FileInfo? DecisionOutFile { get; init; }

    public string? SubtreeStableId { get; init; }

    public string? BaselineCandidateId { get; init; }

    public IReadOnlyList<string> CandidateIdMappings { get; init; } = [];

    public double PrimaryDropTolerance { get; init; } = 0.25d;

    public double ImprovementEpsilon { get; init; } = 0.05d;

    public double ShellDriftTolerance { get; init; } = 0.5d;

    public bool PrintSummary { get; init; } = true;
}

public sealed record UGuiSubtreeCandidateSelectionDecision
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public required string DocumentName { get; init; }

    public required string SubtreeStableId { get; init; }

    public required string PrimarySignal { get; init; }

    public required string BaselineCandidateId { get; init; }

    public required string SelectedCandidateId { get; init; }

    public required bool ChangedSelection { get; init; }

    public required double PrimaryDropTolerance { get; init; }

    public required double ImprovementEpsilon { get; init; }

    public required double ShellDriftTolerance { get; init; }

    public required IReadOnlyList<UGuiSubtreeCandidateEvaluation> Candidates { get; init; }

    public required string Notes { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed record UGuiSubtreeCandidateEvaluation
{
    public required string CandidateId { get; init; }

    public required string SourceKey { get; init; }

    public required int RunCount { get; init; }

    public required double RootScore { get; init; }

    public required double SubtreeScore { get; init; }

    public double? DirectChildAggregateScore { get; init; }

    public double? OutsideSubtreeScore { get; init; }

    public required double PrimaryScore { get; init; }

    public required double PrimaryDropFromBaseline { get; init; }

    public required double RootGainFromBaseline { get; init; }

    public double? OutsideGainFromBaseline { get; init; }

    public double? ShellDriftFromBaseline { get; init; }

    public double? ParentShellDriftFromBaseline { get; init; }

    public double? ShellDriftFromExpected { get; init; }

    public double? ParentShellDriftFromExpected { get; init; }

    public required bool PassesPrimaryGate { get; init; }

    public required bool PassesImprovementGate { get; init; }

    public required bool PassesShellGate { get; init; }

    public required bool IsBaseline { get; init; }

    public required bool IsSelected { get; init; }
}

public static class UGuiSubtreeCandidateSelectionHandler
{
    public static int Execute(UGuiSubtreeCandidateSelectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.ProofReportFile == null || !options.ProofReportFile.Exists)
        {
            throw new FileNotFoundException("Grouped proof report is required.", options.ProofReportFile?.FullName);
        }

        if (options.BuildProgramFile == null || !options.BuildProgramFile.Exists)
        {
            throw new FileNotFoundException("uGUI build program is required.", options.BuildProgramFile?.FullName);
        }

        var proofReport = JsonSerializer.Deserialize<UGuiSubtreeProofReport>(File.ReadAllText(options.ProofReportFile.FullName))
            ?? throw new InvalidOperationException($"Failed to deserialize proof report '{options.ProofReportFile.FullName}'.");
        var buildProgram = JsonSerializer.Deserialize<UGuiBuildProgram>(File.ReadAllText(options.BuildProgramFile.FullName))
            ?? throw new InvalidOperationException($"Failed to deserialize build program '{options.BuildProgramFile.FullName}'.");

        var decision = BuildDecision(options, proofReport, buildProgram);
        var updatedBuildProgram = ApplySelection(buildProgram, decision.SubtreeStableId, decision.SelectedCandidateId);

        var outPath = options.OutFile?.FullName ?? options.BuildProgramFile.FullName;
        EnsureParentDirectory(outPath);
        File.WriteAllText(outPath, GenerationDocumentPreprocessor.ToJson(updatedBuildProgram));

        if (options.DecisionOutFile != null)
        {
            EnsureParentDirectory(options.DecisionOutFile.FullName);
            File.WriteAllText(options.DecisionOutFile.FullName, decision.ToJson());
        }

        if (options.PrintSummary)
        {
            Console.WriteLine("=== uGUI Subtree Candidate Selection ===");
            Console.WriteLine($"Document:             {decision.DocumentName}");
            Console.WriteLine($"Subtree stable id:    {decision.SubtreeStableId}");
            Console.WriteLine($"Primary signal:       {decision.PrimarySignal}");
            Console.WriteLine($"Baseline candidate:   {decision.BaselineCandidateId}");
            Console.WriteLine($"Selected candidate:   {decision.SelectedCandidateId}");
            Console.WriteLine($"Selection changed:    {(decision.ChangedSelection ? "YES" : "NO")}");
            Console.WriteLine($"Build program:        {outPath}");
            if (options.DecisionOutFile != null)
            {
                Console.WriteLine($"Decision report:      {options.DecisionOutFile.FullName}");
            }
        }

        return 0;
    }

    internal static UGuiSubtreeCandidateSelectionDecision BuildDecision(
        UGuiSubtreeCandidateSelectionOptions options,
        UGuiSubtreeProofReport report,
        UGuiBuildProgram buildProgram)
    {
        var stableId = options.SubtreeStableId ?? report.SubtreeStableId;
        if (string.IsNullOrWhiteSpace(stableId))
        {
            throw new InvalidOperationException("Subtree stable id is required.");
        }

        var catalog = buildProgram.CandidateCatalogs.FirstOrDefault(candidateCatalog => string.Equals(candidateCatalog.StableId, stableId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Build program does not define a candidate catalog for subtree '{stableId}'.");
        var candidateIdByKey = BuildCandidateIdByKey(options.CandidateIdMappings);
        var catalogCandidateIds = new HashSet<string>(catalog.Candidates.Select(static candidate => candidate.CandidateId), StringComparer.Ordinal);
        var candidateGroups = report.Runs
            .GroupBy(run => ResolveCandidateId(run.CandidateKey, candidateIdByKey), StringComparer.Ordinal)
            .Where(group => catalogCandidateIds.Contains(group.Key))
            .Select(group => new CandidateAggregate(
                group.Key,
                group.First().CandidateKey,
                group.Count(),
                group.Average(static run => run.RootScore),
                group.Average(static run => run.SubtreeScore),
                group.All(static run => run.DirectChildAggregateScore.HasValue) ? group.Average(static run => run.DirectChildAggregateScore!.Value) : null,
                group.All(static run => run.OutsideSubtreeScore.HasValue) ? group.Average(static run => run.OutsideSubtreeScore!.Value) : null,
                AverageShellGeometry(group.Select(static run => run.SubtreeShell).ToList()),
                AverageShellGeometry(group.Select(static run => run.ParentShell).ToList())))
            .ToList();

        if (candidateGroups.Count == 0)
        {
            throw new InvalidOperationException("Proof report did not contain any candidate groups that match the build-program catalog.");
        }

        var baselineCandidateId = ResolveBaselineCandidateId(options, buildProgram, stableId, candidateGroups);
        var baseline = candidateGroups.FirstOrDefault(group => string.Equals(group.CandidateId, baselineCandidateId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Baseline candidate '{baselineCandidateId}' was not found in the grouped proof report.");
        var primarySignal = report.LocalScoring.PrimarySignal;
        var enforceShellGate = ShouldEnforceShellGate(report.SubtreeSolveStage);
        var baselinePrimary = ResolvePrimaryScore(primarySignal, baseline);
        var ranked = candidateGroups
            .Select(group =>
            {
                var primaryScore = ResolvePrimaryScore(primarySignal, group);
                var primaryDrop = baselinePrimary - primaryScore;
                var rootGain = group.RootScore - baseline.RootScore;
                var outsideGain = baseline.OutsideSubtreeScore.HasValue && group.OutsideSubtreeScore.HasValue
                    ? group.OutsideSubtreeScore.Value - baseline.OutsideSubtreeScore.Value
                    : (double?)null;
                var shellDrift = ComputeShellDrift(group.SubtreeShell, baseline.SubtreeShell);
                var parentShellDrift = ComputeShellDrift(group.ParentShell, baseline.ParentShell);
                var shellDriftFromExpected = ComputeShellDriftFromExpected(group.SubtreeShell);
                var parentShellDriftFromExpected = ComputeShellDriftFromExpected(group.ParentShell);
                var passesPrimaryGate = primaryDrop <= options.PrimaryDropTolerance;
                var passesImprovementGate = rootGain >= options.ImprovementEpsilon
                    || (outsideGain.HasValue && outsideGain.Value >= options.ImprovementEpsilon);
                var effectiveShellDrift = shellDriftFromExpected ?? shellDrift ?? 0d;
                var effectiveParentShellDrift = parentShellDriftFromExpected ?? parentShellDrift ?? 0d;
                var passesShellGate = !enforceShellGate
                    || (effectiveShellDrift <= options.ShellDriftTolerance
                        && effectiveParentShellDrift <= options.ShellDriftTolerance);

                return new UGuiSubtreeCandidateEvaluation
                {
                    CandidateId = group.CandidateId,
                    SourceKey = group.SourceKey,
                    RunCount = group.RunCount,
                    RootScore = Math.Round(group.RootScore, 4),
                    SubtreeScore = Math.Round(group.SubtreeScore, 4),
                    DirectChildAggregateScore = group.DirectChildAggregateScore is null ? null : Math.Round(group.DirectChildAggregateScore.Value, 4),
                    OutsideSubtreeScore = group.OutsideSubtreeScore is null ? null : Math.Round(group.OutsideSubtreeScore.Value, 4),
                    PrimaryScore = Math.Round(primaryScore, 4),
                    PrimaryDropFromBaseline = Math.Round(primaryDrop, 4),
                    RootGainFromBaseline = Math.Round(rootGain, 4),
                    OutsideGainFromBaseline = outsideGain is null ? null : Math.Round(outsideGain.Value, 4),
                    ShellDriftFromBaseline = shellDrift is null ? null : Math.Round(shellDrift.Value, 4),
                    ParentShellDriftFromBaseline = parentShellDrift is null ? null : Math.Round(parentShellDrift.Value, 4),
                    ShellDriftFromExpected = shellDriftFromExpected is null ? null : Math.Round(shellDriftFromExpected.Value, 4),
                    ParentShellDriftFromExpected = parentShellDriftFromExpected is null ? null : Math.Round(parentShellDriftFromExpected.Value, 4),
                    PassesPrimaryGate = passesPrimaryGate,
                    PassesImprovementGate = passesImprovementGate,
                    PassesShellGate = passesShellGate,
                    IsBaseline = string.Equals(group.CandidateId, baselineCandidateId, StringComparison.Ordinal),
                    IsSelected = false
                };
            })
            .ToList();

        var selectedCandidateId = ranked
            .Where(candidate => !candidate.IsBaseline && candidate.PassesPrimaryGate && candidate.PassesImprovementGate && candidate.PassesShellGate)
            .OrderByDescending(static candidate => candidate.OutsideGainFromBaseline ?? double.NegativeInfinity)
            .ThenByDescending(static candidate => candidate.RootGainFromBaseline)
            .ThenByDescending(static candidate => candidate.PrimaryScore)
            .ThenByDescending(static candidate => candidate.RootScore)
            .Select(static candidate => candidate.CandidateId)
            .FirstOrDefault() ?? baselineCandidateId;

        ranked = ranked
            .Select(candidate => candidate with
            {
                IsSelected = string.Equals(candidate.CandidateId, selectedCandidateId, StringComparison.Ordinal)
            })
            .ToList();

        var changed = !string.Equals(selectedCandidateId, baselineCandidateId, StringComparison.Ordinal);
        return new UGuiSubtreeCandidateSelectionDecision
        {
            DocumentName = report.DocumentName,
            SubtreeStableId = stableId,
            PrimarySignal = primarySignal,
            BaselineCandidateId = baselineCandidateId,
            SelectedCandidateId = selectedCandidateId,
            ChangedSelection = changed,
            PrimaryDropTolerance = options.PrimaryDropTolerance,
            ImprovementEpsilon = options.ImprovementEpsilon,
            ShellDriftTolerance = options.ShellDriftTolerance,
            Candidates = ranked,
            Notes = changed
                ? "Selected the highest-improving candidate that preserved the local subtree objective and shell geometry within tolerance."
                : "No candidate improved parent fidelity without exceeding the allowed local-objective regression or shell-geometry drift."
        };
    }

    private static bool ShouldEnforceShellGate(string? solveStage)
        => string.Equals(solveStage, "atom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(solveStage, "motif", StringComparison.OrdinalIgnoreCase);

    private static string ResolveBaselineCandidateId(
        UGuiSubtreeCandidateSelectionOptions options,
        UGuiBuildProgram buildProgram,
        string stableId,
        IReadOnlyList<CandidateAggregate> candidateGroups)
    {
        if (!string.IsNullOrWhiteSpace(options.BaselineCandidateId))
        {
            return options.BaselineCandidateId;
        }

        var accepted = buildProgram.AcceptedCandidates.FirstOrDefault(selection => string.Equals(selection.StableId, stableId, StringComparison.Ordinal));
        if (accepted != null)
        {
            return accepted.CandidateId;
        }

        return candidateGroups[0].CandidateId;
    }

    private static Dictionary<string, string> BuildCandidateIdByKey(IReadOnlyList<string> rawMappings)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawMapping in rawMappings)
        {
            if (string.IsNullOrWhiteSpace(rawMapping))
            {
                continue;
            }

            var separatorIndex = rawMapping.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == rawMapping.Length - 1)
            {
                throw new InvalidOperationException($"Candidate mapping '{rawMapping}' must be in the form key=candidate-id.");
            }

            var key = rawMapping[..separatorIndex].Trim();
            var candidateId = rawMapping[(separatorIndex + 1)..].Trim();
            map[key] = candidateId;
        }

        return map;
    }

    private static string ResolveCandidateId(string candidateKey, Dictionary<string, string> candidateIdByKey)
        => candidateIdByKey.TryGetValue(candidateKey, out var candidateId)
            ? candidateId
            : candidateKey;

    private static UGuiShellGeometrySnapshot? AverageShellGeometry(IReadOnlyList<UGuiShellGeometrySnapshot?> snapshots)
    {
        var available = snapshots.Where(static snapshot => snapshot != null).Cast<UGuiShellGeometrySnapshot>().ToList();
        if (available.Count == 0)
        {
            return null;
        }

        return new UGuiShellGeometrySnapshot
        {
            LocalPath = available[0].LocalPath,
            ActualX = available.Average(static snapshot => snapshot.ActualX),
            ActualY = available.Average(static snapshot => snapshot.ActualY),
            ActualWidth = available.Average(static snapshot => snapshot.ActualWidth),
            ActualHeight = available.Average(static snapshot => snapshot.ActualHeight),
            ExpectedX = available.Average(static snapshot => snapshot.ExpectedX),
            ExpectedY = available.Average(static snapshot => snapshot.ExpectedY),
            ExpectedWidth = available.Average(static snapshot => snapshot.ExpectedWidth),
            ExpectedHeight = available.Average(static snapshot => snapshot.ExpectedHeight)
        };
    }

    private static double? ComputeShellDrift(UGuiShellGeometrySnapshot? candidate, UGuiShellGeometrySnapshot? baseline)
    {
        if (candidate == null || baseline == null)
        {
            return null;
        }

        return new[]
        {
            Math.Abs(candidate.ActualWidth - baseline.ActualWidth),
            Math.Abs(candidate.ActualHeight - baseline.ActualHeight)
        }.Max();
    }

    private static double? ComputeShellDriftFromExpected(UGuiShellGeometrySnapshot? candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        return new[]
        {
            Math.Abs(candidate.ActualWidth - candidate.ExpectedWidth),
            Math.Abs(candidate.ActualHeight - candidate.ExpectedHeight)
        }.Max();
    }

    private static double ResolvePrimaryScore(string primarySignal, CandidateAggregate candidate)
        => string.Equals(primarySignal, "direct-child-aggregate", StringComparison.Ordinal)
           && candidate.DirectChildAggregateScore.HasValue
            ? candidate.DirectChildAggregateScore.Value
            : candidate.SubtreeScore;

    private static UGuiBuildProgram ApplySelection(UGuiBuildProgram buildProgram, string stableId, string candidateId)
    {
        var accepted = buildProgram.AcceptedCandidates
            .Where(selection => !string.Equals(selection.StableId, stableId, StringComparison.Ordinal))
            .ToList();
        accepted.Add(new UGuiBuildSelection
        {
            StableId = stableId,
            CandidateId = candidateId
        });

        return buildProgram with
        {
            AcceptedCandidates = accepted
        };
    }

    private static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private sealed record CandidateAggregate(
        string CandidateId,
        string SourceKey,
        int RunCount,
        double RootScore,
        double SubtreeScore,
        double? DirectChildAggregateScore,
        double? OutsideSubtreeScore,
        UGuiShellGeometrySnapshot? SubtreeShell,
        UGuiShellGeometrySnapshot? ParentShell);
}
