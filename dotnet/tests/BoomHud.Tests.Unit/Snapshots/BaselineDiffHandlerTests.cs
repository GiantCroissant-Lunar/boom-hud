using System.Text.Json;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Cli.Handlers.Baseline;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BoomHud.Tests.Unit.Snapshots;

/// <summary>
/// Unit tests for BaselineDiffHandler.
/// Uses temp folders with fake manifests and PNGs to validate diff artifact generation.
/// </summary>
public sealed class BaselineDiffHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _currentDir;
    private readonly string _baselineDir;
    private readonly string _outputDir;

    public BaselineDiffHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"boomhud-diff-tests-{Guid.NewGuid():N}");
        _currentDir = Path.Combine(_tempDir, "current");
        _baselineDir = Path.Combine(_tempDir, "baseline");
        _outputDir = Path.Combine(_tempDir, "diffs");

        Directory.CreateDirectory(_currentDir);
        Directory.CreateDirectory(_baselineDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Fact]
    public void Execute_WithChangedFrame_GeneratesDiffTripletAndReport()
    {
        // Arrange
        WriteManifest(_baselineDir, CreateManifest(("Hover", "baseline-hash")));
        WriteManifest(_currentDir, CreateManifest(("Hover", "current-hash")));

        CreatePng(_baselineDir, "Hover.png", 16, 16, new Rgba32(255, 255, 255, 255));
        CreatePng(_currentDir, "Hover.png", 16, 16, new Rgba32(0, 0, 0, 255));

        var options = new BaselineDiffOptions
        {
            CurrentDir = new DirectoryInfo(_currentDir),
            BaselineDir = new DirectoryInfo(_baselineDir),
            OutputDir = new DirectoryInfo(_outputDir),
            Tolerance = 0,
            Verbose = false
        };

        // Act
        var exitCode = BaselineDiffHandler.Execute(options);

        // Assert
        exitCode.Should().Be(0);
        File.Exists(Path.Combine(_outputDir, "000_Hover__baseline.png")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "000_Hover__current.png")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "000_Hover__diff.png")).Should().BeTrue();

        var report = LoadReport(Path.Combine(_outputDir, "diff-report.json"));
        report.Should().NotBeNull();
        report!.Summary.Changed.Should().Be(1);
        report.Frames.Should().ContainSingle();
        report.Frames[0].Status.Should().Be(FrameCompareStatus.Changed);
        report.Frames[0].DiffPath.Should().Be("000_Hover__diff.png");
        report.Frames[0].DiffMetrics.Should().NotBeNull();
        report.Frames[0].DiffMetrics!.ChangedPercent.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Execute_WithDimensionMismatch_GeneratesDiffAndMarksStatus()
    {
        // Arrange
        WriteManifest(_baselineDir, CreateManifest(("Viewport", "baseline-hash")));
        WriteManifest(_currentDir, CreateManifest(("Viewport", "current-hash")));

        CreatePng(_baselineDir, "Viewport.png", 10, 10, new Rgba32(20, 20, 20, 255));
        CreatePng(_currentDir, "Viewport.png", 12, 10, new Rgba32(30, 30, 30, 255));

        var options = new BaselineDiffOptions
        {
            CurrentDir = new DirectoryInfo(_currentDir),
            BaselineDir = new DirectoryInfo(_baselineDir),
            OutputDir = new DirectoryInfo(_outputDir)
        };

        // Act
        var exitCode = BaselineDiffHandler.Execute(options);

        // Assert
        exitCode.Should().Be(0);
        File.Exists(Path.Combine(_outputDir, "000_Viewport__diff.png")).Should().BeTrue();

        var report = LoadReport(Path.Combine(_outputDir, "diff-report.json"));
        report.Should().NotBeNull();
        report!.Summary.DimensionMismatch.Should().Be(1);
        report.Frames.Should().ContainSingle();
        report.Frames[0].Status.Should().Be(FrameCompareStatus.DimensionMismatch);
        report.Frames[0].DiffPath.Should().Be("000_Viewport__diff.png");
        report.Frames[0].DiffMetrics.Should().NotBeNull();
        report.Frames[0].DiffMetrics!.DimensionsMatch.Should().BeFalse();
    }

    [Fact]
    public void Execute_WithUnchangedFrames_WritesReportButNoDiffArtifacts()
    {
        // Arrange
        WriteManifest(_baselineDir, CreateManifest(("Idle", "same-hash")));
        WriteManifest(_currentDir, CreateManifest(("Idle", "same-hash")));

        CreatePng(_baselineDir, "Idle.png", 8, 8, new Rgba32(100, 100, 100, 255));
        CreatePng(_currentDir, "Idle.png", 8, 8, new Rgba32(100, 100, 100, 255));

        var options = new BaselineDiffOptions
        {
            CurrentDir = new DirectoryInfo(_currentDir),
            BaselineDir = new DirectoryInfo(_baselineDir),
            OutputDir = new DirectoryInfo(_outputDir)
        };

        // Act
        var exitCode = BaselineDiffHandler.Execute(options);

        // Assert
        exitCode.Should().Be(0);
        File.Exists(Path.Combine(_outputDir, "diff-report.json")).Should().BeTrue();
        Directory.GetFiles(_outputDir, "*__diff.png").Should().BeEmpty();

        var report = LoadReport(Path.Combine(_outputDir, "diff-report.json"));
        report.Should().NotBeNull();
        report!.Summary.Unchanged.Should().Be(1);
        report.Frames.Should().ContainSingle();
        report.Frames[0].Status.Should().Be(FrameCompareStatus.Unchanged);
        report.Frames[0].DiffPath.Should().BeNull();
    }

    [Fact]
    public void Execute_WithMissingBaselineDirectory_ReturnsError()
    {
        // Arrange
        WriteManifest(_currentDir, CreateManifest(("OnlyCurrent", "hash")));
        CreatePng(_currentDir, "OnlyCurrent.png", 8, 8, new Rgba32(0, 0, 0, 255));

        var missingBaseline = Path.Combine(_tempDir, "missing-baseline");
        var options = new BaselineDiffOptions
        {
            CurrentDir = new DirectoryInfo(_currentDir),
            BaselineDir = new DirectoryInfo(missingBaseline),
            OutputDir = new DirectoryInfo(_outputDir)
        };

        // Act
        var exitCode = BaselineDiffHandler.Execute(options);

        // Assert
        exitCode.Should().Be(1);
        Directory.Exists(_outputDir).Should().BeFalse();
    }

    private static void WriteManifest(string dir, SnapshotOutputManifest manifest)
    {
        var path = Path.Combine(dir, "snapshots.manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private static SnapshotOutputManifest CreateManifest(params (string state, string hash)[] snapshots)
    {
        return new SnapshotOutputManifest
        {
            Version = "1.0",
            Target = "godot",
            GodotVersion = "4.2.0",
            ToolVersion = "1.0.0",
            Viewport = new ViewportConfig { Width = 800, Height = 600 },
            Snapshots = snapshots.Select(snapshot => new SnapshotFileInfo
            {
                State = snapshot.state,
                Path = $"{snapshot.state}.png",
                Sha256 = snapshot.hash
            }).ToList()
        };
    }

    private static void CreatePng(string dir, string fileName, int width, int height, Rgba32 color)
    {
        using var image = new Image<Rgba32>(width, height, color);
        image.SaveAsPng(Path.Combine(dir, fileName));
    }

    private static BaselineReport? LoadReport(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<BaselineReport>(json);
    }
}