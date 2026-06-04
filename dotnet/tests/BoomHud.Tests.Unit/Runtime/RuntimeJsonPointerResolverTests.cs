using System.Text.Json.Nodes;
using BoomHud.Godot.Runtime;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Runtime;

public sealed class RuntimeJsonPointerResolverTests
{
    [Fact]
    public void TryResolve_ObjectPath_ReturnsNestedValue()
    {
        var data = JsonNode.Parse(
            """
            {
              "agent": {
                "summary": "Done"
              }
            }
            """);

        var found = RuntimeJsonPointerResolver.TryResolve(data, "/agent/summary", out var value);

        found.Should().BeTrue();
        value!.GetValue<string>().Should().Be("Done");
    }

    [Fact]
    public void TryResolve_ArrayIndex_ReturnsArrayItem()
    {
        var data = JsonNode.Parse(
            """
            {
              "items": ["first", "second"]
            }
            """);

        var found = RuntimeJsonPointerResolver.TryResolve(data, "/items/1", out var value);

        found.Should().BeTrue();
        value!.GetValue<string>().Should().Be("second");
    }

    [Fact]
    public void TryResolve_EscapedPathSegments_ReturnsEscapedProperty()
    {
        var data = JsonNode.Parse(
            """
            {
              "a/b": {
                "c~d": 42
              }
            }
            """);

        var found = RuntimeJsonPointerResolver.TryResolve(data, "/a~1b/c~0d", out var value);

        found.Should().BeTrue();
        value!.GetValue<int>().Should().Be(42);
    }

    [Fact]
    public void TryResolve_MissingPath_ReturnsFalse()
    {
        var data = JsonNode.Parse("""{ "agent": {} }""");

        var found = RuntimeJsonPointerResolver.TryResolve(data, "/agent/status", out var value);

        found.Should().BeFalse();
        value.Should().BeNull();
    }
}

