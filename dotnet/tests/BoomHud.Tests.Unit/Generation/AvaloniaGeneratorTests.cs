using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Gen.Avalonia;
using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace BoomHud.Tests.Unit.Generation;

public class AvaloniaGeneratorTests
{
    private readonly AvaloniaGenerator _generator = new();
    private readonly GenerationOptions _options = new()
    {
        Namespace = "TestNamespace",
        IncludeComments = true,
        UseNullableAnnotations = true
    };

    [Fact]
    public void Generate_WithViewModelNamespace_UsesThatNamespaceInAxaml()
    {
        var options = _options with { ViewModelNamespace = "FantaSim.Hud.Contracts" };

        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, options);

        var axamlFile = result.Files.First(f => f.Path == "TestView.axaml");
        axamlFile.Content.Should().Contain("xmlns:vm=\"clr-namespace:FantaSim.Hud.Contracts\"");
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

        result.Files.Should().Contain(f => f.Path == "TestView.axaml");
        result.Files.Should().Contain(f => f.Path == "TestView.axaml.cs");
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

        composeFile.Content.Should().Contain("using FantaSim.Hud.Contracts;");
        composeFile.Content.Should().Contain("FindControl<global::Avalonia.Controls.Control>(\"slot_child\")");
        composeFile.Content.Should().Contain("resolver.Resolve<IChildViewModel>");
        composeFile.Content.Should().Contain("\"slot.child\"");
    }

    [Fact]
    public void Generate_EmbedsDriftIds_InCodeBehind()
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
        var codeBehind = result.Files.First(f => f.Path == "RootView.axaml.cs");

        codeBehind.Content.Should().Contain("public const string BoomHudSourceId = \"sha256:");
        codeBehind.Content.Should().Contain("public const string BoomHudContractId = \"contract:test\";");
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

        var fileA = _generator.Generate(docA, options).Files.First(f => f.Path == "RootView.axaml.cs");
        var fileB = _generator.Generate(docB, options).Files.First(f => f.Path == "RootView.axaml.cs");

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
    public void Generate_MinimalDocument_ProducesThreeFiles()
    {
        var doc = new HudDocument
        {
            Name = "TestComponent",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        result.Success.Should().BeTrue();
        result.Files.Should().HaveCount(3);
        result.Files.Should().Contain(f => f.Path == "TestComponentView.axaml");
        result.Files.Should().Contain(f => f.Path == "TestComponentView.axaml.cs");
        result.Files.Should().Contain(f => f.Path == "ITestComponentViewModel.g.cs");
    }

    [Fact]
    public void Generate_AxamlFile_ContainsUserControlRoot()
    {
        var doc = new HudDocument
        {
            Name = "MyHud",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("<UserControl");
        axamlFile.Content.Should().Contain("xmlns=\"https://github.com/avaloniaui\"");
        axamlFile.Content.Should().Contain("x:Class=\"TestNamespace.MyHudView\"");
    }

    [Fact]
    public void Generate_WithLabel_CreatesTextBlock()
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

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("<TextBlock");
        axamlFile.Content.Should().Contain("x:Name=\"myLabel\"");
    }

    [Fact]
    public void Generate_WithButton_CreatesButtonElement()
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

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("<Button");
        axamlFile.Content.Should().Contain("x:Name=\"submitBtn\"");
    }

    [Fact]
    public void Generate_WithProgressBar_CreatesProgressBarElement()
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

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("<ProgressBar");
        axamlFile.Content.Should().Contain("x:Name=\"healthBar\"");
        axamlFile.Content.Should().Contain("Minimum=\"0\"");
        axamlFile.Content.Should().Contain("Maximum=\"1\"");
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

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("Width=\"100\"");
        axamlFile.Content.Should().Contain("Height=\"50\"");
    }

    [Fact]
    public void Generate_WithHorizontalLayout_UsesStackPanelHorizontal()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec { Type = LayoutType.Horizontal }
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("StackPanel Orientation=\"Horizontal\"");
    }

    [Fact]
    public void Generate_WithVerticalLayout_UsesStackPanelVertical()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec { Type = LayoutType.Vertical }
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("StackPanel Orientation=\"Vertical\"");
    }

    [Fact]
    public void Generate_WithGridLayout_UsesGrid()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec { Type = LayoutType.Grid }
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("<Grid");
    }

    [Fact]
    public void Generate_WithDockLayout_UsesDockPanel()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec { Type = LayoutType.Dock }
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("<DockPanel");
    }

    [Fact]
    public void Generate_WithBinding_GeneratesBindingExpression()
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

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("{Binding Player.Name}");
    }

    [Fact]
    public void Generate_WithTwoWayBinding_IncludesMode()
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
                        Id = "input",
                        Type = ComponentType.TextInput,
                        Bindings =
                        [
                            new BindingSpec
                            {
                                Property = "text",
                                Path = "UserInput",
                                Mode = BindingMode.TwoWay
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("Mode=TwoWay");
    }

    [Fact]
    public void Generate_WithStringFormat_IncludesFormat()
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
                                Path = "Health",
                                Format = "{0} HP"
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("StringFormat={0} HP");
    }

    [Fact]
    public void Generate_WithForegroundColor_SetsForeground()
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

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("Foreground=\"Red\"");
    }

    [Fact]
    public void Generate_WithBackgroundColor_SetsBackground()
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
                        Style = new StyleSpec
                        {
                            Background = new Color(34, 34, 34, 255) // #222222
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("Background=\"#222222\"");
    }

    [Fact]
    public void Generate_WithMargin_SetsMarginAttribute()
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
                            Margin = new Spacing(10, 20, 10, 20)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("Margin=\"20,10,20,10\"");
    }

    [Fact]
    public void Generate_WithPadding_SetsPaddingAttribute()
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
                            Padding = new Spacing(5)
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("Padding=\"5,5,5,5\"");
    }

    [Fact]
    public void Generate_WithGridPosition_SetsGridAttachedProperties()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec { Type = LayoutType.Grid },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "cell",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            GridRow = 1,
                            GridColumn = 2,
                            GridRowSpan = 2,
                            GridColumnSpan = 3
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("Grid.Row=\"1\"");
        axamlFile.Content.Should().Contain("Grid.Column=\"2\"");
        axamlFile.Content.Should().Contain("Grid.RowSpan=\"2\"");
        axamlFile.Content.Should().Contain("Grid.ColumnSpan=\"3\"");
    }

    [Fact]
    public void Generate_WithDockPosition_SetsDockAttachedProperty()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode
            {
                Type = ComponentType.Container,
                Layout = new LayoutSpec { Type = LayoutType.Dock },
                Children =
                [
                    new ComponentNode
                    {
                        Id = "header",
                        Type = ComponentType.Label,
                        Layout = new LayoutSpec
                        {
                            Dock = DockPosition.Top
                        }
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("DockPanel.Dock=\"Top\"");
    }

    [Fact]
    public void Generate_CodeBehind_ExtendsUserControl()
    {
        var doc = new HudDocument
        {
            Name = "Test",
            Root = new ComponentNode { Type = ComponentType.Container }
        };

        var result = _generator.Generate(doc, _options);

        var codeBehind = result.Files.First(f => f.Path.EndsWith(".axaml.cs", StringComparison.Ordinal));
        codeBehind.Content.Should().Contain("public partial class TestView : UserControl");
        codeBehind.Content.Should().Contain("InitializeComponent()");
    }

    [Fact]
    public void Generate_ViewModelInterface_ImplementsINotifyPropertyChanged()
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
                            new BindingSpec { Property = "text", Path = "Message" }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var vmFile = result.Files.First(f => f.Path.Contains("ViewModel"));
        vmFile.Content.Should().Contain("INotifyPropertyChanged");
        vmFile.Content.Should().Contain("Message { get; }");
    }

    [Fact]
    public void Generate_WithStaticText_IncludesTextContent()
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

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("❤️");
    }

    [Fact]
    public void Generate_WithNestedChildren_PreservesHierarchy()
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
                        Id = "outer",
                        Type = ComponentType.Panel,
                        Children =
                        [
                            new ComponentNode
                            {
                                Id = "inner",
                                Type = ComponentType.Label
                            }
                        ]
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        // Border (panel) should contain TextBlock (label)
        axamlFile.Content.Should().Contain("<Border");
        axamlFile.Content.Should().Contain("<TextBlock");
        axamlFile.Content.Should().Contain("x:Name=\"outer\"");
        axamlFile.Content.Should().Contain("x:Name=\"inner\"");
    }

    [Fact]
    public void Generate_WithScrollView_CreatesScrollViewer()
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
                        Id = "scroller",
                        Type = ComponentType.ScrollView
                    }
                ]
            }
        };

        var result = _generator.Generate(doc, _options);

        var axamlFile = result.Files.First(f => f.Path.EndsWith(".axaml", StringComparison.Ordinal));
        axamlFile.Content.Should().Contain("<ScrollViewer");
        axamlFile.Content.Should().Contain("x:Name=\"scroller\"");
    }

    [Fact]
    public void TargetFramework_ReturnsAvalonia()
    {
        _generator.TargetFramework.Should().Be("Avalonia");
    }

    [Fact]
    public void Capabilities_ReturnsAvaloniaCapabilities()
    {
        _generator.Capabilities.Should().BeOfType<AvaloniaCapabilities>();
    }
}
