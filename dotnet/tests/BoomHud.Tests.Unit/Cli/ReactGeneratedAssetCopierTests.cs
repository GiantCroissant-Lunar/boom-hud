using BoomHud.Abstractions.IR;
using BoomHud.Cli;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Cli;

public sealed class ReactGeneratedAssetCopierTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "boomhud-react-assets-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CopyBackgroundImageAssets_CopiesRelativeSourceAssetIntoOutput()
    {
        var inputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "input"));
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "output"));
        var imageDirectory = Directory.CreateDirectory(Path.Combine(inputDirectory.FullName, "images"));
        var sourceImagePath = Path.Combine(imageDirectory.FullName, "viewport.png");
        File.WriteAllText(sourceImagePath, "placeholder-image");

        var result = ReactGeneratedAssetCopier.CopyBackgroundImageAssets(
            CreateDocument("./images/viewport.png"),
            [new FileInfo(Path.Combine(inputDirectory.FullName, "hud.pen"))],
            outputDirectory.FullName);

        var targetImagePath = Path.Combine(outputDirectory.FullName, "images", "viewport.png");
        File.Exists(targetImagePath).Should().BeTrue();
        File.ReadAllText(targetImagePath).Should().Be("placeholder-image");
        result.CopiedFiles.Should().ContainSingle(path => path == targetImagePath);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void CopyBackgroundImageAssets_CollectsComponentAssetsToo()
    {
        var inputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "component-input"));
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "component-output"));
        var imageDirectory = Directory.CreateDirectory(Path.Combine(inputDirectory.FullName, "images"));
        var sourceImagePath = Path.Combine(imageDirectory.FullName, "portrait.png");
        File.WriteAllText(sourceImagePath, "component-image");

        var document = new HudDocument
        {
            Name = "Hud",
            Root = new ComponentNode { Id = "root", Type = ComponentType.Container },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["portrait"] = new()
                {
                    Id = "portrait",
                    Name = "Portrait",
                    Root = new ComponentNode
                    {
                        Id = "portrait-root",
                        Type = ComponentType.Container,
                        Style = new StyleSpec
                        {
                            BackgroundImage = new BackgroundImageSpec { Url = "./images/portrait.png" }
                        }
                    }
                }
            }
        };

        var result = ReactGeneratedAssetCopier.CopyBackgroundImageAssets(
            document,
            [new FileInfo(Path.Combine(inputDirectory.FullName, "hud.pen"))],
            outputDirectory.FullName);

        var targetImagePath = Path.Combine(outputDirectory.FullName, "images", "portrait.png");
        File.Exists(targetImagePath).Should().BeTrue();
        result.CopiedFiles.Should().ContainSingle(path => path == targetImagePath);
    }

    [Fact]
    public void CopyBackgroundImageAssets_WarnsWhenRelativeAssetIsMissing()
    {
        var inputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "missing-input"));
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "missing-output"));

        var result = ReactGeneratedAssetCopier.CopyBackgroundImageAssets(
            CreateDocument("./images/missing.png"),
            [new FileInfo(Path.Combine(inputDirectory.FullName, "hud.pen"))],
            outputDirectory.FullName);

        result.CopiedFiles.Should().BeEmpty();
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].Should().Contain("./images/missing.png");
        File.Exists(Path.Combine(outputDirectory.FullName, "images", "missing.png")).Should().BeFalse();
    }

    [Fact]
    public void CopyBackgroundImageAssets_IgnoresAbsoluteUrls()
    {
        var inputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "absolute-input"));
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "absolute-output"));

        var result = ReactGeneratedAssetCopier.CopyBackgroundImageAssets(
            CreateDocument("https://example.com/background.png"),
            [new FileInfo(Path.Combine(inputDirectory.FullName, "hud.pen"))],
            outputDirectory.FullName);

        result.CopiedFiles.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static HudDocument CreateDocument(string backgroundImageUrl)
        => new()
        {
            Name = "Hud",
            Root = new ComponentNode
            {
                Id = "root",
                Type = ComponentType.Container,
                Style = new StyleSpec
                {
                    BackgroundImage = new BackgroundImageSpec { Url = backgroundImageUrl }
                }
            }
        };
}