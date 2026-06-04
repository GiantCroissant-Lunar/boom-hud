using System.Text.Json;
using BoomHud.Abstractions.Runtime;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Runtime;

public sealed class RuntimeSurfaceValidatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    [Fact]
    public void Validate_SampleRuntimeSurface_IsValid()
    {
        var document = LoadSampleSurface();

        var result = RuntimeSurfaceValidator.Validate(document, RuntimeSurfaceCatalog.Basic);

        result.IsValid.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        document.Root.Children.Should().HaveCount(4);
    }

    [Fact]
    public void Validate_UnknownComponentType_ReturnsCatalogError()
    {
        var document = LoadSampleSurface() with
        {
            Root = LoadSampleSurface().Root with
            {
                Type = "godotNode3D"
            }
        };

        var result = RuntimeSurfaceValidator.Validate(document, RuntimeSurfaceCatalog.Basic);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "BHRT013");
    }

    [Fact]
    public void Validate_DuplicateComponentIds_ReturnsDuplicateError()
    {
        var document = LoadSampleSurface();
        var duplicate = document with
        {
            Root = document.Root with
            {
                Children =
                [
                    document.Root.Children[0],
                    document.Root.Children[1] with { Id = document.Root.Children[0].Id }
                ]
            }
        };

        var result = RuntimeSurfaceValidator.Validate(duplicate, RuntimeSurfaceCatalog.Basic);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "BHRT011");
    }

    [Fact]
    public void Validate_InvalidBindingPointer_ReturnsPointerError()
    {
        var document = LoadSampleSurface();
        var invalid = document with
        {
            Root = document.Root with
            {
                Properties = new Dictionary<string, RuntimeValue>
                {
                    ["title"] = new()
                    {
                        Binding = new RuntimeBinding
                        {
                            Path = "summary/title"
                        }
                    }
                }
            }
        };

        var result = RuntimeSurfaceValidator.Validate(invalid, RuntimeSurfaceCatalog.Basic);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "BHRT042");
    }

    [Fact]
    public void Validate_ActionOnNonInteractiveComponent_ReturnsEventError()
    {
        var document = LoadSampleSurface();
        var invalid = document with
        {
            Root = document.Root with
            {
                Children =
                [
                    document.Root.Children[1] with
                    {
                        Actions =
                        [
                            new RuntimeActionDescriptor
                            {
                                Event = "pressed",
                                Command = "agent.task.get"
                            }
                        ]
                    }
                ]
            }
        };

        var result = RuntimeSurfaceValidator.Validate(invalid, RuntimeSurfaceCatalog.Basic);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "BHRT051");
    }

    [Fact]
    public void Validate_DataModelUpdate_RequiresMatchingSurfaceAndJsonPointer()
    {
        var update = new RuntimeDataModelUpdate
        {
            SurfaceId = "other.surface",
            Path = "/summary/progress",
            Operation = RuntimeDataModelOperations.Replace
        };

        var result = RuntimeSurfaceValidator.Validate(update, expectedSurfaceId: "orc.task.summary");

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "BHRT004");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/summary/title")]
    [InlineData("/escaped~0tilde")]
    [InlineData("/escaped~1slash")]
    public void IsValidJsonPointer_ValidPointers_ReturnTrue(string path)
    {
        RuntimeSurfaceValidator.IsValidJsonPointer(path).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("summary/title")]
    [InlineData("/bad~escape")]
    [InlineData("/bad~")]
    public void IsValidJsonPointer_InvalidPointers_ReturnFalse(string? path)
    {
        RuntimeSurfaceValidator.IsValidJsonPointer(path).Should().BeFalse();
    }

    private static RuntimeSurfaceDocument LoadSampleSurface()
    {
        var samplePath = FindRepoFile("samples/runtime/orc-agent-summary.surface.json");
        var document = JsonSerializer.Deserialize<RuntimeSurfaceDocument>(File.ReadAllText(samplePath), JsonOptions);

        document.Should().NotBeNull();
        return document!;
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                return path;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file '{relativePath}'.", relativePath);
    }
}
