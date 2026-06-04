using System.Text.Json;
using BoomHud.Cli.Handlers.Pencil;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BoomHud.Tests.Unit.Pencil;

public sealed class PencilRefinementLoopHandlerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "BoomHudTests", Guid.NewGuid().ToString("N"));

    public PencilRefinementLoopHandlerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void RunLoop_WhenNoFurtherPatchedPenIsProduced_StopsOnNoDeterministicPatchOps()
    {
        var penPath = Path.Combine(_tempDir, "QuestHud.pen");
        var referencePath = Path.Combine(_tempDir, "reference.png");
        File.WriteAllText(penPath, CreateSimplePenJson(fontSize: 12));
        CreateBlankPng(referencePath);

        var scoreCalls = 0;
        var result = PencilRefinementLoopHandler.RunLoop(
            new PencilRefinementLoopOptions
            {
                PenFile = new FileInfo(penPath),
                ReferenceFile = new FileInfo(referencePath),
                WorkingDirectory = new DirectoryInfo(Path.Combine(_tempDir, "work")),
                MaxIterations = 3,
                PrintSummary = false
            },
            request =>
            {
                CreateBlankPng(request.OutputPngPath);
                return 0;
            },
            request =>
            {
                scoreCalls++;
                if (scoreCalls == 1)
                {
                    File.WriteAllText(request.PatchedPenPath, CreateSimplePenJson(fontSize: 18));
                }

                return 0;
            });

        result.StopReason.Should().Be(PencilRefinementLoopStopReason.NoDeterministicPatchOps);
        result.Iterations.Should().HaveCount(2);
        result.FinalPenPath.Should().EndWith("QuestHud.patched.pen");
        File.Exists(result.SummaryPath).Should().BeTrue();
    }

    [Fact]
    public void RunLoop_WhenPatchedPenMatchesSource_StopsOnNoEffectiveChange()
    {
        var penPath = Path.Combine(_tempDir, "QuestHud.pen");
        var referencePath = Path.Combine(_tempDir, "reference.png");
        var penJson = CreateSimplePenJson(fontSize: 12);
        File.WriteAllText(penPath, penJson);
        CreateBlankPng(referencePath);

        var result = PencilRefinementLoopHandler.RunLoop(
            new PencilRefinementLoopOptions
            {
                PenFile = new FileInfo(penPath),
                ReferenceFile = new FileInfo(referencePath),
                WorkingDirectory = new DirectoryInfo(Path.Combine(_tempDir, "work-nochange")),
                MaxIterations = 3,
                PrintSummary = false
            },
            request =>
            {
                CreateBlankPng(request.OutputPngPath);
                return 0;
            },
            request =>
            {
                File.WriteAllText(request.PatchedPenPath, penJson);
                return 0;
            });

        result.StopReason.Should().Be(PencilRefinementLoopStopReason.NoEffectiveChange);
        result.Iterations.Should().ContainSingle();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static string CreateSimplePenJson(int fontSize)
    {
        return $$"""
{
  "version": "2.10",
  "name": "QuestHud",
  "children": [
    {
      "id": "root",
      "type": "frame",
      "width": 220,
      "height": 100,
      "children": [
        {
          "id": "title",
          "type": "text",
          "content": "QUEST",
          "fontSize": {{fontSize}}
        }
      ]
    }
  ]
}
""";
    }

    private static void CreateBlankPng(string path)
    {
        using var image = new Image<Rgba32>(16, 16, new Rgba32(255, 255, 255, 255));
        image.SaveAsPng(path);
    }
}
