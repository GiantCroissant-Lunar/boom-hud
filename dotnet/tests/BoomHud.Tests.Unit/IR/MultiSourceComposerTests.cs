using System.Collections.Generic;
using BoomHud.Abstractions.Diagnostics;
using BoomHud.Abstractions.IR;
using Xunit;

namespace BoomHud.Tests.Unit.IR;

public class MultiSourceComposerTests
{
    private static HudDocument CreateDoc(string name, params string[] componentNames)
    {
        var components = new Dictionary<string, HudComponentDefinition>();
        foreach (var cn in componentNames)
        {
            components[cn] = new HudComponentDefinition
            {
                Id = cn,
                Name = cn,
                Root = new ComponentNode { Type = ComponentType.Container }
            };
        }

        return new HudDocument
        {
            Name = name,
            Root = new ComponentNode { Type = ComponentType.Container },
            Components = components,
            Styles = new Dictionary<string, StyleSpec>()
        };
    }

    [Fact]
    public void SingleSource_ReturnsDocumentUnchanged()
    {
        var doc = CreateDoc("doc1", "Button", "Label");
        var source = new SourcedDocument(doc, new SourceIdentity("file1.ir.json"));

        var result = MultiSourceComposer.Compose([source]);

        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal("doc1", result.Document.Name);
        Assert.Equal(2, result.Document.Components.Count);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void TwoSources_NoCollision_MergesComponents()
    {
        var doc1 = CreateDoc("doc1", "Button", "Label");
        var doc2 = CreateDoc("doc2", "Card", "Input");

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("file1.ir.json")),
            new(doc2, new SourceIdentity("file2.ir.json"))
        };

        var result = MultiSourceComposer.Compose(sources);

        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal(4, result.Document.Components.Count);
        Assert.Contains("Button", result.Document.Components.Keys);
        Assert.Contains("Label", result.Document.Components.Keys);
        Assert.Contains("Card", result.Document.Components.Keys);
        Assert.Contains("Input", result.Document.Components.Keys);
    }

    [Fact]
    public void TwoSources_WithCollision_ReturnsBH0100Error()
    {
        var doc1 = CreateDoc("doc1", "Button", "Label");
        var doc2 = CreateDoc("doc2", "Button", "Card"); // Button is duplicate

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("file1.ir.json")),
            new(doc2, new SourceIdentity("file2.ir.json"))
        };

        var result = MultiSourceComposer.Compose(sources);

        Assert.False(result.Success);
        Assert.Null(result.Document);
        Assert.Single(result.Diagnostics);

        var diag = result.Diagnostics[0];
        Assert.Equal(DiagnosticCodes.ComponentCollision, diag.Code);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("Button", diag.Message);
        Assert.Contains("file1.ir.json", diag.Message);
        Assert.Contains("file2.ir.json", diag.Message);
    }

    [Fact]
    public void ThreeSources_MultipleCollisions_ReportsAll()
    {
        var doc1 = CreateDoc("doc1", "Button", "Label");
        var doc2 = CreateDoc("doc2", "Button", "Card"); // Button collides
        var doc3 = CreateDoc("doc3", "Label", "Dialog"); // Label collides

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("file1.ir.json")),
            new(doc2, new SourceIdentity("file2.ir.json")),
            new(doc3, new SourceIdentity("file3.ir.json"))
        };

        var result = MultiSourceComposer.Compose(sources);

        Assert.False(result.Success);
        Assert.Null(result.Document);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, d =>
        {
            Assert.Equal(DiagnosticCodes.ComponentCollision, d.Code);
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        });
    }

    [Fact]
    public void RootComponentOption_SelectsCorrectDocument()
    {
        var doc1 = CreateDoc("MainApp", "Header");
        var doc2 = CreateDoc("Settings", "SettingsPanel");

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("main.ir.json")),
            new(doc2, new SourceIdentity("settings.ir.json"))
        };

        // Select Settings as root
        var result = MultiSourceComposer.Compose(sources, "Settings");

        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal("Settings", result.Document.Name);
        Assert.Equal(2, result.Document.Components.Count);
    }

    [Fact]
    public void RootComponentOption_NotFound_ReturnsError()
    {
        var doc1 = CreateDoc("doc1", "Button");
        var doc2 = CreateDoc("doc2", "Card");

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("file1.ir.json")),
            new(doc2, new SourceIdentity("file2.ir.json"))
        };

        var result = MultiSourceComposer.Compose(sources, "NonExistent");

        Assert.False(result.Success);
        Assert.Null(result.Document);
        Assert.Single(result.Diagnostics);
        Assert.Equal("BH0111", result.Diagnostics[0].Code);
        Assert.Contains("NonExistent", result.Diagnostics[0].Message);
    }

    [Fact]
    public void CollisionDetection_IsCaseInsensitive()
    {
        var doc1 = CreateDoc("doc1", "Button");
        var doc2 = CreateDoc("doc2", "button"); // Same name, different case

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("file1.ir.json")),
            new(doc2, new SourceIdentity("file2.ir.json"))
        };

        var result = MultiSourceComposer.Compose(sources);

        Assert.False(result.Success);
        Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCodes.ComponentCollision, result.Diagnostics[0].Code);
    }

    [Fact]
    public void EmptySources_ReturnsError()
    {
        var result = MultiSourceComposer.Compose([]);

        Assert.False(result.Success);
        Assert.Null(result.Document);
        Assert.Single(result.Diagnostics);
        Assert.Equal("BH0099", result.Diagnostics[0].Code);
    }

    [Fact]
    public void StyleCollision_WarnsButDoesNotFail()
    {
        var doc1 = new HudDocument
        {
            Name = "doc1",
            Root = new ComponentNode { Type = ComponentType.Container },
            Components = new Dictionary<string, HudComponentDefinition>(),
            Styles = new Dictionary<string, StyleSpec>
            {
                ["MyStyle"] = new StyleSpec { Foreground = new Color(255, 0, 0) }
            }
        };

        var doc2 = new HudDocument
        {
            Name = "doc2",
            Root = new ComponentNode { Type = ComponentType.Container },
            Components = new Dictionary<string, HudComponentDefinition>(),
            Styles = new Dictionary<string, StyleSpec>
            {
                ["MyStyle"] = new StyleSpec { Foreground = new Color(0, 255, 0) }
            }
        };

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("file1.ir.json")),
            new(doc2, new SourceIdentity("file2.ir.json"))
        };

        var result = MultiSourceComposer.Compose(sources);

        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Single(result.Diagnostics);
        Assert.Equal("BH0110", result.Diagnostics[0].Code);
        Assert.Equal(DiagnosticSeverity.Warning, result.Diagnostics[0].Severity);

        // Verify enhanced message format with winner/loser and suggestion
        var message = result.Diagnostics[0].Message;
        Assert.Contains("winner", message);
        Assert.Contains("loser", message);
        Assert.Contains("file1.ir.json", message);  // winner
        Assert.Contains("file2.ir.json", message);  // loser
        Assert.Contains("Consider defining styles inside components", message);
    }
}
