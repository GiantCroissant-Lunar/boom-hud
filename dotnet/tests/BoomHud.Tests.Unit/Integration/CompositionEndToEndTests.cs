using System.Collections.Generic;
using BoomHud.Abstractions.Diagnostics;
using BoomHud.Abstractions.IR;
using Xunit;

namespace BoomHud.Tests.Unit.Integration;

/// <summary>
/// End-to-end tests for multi-source composition.
/// These test the composition flow independent of the CLI.
/// </summary>
public class CompositionEndToEndTests
{
    [Fact]
    public void Compose_TwoValidDocuments_MergesSuccessfully()
    {
        // Arrange: Create two separate design documents with non-overlapping components
        var doc1 = new HudDocument
        {
            Name = "SharedComponents",
            Root = new ComponentNode { Type = ComponentType.Container },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["Button"] = new HudComponentDefinition
                {
                    Id = "button",
                    Name = "Button",
                    Root = new ComponentNode
                    {
                        Type = ComponentType.Button,
                        Style = new StyleSpec { Background = new Color(50, 50, 200) }
                    }
                },
                ["Label"] = new HudComponentDefinition
                {
                    Id = "label",
                    Name = "Label",
                    Root = new ComponentNode
                    {
                        Type = ComponentType.Label,
                        Style = new StyleSpec { Foreground = new Color(255, 255, 255) }
                    }
                }
            }
        };

        var doc2 = new HudDocument
        {
            Name = "GameHud",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children = [
                    new ComponentNode { Type = ComponentType.Label, Id = "health" },
                    new ComponentNode { Type = ComponentType.Label, Id = "score" }
                ]
            },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["StatusBar"] = new HudComponentDefinition
                {
                    Id = "status-bar",
                    Name = "StatusBar",
                    Root = new ComponentNode { Type = ComponentType.Container }
                }
            }
        };

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("shared.ir.json")),
            new(doc2, new SourceIdentity("game-hud.ir.json"))
        };

        // Act: Compose
        var result = MultiSourceComposer.Compose(sources);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal("SharedComponents", result.Document.Name); // First doc is root by default
        Assert.Equal(3, result.Document.Components.Count);
        Assert.Contains("Button", result.Document.Components.Keys);
        Assert.Contains("Label", result.Document.Components.Keys);
        Assert.Contains("StatusBar", result.Document.Components.Keys);
    }

    [Fact]
    public void Compose_WithRootOption_SelectsCorrectRoot()
    {
        // Arrange: Two documents
        var sharedDoc = new HudDocument
        {
            Name = "Shared",
            Root = new ComponentNode { Type = ComponentType.Container },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["Button"] = new HudComponentDefinition
                {
                    Id = "btn",
                    Name = "Button",
                    Root = new ComponentNode { Type = ComponentType.Button }
                }
            }
        };

        var mainDoc = new HudDocument
        {
            Name = "MainApp",
            Root = new ComponentNode
            {
                Id = "main-root",
                Type = ComponentType.Container,
                Children = [new ComponentNode { Type = ComponentType.Button, Id = "start-btn" }]
            },
            Components = new Dictionary<string, HudComponentDefinition>()
        };

        var sources = new List<SourcedDocument>
        {
            new(sharedDoc, new SourceIdentity("shared.ir.json")),
            new(mainDoc, new SourceIdentity("main.ir.json"))
        };

        // Act: Compose with explicit root
        var result = MultiSourceComposer.Compose(sources, "MainApp");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal("MainApp", result.Document.Name);
        Assert.Equal("main-root", result.Document.Root.Id);
    }

    [Fact]
    public void Compose_WithCollision_FailsWithBH0100()
    {
        // Arrange: Two documents with overlapping component names
        var doc1 = new HudDocument
        {
            Name = "Doc1",
            Root = new ComponentNode { Type = ComponentType.Container },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["Button"] = new HudComponentDefinition
                {
                    Id = "btn1",
                    Name = "Button",
                    Root = new ComponentNode { Type = ComponentType.Button }
                }
            }
        };

        var doc2 = new HudDocument
        {
            Name = "Doc2",
            Root = new ComponentNode { Type = ComponentType.Container },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                ["Button"] = new HudComponentDefinition  // Same name - collision!
                {
                    Id = "btn2",
                    Name = "Button",
                    Root = new ComponentNode { Type = ComponentType.Button }
                }
            }
        };

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("doc1.ir.json")),
            new(doc2, new SourceIdentity("doc2.ir.json"))
        };

        // Act
        var result = MultiSourceComposer.Compose(sources);

        // Assert: Must fail with BH0100
        Assert.False(result.Success);
        Assert.Null(result.Document);
        Assert.Single(result.Diagnostics);

        var diag = result.Diagnostics[0];
        Assert.Equal(DiagnosticCodes.ComponentCollision, diag.Code);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("Button", diag.Message);
        Assert.Contains("doc1.ir.json", diag.Message);
        Assert.Contains("doc2.ir.json", diag.Message);
    }

    [Fact]
    public void Compose_ThreeDocuments_WithMultipleCollisions_ReportsAll()
    {
        // Arrange: Three documents with multiple collisions
        var doc1 = CreateDocWithComponents("Doc1", "Header", "Footer", "Button");
        var doc2 = CreateDocWithComponents("Doc2", "Button", "Card");  // Button collides
        var doc3 = CreateDocWithComponents("Doc3", "Header", "Modal"); // Header collides

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("doc1.ir.json")),
            new(doc2, new SourceIdentity("doc2.ir.json")),
            new(doc3, new SourceIdentity("doc3.ir.json"))
        };

        // Act
        var result = MultiSourceComposer.Compose(sources);

        // Assert: Must fail with 2 BH0100 errors
        Assert.False(result.Success);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, d =>
        {
            Assert.Equal(DiagnosticCodes.ComponentCollision, d.Code);
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        });

        // Check we got errors for both collisions
        var buttonCollision = result.Diagnostics.Any(d => d.Message.Contains("Button"));
        var headerCollision = result.Diagnostics.Any(d => d.Message.Contains("Header"));
        Assert.True(buttonCollision);
        Assert.True(headerCollision);
    }

    [Fact]
    public void Compose_CaseInsensitive_DetectsCollision()
    {
        // Arrange: Component names differ only by case
        var doc1 = CreateDocWithComponents("Doc1", "Button");
        var doc2 = CreateDocWithComponents("Doc2", "button");  // Same component, different case

        var sources = new List<SourcedDocument>
        {
            new(doc1, new SourceIdentity("doc1.ir.json")),
            new(doc2, new SourceIdentity("doc2.ir.json"))
        };

        // Act
        var result = MultiSourceComposer.Compose(sources);

        // Assert: Must detect as collision (case-insensitive)
        Assert.False(result.Success);
        Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCodes.ComponentCollision, result.Diagnostics[0].Code);
    }

    private static HudDocument CreateDocWithComponents(string name, params string[] componentNames)
    {
        var components = new Dictionary<string, HudComponentDefinition>();
        foreach (var cn in componentNames)
        {
            components[cn] = new HudComponentDefinition
            {
                Id = cn.ToLowerInvariant(),
                Name = cn,
                Root = new ComponentNode { Type = ComponentType.Container }
            };
        }

        return new HudDocument
        {
            Name = name,
            Root = new ComponentNode { Type = ComponentType.Container },
            Components = components
        };
    }
}
