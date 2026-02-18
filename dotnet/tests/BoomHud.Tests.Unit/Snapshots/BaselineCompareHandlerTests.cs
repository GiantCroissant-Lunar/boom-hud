using System.Text.Json;
using BoomHud.Abstractions.Snapshots;
using BoomHud.Cli.Handlers.Baseline;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BoomHud.Tests.Unit.Snapshots;

/// <summary>
/// Unit tests for BaselineCompareHandler.
/// Uses temp folders with fake manifests to test comparison logic.
/// </summary>
public class BaselineCompareHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _currentDir;
    private readonly string _baselineDir;

    public BaselineCompareHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"boomhud-tests-{Guid.NewGuid():N}");
        _currentDir = Path.Combine(_tempDir, "current");
        _baselineDir = Path.Combine(_tempDir, "baseline");
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

    #region Test Helpers

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static void WriteManifest(string dir, SnapshotOutputManifest manifest)
    {
        var path = Path.Combine(dir, "snapshots.manifest.json");
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static SnapshotOutputManifest CreateManifest(
        string godotVersion = "4.2.0",
        params (string state, string hash)[] snapshots)
    {
        return new SnapshotOutputManifest
        {
            Version = "1.0",
            Target = "godot",
            GodotVersion = godotVersion,
            ToolVersion = "1.0.0",
            Viewport = new ViewportConfig { Width = 800, Height = 600 },
            Snapshots = snapshots.Select(s => new SnapshotFileInfo
            {
                State = s.state,
                Path = $"{s.state}.png",
                Sha256 = s.hash
            }).ToList()
        };
    }

    private static void CreateDummyPng(string dir, string filename, int width = 100, int height = 100)
    {
        // Create a valid PNG using ImageSharp
        using var image = new Image<Rgba32>(width, height, new Rgba32(255, 255, 255, 255));
        image.SaveAsPng(Path.Combine(dir, filename));
    }

    #endregion

    #region Status Count Tests

    [Fact]
    public void CompareManifests_AllUnchanged_ReturnsCorrectCounts()
    {
        // Arrange
        var manifest = CreateManifest("4.2.0",
            ("state1", "hash1"),
            ("state2", "hash2"),
            ("state3", "hash3"));

        WriteManifest(_currentDir, manifest);
        WriteManifest(_baselineDir, manifest);

        // Create dummy PNGs
        foreach (var snap in manifest.Snapshots)
        {
            CreateDummyPng(_currentDir, snap.Path);
            CreateDummyPng(_baselineDir, snap.Path);
        }

        // Act
        var report = BaselineCompareHandler.CompareManifestsWithMetrics(
            manifest, manifest,
            _currentDir, _baselineDir,
            Path.Combine(_currentDir, "manifest.json"),
            Path.Combine(_baselineDir, "manifest.json"),
            tolerance: 0, minChangedPercent: 0, protectedFrames: []);

        // Assert
        report.Summary.Total.Should().Be(3);
        report.Summary.Unchanged.Should().Be(3);
        report.Summary.Changed.Should().Be(0);
        report.Summary.MissingBaseline.Should().Be(0);
        report.Summary.MissingCurrent.Should().Be(0);
        report.Summary.Compatible.Should().BeTrue();
    }

    [Fact]
    public void CompareManifests_WithChanges_ReturnsCorrectCounts()
    {
        // Arrange
        var baselineManifest = CreateManifest("4.2.0",
            ("state1", "hash1"),
            ("state2", "hash2"));

        var currentManifest = CreateManifest("4.2.0",
            ("state1", "hash1"),
            ("state2", "hash2_changed"));

        WriteManifest(_currentDir, currentManifest);
        WriteManifest(_baselineDir, baselineManifest);

        foreach (var snap in baselineManifest.Snapshots)
        {
            CreateDummyPng(_baselineDir, snap.Path);
        }
        foreach (var snap in currentManifest.Snapshots)
        {
            CreateDummyPng(_currentDir, snap.Path);
        }

        // Act
        var report = BaselineCompareHandler.CompareManifestsWithMetrics(
            currentManifest, baselineManifest,
            _currentDir, _baselineDir,
            Path.Combine(_currentDir, "manifest.json"),
            Path.Combine(_baselineDir, "manifest.json"),
            tolerance: 0, minChangedPercent: 0, protectedFrames: []);

        // Assert
        report.Summary.Total.Should().Be(2);
        report.Summary.Unchanged.Should().Be(1);
        report.Summary.Changed.Should().Be(1);
    }

    [Fact]
    public void CompareManifests_NewFrames_MissingBaseline()
    {
        // Arrange
        var baselineManifest = CreateManifest("4.2.0",
            ("state1", "hash1"));

        var currentManifest = CreateManifest("4.2.0",
            ("state1", "hash1"),
            ("state2_new", "hash2"));

        WriteManifest(_currentDir, currentManifest);
        WriteManifest(_baselineDir, baselineManifest);

        foreach (var snap in baselineManifest.Snapshots)
        {
            CreateDummyPng(_baselineDir, snap.Path);
        }
        foreach (var snap in currentManifest.Snapshots)
        {
            CreateDummyPng(_currentDir, snap.Path);
        }

        // Act
        var report = BaselineCompareHandler.CompareManifestsWithMetrics(
            currentManifest, baselineManifest,
            _currentDir, _baselineDir,
            Path.Combine(_currentDir, "manifest.json"),
            Path.Combine(_baselineDir, "manifest.json"),
            tolerance: 0, minChangedPercent: 0, protectedFrames: []);

        // Assert
        report.Summary.Total.Should().Be(2);
        report.Summary.MissingBaseline.Should().Be(1);
        report.Frames.First(f => f.Name == "state2_new").Status.Should().Be(FrameCompareStatus.MissingBaseline);
    }

    [Fact]
    public void CompareManifests_RemovedFrames_MissingCurrent()
    {
        // Arrange
        var baselineManifest = CreateManifest("4.2.0",
            ("state1", "hash1"),
            ("state2_removed", "hash2"));

        var currentManifest = CreateManifest("4.2.0",
            ("state1", "hash1"));

        WriteManifest(_currentDir, currentManifest);
        WriteManifest(_baselineDir, baselineManifest);

        foreach (var snap in baselineManifest.Snapshots)
        {
            CreateDummyPng(_baselineDir, snap.Path);
        }
        foreach (var snap in currentManifest.Snapshots)
        {
            CreateDummyPng(_currentDir, snap.Path);
        }

        // Act
        var report = BaselineCompareHandler.CompareManifestsWithMetrics(
            currentManifest, baselineManifest,
            _currentDir, _baselineDir,
            Path.Combine(_currentDir, "manifest.json"),
            Path.Combine(_baselineDir, "manifest.json"),
            tolerance: 0, minChangedPercent: 0, protectedFrames: []);

        // Assert
        report.Summary.Total.Should().Be(2);
        report.Summary.MissingCurrent.Should().Be(1);
        report.Frames.First(f => f.Name == "state2_removed").Status.Should().Be(FrameCompareStatus.MissingCurrent);
    }

    #endregion

    #region Actionable Tests (Godot Version Mismatch)

    [Fact]
    public void CompareManifests_GodotVersionMismatch_MarksNonActionable()
    {
        // Arrange
        var baselineManifest = CreateManifest("4.1.0",
            ("state1", "hash1"),
            ("state2", "hash2_changed"));

        var currentManifest = CreateManifest("4.2.0",
            ("state1", "hash1"),
            ("state2", "hash2_changed_different"));

        WriteManifest(_currentDir, currentManifest);
        WriteManifest(_baselineDir, baselineManifest);

        foreach (var snap in baselineManifest.Snapshots)
        {
            CreateDummyPng(_baselineDir, snap.Path);
        }
        foreach (var snap in currentManifest.Snapshots)
        {
            CreateDummyPng(_currentDir, snap.Path);
        }

        // Act
        var report = BaselineCompareHandler.CompareManifestsWithMetrics(
            currentManifest, baselineManifest,
            _currentDir, _baselineDir,
            Path.Combine(_currentDir, "manifest.json"),
            Path.Combine(_baselineDir, "manifest.json"),
            tolerance: 0, minChangedPercent: 0, protectedFrames: []);

        // Assert
        report.Summary.Compatible.Should().BeFalse();
        report.Summary.IncompatibilityReason.Should().Contain("Godot version mismatch");
        report.Summary.ChangedNonActionable.Should().Be(1);

        var changedFrame = report.Frames.First(f => f.Name == "state2");
        changedFrame.Actionable.Should().BeFalse();
        changedFrame.Status.Should().Be(FrameCompareStatus.ChangedNonActionable);
    }

    #endregion

    #region Fail-On Tests

    [Fact]
    public void ParseFailOn_Any_ReturnsAnyMode()
    {
        // Act
        var (mode, percent) = BaselineCompareOptions.ParseFailOn("any", legacyFailOnChanged: false);

        // Assert
        mode.Should().Be(FailOnMode.Any);
        percent.Should().BeNull();
    }

    [Fact]
    public void ParseFailOn_Percent_ReturnsPercentMode()
    {
        // Act
        var (mode, percent) = BaselineCompareOptions.ParseFailOn("percent:0.5", legacyFailOnChanged: false);

        // Assert
        mode.Should().Be(FailOnMode.Percent);
        percent.Should().Be(0.5);
    }

    [Fact]
    public void ParseFailOn_LegacyFlag_ReturnsAnyMode()
    {
        // Act
        var (mode, percent) = BaselineCompareOptions.ParseFailOn(null, legacyFailOnChanged: true);

        // Assert
        mode.Should().Be(FailOnMode.Any);
        percent.Should().BeNull();
    }

    [Fact]
    public void ParseFailOn_Invalid_ReturnsNoneMode()
    {
        // Act
        var (mode, percent) = BaselineCompareOptions.ParseFailOn("invalid", legacyFailOnChanged: false);

        // Assert
        mode.Should().Be(FailOnMode.None);
        percent.Should().BeNull();
    }

    #endregion

    #region Protected Frames Tests

    [Fact]
    public void CompareManifests_ProtectedFrameBelowThreshold_StillMarkedChanged()
    {
        // Even if minChangedPercent would filter it out, protected frames stay marked as Changed
        // This test verifies the protected frames logic in CompareManifestsWithMetrics

        // Arrange
        var baselineManifest = CreateManifest("4.2.0",
            ("protected_frame", "hash1"));

        var currentManifest = CreateManifest("4.2.0",
            ("protected_frame", "hash1_slightly_different"));

        WriteManifest(_currentDir, currentManifest);
        WriteManifest(_baselineDir, baselineManifest);

        CreateDummyPng(_baselineDir, "protected_frame.png");
        CreateDummyPng(_currentDir, "protected_frame.png");

        // Act
        var report = BaselineCompareHandler.CompareManifestsWithMetrics(
            currentManifest, baselineManifest,
            _currentDir, _baselineDir,
            Path.Combine(_currentDir, "manifest.json"),
            Path.Combine(_baselineDir, "manifest.json"),
            tolerance: 0,
            minChangedPercent: 99.0, // Very high threshold
            protectedFrames: ["protected_frame"]);

        // Assert - protected frame should be marked as Changed even with high minChangedPercent
        var frame = report.Frames.First(f => f.Name == "protected_frame");
        frame.Status.Should().Be(FrameCompareStatus.Changed);
    }

    #endregion

    #region LoadManifest Tests

    [Fact]
    public void LoadManifest_ValidFile_ReturnsManifest()
    {
        // Arrange
        var manifest = CreateManifest("4.2.0", ("state1", "hash1"));
        WriteManifest(_currentDir, manifest);

        // Act
        var loaded = BaselineCompareHandler.LoadManifest(Path.Combine(_currentDir, "snapshots.manifest.json"));

        // Assert
        loaded.Should().NotBeNull();
        loaded!.GodotVersion.Should().Be("4.2.0");
        loaded.Snapshots.Should().HaveCount(1);
    }

    [Fact]
    public void LoadManifest_InvalidFile_ReturnsNull()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_currentDir, "invalid.json"), "not json");

        // Act
        var loaded = BaselineCompareHandler.LoadManifest(Path.Combine(_currentDir, "invalid.json"));

        // Assert
        loaded.Should().BeNull();
    }

    [Fact]
    public void LoadManifest_MissingFile_ReturnsNull()
    {
        // Act
        var loaded = BaselineCompareHandler.LoadManifest(Path.Combine(_currentDir, "missing.json"));

        // Assert
        loaded.Should().BeNull();
    }

    #endregion
}
