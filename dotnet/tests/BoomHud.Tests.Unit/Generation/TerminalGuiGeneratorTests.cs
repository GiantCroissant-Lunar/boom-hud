using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Gen.TerminalGui;
using FluentAssertions;
using System.Text.RegularExpressions;
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
    public void Generate_WithViewModelNamespace_UsesThatNamespaceInViewFiles()
    {
        var options = _options with { ViewModelNamespace = "FantaSim.Hud.Contracts" };

        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("using FantaSim.Hud.Contracts;");
    }

    [Fact]
    public void Generate_WithNoViewModelInterfaces_SuppressesInterfaceFile()
    {
        var options = _options with { EmitViewModelInterfaces = false };

        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);

        result.Files.Should().Contain(f => f.Path == "TestView.g.cs");
        result.Files.Should().NotContain(f => f.Path == "ITestViewModel.g.cs");
    }

    [Fact]
    public void Generate_WithCompose_EmitsComposeFile_AndUsesSlotKeyLookup()
    {
        var options = _options with
        {
            EmitCompose = true,
            EmitViewModelInterfaces = false,
            ViewModelNamespace = "FantaSim.Hud.Contracts"
        };

        var componentId = "component:Child";
        var doc = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode
                    {
                        Type = ComponentType.Container,
                        ComponentRefId = componentId,
                        SlotKey = "slot.child"
                    }
                ]
            },
            Components = new Dictionary<string, HudComponentDefinition>
            {
                [componentId] = new HudComponentDefinition
                {
                    Id = componentId,
                    Name = "Child",
                    Root = new ComponentNode { Type = ComponentType.Container }
                }
            }
        };

        var result = _generator.Generate(doc, options);

        result.Files.Should().Contain(f => f.Path == "RootView.Compose.g.cs");
        var composeFile = result.Files.First(f => f.Path == "RootView.Compose.g.cs");

        composeFile.Content.Should().Contain("FindSlot<ChildView>(\"slot.child\")");
        composeFile.Content.Should().Contain("\"slot.child\"");
        composeFile.Content.Should().Contain("resolver.Resolve<IChildViewModel>");
    }

    [Fact]
    public void Generate_EmbedsDriftIds_InView()
    {
        var options = _options with
        {
            ContractId = "contract:test",
            EmitViewModelInterfaces = false
        };

        var doc = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);
        var viewFile = result.Files.First(f => f.Path == "RootView.g.cs");

        viewFile.Content.Should().Contain("public const string BoomHudSourceId = \"sha256:");
        viewFile.Content.Should().Contain("public const string BoomHudContractId = \"contract:test\";");
    }

    [Fact]
    public void Generate_SourceId_ChangesWhenDocumentChanges()
    {
        var options = _options with { EmitViewModelInterfaces = false };

        var docA = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode { Id = "a", Type = ComponentType.Label }
                ]
            }
        };

        var docB = new HudDocument
        {
            Name = "Root",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Children =
                [
                    new ComponentNode { Id = "a", Type = ComponentType.Label },
                    new ComponentNode { Id = "b", Type = ComponentType.Label }
                ]
            }
        };

        var fileA = _generator.Generate(docA, options).Files.First(f => f.Path == "RootView.g.cs");
        var fileB = _generator.Generate(docB, options).Files.First(f => f.Path == "RootView.g.cs");

        var idA = ExtractConst(fileA.Content, "BoomHudSourceId");
        var idB = ExtractConst(fileB.Content, "BoomHudSourceId");

        idA.Should().StartWith("sha256:");
        idB.Should().StartWith("sha256:");
        idA.Should().NotBe(idB);
    }

    private static string ExtractConst(string content, string constName)
    {
        var m = Regex.Match(content, $@"public const string\s+{Regex.Escape(constName)}\s*=\s*""([^""]+)""", RegexOptions.CultureInvariant);
        m.Success.Should().BeTrue($"Expected to find const '{constName}' in generated output.");
        return m.Groups[1].Value;
    }

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
        viewFile.Content.Should().Contain("public partial class MyHudView : Terminal.Gui.ViewBase.View");
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
    public void Generate_WithMenuBar_CreatesMenuBarComponent()
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
                        Id = "menuBar1",
                        Type = ComponentType.MenuBar
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("private MenuBar _menuBar1");
        viewFile.Content.Should().Contain("_menuBar1 = new MenuBar()");
    }

    [Fact]
    public void Generate_WithMenuHierarchy_CreatesMenuAndMenuItemComponents()
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
                        Id = "menuBar1",
                        Type = ComponentType.MenuBar,
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "fileMenu",
                                Type = ComponentType.Menu,
                                Children =
                                [
                                    new ComponentNode
                                    {
                                        Id = "openItem",
                                        Type = ComponentType.MenuItem
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("private Menu _fileMenu");
        viewFile.Content.Should().Contain("private MenuItem _openItem");
        viewFile.Content.Should().Contain("_fileMenu = new Menu()");
        viewFile.Content.Should().Contain("_openItem = new MenuItem()");
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
        viewFile.Content.Should().Contain("SetScheme(new Scheme");
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
    public void Generate_AccessorNameCollision_WithViewTitle_IsRenamed()
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
                        Id = "title",
                        Type = ComponentType.Label
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("private Label _title = null!;");
        viewFile.Content.Should().Contain("public Label TitleComponent => _title;");
        viewFile.Content.Should().NotContain("public Label Title => _title;");
    }

    [Fact]
    public void Generate_FieldNameCollision_WithViewModelField_IsRenamed()
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
                        Id = "viewModel",
                        Type = ComponentType.Label,
                        Bindings =
                        [
                            new BindingSpec
                            {
                                Property = "text",
                                Path = "Name"
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var viewFile = result.Files.First(f => f.Path.EndsWith("View.g.cs", StringComparison.Ordinal));
        viewFile.Content.Should().Contain("private Label _viewModel2 = null!;");
        viewFile.Content.Should().Contain("public Label ViewModelComponent => _viewModel2;");
        viewFile.Content.Should().Contain("_viewModel2.Text = _viewModel.Name");
        viewFile.Content.Should().NotContain("private Label _viewModel = null!;");
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
