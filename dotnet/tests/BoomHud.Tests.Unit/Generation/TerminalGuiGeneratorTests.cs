using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Gen.TerminalGui;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public class TerminalGuiGeneratorTests
{
    private readonly TerminalGuiGenerator _generator = new();
    private readonly GenerationOptions _options = new()
    {
        Namespace = "TestNamespace",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void Generate_MinimalDocument_ProducesViewAndViewModelFiles()
    {
        var doc = new HudDocument
        {
            Name = "TestComponent",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().HaveCount(2);
        result.Files.Should().Contain(f => f.Path == "TestComponentView.g.cs");
        result.Files.Should().Contain(f => f.Path == "ITestComponentViewModel.g.cs");
    }

    [Fact]
    public void Generate_ViewClass_ContainsCorrectNamespace()
    {
        var doc = new HudDocument
        {
            Name = "MyHud",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("namespace TestNamespace;");
    }

    [Fact]
    public void Generate_ViewClass_ExtendsView()
    {
        var doc = new HudDocument
        {
            Name = "MyHud",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("public partial class MyHudView : View");
    }

    [Fact]
    public void Generate_WithLabel_CreatesLabelComponent()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "myLabel",
                        Type = ComponentType.Label
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("private Label _myLabel");
        viewFile.Content.Should().Contain("_myLabel = new Label()");
        viewFile.Content.Should().Contain("_myLabel.Id = \"myLabel\"");
    }

    [Fact]
    public void Generate_WithButton_CreatesButtonComponent()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "submitBtn",
                        Type = ComponentType.Button
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("private Button _submitBtn");
        viewFile.Content.Should().Contain("_submitBtn = new Button()");
    }

    [Fact]
    public void Generate_WithProgressBar_CreatesProgressBarComponent()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "healthBar",
                        Type = ComponentType.ProgressBar
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("private ProgressBar _healthBar");
        viewFile.Content.Should().Contain("_healthBar = new ProgressBar()");
    }

    [Fact]
    public void Generate_WithLayout_SetsWidthAndHeight()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "panel",
                        Type = ComponentType.Panel,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Pixels(100),
                            Height = Dimension.Pixels(50)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("_panel.Width = Dim.Absolute(100)");
        viewFile.Content.Should().Contain("_panel.Height = Dim.Absolute(50)");
    }

    [Fact]
    public void Generate_WithPercentDimension_UsesDimPercent()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "panel",
                        Type = ComponentType.Panel,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Percent(50)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("_panel.Width = Dim.Percent(50f)");
    }

    [Fact]
    public void Generate_WithFillDimension_UsesDimFill()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "panel",
                        Type = ComponentType.Panel,
                        Layout = new LayoutSpec
                        {
                            Width = Dimension.Fill
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("_panel.Width = Dim.Fill()");
    }

    [Fact]
    public void Generate_WithBinding_GeneratesRefreshCode()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "nameLabel",
                        Type = ComponentType.Label,
                        Bindings =
                        [
                            new BindingSpec
                            {
                                Property = "text",
                                Path = "Player.Name"
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("public void RefreshBindings()");
        viewFile.Content.Should().Contain("_nameLabel.Text = _viewModel.PlayerName");
    }

    [Fact]
    public void Generate_WithBindingFormat_GeneratesStringFormat()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "healthLabel",
                        Type = ComponentType.Label,
                        Bindings =
                        [
                            new BindingSpec
                            {
                                Property = "text",
                                Path = "Player.Health",
                                Format = "{0} HP"
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("string.Format(\"{0} HP\"");
    }

    [Fact]
    public void Generate_WithProgressBarBinding_GeneratesFractionAssignment()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "healthBar",
                        Type = ComponentType.ProgressBar,
                        Bindings =
                        [
                            new BindingSpec
                            {
                                Property = "value",
                                Path = "Player.HealthPercent"
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("_healthBar.Fraction = Convert.ToSingle");
    }

    [Fact]
    public void Generate_ViewModelInterface_ContainsBindingProperties()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "label",
                        Type = ComponentType.Label,
                        Bindings =
                        [
                            new BindingSpec { Property = "text", Path = "Player.Name" },
                            new BindingSpec { Property = "visible", Path = "Player.IsAlive" }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var vmFile = result.Files.First(f => f.Path.Contains("ViewModel"));
        vmFile.Content.Should().Contain("interface ITestViewModel");
        vmFile.Content.Should().Contain("object? PlayerName { get; }");
        vmFile.Content.Should().Contain("object? PlayerIsAlive { get; }");
    }

    [Fact]
    public void Generate_WithStyle_SetsColorScheme()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "label",
                        Type = ComponentType.Label,
                        Style = new StyleSpec
                        {
                            Foreground = Color.Red
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("ColorScheme");
        viewFile.Content.Should().Contain("Color.Red");
    }

    [Fact]
    public void Generate_WithNestedChildren_GeneratesHierarchy()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "parent",
                        Type = ComponentType.Container,
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "child",
                                Type = ComponentType.Label
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("this.Add(_parent)");
        viewFile.Content.Should().Contain("_parent.Add(_child)");
    }

    [Fact]
    public void Generate_WithStaticValue_SetsTextProperty()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "icon",
                        Type = ComponentType.Icon,
                        Properties = new Dictionary<string, BindableValue<object?>>
                        {
                            ["value"] = new() { Value = "❤️" }
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("_icon.Text = \"❤️\"");
    }

    [Fact]
    public void Generate_WithComments_IncludesAutoGeneratedHeader()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("<auto-generated>");
        viewFile.Content.Should().Contain("Generated by BoomHud.Gen.TerminalGui");
    }

    [Fact]
    public void Generate_WithNullableAnnotations_IncludesNullableDirective()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("#nullable enable");
    }

    [Fact]
    public void Generate_ComponentAccessors_ArePublic()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Id = "myButton",
                        Type = ComponentType.Button
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("public Button MyButton => _myButton");
    }

    [Fact]
    public void Generate_ViewModelProperty_HasRefreshBindingsCall()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("public ITestViewModel? ViewModel");
        viewFile.Content.Should().Contain("RefreshBindings()");
    }

    [Fact]
    public void TargetFramework_ReturnsTerminalGui()
    {
        _generator.TargetFramework.Should().Be("Terminal.Gui");
    }

    [Fact]
    public void Capabilities_ReturnsTerminalGuiCapabilities()
    {
        _generator.Capabilities.Should().BeOfType<TerminalGuiCapabilities>();
    }
}
