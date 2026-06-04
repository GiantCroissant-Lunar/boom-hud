using System.Text.Json.Nodes;
using BoomHud.Abstractions.Runtime;
using BoomHud.Godot.Runtime;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Runtime;

public sealed class RuntimeValueResolverTests
{
    [Fact]
    public void ResolveText_LiteralString_ReturnsLiteral()
    {
        var properties = new Dictionary<string, RuntimeValue>
        {
            ["text"] = new RuntimeValue { Literal = JsonValue.Create("ready") }
        };

        var text = RuntimeValueResolver.ResolveText(properties, "text", dataModel: null);

        text.Should().Be("ready");
    }

    [Fact]
    public void ResolveText_Binding_ReturnsDataModelValue()
    {
        var data = JsonNode.Parse("""{ "agent": { "status": "running" } }""")!.AsObject();
        var properties = new Dictionary<string, RuntimeValue>
        {
            ["text"] = new RuntimeValue
            {
                Binding = new RuntimeBinding
                {
                    Path = "/agent/status",
                }
            }
        };

        var text = RuntimeValueResolver.ResolveText(properties, "text", data);

        text.Should().Be("running");
    }

    [Fact]
    public void ResolveText_BindingWithFallback_ReturnsFallbackWhenPathMissing()
    {
        var data = JsonNode.Parse("""{ "agent": {} }""")!.AsObject();
        var properties = new Dictionary<string, RuntimeValue>
        {
            ["text"] = new RuntimeValue
            {
                Binding = new RuntimeBinding
                {
                    Path = "/agent/status",
                    Fallback = JsonValue.Create("unknown"),
                }
            }
        };

        var text = RuntimeValueResolver.ResolveText(properties, "text", data);

        text.Should().Be("unknown");
    }

    [Fact]
    public void ResolveText_BindingWithFormat_AppliesInvariantFormat()
    {
        var data = JsonNode.Parse("""{ "progress": 0.75 }""")!.AsObject();
        var properties = new Dictionary<string, RuntimeValue>
        {
            ["text"] = new RuntimeValue
            {
                Binding = new RuntimeBinding
                {
                    Path = "/progress",
                    Format = "Progress {0:P0}",
                }
            }
        };

        var text = RuntimeValueResolver.ResolveText(properties, "text", data);

        text.Should().Be("Progress 75 %");
    }

    [Fact]
    public void ResolveStringList_ArrayValue_ReturnsTextItems()
    {
        var data = JsonNode.Parse("""{ "items": ["one", "two"] }""")!.AsObject();
        var properties = new Dictionary<string, RuntimeValue>
        {
            ["items"] = new RuntimeValue
            {
                Binding = new RuntimeBinding
                {
                    Path = "/items",
                }
            }
        };

        var items = RuntimeValueResolver.ResolveStringList(properties, "items", data);

        items.Should().Equal("one", "two");
    }
}

