using System.Globalization;
using System.Text;
using System.Text.Json;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Gen.Unity;

/// <summary>
/// Code generator for Unity UI Toolkit artifacts.
/// </summary>
public sealed class UnityGenerator : IBackendGenerator
{
    public string TargetFramework => "Unity UI Toolkit";

    public ICapabilityManifest Capabilities => UnityCapabilities.Instance;

    public GenerationResult Generate(HudDocument document, GenerationOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var files = new List<GeneratedFile>();

        if (options.EmitCompose)
        {
            diagnostics.Add(Diagnostic.Warning(
                "Unity compose helpers are not implemented yet; skipping compose output.",
                code: "BHU2000"));
        }

        try
        {
            foreach (var component in document.Components.Values)
            {
                var componentDocument = new HudDocument
                {
                    Name = component.Name,
                    Metadata = component.Metadata,
                    Root = component.Root,
                    Styles = document.Styles,
                    Components = document.Components
                };

                EmitDocumentArtifacts(componentDocument, options, diagnostics, files);
            }

            EmitDocumentArtifacts(document, options, diagnostics, files);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Diagnostic.Error($"Generation failed: {ex.Message}"));
        }

        return new GenerationResult
        {
            Files = files,
            Diagnostics = diagnostics
        };
    }

    private static void EmitDocumentArtifacts(
        HudDocument document,
        GenerationOptions options,
        List<Diagnostic> diagnostics,
        List<GeneratedFile> files)
    {
        var plan = UnityBackendPlanner.CreatePlan(document, options, diagnostics);

        files.Add(new GeneratedFile
        {
            Path = $"{document.Name}View.uxml",
            Content = GenerateUxml(plan),
            Type = GeneratedFileType.Markup
        });

        files.Add(new GeneratedFile
        {
            Path = $"{document.Name}View.uss",
            Content = GenerateUss(plan, options.Theme),
            Type = GeneratedFileType.Resource
        });

        files.Add(new GeneratedFile
        {
            Path = $"{document.Name}View.gen.cs",
            Content = GenerateController(document, plan, options),
            Type = GeneratedFileType.SourceCode
        });

        if (options.EmitViewModelInterfaces)
        {
            files.Add(new GeneratedFile
            {
                Path = $"I{document.Name}ViewModel.g.cs",
                Content = GenerateViewModelInterface(document.Name, plan, options),
                Type = GeneratedFileType.SourceCode
            });
        }
    }

    private static string GenerateUxml(UnityBackendPlan plan)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">");
        AppendUxmlNode(builder, plan.Root, 1);
        builder.AppendLine("</ui:UXML>");
        return builder.ToString();
    }

    private static void AppendUxmlNode(StringBuilder builder, UnityPlannedNode node, int indent)
    {
        var indentText = new string(' ', indent * 4);
        var classAttribute = node.CssClass;

        if (node.Children.Count == 0)
        {
            builder.Append(indentText);
            builder.Append('<');
            builder.Append("ui:");
            builder.Append(node.UxmlTag);
            builder.Append(" name=\"");
            builder.Append(XmlEscape(node.Name));
            builder.Append("\" class=\"");
            builder.Append(XmlEscape(classAttribute));
            builder.AppendLine("\" />");
            return;
        }

        builder.Append(indentText);
        builder.Append('<');
        builder.Append("ui:");
        builder.Append(node.UxmlTag);
        builder.Append(" name=\"");
        builder.Append(XmlEscape(node.Name));
        builder.Append("\" class=\"");
        builder.Append(XmlEscape(classAttribute));
        builder.AppendLine("\">");

        foreach (var child in node.Children)
        {
            AppendUxmlNode(builder, child, indent + 1);
        }

        builder.Append(indentText);
        builder.Append("</ui:");
        builder.Append(node.UxmlTag);
        builder.AppendLine(">");
    }

    private static string GenerateUss(UnityBackendPlan plan, ThemeDocument? theme)
    {
        var builder = new StringBuilder();
        AppendUssNode(builder, plan.Root, theme, parentLayoutType: null, parentGap: null, siblingIndex: 0);
        return builder.ToString();
    }

    private static void AppendUssNode(
        StringBuilder builder,
        UnityPlannedNode node,
        ThemeDocument? theme,
        LayoutType? parentLayoutType,
        Spacing? parentGap,
        int siblingIndex)
    {
        builder.Append('.');
        builder.Append(node.CssClass);
        builder.AppendLine(" {");

        AppendLayoutStyles(builder, node.Source, parentLayoutType, parentGap, siblingIndex);
        AppendVisualStyles(builder, node.Source.Style, theme);

        builder.AppendLine("}");
        builder.AppendLine();

        for (var index = 0; index < node.Children.Count; index++)
        {
            var child = node.Children[index];
            AppendUssNode(builder, child, theme, node.Source.Layout?.Type, node.Source.Layout?.Gap, index);
        }
    }

    private static void AppendLayoutStyles(
        StringBuilder builder,
        ComponentNode source,
        LayoutType? parentLayoutType,
        Spacing? parentGap,
        int siblingIndex)
    {
        var layout = source.Layout;
        var style = source.Style;

        if (layout == null && style == null)
        {
            return;
        }

        if (layout != null)
        {
            switch (layout.Type)
            {
                case LayoutType.Horizontal:
                    AppendCssDeclaration(builder, "flex-direction", "row");
                    break;
                case LayoutType.Vertical:
                case LayoutType.Stack:
                case LayoutType.Grid:
                case LayoutType.Dock:
                    AppendCssDeclaration(builder, "flex-direction", "column");
                    break;
                case LayoutType.Absolute:
                    break;
            }

            AppendAbsolutePlacementStyles(builder, source, parentLayoutType);

            AppendDimensionStyles(builder, "width", layout.Width, parentLayoutType);
            AppendDimensionStyles(builder, "height", layout.Height, parentLayoutType);
            AppendDimensionStyles(builder, "min-width", layout.MinWidth, parentLayoutType);
            AppendDimensionStyles(builder, "min-height", layout.MinHeight, parentLayoutType);
            AppendDimensionStyles(builder, "max-width", layout.MaxWidth, parentLayoutType);
            AppendDimensionStyles(builder, "max-height", layout.MaxHeight, parentLayoutType);
            AppendSpacingStyles(builder, "margin", MergeParentGapIntoMargin(layout.Margin, parentLayoutType, parentGap, siblingIndex));
            AppendSpacingStyles(builder, "padding", layout.Padding);

            if (layout.Weight is { } weight)
            {
                AppendCssDeclaration(builder, "flex-grow", weight.ToString(CultureInfo.InvariantCulture));
            }

            if (layout.Align is { } align)
            {
                AppendCssDeclaration(builder, "align-items", MapAlignment(align));
            }

            if (layout.Justify is { } justify)
            {
                AppendCssDeclaration(builder, "justify-content", MapJustification(justify));
            }
        }

        if (style != null)
        {
            AppendDimensionStyles(builder, "width", style.Width, parentLayoutType);
            AppendDimensionStyles(builder, "height", style.Height, parentLayoutType);
        }
    }

    private static Spacing? MergeParentGapIntoMargin(
        Spacing? margin,
        LayoutType? parentLayoutType,
        Spacing? parentGap,
        int siblingIndex)
    {
        if (siblingIndex <= 0 || parentGap == null)
        {
            return margin;
        }

        var resolvedMargin = margin ?? Spacing.Zero;

        return parentLayoutType switch
        {
            LayoutType.Horizontal => resolvedMargin with { Left = resolvedMargin.Left + parentGap.Value.Left },
            LayoutType.Vertical or LayoutType.Stack or LayoutType.Grid or LayoutType.Dock
                => resolvedMargin with { Top = resolvedMargin.Top + parentGap.Value.Top },
            _ => resolvedMargin
        };
    }

    private static void AppendAbsolutePlacementStyles(StringBuilder builder, ComponentNode source, LayoutType? parentLayoutType)
    {
        if (parentLayoutType != LayoutType.Absolute)
        {
            return;
        }

        var hasAbsoluteCoordinates = false;
        if (TryGetNumericMetadata(source, BoomHudMetadataKeys.PencilLeft, out var left))
        {
            if (!hasAbsoluteCoordinates)
            {
                AppendCssDeclaration(builder, "position", "absolute");
                hasAbsoluteCoordinates = true;
            }

            AppendCssDeclaration(builder, "left", ToPixels(left));
        }

        if (TryGetNumericMetadata(source, BoomHudMetadataKeys.PencilTop, out var top))
        {
            if (!hasAbsoluteCoordinates)
            {
                AppendCssDeclaration(builder, "position", "absolute");
                hasAbsoluteCoordinates = true;
            }

            AppendCssDeclaration(builder, "top", ToPixels(top));
        }
    }

    private static bool TryGetNumericMetadata(ComponentNode source, string key, out double value)
    {
        value = 0;
        if (!source.InstanceOverrides.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        switch (raw)
        {
            case double doubleValue:
                value = doubleValue;
                return true;
            case float floatValue:
                value = floatValue;
                return true;
            case int intValue:
                value = intValue;
                return true;
            case long longValue:
                value = longValue;
                return true;
            case string stringValue when double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    private static void AppendVisualStyles(StringBuilder builder, StyleSpec? style, ThemeDocument? theme)
    {
        if (style == null)
        {
            return;
        }

        var foreground = ResolveColor(style.Foreground, style.ForegroundToken, theme);
        if (foreground != null)
        {
            AppendCssDeclaration(builder, "color", foreground);
        }

        var background = ResolveColor(style.Background, style.BackgroundToken, theme);
        if (background != null)
        {
            AppendCssDeclaration(builder, "background-color", background);
        }

        var fontSize = ResolveDimension(style.FontSize, style.FontSizeToken, theme?.FontSizes);
        if (fontSize != null)
        {
            AppendCssDeclaration(builder, "font-size", ToPixels(fontSize.Value));
        }

        if (style.LetterSpacing is { } letterSpacing)
        {
            AppendCssDeclaration(builder, "letter-spacing", ToPixels(letterSpacing));
        }

        if (TryMapUnityFontStyle(style.FontWeight, style.FontStyle, out var unityFontStyle))
        {
            AppendCssDeclaration(builder, "-unity-font-style", unityFontStyle);
        }

        if (style.Opacity is { } opacity)
        {
            AppendCssDeclaration(builder, "opacity", opacity.ToString(CultureInfo.InvariantCulture));
        }

        if (style.BorderRadius is { } borderRadius)
        {
            AppendCssDeclaration(builder, "border-top-left-radius", ToPixels(borderRadius));
            AppendCssDeclaration(builder, "border-top-right-radius", ToPixels(borderRadius));
            AppendCssDeclaration(builder, "border-bottom-left-radius", ToPixels(borderRadius));
            AppendCssDeclaration(builder, "border-bottom-right-radius", ToPixels(borderRadius));
        }

        if (style.Border is { } border)
        {
            AppendCssDeclaration(builder, "border-left-width", ToPixels(border.Width));
            AppendCssDeclaration(builder, "border-right-width", ToPixels(border.Width));
            AppendCssDeclaration(builder, "border-top-width", ToPixels(border.Width));
            AppendCssDeclaration(builder, "border-bottom-width", ToPixels(border.Width));

            var borderColor = ResolveColor(border.Color, style.BorderColorToken, theme);
            if (borderColor != null)
            {
                AppendCssDeclaration(builder, "border-left-color", borderColor);
                AppendCssDeclaration(builder, "border-right-color", borderColor);
                AppendCssDeclaration(builder, "border-top-color", borderColor);
                AppendCssDeclaration(builder, "border-bottom-color", borderColor);
            }
        }
    }

    private static string GenerateController(HudDocument document, UnityBackendPlan plan, GenerationOptions options)
    {
        var builder = new StringBuilder();
        var viewModelNamespace = plan.ViewModelNamespace;
        var flattenedNodes = Flatten(plan.Root).ToList();
        var queryNodes = flattenedNodes.Skip(1).ToList();

        if (options.IncludeComments)
        {
            builder.AppendLine("// <auto-generated>");
            AppendInvariantLine(builder, $"// Generated by BoomHud.Gen.Unity from {document.Name}");
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine();
        }

        if (options.UseNullableAnnotations)
        {
            builder.AppendLine("#nullable enable");
            builder.AppendLine();
        }

        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.ComponentModel;");
        builder.AppendLine("using System.Globalization;");
        builder.AppendLine("using UnityEngine;");
        builder.AppendLine("using UnityEngine.TextCore.Text;");
        builder.AppendLine("using UnityEngine.UIElements;");
        if (!string.Equals(viewModelNamespace, options.Namespace, StringComparison.Ordinal))
        {
            AppendInvariantLine(builder, $"using {viewModelNamespace};");
        }

        builder.AppendLine();
        AppendInvariantLine(builder, $"namespace {options.Namespace}");
        builder.AppendLine("{");
        AppendInvariantLine(builder, $"public sealed class {document.Name}View");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly I" + document.Name + "ViewModel? _initialViewModel = null;");
        builder.AppendLine("    private I" + document.Name + "ViewModel? _viewModel;");
        builder.AppendLine();
        AppendInvariantLine(builder, $"    public {plan.Root.ElementType} Root {{ get; }}");

        foreach (var node in queryNodes)
        {
            AppendInvariantLine(builder, $"    public {node.ElementType} {node.Name} {{ get; }}");
        }

        builder.AppendLine();
        AppendInvariantLine(builder, $"    public I{document.Name}ViewModel? ViewModel");
        builder.AppendLine("    {");
        builder.AppendLine("        get => _viewModel;");
        builder.AppendLine("        set");
        builder.AppendLine("        {");
        builder.AppendLine("            if (_viewModel != null)");
        builder.AppendLine("            {");
        builder.AppendLine("                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            _viewModel = value;");
        builder.AppendLine();
        builder.AppendLine("            if (_viewModel != null)");
        builder.AppendLine("            {");
        builder.AppendLine("                _viewModel.PropertyChanged += OnViewModelPropertyChanged;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            Refresh();");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        AppendInvariantLine(builder, $"    public {document.Name}View(VisualElement root)");
        builder.AppendLine("    {");
        AppendInvariantLine(builder, $"        Root = root as {plan.Root.ElementType} ?? throw new ArgumentException(\"Expected root element type {plan.Root.ElementType}.\", nameof(root));");

        foreach (var node in queryNodes)
        {
            AppendInvariantLine(builder, $"        {node.Name} = Root.Q<{node.ElementType}>(\"{node.Name}\") ?? throw new InvalidOperationException(\"Could not find generated element '{node.Name}'.\");");
        }

        builder.AppendLine();
        builder.AppendLine("        ApplyStaticValues();");
        builder.AppendLine("        ViewModel = _initialViewModel;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public void Refresh()");
        builder.AppendLine("    {");
        builder.AppendLine("        if (_viewModel == null)");
        builder.AppendLine("        {");
        builder.AppendLine("            return;");
        builder.AppendLine("        }");
        builder.AppendLine();

        foreach (var node in flattenedNodes)
        {
            AppendBindingRefreshStatements(builder, node, plan, options.Theme);
        }

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private void ApplyStaticValues()");
        builder.AppendLine("    {");

        foreach (var node in flattenedNodes)
        {
            AppendStaticAssignments(builder, node, plan.Root);
        }

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)");
        builder.AppendLine("    {");
        builder.AppendLine("        Refresh();");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static readonly Dictionary<string, FontDefinition> s_fontDefinitions = new(StringComparer.OrdinalIgnoreCase);");
        builder.AppendLine("    private static readonly Dictionary<string, FontAsset> s_sdfFontAssets = new(StringComparer.OrdinalIgnoreCase);");
        builder.AppendLine("    private static readonly Dictionary<string, string> s_fontResourcePaths = new(StringComparer.OrdinalIgnoreCase)");
        builder.AppendLine("    {");
        builder.AppendLine("        [\"Press Start 2P\"] = \"BoomHudFonts/PressStart2P-Regular\",");
        builder.AppendLine("        [\"lucide\"] = \"BoomHudFonts/lucide\"");
        builder.AppendLine("    };");
        builder.AppendLine("    private static readonly Dictionary<string, string> s_lucideGlyphs = new(StringComparer.OrdinalIgnoreCase)");
        builder.AppendLine("    {");
        builder.AppendLine("        [\"cross\"] = \"\\uE1E5\",");
        builder.AppendLine("        [\"flame\"] = \"\\uE0D2\",");
        builder.AppendLine("        [\"flask-conical\"] = \"\\uE0D5\",");
        builder.AppendLine("        [\"moon\"] = \"\\uE11E\",");
        builder.AppendLine("        [\"shield\"] = \"\\uE158\",");
        builder.AppendLine("        [\"sparkles\"] = \"\\uE412\",");
        builder.AppendLine("        [\"sword\"] = \"\\uE2B3\",");
        builder.AppendLine("        [\"swords\"] = \"\\uE2B4\",");
        builder.AppendLine("        [\"wand\"] = \"\\uE246\",");
        builder.AppendLine("        [\"wand-2\"] = \"\\uE357\",");
        builder.AppendLine("        [\"wand-sparkles\"] = \"\\uE357\"");
        builder.AppendLine("    };");
        builder.AppendLine();
        builder.AppendLine("    private static string AsString(object? value) => value?.ToString() ?? string.Empty;");
        builder.AppendLine();
        builder.AppendLine("    private static float AsFloat(object? value)");
        builder.AppendLine("    {");
        builder.AppendLine("        return value switch");
        builder.AppendLine("        {");
        builder.AppendLine("            null => 0f,");
        builder.AppendLine("            float floatValue => floatValue,");
        builder.AppendLine("            double doubleValue => (float)doubleValue,");
        builder.AppendLine("            int intValue => intValue,");
        builder.AppendLine("            long longValue => longValue,");
        builder.AppendLine("            decimal decimalValue => (float)decimalValue,");
        builder.AppendLine("            string stringValue when float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,");
        builder.AppendLine("            _ => 0f");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static bool AsBool(object? value)");
        builder.AppendLine("    {");
        builder.AppendLine("        return value switch");
        builder.AppendLine("        {");
        builder.AppendLine("            bool boolValue => boolValue,");
        builder.AppendLine("            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,");
        builder.AppendLine("            int intValue => intValue != 0,");
        builder.AppendLine("            long longValue => longValue != 0,");
        builder.AppendLine("            _ => false");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static string? ResolveMappedStyleValue(object? value, string? fallbackValue, params string[] mappings)");
        builder.AppendLine("    {");
        builder.AppendLine("        var key = AsString(value);");
        builder.AppendLine();
        builder.AppendLine("        for (var index = 0; index + 1 < mappings.Length; index += 2)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (string.Equals(mappings[index], key, StringComparison.OrdinalIgnoreCase))");
        builder.AppendLine("            {");
        builder.AppendLine("                return mappings[index + 1];");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return string.IsNullOrWhiteSpace(key) ? fallbackValue : key;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static Color ParseStyleColor(string? value, string? fallbackValue)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (!string.IsNullOrWhiteSpace(value) && ColorUtility.TryParseHtmlString(value, out var parsed))");
        builder.AppendLine("        {");
        builder.AppendLine("            return parsed;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (!string.IsNullOrWhiteSpace(fallbackValue) && ColorUtility.TryParseHtmlString(fallbackValue, out var fallback))");
        builder.AppendLine("        {");
        builder.AppendLine("            return fallback;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return default;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static void ApplyTextLabelStyle(Label label)");
        builder.AppendLine("    {");
        builder.AppendLine("        label.style.whiteSpace = WhiteSpace.NoWrap;");
        builder.AppendLine("        label.style.flexShrink = 0;");
        builder.AppendLine("        label.style.overflow = Overflow.Visible;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static bool ApplyFontFamily(VisualElement element, string? familyName, float pointSize)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (element == null || string.IsNullOrWhiteSpace(familyName))");
        builder.AppendLine("        {");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (!TryResolveFontDefinition(familyName, pointSize, out var fontDefinition))");
        builder.AppendLine("        {");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        element.style.unityFontDefinition = fontDefinition;");
        builder.AppendLine("        return true;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static bool TryResolveFontDefinition(string? familyName, float pointSize, out FontDefinition fontDefinition)");
        builder.AppendLine("    {");
        builder.AppendLine("        fontDefinition = default;");
        builder.AppendLine();
        builder.AppendLine("        if (string.IsNullOrWhiteSpace(familyName))");
        builder.AppendLine("        {");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var normalizedFamily = familyName.Trim();");
        builder.AppendLine("        var normalizedSize = Mathf.Max(1, Mathf.RoundToInt(pointSize));");
        builder.AppendLine("        var cacheKey = $\"{normalizedFamily}|{normalizedSize}\";");
        builder.AppendLine();
        builder.AppendLine("        if (s_fontDefinitions.TryGetValue(cacheKey, out fontDefinition))");
        builder.AppendLine("        {");
        builder.AppendLine("            return true;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (TryLoadSdfFontAsset(normalizedFamily, out var fontAsset))");
        builder.AppendLine("        {");
        builder.AppendLine("            fontDefinition = FontDefinition.FromSDFFont(fontAsset);");
        builder.AppendLine("            s_fontDefinitions[cacheKey] = fontDefinition;");
        builder.AppendLine("            return true;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (!TryLoadFont(normalizedFamily, normalizedSize, out var font))");
        builder.AppendLine("        {");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        fontDefinition = FontDefinition.FromFont(font);");
        builder.AppendLine("        s_fontDefinitions[cacheKey] = fontDefinition;");
        builder.AppendLine("        return true;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static bool TryLoadSdfFontAsset(string familyName, out FontAsset fontAsset)");
        builder.AppendLine("    {");
        builder.AppendLine("        fontAsset = null!;");
        builder.AppendLine();
        builder.AppendLine("        var normalizedFamily = familyName.Trim();");
        builder.AppendLine("        if (s_sdfFontAssets.TryGetValue(normalizedFamily, out fontAsset))");
        builder.AppendLine("        {");
        builder.AppendLine("            return fontAsset != null;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        fontAsset = LoadBundledSdfFontAsset(normalizedFamily)!;");
        builder.AppendLine("        if (fontAsset == null)");
        builder.AppendLine("        {");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        s_sdfFontAssets[normalizedFamily] = fontAsset;");
        builder.AppendLine("        return true;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static FontAsset? LoadBundledSdfFontAsset(string familyName)");
        builder.AppendLine("    {");
        builder.AppendLine("        var normalizedFamily = familyName.Trim();");
        builder.AppendLine();
        builder.AppendLine("        if (s_fontResourcePaths.TryGetValue(normalizedFamily, out var resourcePath))");
        builder.AppendLine("        {");
        builder.AppendLine("            return Resources.Load<FontAsset>(resourcePath);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var compactName = normalizedFamily.Replace(\" \", string.Empty, StringComparison.Ordinal).Replace(\"-\", string.Empty, StringComparison.Ordinal);");
        builder.AppendLine("        return Resources.Load<FontAsset>($\"BoomHudFonts/{compactName}\") ?? Resources.Load<FontAsset>($\"BoomHudFonts/{normalizedFamily}\");");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static bool TryLoadFont(string familyName, int pointSize, out Font font)");
        builder.AppendLine("    {");
        builder.AppendLine("        font = null!;");
        builder.AppendLine();
        builder.AppendLine("        var resourceFont = LoadBundledFont(familyName);");
        builder.AppendLine("        if (resourceFont != null)");
        builder.AppendLine("        {");
        builder.AppendLine("            font = resourceFont;");
        builder.AppendLine("            return true;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        try");
        builder.AppendLine("        {");
        builder.AppendLine("            var osFont = Font.CreateDynamicFontFromOSFont(familyName, pointSize);");
        builder.AppendLine("            if (osFont == null)");
        builder.AppendLine("            {");
        builder.AppendLine("                return false;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            font = osFont;");
        builder.AppendLine("            return true;");
        builder.AppendLine("        }");
        builder.AppendLine("        catch");
        builder.AppendLine("        {");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static Font? LoadBundledFont(string familyName)");
        builder.AppendLine("    {");
        builder.AppendLine("        var normalizedFamily = familyName.Trim();");
        builder.AppendLine();
        builder.AppendLine("        if (s_fontResourcePaths.TryGetValue(normalizedFamily, out var resourcePath))");
        builder.AppendLine("        {");
        builder.AppendLine("            return Resources.Load<Font>(resourcePath);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var compactName = normalizedFamily.Replace(\" \", string.Empty, StringComparison.Ordinal).Replace(\"-\", string.Empty, StringComparison.Ordinal);");
        builder.AppendLine("        return Resources.Load<Font>($\"BoomHudFonts/{compactName}\") ?? Resources.Load<Font>($\"BoomHudFonts/{normalizedFamily}\");");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static string ResolveIconText(string value, string? familyName, float pointSize)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (!TryResolveFontDefinition(familyName, pointSize, out _))");
        builder.AppendLine("        {");
        builder.AppendLine("            return NormalizeIconText(value);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (TryResolveIconGlyphText(value, familyName, out var glyphText))");
        builder.AppendLine("        {");
        builder.AppendLine("            return glyphText;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return string.Equals(familyName?.Trim(), \"lucide\", StringComparison.OrdinalIgnoreCase) ? NormalizeIconText(value) : value;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static bool TryResolveIconGlyphText(string value, string? familyName, out string glyphText)");
        builder.AppendLine("    {");
        builder.AppendLine("        glyphText = string.Empty;");
        builder.AppendLine();
        builder.AppendLine("        if (string.IsNullOrWhiteSpace(familyName))");
        builder.AppendLine("        {");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var normalizedFamily = familyName.Trim();");
        builder.AppendLine("        if (string.Equals(normalizedFamily, \"lucide\", StringComparison.OrdinalIgnoreCase) && s_lucideGlyphs.TryGetValue(value, out glyphText))");
        builder.AppendLine("        {");
        builder.AppendLine("            return true;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return false;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static string NormalizeIconText(string value)");
        builder.AppendLine("    {");
        builder.AppendLine("        return value switch");
        builder.AppendLine("        {");
        builder.AppendLine("            \"sword\" => \"†\",");
        builder.AppendLine("            \"swords\" => \"⚔\",");
        builder.AppendLine("            \"sparkles\" => \"✦\",");
        builder.AppendLine("            \"wand-sparkles\" => \"✦\",");
        builder.AppendLine("            \"shield\" => \"⛨\",");
        builder.AppendLine("            \"flask-conical\" => \"⚗\",");
        builder.AppendLine("            \"flame\" => \"✹\",");
        builder.AppendLine("            \"moon\" => \"☾\",");
        builder.AppendLine("            \"cross\" => \"✚\",");
        builder.AppendLine("            _ => value");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static void ApplyIconLabelStyle(Label label, float boxWidth, float boxHeight)");
        builder.AppendLine("    {");
        builder.AppendLine("        var iconSize = Mathf.Max(1f, Mathf.Min(boxWidth, boxHeight));");
        builder.AppendLine("        label.style.unityTextAlign = TextAnchor.MiddleCenter;");
        builder.AppendLine("        label.style.unityFontStyleAndWeight = FontStyle.Normal;");
        builder.AppendLine("        label.style.whiteSpace = WhiteSpace.NoWrap;");
        builder.AppendLine("        label.style.flexShrink = 0;");
        builder.AppendLine("        label.style.alignItems = Align.Center;");
        builder.AppendLine("        label.style.justifyContent = Justify.Center;");
        builder.AppendLine("        label.style.overflow = Overflow.Visible;");
        builder.AppendLine("        label.style.paddingLeft = 0f;");
        builder.AppendLine("        label.style.paddingTop = 0f;");
        builder.AppendLine("        label.style.paddingRight = 0f;");
        builder.AppendLine("        label.style.paddingBottom = 0f;");
        builder.AppendLine("        label.style.width = boxWidth;");
        builder.AppendLine("        label.style.height = boxHeight;");
        builder.AppendLine("        label.style.minWidth = boxWidth;");
        builder.AppendLine("        label.style.minHeight = boxHeight;");
        builder.AppendLine("        label.style.fontSize = iconSize;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendBindingRefreshStatements(StringBuilder builder, UnityPlannedNode node, UnityBackendPlan plan, ThemeDocument? theme)
    {
        var accessor = GetNodeAccessor(node, plan.Root);

        foreach (var binding in node.Source.Bindings)
        {
            if (!TryGetViewModelIdentifier(plan, binding.Path, out var identifier))
            {
                continue;
            }

            if (TryBuildRefreshAssignment(accessor, node, binding, identifier, theme, out var assignment))
            {
                builder.AppendLine("        " + assignment);
            }
        }

        if (node.Source.Visible.IsBound && node.Source.Visible.BindingPath != null && TryGetViewModelIdentifier(plan, node.Source.Visible.BindingPath, out var visibleIdentifier))
        {
            AppendInvariantLine(builder, $"        {accessor}.style.display = AsBool(_viewModel.{visibleIdentifier}) ? DisplayStyle.Flex : DisplayStyle.None;");
        }

        if (node.Source.Enabled.IsBound && node.Source.Enabled.BindingPath != null && TryGetViewModelIdentifier(plan, node.Source.Enabled.BindingPath, out var enabledIdentifier))
        {
            AppendInvariantLine(builder, $"        {accessor}.SetEnabled(AsBool(_viewModel.{enabledIdentifier}));");
        }

        if (node.Source.Tooltip is { IsBound: true, BindingPath: not null } tooltip && TryGetViewModelIdentifier(plan, tooltip.BindingPath, out var tooltipIdentifier))
        {
            AppendInvariantLine(builder, $"        {accessor}.tooltip = AsString(_viewModel.{tooltipIdentifier});");
        }
    }

    private static bool TryBuildRefreshAssignment(string accessor, UnityPlannedNode node, BindingSpec binding, string identifier, ThemeDocument? theme, out string assignment)
    {
        var normalizedProperty = binding.Property.ToLowerInvariant();
        var sourceExpression = $"_viewModel.{identifier}";

        switch (normalizedProperty)
        {
            case "style.foreground":
                var foregroundFallback = ResolveColor(node.Source.Style?.Foreground, node.Source.Style?.ForegroundToken, theme);
                assignment = $"{accessor}.style.color = ParseStyleColor({BuildColorBindingExpression(binding, sourceExpression, foregroundFallback, theme)}, {ToNullableStringLiteral(foregroundFallback)});";
                return true;
            case "style.background":
                var backgroundFallback = ResolveColor(node.Source.Style?.Background, node.Source.Style?.BackgroundToken, theme);
                assignment = $"{accessor}.style.backgroundColor = ParseStyleColor({BuildColorBindingExpression(binding, sourceExpression, backgroundFallback, theme)}, {ToNullableStringLiteral(backgroundFallback)});";
                return true;
            case "style.bordercolor":
                var borderColorFallback = ResolveColor(node.Source.Style?.Border?.Color, node.Source.Style?.BorderColorToken, theme);
                assignment = string.Join(Environment.NewLine,
                    $"{accessor}.style.borderLeftColor = ParseStyleColor({BuildColorBindingExpression(binding, sourceExpression, borderColorFallback, theme)}, {ToNullableStringLiteral(borderColorFallback)});",
                    $"        {accessor}.style.borderRightColor = ParseStyleColor({BuildColorBindingExpression(binding, sourceExpression, borderColorFallback, theme)}, {ToNullableStringLiteral(borderColorFallback)});",
                    $"        {accessor}.style.borderTopColor = ParseStyleColor({BuildColorBindingExpression(binding, sourceExpression, borderColorFallback, theme)}, {ToNullableStringLiteral(borderColorFallback)});",
                    $"        {accessor}.style.borderBottomColor = ParseStyleColor({BuildColorBindingExpression(binding, sourceExpression, borderColorFallback, theme)}, {ToNullableStringLiteral(borderColorFallback)});");
                return true;
            case "style.borderwidth":
                assignment = string.Join(Environment.NewLine,
                    $"{accessor}.style.borderLeftWidth = AsFloat({sourceExpression});",
                    $"        {accessor}.style.borderRightWidth = AsFloat({sourceExpression});",
                    $"        {accessor}.style.borderTopWidth = AsFloat({sourceExpression});",
                    $"        {accessor}.style.borderBottomWidth = AsFloat({sourceExpression});");
                return true;
            case "style.opacity":
                assignment = $"{accessor}.style.opacity = AsFloat({sourceExpression});";
                return true;
            default:
                return TryBuildAssignment(accessor, node, binding.Property, identifier, isStatic: false, out assignment);
        }
    }

    private static void AppendStaticAssignments(StringBuilder builder, UnityPlannedNode node, UnityPlannedNode root)
    {
        var accessor = GetNodeAccessor(node, root);

        if (node.Source.Type == ComponentType.TextArea && accessor != "Root")
        {
            AppendInvariantLine(builder, $"        {accessor}.multiline = true;");
        }

        foreach (var binding in GetBindings(node.Source))
        {
            if (binding.StaticValue == null)
            {
                continue;
            }

            if (TryBuildAssignment(accessor, node, binding.Property, binding.StaticValue, isStatic: true, out var assignment))
            {
                builder.AppendLine("        " + assignment);
            }
        }

        if (!node.Source.Visible.IsBound && node.Source.Visible.Value is { } isVisible)
        {
            AppendInvariantLine(builder, $"        {accessor}.style.display = {(isVisible ? "DisplayStyle.Flex" : "DisplayStyle.None")};");
        }

        if (!node.Source.Enabled.IsBound && node.Source.Enabled.Value is { } isEnabled)
        {
            AppendInvariantLine(builder, $"        {accessor}.SetEnabled({(isEnabled ? "true" : "false")});");
        }

        if (node.Source.Tooltip is { IsBound: false, Value: not null } tooltip)
        {
            AppendInvariantLine(builder, $"        {accessor}.tooltip = {ToStringLiteral(tooltip.Value)};");
        }

        if (TryGetStaticFontFamilyAssignment(accessor, node, out var fontAssignment))
        {
            builder.AppendLine("        " + fontAssignment);
        }
    }

    private static bool TryGetStaticFontFamilyAssignment(string accessor, UnityPlannedNode node, out string assignment)
    {
        assignment = string.Empty;

        var style = node.Source.Style;

        if (style == null || string.IsNullOrWhiteSpace(style.FontFamily))
        {
            return false;
        }

        var fontSizeValue = style.FontSize;
        if (fontSizeValue == null && node.Source.Type == ComponentType.Icon)
        {
            fontSizeValue = GetNodePixelDimension(node.Source.Layout?.Width)
                ?? GetNodePixelDimension(node.Source.Layout?.Height)
                ?? GetNodePixelDimension(node.Source.Style?.Width)
                ?? GetNodePixelDimension(node.Source.Style?.Height)
                ?? 16d;
        }

        var fontSize = ToFloatLiteral(fontSizeValue ?? 16d);
        assignment = $"ApplyFontFamily({accessor}, {ToStringLiteral(style.FontFamily)}, {fontSize});";
        return true;
    }

    private static bool TryBuildAssignment(string accessor, UnityPlannedNode node, string property, string valueExpression, bool isStatic, out string assignment)
    {
        assignment = string.Empty;
        var normalizedProperty = property.ToLowerInvariant();

        switch (node.Source.Type)
        {
            case ComponentType.Label:
            case ComponentType.Badge:
                if (normalizedProperty is "text" or "value")
                {
                    assignment = string.Join(Environment.NewLine,
                        $"{accessor}.text = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};",
                        $"        ApplyTextLabelStyle({accessor});");
                    return true;
                }
                break;
            case ComponentType.Button:
                if (normalizedProperty is "text" or "value")
                {
                    assignment = $"{accessor}.text = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
            case ComponentType.Icon:
                if (normalizedProperty is "text" or "value")
                {
                    var rawTextExpression = isStatic ? valueExpression : $"AsString(_viewModel.{valueExpression})";
                    var iconFontFamily = ToNullableStringLiteral(node.Source.Style?.FontFamily);
                    var iconPointSize = ToFloatLiteral(node.Source.Style?.FontSize ?? GetNodePixelDimension(node.Source.Layout?.Width) ?? GetNodePixelDimension(node.Source.Layout?.Height) ?? 16d);
                    var textExpression = $"ResolveIconText({rawTextExpression}, {iconFontFamily}, {iconPointSize})";
                    var iconWidth = ToFloatLiteral(GetNodePixelDimension(node.Source.Layout?.Width) ?? GetNodePixelDimension(node.Source.Style?.Width) ?? 16d);
                    var iconHeight = ToFloatLiteral(GetNodePixelDimension(node.Source.Layout?.Height) ?? GetNodePixelDimension(node.Source.Style?.Height) ?? 16d);
                    assignment = string.Join(Environment.NewLine,
                        $"{accessor}.text = {textExpression};",
                        $"        ApplyIconLabelStyle({accessor}, {iconWidth}, {iconHeight});");
                    return true;
                }
                break;
            case ComponentType.TextInput:
            case ComponentType.TextArea:
                if (normalizedProperty is "text" or "value")
                {
                    assignment = $"{accessor}.value = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
            case ComponentType.Checkbox:
            case ComponentType.RadioButton:
                if (normalizedProperty == "value")
                {
                    assignment = $"{accessor}.value = {(isStatic ? valueExpression : "AsBool(_viewModel." + valueExpression + ")")};";
                    return true;
                }

                if (normalizedProperty == "text")
                {
                    assignment = $"{accessor}.text = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
            case ComponentType.ProgressBar:
                if (normalizedProperty == "value")
                {
                    assignment = $"{accessor}.value = {(isStatic ? valueExpression : "AsFloat(_viewModel." + valueExpression + ")")};";
                    return true;
                }

                if (normalizedProperty == "max")
                {
                    assignment = $"{accessor}.highValue = {(isStatic ? valueExpression : "AsFloat(_viewModel." + valueExpression + ")")};";
                    return true;
                }

                if (normalizedProperty == "text")
                {
                    assignment = $"{accessor}.title = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
            case ComponentType.Slider:
                if (normalizedProperty == "value")
                {
                    assignment = $"{accessor}.value = {(isStatic ? valueExpression : "AsFloat(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
            case ComponentType.Image:
                if (normalizedProperty == "tooltip")
                {
                    assignment = $"{accessor}.tooltip = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
        }

        return false;
    }

    private static bool TryMapUnityFontStyle(FontWeight? fontWeight, FontStyle? fontStyle, out string unityFontStyle)
    {
        var isBold = fontWeight == FontWeight.Bold;
        var isItalic = fontStyle == FontStyle.Italic;

        unityFontStyle = (isBold, isItalic) switch
        {
            (true, true) => "bold-and-italic",
            (true, false) => "bold",
            (false, true) => "italic",
            _ => "normal"
        };

        return fontWeight != null || fontStyle != null;
    }
    private static IEnumerable<(string Property, string? StaticValue, string? BindingPath)> GetBindings(ComponentNode node)
    {
        foreach (var binding in node.Bindings)
        {
            yield return (binding.Property, null, binding.Path);
        }

        foreach (var property in node.Properties)
        {
            if (property.Value.IsBound)
            {
                yield return (property.Key, null, property.Value.BindingPath);
                continue;
            }

            yield return (property.Key, ToValueLiteral(property.Value.Value), null);
        }
    }

    private static bool TryGetViewModelIdentifier(UnityBackendPlan plan, string path, out string identifier)
    {
        var property = plan.ViewModelProperties.FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.Ordinal));
        if (property == null)
        {
            identifier = string.Empty;
            return false;
        }

        identifier = property.Identifier;
        return true;
    }

    private static string GetNodeAccessor(UnityPlannedNode node, UnityPlannedNode root)
        => string.Equals(node.Name, root.Name, StringComparison.Ordinal) ? "Root" : node.Name;

    private static IEnumerable<UnityPlannedNode> Flatten(UnityPlannedNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private static string GenerateViewModelInterface(string documentName, UnityBackendPlan plan, GenerationOptions options)
    {
        var builder = new StringBuilder();

        if (options.IncludeComments)
        {
            builder.AppendLine("// <auto-generated>");
            AppendInvariantLine(builder, $"// Generated by BoomHud.Gen.Unity from {documentName}");
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine();
        }

        if (options.UseNullableAnnotations)
        {
            builder.AppendLine("#nullable enable");
            builder.AppendLine();
        }

        builder.AppendLine("using System.ComponentModel;");
        builder.AppendLine();
        AppendInvariantLine(builder, $"namespace {plan.ViewModelNamespace}");
        builder.AppendLine("{");
        AppendInvariantLine(builder, $"public interface I{documentName}ViewModel : INotifyPropertyChanged");
        builder.AppendLine("{");
        foreach (var property in plan.ViewModelProperties)
        {
            AppendInvariantLine(builder, $"    object? {property.Identifier} {{ get; }}");
        }

        builder.AppendLine("}");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendDimensionStyles(StringBuilder builder, string propertyName, Dimension? dimension, LayoutType? parentLayoutType)
    {
        if (dimension == null)
        {
            return;
        }

        switch (dimension.Value.Unit)
        {
            case DimensionUnit.Pixels:
                AppendCssDeclaration(builder, propertyName, ToPixels(dimension.Value.Value));
                break;
            case DimensionUnit.Percent:
                AppendCssDeclaration(builder, propertyName, dimension.Value.Value.ToString(CultureInfo.InvariantCulture) + "%");
                break;
            case DimensionUnit.Cells:
                AppendCssDeclaration(builder, propertyName, ToPixels(dimension.Value.Value));
                break;
            case DimensionUnit.Auto:
                AppendCssDeclaration(builder, propertyName, "auto");
                break;
            case DimensionUnit.Fill:
            case DimensionUnit.Star:
                AppendFillDimensionStyles(builder, propertyName, dimension.Value.Value == 0 ? 1 : dimension.Value.Value, parentLayoutType);
                break;
        }
    }

    private static void AppendFillDimensionStyles(StringBuilder builder, string propertyName, double value, LayoutType? parentLayoutType)
    {
        var growValue = value.ToString(CultureInfo.InvariantCulture);

        switch (propertyName)
        {
            case "width":
                if (parentLayoutType == LayoutType.Horizontal)
                {
                    AppendCssDeclaration(builder, "flex-grow", growValue);
                    return;
                }

                if (parentLayoutType is LayoutType.Vertical or LayoutType.Stack or LayoutType.Grid or LayoutType.Dock)
                {
                    AppendCssDeclaration(builder, "align-self", "stretch");
                    return;
                }

                AppendCssDeclaration(builder, propertyName, "100%");
                return;

            case "height":
                if (parentLayoutType is LayoutType.Vertical or LayoutType.Stack or LayoutType.Grid or LayoutType.Dock)
                {
                    AppendCssDeclaration(builder, "flex-grow", growValue);
                    return;
                }

                if (parentLayoutType == LayoutType.Horizontal)
                {
                    AppendCssDeclaration(builder, "align-self", "stretch");
                    return;
                }

                AppendCssDeclaration(builder, propertyName, "100%");
                return;

            default:
                AppendCssDeclaration(builder, "flex-grow", growValue);
                return;
        }
    }

    private static void AppendSpacingStyles(StringBuilder builder, string propertyName, Spacing? spacing)
    {
        if (spacing == null)
        {
            return;
        }

        AppendCssDeclaration(builder, propertyName + "-top", ToPixels(spacing.Value.Top));
        AppendCssDeclaration(builder, propertyName + "-right", ToPixels(spacing.Value.Right));
        AppendCssDeclaration(builder, propertyName + "-bottom", ToPixels(spacing.Value.Bottom));
        AppendCssDeclaration(builder, propertyName + "-left", ToPixels(spacing.Value.Left));
    }

    private static void AppendCssDeclaration(StringBuilder builder, string propertyName, string value)
    {
        builder.Append("    ");
        builder.Append(propertyName);
        builder.Append(": ");
        builder.Append(value);
        builder.AppendLine(";");
    }

    private static string MapAlignment(Alignment alignment) => alignment switch
    {
        Alignment.Start => "flex-start",
        Alignment.Center => "center",
        Alignment.End => "flex-end",
        Alignment.Stretch => "stretch",
        _ => "stretch"
    };

    private static string MapJustification(Justification justification) => justification switch
    {
        Justification.Start => "flex-start",
        Justification.Center => "center",
        Justification.End => "flex-end",
        Justification.SpaceBetween => "space-between",
        Justification.SpaceAround => "space-around",
        Justification.SpaceEvenly => "space-evenly",
        _ => "flex-start"
    };

    private static string ToPixels(double value)
        => value.ToString(CultureInfo.InvariantCulture) + "px";

    private static double? GetNodePixelDimension(Dimension? dimension)
        => dimension is { Unit: DimensionUnit.Pixels } ? dimension.Value.Value : null;

    private static string ToFloatLiteral(double value)
        => value.ToString(CultureInfo.InvariantCulture) + "f";

    private static void AppendInvariantLine(StringBuilder builder, FormattableString value)
        => builder.AppendLine(global::System.FormattableString.Invariant(value));

    private static string? ResolveColor(Color? value, string? token, ThemeDocument? theme)
    {
        if (value is { } explicitColor)
        {
            return explicitColor.ToHex();
        }

        if (!string.IsNullOrWhiteSpace(token) && theme != null && theme.Colors.TryGetValue(token, out var themedColor))
        {
            return themedColor.ToHex();
        }

        return null;
    }

    private static string BuildColorBindingExpression(BindingSpec binding, string sourceExpression, string? defaultFallback, ThemeDocument? theme)
    {
        var fallback = ResolveBindingColorLiteral(binding.Fallback, theme) ?? defaultFallback;
        var arguments = new List<string>
        {
            sourceExpression,
            ToNullableStringLiteral(fallback)
        };

        foreach (var (key, value) in ResolveColorMap(binding.ConverterParameter, theme))
        {
            arguments.Add(ToStringLiteral(key));
            arguments.Add(ToStringLiteral(value));
        }

        return $"ResolveMappedStyleValue({string.Join(", ", arguments)})";
    }

    private static List<(string Key, string Value)> ResolveColorMap(object? value, ThemeDocument? theme)
    {
        if (value is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var result = new List<(string Key, string Value)>();
        foreach (var property in element.EnumerateObject())
        {
            var resolved = ResolveBindingColorLiteral(property.Value, theme);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                result.Add((property.Name, resolved));
            }
        }

        return result;
    }

    private static string? ResolveBindingColorLiteral(object? value, ThemeDocument? theme)
    {
        return value switch
        {
            null => null,
            string stringValue => ResolveBindingColorString(stringValue, theme),
            JsonElement element => ResolveBindingColorElement(element, theme),
            _ => null
        };
    }

    private static string? ResolveBindingColorElement(JsonElement element, ThemeDocument? theme)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => ResolveBindingColorString(element.GetString(), theme),
            JsonValueKind.Object when element.TryGetProperty("$ref", out var refProperty) => ResolveBindingColorString(refProperty.GetString(), theme),
            _ => null
        };
    }

    private static string? ResolveBindingColorString(string? value, ThemeDocument? theme)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.StartsWith('#') || value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var normalizedToken = value.StartsWith("tokens.", StringComparison.OrdinalIgnoreCase)
            ? value["tokens.".Length..]
            : value.StartsWith('$') ? value[1..] : value;

        if (theme != null && theme.Colors.TryGetValue(normalizedToken, out var themedColor))
        {
            return themedColor.ToHex();
        }

        return null;
    }

    private static double? ResolveDimension(double? value, string? token, IReadOnlyDictionary<string, double>? tokenValues)
    {
        if (value is { } explicitValue)
        {
            return explicitValue;
        }

        if (!string.IsNullOrWhiteSpace(token) && tokenValues != null && tokenValues.TryGetValue(token, out var themedValue))
        {
            return themedValue;
        }

        return null;
    }

    private static string XmlEscape(string value)
        => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string? ToValueLiteral(object? value)
    {
        return value switch
        {
            null => null,
            string stringValue => ToStringLiteral(stringValue),
            bool boolValue => boolValue ? "true" : "false",
            float floatValue => floatValue.ToString(CultureInfo.InvariantCulture) + "f",
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            long longValue => longValue.ToString(CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture) + "m",
            _ => ToStringLiteral(value.ToString() ?? string.Empty)
        };
    }

    private static string ToStringLiteral(string value)
        => "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    private static string ToNullableStringLiteral(string? value)
        => value == null ? "null" : ToStringLiteral(value);
}