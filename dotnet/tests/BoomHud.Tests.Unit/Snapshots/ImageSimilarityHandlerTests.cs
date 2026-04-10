using System.Text.Json;
using BoomHud.Cli.Handlers.Baseline;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BoomHud.Tests.Unit.Snapshots;

public sealed class ImageSimilarityHandlerTests : IDisposable
{
    private readonly string _tempDir;

    public ImageSimilarityHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"boomhud-image-score-{Guid.NewGuid():N}");
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
    public void Execute_WithIdenticalImages_ReportsHundredPercentSimilarity()
    {
        var referencePath = Path.Combine(_tempDir, "reference.png");
        var candidatePath = Path.Combine(_tempDir, "candidate.png");
        var reportPath = Path.Combine(_tempDir, "report.json");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 16, 16, new Rgba32(255, 255, 255, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        report.PixelIdentityPercent.Should().Be(100);
        report.DeltaSimilarityPercent.Should().Be(100);
        report.OverallSimilarityPercent.Should().Be(100);
        report.Metrics.ChangedPixels.Should().Be(0);
        report.Findings.Should().BeEmpty();
        report.Analysis.Should().NotBeNull();
    }

    [Fact]
    public void Execute_WithDifferentImages_WritesReportAndDiff()
    {
        var referencePath = Path.Combine(_tempDir, "reference.png");
        var candidatePath = Path.Combine(_tempDir, "candidate.png");
        var reportPath = Path.Combine(_tempDir, "report.json");
        var diffPath = Path.Combine(_tempDir, "diff.png");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 16, 16, new Rgba32(0, 0, 0, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            DiffFile = new FileInfo(diffPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);
        File.Exists(diffPath).Should().BeTrue();

        var report = LoadReport(reportPath);
        report.Metrics.ChangedPercent.Should().BeGreaterThan(0);
        report.OverallSimilarityPercent.Should().BeLessThan(100);
        report.DiffPath.Should().Be(diffPath);
        report.Findings.Should().NotBeEmpty();
    }

    [Fact]
    public void Execute_WithNormalizeStretch_MakesMismatchedSolidImagesComparable()
    {
        var referencePath = Path.Combine(_tempDir, "reference.png");
        var candidatePath = Path.Combine(_tempDir, "candidate.png");
        var reportPath = Path.Combine(_tempDir, "report-normalized.json");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 8, 8, new Rgba32(255, 255, 255, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            NormalizeMode = "stretch",
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        report.Normalization.Should().NotBeNull();
        report.Normalization!.Mode.Should().Be("stretch");
        report.Metrics.DimensionsMatch.Should().BeTrue();
        report.OverallSimilarityPercent.Should().Be(100);
        report.Findings.Should().NotContain(f => f.Category == "dimension-mismatch");
    }

    [Fact]
    public void Execute_WithFailBelow_ReturnsThresholdExitCode()
    {
        var referencePath = Path.Combine(_tempDir, "reference-threshold.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-threshold.png");
        var reportPath = Path.Combine(_tempDir, "report-threshold.json");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 16, 16, new Rgba32(0, 0, 0, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            FailBelowOverallPercent = 95,
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(2);

        var report = LoadReport(reportPath);
        report.FailBelowOverallPercent.Should().Be(95);
        report.PassedThreshold.Should().BeFalse();
    }

    [Fact]
    public void Execute_WithDimensionMismatch_ReportsDimensionFinding()
    {
        var referencePath = Path.Combine(_tempDir, "reference-dimension.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-dimension.png");
        var reportPath = Path.Combine(_tempDir, "report-dimension.json");

        CreatePng(referencePath, 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 8, 8, new Rgba32(255, 255, 255, 255));

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        report.Findings.Should().Contain(f => f.Category == "dimension-mismatch");
    }

    [Fact]
    public void Execute_WithRightEdgeDrift_ReportsEdgeFinding()
    {
        var referencePath = Path.Combine(_tempDir, "reference-right-edge.png");
        var candidatePath = Path.Combine(_tempDir, "candidate-right-edge.png");
        var reportPath = Path.Combine(_tempDir, "report-right-edge.json");

        CreatePng(referencePath, 20, 20, new Rgba32(255, 255, 255, 255));
        CreatePng(candidatePath, 20, 20, new Rgba32(255, 255, 255, 255));

        using (var candidate = Image.Load<Rgba32>(candidatePath))
        {
            for (var y = 0; y < candidate.Height; y++)
            {
                for (var x = candidate.Width - 2; x < candidate.Width; x++)
                {
                    candidate[x, y] = new Rgba32(0, 0, 0, 255);
                }
            }

            candidate.SaveAsPng(candidatePath);
        }

        var exitCode = ImageSimilarityHandler.Execute(new ImageSimilarityOptions
        {
            ReferenceFile = new FileInfo(referencePath),
            CandidateFile = new FileInfo(candidatePath),
            OutFile = new FileInfo(reportPath),
            PrintSummary = false,
            Tolerance = 0
        });

        exitCode.Should().Be(0);

        var report = LoadReport(reportPath);
        report.Findings.Should().Contain(f => f.Category == "edge-alignment-mismatch" && f.Region == "right-edge");
    }

    private static void CreatePng(string path, int width, int height, Rgba32 color)
    {
        using var image = new Image<Rgba32>(width, height, color);
        image.SaveAsPng(path);
    }

    private static ImageSimilarityReport LoadReport(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ImageSimilarityReport>(json)!;
    }
}
