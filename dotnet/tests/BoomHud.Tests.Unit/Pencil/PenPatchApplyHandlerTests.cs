using System.Text.Json;
using System.Text.Json.Nodes;
using BoomHud.Cli.Handlers.Pencil;
using BoomHud.Gen.Pencil;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Pencil;

public sealed class PenPatchApplyHandlerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "BoomHudTests", Guid.NewGuid().ToString("N"));

    public PenPatchApplyHandlerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Execute_WithBatchOps_AppliesDirectNodeUpdates()
    {
        var penPath = Path.Combine(_tempDir, "QuestHud.pen");
        var batchOpsPath = Path.Combine(_tempDir, "QuestHud.pen-batch-ops.txt");
        var outputPath = Path.Combine(_tempDir, "QuestHud.patched.pen");

        File.WriteAllText(penPath, """
{
  "version": "2.10",
  "name": "QuestHud",
  "children": [
    {
      "id": "QH01",
      "type": "frame",
      "children": [
        {
          "id": "QH02",
          "type": "text",
          "content": "QUEST",
          "fontSize": 12
        }
      ]
    }
  ]
}
""");

        File.WriteAllText(batchOpsPath, """
U("QH02", {"fontFamily": "Press Start 2P", "fontSize": 14, "textGrowth": "fixed-width"})
""");

        var exitCode = PenPatchApplyHandler.Execute(new PenPatchApplyOptions
        {
            PenFile = new FileInfo(penPath),
            BatchOpsFile = new FileInfo(batchOpsPath),
            OutFile = new FileInfo(outputPath),
            PrintSummary = false
        });

        exitCode.Should().Be(0);
        File.Exists(outputPath).Should().BeTrue();

        var patched = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        var textNode = patched["children"]![0]!["children"]![0]!.AsObject();
        textNode["fontFamily"]!.GetValue<string>().Should().Be("Press Start 2P");
        textNode["fontSize"]!.GetValue<int>().Should().Be(14);
        textNode["textGrowth"]!.GetValue<string>().Should().Be("fixed-width");
    }

    [Fact]
    public void Execute_WithPatchPlan_AppliesOnlyDeterministicSteps()
    {
        var penPath = Path.Combine(_tempDir, "QuestHud.pen");
        var patchPlanPath = Path.Combine(_tempDir, "QuestHud.pen-patch-plan.json");
        var outputPath = Path.Combine(_tempDir, "QuestHud.patched.pen");

        File.WriteAllText(penPath, """
{
  "version": "2.10",
  "name": "QuestHud",
  "children": [
    {
      "id": "QH01",
      "type": "frame",
      "children": [
        {
          "id": "QH02",
          "type": "text",
          "content": "QUEST",
          "fontSize": 12
        },
        {
          "id": "QH03",
          "type": "frame"
        }
      ]
    }
  ]
}
""");

        var patchPlan = new PencilPatchPlan
        {
            DocumentName = "QuestHud",
            TargetFormat = "pen",
            ActionCount = 2,
            Steps =
            [
                new PencilPatchPlanStep
                {
                    Order = 1,
                    TargetStableId = "root/0",
                    TargetPenId = "QH02",
                    ReasonPhase = "text-icon-metrics",
                    ActionType = "metric-profile-adjustment",
                    Description = "Tune typography.",
                    RequiresStructuralRewrite = false,
                    SuggestedProperties = new Dictionary<string, object?>
                    {
                        ["fontSize"] = 16d
                    }
                },
                new PencilPatchPlanStep
                {
                    Order = 2,
                    TargetStableId = "root/1",
                    TargetPenId = "QH03",
                    ReasonPhase = "structural-match",
                    ActionType = "panel-motif-split",
                    Description = "Split the panel motif.",
                    RequiresStructuralRewrite = true
                }
            ]
        };

        File.WriteAllText(patchPlanPath, JsonSerializer.Serialize(patchPlan));

        var exitCode = PenPatchApplyHandler.Execute(new PenPatchApplyOptions
        {
            PenFile = new FileInfo(penPath),
            PatchPlanFile = new FileInfo(patchPlanPath),
            OutFile = new FileInfo(outputPath),
            PrintSummary = false
        });

        exitCode.Should().Be(0);

        var patched = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        var rootFrame = patched["children"]![0]!.AsObject();
        rootFrame["children"]![0]!["fontSize"]!.GetValue<int>().Should().Be(16);
        rootFrame["children"]![1]!["id"]!.GetValue<string>().Should().Be("QH03");
    }

    [Fact]
    public void Execute_WithNoOpBatchOps_DoesNotWritePatchedFile()
    {
        var penPath = Path.Combine(_tempDir, "QuestHud.pen");
        var batchOpsPath = Path.Combine(_tempDir, "QuestHud.pen-batch-ops.txt");
        var outputPath = Path.Combine(_tempDir, "QuestHud.patched.pen");

        File.WriteAllText(penPath, """
{
  "version": "2.10",
  "name": "QuestHud",
  "children": [
    {
      "id": "QH01",
      "type": "frame",
      "width": 1920,
      "height": 1080,
      "layout": "none"
    }
  ]
}
""");

        File.WriteAllText(batchOpsPath, """
U("QH01", {"width": 1920, "height": 1080, "layout": "none"})
""");

        var exitCode = PenPatchApplyHandler.Execute(new PenPatchApplyOptions
        {
            PenFile = new FileInfo(penPath),
            BatchOpsFile = new FileInfo(batchOpsPath),
            OutFile = new FileInfo(outputPath),
            PrintSummary = false
        });

        exitCode.Should().Be(0);
        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public void Execute_WithDescendantPath_WritesInstanceDescendantsOverride()
    {
        var penPath = Path.Combine(_tempDir, "QuestHud.pen");
        var batchOpsPath = Path.Combine(_tempDir, "QuestHud.pen-batch-ops.txt");
        var outputPath = Path.Combine(_tempDir, "QuestHud.patched.pen");

        File.WriteAllText(penPath, """
{
  "version": "2.10",
  "name": "QuestHud",
  "children": [
    {
      "id": "instance01",
      "type": "ref",
      "ref": "quest_card"
    }
  ]
}
""");

        File.WriteAllText(batchOpsPath, """
U("instance01/titleLabel", {"fontSize": 18, "fontFamily": "Arcade Classic"})
""");

        var exitCode = PenPatchApplyHandler.Execute(new PenPatchApplyOptions
        {
            PenFile = new FileInfo(penPath),
            BatchOpsFile = new FileInfo(batchOpsPath),
            OutFile = new FileInfo(outputPath),
            PrintSummary = false
        });

        exitCode.Should().Be(0);

        var patched = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        var instanceNode = patched["children"]![0]!.AsObject();
        var descendants = instanceNode["descendants"]!.AsObject();
        descendants["titleLabel"]!["fontSize"]!.GetValue<int>().Should().Be(18);
        descendants["titleLabel"]!["fontFamily"]!.GetValue<string>().Should().Be("Arcade Classic");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
