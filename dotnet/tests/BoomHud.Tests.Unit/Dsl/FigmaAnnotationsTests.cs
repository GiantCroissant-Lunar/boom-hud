using System;
using BoomHud.Abstractions.IR;
using BoomHud.Dsl.Figma;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Dsl;

public class FigmaAnnotationsTests
{
    private readonly FigmaParser _parser = new();

    [Fact]
    public void Apply_TextBindingAnnotation_BindsTextProperty()
    {
        var json = """
            {
              "name": "Test File",
              "document": {
                "id": "0:0",
                "type": "DOCUMENT",
                "children": [
                  {
                    "id": "0:1",
                    "type": "CANVAS",
                    "children": [
                      {
                        "id": "1:1",
                        "name": "Status Bar",
                        "type": "FRAME",
                        "children": [
                          {
                            "id": "1:2",
                            "name": "Left Group",
                            "type": "FRAME",
                            "children": [
                              {
                                "id": "1:3",
                                "name": "Template Value",
                                "type": "TEXT",
                                "characters": "(none)"
                              }
                            ]
                          }
                        ]
                      }
                    ]
                  }
                ]
              }
            }
            """;

        var doc = _parser.Parse(json);

        var annotations = FigmaAnnotations.Parse("""
            {
              "nodes": [
                {
                  "match": { "path": ["statusBar", "leftGroup", "templateValue"] },
                  "bindings": { "text": "Template" }
                }
              ]
            }
            """);

        var updated = FigmaAnnotations.Apply(doc, annotations);

        var templateValue = FindNodeById(updated.Root, "templateValue");
        templateValue.Should().NotBeNull();

        templateValue!.Properties.Should().ContainKey("text");
        templateValue.Properties["text"].IsBound.Should().BeTrue();
        templateValue.Properties["text"].BindingPath.Should().Be("Template");

        templateValue.Bindings.Should().ContainSingle(b => b.Property == "text" && b.Path == "Template");
    }

    [Fact]
    public void Apply_TypeOverrideAnnotation_ChangesComponentType()
    {
        var json = """
            {
              "name": "Test File",
              "document": {
                "id": "0:0",
                "type": "DOCUMENT",
                "children": [
                  {
                    "id": "0:1",
                    "type": "CANVAS",
                    "children": [
                      {
                        "id": "1:1",
                        "name": "Menu Bar",
                        "type": "FRAME",
                        "children": [
                          {
                            "id": "1:2",
                            "name": "Left Group",
                            "type": "FRAME"
                          }
                        ]
                      }
                    ]
                  }
                ]
              }
            }
            """;

        var doc = _parser.Parse(json);

        var annotations = FigmaAnnotations.Parse("""
            {
              "nodes": [
                {
                  "match": { "path": ["menuBar", "leftGroup"] },
                  "set": { "type": "Menu" }
                }
              ]
            }
            """);

        var updated = FigmaAnnotations.Apply(doc, annotations);

        var leftGroup = FindNodeById(updated.Root, "leftGroup");
        leftGroup.Should().NotBeNull();
        leftGroup!.Type.Should().Be(ComponentType.Menu);
    }

    private static ComponentNode? FindNodeById(ComponentNode node, string id)
    {
        if (string.Equals(node.Id, id, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var found = FindNodeById(child, id);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
