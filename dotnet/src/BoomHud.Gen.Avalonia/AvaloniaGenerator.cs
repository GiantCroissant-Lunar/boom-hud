using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;

#pragma warning disable CA1305 // StringBuilder.Append with interpolated strings - culture not relevant for XML generation

namespace BoomHud.Gen.Avalonia;

/// <summary>
/// Code generator for Avalonia AXAML.
/// </summary>
public sealed class AvaloniaGenerator : IBackendGenerator
{
    public string TargetFramework => "Avalonia";

    public ICapabilityManifest Capabilities => AvaloniaCapabilities.Instance;

    public GenerationResult Generate(HudDocument document, GenerationOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var files = new List<GeneratedFile>();
        var prepared = GenerationDocumentPreprocessor.Prepare(document, options, "avalonia");
        document = prepared.Document;
        diagnostics.AddRange(prepared.Diagnostics);

        try
        {
            foreach (var component in document.Components.Values)
            {
                var componentDocument = new HudDocument
                {
                    Name = component.Name,
                    Metadata = component.Metadata,
                    Root = component.Root,
                    Styles = document.Styles
                };

                var componentAxamlCode = GenerateAxaml(componentDocument, options, diagnostics, document.Components);
                files.Add(new GeneratedFile
                {
                    Path = $"{componentDocument.Name}View.axaml",
                    Content = componentAxamlCode,
                    Type = GeneratedFileType.Markup
                });

                var componentCodeBehind = GenerateCodeBehind(componentDocument, options);
                files.Add(new GeneratedFile
                {
                    Path = $"{componentDocument.Name}View.axaml.cs",
                    Content = componentCodeBehind,
                    Type = GeneratedFileType.SourceCode
                });

                if (options.EmitCompose)
                {
                    var composeCode = GenerateCompose(componentDocument, options, document.Components);
                    files.Add(new GeneratedFile
                    {
                        Path = $"{componentDocument.Name}View.Compose.g.cs",
                        Content = composeCode,
                        Type = GeneratedFileType.SourceCode
                    });
                }

                if (options.EmitViewModelInterfaces)
                {
                    var componentViewModelCode = GenerateViewModelInterface(componentDocument, options);
                    files.Add(new GeneratedFile
                    {
                        Path = $"I{componentDocument.Name}ViewModel.g.cs",
                        Content = componentViewModelCode,
                        Type = GeneratedFileType.SourceCode
                    });
                }
            }

            // Generate AXAML markup
            var axamlCode = GenerateAxaml(document, options, diagnostics, document.Components);
            files.Add(new GeneratedFile
            {
                Path = $"{document.Name}View.axaml",
                Content = axamlCode,
                Type = GeneratedFileType.Markup
            });

            // Generate code-behind
            var codeBehind = GenerateCodeBehind(document, options);
            files.Add(new GeneratedFile
            {
                Path = $"{document.Name}View.axaml.cs",
                Content = codeBehind,
                Type = GeneratedFileType.SourceCode
            });

            if (options.EmitCompose)
            {
                var composeCode = GenerateCompose(document, options, document.Components);
                files.Add(new GeneratedFile
                {
                    Path = $"{document.Name}View.Compose.g.cs",
                    Content = composeCode,
                    Type = GeneratedFileType.SourceCode
                });
            }

            // Generate ViewModel interface
            if (options.EmitViewModelInterfaces)
            {
                var viewModelCode = GenerateViewModelInterface(document, options);
                files.Add(new GeneratedFile
                {
                    Path = $"I{document.Name}ViewModel.g.cs",
                    Content = viewModelCode,
                    Type = GeneratedFileType.SourceCode
                });
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(Diagnostic.Error($"Generation failed: {ex.Message}"));
        }

        if (GenerationDocumentPreprocessor.CreateSummaryArtifact(document.Name, prepared.SyntheticComponentization) is { } artifact)
        {
            files.Add(artifact);
        }

        if (options.EmitSourceSemanticArtifact
            && GenerationDocumentPreprocessor.CreateSourceSemanticArtifact(document.Name, prepared.SourceSemanticDocument) is { } sourceSemanticArtifact)
        {
            files.Add(sourceSemanticArtifact);
        }

        if (options.EmitVisualIrArtifact
            && GenerationDocumentPreprocessor.CreateVisualIrArtifact(document.Name, prepared.VisualDocument) is { } visualIrArtifact)
        {
            files.Add(visualIrArtifact);
        }

        if (options.EmitVisualSynthesisArtifact
            && GenerationDocumentPreprocessor.CreateVisualSynthesisArtifact(document.Name, prepared.VisualSynthesis) is { } visualSynthesisArtifact)
        {
            files.Add(visualSynthesisArtifact);
        }

        if (options.EmitVisualRefinementArtifact
            && GenerationDocumentPreprocessor.CreateVisualRefinementArtifact(document.Name, prepared.VisualRefinement) is { } visualRefinementArtifact)
        {
            files.Add(visualRefinementArtifact);
        }

        return new GenerationResult
        {
            Files = files,
            Diagnostics = diagnostics
        };
    }

    private static string GenerateAxaml(
        HudDocument document,
        GenerationOptions options,
        List<Diagnostic> diagnostics,
        IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        var sb = new StringBuilder();

        var viewModelNamespace = options.ViewModelNamespace ?? options.Namespace;

        // XML declaration
        sb.AppendLine("<UserControl xmlns=\"https://github.com/avaloniaui\"");
        sb.AppendLine("             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
        sb.AppendLine("             xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\"");
        sb.AppendLine("             xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"");
        sb.AppendLine($"             xmlns:views=\"clr-namespace:{options.Namespace}\"");
        sb.AppendLine($"             xmlns:vm=\"clr-namespace:{viewModelNamespace}\"");
        sb.AppendLine($"             mc:Ignorable=\"d\" d:DesignWidth=\"800\" d:DesignHeight=\"450\"");
        sb.AppendLine($"             x:Class=\"{options.Namespace}.{document.Name}View\" x:DataType=\"vm:I{document.Name}ViewModel\">");

        // Emit theme resources (if provided) into UserControl.Resources so views can bind to tokens.
        if (options.Theme is ThemeDocument theme)
        {
            GenerateThemeResources(sb, theme, 1);
        }

        // Generate root content
        GenerateComponentAxaml(sb, document.Root, 1, diagnostics, options.Theme, components, options.Namespace);

        sb.AppendLine("</UserControl>");

        return sb.ToString();
    }

    private static void GenerateThemeResources(StringBuilder sb, ThemeDocument theme, int indent)
    {
        var indentStr = new string(' ', indent * 4);
        var innerIndent = new string(' ', (indent + 1) * 4);

        sb.AppendLine($"{indentStr}<UserControl.Resources>");

        // Color tokens -> SolidColorBrush resources
        foreach (var kvp in theme.Colors)
        {
            var key = XmlEscape(kvp.Key + "Brush");
            var colorValue = ColorToAvalonia(kvp.Value);
            sb.AppendLine($"{innerIndent}<SolidColorBrush x:Key=\"{key}\" Color=\"{colorValue}\" />");
        }

        // Dimension tokens -> doubles
        foreach (var kvp in theme.Dimensions)
        {
            var key = XmlEscape(kvp.Key);
            sb.AppendLine($"{innerIndent}<x:Double x:Key=\"{key}\">{kvp.Value.ToString(CultureInfo.InvariantCulture)}</x:Double>");
        }

        // Font size tokens -> doubles with a clearer suffix
        foreach (var kvp in theme.FontSizes)
        {
            var key = XmlEscape(kvp.Key + "FontSize");
            sb.AppendLine($"{innerIndent}<x:Double x:Key=\"{key}\">{kvp.Value.ToString(CultureInfo.InvariantCulture)}</x:Double>");
        }

        sb.AppendLine($"{indentStr}</UserControl.Resources>");
    }

    private static void GenerateComponentAxaml(
        StringBuilder sb,
        ComponentNode node,
        int indent,
        List<Diagnostic> diagnostics,
        ThemeDocument? theme,
        IReadOnlyDictionary<string, HudComponentDefinition> components,
        string @namespace)
    {
        var indentStr = new string(' ', indent * 4);

        if (node.ComponentRefId != null && components.TryGetValue(node.ComponentRefId, out var componentDef))
        {
            var elementName = $"views:{componentDef.Name}View";
            sb.Append($"{indentStr}<{elementName}");

            var refName = node.SlotKey ?? node.Id;
            if (!string.IsNullOrWhiteSpace(refName))
            {
                sb.Append($" x:Name=\"{XmlEscape(ToXamlName(refName))}\"");
            }

            GenerateLayoutAttributes(sb, node.Layout, elementName);
            GenerateStyleAttributes(sb, node.Style, elementName, theme);
            GenerateComponentAttributes(sb, node);
            GenerateBindingAttributes(sb, node);

            sb.AppendLine(" />");

            if (node.InstanceOverrides.Count > 0)
            {
                diagnostics.Add(Diagnostic.Warning(
                    $"Component instance overrides are not yet applied for '{componentDef.Name}'",
                    node.Id));
            }

            return;
        }

        // Determine container type based on layout
        var layoutType = node.Layout?.Type ?? LayoutType.Vertical;
        var containerElement = GetContainerElement(node.Type, layoutType);

        // Start element
        sb.Append($"{indentStr}<{containerElement}");

        // Add name if present
        if (!string.IsNullOrEmpty(node.Id))
        {
            sb.Append($" x:Name=\"{node.Id}\"");
        }

        // Add layout attributes
        GenerateLayoutAttributes(sb, node.Layout, containerElement);

        // Add layout-specific attributes (e.g. orientation)
        GenerateOrientationAttribute(sb, layoutType, containerElement);

        // Add spacing between children for StackPanel-based layouts
        GenerateGapAttributes(sb, node.Layout, containerElement);

        // Add style attributes (prefer theme tokens when available)
        GenerateStyleAttributes(sb, node.Style, containerElement, theme);

        // Add component-specific attributes
        GenerateComponentAttributes(sb, node);

        // Add bindings
        GenerateBindingAttributes(sb, node);

        // Check if we have children or content
        var hasChildren = node.Children.Count > 0;
        var hasContent = HasInlineContent(node);

        if (!hasChildren && !hasContent)
        {
            sb.AppendLine(" />");
        }
        else
        {
            sb.AppendLine(">");

            // Generate inline content (text, value)
            if (hasContent)
            {
                GenerateInlineContent(sb, node, indent + 1);
            }

            // Generate children
            if (node.Type == ComponentType.Menu)
            {
                foreach (var child in node.Children)
                {
                    // Minimal convention: inside a Menu container, Label nodes become MenuItems.
                    if (child.Type == ComponentType.Label)
                    {
                        var menuItemNode = child with { Type = ComponentType.MenuItem };
                        GenerateComponentAxaml(sb, menuItemNode, indent + 1, diagnostics, theme, components, @namespace);
                        continue;
                    }

                    if (child.Type == ComponentType.MenuItem)
                    {
                        GenerateComponentAxaml(sb, child, indent + 1, diagnostics, theme, components, @namespace);
                        continue;
                    }

                    diagnostics.Add(Diagnostic.Warning(
                        $"Menu '{node.Id ?? node.Type.ToString()}' contains unsupported child type '{child.Type}'. Only Label/MenuItem children are supported.",
                        child.Id));
                }
            }
            else
            {
                foreach (var child in node.Children)
                {
                    GenerateComponentAxaml(sb, child, indent + 1, diagnostics, theme, components, @namespace);
                }
            }

            sb.AppendLine($"{indentStr}</{containerElement}>");
        }
    }

    private static bool SupportsPadding(string elementName)
    {
        // In Avalonia, Border and many ContentControls support Padding; StackPanel and Grid do not.
        return string.Equals(elementName, "Border", StringComparison.Ordinal)
               || string.Equals(elementName, "UserControl", StringComparison.Ordinal);
    }

    private static bool SupportsBorder(string elementName)
    {
        // BorderBrush/BorderThickness and CornerRadius are reliably available on Border.
        // To avoid AVLN errors, we restrict these style attributes to Border for now.
        return string.Equals(elementName, "Border", StringComparison.Ordinal);
    }

    private static void GenerateChildAxaml(StringBuilder sb, ComponentNode node, int indent, List<Diagnostic> diagnostics, ThemeDocument? theme)
    {
        var indentStr = new string(' ', indent * 4);
        var element = MapComponentToElement(node.Type);

        sb.Append($"{indentStr}<{element}");

        // Add name if present
        if (!string.IsNullOrEmpty(node.Id))
        {
            sb.Append($" x:Name=\"{node.Id}\"");
        }

        // Add layout attributes
        GenerateLayoutAttributes(sb, node.Layout, element);

        // Add layout-specific attributes (e.g. orientation)
        GenerateOrientationAttribute(sb, node.Layout?.Type, element);

        // Add spacing between children for StackPanel-based layouts
        GenerateGapAttributes(sb, node.Layout, element);

        // Add style attributes (prefer theme tokens when available)
        GenerateStyleAttributes(sb, node.Style, element, theme);

        // Add component-specific attributes
        GenerateComponentAttributes(sb, node);

        // Add bindings
        GenerateBindingAttributes(sb, node);

        // Check if we have children
        var hasChildren = node.Children.Count > 0;
        var hasContent = HasInlineContent(node);

        if (!hasChildren && !hasContent)
        {
            sb.AppendLine(" />");
        }
        else
        {
            sb.AppendLine(">");

            // Generate inline content
            if (hasContent)
            {
                GenerateInlineContent(sb, node, indent + 1);
            }

            // Generate children - may need wrapper for layout
            if (hasChildren)
            {
                var needsLayoutWrapper = NeedsLayoutWrapper(node);
                if (needsLayoutWrapper)
                {
                    var wrapperElement = GetLayoutWrapper(node.Layout?.Type ?? LayoutType.Vertical);
                    var wrapperIndent = indentStr + "    ";
                    sb.Append($"{wrapperIndent}<{wrapperElement}");
                    GenerateOrientationAttribute(sb, node.Layout?.Type, wrapperElement);
                    // Apply spacing between children inside wrapped containers as well
                    GenerateGapAttributes(sb, node.Layout, wrapperElement);
                    sb.AppendLine(">");

                    foreach (var child in node.Children)
                    {
                        GenerateChildAxaml(sb, child, indent + 2, diagnostics, theme);
                    }

                    sb.AppendLine($"{wrapperIndent}</{wrapperElement}>");
                }
                else
                {
                    foreach (var child in node.Children)
                    {
                        GenerateChildAxaml(sb, child, indent + 1, diagnostics, theme);
                    }
                }
            }

            sb.AppendLine($"{indentStr}</{element}>");
        }
    }

    private static void GenerateLayoutAttributes(StringBuilder sb, LayoutSpec? layout, string elementName)
    {
        if (layout == null) return;

        // Width
        if (layout.Width != null)
        {
            var widthValue = DimensionToAvalonia(layout.Width.Value);
            if (widthValue != null)
            {
                sb.Append($" Width=\"{widthValue}\"");
            }
        }

        // Height
        if (layout.Height != null)
        {
            var heightValue = DimensionToAvalonia(layout.Height.Value);
            if (heightValue != null)
            {
                sb.Append($" Height=\"{heightValue}\"");
            }
        }

        // MinWidth/MinHeight
        if (layout.MinWidth != null)
        {
            sb.Append($" MinWidth=\"{(int)layout.MinWidth.Value.Value}\"");
        }
        if (layout.MinHeight != null)
        {
            sb.Append($" MinHeight=\"{(int)layout.MinHeight.Value.Value}\"");
        }

        // MaxWidth/MaxHeight
        if (layout.MaxWidth != null)
        {
            sb.Append($" MaxWidth=\"{(int)layout.MaxWidth.Value.Value}\"");
        }
        if (layout.MaxHeight != null)
        {
            sb.Append($" MaxHeight=\"{(int)layout.MaxHeight.Value.Value}\"");
        }

        // Margin
        if (layout.Margin != null)
        {
            var m = layout.Margin.Value;
            sb.Append($" Margin=\"{(int)m.Left},{(int)m.Top},{(int)m.Right},{(int)m.Bottom}\"");
        }

        // Padding (only for elements that support it)
        if (layout.Padding != null && SupportsPadding(elementName))
        {
            var p = layout.Padding.Value;
            sb.Append($" Padding=\"{(int)p.Left},{(int)p.Top},{(int)p.Right},{(int)p.Bottom}\"");
        }

        // Alignment
        if (layout.Align != null)
        {
            var vertAlign = layout.Align switch
            {
                Alignment.Start => "Top",
                Alignment.Center => "Center",
                Alignment.End => "Bottom",
                Alignment.Stretch => "Stretch",
                _ => null
            };
            if (vertAlign != null)
            {
                sb.Append($" VerticalAlignment=\"{vertAlign}\"");
            }
        }

        if (layout.Justify != null)
        {
            var horizAlign = layout.Justify switch
            {
                Justification.Start => "Left",
                Justification.Center => "Center",
                Justification.End => "Right",
                _ => null
            };
            if (horizAlign != null)
            {
                sb.Append($" HorizontalAlignment=\"{horizAlign}\"");
            }
        }

        // Grid positioning
        if (layout.GridRow != null)
        {
            sb.Append($" Grid.Row=\"{layout.GridRow}\"");
        }
        if (layout.GridColumn != null)
        {
            sb.Append($" Grid.Column=\"{layout.GridColumn}\"");
        }
        if (layout.GridRowSpan > 1)
        {
            sb.Append($" Grid.RowSpan=\"{layout.GridRowSpan}\"");
        }
        if (layout.GridColumnSpan > 1)
        {
            sb.Append($" Grid.ColumnSpan=\"{layout.GridColumnSpan}\"");
        }

        // Dock positioning
        if (layout.Dock != null)
        {
            var dockValue = layout.Dock switch
            {
                DockPosition.Top => "Top",
                DockPosition.Bottom => "Bottom",
                DockPosition.Left => "Left",
                DockPosition.Right => "Right",
                _ => null
            };
            if (dockValue != null)
            {
                sb.Append($" DockPanel.Dock=\"{dockValue}\"");
            }
        }
    }

    private static void GenerateOrientationAttribute(StringBuilder sb, LayoutType? layoutType, string elementName)
    {
        if (layoutType == null)
        {
            return;
        }

        // Only StackPanel supports Orientation
        if (!string.Equals(elementName, "StackPanel", StringComparison.Ordinal))
        {
            return;
        }

        string? orientation = layoutType switch
        {
            LayoutType.Horizontal => "Horizontal",
            LayoutType.Vertical => "Vertical",
            LayoutType.Stack => "Vertical",
            _ => null
        };

        if (orientation != null)
        {
            sb.Append($" Orientation=\"{orientation}\"");
        }
    }

    private static void GenerateGapAttributes(StringBuilder sb, LayoutSpec? layout, string elementName)
    {
        if (layout?.Gap == null)
        {
            return;
        }

        // Currently only StackPanel has a Spacing property we can use directly.
        if (!string.Equals(elementName, "StackPanel", StringComparison.Ordinal))
        {
            return;
        }

        var gap = layout.Gap.Value;
        // Use the primary axis component of the gap as the spacing value.
        var spacing = layout.Type switch
        {
            LayoutType.Horizontal => (int)gap.Left,
            LayoutType.Vertical => (int)gap.Top,
            LayoutType.Stack => (int)gap.Top,
            _ => (int)gap.Top
        };

        if (spacing > 0)
        {
            sb.Append($" Spacing=\"{spacing}\"");
        }
    }

    private static void GenerateStyleAttributes(StringBuilder sb, StyleSpec? style, string elementName, ThemeDocument? theme)
    {
        if (style == null) return;

        // Foreground
        if (style.Foreground != null)
        {
            if (TryGetBrushResourceKey(theme, style.ForegroundToken, style.Foreground.Value, out var brushKey))
            {
                sb.Append($" Foreground=\"{{StaticResource {brushKey}}}\"");
            }
            else
            {
                var colorValue = ColorToAvalonia(style.Foreground.Value);
                sb.Append($" Foreground=\"{colorValue}\"");
            }
        }

        // Background
        if (style.Background != null)
        {
            if (TryGetBrushResourceKey(theme, style.BackgroundToken, style.Background.Value, out var brushKey))
            {
                sb.Append($" Background=\"{{StaticResource {brushKey}}}\"");
            }
            else
            {
                var colorValue = ColorToAvalonia(style.Background.Value);
                sb.Append($" Background=\"{colorValue}\"");
            }
        }

        // FontSize
        if (style.FontSize != null)
        {
            if (TryGetFontSizeResourceKey(theme, style.FontSizeToken, style.FontSize.Value, out var fontSizeKey))
            {
                sb.Append($" FontSize=\"{{StaticResource {fontSizeKey}}}\"");
            }
            else
            {
                sb.Append($" FontSize=\"{style.FontSize}\"");
            }
        }

        // FontWeight
        if (style.FontWeight != null)
        {
            var weight = style.FontWeight switch
            {
                FontWeight.Light => "Light",
                FontWeight.Normal => "Normal",
                FontWeight.Bold => "Bold",
                _ => null
            };
            if (weight != null)
            {
                sb.Append($" FontWeight=\"{weight}\"");
            }
        }

        // FontStyle
        if (style.FontStyle != null)
        {
            var fontStyle = style.FontStyle switch
            {
                Abstractions.IR.FontStyle.Italic => "Italic",
                Abstractions.IR.FontStyle.Normal => "Normal",
                _ => null
            };
            if (fontStyle != null)
            {
                sb.Append($" FontStyle=\"{fontStyle}\"");
            }
        }

        // Opacity
        if (style.Opacity != null)
        {
            sb.Append($" Opacity=\"{style.Opacity.Value.ToString(CultureInfo.InvariantCulture)}\"");
        }

        // Border (only on supported elements)
        if (style.Border != null && SupportsBorder(elementName))
        {
            string borderBrushAttr;
            if (style.Border.Color != null && TryGetBrushResourceKey(theme, style.BorderColorToken, style.Border.Color.Value, out var borderBrushKey))
            {
                borderBrushAttr = $"{{StaticResource {borderBrushKey}}}";
            }
            else
            {
                var borderColor = style.Border.Color != null
                    ? ColorToAvalonia(style.Border.Color.Value)
                    : "Gray";
                borderBrushAttr = borderColor;
            }
            var borderWidth = style.Border.Width;
            sb.Append($" BorderBrush=\"{borderBrushAttr}\" BorderThickness=\"{borderWidth}\"");
        }

        // CornerRadius (only on supported elements)
        if (style.BorderRadius != null && SupportsBorder(elementName))
        {
            sb.Append($" CornerRadius=\"{style.BorderRadius}\"");
        }

        // Width/Height from style
        if (style.Width != null)
        {
            var widthValue = DimensionToAvalonia(style.Width.Value);
            if (widthValue != null)
            {
                sb.Append($" Width=\"{widthValue}\"");
            }
        }
        if (style.Height != null)
        {
            var heightValue = DimensionToAvalonia(style.Height.Value);
            if (heightValue != null)
            {
                sb.Append($" Height=\"{heightValue}\"");
            }
        }
    }

    private static void GenerateComponentAttributes(StringBuilder sb, ComponentNode node)
    {
        switch (node.Type)
        {
            case ComponentType.MenuItem:
                // Avalonia MenuItem uses Header for the visible label.
                if (node.Properties.TryGetValue("text", out var headerText) && !headerText.IsBound)
                {
                    sb.Append($" Header=\"{XmlEscape(headerText.Value?.ToString() ?? string.Empty)}\"");
                }
                else if (node.Properties.TryGetValue("value", out var headerValue) && !headerValue.IsBound)
                {
                    sb.Append($" Header=\"{XmlEscape(headerValue.Value?.ToString() ?? string.Empty)}\"");
                }
                break;

            case ComponentType.ProgressBar:
                sb.Append(" Minimum=\"0\" Maximum=\"1\"");
                if (node.Properties.TryGetValue("value", out var progressValue) && !progressValue.IsBound)
                {
                    sb.Append($" Value=\"{progressValue.Value}\"");
                }
                break;

            case ComponentType.Slider:
                if (node.Properties.TryGetValue("min", out var minValue))
                {
                    sb.Append($" Minimum=\"{minValue.Value}\"");
                }
                if (node.Properties.TryGetValue("max", out var maxValue))
                {
                    sb.Append($" Maximum=\"{maxValue.Value}\"");
                }
                break;

            case ComponentType.Checkbox:
                if (node.Properties.TryGetValue("checked", out var checkedValue) && !checkedValue.IsBound)
                {
                    sb.Append($" IsChecked=\"{checkedValue.Value}\"");
                }
                break;

            case ComponentType.Image:
                if (node.Properties.TryGetValue("source", out var sourceValue) && !sourceValue.IsBound)
                {
                    sb.Append($" Source=\"{XmlEscape(sourceValue.Value?.ToString() ?? "")}\"");
                }
                break;

            case ComponentType.ScrollView:
                sb.Append(" HorizontalScrollBarVisibility=\"Auto\" VerticalScrollBarVisibility=\"Auto\"");
                break;
        }

        // Tooltip
        if (node.Tooltip != null && !node.Tooltip.Value.IsBound)
        {
            sb.Append($" ToolTip.Tip=\"{XmlEscape(node.Tooltip.Value.Value?.ToString() ?? "")}\"");
        }

        // Visibility binding
        if (!node.Visible.IsBound && node.Visible.Value == false)
        {
            sb.Append(" IsVisible=\"False\"");
        }

        // Enabled binding
        if (!node.Enabled.IsBound && node.Enabled.Value == false)
        {
            sb.Append(" IsEnabled=\"False\"");
        }
    }

    private static void GenerateBindingAttributes(StringBuilder sb, ComponentNode node)
    {
        foreach (var binding in node.Bindings)
        {
            var avaloniaProperty = MapBindingProperty(binding.Property, node.Type);
            if (avaloniaProperty == null) continue;

            var bindingExpr = GenerateBindingExpression(binding);
            sb.Append($" {avaloniaProperty}=\"{bindingExpr}\"");
        }

        // Handle visible/enabled bindings
        if (node.Visible.IsBound)
        {
            sb.Append($" IsVisible=\"{{Binding {node.Visible.BindingPath}}}\"");
        }
        if (node.Enabled.IsBound)
        {
            sb.Append($" IsEnabled=\"{{Binding {node.Enabled.BindingPath}}}\"");
        }
    }

    private static string GenerateBindingExpression(BindingSpec binding)
    {
        var parts = new List<string> { $"Binding {binding.Path}" };

        if (binding.Mode != BindingMode.OneWay)
        {
            var modeStr = binding.Mode switch
            {
                BindingMode.TwoWay => "TwoWay",
                BindingMode.OneTime => "OneTime",
                _ => null
            };
            if (modeStr != null)
            {
                parts.Add($"Mode={modeStr}");
            }
        }

        if (!string.IsNullOrEmpty(binding.Converter))
        {
            parts.Add($"Converter={{StaticResource {binding.Converter}}}");
        }

        if (!string.IsNullOrEmpty(binding.Format))
        {
            parts.Add($"StringFormat={XmlEscape(binding.Format)}");
        }

        if (binding.Fallback != null)
        {
            parts.Add($"FallbackValue={binding.Fallback}");
        }

        return "{" + string.Join(", ", parts) + "}";
    }

    private static string GetBindingMemberName(BindingSpec binding)
        => !string.IsNullOrWhiteSpace(binding.Key) ? binding.Key! : binding.Path;

    private static string ToXamlName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "_";

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }

        if (sb.Length == 0)
            return "_";

        if (!char.IsLetter(sb[0]) && sb[0] != '_')
            sb.Insert(0, '_');

        return sb.ToString();
    }

    private static bool HasInlineContent(ComponentNode node)
    {
        return node.Type switch
        {
            ComponentType.Label => node.Properties.ContainsKey("text") || node.Properties.ContainsKey("value"),
            ComponentType.Button => node.Properties.ContainsKey("text") || node.Properties.ContainsKey("value"),
            ComponentType.Icon => node.Properties.ContainsKey("value"),
            _ => false
        };
    }

    private static void GenerateInlineContent(StringBuilder sb, ComponentNode node, int indent)
    {
        var indentStr = new string(' ', indent * 4);

        string? content = null;
        if (node.Properties.TryGetValue("text", out var textProp) && !textProp.IsBound)
        {
            content = textProp.Value?.ToString();
        }
        else if (node.Properties.TryGetValue("value", out var valueProp) && !valueProp.IsBound)
        {
            content = valueProp.Value?.ToString();
        }

        if (content != null)
        {
            sb.AppendLine($"{indentStr}{XmlEscape(content)}");
        }
    }

    private static bool NeedsLayoutWrapper(ComponentNode node)
    {
        // ContentControls like Button and Border-based containers (Panel/Container) need a wrapper for multiple logical children
        return node.Type switch
        {
            ComponentType.Button => node.Children.Count > 1,
            ComponentType.Panel => node.Children.Count > 0,
            ComponentType.Container => node.Children.Count > 0,
            ComponentType.ScrollView => node.Children.Count > 1,
            _ => false
        };
    }

    private static string GetLayoutWrapper(LayoutType layoutType)
    {
        return layoutType switch
        {
            LayoutType.Horizontal => "StackPanel",
            LayoutType.Vertical => "StackPanel",
            LayoutType.Grid => "Grid",
            LayoutType.Dock => "DockPanel",
            _ => "StackPanel"
        };
    }

    private static string GetContainerElement(ComponentType type, LayoutType layoutType)
    {
        // For root container, use layout-appropriate panel
        if (type == ComponentType.Container)
        {
            return layoutType switch
            {
                LayoutType.Horizontal => "StackPanel",
                LayoutType.Vertical => "StackPanel",
                LayoutType.Stack => "StackPanel",
                LayoutType.Grid => "Grid",
                LayoutType.Dock => "DockPanel",
                LayoutType.Absolute => "Canvas",
                _ => "StackPanel"
            };
        }

        return MapComponentToElement(type);
    }

    private static string MapComponentToElement(ComponentType type) => type switch
    {
        ComponentType.Label => "TextBlock",
        ComponentType.Button => "Button",
        ComponentType.TextInput => "TextBox",
        ComponentType.TextArea => "TextBox",
        ComponentType.Checkbox => "CheckBox",
        ComponentType.RadioButton => "RadioButton",
        ComponentType.ProgressBar => "ProgressBar",
        ComponentType.Slider => "Slider",
        ComponentType.Icon => "TextBlock", // Icons as text/emoji or use PathIcon
        ComponentType.Image => "Image",
        ComponentType.MenuBar => "Menu",
        ComponentType.Menu => "MenuItem",
        ComponentType.MenuItem => "MenuItem",
        ComponentType.Timeline => "StackPanel",
        ComponentType.Container => "Border",
        ComponentType.ScrollView => "ScrollViewer",
        ComponentType.Panel => "Border",
        ComponentType.TabView => "TabControl",
        ComponentType.SplitView => "SplitView",
        ComponentType.ListBox => "ListBox",
        ComponentType.ListView => "ListBox",
        ComponentType.TreeView => "TreeView",
        ComponentType.DataGrid => "DataGrid",
        ComponentType.Stack => "StackPanel",
        ComponentType.Grid => "Grid",
        ComponentType.Dock => "DockPanel",
        ComponentType.Spacer => "Border", // Empty border as spacer
        _ => "Border"
    };

    private static string? MapBindingProperty(string property, ComponentType componentType)
    {
        return property.ToLowerInvariant() switch
        {
            "text" => componentType switch
            {
                ComponentType.MenuItem => "Header",
                ComponentType.TextInput => "Text",
                ComponentType.TextArea => "Text",
                _ => "Text"
            },
            "value" => componentType switch
            {
                ComponentType.ProgressBar => "Value",
                ComponentType.Slider => "Value",
                ComponentType.Checkbox => "IsChecked",
                _ => null
            },
            "items" => "ItemsSource",
            "selecteditem" => "SelectedItem",
            "selectedindex" => "SelectedIndex",
            "source" => "Source",
            "tooltip" => "ToolTip.Tip",
            "command" => "Command",
            "commandparameter" => "CommandParameter",
            _ => null
        };
    }

    private static string? DimensionToAvalonia(Dimension dim)
    {
        return dim.Unit switch
        {
            DimensionUnit.Pixels => dim.Value.ToString(CultureInfo.InvariantCulture),
            DimensionUnit.Cells => dim.Value.ToString(CultureInfo.InvariantCulture), // Treat as pixels
            DimensionUnit.Percent => $"{dim.Value}%", // Avalonia doesn't support % directly, but we output it
            DimensionUnit.Star => "*",
            DimensionUnit.Auto => "Auto",
            DimensionUnit.Fill => "*",
            _ => dim.Value.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string ColorToAvalonia(Color color)
    {
        // Check for named colors
        var named = (color.R, color.G, color.B) switch
        {
            (0, 0, 0) => "Black",
            (255, 255, 255) => "White",
            (255, 0, 0) => "Red",
            (0, 255, 0) => "Green",
            (0, 0, 255) => "Blue",
            (255, 255, 0) => "Yellow",
            (0, 255, 255) => "Cyan",
            (255, 0, 255) => "Magenta",
            (128, 128, 128) => "Gray",
            (192, 192, 192) => "LightGray",
            (64, 64, 64) => "DarkGray",
            (255, 165, 0) => "Orange",
            (128, 0, 128) => "Purple",
            (165, 42, 42) => "Brown",
            (255, 192, 203) => "Pink",
            _ => null
        };

        if (named != null) return named;

        // Return hex color
        return color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool TryGetBrushResourceKey(ThemeDocument? theme, string? tokenKey, Color color, out string resourceKey)
    {
        resourceKey = string.Empty;

        if (theme == null)
        {
            return false;
        }

        // Prefer explicit token key if it exists in the theme document
        if (!string.IsNullOrWhiteSpace(tokenKey) && theme.Colors.ContainsKey(tokenKey))
        {
            resourceKey = XmlEscape(tokenKey + "Brush");
            return true;
        }

        // Otherwise try to match by value
        foreach (var kvp in theme.Colors)
        {
            if (kvp.Value.Equals(color))
            {
                resourceKey = XmlEscape(kvp.Key + "Brush");
                return true;
            }
        }

        return false;
    }

    private static bool TryGetFontSizeResourceKey(ThemeDocument? theme, string? tokenKey, double size, out string resourceKey)
    {
        resourceKey = string.Empty;

        if (theme == null)
        {
            return false;
        }

        // Prefer explicit token key if it exists in the theme document
        if (!string.IsNullOrWhiteSpace(tokenKey) && theme.FontSizes.ContainsKey(tokenKey))
        {
            resourceKey = XmlEscape(tokenKey + "FontSize");
            return true;
        }

        // Otherwise try to match by numeric value (with small tolerance)
        const double tolerance = 0.01;
        foreach (var kvp in theme.FontSizes)
        {
            if (Math.Abs(kvp.Value - size) <= tolerance)
            {
                resourceKey = XmlEscape(kvp.Key + "FontSize");
                return true;
            }
        }

        return false;
    }

    private static string GenerateCodeBehind(HudDocument document, GenerationOptions options)
    {
        var cb = new CodeBuilder();

        var sourceId = ComputeSourceId(document);
        var contractId = options.ContractId ?? string.Empty;
        var normalizedPseudoNodes = CollectNormalizedPseudoNodes(document);

        if (options.IncludeComments)
        {
            cb.AppendLine("// <auto-generated>");
            cb.AppendLine($"// Generated by BoomHud.Gen.Avalonia from {document.Name}");
            cb.AppendLine("// </auto-generated>");
            cb.AppendLine();
        }

        if (options.UseNullableAnnotations)
        {
            cb.AppendLine("#nullable enable");
            cb.AppendLine();
        }

        cb.AppendLine("using Avalonia.Controls;");
        cb.AppendLine();

        cb.AppendLine($"namespace {options.Namespace};");
        cb.AppendLine();

        if (options.IncludeComments && document.Metadata?.Description != null)
        {
            var description = ApplyDescriptionReplacements(document.Metadata.Description, options.DescriptionReplacements);
            cb.AppendLine("/// <summary>");
            cb.AppendLine($"/// {description}");
            cb.AppendLine("/// </summary>");
        }

        cb.AppendLine($"public partial class {document.Name}View : UserControl");
        cb.OpenBlock();

        cb.AppendLine($"public const string BoomHudSourceId = \"{sourceId}\";");
        cb.AppendLine($"public const string BoomHudContractId = \"{contractId.Replace("\"", "\\\"")}\";");
        cb.AppendLine($"public static readonly string[] BoomHudNormalizedPseudoNodes = {FormatStringArrayLiteral(normalizedPseudoNodes)};");
        cb.AppendLine();

        cb.AppendLine($"public {document.Name}View()");
        cb.OpenBlock();
        cb.AppendLine("InitializeComponent();");
        cb.CloseBlock();

        cb.CloseBlock();

        return cb.ToString();
    }

    private static string ComputeSourceId(HudDocument document)
    {
        var sb = new StringBuilder();
        sb.Append("doc:").Append(document.Name).Append('\n');
        AppendNode(sb, document.Root);
        return "sha256:" + ComputeSha256Hex(sb.ToString());
    }

    private static List<string> CollectNormalizedPseudoNodes(HudDocument document)
    {
        var results = new List<string>();
        CollectNormalizedPseudoNodes(document.Root, currentPath: [], results);
        results.Sort(StringComparer.Ordinal);
        return results;
    }

    private static void CollectNormalizedPseudoNodes(ComponentNode node, List<string> currentPath, List<string> results)
    {
        var nextPath = new List<string>(currentPath);
        if (!string.IsNullOrWhiteSpace(node.Id))
        {
            nextPath.Add(node.Id);
        }

        if (node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.NormalizedFromPseudoType, out var normalized)
            && normalized is bool normalizedBool
            && normalizedBool
            && node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalFigmaType, out var original)
            && original is string originalStr)
        {
            results.Add($"{string.Join("/", nextPath)}|{originalStr}|{node.Type}");
        }

        foreach (var child in node.Children)
        {
            CollectNormalizedPseudoNodes(child, nextPath, results);
        }
    }

    private static string FormatStringArrayLiteral(List<string> items)
    {
        if (items.Count == 0)
        {
            return "new string[0]";
        }

        return "new[] { " + string.Join(", ", items.Select(s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")) + " }";
    }

    private static void AppendNode(StringBuilder sb, ComponentNode node)
    {
        sb.Append("node:")
            .Append(node.Type.ToString()).Append('|')
            .Append(node.Id ?? string.Empty).Append('|')
            .Append(node.SlotKey ?? string.Empty).Append('|')
            .Append(node.ComponentRefId ?? string.Empty).Append('\n');

        foreach (var b in node.Bindings.OrderBy(b => b.Property, StringComparer.Ordinal).ThenBy(b => b.Path, StringComparer.Ordinal))
        {
            sb.Append("bind:")
                .Append(b.Property).Append('|')
                .Append(b.Path).Append('|')
                .Append(b.Key ?? string.Empty).Append('|')
                .Append(b.Format ?? string.Empty).Append('\n');
        }

        if (node.Command != null)
        {
            sb.Append("cmd:").Append(node.Command).Append('\n');
        }

        foreach (var child in node.Children)
        {
            AppendNode(sb, child);
        }
    }

    private static string ComputeSha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        var hex = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return hex.ToString();
    }

    private static string ApplyDescriptionReplacements(
        string description,
        IReadOnlyDictionary<string, string> replacements)
    {
        if (string.IsNullOrEmpty(description) || replacements.Count == 0)
        {
            return description;
        }

        var updated = description;
        foreach (var kvp in replacements)
        {
            if (string.IsNullOrEmpty(kvp.Key))
            {
                continue;
            }

            updated = updated.Replace(kvp.Key, kvp.Value ?? string.Empty, StringComparison.Ordinal);
        }

        return updated;
    }

    private static string GenerateViewModelInterface(HudDocument document, GenerationOptions options)
    {
        var cb = new CodeBuilder();

        var viewModelNamespace = options.ViewModelNamespace ?? options.Namespace;

        if (options.IncludeComments)
        {
            cb.AppendLine("// <auto-generated>");
            cb.AppendLine($"// Generated by BoomHud.Gen.Avalonia from {document.Name}");
            cb.AppendLine("// </auto-generated>");
            cb.AppendLine();
        }

        if (options.UseNullableAnnotations)
        {
            cb.AppendLine("#nullable enable");
            cb.AppendLine();
        }

        cb.AppendLine("using System.ComponentModel;");
        cb.AppendLine();

        cb.AppendLine($"namespace {viewModelNamespace};");
        cb.AppendLine();

        if (options.IncludeComments)
        {
            cb.AppendLine("/// <summary>");
            cb.AppendLine($"/// ViewModel interface for {document.Name}.");
            cb.AppendLine("/// </summary>");
        }

        cb.AppendLine($"public interface I{document.Name}ViewModel : INotifyPropertyChanged");
        cb.OpenBlock();

        // Collect all binding paths
        var bindingPaths = new HashSet<string>();
        CollectBindingPaths(document.Root, bindingPaths);

        foreach (var path in bindingPaths.OrderBy(p => p, StringComparer.Ordinal))
        {
            var propertyName = path.Replace(".", "", StringComparison.Ordinal);
            var propertyType = InferPropertyType(path, document.Root);
            cb.AppendLine($"{propertyType} {propertyName} {{ get; }}");
        }

        cb.CloseBlock();

        return cb.ToString();
    }

    private static void CollectBindingPaths(ComponentNode node, HashSet<string> paths)
    {
        foreach (var binding in node.Bindings)
        {
            paths.Add(GetBindingMemberName(binding));
        }

        if (node.Visible.IsBound && node.Visible.BindingPath != null)
        {
            paths.Add(node.Visible.BindingPath);
        }

        if (node.Enabled.IsBound && node.Enabled.BindingPath != null)
        {
            paths.Add(node.Enabled.BindingPath);
        }

        foreach (var child in node.Children)
        {
            CollectBindingPaths(child, paths);
        }
    }

    private static string InferPropertyType(string path, ComponentNode root)
    {
        // Try to infer type from usage
        var binding = FindBindingByMember(root, path);
        if (binding != null)
        {
            return binding.Property.ToLowerInvariant() switch
            {
                "value" => "double",
                "text" => "string?",
                "visible" => "bool",
                "enabled" => "bool",
                "items" => "System.Collections.IEnumerable?",
                "selecteditem" => "object?",
                "checked" => "bool",
                _ => "object?"
            };
        }

        return "object?";
    }

    private static BindingSpec? FindBindingByPath(ComponentNode node, string path)
    {
        var binding = node.Bindings.FirstOrDefault(b => b.Path == path);
        if (binding != null) return binding;

        foreach (var child in node.Children)
        {
            binding = FindBindingByPath(child, path);
            if (binding != null) return binding;
        }

        return null;
    }

    private static string GenerateCompose(HudDocument document, GenerationOptions options, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        var cb = new CodeBuilder();
        var viewModelNamespace = options.ViewModelNamespace ?? options.Namespace;

        cb.AppendLine("#nullable enable");
        cb.AppendLine();
        cb.AppendLine("using System;");
        cb.AppendLine("using System.Collections.Generic;");
        cb.AppendLine("using Avalonia.Controls;");

        if (!string.Equals(viewModelNamespace, options.Namespace, StringComparison.Ordinal))
        {
            cb.AppendLine($"using {viewModelNamespace};");
        }

        cb.AppendLine();
        cb.AppendLine($"namespace {options.Namespace};");
        cb.AppendLine();

        cb.AppendLine($"public static class {document.Name}_Compose");
        cb.OpenBlock();
        cb.AppendLine("public interface IChildVmResolver");
        cb.OpenBlock();
        cb.AppendLine("T Resolve<T>(object parentVm, string slotKey) where T : class;");
        cb.CloseBlock();
        cb.AppendLine();

        cb.AppendLine("private sealed class DisposableAction : IDisposable");
        cb.OpenBlock();
        cb.AppendLine("private readonly Action _dispose;");
        cb.AppendLine("public DisposableAction(Action dispose) { _dispose = dispose; }");
        cb.AppendLine("public void Dispose() { _dispose(); }");
        cb.CloseBlock();
        cb.AppendLine();

        cb.AppendLine("private sealed class CompositeDisposable : IDisposable");
        cb.OpenBlock();
        cb.AppendLine("private readonly List<IDisposable> _items = new();");
        cb.AppendLine("public void Add(IDisposable d) { _items.Add(d); }");
        cb.AppendLine("public void Dispose() { for (var i = _items.Count - 1; i >= 0; i--) _items[i].Dispose(); }");
        cb.CloseBlock();
        cb.AppendLine();

        cb.AppendLine($"public static IDisposable Apply({document.Name}View root, I{document.Name}ViewModel vm, IChildVmResolver resolver)");
        cb.OpenBlock();
        cb.AppendLine("var d = new CompositeDisposable();");
        cb.AppendLine("root.DataContext = vm;");
        cb.AppendLine("d.Add(new DisposableAction(() => root.DataContext = null));");

        var childInstances = new List<(ComponentNode Node, HudComponentDefinition Def)>();
        CollectComponentInstances(document.Root, components, childInstances);
        foreach (var (node, def) in childInstances)
        {
            var slotKey = node.SlotKey ?? node.Id ?? def.Name;
            var fieldName = ToXamlName(node.SlotKey ?? node.Id ?? string.Empty);
            if (string.IsNullOrWhiteSpace(fieldName))
                continue;

            cb.AppendLine();
            cb.AppendLine($"var childView = root.FindControl<global::Avalonia.Controls.Control>(\"{fieldName}\");");
            cb.AppendLine("if (childView == null) throw new InvalidOperationException(\"Could not find child control: \" + \"" + fieldName + "\");");
            cb.AppendLine($"var childVm = resolver.Resolve<I{def.Name}ViewModel>(vm, \"{slotKey.Replace("\"", "\\\"")}\");");
            cb.AppendLine("childView.DataContext = childVm;");
            cb.AppendLine("d.Add(new DisposableAction(() => childView.DataContext = null));");
        }

        cb.AppendLine();
        cb.AppendLine("return d;");
        cb.CloseBlock();

        cb.CloseBlock();
        return cb.ToString();
    }

    private static void CollectComponentInstances(ComponentNode node, IReadOnlyDictionary<string, HudComponentDefinition> components, List<(ComponentNode Node, HudComponentDefinition Def)> results)
    {
        if (node.ComponentRefId != null && components.TryGetValue(node.ComponentRefId, out var def))
        {
            results.Add((node, def));
        }

        foreach (var child in node.Children)
        {
            CollectComponentInstances(child, components, results);
        }
    }

    private static BindingSpec? FindBindingByMember(ComponentNode node, string member)
    {
        var binding = node.Bindings.FirstOrDefault(b => string.Equals(GetBindingMemberName(b), member, StringComparison.Ordinal));
        if (binding != null) return binding;

        foreach (var child in node.Children)
        {
            binding = FindBindingByMember(child, member);
            if (binding != null) return binding;
        }

        return null;
    }

    private static string XmlEscape(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
