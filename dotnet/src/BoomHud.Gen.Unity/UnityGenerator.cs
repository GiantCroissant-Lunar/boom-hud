using System.Globalization;
using System.Text;
using System.Text.Json;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;

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
        var prepared = GenerationDocumentPreprocessor.Prepare(document, options, "unity");
        document = prepared.Document;
        diagnostics.AddRange(prepared.Diagnostics);

        if (options.EmitCompose)
        {
            diagnostics.Add(Diagnostic.Warning(
                "Unity compose helpers are not implemented yet; skipping compose output.",
                code: "BHU2000"));
        }

        try
        {
            var visualPlan = VisualToUnityToolkitPlan.Build(prepared.VisualDocument);
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

                EmitDocumentArtifacts(componentDocument, options, diagnostics, files, visualPlan);
            }

            var mainPlan = EmitDocumentArtifacts(document, options, diagnostics, files, visualPlan);
            if (options.Motion != null)
            {
                var motionResult = UnityMotionExporter.Generate(document, mainPlan, options.Motion, options);
                files.AddRange(motionResult.Files);
                diagnostics.AddRange(motionResult.Diagnostics);
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

    private static UnityBackendPlan EmitDocumentArtifacts(
        HudDocument document,
        GenerationOptions options,
        List<Diagnostic> diagnostics,
        List<GeneratedFile> files,
        VisualToUnityToolkitPlan? visualPlan)
    {
        var plan = UnityBackendPlanner.CreatePlan(document, options, diagnostics, visualPlan);

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

        return plan;
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
        AppendUssNode(builder, plan.Root, theme, parentLayoutType: null, parentLayout: null, parentGap: null, siblingIndex: 0);
        return builder.ToString();
    }

    private static void AppendUssNode(
        StringBuilder builder,
        UnityPlannedNode node,
        ThemeDocument? theme,
        LayoutType? parentLayoutType,
        LayoutSpec? parentLayout,
        Spacing? parentGap,
        int siblingIndex)
    {
        builder.Append('.');
        builder.Append(node.CssClass);
        builder.AppendLine(" {");

        AppendLayoutStyles(builder, node, parentLayoutType, parentLayout, parentGap, siblingIndex);
        AppendVisualStyles(builder, node, theme);

        builder.AppendLine("}");
        builder.AppendLine();

        for (var index = 0; index < node.Children.Count; index++)
        {
            var child = node.Children[index];
            AppendUssNode(builder, child, theme, node.Source.Layout?.Type, node.Source.Layout, LayoutPolicyService.ResolveGap(node.Source.Layout?.Gap, node.Policy), index);
        }
    }

    private static void AppendLayoutStyles(
        StringBuilder builder,
        UnityPlannedNode node,
        LayoutType? parentLayoutType,
        LayoutSpec? parentLayout,
        Spacing? parentGap,
        int siblingIndex)
    {
        var source = node.Source;
        var layout = source.Layout;
        var style = source.Style;
        AppendAbsolutePlacementStyles(builder, source, parentLayoutType, node.Policy, node.VisualNode?.EdgeContract);

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
                    if (source.Children.Count > 0)
                    {
                        AppendCssDeclaration(builder, "flex-direction", "column");
                    }
                    break;
            }

            AppendDimensionStyles(builder, "width", layout.Width, parentLayoutType, parentLayout, node.Policy);
            AppendDimensionStyles(builder, "height", layout.Height, parentLayoutType, parentLayout, node.Policy);
            AppendDimensionStyles(builder, "min-width", layout.MinWidth, parentLayoutType, parentLayout, node.Policy);
            AppendDimensionStyles(builder, "min-height", layout.MinHeight, parentLayoutType, parentLayout, node.Policy);
            AppendDimensionStyles(builder, "max-width", layout.MaxWidth, parentLayoutType, parentLayout, node.Policy);
            AppendDimensionStyles(builder, "max-height", layout.MaxHeight, parentLayoutType, parentLayout, node.Policy);
            AppendSpacingStyles(builder, "margin", MergeParentGapIntoMargin(layout.Margin, parentLayoutType, parentGap, siblingIndex));
            AppendSpacingStyles(builder, "padding", LayoutPolicyService.ResolvePadding(layout.Padding, node.Policy));
            if (layout.ClipContent)
            {
                AppendCssDeclaration(builder, "overflow", "hidden");
            }

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
                var resolvedJustify = ShouldFallbackToStartJustification(layout, parentLayoutType, parentLayout)
                    ? Justification.Start
                    : justify;
                AppendCssDeclaration(builder, "justify-content", MapJustification(resolvedJustify));
            }
        }

        AppendPolicyLayoutOverrides(builder, node.Policy);

        if (style != null)
        {
            AppendDimensionStyles(builder, "width", style.Width, parentLayoutType, parentLayout, node.Policy);
            AppendDimensionStyles(builder, "height", style.Height, parentLayoutType, parentLayout, node.Policy);
        }
    }

    private static void AppendPolicyLayoutOverrides(StringBuilder builder, ResolvedGeneratorPolicy policy)
    {
        if (LayoutPolicyService.ResolvePositionMode(policy) is { } positionMode)
        {
            var normalizedPosition = positionMode.Trim().ToLowerInvariant();
            if (normalizedPosition is "absolute" or "relative")
            {
                AppendCssDeclaration(builder, "position", normalizedPosition);
            }
        }

        if (LayoutPolicyService.ResolveFlexAlignmentPreset(policy) is not { } alignmentPreset)
        {
            return;
        }

        switch (alignmentPreset.Trim().ToLowerInvariant())
        {
            case "top-left":
            case "start":
                AppendCssDeclaration(builder, "align-items", "flex-start");
                AppendCssDeclaration(builder, "justify-content", "flex-start");
                break;
            case "top-center":
                AppendCssDeclaration(builder, "align-items", "center");
                AppendCssDeclaration(builder, "justify-content", "flex-start");
                break;
            case "top-right":
                AppendCssDeclaration(builder, "align-items", "flex-end");
                AppendCssDeclaration(builder, "justify-content", "flex-start");
                break;
            case "middle-left":
                AppendCssDeclaration(builder, "align-items", "flex-start");
                AppendCssDeclaration(builder, "justify-content", "center");
                break;
            case "center":
            case "middle-center":
                AppendCssDeclaration(builder, "align-items", "center");
                AppendCssDeclaration(builder, "justify-content", "center");
                break;
            case "middle-right":
                AppendCssDeclaration(builder, "align-items", "flex-end");
                AppendCssDeclaration(builder, "justify-content", "center");
                break;
            case "bottom-left":
                AppendCssDeclaration(builder, "align-items", "flex-start");
                AppendCssDeclaration(builder, "justify-content", "flex-end");
                break;
            case "bottom-center":
                AppendCssDeclaration(builder, "align-items", "center");
                AppendCssDeclaration(builder, "justify-content", "flex-end");
                break;
            case "bottom-right":
            case "end":
                AppendCssDeclaration(builder, "align-items", "flex-end");
                AppendCssDeclaration(builder, "justify-content", "flex-end");
                break;
            case "stretch":
                AppendCssDeclaration(builder, "align-items", "stretch");
                break;
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

    private static void AppendAbsolutePlacementStyles(StringBuilder builder, ComponentNode source, LayoutType? parentLayoutType, ResolvedGeneratorPolicy policy, EdgeContract? edgeContract)
    {
        var sourceHasAbsolutePlacement = edgeContract?.Participation == LayoutParticipation.Overlay
                                         || LayoutPolicyService.HasAbsolutePlacement(source, policy);
        if (parentLayoutType != LayoutType.Absolute && !sourceHasAbsolutePlacement)
        {
            return;
        }

        var hasAbsoluteCoordinates = parentLayoutType == LayoutType.Absolute || sourceHasAbsolutePlacement;
        if (hasAbsoluteCoordinates)
        {
            AppendCssDeclaration(builder, "position", "absolute");
        }

        var left = ResolveAbsoluteOffset(source, static layout => layout.Left, BoomHudMetadataKeys.PencilLeft);
        var offsetX = LayoutPolicyService.ResolveOffsetAdjustment("x", policy);
        if (left is { Unit: DimensionUnit.Pixels } leftPixels && !double.IsNaN(offsetX) && Math.Abs(offsetX) > double.Epsilon)
        {
            left = Dimension.Pixels(leftPixels.Value + offsetX);
        }
        else if (left == null && hasAbsoluteCoordinates && Math.Abs(offsetX) > double.Epsilon)
        {
            left = Dimension.Pixels(offsetX);
        }
        if (left != null)
        {
            if (!hasAbsoluteCoordinates)
            {
                AppendCssDeclaration(builder, "position", "absolute");
                hasAbsoluteCoordinates = true;
            }

            AppendOffsetStyle(builder, "left", left.Value);
        }

        var top = ResolveAbsoluteOffset(source, static layout => layout.Top, BoomHudMetadataKeys.PencilTop);
        var offsetY = LayoutPolicyService.ResolveOffsetAdjustment("y", policy);
        if (top is { Unit: DimensionUnit.Pixels } topPixels && !double.IsNaN(offsetY) && Math.Abs(offsetY) > double.Epsilon)
        {
            top = Dimension.Pixels(topPixels.Value + offsetY);
        }
        else if (top == null && hasAbsoluteCoordinates && Math.Abs(offsetY) > double.Epsilon)
        {
            top = Dimension.Pixels(offsetY);
        }
        if (top != null)
        {
            if (!hasAbsoluteCoordinates)
            {
                AppendCssDeclaration(builder, "position", "absolute");
                hasAbsoluteCoordinates = true;
            }

            AppendOffsetStyle(builder, "top", top.Value);
        }

        if (sourceHasAbsolutePlacement && parentLayoutType == null)
        {
            if (left == null)
            {
                AppendCssDeclaration(builder, "left", ToPixels(0));
            }

            if (top == null)
            {
                AppendCssDeclaration(builder, "top", ToPixels(0));
            }
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

    private static Dimension? ResolveAbsoluteOffset(
        ComponentNode source,
        Func<LayoutSpec, Dimension?> selector,
        string metadataKey)
    {
        if (source.Layout != null && selector(source.Layout) is { } dimension)
        {
            return dimension;
        }

        return TryGetNumericMetadata(source, metadataKey, out var value)
            ? Dimension.Pixels(value)
            : null;
    }

    private static void AppendOffsetStyle(StringBuilder builder, string propertyName, Dimension offset)
    {
        switch (offset.Unit)
        {
            case DimensionUnit.Pixels:
                AppendCssDeclaration(builder, propertyName, ToPixels(offset.Value));
                break;
            case DimensionUnit.Percent:
                AppendCssDeclaration(builder, propertyName, offset.Value.ToString(CultureInfo.InvariantCulture) + "%");
                break;
            case DimensionUnit.Cells:
                AppendCssDeclaration(builder, propertyName, ToPixels(offset.Value));
                break;
        }
    }

    private static void AppendVisualStyles(StringBuilder builder, UnityPlannedNode node, ThemeDocument? theme)
    {
        var style = node.Source.Style;
        var policy = node.Policy;
        var widthDimension = node.Source.Layout?.Width ?? style?.Width;
        var heightDimension = node.Source.Layout?.Height ?? style?.Height;
        var textMetric = node.MetricProfile?.Text;
        var iconMetric = node.MetricProfile?.Icon;
        var resolvedFontSize = iconMetric?.ResolvedFontSize
                               ?? textMetric?.ResolvedFontSize
                               ?? TextPolicyService.ResolveFontSize(node.Source, widthDimension, heightDimension, policy);
        var resolvedLetterSpacing = textMetric?.ResolvedLetterSpacing
                                    ?? TextPolicyService.ResolveLetterSpacing(node.Source, policy);
        if (style == null
            && string.IsNullOrWhiteSpace(policy.Text.FontFamily)
            && resolvedFontSize is not > 0d
            && textMetric?.ResolvedLineHeight is not > 0d
            && policy.Text.LineHeight is not > 0d
            && resolvedLetterSpacing is not > 0d)
        {
            return;
        }

        var foreground = style == null ? null : ResolveColor(style.Foreground, style.ForegroundToken, theme);
        if (foreground != null)
        {
            AppendCssDeclaration(builder, "color", foreground);
        }

        var background = style == null ? null : ResolveColor(style.Background, style.BackgroundToken, theme);
        if (background != null)
        {
            AppendCssDeclaration(builder, "background-color", background);
        }

        var fontFamily = iconMetric?.ResolvedFontFamily
                         ?? textMetric?.ResolvedFontFamily
                         ?? policy.Text.FontFamily
                         ?? style?.FontFamily;
        var fontSize = resolvedFontSize;
        if (fontSize != null)
        {
            AppendCssDeclaration(builder, "font-size", ToPixels(fontSize.Value));

            var lineHeight = textMetric?.ResolvedLineHeight
                             ?? TextPolicyService.ResolveLineHeight(style, fontSize.Value, policy);
            if (lineHeight != null)
            {
                AppendCssDeclaration(builder, "line-height", ToPixels(lineHeight.Value));
            }
            else if (string.Equals(fontFamily, "Press Start 2P", StringComparison.Ordinal)
                && fontSize.Value <= 8d)
            {
                AppendCssDeclaration(builder, "line-height", "12px");
            }
        }

        if (resolvedLetterSpacing is { } letterSpacing)
        {
            AppendCssDeclaration(builder, "letter-spacing", ToPixels(letterSpacing));
        }

        if (style != null && TryMapUnityFontStyle(style.FontWeight, style.FontStyle, out var unityFontStyle))
        {
            AppendCssDeclaration(builder, "-unity-font-style", unityFontStyle);
        }

        if (style?.Opacity is { } opacity)
        {
            AppendCssDeclaration(builder, "opacity", opacity.ToString(CultureInfo.InvariantCulture));
        }

        if (style?.BorderRadius is { } borderRadius)
        {
            AppendCssDeclaration(builder, "border-top-left-radius", ToPixels(borderRadius));
            AppendCssDeclaration(builder, "border-top-right-radius", ToPixels(borderRadius));
            AppendCssDeclaration(builder, "border-bottom-left-radius", ToPixels(borderRadius));
            AppendCssDeclaration(builder, "border-bottom-right-radius", ToPixels(borderRadius));
        }

        if (style?.Border is { } border)
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
        var componentNodes = queryNodes.Where(static node => node.ComponentView != null).ToList();

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
        builder.AppendLine("using System.Linq;");
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
        builder.AppendLine("    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? _componentOverrides;");
        builder.AppendLine("    private I" + document.Name + "ViewModel? _viewModel;");
        builder.AppendLine();
        AppendInvariantLine(builder, $"    public {plan.Root.ElementType} Root {{ get; }}");

        foreach (var node in queryNodes)
        {
            AppendInvariantLine(builder, $"    public {node.ElementType} {node.Name} {{ get; }}");
        }

        foreach (var node in componentNodes)
        {
            AppendInvariantLine(builder, $"    private readonly {node.ComponentView}View? _{ToCamelCase(node.Name)}Component;");
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
        builder.AppendLine("            ApplyComponentViewModels();");
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
        AppendInvariantLine(builder, $"    public {document.Name}View(VisualElement root, IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? componentOverrides = null)");
        builder.AppendLine("    {");
        AppendInvariantLine(builder, $"        Root = root as {plan.Root.ElementType} ?? throw new ArgumentException(\"Expected root element type {plan.Root.ElementType}.\", nameof(root));");
        builder.AppendLine("        _componentOverrides = componentOverrides;");

        foreach (var node in queryNodes)
        {
            if (node.ComponentView != null)
            {
                AppendInvariantLine(builder, $"        var {ToCamelCase(node.Name)}Placeholder = Root.Q<VisualElement>(\"{node.Name}\") ?? throw new InvalidOperationException(\"Could not find generated component placeholder '{node.Name}'.\");");
                AppendInvariantLine(builder, $"        _{ToCamelCase(node.Name)}Component = {node.ComponentView}View.Attach({ToCamelCase(node.Name)}Placeholder, {BuildComponentOverrideLiteral(node.Source) ?? "null"});");
                AppendInvariantLine(builder, $"        {node.Name} = _{ToCamelCase(node.Name)}Component.Root;");
            }
            else
            {
                AppendInvariantLine(builder, $"        {node.Name} = Root.Q<{node.ElementType}>(\"{node.Name}\") ?? throw new InvalidOperationException(\"Could not find generated element '{node.Name}'.\");");
            }
        }

        builder.AppendLine();
        builder.AppendLine("        ApplyStaticValues();");
        builder.AppendLine("        ApplyInstanceOverrides();");
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
        builder.AppendLine("    private void ApplyInstanceOverrides()");
        builder.AppendLine("    {");
        builder.AppendLine("        if (_componentOverrides == null)");
        builder.AppendLine("        {");
        builder.AppendLine("            return;");
        builder.AppendLine("        }");
        builder.AppendLine();
        var componentOverrideIndex = 0;
        AppendComponentOverrideAssignmentsRecursive(builder, plan.Root, plan.Root, ref componentOverrideIndex);
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private void ApplyStaticValues()");
        builder.AppendLine("    {");

        AppendStaticAssignmentsRecursive(builder, plan.Root, plan.Root, options.Theme, parentLayoutType: null, parentLayout: null, parentGap: null, siblingIndex: 0);

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private void ApplyComponentViewModels()");
        builder.AppendLine("    {");
        foreach (var node in componentNodes)
        {
            AppendInvariantLine(builder, $"        if (_{ToCamelCase(node.Name)}Component != null)");
            builder.AppendLine("        {");
            AppendInvariantLine(builder, $"            _{ToCamelCase(node.Name)}Component.ViewModel = _viewModel is I{node.ComponentView}ViewModel componentViewModel ? componentViewModel : null;");
            builder.AppendLine("        }");
        }
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)");
        builder.AppendLine("    {");
        builder.AppendLine("        Refresh();");
        builder.AppendLine("    }");
        builder.AppendLine();
        AppendInvariantLine(builder, $"    private const string VisualTreeResourcePath = \"BoomHudGenerated/{document.Name}View\";");
        AppendInvariantLine(builder, $"    private const string StyleSheetResourcePath = \"BoomHudGenerated/{document.Name}View\";");
        AppendInvariantLine(builder, $"    private const string GeneratedRootName = \"{plan.Root.Name}\";");
        builder.AppendLine("    private static VisualTreeAsset? s_visualTreeAsset;");
        builder.AppendLine("    private static StyleSheet? s_generatedStyleSheet;");
        builder.AppendLine();
        AppendInvariantLine(builder, $"    public static {document.Name}View Create(VisualElement parent, string? instanceName = null, IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? componentOverrides = null)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (parent == null)");
        builder.AppendLine("        {");
        builder.AppendLine("            throw new ArgumentNullException(nameof(parent));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var root = CreateGeneratedRoot(instanceName);");
        builder.AppendLine("        parent.Add(root);");
        AppendInvariantLine(builder, $"        return new {document.Name}View(root, componentOverrides);");
        builder.AppendLine("    }");
        builder.AppendLine();
        AppendInvariantLine(builder, $"    public static {document.Name}View Attach(VisualElement placeholder, IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? componentOverrides = null)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (placeholder == null)");
        builder.AppendLine("        {");
        builder.AppendLine("            throw new ArgumentNullException(nameof(placeholder));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var parent = placeholder.parent ?? throw new InvalidOperationException(\"Cannot attach a generated component without a parent element.\");");
        builder.AppendLine("        var placeholderIndex = parent.IndexOf(placeholder);");
        builder.AppendLine("        var root = CreateGeneratedRoot(string.IsNullOrWhiteSpace(placeholder.name) ? null : placeholder.name);");
        builder.AppendLine("        parent.Insert(placeholderIndex, root);");
        builder.AppendLine("        placeholder.RemoveFromHierarchy();");
        AppendInvariantLine(builder, $"        return new {document.Name}View(root, componentOverrides);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static VisualElement CreateGeneratedRoot(string? instanceName)");
        builder.AppendLine("    {");
        builder.AppendLine("        s_visualTreeAsset ??= Resources.Load<VisualTreeAsset>(VisualTreeResourcePath)");
        builder.AppendLine("            ?? throw new InvalidOperationException($\"Could not load VisualTreeAsset from Resources/{VisualTreeResourcePath}.uxml\");");
        builder.AppendLine("        s_generatedStyleSheet ??= Resources.Load<StyleSheet>(StyleSheetResourcePath);");
        builder.AppendLine();
        builder.AppendLine("        var staging = new VisualElement();");
        builder.AppendLine("        s_visualTreeAsset.CloneTree(staging);");
        AppendInvariantLine(builder, $"        var root = staging.Q<{plan.Root.ElementType}>(GeneratedRootName)");
        builder.AppendLine("            ?? throw new InvalidOperationException($\"Could not find generated root element '{GeneratedRootName}' after cloning the tree.\");");
        builder.AppendLine("        root.RemoveFromHierarchy();");
        builder.AppendLine("        root.name = string.IsNullOrWhiteSpace(instanceName) ? GeneratedRootName : instanceName!;");
        builder.AppendLine();
        builder.AppendLine("        if (s_generatedStyleSheet != null && !root.styleSheets.Contains(s_generatedStyleSheet))");
        builder.AppendLine("        {");
        builder.AppendLine("            root.styleSheets.Add(s_generatedStyleSheet);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return root;");
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
        builder.AppendLine("    private bool TryGetComponentOverrideValue(string nodePath, string propertyName, out object? value)");
        builder.AppendLine("    {");
        builder.AppendLine("        value = null;");
        builder.AppendLine("        if (_componentOverrides == null || !_componentOverrides.TryGetValue(nodePath, out var propertyOverrides))");
        builder.AppendLine("        {");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (propertyOverrides.TryGetValue(propertyName, out value))");
        builder.AppendLine("        {");
        builder.AppendLine("            return true;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        foreach (var candidate in propertyOverrides)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (string.Equals(candidate.Key, propertyName, StringComparison.OrdinalIgnoreCase))");
        builder.AppendLine("            {");
        builder.AppendLine("                value = candidate.Value;");
        builder.AppendLine("                return true;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return false;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static StyleLength ParseStyleLength(string value)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (string.IsNullOrWhiteSpace(value))");
        builder.AppendLine("        {");
        builder.AppendLine("            return StyleKeyword.Null;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (string.Equals(value, \"auto\", StringComparison.OrdinalIgnoreCase))");
        builder.AppendLine("        {");
        builder.AppendLine("            return StyleKeyword.Auto;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (value.EndsWith(\"%\", StringComparison.Ordinal))");
        builder.AppendLine("        {");
        builder.AppendLine("            var number = value[..^1];");
        builder.AppendLine("            return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)");
        builder.AppendLine("                ? new StyleLength(new Length(percent, LengthUnit.Percent))");
        builder.AppendLine("                : StyleKeyword.Null;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (value.EndsWith(\"px\", StringComparison.OrdinalIgnoreCase))");
        builder.AppendLine("        {");
        builder.AppendLine("            var number = value[..^2];");
        builder.AppendLine("            return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels)");
        builder.AppendLine("                ? new StyleLength(pixels)");
        builder.AppendLine("                : StyleKeyword.Null;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw)");
        builder.AppendLine("            ? new StyleLength(raw)");
        builder.AppendLine("            : StyleKeyword.Null;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static float ParseStyleFloat(string value)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (string.IsNullOrWhiteSpace(value))");
        builder.AppendLine("        {");
        builder.AppendLine("            return 0f;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var normalized = value.EndsWith(\"px\", StringComparison.OrdinalIgnoreCase)");
        builder.AppendLine("            ? value[..^2]");
        builder.AppendLine("            : value;");
        builder.AppendLine();
        builder.AppendLine("        return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)");
        builder.AppendLine("            ? parsed");
        builder.AppendLine("            : 0f;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static Position ParsePosition(string value)");
        builder.AppendLine("        => string.Equals(value, \"absolute\", StringComparison.OrdinalIgnoreCase)");
        builder.AppendLine("            ? Position.Absolute");
        builder.AppendLine("            : Position.Relative;");
        builder.AppendLine();
        builder.AppendLine("    private static FlexDirection ParseFlexDirection(string value)");
        builder.AppendLine("        => string.Equals(value, \"row\", StringComparison.OrdinalIgnoreCase)");
        builder.AppendLine("            ? FlexDirection.Row");
        builder.AppendLine("            : FlexDirection.Column;");
        builder.AppendLine();
        builder.AppendLine("    private static Overflow ParseOverflow(string value)");
        builder.AppendLine("        => string.Equals(value, \"hidden\", StringComparison.OrdinalIgnoreCase)");
        builder.AppendLine("            ? Overflow.Hidden");
        builder.AppendLine("            : Overflow.Visible;");
        builder.AppendLine();
        builder.AppendLine("    private static Align ParseAlign(string value)");
        builder.AppendLine("        => value.ToLowerInvariant() switch");
        builder.AppendLine("        {");
        builder.AppendLine("            \"flex-start\" => Align.FlexStart,");
        builder.AppendLine("            \"center\" => Align.Center,");
        builder.AppendLine("            \"flex-end\" => Align.FlexEnd,");
        builder.AppendLine("            \"stretch\" => Align.Stretch,");
        builder.AppendLine("            \"auto\" => Align.Auto,");
        builder.AppendLine("            _ => Align.Stretch");
        builder.AppendLine("        };");
        builder.AppendLine();
        builder.AppendLine("    private static Justify ParseJustify(string value)");
        builder.AppendLine("        => value.ToLowerInvariant() switch");
        builder.AppendLine("        {");
        builder.AppendLine("            \"flex-start\" => Justify.FlexStart,");
        builder.AppendLine("            \"center\" => Justify.Center,");
        builder.AppendLine("            \"flex-end\" => Justify.FlexEnd,");
        builder.AppendLine("            \"space-between\" => Justify.SpaceBetween,");
        builder.AppendLine("            \"space-around\" => Justify.SpaceAround,");
        builder.AppendLine("            \"space-evenly\" => Justify.SpaceEvenly,");
        builder.AppendLine("            _ => Justify.FlexStart");
        builder.AppendLine("        };");
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
        builder.AppendLine("    private static void SetImageSource(Image image, string resourcePath)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (string.IsNullOrWhiteSpace(resourcePath))");
        builder.AppendLine("        {");
        builder.AppendLine("            image.image = null;");
        builder.AppendLine("            return;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var normalized = resourcePath.Replace(\"\\\\\", \"/\", StringComparison.Ordinal).TrimStart('/');");
        builder.AppendLine("        var extensionIndex = normalized.LastIndexOf('.');");
        builder.AppendLine("        if (extensionIndex > 0)");
        builder.AppendLine("        {");
        builder.AppendLine("            normalized = normalized[..extensionIndex];");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        image.image = Resources.Load<Texture2D>(normalized);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static void ApplyTextLabelStyle(Label label, bool wrapText)");
        builder.AppendLine("    {");
        builder.AppendLine("        label.style.whiteSpace = wrapText ? WhiteSpace.Normal : WhiteSpace.NoWrap;");
        builder.AppendLine("        label.style.flexShrink = wrapText ? 1 : 0;");
        builder.AppendLine("        label.style.overflow = wrapText ? Overflow.Hidden : Overflow.Visible;");
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
        builder.AppendLine("        var fontCandidates = ExpandFontFamilyCandidates(familyName);");
        builder.AppendLine("        foreach (var candidate in fontCandidates)");
        builder.AppendLine("        {");
        builder.AppendLine("            resourceFont = LoadBundledFont(candidate);");
        builder.AppendLine("            if (resourceFont != null)");
        builder.AppendLine("            {");
        builder.AppendLine("                font = resourceFont;");
        builder.AppendLine("                return true;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        try");
        builder.AppendLine("        {");
        builder.AppendLine("            var osFont = Font.CreateDynamicFontFromOSFont(fontCandidates, pointSize);");
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
        builder.AppendLine("    private static string[] ExpandFontFamilyCandidates(string familyName)");
        builder.AppendLine("    {");
        builder.AppendLine("        var rawCandidates = familyName.Split(',');");
        builder.AppendLine("        var expanded = new List<string>();");
        builder.AppendLine();
        builder.AppendLine("        foreach (var rawCandidate in rawCandidates)");
        builder.AppendLine("        {");
        builder.AppendLine("            var candidate = rawCandidate.Trim().Trim('\\'', '\"');");
        builder.AppendLine("            if (string.IsNullOrWhiteSpace(candidate))");
        builder.AppendLine("            {");
        builder.AppendLine("                continue;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            switch (candidate.ToLowerInvariant())");
        builder.AppendLine("            {");
        builder.AppendLine("                case \"monospace\":");
        builder.AppendLine("                    expanded.Add(\"Consolas\");");
        builder.AppendLine("                    expanded.Add(\"Cascadia Mono\");");
        builder.AppendLine("                    expanded.Add(\"Courier New\");");
        builder.AppendLine("                    expanded.Add(\"Lucida Console\");");
        builder.AppendLine("                    break;");
        builder.AppendLine("                case \"sans-serif\":");
        builder.AppendLine("                case \"sans serif\":");
        builder.AppendLine("                    expanded.Add(\"Segoe UI\");");
        builder.AppendLine("                    expanded.Add(\"Arial\");");
        builder.AppendLine("                    break;");
        builder.AppendLine("                case \"serif\":");
        builder.AppendLine("                    expanded.Add(\"Georgia\");");
        builder.AppendLine("                    expanded.Add(\"Times New Roman\");");
        builder.AppendLine("                    break;");
        builder.AppendLine("                default:");
        builder.AppendLine("                    expanded.Add(candidate);");
        builder.AppendLine("                    break;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (expanded.Count == 0)");
        builder.AppendLine("        {");
        builder.AppendLine("            expanded.Add(familyName.Trim());");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return expanded.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();");
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
        builder.AppendLine("    private static void ApplyIconLabelStyle(Label label, float boxWidth, float boxHeight, float baselineOffset, bool opticalCentering, string sizeMode, float explicitFontSize)");
        builder.AppendLine("    {");
        builder.AppendLine("        var iconSize = explicitFontSize > 0f ? explicitFontSize : string.Equals(sizeMode, \"match-height\", StringComparison.OrdinalIgnoreCase) ? Mathf.Max(1f, boxHeight) : Mathf.Max(1f, Mathf.Min(boxWidth, boxHeight));");
        builder.AppendLine("        label.style.unityTextAlign = TextAnchor.MiddleCenter;");
        builder.AppendLine("        label.style.unityFontStyleAndWeight = FontStyle.Normal;");
        builder.AppendLine("        label.style.whiteSpace = WhiteSpace.NoWrap;");
        builder.AppendLine("        label.style.flexShrink = 0;");
        builder.AppendLine("        label.style.alignItems = opticalCentering ? Align.Center : Align.FlexStart;");
        builder.AppendLine("        label.style.justifyContent = opticalCentering ? Justify.Center : Justify.FlexStart;");
        builder.AppendLine("        label.style.overflow = Overflow.Visible;");
        builder.AppendLine("        label.style.paddingLeft = 0f;");
        builder.AppendLine("        label.style.paddingTop = 0f;");
        builder.AppendLine("        label.style.paddingRight = 0f;");
        builder.AppendLine("        label.style.paddingBottom = 0f;");
        builder.AppendLine("        label.style.width = boxWidth;");
        builder.AppendLine("        label.style.height = boxHeight;");
        builder.AppendLine("        label.style.minWidth = boxWidth;");
        builder.AppendLine("        label.style.minHeight = boxHeight;");
        builder.AppendLine("        if (opticalCentering && boxWidth >= 32f && boxHeight >= 32f && Mathf.Approximately(baselineOffset, 0f))");
        builder.AppendLine("        {");
        builder.AppendLine("            label.style.marginTop = -1f;");
        builder.AppendLine("        }");
        builder.AppendLine("        else if (!Mathf.Approximately(baselineOffset, 0f))");
        builder.AppendLine("        {");
        builder.AppendLine("            label.style.marginTop = baselineOffset;");
        builder.AppendLine("        }");
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

    private static void AppendStaticAssignmentsRecursive(
        StringBuilder builder,
        UnityPlannedNode node,
        UnityPlannedNode root,
        ThemeDocument? theme,
        LayoutType? parentLayoutType,
        LayoutSpec? parentLayout,
        Spacing? parentGap,
        int siblingIndex)
    {
        AppendStaticAssignments(builder, node, root, theme, parentLayoutType, parentLayout, parentGap, siblingIndex);

        for (var index = 0; index < node.Children.Count; index++)
        {
            AppendStaticAssignmentsRecursive(
                builder,
                node.Children[index],
                root,
                theme,
                node.Source.Layout?.Type,
                node.Source.Layout,
                LayoutPolicyService.ResolveGap(node.Source.Layout?.Gap, node.Policy),
                index);
        }
    }

    private static void AppendComponentOverrideAssignmentsRecursive(
        StringBuilder builder,
        UnityPlannedNode node,
        UnityPlannedNode root,
        ref int overrideIndex)
    {
        foreach (var property in node.Source.Properties.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!ComponentInstanceOverrideSupport.IsSupportedProperty(node.Source, property.Key))
            {
                continue;
            }

            var accessor = GetNodeAccessor(node, root);
            var overrideVariableName = FormattableString.Invariant($"componentOverrideValue{overrideIndex++}");
            if (!TryBuildInstanceOverrideAssignment(accessor, node, property.Key, overrideVariableName, out var assignment))
            {
                continue;
            }

            AppendInvariantLine(builder, $"        if (TryGetComponentOverrideValue({ToStringLiteral(node.RelativePath)}, {ToStringLiteral(property.Key)}, out var {overrideVariableName}))");
            builder.AppendLine("        {");
            foreach (var line in assignment.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
            {
                builder.AppendLine("            " + line);
            }
            builder.AppendLine("        }");
        }

        foreach (var child in node.Children)
        {
            AppendComponentOverrideAssignmentsRecursive(builder, child, root, ref overrideIndex);
        }
    }

    private static void AppendStaticAssignments(
        StringBuilder builder,
        UnityPlannedNode node,
        UnityPlannedNode root,
        ThemeDocument? theme,
        LayoutType? parentLayoutType,
        LayoutSpec? parentLayout,
        Spacing? parentGap,
        int siblingIndex)
    {
        var accessor = GetNodeAccessor(node, root);

        if (node.Source.Type == ComponentType.TextArea && node.ElementType == "TextField" && accessor != "Root")
        {
            AppendInvariantLine(builder, $"        {accessor}.multiline = true;");
        }

        AppendStaticLayoutAssignments(builder, accessor, node, parentLayoutType, parentLayout, parentGap, siblingIndex);

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

        AppendStaticStyleAssignments(builder, accessor, node, theme);
    }

    private static void AppendStaticLayoutAssignments(
        StringBuilder builder,
        string accessor,
        UnityPlannedNode node,
        LayoutType? parentLayoutType,
        LayoutSpec? parentLayout,
        Spacing? parentGap,
        int siblingIndex)
    {
        var declarations = new StringBuilder();
        AppendLayoutStyles(declarations, node, parentLayoutType, parentLayout, parentGap, siblingIndex);

        var lines = declarations.ToString()
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var propertyName = line[..separatorIndex].Trim();
            var propertyValue = line[(separatorIndex + 1)..].Trim().TrimEnd(';').Trim();
            if (TryBuildStaticLayoutAssignment(accessor, propertyName, propertyValue, out var assignment))
            {
                AppendInvariantLine(builder, $"        {assignment}");
            }
        }
    }

    private static bool TryBuildStaticLayoutAssignment(string accessor, string propertyName, string propertyValue, out string assignment)
    {
        assignment = string.Empty;

        switch (propertyName)
        {
            case "flex-direction":
                assignment = $"{accessor}.style.flexDirection = ParseFlexDirection({ToStringLiteral(propertyValue)});";
                return true;
            case "position":
                assignment = $"{accessor}.style.position = ParsePosition({ToStringLiteral(propertyValue)});";
                return true;
            case "left":
                assignment = $"{accessor}.style.left = ParseStyleLength({ToStringLiteral(propertyValue)});";
                return true;
            case "top":
                assignment = $"{accessor}.style.top = ParseStyleLength({ToStringLiteral(propertyValue)});";
                return true;
            case "width":
                assignment = $"{accessor}.style.width = ParseStyleLength({ToStringLiteral(propertyValue)});";
                return true;
            case "height":
                assignment = $"{accessor}.style.height = ParseStyleLength({ToStringLiteral(propertyValue)});";
                return true;
            case "min-width":
                assignment = $"{accessor}.style.minWidth = ParseStyleLength({ToStringLiteral(propertyValue)});";
                return true;
            case "min-height":
                assignment = $"{accessor}.style.minHeight = ParseStyleLength({ToStringLiteral(propertyValue)});";
                return true;
            case "max-width":
                assignment = $"{accessor}.style.maxWidth = ParseStyleLength({ToStringLiteral(propertyValue)});";
                return true;
            case "max-height":
                assignment = $"{accessor}.style.maxHeight = ParseStyleLength({ToStringLiteral(propertyValue)});";
                return true;
            case "margin-top":
                assignment = $"{accessor}.style.marginTop = ParseStyleFloat({ToStringLiteral(propertyValue)});";
                return true;
            case "margin-right":
                assignment = $"{accessor}.style.marginRight = ParseStyleFloat({ToStringLiteral(propertyValue)});";
                return true;
            case "margin-bottom":
                assignment = $"{accessor}.style.marginBottom = ParseStyleFloat({ToStringLiteral(propertyValue)});";
                return true;
            case "margin-left":
                assignment = $"{accessor}.style.marginLeft = ParseStyleFloat({ToStringLiteral(propertyValue)});";
                return true;
            case "padding-top":
                assignment = $"{accessor}.style.paddingTop = ParseStyleFloat({ToStringLiteral(propertyValue)});";
                return true;
            case "padding-right":
                assignment = $"{accessor}.style.paddingRight = ParseStyleFloat({ToStringLiteral(propertyValue)});";
                return true;
            case "padding-bottom":
                assignment = $"{accessor}.style.paddingBottom = ParseStyleFloat({ToStringLiteral(propertyValue)});";
                return true;
            case "padding-left":
                assignment = $"{accessor}.style.paddingLeft = ParseStyleFloat({ToStringLiteral(propertyValue)});";
                return true;
            case "overflow":
                assignment = $"{accessor}.style.overflow = ParseOverflow({ToStringLiteral(propertyValue)});";
                return true;
            case "flex-grow":
                assignment = $"{accessor}.style.flexGrow = ParseStyleFloat({ToStringLiteral(propertyValue)});";
                return true;
            case "align-items":
                assignment = $"{accessor}.style.alignItems = ParseAlign({ToStringLiteral(propertyValue)});";
                return true;
            case "align-self":
                assignment = $"{accessor}.style.alignSelf = ParseAlign({ToStringLiteral(propertyValue)});";
                return true;
            case "justify-content":
                assignment = $"{accessor}.style.justifyContent = ParseJustify({ToStringLiteral(propertyValue)});";
                return true;
            default:
                return false;
        }
    }

    private static bool TryBuildInstanceOverrideAssignment(string accessor, UnityPlannedNode node, string property, string valueExpression, out string assignment)
    {
        assignment = string.Empty;
        var normalizedProperty = property.ToLowerInvariant();

        if (IsIconLabelNode(node) && normalizedProperty is "text" or "value")
        {
            var iconFontFamily = ToNullableStringLiteral(TextPolicyService.ResolveFontFamily(node.Source, node.Policy));
            var iconPointSize = ToFloatLiteral(TextPolicyService.ResolveFontSize(node.Source, node.Source.Layout?.Width ?? node.Source.Style?.Width, node.Source.Layout?.Height ?? node.Source.Style?.Height, node.Policy) ?? 16d);
            var textExpression = $"ResolveIconText(AsString({valueExpression}), {iconFontFamily}, {iconPointSize})";
            var iconWidth = ToFloatLiteral(GetNodePixelDimension(node.Source.Layout?.Width) ?? GetNodePixelDimension(node.Source.Style?.Width) ?? 16d);
            var iconHeight = ToFloatLiteral(GetNodePixelDimension(node.Source.Layout?.Height) ?? GetNodePixelDimension(node.Source.Style?.Height) ?? 16d);
            var baselineOffset = ToFloatLiteral(IconPolicyService.ResolveBaselineOffset(node.Policy));
            var opticalCentering = IconPolicyService.UseOpticalCentering(node.Policy) ? "true" : "false";
            var sizeMode = ToStringLiteral(IconPolicyService.ResolveSizeMode(node.Policy));
            var explicitIconFontSize = ToFloatLiteral(IconPolicyService.ResolveFontSize(
                node.Source,
                node.Source.Layout?.Width ?? node.Source.Style?.Width,
                node.Source.Layout?.Height ?? node.Source.Style?.Height,
                node.Policy) ?? 0d);
            assignment = string.Join(Environment.NewLine,
                $"{accessor}.text = {textExpression};",
                $"ApplyIconLabelStyle({accessor}, {iconWidth}, {iconHeight}, {baselineOffset}, {opticalCentering}, {sizeMode}, {explicitIconFontSize});");
            return true;
        }

        switch (node.ElementType)
        {
            case "Label":
                if (normalizedProperty is "text" or "value")
                {
                    var wrapTextLiteral = TextPolicyService.ShouldWrapText(node.Source, node.Policy) ? "true" : "false";
                    assignment = string.Join(Environment.NewLine,
                        $"{accessor}.text = AsString({valueExpression});",
                        $"ApplyTextLabelStyle({accessor}, {wrapTextLiteral});");
                    return true;
                }
                break;
            case "Button":
                if (normalizedProperty is "text" or "value")
                {
                    assignment = $"{accessor}.text = AsString({valueExpression});";
                    return true;
                }
                break;
            case "TextField":
                if (normalizedProperty is "text" or "value")
                {
                    assignment = $"{accessor}.value = AsString({valueExpression});";
                    return true;
                }
                break;
            case "Toggle":
                if (normalizedProperty == "value")
                {
                    assignment = $"{accessor}.value = AsBool({valueExpression});";
                    return true;
                }

                if (normalizedProperty is "text" or "content")
                {
                    assignment = $"{accessor}.text = AsString({valueExpression});";
                    return true;
                }
                break;
            case "ProgressBar":
                if (normalizedProperty == "value")
                {
                    assignment = $"{accessor}.value = AsFloat({valueExpression});";
                    return true;
                }

                if (normalizedProperty is "text" or "content")
                {
                    assignment = $"{accessor}.title = AsString({valueExpression});";
                    return true;
                }
                break;
            case "Slider":
                if (normalizedProperty == "value")
                {
                    assignment = $"{accessor}.value = AsFloat({valueExpression});";
                    return true;
                }
                break;
            case "Image":
                if (normalizedProperty is "source" or "src" or "value")
                {
                    assignment = $"SetImageSource({accessor}, AsString({valueExpression}));";
                    return true;
                }
                break;
        }

        return false;
    }

    private static void AppendStaticStyleAssignments(
        StringBuilder builder,
        string accessor,
        UnityPlannedNode node,
        ThemeDocument? theme)
    {
        var style = node.Source.Style;
        if (style == null)
        {
            return;
        }

        var foreground = ResolveColor(style.Foreground, style.ForegroundToken, theme);
        if (!string.IsNullOrWhiteSpace(foreground))
        {
            AppendInvariantLine(builder, $"        {accessor}.style.color = ParseStyleColor({ToStringLiteral(foreground)}, null);");
        }

        var background = ResolveColor(style.Background, style.BackgroundToken, theme);
        if (!string.IsNullOrWhiteSpace(background))
        {
            AppendInvariantLine(builder, $"        {accessor}.style.backgroundColor = ParseStyleColor({ToStringLiteral(background)}, null);");
        }

        var fontSize = TextPolicyService.ResolveFontSize(
            node.Source,
            node.Source.Layout?.Width ?? style.Width,
            node.Source.Layout?.Height ?? style.Height,
            node.Policy);
        if (fontSize is { } resolvedFontSize)
        {
            AppendInvariantLine(builder, $"        {accessor}.style.fontSize = {ToFloatLiteral(resolvedFontSize)};");
        }

        if (TextPolicyService.ResolveLetterSpacing(node.Source, node.Policy) is { } letterSpacing)
        {
            AppendInvariantLine(builder, $"        {accessor}.style.letterSpacing = {ToFloatLiteral(letterSpacing)};");
        }

        if (TryMapUnityFontStyle(style.FontWeight, style.FontStyle, out var unityFontStyle)
            && TryGetUnityFontStyleLiteral(unityFontStyle, out var unityFontStyleLiteral))
        {
            AppendInvariantLine(builder, $"        {accessor}.style.unityFontStyleAndWeight = {unityFontStyleLiteral};");
        }

        if (style.BorderRadius is { } borderRadius)
        {
            var radius = ToFloatLiteral(borderRadius);
            AppendInvariantLine(builder, $"        {accessor}.style.borderTopLeftRadius = {radius};");
            AppendInvariantLine(builder, $"        {accessor}.style.borderTopRightRadius = {radius};");
            AppendInvariantLine(builder, $"        {accessor}.style.borderBottomLeftRadius = {radius};");
            AppendInvariantLine(builder, $"        {accessor}.style.borderBottomRightRadius = {radius};");
        }

        if (style.Border is { } border)
        {
            var width = ToFloatLiteral(border.Width);
            AppendInvariantLine(builder, $"        {accessor}.style.borderLeftWidth = {width};");
            AppendInvariantLine(builder, $"        {accessor}.style.borderRightWidth = {width};");
            AppendInvariantLine(builder, $"        {accessor}.style.borderTopWidth = {width};");
            AppendInvariantLine(builder, $"        {accessor}.style.borderBottomWidth = {width};");

            var borderColor = ResolveColor(border.Color, style.BorderColorToken, theme);
            if (!string.IsNullOrWhiteSpace(borderColor))
            {
                AppendInvariantLine(builder, $"        {accessor}.style.borderLeftColor = ParseStyleColor({ToStringLiteral(borderColor)}, null);");
                AppendInvariantLine(builder, $"        {accessor}.style.borderRightColor = ParseStyleColor({ToStringLiteral(borderColor)}, null);");
                AppendInvariantLine(builder, $"        {accessor}.style.borderTopColor = ParseStyleColor({ToStringLiteral(borderColor)}, null);");
                AppendInvariantLine(builder, $"        {accessor}.style.borderBottomColor = ParseStyleColor({ToStringLiteral(borderColor)}, null);");
            }
        }

        if (style.Opacity is { } opacity)
        {
            AppendInvariantLine(builder, $"        {accessor}.style.opacity = {ToFloatLiteral(opacity)};");
        }
    }

    private static bool TryGetStaticFontFamilyAssignment(string accessor, UnityPlannedNode node, out string assignment)
    {
        assignment = string.Empty;
        if (!SupportsFontFamily(node))
        {
            return false;
        }

        var fontFamily = TextPolicyService.ResolveFontFamily(node.Source, node.Policy);
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return false;
        }

        var fontSizeValue = TextPolicyService.ResolveFontSize(
            node.Source,
            node.Source.Layout?.Width ?? node.Source.Style?.Width,
            node.Source.Layout?.Height ?? node.Source.Style?.Height,
            node.Policy);

        var fontSize = ToFloatLiteral(fontSizeValue ?? 16d);
        assignment = $"ApplyFontFamily({accessor}, {ToStringLiteral(fontFamily)}, {fontSize});";
        return true;
    }

    private static bool TryGetUnityFontStyleLiteral(string unityFontStyle, out string literal)
    {
        literal = unityFontStyle switch
        {
            "normal" => "UnityEngine.FontStyle.Normal",
            "bold" => "UnityEngine.FontStyle.Bold",
            "italic" => "UnityEngine.FontStyle.Italic",
            "bold-and-italic" => "UnityEngine.FontStyle.BoldAndItalic",
            _ => string.Empty
        };

        return literal.Length > 0;
    }

    private static bool TryBuildAssignment(string accessor, UnityPlannedNode node, string property, string valueExpression, bool isStatic, out string assignment)
    {
        assignment = string.Empty;
        var normalizedProperty = property.ToLowerInvariant();

        if (IsIconLabelNode(node) && normalizedProperty is "text" or "value")
        {
            var rawTextExpression = isStatic ? valueExpression : $"AsString(_viewModel.{valueExpression})";
            var iconFontFamily = ToNullableStringLiteral(TextPolicyService.ResolveFontFamily(node.Source, node.Policy));
            var iconPointSize = ToFloatLiteral(TextPolicyService.ResolveFontSize(node.Source, node.Source.Layout?.Width ?? node.Source.Style?.Width, node.Source.Layout?.Height ?? node.Source.Style?.Height, node.Policy) ?? 16d);
            var textExpression = $"ResolveIconText({rawTextExpression}, {iconFontFamily}, {iconPointSize})";
            var iconWidth = ToFloatLiteral(GetNodePixelDimension(node.Source.Layout?.Width) ?? GetNodePixelDimension(node.Source.Style?.Width) ?? 16d);
            var iconHeight = ToFloatLiteral(GetNodePixelDimension(node.Source.Layout?.Height) ?? GetNodePixelDimension(node.Source.Style?.Height) ?? 16d);
            var baselineOffset = ToFloatLiteral(IconPolicyService.ResolveBaselineOffset(node.Policy));
            var opticalCentering = IconPolicyService.UseOpticalCentering(node.Policy) ? "true" : "false";
            var sizeMode = ToStringLiteral(IconPolicyService.ResolveSizeMode(node.Policy));
            var explicitIconFontSize = ToFloatLiteral(IconPolicyService.ResolveFontSize(
                node.Source,
                node.Source.Layout?.Width ?? node.Source.Style?.Width,
                node.Source.Layout?.Height ?? node.Source.Style?.Height,
                node.Policy) ?? 0d);
            assignment = string.Join(Environment.NewLine,
                $"{accessor}.text = {textExpression};",
                $"        ApplyIconLabelStyle({accessor}, {iconWidth}, {iconHeight}, {baselineOffset}, {opticalCentering}, {sizeMode}, {explicitIconFontSize});");
            return true;
        }

        switch (node.ElementType)
        {
            case "Label":
                if (normalizedProperty is "text" or "value")
                {
                    var wrapTextLiteral = TextPolicyService.ShouldWrapText(node.Source, node.Policy) ? "true" : "false";
                    assignment = string.Join(Environment.NewLine,
                        $"{accessor}.text = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};",
                        $"        ApplyTextLabelStyle({accessor}, {wrapTextLiteral});");
                    return true;
                }
                break;
            case "Button":
                if (normalizedProperty is "text" or "value")
                {
                    assignment = $"{accessor}.text = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
            case "TextField":
                if (normalizedProperty is "text" or "value")
                {
                    assignment = $"{accessor}.value = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
            case "Toggle":
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
            case "ProgressBar":
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
            case "Slider":
                if (normalizedProperty == "value")
                {
                    assignment = $"{accessor}.value = {(isStatic ? valueExpression : "AsFloat(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
            case "Image":
                if (normalizedProperty == "tooltip")
                {
                    assignment = $"{accessor}.tooltip = {(isStatic ? valueExpression : "AsString(_viewModel." + valueExpression + ")")};";
                    return true;
                }
                break;
        }

        return false;
    }

    private static bool SupportsFontFamily(UnityPlannedNode node)
        => node.ElementType is "Label" or "Button" or "TextField" or "Toggle";

    private static bool IsIconLabelNode(UnityPlannedNode node)
        => node.Source.Type == ComponentType.Icon && node.ElementType == "Label";

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

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length == 1
            ? value.ToLowerInvariant()
            : char.ToLowerInvariant(value[0]) + value[1..];
    }

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

    private static void AppendDimensionStyles(
        StringBuilder builder,
        string propertyName,
        Dimension? dimension,
        LayoutType? parentLayoutType,
        LayoutSpec? parentLayout,
        ResolvedGeneratorPolicy policy)
    {
        if (dimension == null)
        {
            return;
        }

        switch (dimension.Value.Unit)
        {
            case DimensionUnit.Pixels:
                AppendCssDeclaration(builder, propertyName, ToPixels(ResolveDimensionValue(propertyName, dimension.Value.Value, policy)));
                break;
            case DimensionUnit.Percent:
                AppendCssDeclaration(builder, propertyName, dimension.Value.Value.ToString(CultureInfo.InvariantCulture) + "%");
                break;
            case DimensionUnit.Cells:
                AppendCssDeclaration(builder, propertyName, ToPixels(ResolveDimensionValue(propertyName, dimension.Value.Value, policy)));
                break;
            case DimensionUnit.Auto:
                AppendCssDeclaration(builder, propertyName, "auto");
                break;
            case DimensionUnit.Fill:
            case DimensionUnit.Star:
                AppendFillDimensionStyles(
                    builder,
                    propertyName,
                    dimension.Value.Value == 0 ? 1 : dimension.Value.Value,
                    parentLayoutType,
                    parentLayout);
                break;
        }
    }

    private static double ResolveDimensionValue(string propertyName, double value, ResolvedGeneratorPolicy policy)
    {
        var delta = propertyName == "width"
            ? policy.Layout.PreferredWidthDelta
            : propertyName == "height"
                ? policy.Layout.PreferredHeightDelta
                : null;

        var adjusted = value + (delta ?? 0d);
        return adjusted > 0d ? adjusted : value;
    }

    private static void AppendFillDimensionStyles(
        StringBuilder builder,
        string propertyName,
        double value,
        LayoutType? parentLayoutType,
        LayoutSpec? parentLayout)
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

    private static bool HasDefiniteCrossAxisSize(Dimension? dimension)
    {
        return dimension is { Unit: not DimensionUnit.Auto and not DimensionUnit.Fill and not DimensionUnit.Star };
    }

    private static bool ShouldFallbackToStartJustification(
        LayoutSpec layout,
        LayoutType? parentLayoutType,
        LayoutSpec? parentLayout)
    {
        if (layout.Justify is not (Justification.SpaceBetween or Justification.SpaceAround or Justification.SpaceEvenly))
        {
            return false;
        }

        return layout.Type switch
        {
            LayoutType.Horizontal => !HasDefiniteRenderedDimension("width", layout.Width, parentLayoutType, parentLayout),
            LayoutType.Vertical or LayoutType.Stack or LayoutType.Grid or LayoutType.Dock
                => !HasDefiniteRenderedDimension("height", layout.Height, parentLayoutType, parentLayout),
            _ => false
        };
    }

    private static bool HasDefiniteRenderedDimension(
        string propertyName,
        Dimension? dimension,
        LayoutType? parentLayoutType,
        LayoutSpec? parentLayout)
    {
        if (dimension == null)
        {
            return false;
        }

        return dimension.Value.Unit switch
        {
            DimensionUnit.Pixels or DimensionUnit.Percent or DimensionUnit.Cells => true,
            DimensionUnit.Auto => false,
            DimensionUnit.Fill or DimensionUnit.Star => propertyName switch
            {
                "width" when parentLayoutType == LayoutType.Horizontal => true,
                "width" when parentLayoutType is LayoutType.Vertical or LayoutType.Stack or LayoutType.Grid or LayoutType.Dock
                    => HasDefiniteCrossAxisSize(parentLayout?.Width),
                "height" when parentLayoutType is LayoutType.Vertical or LayoutType.Stack or LayoutType.Grid or LayoutType.Dock
                    => true,
                "height" when parentLayoutType == LayoutType.Horizontal
                    => HasDefiniteCrossAxisSize(parentLayout?.Height),
                _ => false
            },
            _ => false
        };
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

    private static string? BuildComponentOverrideLiteral(ComponentNode node)
    {
        var overrides = ComponentInstanceOverrideSupport.GetPropertyOverrides(node);
        if (overrides.Count == 0)
        {
            return null;
        }

        return "new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal)\n        {\n" +
            string.Join(",\n",
                overrides.Select(pathEntry =>
                    $"            [{ToStringLiteral(pathEntry.Key)}] = new Dictionary<string, object?>(StringComparer.Ordinal)\n            {{\n" +
                    string.Join(",\n", pathEntry.Value.Select(propertyEntry => $"                [{ToStringLiteral(propertyEntry.Key)}] = {ToValueLiteral(propertyEntry.Value) ?? "null"}")) +
                    "\n            }")) +
            "\n        }";
    }

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
