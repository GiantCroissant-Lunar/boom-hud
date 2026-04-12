using System.Text.Json;
using BoomHud.Abstractions.Generation;
using BoomHud.Cli;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Cli;

public sealed class GeneratedFileWriterTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "boomhud-generated-files-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Write_CreatesFilesAndManifest()
    {
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "output"));

        GeneratedFileWriter.Write(outputDirectory.FullName, "Hud",
        [
            new GeneratedFile
            {
                Path = "HudView.cs",
                Content = "// generated"
            },
            new GeneratedFile
            {
                Path = "nested/HudView.uxml",
                Content = "<ui:UXML />"
            }
        ]);

        File.ReadAllText(Path.Combine(outputDirectory.FullName, "HudView.cs")).Should().Be("// generated");
        File.ReadAllText(Path.Combine(outputDirectory.FullName, "nested", "HudView.uxml")).Should().Be("<ui:UXML />");

        var manifestPath = Path.Combine(outputDirectory.FullName, GeneratedFileWriter.ManifestFileName);
        File.Exists(manifestPath).Should().BeTrue();

        using var manifestJson = JsonDocument.Parse(File.ReadAllText(manifestPath));
        manifestJson.RootElement.GetProperty("Scopes").GetProperty("Hud").EnumerateArray().Select(static x => x.GetString()).Should().BeEquivalentTo(
        [
            "HudView.cs",
            "nested/HudView.uxml"
        ]);
    }

    [Fact]
    public void Write_DeletesStaleGeneratedFiles_AndPreservesUnrelatedFiles()
    {
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "output"));
        var staleDirectory = Directory.CreateDirectory(Path.Combine(outputDirectory.FullName, "stale"));
        var unrelatedPath = Path.Combine(outputDirectory.FullName, "keep-me.txt");
        File.WriteAllText(unrelatedPath, "user-owned");

        GeneratedFileWriter.Write(outputDirectory.FullName, "Hud",
        [
            new GeneratedFile
            {
                Path = "CurrentView.cs",
                Content = "// first"
            },
            new GeneratedFile
            {
                Path = "stale/OldView.cs",
                Content = "// old"
            }
        ]);

        File.Exists(Path.Combine(staleDirectory.FullName, "OldView.cs")).Should().BeTrue();

        GeneratedFileWriter.Write(outputDirectory.FullName, "Hud",
        [
            new GeneratedFile
            {
                Path = "CurrentView.cs",
                Content = "// second"
            }
        ]);

        File.ReadAllText(Path.Combine(outputDirectory.FullName, "CurrentView.cs")).Should().Be("// second");
        File.Exists(Path.Combine(staleDirectory.FullName, "OldView.cs")).Should().BeFalse();
        Directory.Exists(staleDirectory.FullName).Should().BeFalse();
        File.ReadAllText(unrelatedPath).Should().Be("user-owned");
    }

    [Fact]
    public void Write_WithoutManifest_DeletesLegacySyntheticArtifactsThatAreNoLongerEmitted()
    {
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "legacy-output"));
        var staleSummaryPath = Path.Combine(outputDirectory.FullName, "QuestHud.synthetic-components.json");
        var staleComponentPath = Path.Combine(outputDirectory.FullName, "SyntheticContainerABCView.uxml");
        var unrelatedPath = Path.Combine(outputDirectory.FullName, "keep-me.txt");

        File.WriteAllText(staleSummaryPath, "{ }");
        File.WriteAllText(staleComponentPath, "<ui:UXML />");
        File.WriteAllText(unrelatedPath, "user-owned");

        GeneratedFileWriter.Write(outputDirectory.FullName, "QuestHud",
        [
            new GeneratedFile
            {
                Path = "QuestHudView.uxml",
                Content = "<ui:UXML />"
            }
        ]);

        File.Exists(staleSummaryPath).Should().BeFalse();
        File.Exists(staleComponentPath).Should().BeFalse();
        File.ReadAllText(unrelatedPath).Should().Be("user-owned");
    }

    [Fact]
    public void Write_MultipleScopes_PreserveEachOthersGeneratedFiles()
    {
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "scoped-output"));

        GeneratedFileWriter.Write(outputDirectory.FullName, "PartyStatusStrip",
        [
            new GeneratedFile
            {
                Path = "PartyStatusStripView.uxml",
                Content = "<ui:UXML />"
            }
        ]);

        GeneratedFileWriter.Write(outputDirectory.FullName, "QuestSidebar",
        [
            new GeneratedFile
            {
                Path = "QuestSidebarView.uxml",
                Content = "<ui:UXML />"
            }
        ]);

        File.Exists(Path.Combine(outputDirectory.FullName, "PartyStatusStripView.uxml")).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory.FullName, "QuestSidebarView.uxml")).Should().BeTrue();

        GeneratedFileWriter.Write(outputDirectory.FullName, "QuestSidebar",
        [
            new GeneratedFile
            {
                Path = "QuestSidebarView.uss",
                Content = ".quest-sidebar {}"
            }
        ]);

        File.Exists(Path.Combine(outputDirectory.FullName, "PartyStatusStripView.uxml")).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory.FullName, "QuestSidebarView.uxml")).Should().BeFalse();
        File.Exists(Path.Combine(outputDirectory.FullName, "QuestSidebarView.uss")).Should().BeTrue();
    }

    [Fact]
    public void Write_TurningOffVisualIr_RemovesStaleVisualIrArtifactForThatScope()
    {
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "visual-ir-output"));

        GeneratedFileWriter.Write(outputDirectory.FullName, "QuestHud",
        [
            new GeneratedFile
            {
                Path = "QuestHudView.tsx",
                Content = "// generated"
            },
            new GeneratedFile
            {
                Path = "QuestHud.visual-ir.json",
                Content = "{ }"
            },
            new GeneratedFile
            {
                Path = "QuestHud.synthetic-components.json",
                Content = "{ }"
            }
        ]);

        GeneratedFileWriter.Write(outputDirectory.FullName, "QuestHud",
        [
            new GeneratedFile
            {
                Path = "QuestHudView.tsx",
                Content = "// regenerated"
            },
            new GeneratedFile
            {
                Path = "QuestHud.synthetic-components.json",
                Content = "{ \"still\": true }"
            }
        ]);

        File.ReadAllText(Path.Combine(outputDirectory.FullName, "QuestHudView.tsx")).Should().Be("// regenerated");
        File.Exists(Path.Combine(outputDirectory.FullName, "QuestHud.visual-ir.json")).Should().BeFalse();
        File.Exists(Path.Combine(outputDirectory.FullName, "QuestHud.synthetic-components.json")).Should().BeTrue();
    }

    [Fact]
    public void Write_TurningOffVisualPlanningArtifacts_RemovesStaleArtifactsForThatScope()
    {
        var outputDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "visual-planning-output"));

        GeneratedFileWriter.Write(outputDirectory.FullName, "QuestHud",
        [
            new GeneratedFile
            {
                Path = "QuestHudView.tsx",
                Content = "// generated"
            },
            new GeneratedFile
            {
                Path = "QuestHud.visual-synthesis.json",
                Content = "{ }"
            },
            new GeneratedFile
            {
                Path = "QuestHud.visual-refinement.json",
                Content = "{ }"
            }
        ]);

        GeneratedFileWriter.Write(outputDirectory.FullName, "QuestHud",
        [
            new GeneratedFile
            {
                Path = "QuestHudView.tsx",
                Content = "// regenerated"
            }
        ]);

        File.ReadAllText(Path.Combine(outputDirectory.FullName, "QuestHudView.tsx")).Should().Be("// regenerated");
        File.Exists(Path.Combine(outputDirectory.FullName, "QuestHud.visual-synthesis.json")).Should().BeFalse();
        File.Exists(Path.Combine(outputDirectory.FullName, "QuestHud.visual-refinement.json")).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
