using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;
using BoomHud.Generators.VisualIR;

namespace BoomHud.Gen.UGui;

public sealed partial class UGuiGenerator : IBackendGenerator
{
    public string TargetFramework => "Unity uGUI";
    public ICapabilityManifest Capabilities => UGuiCapabilities.Instance;

    public GenerationResult Generate(HudDocument document, GenerationOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var files = new List<GeneratedFile>();
        var prepared = GenerationDocumentPreprocessor.Prepare(document, options, "ugui");
        var buildProgramOverride = LoadBuildProgramOverride(options, diagnostics);
        document = prepared.Document;
        diagnostics.AddRange(prepared.Diagnostics);

        try
        {
            var visualPlan = VisualToUGuiPlan.Build(prepared.VisualDocument);
            foreach (var component in document.Components.Values)
            {
                Emit(new HudDocument
                {
                    Name = component.Name,
                    Metadata = component.Metadata,
                    Root = component.Root,
                    Styles = document.Styles,
                    Components = document.Components
                }, options, diagnostics, files, visualPlan, buildProgramOverride);
            }

            var plan = Emit(document, options, diagnostics, files, visualPlan, buildProgramOverride);
            if (options.Motion != null)
            {
                EmitMotion(document, plan, options, diagnostics, files);
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

        if (options.EmitUGuiBuildProgramArtifact
            && GenerationDocumentPreprocessor.CreateUGuiBuildProgramArtifact(document.Name, prepared.UGuiBuildProgram) is { } uguiBuildProgramArtifact)
        {
            files.Add(uguiBuildProgramArtifact);
        }

        return new GenerationResult { Files = files, Diagnostics = diagnostics };
    }

    private static UGuiBuildProgram? LoadBuildProgramOverride(GenerationOptions options, List<Diagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(options.UGuiBuildProgramPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UGuiBuildProgram>(File.ReadAllText(options.UGuiBuildProgramPath))
                ?? throw new InvalidOperationException("The file did not deserialize into a uGUI build program.");
        }
        catch (Exception exception)
        {
            diagnostics.Add(Diagnostic.Warning(
                $"Failed to load experimental uGUI build program '{options.UGuiBuildProgramPath}': {exception.Message}",
                code: "BHUG1004"));
            return null;
        }
    }

    private static PlanDocument Emit(HudDocument document, GenerationOptions options, List<Diagnostic> diagnostics, List<GeneratedFile> files, VisualToUGuiPlan? visualPlan, UGuiBuildProgram? buildProgramOverride)
    {
        var plan = Planner.Create(document, diagnostics, options.RuleSet, visualPlan, buildProgramOverride);
        files.Add(new GeneratedFile { Path = $"{document.Name}View.ugui.cs", Content = GenerateView(document, plan, options, diagnostics), Type = GeneratedFileType.SourceCode });
        if (options.EmitViewModelInterfaces)
        {
            files.Add(new GeneratedFile { Path = $"I{document.Name}ViewModel.g.cs", Content = GenerateInterface(document.Name, plan.Properties, options), Type = GeneratedFileType.SourceCode });
        }

        return plan;
    }

    private static string GenerateView(HudDocument document, PlanDocument plan, GenerationOptions options, List<Diagnostic> diagnostics)
    {
        var builder = new StringBuilder();
        if (options.IncludeComments)
        {
            builder.AppendLine("// <auto-generated>");
            builder.AppendLine($"// Generated by BoomHud.Gen.UGui from {document.Name}");
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
        builder.AppendLine("using PropertyChangedEventArgs = System.ComponentModel.PropertyChangedEventArgs;");
        builder.AppendLine("using System.Globalization;");
        builder.AppendLine("using TMPro;");
        builder.AppendLine("using UnityEngine;");
        builder.AppendLine("using UnityEngine.UI;");
        builder.AppendLine();
        builder.AppendLine($"namespace {options.Namespace}");
        builder.AppendLine("{");
        builder.AppendLine($"public sealed class {document.Name}View");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? _componentOverrides;");
        builder.AppendLine($"    private I{document.Name}ViewModel? _viewModel;");
        builder.AppendLine("    public RectTransform Root { get; }");
        foreach (var node in plan.Nodes)
        {
            builder.AppendLine($"    public {node.FieldType} {node.FieldName} {{ get; }}");
        }

        builder.AppendLine();
        builder.AppendLine($"    public I{document.Name}ViewModel? ViewModel");
        builder.AppendLine("    {");
        builder.AppendLine("        get => _viewModel;");
        builder.AppendLine("        set");
        builder.AppendLine("        {");
        builder.AppendLine("            if (ReferenceEquals(_viewModel, value)) return;");
        builder.AppendLine("            if (_viewModel != null) _viewModel.PropertyChanged -= OnChanged;");
        builder.AppendLine("            _viewModel = value;");
        builder.AppendLine("            if (_viewModel != null) _viewModel.PropertyChanged += OnChanged;");
        builder.AppendLine("            Refresh();");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    public {document.Name}View(Transform? parent = null, I{document.Name}ViewModel? viewModel = null, IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? componentOverrides = null)");
        builder.AppendLine("    {");
        builder.AppendLine("        _componentOverrides = componentOverrides;");
        builder.AppendLine($"        Root = CreateRect(\"{plan.Root.Name}\", parent);");
        AppendSetup(builder, plan.Root, "Root", parentNode: null, document.Components, diagnostics, 2);
        foreach (var child in plan.Root.Children)
        {
            AppendCreate(builder, child, "Root", plan.Root, document.Components, diagnostics, 2);
        }
        builder.AppendLine("        ApplyInstanceOverrides();");
        builder.AppendLine("        ViewModel = viewModel;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    private {document.Name}View(RectTransform root, I{document.Name}ViewModel? viewModel, IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? componentOverrides)");
        builder.AppendLine("    {");
        builder.AppendLine("        _componentOverrides = componentOverrides;");
        builder.AppendLine("        Root = root;");
        AppendBindExisting(builder, plan.Root, parentPath: null, indentLevel: 2);
        builder.AppendLine("        ApplyInstanceOverrides();");
        builder.AppendLine("        ViewModel = viewModel;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    public static {document.Name}View Bind(RectTransform root, I{document.Name}ViewModel? viewModel = null, IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? componentOverrides = null) => new(root, viewModel, componentOverrides);");
        builder.AppendLine();
        builder.AppendLine("    private void ApplyInstanceOverrides()");
        builder.AppendLine("    {");
        builder.AppendLine("        if (_componentOverrides == null) return;");
        var componentOverrideIndex = 0;
        AppendComponentOverrideAssignmentsRecursive(builder, plan.Root, 2, ref componentOverrideIndex);
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public void Refresh()");
        builder.AppendLine("    {");
        builder.AppendLine("        if (_viewModel == null) return;");
        AppendRefresh(builder, plan.Root, plan, 2);
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();");
        builder.AppendLine();
        builder.AppendLine("    private bool TryGetComponentOverrideValue(string nodePath, string propertyName, out object? value)");
        builder.AppendLine("    {");
        builder.AppendLine("        value = null;");
        builder.AppendLine("        if (_componentOverrides == null || !_componentOverrides.TryGetValue(nodePath, out var propertyOverrides)) return false;");
        builder.AppendLine("        if (propertyOverrides.TryGetValue(propertyName, out value)) return true;");
        builder.AppendLine("        foreach (var candidate in propertyOverrides)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (string.Equals(candidate.Key, propertyName, StringComparison.OrdinalIgnoreCase))");
        builder.AppendLine("            {");
        builder.AppendLine("                value = candidate.Value;");
        builder.AppendLine("                return true;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("        return false;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append(HelperCode);
        builder.AppendLine("}");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string GenerateInterface(string name, IReadOnlyList<ViewModelProperty> properties, GenerationOptions options)
    {
        var builder = new StringBuilder();
        if (options.IncludeComments)
        {
            builder.AppendLine("// <auto-generated>");
            builder.AppendLine($"// Generated by BoomHud.Gen.UGui from {name}");
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
        builder.AppendLine($"namespace {options.ViewModelNamespace ?? options.Namespace}");
        builder.AppendLine("{");
        builder.AppendLine($"public interface I{name}ViewModel : INotifyPropertyChanged");
        builder.AppendLine("{");
        foreach (var property in properties)
        {
            builder.AppendLine($"    object? {property.Identifier} {{ get; }}");
        }
        builder.AppendLine("}");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendCreate(StringBuilder builder, PlannedNode node, string parentAccessor, PlannedNode? parentNode, IReadOnlyDictionary<string, HudComponentDefinition> components, List<Diagnostic> diagnostics, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        if (node.ComponentView != null)
        {
            var local = ToCamel(node.FieldName) + "View";
            builder.AppendLine($"{indent}var {local} = new {node.ComponentView}View({parentAccessor}, null, {BuildComponentOverrideLiteral(node.Source) ?? "null"});");
            builder.AppendLine($"{indent}{node.FieldName} = {local}.Root;");
            builder.AppendLine($"{indent}{node.FieldName}.name = \"{node.Name}\";");
            if (IsSyntheticComponentInstance(node.Source))
            {
                AppendSyntheticInstancePlacement(builder, node, node.FieldName, parentNode, indentLevel);
            }
            else
            {
                AppendSetup(builder, node, node.FieldName, parentNode, components, diagnostics, indentLevel);
            }
            return;
        }

        builder.AppendLine($"{indent}{node.FieldName} = {CreateCall(node, parentAccessor)};");
        AppendSetup(builder, node, node.FieldName, parentNode, components, diagnostics, indentLevel);
        foreach (var child in node.Children)
        {
            AppendCreate(builder, child, ChildParent(node), node, components, diagnostics, indentLevel);
        }
    }

    private static void AppendSetup(StringBuilder builder, PlannedNode node, string accessor, PlannedNode? parentNode, IReadOnlyDictionary<string, HudComponentDefinition> components, List<Diagnostic> diagnostics, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        var rect = accessor == "Root" ? "Root" : $"RectOf({accessor})";
        var layout = node.Source.Layout;
        var style = node.Source.Style;
        var edgeContract = node.VisualNode?.EdgeContract;
        var textMetric = node.MetricProfile?.Text;
        var iconMetric = node.MetricProfile?.Icon;
        var parentLayout = parentNode?.Source.Layout?.Type;
        var absolute = parentLayout == LayoutType.Absolute
                       || edgeContract?.Participation == LayoutParticipation.Overlay
                       || LayoutPolicyService.HasAbsolutePlacement(node.Source, node.Policy);
        var widthDimension = layout?.Width ?? style?.Width;
        var heightDimension = layout?.Height ?? style?.Height;
        var anchorPreset = LayoutPolicyService.ResolveAnchorPreset(node.Policy);
        var pivotPreset = LayoutPolicyService.ResolvePivotPreset(node.Policy);
        var rectTransformMode = LayoutPolicyService.ResolveRectTransformMode(node.Policy);
        var edgeInsetPolicy = LayoutPolicyService.ResolveEdgeInsetPolicy(node.Policy);
        var flexAlignmentPreset = ResolveLayoutAlignmentPreset(node);

        var configuredWidth = absolute
            ? ConfigureAbsoluteSize(node, widthDimension, "width")
            : ConfigureSize(node, parentNode, widthDimension, "width");
        var configuredHeight = absolute
            ? ConfigureAbsoluteSize(node, heightDimension, "height")
            : ConfigureSize(node, parentNode, heightDimension, "height");

        builder.AppendLine($"{indent}ConfigureRect({rect}, width: {ToNullableFloatLiteral(configuredWidth)}, height: {ToNullableFloatLiteral(configuredHeight)}, left: {ToNullableFloatLiteral(absolute ? AbsoluteOffset(node.Source, static x => x.Left, BoomHudMetadataKeys.PencilLeft, node.Policy, "x") : null)}, top: {ToNullableFloatLiteral(absolute ? AbsoluteOffset(node.Source, static x => x.Top, BoomHudMetadataKeys.PencilTop, node.Policy, "y") : null)}, absolute: {Bool(absolute)});");
        if (!string.IsNullOrWhiteSpace(anchorPreset))
        {
            builder.AppendLine($"{indent}ApplyRectAnchorPreset({rect}, {ToStringLiteral(anchorPreset)});");
        }

        if (!string.IsNullOrWhiteSpace(pivotPreset))
        {
            builder.AppendLine($"{indent}ApplyRectPivotPreset({rect}, {ToStringLiteral(pivotPreset)});");
        }

        if (!string.IsNullOrWhiteSpace(rectTransformMode))
        {
            builder.AppendLine($"{indent}ApplyRectTransformMode({rect}, {ToStringLiteral(rectTransformMode)});");
        }

        if (!string.IsNullOrWhiteSpace(edgeInsetPolicy))
        {
            builder.AppendLine($"{indent}ApplyEdgeInsetPolicy({rect}, {ToStringLiteral(edgeInsetPolicy)});");
        }

        var contentFitHorizontal = absolute
            ? ShouldAbsoluteContentFit(node, widthDimension, "width")
            : ShouldContentFit(node, "width", parentLayout);
        var contentFitVertical = absolute
            ? ShouldAbsoluteContentFit(node, heightDimension, "height")
            : ShouldContentFit(node, "height", parentLayout);
        builder.AppendLine($"{indent}ApplyLayoutSizing({rect}, ignoreLayout: {Bool(absolute)}, preferredWidth: {ToNullableFloatLiteral(!absolute ? PreferredSize(node, parentNode, widthDimension, "width") : null)}, preferredHeight: {ToNullableFloatLiteral(!absolute ? PreferredSize(node, parentNode, heightDimension, "height") : null)}, flexibleWidth: {ToNullableFloatLiteral(!absolute ? FlexibleSize(node, parentNode, widthDimension, "width", parentLayout, contentFitHorizontal) : null)}, flexibleHeight: {ToNullableFloatLiteral(!absolute ? FlexibleSize(node, parentNode, heightDimension, "height", parentLayout, contentFitVertical) : null)});");
        builder.AppendLine($"{indent}ApplyContentSizeFit({rect}, horizontal: {Bool(contentFitHorizontal)}, vertical: {Bool(contentFitVertical)});");

        if (layout != null && ShouldEmitLayoutGroup(node))
        {
            var resolvedGap = LayoutPolicyService.ResolveGap(layout.Gap, node.Policy);
            var resolvedPadding = LayoutPolicyService.ResolvePadding(layout.Padding, node.Policy);
            var shellLayout = ResolveShellLayout(node, parentNode, resolvedGap, resolvedPadding);
            switch (layout.Type)
            {
                case LayoutType.Horizontal:
                    builder.AppendLine($"{indent}{BuildHorizontalLayoutCall(rect, shellLayout, flexAlignmentPreset)}");
                    break;
                case LayoutType.Vertical:
                case LayoutType.Stack:
                    builder.AppendLine($"{indent}{BuildVerticalLayoutCall(rect, shellLayout, flexAlignmentPreset)}");
                    break;
                case LayoutType.Grid:
                case LayoutType.Dock:
                    diagnostics.Add(Diagnostic.Warning($"Unity uGUI falls back to vertical layout for '{layout.Type}'.", node.Source.Id, "BHUG1001"));
                    builder.AppendLine($"{indent}{BuildVerticalLayoutCall(rect, shellLayout, flexAlignmentPreset)}");
                    break;
            }
        }

        var resolvedFontFamily = ResolveFontFamily(node, textMetric, iconMetric, widthDimension, heightDimension);
        var resolvedFontSize = ResolveFontSize(node, textMetric, iconMetric, widthDimension, heightDimension);

        if (style != null || node.Source.Type == ComponentType.Icon)
        {
            builder.AppendLine(
                $"{indent}ApplyStyle({accessor}, " +
                $"fg: {ToNullableStringLiteral(style?.Foreground?.ToHex())}, " +
                $"bg: {ToNullableStringLiteral(style?.Background?.ToHex())}, " +
                $"fontFamily: {ToNullableStringLiteral(resolvedFontFamily)}, " +
                $"fontSize: {ToNullableIntLiteral(resolvedFontSize)}, " +
                $"borderColor: {ToNullableStringLiteral(style?.Border?.Color?.ToHex())}, " +
                $"borderWidth: {ToNullableFloatLiteral(style?.Border is { Width: > 0 } border ? border.Width : null)}, " +
                $"treatAsIcon: {Bool(IsIconTextNode(node))});");
        }

        if (IsIconTextNode(node))
        {
            var width = Pixels(widthDimension) ?? 16d;
            var height = Pixels(heightDimension) ?? 16d;
            builder.AppendLine(
                $"{indent}ApplyIconMetrics({accessor}, boxWidth: {ToFloatLiteral(width)}, boxHeight: {ToFloatLiteral(height)}, baselineOffset: {ToFloatLiteral(iconMetric?.BaselineOffset ?? IconPolicyService.ResolveBaselineOffset(node.Policy))}, opticalCentering: {Bool(iconMetric?.OpticalCentering ?? IconPolicyService.UseOpticalCentering(node.Policy))}, sizeMode: {ToStringLiteral(iconMetric?.SizeMode ?? IconPolicyService.ResolveSizeMode(node.Policy))}, explicitFontSize: {ToFloatLiteral(resolvedFontSize ?? 0d)});");
        }

        if (ShouldApplyTextMetrics(node))
        {
            builder.AppendLine($"{indent}ApplyTextMetrics({accessor}, lineSpacing: {ToNullableFloatLiteral(TextPolicyService.ResolveLineSpacing(node.Source, widthDimension, heightDimension, node.Policy))}, wrapText: {Bool(textMetric?.WrapText ?? TextPolicyService.ShouldWrapText(node.Source, node.Policy))});");
        }

        foreach (var pair in Bindings(node.Source).Where(static pair => pair.StaticValue != null))
        {
            if (TryAssignment(node, pair.Property, pair.StaticValue!, staticValue: true, out var assignment))
            {
                builder.AppendLine($"{indent}{assignment}");
            }
        }

        if (!node.Source.Visible.IsBound)
        {
            builder.AppendLine($"{indent}{accessor}.gameObject.SetActive({Bool(node.Source.Visible.Value != false)});");
        }

        if (!node.Source.Enabled.IsBound && IsSelectable(node))
        {
            builder.AppendLine($"{indent}ApplyEnabled({accessor}, {Bool(node.Source.Enabled.Value != false)});");
        }

        if (node.Source.ComponentRefId != null && node.ComponentView == null && !components.ContainsKey(node.Source.ComponentRefId))
        {
            diagnostics.Add(Diagnostic.Warning($"Component reference '{node.Source.ComponentRefId}' was not found for uGUI generation.", node.Source.Id, "BHUG1004"));
        }
    }

    private static bool IsSyntheticComponentInstance(ComponentNode node)
        => node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.SyntheticComponentInstance, out var value)
           && value is bool isSynthetic
           && isSynthetic;

    private static void AppendSyntheticInstancePlacement(
        StringBuilder builder,
        PlannedNode node,
        string accessor,
        PlannedNode? parentNode,
        int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        var rect = accessor == "Root" ? "Root" : $"RectOf({accessor})";
        var parentLayout = parentNode?.Source.Layout?.Type;
        var edgeContract = node.VisualNode?.EdgeContract;
        var absolute = parentLayout == LayoutType.Absolute
                       || edgeContract?.Participation == LayoutParticipation.Overlay
                       || LayoutPolicyService.HasAbsolutePlacement(node.Source, node.Policy);
        var anchorPreset = LayoutPolicyService.ResolveAnchorPreset(node.Policy);
        var pivotPreset = LayoutPolicyService.ResolvePivotPreset(node.Policy);
        var rectTransformMode = LayoutPolicyService.ResolveRectTransformMode(node.Policy);
        var edgeInsetPolicy = LayoutPolicyService.ResolveEdgeInsetPolicy(node.Policy);

        if (!absolute
            && string.IsNullOrWhiteSpace(anchorPreset)
            && string.IsNullOrWhiteSpace(pivotPreset)
            && string.IsNullOrWhiteSpace(rectTransformMode)
            && string.IsNullOrWhiteSpace(edgeInsetPolicy))
        {
            return;
        }

        builder.AppendLine($"{indent}ConfigureRect({rect}, width: null, height: null, left: {ToNullableFloatLiteral(absolute ? AbsoluteOffset(node.Source, static x => x.Left, BoomHudMetadataKeys.PencilLeft, node.Policy, "x") : null)}, top: {ToNullableFloatLiteral(absolute ? AbsoluteOffset(node.Source, static x => x.Top, BoomHudMetadataKeys.PencilTop, node.Policy, "y") : null)}, absolute: {Bool(absolute)});");
        if (!string.IsNullOrWhiteSpace(anchorPreset))
        {
            builder.AppendLine($"{indent}ApplyRectAnchorPreset({rect}, {ToStringLiteral(anchorPreset)});");
        }

        if (!string.IsNullOrWhiteSpace(pivotPreset))
        {
            builder.AppendLine($"{indent}ApplyRectPivotPreset({rect}, {ToStringLiteral(pivotPreset)});");
        }

        if (!string.IsNullOrWhiteSpace(rectTransformMode))
        {
            builder.AppendLine($"{indent}ApplyRectTransformMode({rect}, {ToStringLiteral(rectTransformMode)});");
        }

        if (!string.IsNullOrWhiteSpace(edgeInsetPolicy))
        {
            builder.AppendLine($"{indent}ApplyEdgeInsetPolicy({rect}, {ToStringLiteral(edgeInsetPolicy)});");
        }

        if (absolute)
        {
            builder.AppendLine($"{indent}ApplyLayoutSizing({rect}, ignoreLayout: true, preferredWidth: null, preferredHeight: null, flexibleWidth: null, flexibleHeight: null);");
        }
    }

    private static void AppendComponentOverrideAssignmentsRecursive(StringBuilder builder, PlannedNode node, int indentLevel, ref int overrideIndex)
    {
        var indent = new string(' ', indentLevel * 4);
        foreach (var property in node.Source.Properties.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!ComponentInstanceOverrideSupport.IsSupportedProperty(node.Source, property.Key))
            {
                continue;
            }

            var overrideVariableName = FormattableString.Invariant($"componentOverrideValue{overrideIndex++}");
            if (!TryBuildInstanceOverrideAssignment(node, property.Key, overrideVariableName, out var assignment))
            {
                continue;
            }

            builder.AppendLine($"{indent}if (TryGetComponentOverrideValue({ToStringLiteral(node.RelativePath)}, {ToStringLiteral(property.Key)}, out var {overrideVariableName}))");
            builder.AppendLine($"{indent}{{");
            foreach (var line in assignment.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
            {
                builder.AppendLine($"{indent}    {line}");
            }
            builder.AppendLine($"{indent}}}");
        }

        foreach (var child in node.Children)
        {
            AppendComponentOverrideAssignmentsRecursive(builder, child, indentLevel, ref overrideIndex);
        }
    }

    private static void AppendBindExisting(StringBuilder builder, PlannedNode node, string? parentPath, int indentLevel)
    {
        foreach (var child in node.Children)
        {
            var indent = new string(' ', indentLevel * 4);
            var path = string.IsNullOrEmpty(parentPath) ? child.Name : parentPath + "/" + child.Name;
            builder.AppendLine($"{indent}{child.FieldName} = {BindLookup(child, path)};");
            AppendBindExisting(builder, child, path, indentLevel);
        }
    }

    private static void AppendRefresh(StringBuilder builder, PlannedNode node, PlanDocument plan, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        var accessor = node.FieldName == "Root" ? "Root" : node.FieldName;
        if (node.Source.Visible.IsBound && TryIdentifier(plan, node.Source.Visible.BindingPath!, out var visible))
        {
            builder.AppendLine($"{indent}{accessor}.gameObject.SetActive(AsBool(_viewModel.{visible}, true));");
        }

        if (node.Source.Enabled.IsBound && IsSelectable(node) && TryIdentifier(plan, node.Source.Enabled.BindingPath!, out var enabled))
        {
            builder.AppendLine($"{indent}ApplyEnabled({accessor}, AsBool(_viewModel.{enabled}, true));");
        }

        foreach (var pair in Bindings(node.Source).Where(static pair => pair.BindingPath != null))
        {
            if (TryIdentifier(plan, pair.BindingPath!, out var identifier) && TryAssignment(node, pair.Property, "_viewModel." + identifier, staticValue: false, out var assignment))
            {
                builder.AppendLine($"{indent}{assignment}");
            }
        }

        foreach (var child in node.Children)
        {
            AppendRefresh(builder, child, plan, indentLevel);
        }
    }

    private static bool TryAssignment(PlannedNode node, string property, string value, bool staticValue, out string assignment)
    {
        assignment = string.Empty;
        var accessor = node.FieldName == "Root" ? "Root" : node.FieldName;
        var normalized = Normalize(property);
        var textValue = staticValue ? value : $"AsString({value})";

        if (IsIconTextNode(node) && normalized is "text" or "content" or "value")
        {
            assignment = $"{accessor}.text = ResolveIconText({textValue});";
            return true;
        }

        switch (node.FieldType)
        {
            case "Text":
            case "TextMeshProUGUI":
                if (normalized is "text" or "content" or "value") { assignment = $"{accessor}.text = {textValue};"; return true; }
                break;
            case "Button":
                if (normalized is "text" or "content" or "value") { assignment = $"SetButtonText({accessor}, {textValue});"; return true; }
                break;
            case "InputField":
                if (normalized is "text" or "content" or "value") { assignment = $"{accessor}.text = {textValue};"; return true; }
                break;
            case "Toggle":
                if (normalized is "checked" or "value") { assignment = $"{accessor}.isOn = {(staticValue ? value : $"AsBool({value}, false)")};"; return true; }
                if (normalized is "text" or "content") { assignment = $"SetToggleText({accessor}, {textValue});"; return true; }
                break;
            case "Slider":
                if (normalized == "value") { assignment = $"{accessor}.value = {(staticValue ? value : $"AsFloat({value})")};"; return true; }
                break;
            case "Image":
                if (normalized is "source" or "src" or "value") { assignment = $"SetImage({accessor}, {textValue});"; return true; }
                break;
        }

        return false;
    }

    private static bool TryBuildInstanceOverrideAssignment(PlannedNode node, string property, string valueExpression, out string assignment)
    {
        assignment = string.Empty;
        var accessor = node.FieldName == "Root" ? "Root" : node.FieldName;
        var normalized = Normalize(property);

        if (IsIconTextNode(node) && normalized is "text" or "content" or "value")
        {
            assignment = $"{accessor}.text = ResolveIconText(AsString({valueExpression}));";
            return true;
        }

        switch (node.FieldType)
        {
            case "Text":
            case "TextMeshProUGUI":
                if (normalized is "text" or "content" or "value") { assignment = $"{accessor}.text = AsString({valueExpression});"; return true; }
                break;
            case "Button":
                if (normalized is "text" or "content" or "value") { assignment = $"SetButtonText({accessor}, AsString({valueExpression}));"; return true; }
                break;
            case "InputField":
                if (normalized is "text" or "content" or "value") { assignment = $"{accessor}.text = AsString({valueExpression});"; return true; }
                break;
            case "Toggle":
                if (normalized is "checked" or "value") { assignment = $"{accessor}.isOn = AsBool({valueExpression}, false);"; return true; }
                if (normalized is "text" or "content") { assignment = $"SetToggleText({accessor}, AsString({valueExpression}));"; return true; }
                break;
            case "Slider":
                if (normalized == "value") { assignment = $"{accessor}.value = AsFloat({valueExpression});"; return true; }
                break;
            case "Image":
                if (normalized is "source" or "src" or "value") { assignment = $"SetImage({accessor}, AsString({valueExpression}));"; return true; }
                break;
        }

        return false;
    }

    private static string BindLookup(PlannedNode node, string path)
        => node.FieldType == "RectTransform"
            ? $"RequireRect(Root, \"{path}\")"
            : $"RequireComponent<{node.FieldType}>(Root, \"{path}\")";

    private static IEnumerable<(string Property, string? StaticValue, string? BindingPath)> Bindings(ComponentNode node)
    {
        foreach (var binding in node.Bindings) yield return (binding.Property, null, binding.Path);
        foreach (var property in node.Properties)
        {
            yield return property.Value.IsBound ? (property.Key, null, property.Value.BindingPath) : (property.Key, Literal(property.Value.Value), null);
        }
    }

    private static bool TryIdentifier(PlanDocument plan, string path, out string identifier)
    {
        var match = plan.Properties.FirstOrDefault(p => string.Equals(p.Path, path, StringComparison.Ordinal));
        if (match == null)
        {
            identifier = string.Empty;
            return false;
        }

        identifier = match.Identifier;
        return true;
    }

    private static double? AbsoluteOffset(ComponentNode node, Func<LayoutSpec, Dimension?> selector, string metadataKey, ResolvedGeneratorPolicy policy, string axis)
    {
        double? value = null;
        if (node.Layout != null && selector(node.Layout) is { Unit: DimensionUnit.Pixels } dimension)
        {
            value = dimension.Value;
        }
        else if (node.InstanceOverrides.TryGetValue(metadataKey, out var raw) && raw != null)
        {
            value = raw switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                _ => null
            };
        }

        var adjustment = LayoutPolicyService.ResolveOffsetAdjustment(axis, policy);
        if (value == null)
        {
            return Math.Abs(adjustment) > double.Epsilon ? adjustment : null;
        }

        return value + adjustment;
    }

    private static double? Pixels(Dimension? dimension)
        => dimension switch
        {
            { Unit: DimensionUnit.Pixels } pixels => pixels.Value,
            { Unit: DimensionUnit.Cells } cells => cells.Value,
            _ => null
        };

    private static string? ResolveFontFamily(
        PlannedNode node,
        TextMetricProfile? textMetric,
        IconMetricProfile? iconMetric,
        Dimension? widthDimension,
        Dimension? heightDimension)
    {
        if (IsIconTextNode(node))
        {
            return iconMetric?.ResolvedFontFamily
                   ?? textMetric?.ResolvedFontFamily
                   ?? TextPolicyService.ResolveFontFamily(node.Source, node.Policy);
        }

        return node.Policy.Text.FontFamily
               ?? textMetric?.ResolvedFontFamily
               ?? iconMetric?.ResolvedFontFamily
               ?? TextPolicyService.ResolveFontFamily(node.Source, node.Policy);
    }

    private static double? ResolveFontSize(
        PlannedNode node,
        TextMetricProfile? textMetric,
        IconMetricProfile? iconMetric,
        Dimension? widthDimension,
        Dimension? heightDimension)
    {
        if (IsIconTextNode(node))
        {
            var baseFontSize = iconMetric?.ResolvedFontSize ?? textMetric?.ResolvedFontSize;
            if (node.Policy.Icon.FontSize is double explicitIconFontSize && explicitIconFontSize > 0d)
            {
                baseFontSize = explicitIconFontSize;
            }
            else if (baseFontSize is null || baseFontSize <= 0d)
            {
                baseFontSize = IconPolicyService.ResolveFontSize(node.Source, widthDimension, heightDimension, node.Policy);
            }

            if (node.Policy.Icon.FontSizeDelta is { } iconFontSizeDelta)
            {
                baseFontSize = (baseFontSize ?? 0d) + iconFontSizeDelta;
            }

            return baseFontSize is > 0d ? baseFontSize : null;
        }

        var resolvedTextFontSize = textMetric?.ResolvedFontSize ?? iconMetric?.ResolvedFontSize;
        if (node.Policy.Text.FontSize is double explicitTextFontSize && explicitTextFontSize > 0d)
        {
            resolvedTextFontSize = explicitTextFontSize;
        }
        else if (resolvedTextFontSize is null || resolvedTextFontSize <= 0d)
        {
            resolvedTextFontSize = TextPolicyService.ResolveFontSize(node.Source, widthDimension, heightDimension, node.Policy);
        }

        if (node.Policy.Text.FontSizeDelta is { } textFontSizeDelta)
        {
            resolvedTextFontSize = (resolvedTextFontSize ?? 0d) + textFontSizeDelta;
        }

        return resolvedTextFontSize is > 0d ? resolvedTextFontSize : null;
    }

    private static double? FlexibleSize(PlannedNode node, PlannedNode? parentNode, Dimension? dimension, string axis, LayoutType? parentLayout, bool preferContentSize)
        => preferContentSize || ShouldPreservePreferredSizeInParent(node, parentNode, axis)
            ? null
            : LayoutPolicyService.ResolveFlexibleSize(dimension, axis, parentLayout, IsFlexibleContainer(node), node.Policy);

    private static double? ConfigureSize(PlannedNode node, PlannedNode? parentNode, Dimension? dimension, string axis)
    {
        if (Pixels(dimension) is { } explicitPixels)
        {
            return PromoteIntrinsicShellSize(node, axis, explicitPixels);
        }

        if (ResolveCrossAxisFillSize(node, parentNode, axis) is { } crossAxisFill)
        {
            return crossAxisFill;
        }

        return ShouldPreservePreferredSizeInParent(node, parentNode, axis)
            ? PreferredSize(node, parentNode, dimension, axis)
            : null;
    }

    private static double? ConfigureAbsoluteSize(PlannedNode node, Dimension? dimension, string axis)
        => LayoutPolicyService.ResolvePreferredSize(dimension, axis, node.Policy);

    private static double? PreferredSize(PlannedNode node, PlannedNode? parentNode, Dimension? dimension, string axis)
    {
        if (LayoutPolicyService.ResolvePreferredSize(dimension, axis, node.Policy) is { } explicitPreferred)
        {
            return PromoteIntrinsicShellSize(node, axis, explicitPreferred);
        }

        if (ResolveCrossAxisFillSize(node, parentNode, axis) is { } crossAxisFill)
        {
            return crossAxisFill;
        }

        return ShouldPreservePreferredSizeInParent(node, parentNode, axis)
            ? EstimateIntrinsicAxisSize(node, axis)
            : null;
    }

    private static double PromoteIntrinsicShellSize(PlannedNode node, string axis, double explicitSize)
    {
        if (!ShouldPromoteIntrinsicShellSize(node, axis))
        {
            return explicitSize;
        }

        var intrinsic = EstimateChildStackAxisSize(node, axis);
        if (!intrinsic.HasValue)
        {
            return explicitSize;
        }

        var overflow = intrinsic.Value - explicitSize;
        return overflow is > 6d and <= 64d
            ? intrinsic.Value
            : explicitSize;
    }

    private static bool ShouldPromoteIntrinsicShellSize(PlannedNode node, string axis)
    {
        if (node.ComponentView != null
            || node.Children.Count == 0
            || node.Source.Layout?.Type is not (LayoutType.Vertical or LayoutType.Stack or LayoutType.Horizontal))
        {
            return false;
        }

        if (node.VisualNode?.EdgeContract.Participation == LayoutParticipation.Overlay
            || LayoutPolicyService.HasAbsolutePlacement(node.Source, node.Policy))
        {
            return false;
        }

        var dimension = axis == "width"
            ? node.Source.Layout?.Width ?? node.Source.Style?.Width
            : node.Source.Layout?.Height ?? node.Source.Style?.Height;
        return HasPinnedSize(dimension) && HasExplicitContentPreference(node, axis);
    }

    private static double? EstimateChildStackAxisSize(PlannedNode node, string axis)
    {
        if (node.Source.Layout?.Type is not (LayoutType.Vertical or LayoutType.Stack or LayoutType.Horizontal))
        {
            return null;
        }

        var isHorizontal = node.Source.Layout.Type == LayoutType.Horizontal;
        var mainAxis = isHorizontal ? "width" : "height";
        var resolvedPadding = LayoutPolicyService.ResolvePadding(node.Source.Layout.Padding, node.Policy) ?? Spacing.Zero;
        var gap = Gap(LayoutPolicyService.ResolveGap(node.Source.Layout.Gap, node.Policy), isHorizontal);
        var childSizes = node.Children
            .Select(child => ResolveNominalAxisSize(child, axis))
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .ToList();
        if (childSizes.Count == 0)
        {
            return null;
        }

        var paddingTotal = axis == "width"
            ? resolvedPadding.Left + resolvedPadding.Right
            : resolvedPadding.Top + resolvedPadding.Bottom;
        var gapTotal = axis == mainAxis
            ? gap * Math.Max(0, node.Children.Count - 1)
            : 0d;
        var content = axis == mainAxis
            ? childSizes.Sum()
            : childSizes.Max();
        return content + gapTotal + paddingTotal;
    }

    private static bool IsFlexibleContainer(PlannedNode node)
        => node.ComponentView != null
            || node.Source.Children.Count > 0
            || node.FieldType is "RectTransform" or "ScrollRect";

    private static bool ShouldEmitLayoutGroup(PlannedNode node)
        => node.Source.Children.Count > 0
            && node.ComponentView == null
            && node.FieldType is "RectTransform" or "ScrollRect";

    private static bool ShouldContentFit(PlannedNode node, string axis, LayoutType? parentLayout)
    {
        if (LayoutPolicyService.ShouldPreferContentSize(axis, parentLayout != null, node.Source.Layout, IsFlexibleContainer(node), node.Policy))
        {
            return true;
        }

        return axis == "height" && ShouldPropagateVerticalContentHug(node);
    }

    private static bool ShouldAbsoluteContentFit(PlannedNode node, Dimension? dimension, string axis)
    {
        if (!SupportsAbsoluteContentFit(node) || HasPinnedSize(dimension))
        {
            return false;
        }

        var explicitPreference = axis == "width"
            ? node.Policy.Layout.PreferContentWidth
            : node.Policy.Layout.PreferContentHeight;
        if (explicitPreference is { } preferContent)
        {
            return preferContent;
        }

        return dimension is null or { Unit: DimensionUnit.Auto };
    }

    private static bool SupportsAbsoluteContentFit(PlannedNode node)
        => node.FieldType is "Text" or "TextMeshProUGUI";

    private static bool ShouldPropagateVerticalContentHug(PlannedNode node)
    {
        if (node.ComponentView != null
            || node.FieldType == "ScrollRect"
            || node.Source.Layout?.Type is not (LayoutType.Vertical or LayoutType.Stack)
            || node.Source.Children.Count == 0)
        {
            return false;
        }

        if (HasPinnedHeight(node.Source.Layout?.Height) || HasPinnedHeight(node.Source.Style?.Height))
        {
            return false;
        }

        return node.Children.All(static child => IsVerticalTextStackChild(child));
    }

    private static bool IsVerticalTextStackChild(PlannedNode child)
    {
        if (LayoutPolicyService.HasAbsolutePlacement(child.Source, child.Policy))
        {
            return false;
        }

        if (child.Source.Layout?.Height is { Unit: DimensionUnit.Fill or DimensionUnit.Star }
            || child.Source.Style?.Height is { Unit: DimensionUnit.Fill or DimensionUnit.Star })
        {
            return false;
        }

        if (child.FieldType is "Text" or "TextMeshProUGUI" or "Button" or "Toggle" or "InputField")
        {
            return true;
        }

        return ShouldPropagateVerticalContentHug(child);
    }

    private static bool HasPinnedHeight(Dimension? dimension)
        => dimension is { Unit: DimensionUnit.Pixels or DimensionUnit.Percent or DimensionUnit.Cells };

    private static bool HasPinnedSize(Dimension? dimension)
        => dimension is { Unit: DimensionUnit.Pixels or DimensionUnit.Percent or DimensionUnit.Cells };

    private static bool ShouldApplyTextMetrics(PlannedNode node)
        => node.FieldType is "Text" or "TextMeshProUGUI" or "Button" or "Toggle" or "InputField";

    private static double Gap(Spacing? spacing, bool horizontal)
        => spacing == null ? 0d : horizontal ? Math.Max(spacing.Value.Left, spacing.Value.Right) : Math.Max(spacing.Value.Top, spacing.Value.Bottom);

    private static ShellLayoutSettings ResolveShellLayout(PlannedNode node, PlannedNode? parentNode, Spacing? gap, Spacing? padding)
    {
        var resolvedPadding = padding ?? Spacing.Zero;
        var horizontal = node.Source.Layout?.Type == LayoutType.Horizontal;
        var mainAxis = horizontal ? "width" : "height";
        var settings = new ShellLayoutSettings
        {
            Gap = Gap(gap, horizontal),
            Padding = resolvedPadding,
            ChildControlWidth = ShouldControlChildAxis(node, "width"),
            ChildControlHeight = ShouldControlChildAxis(node, "height")
        };

        if (ShouldDisableMainAxisChildControlForCrossAxisFillShell(node, parentNode, mainAxis))
        {
            settings = settings with
            {
                ChildControlWidth = horizontal ? false : settings.ChildControlWidth,
                ChildControlHeight = horizontal ? settings.ChildControlHeight : false
            };
        }

        var crossAxis = horizontal ? "height" : "width";
        if (ShouldDisableCrossAxisChildControlForOverflowingChildShells(node, crossAxis))
        {
            settings = settings with
            {
                ChildControlWidth = horizontal ? settings.ChildControlWidth : false,
                ChildControlHeight = horizontal ? false : settings.ChildControlHeight
            };
        }

        if (ShouldPreserveSourceShellSpacing(node, parentNode, mainAxis))
        {
            return settings;
        }

        return TryAdjustShellOverflow(node, parentNode, settings) is { } adjusted
            ? adjusted
            : settings;
    }

    private static ShellLayoutSettings? TryAdjustShellOverflow(PlannedNode node, PlannedNode? parentNode, ShellLayoutSettings settings)
    {
        if (node.Source.Layout?.Type is not (LayoutType.Vertical or LayoutType.Stack or LayoutType.Horizontal))
        {
            return null;
        }

        var mainAxis = node.Source.Layout.Type == LayoutType.Horizontal ? "width" : "height";
        if (ShouldPreservePreferredSizeInParent(node, parentNode, mainAxis))
        {
            return null;
        }

        var available = ResolveAvailableMainAxis(node, parentNode, mainAxis);
        if (!available.HasValue || available.Value <= double.Epsilon)
        {
            return null;
        }

        var preferred = EstimatePreferredMainAxis(node, settings, mainAxis);
        var overflow = preferred - available.Value;
        if (overflow <= 6d || overflow > 48d)
        {
            return null;
        }

        var adjustable = mainAxis == "width"
            ? settings.Padding.Left + settings.Padding.Right + (settings.Gap * Math.Max(0, node.Children.Count - 1))
            : settings.Padding.Top + settings.Padding.Bottom + (settings.Gap * Math.Max(0, node.Children.Count - 1));
        if (adjustable <= double.Epsilon)
        {
            return null;
        }

        var scale = Math.Clamp((adjustable - overflow) / adjustable, 0.25d, 1d);
        var adjustedGap = settings.Gap * scale;
        var adjustedPadding = mainAxis == "width"
            ? new Spacing(
                settings.Padding.Top,
                settings.Padding.Right * scale,
                settings.Padding.Bottom,
                settings.Padding.Left * scale)
            : new Spacing(
                settings.Padding.Top * scale,
                settings.Padding.Right,
                settings.Padding.Bottom * scale,
                settings.Padding.Left);

        return settings with
        {
            Gap = adjustedGap,
            Padding = adjustedPadding,
            ChildControlWidth = mainAxis == "width" ? false : settings.ChildControlWidth,
            ChildControlHeight = mainAxis == "height" ? false : settings.ChildControlHeight
        };
    }

    private static bool ShouldPreserveSourceShellSpacing(PlannedNode node, PlannedNode? parentNode, string axis)
    {
        if (parentNode == null)
        {
            return false;
        }

        if (!IsCrossAxisOfParent(parentNode, axis))
        {
            return false;
        }

        var dimension = axis == "width"
            ? node.Source.Layout?.Width ?? node.Source.Style?.Width
            : node.Source.Layout?.Height ?? node.Source.Style?.Height;
        if (HasPinnedSize(dimension))
        {
            return false;
        }

        var edgeContract = node.VisualNode?.EdgeContract;
        if (edgeContract == null)
        {
            return true;
        }

        var overflowBehavior = axis == "width"
            ? edgeContract.OverflowX
            : edgeContract.OverflowY;
        return overflowBehavior != OverflowBehavior.Clip;
    }

    private static bool ShouldDisableMainAxisChildControlForCrossAxisFillShell(PlannedNode node, PlannedNode? parentNode, string axis)
    {
        if (parentNode == null || !IsCrossAxisOfParent(parentNode, axis))
        {
            return false;
        }

        var dimension = axis == "width"
            ? node.Source.Layout?.Width ?? node.Source.Style?.Width
            : node.Source.Layout?.Height ?? node.Source.Style?.Height;
        if (dimension?.Unit is not (DimensionUnit.Fill or DimensionUnit.Star))
        {
            return false;
        }

        if (node.Source.Layout?.ClipContent == true)
        {
            return false;
        }

        var edgeContract = node.VisualNode?.EdgeContract;
        var overflowBehavior = axis == "width"
            ? edgeContract?.OverflowX
            : edgeContract?.OverflowY;
        if (overflowBehavior == OverflowBehavior.Clip)
        {
            return false;
        }

        var horizontal = node.Source.Layout?.Type == LayoutType.Horizontal;
        var mainAxis = horizontal ? "width" : "height";
        if (!string.Equals(axis, mainAxis, StringComparison.Ordinal))
        {
            return false;
        }

        var settings = new ShellLayoutSettings
        {
            Gap = Gap(LayoutPolicyService.ResolveGap(node.Source.Layout?.Gap, node.Policy), horizontal),
            Padding = LayoutPolicyService.ResolvePadding(node.Source.Layout?.Padding, node.Policy) ?? Spacing.Zero,
            ChildControlWidth = ShouldControlChildAxis(node, "width"),
            ChildControlHeight = ShouldControlChildAxis(node, "height")
        };
        var available = ResolveAvailableMainAxis(node, parentNode, mainAxis);
        var preferred = EstimatePreferredMainAxis(node, settings, mainAxis);
        var overflow = preferred - available.GetValueOrDefault();
        return available.HasValue
            && overflow is > 6d and <= 64d;
    }

    private static bool ShouldDisableCrossAxisChildControlForOverflowingChildShells(PlannedNode node, string axis)
        => node.Children.Any(child => ShouldDisableMainAxisChildControlForCrossAxisFillShell(child, node, axis));

    private static double EstimatePreferredMainAxis(PlannedNode node, ShellLayoutSettings settings, string axis)
    {
        var childSizes = node.Children
            .Select(child => ResolveNominalAxisSize(child, axis))
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .Sum();

        var gapTotal = settings.Gap * Math.Max(0, node.Children.Count - 1);
        var paddingTotal = axis == "width"
            ? settings.Padding.Left + settings.Padding.Right
            : settings.Padding.Top + settings.Padding.Bottom;
        return childSizes + gapTotal + paddingTotal;
    }

    private static bool ShouldPreservePreferredSizeInParent(PlannedNode node, PlannedNode? parentNode, string axis)
    {
        if (parentNode == null || !IsCrossAxisOfParent(parentNode, axis))
        {
            return false;
        }

        if (node.Source.Layout?.Type is not (LayoutType.Vertical or LayoutType.Stack or LayoutType.Horizontal))
        {
            return false;
        }

        if (HasPinnedSize(axis == "width"
                ? node.Source.Layout?.Width ?? node.Source.Style?.Width
                : node.Source.Layout?.Height ?? node.Source.Style?.Height))
        {
            return false;
        }

        if (!HasExplicitContentPreference(node, axis))
        {
            return false;
        }

        var available = ResolveAvailableMainAxis(node, parentNode, axis);
        var preferred = EstimateIntrinsicAxisSize(node, axis);
        return available.HasValue
            && preferred.HasValue
            && preferred.Value > available.Value + 6d
            && preferred.Value - available.Value <= 64d;
    }

    private static bool IsCrossAxisOfParent(PlannedNode parentNode, string axis)
        => (axis == "height" && parentNode.Source.Layout?.Type == LayoutType.Horizontal)
           || (axis == "width" && parentNode.Source.Layout?.Type is LayoutType.Vertical or LayoutType.Stack);

    private static bool HasExplicitContentPreference(PlannedNode node, string axis)
        => axis == "width"
            ? node.Policy.Layout.PreferContentWidth == true
            : node.Policy.Layout.PreferContentHeight == true;

    private static double? ResolveCrossAxisFillSize(PlannedNode node, PlannedNode? parentNode, string axis)
    {
        if (parentNode == null || !IsCrossAxisOfParent(parentNode, axis))
        {
            return null;
        }

        var dimension = axis == "width"
            ? node.Source.Layout?.Width ?? node.Source.Style?.Width
            : node.Source.Layout?.Height ?? node.Source.Style?.Height;
        if (HasPinnedSize(dimension) || HasExplicitContentPreference(node, axis))
        {
            return null;
        }

        var edgeContract = node.VisualNode?.EdgeContract;
        var fillAxis = axis == "width"
            ? edgeContract?.WidthSizing == AxisSizing.Fill
            : edgeContract?.HeightSizing == AxisSizing.Fill;
        return fillAxis
            ? ResolveAvailableMainAxis(node, parentNode, axis)
            : null;
    }

    private static double? EstimateIntrinsicAxisSize(PlannedNode node, string axis)
    {
        var dimension = axis == "width"
            ? node.Source.Layout?.Width ?? node.Source.Style?.Width
            : node.Source.Layout?.Height ?? node.Source.Style?.Height;
        if (Pixels(dimension) is { } explicitPixels)
        {
            return explicitPixels;
        }

        var policyPreferred = LayoutPolicyService.ResolvePreferredSize(dimension, axis, node.Policy);
        if (policyPreferred.HasValue)
        {
            return policyPreferred.Value;
        }

        if (node.ReferencedRoot != null && EstimateIntrinsicAxisSize(node.ReferencedRoot, axis) is { } plannedComponentIntrinsic)
        {
            return plannedComponentIntrinsic;
        }

        if (node.ReferencedComponent != null && EstimateIntrinsicAxisSize(node.ReferencedComponent.Root, axis) is { } componentIntrinsic)
        {
            return componentIntrinsic;
        }

        if (node.Source.Layout?.Type is not (LayoutType.Vertical or LayoutType.Stack or LayoutType.Horizontal))
        {
            return node.VisualNode is null
                ? null
                : axis == "width"
                    ? Pixels(node.VisualNode.Box.Width)
                    : Pixels(node.VisualNode.Box.Height);
        }

        var isHorizontal = node.Source.Layout.Type == LayoutType.Horizontal;
        var mainAxis = isHorizontal ? "width" : "height";
        var resolvedPadding = LayoutPolicyService.ResolvePadding(node.Source.Layout.Padding, node.Policy) ?? Spacing.Zero;
        var gap = Gap(LayoutPolicyService.ResolveGap(node.Source.Layout.Gap, node.Policy), isHorizontal);
        var childSizes = node.Children
            .Select(child => EstimateIntrinsicAxisSize(child, axis))
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .ToList();
        if (childSizes.Count == 0)
        {
            return null;
        }

        var paddingTotal = axis == "width"
            ? resolvedPadding.Left + resolvedPadding.Right
            : resolvedPadding.Top + resolvedPadding.Bottom;
        var gapTotal = axis == mainAxis
            ? gap * Math.Max(0, node.Children.Count - 1)
            : 0d;
        var content = axis == mainAxis
            ? childSizes.Sum()
            : childSizes.Max();
        return content + gapTotal + paddingTotal;
    }

    private static double? ResolveAvailableMainAxis(PlannedNode node, PlannedNode? parentNode, string axis)
    {
        var ownDimension = axis == "width"
            ? node.Source.Layout?.Width ?? node.Source.Style?.Width
            : node.Source.Layout?.Height ?? node.Source.Style?.Height;
        if (Pixels(ownDimension) is { } ownPixels)
        {
            return ownPixels;
        }

        var edgeContract = node.VisualNode?.EdgeContract;
        var fillAxis = axis == "width"
            ? edgeContract?.WidthSizing == AxisSizing.Fill
            : edgeContract?.HeightSizing == AxisSizing.Fill;
        if (!fillAxis || parentNode == null)
        {
            if (parentNode == null || HasPinnedSize(ownDimension))
            {
                return null;
            }

            var parentLayoutType = parentNode.Source.Layout?.Type;
            var alignedWithParentCrossAxis =
                (axis == "height" && parentLayoutType == LayoutType.Horizontal)
                || (axis == "width" && parentLayoutType is LayoutType.Vertical or LayoutType.Stack);
            if (!alignedWithParentCrossAxis)
            {
                return null;
            }
        }

        var parentDimension = axis == "width"
            ? parentNode.Source.Layout?.Width ?? parentNode.Source.Style?.Width
            : parentNode.Source.Layout?.Height ?? parentNode.Source.Style?.Height;
        var parentPixels = Pixels(parentDimension);
        if (!parentPixels.HasValue)
        {
            return null;
        }

        var parentPadding = LayoutPolicyService.ResolvePadding(parentNode.Source.Layout?.Padding, parentNode.Policy) ?? Spacing.Zero;
        return axis == "width"
            ? parentPixels.Value - parentPadding.Left - parentPadding.Right
            : parentPixels.Value - parentPadding.Top - parentPadding.Bottom;
    }

    private static double? ResolveNominalAxisSize(PlannedNode node, string axis)
    {
        var dimension = axis == "width"
            ? node.Source.Layout?.Width ?? node.Source.Style?.Width
            : node.Source.Layout?.Height ?? node.Source.Style?.Height;
        if (Pixels(dimension) is { } pixels)
        {
            return pixels;
        }

        var policyPreferred = PreferredSize(node, null, dimension, axis);
        if (policyPreferred.HasValue)
        {
            return policyPreferred.Value;
        }

        if (node.ReferencedRoot != null && ResolveNominalAxisSize(node.ReferencedRoot, axis) is { } plannedComponentNominal)
        {
            return plannedComponentNominal;
        }

        if (node.ReferencedComponent != null && ResolveNominalAxisSize(node.ReferencedComponent.Root, axis) is { } componentNominal)
        {
            return componentNominal;
        }

        if (EstimateIntrinsicAxisSize(node, axis) is { } intrinsic)
        {
            return intrinsic;
        }

        return node.VisualNode is null
            ? null
            : axis == "width"
                ? Pixels(node.VisualNode.Box.Width)
                : Pixels(node.VisualNode.Box.Height);
    }

    private static bool ShouldControlChildAxis(PlannedNode node, string axis)
    {
        if (node.Children.Count == 0)
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (ShouldPreservePreferredSizeInParent(child, node, axis))
            {
                return false;
            }

            var dimension = axis == "width"
                ? child.Source.Layout?.Width ?? child.Source.Style?.Width
                : child.Source.Layout?.Height ?? child.Source.Style?.Height;
            var edgeSizing = axis == "width"
                ? child.VisualNode?.EdgeContract.WidthSizing
                : child.VisualNode?.EdgeContract.HeightSizing;
            if (!HasPinnedSize(dimension) || edgeSizing != AxisSizing.Fixed)
            {
                return true;
            }
        }

        return false;
    }

    private static double? EstimateIntrinsicAxisSize(ComponentNode node, string axis)
    {
        var dimension = axis == "width"
            ? node.Layout?.Width ?? node.Style?.Width
            : node.Layout?.Height ?? node.Style?.Height;
        if (Pixels(dimension) is { } explicitPixels)
        {
            return explicitPixels;
        }

        if (node.Layout?.Type is not (LayoutType.Vertical or LayoutType.Stack or LayoutType.Horizontal))
        {
            return null;
        }

        var isHorizontal = node.Layout.Type == LayoutType.Horizontal;
        var mainAxis = isHorizontal ? "width" : "height";
        var resolvedPadding = node.Layout.Padding ?? Spacing.Zero;
        var gap = Gap(node.Layout.Gap, isHorizontal);
        var childSizes = node.Children
            .Select(child => EstimateIntrinsicAxisSize(child, axis))
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .ToList();
        if (childSizes.Count == 0)
        {
            return null;
        }

        var paddingTotal = axis == "width"
            ? resolvedPadding.Left + resolvedPadding.Right
            : resolvedPadding.Top + resolvedPadding.Bottom;
        var gapTotal = axis == mainAxis
            ? gap * Math.Max(0, node.Children.Count - 1)
            : 0d;
        var content = axis == mainAxis
            ? childSizes.Sum()
            : childSizes.Max();
        return content + gapTotal + paddingTotal;
    }

    private static double? ResolveNominalAxisSize(ComponentNode node, string axis)
    {
        var dimension = axis == "width"
            ? node.Layout?.Width ?? node.Style?.Width
            : node.Layout?.Height ?? node.Style?.Height;
        if (Pixels(dimension) is { } explicitPixels)
        {
            return explicitPixels;
        }

        return EstimateIntrinsicAxisSize(node, axis);
    }

    private static string BuildHorizontalLayoutCall(string rect, ShellLayoutSettings settings, string? alignmentPreset)
    {
        var baseArgs = $"{rect}, {ToFloatLiteral(settings.Gap)}, {ToRectOffsetArgs(settings.Padding)}";
        if (settings.ChildControlWidth && settings.ChildControlHeight)
        {
            return alignmentPreset == null
                ? $"ApplyHorizontalLayout({baseArgs});"
                : $"ApplyHorizontalLayout({baseArgs}, {ToStringLiteral(alignmentPreset)});";
        }

        return $"ApplyHorizontalLayout({baseArgs}, {ToNullableStringLiteral(alignmentPreset)}, childControlWidth: {Bool(settings.ChildControlWidth)}, childControlHeight: {Bool(settings.ChildControlHeight)});";
    }

    private static string BuildVerticalLayoutCall(string rect, ShellLayoutSettings settings, string? alignmentPreset)
    {
        var baseArgs = $"{rect}, {ToFloatLiteral(settings.Gap)}, {ToRectOffsetArgs(settings.Padding)}";
        if (settings.ChildControlWidth && settings.ChildControlHeight)
        {
            return alignmentPreset == null
                ? $"ApplyVerticalLayout({baseArgs});"
                : $"ApplyVerticalLayout({baseArgs}, {ToStringLiteral(alignmentPreset)});";
        }

        return $"ApplyVerticalLayout({baseArgs}, {ToNullableStringLiteral(alignmentPreset)}, childControlWidth: {Bool(settings.ChildControlWidth)}, childControlHeight: {Bool(settings.ChildControlHeight)});";
    }

    private static string? ResolveLayoutAlignmentPreset(PlannedNode node)
    {
        if (LayoutPolicyService.ResolveFlexAlignmentPreset(node.Policy) is { } policyPreset)
        {
            return policyPreset;
        }

        return ResolveLayoutAlignmentPreset(node.Source.Layout);
    }

    private static string? ResolveLayoutAlignmentPreset(LayoutSpec? layout)
    {
        if (layout?.Type is not (LayoutType.Horizontal or LayoutType.Vertical or LayoutType.Stack))
        {
            return null;
        }

        if (layout.Align is null && layout.Justify is null)
        {
            return null;
        }

        var isHorizontal = layout.Type == LayoutType.Horizontal;
        var horizontalPosition = isHorizontal
            ? ResolveHorizontalAlignment(layout.Justify)
            : ResolveHorizontalAlignment(layout.Align);
        var verticalPosition = isHorizontal
            ? ResolveVerticalAlignment(layout.Align)
            : ResolveVerticalAlignment(layout.Justify);

        if (horizontalPosition == "stretch" || verticalPosition == "stretch")
        {
            return "stretch";
        }

        return (verticalPosition ?? "top", horizontalPosition ?? "left") switch
        {
            ("top", "left") => "top-left",
            ("top", "center") => "top-center",
            ("top", "right") => "top-right",
            ("middle", "left") => "middle-left",
            ("middle", "center") => "center",
            ("middle", "right") => "middle-right",
            ("bottom", "left") => "bottom-left",
            ("bottom", "center") => "bottom-center",
            ("bottom", "right") => "bottom-right",
            _ => null
        };
    }

    private static string? ResolveHorizontalAlignment(Alignment? align)
        => align switch
        {
            null or Alignment.Start => "left",
            Alignment.Center => "center",
            Alignment.End => "right",
            Alignment.Stretch => "stretch",
            _ => null
        };

    private static string? ResolveHorizontalAlignment(Justification? justify)
        => justify switch
        {
            null or Justification.Start => "left",
            Justification.Center => "center",
            Justification.End => "right",
            _ => null
        };

    private static string? ResolveVerticalAlignment(Alignment? align)
        => align switch
        {
            null or Alignment.Start => "top",
            Alignment.Center => "middle",
            Alignment.End => "bottom",
            Alignment.Stretch => "stretch",
            _ => null
        };

    private static string? ResolveVerticalAlignment(Justification? justify)
        => justify switch
        {
            null or Justification.Start => "top",
            Justification.Center => "middle",
            Justification.End => "bottom",
            _ => null
        };

    private static string ToRectOffsetArgs(Spacing? spacing)
    {
        var top = ToRectOffsetLiteral(spacing?.Top);
        var right = ToRectOffsetLiteral(spacing?.Right);
        var bottom = ToRectOffsetLiteral(spacing?.Bottom);
        var left = ToRectOffsetLiteral(spacing?.Left);
        return $"{left}, {right}, {top}, {bottom}";
    }

    private static string ToRectOffsetLiteral(double? value)
        => Convert.ToInt32(Math.Round(value ?? 0d, MidpointRounding.AwayFromZero), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    private sealed record ShellLayoutSettings
    {
        public required double Gap { get; init; }

        public required Spacing Padding { get; init; }

        public required bool ChildControlWidth { get; init; }

        public required bool ChildControlHeight { get; init; }
    }

    private static string ChildParent(PlannedNode node)
        => node.FieldType == "ScrollRect" ? $"{ToCamel(node.FieldName)}Content" : $"RectOf({node.FieldName})";

    private static string CreateCall(PlannedNode node, string parentAccessor)
        => node.FieldType switch
        {
            "Text" or "TextMeshProUGUI" => $"CreateText(\"{node.Name}\", {parentAccessor})",
            "Button" => $"CreateButton(\"{node.Name}\", {parentAccessor})",
            "InputField" => $"CreateInput(\"{node.Name}\", {parentAccessor}, {Bool(node.Source.Type == ComponentType.TextArea)})",
            "Toggle" => $"CreateToggle(\"{node.Name}\", {parentAccessor})",
            "Slider" => $"CreateSlider(\"{node.Name}\", {parentAccessor}, {Bool(node.Source.Type == ComponentType.Slider)})",
            "Image" => $"CreateImage(\"{node.Name}\", {parentAccessor})",
            "ScrollRect" => $"CreateScroll(\"{node.Name}\", {parentAccessor}, out var {ToCamel(node.FieldName)}Content)",
            _ => $"CreateRect(\"{node.Name}\", {parentAccessor})"
        };

    private static bool IsSelectable(PlannedNode node)
        => node.FieldType is "Button" or "Toggle" or "Slider" or "InputField";

    private static bool IsIconTextNode(PlannedNode node)
        => node.Source.Type == ComponentType.Icon && node.FieldType is "Text" or "TextMeshProUGUI";

    private static string Normalize(string property)
        => new string(property.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string? Literal(object? value)
        => value switch
        {
            null => null,
            string s => ToStringLiteral(s),
            bool b => Bool(b),
            float f => ToFloatLiteral(f),
            double d => d.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            _ => ToStringLiteral(value.ToString() ?? string.Empty)
        };

    private static string ToStringLiteral(string value)
        => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    private static string Bool(bool value) => value ? "true" : "false";
    private static string ToFloatLiteral(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "f";
    private static string ToNullableFloatLiteral(double? value) => value == null ? "null" : ToFloatLiteral(value.Value);
    private static string ToNullableIntLiteral(double? value) => value == null ? "null" : Convert.ToInt32(Math.Round(value.Value, MidpointRounding.AwayFromZero), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    private static string ToNullableStringLiteral(string? value) => value == null ? "null" : ToStringLiteral(value);
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
                    string.Join(",\n", pathEntry.Value.Select(propertyEntry => $"                [{ToStringLiteral(propertyEntry.Key)}] = {Literal(propertyEntry.Value) ?? "null"}")) +
                    "\n            }")) +
            "\n        }";
    }
    private static string ToCamel(string value) => string.IsNullOrEmpty(value) ? "value" : char.ToLowerInvariant(value[0]) + value[1..];
    private static string ToPropertyIdentifier(string path) => SanitizeIdentifier(Pascalize(path), "Value");
    private static string Pascalize(string value) => string.Concat(value.Split(['.', ':', '-', '/', ' ', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
    private static string SanitizeIdentifier(string value, string fallbackPrefix)
    {
        var cleaned = new string(value.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        if (string.IsNullOrEmpty(cleaned))
        {
            return fallbackPrefix;
        }

        return char.IsLetter(cleaned[0]) || cleaned[0] == '_'
            ? cleaned
            : fallbackPrefix + cleaned;
    }

    private static readonly string HelperCode = """
    private static readonly Dictionary<string, TMP_FontAsset> RuntimeTmpFontCache=new(StringComparer.OrdinalIgnoreCase);
    private static RectTransform CreateRect(string name, Transform? parent){var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();if(parent!=null)rect.SetParent(parent,false);rect.localScale=Vector3.one;rect.anchorMin=new Vector2(0f,1f);rect.anchorMax=new Vector2(0f,1f);rect.pivot=new Vector2(0f,1f);return rect;}
    private static RectTransform RequireRect(Transform root,string path){var target=root.Find(path);if(target==null||!target.TryGetComponent<RectTransform>(out var rect))throw new InvalidOperationException($"Required RectTransform '{path}' was not found beneath '{root.name}'.");return rect;}
    private static T RequireComponent<T>(Transform root,string path) where T : Component{var target=root.Find(path);if(target==null||!target.TryGetComponent<T>(out var component))throw new InvalidOperationException($"Required component '{typeof(T).Name}' at '{path}' was not found beneath '{root.name}'.");return component;}
    private static TextMeshProUGUI CreateText(string name, Transform? parent){var rect=CreateRect(name,parent);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();var fallbackFont=LoadFallbackTmpFontAsset();if(fallbackFont!=null)text.font=fallbackFont;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;text.richText=true;text.raycastTarget=false;text.margin=Vector4.zero;text.extraPadding=false;text.alignment=TextAlignmentOptions.TopLeft;return text;}
    private static Text CreateLegacyText(string name, Transform? parent){var rect=CreateRect(name,parent);rect.gameObject.AddComponent<CanvasRenderer>();var text=rect.gameObject.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.supportRichText=true;return text;}
    private static Image CreateImage(string name, Transform? parent){var rect=CreateRect(name,parent);rect.gameObject.AddComponent<CanvasRenderer>();return rect.gameObject.AddComponent<Image>();}
    private static Button CreateButton(string name, Transform? parent){var image=CreateImage(name,parent);var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;var label=CreateText("Label",image.transform);Stretch(RectOf(label));label.alignment=TextAlignmentOptions.Center;return button;}
    private static Toggle CreateToggle(string name, Transform? parent){var root=CreateRect(name,parent);var toggle=root.gameObject.AddComponent<Toggle>();var bg=CreateImage("Background",root);ConfigureRect(RectOf(bg),18f,18f,0f,0f,true);var check=CreateImage("Checkmark",RectOf(bg));Stretch(RectOf(check));var label=CreateText("Label",root);ConfigureRect(RectOf(label),null,18f,24f,0f,true);label.alignment=TextAlignmentOptions.MidlineLeft;toggle.targetGraphic=bg;toggle.graphic=check;return toggle;}
    private static Slider CreateSlider(string name, Transform? parent,bool interactable){var root=CreateRect(name,parent);var bg=CreateImage("Background",root);Stretch(RectOf(bg));var fillArea=CreateRect("Fill Area",root);Stretch(fillArea);var fill=CreateImage("Fill",fillArea);Stretch(RectOf(fill));var handleArea=CreateRect("Handle Slide Area",root);Stretch(handleArea);var handle=CreateImage("Handle",handleArea);ConfigureRect(RectOf(handle),12f,12f,0f,0f,true);var slider=root.gameObject.AddComponent<Slider>();slider.fillRect=RectOf(fill);slider.handleRect=RectOf(handle);slider.targetGraphic=handle;slider.interactable=interactable;return slider;}
    private static InputField CreateInput(string name, Transform? parent,bool multiline){var bg=CreateImage(name,parent);var text=CreateLegacyText("Text",bg.transform);Stretch(RectOf(text),6f,6f,6f,6f);text.alignment=multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft;var input=bg.gameObject.AddComponent<InputField>();input.textComponent=text;input.lineType=multiline?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;return input;}
    private static ScrollRect CreateScroll(string name, Transform? parent,out RectTransform content){var root=CreateImage(name,parent);var viewport=CreateImage("Viewport",root.transform);Stretch(RectOf(viewport));viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;content=CreateRect("Content",RectOf(viewport));Stretch(content);ApplyVerticalLayout(content,0f,0,0,0,0);var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.viewport=RectOf(viewport);scroll.content=content;scroll.horizontal=false;scroll.vertical=true;return scroll;}
    private static void ApplyHorizontalLayout(RectTransform rect,float spacing,int paddingLeft,int paddingRight,int paddingTop,int paddingBottom,string? alignmentPreset=null,bool childControlWidth=true,bool childControlHeight=true,bool childForceExpandWidth=false,bool childForceExpandHeight=false){var group=rect.gameObject.GetComponent<HorizontalLayoutGroup>()??rect.gameObject.AddComponent<HorizontalLayoutGroup>();group.spacing=spacing;group.padding=new RectOffset(paddingLeft,paddingRight,paddingTop,paddingBottom);group.childControlWidth=childControlWidth;group.childControlHeight=childControlHeight;group.childForceExpandWidth=childForceExpandWidth;group.childForceExpandHeight=childForceExpandHeight;ApplyLayoutAlignment(group,alignmentPreset);}
    private static void ApplyVerticalLayout(RectTransform rect,float spacing,int paddingLeft,int paddingRight,int paddingTop,int paddingBottom,string? alignmentPreset=null,bool childControlWidth=true,bool childControlHeight=true,bool childForceExpandWidth=false,bool childForceExpandHeight=false){var group=rect.gameObject.GetComponent<VerticalLayoutGroup>()??rect.gameObject.AddComponent<VerticalLayoutGroup>();group.spacing=spacing;group.padding=new RectOffset(paddingLeft,paddingRight,paddingTop,paddingBottom);group.childControlWidth=childControlWidth;group.childControlHeight=childControlHeight;group.childForceExpandWidth=childForceExpandWidth;group.childForceExpandHeight=childForceExpandHeight;ApplyLayoutAlignment(group,alignmentPreset);}
    private static void ApplyLayoutSizing(RectTransform rect,bool ignoreLayout,float? preferredWidth,float? preferredHeight,float? flexibleWidth,float? flexibleHeight){var element=rect.gameObject.GetComponent<LayoutElement>()??rect.gameObject.AddComponent<LayoutElement>();element.ignoreLayout=ignoreLayout;element.preferredWidth=preferredWidth??-1f;element.preferredHeight=preferredHeight??-1f;element.flexibleWidth=flexibleWidth??-1f;element.flexibleHeight=flexibleHeight??-1f;}
    private static void ApplyContentSizeFit(RectTransform rect,bool horizontal,bool vertical){var fitter=rect.gameObject.GetComponent<ContentSizeFitter>()??rect.gameObject.AddComponent<ContentSizeFitter>();fitter.horizontalFit=horizontal?ContentSizeFitter.FitMode.PreferredSize:ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=vertical?ContentSizeFitter.FitMode.PreferredSize:ContentSizeFitter.FitMode.Unconstrained;}
    private static void ConfigureRect(RectTransform rect,float? width,float? height,float? left,float? top,bool absolute){if(absolute){rect.anchorMin=new Vector2(0f,1f);rect.anchorMax=new Vector2(0f,1f);rect.pivot=new Vector2(0f,1f);rect.anchoredPosition=new Vector2(left??0f,-(top??0f));}if(width.HasValue)rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width.Value);if(height.HasValue)rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height.Value);}
    private static void ApplyRectAnchorPreset(RectTransform rect,string preset){switch(NormalizeRectPreset(preset)){case "top-left":case "start":rect.anchorMin=new Vector2(0f,1f);rect.anchorMax=new Vector2(0f,1f);break;case "top-center":rect.anchorMin=new Vector2(0.5f,1f);rect.anchorMax=new Vector2(0.5f,1f);break;case "top-right":case "end":rect.anchorMin=new Vector2(1f,1f);rect.anchorMax=new Vector2(1f,1f);break;case "center":case "middle-center":rect.anchorMin=new Vector2(0.5f,0.5f);rect.anchorMax=new Vector2(0.5f,0.5f);break;case "stretch":rect.anchorMin=new Vector2(0f,0f);rect.anchorMax=new Vector2(1f,1f);break;case "stretch-horizontal":rect.anchorMin=new Vector2(0f,1f);rect.anchorMax=new Vector2(1f,1f);break;case "stretch-vertical":rect.anchorMin=new Vector2(0f,0f);rect.anchorMax=new Vector2(0f,1f);break;}}
    private static void ApplyRectPivotPreset(RectTransform rect,string preset){switch(NormalizeRectPreset(preset)){case "top-left":case "start":rect.pivot=new Vector2(0f,1f);break;case "top-center":rect.pivot=new Vector2(0.5f,1f);break;case "top-right":case "end":rect.pivot=new Vector2(1f,1f);break;case "center":case "middle-center":rect.pivot=new Vector2(0.5f,0.5f);break;case "bottom-left":rect.pivot=new Vector2(0f,0f);break;case "bottom-center":rect.pivot=new Vector2(0.5f,0f);break;case "bottom-right":rect.pivot=new Vector2(1f,0f);break;}}
    private static void ApplyRectTransformMode(RectTransform rect,string mode){switch(NormalizeRectPreset(mode)){case "stretch-parent":case "stretch":Stretch(rect);break;case "absolute-overlay":rect.anchorMin=new Vector2(0f,1f);rect.anchorMax=new Vector2(0f,1f);rect.pivot=new Vector2(0f,1f);break;case "top-left":ApplyRectAnchorPreset(rect,"top-left");ApplyRectPivotPreset(rect,"top-left");break;case "center":ApplyRectAnchorPreset(rect,"center");ApplyRectPivotPreset(rect,"center");break;}}
    private static void ApplyEdgeInsetPolicy(RectTransform rect,string policy){switch(NormalizeRectPreset(policy)){case "match-parent":Stretch(rect);break;case "zero-offsets":rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;break;}}
    private static void ApplyLayoutAlignment(HorizontalOrVerticalLayoutGroup group,string? alignmentPreset){switch(NormalizeRectPreset(alignmentPreset)){case "top-left":case "start":group.childAlignment=TextAnchor.UpperLeft;break;case "top-center":group.childAlignment=TextAnchor.UpperCenter;break;case "top-right":group.childAlignment=TextAnchor.UpperRight;break;case "middle-left":group.childAlignment=TextAnchor.MiddleLeft;break;case "center":case "middle-center":group.childAlignment=TextAnchor.MiddleCenter;break;case "middle-right":group.childAlignment=TextAnchor.MiddleRight;break;case "bottom-left":group.childAlignment=TextAnchor.LowerLeft;break;case "bottom-center":group.childAlignment=TextAnchor.LowerCenter;break;case "bottom-right":case "end":group.childAlignment=TextAnchor.LowerRight;break;}}
    private static string NormalizeRectPreset(string? value)=>string.IsNullOrWhiteSpace(value)?string.Empty:value.Trim().ToLowerInvariant();
    private static void Stretch(RectTransform rect,float left=0f,float right=0f,float top=0f,float bottom=0f){rect.anchorMin=new Vector2(0f,0f);rect.anchorMax=new Vector2(1f,1f);rect.pivot=new Vector2(0.5f,0.5f);rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);}
    private static void ApplyStyle(Component component,string? fg,string? bg,string? fontFamily,int? fontSize,string? borderColor,float? borderWidth,bool treatAsIcon){if(!string.IsNullOrWhiteSpace(bg))EnsureImage(component.gameObject).color=ParseColor(bg,Color.white);if(!string.IsNullOrWhiteSpace(borderColor)&&borderWidth.HasValue&&borderWidth.Value>0f)ApplyBorder(component.gameObject,ParseColor(borderColor,Color.white),borderWidth.Value);if(component is TMP_Text tmp){ApplyTmpTextStyle(tmp,fg,fontFamily,fontSize,treatAsIcon);return;}if(component is Text text){if(!string.IsNullOrWhiteSpace(fg))text.color=ParseColor(fg,text.color);if(!string.IsNullOrWhiteSpace(fontFamily)&&TryFont(fontFamily,out var font))text.font=font;if(fontSize.HasValue)text.fontSize=fontSize.Value;if(treatAsIcon){text.alignment=TextAnchor.MiddleCenter;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;}return;}if(component is Button button){if(TryTmpLabel(button.gameObject,out var tmpLabel)){ApplyTmpTextStyle(tmpLabel,fg,fontFamily,fontSize,false);return;}if(TryLegacyLabel(button.gameObject,out var legacyLabel)){if(!string.IsNullOrWhiteSpace(fg))legacyLabel.color=ParseColor(fg,legacyLabel.color);if(!string.IsNullOrWhiteSpace(fontFamily)&&TryFont(fontFamily,out var legacyFont))legacyLabel.font=legacyFont;if(fontSize.HasValue)legacyLabel.fontSize=fontSize.Value;}return;}if(component is Toggle toggle){if(TryTmpLabel(toggle.gameObject,out var tmpToggleLabel)){ApplyTmpTextStyle(tmpToggleLabel,fg,fontFamily,fontSize,false);return;}if(TryLegacyLabel(toggle.gameObject,out var legacyToggleLabel)){if(!string.IsNullOrWhiteSpace(fg))legacyToggleLabel.color=ParseColor(fg,legacyToggleLabel.color);if(!string.IsNullOrWhiteSpace(fontFamily)&&TryFont(fontFamily,out var legacyFont))legacyToggleLabel.font=legacyFont;if(fontSize.HasValue)legacyToggleLabel.fontSize=fontSize.Value;}return;}if(component is InputField input&&input.textComponent!=null){if(!string.IsNullOrWhiteSpace(fg))input.textComponent.color=ParseColor(fg,input.textComponent.color);if(!string.IsNullOrWhiteSpace(fontFamily)&&TryFont(fontFamily,out var font))input.textComponent.font=font;if(fontSize.HasValue)input.textComponent.fontSize=fontSize.Value;}}
    private static void ApplyIconMetrics(Component component,float boxWidth,float boxHeight,float baselineOffset,bool opticalCentering,string sizeMode,float explicitFontSize){var iconSize=explicitFontSize>0f?explicitFontSize:string.Equals(sizeMode,"match-height",StringComparison.OrdinalIgnoreCase)?Mathf.Max(1f,boxHeight):Mathf.Max(1f,Mathf.Min(boxWidth,boxHeight));if(component is TMP_Text tmp){tmp.fontSize=Mathf.RoundToInt(iconSize);tmp.alignment=opticalCentering?TextAlignmentOptions.Center:TextAlignmentOptions.Top;tmp.textWrappingMode=TextWrappingModes.NoWrap;tmp.overflowMode=TextOverflowModes.Overflow;var tmpRect=RectOf(tmp);if(opticalCentering&&Mathf.Approximately(baselineOffset,0f)&&boxHeight>iconSize){baselineOffset=-1f;}tmpRect.anchoredPosition=new Vector2(tmpRect.anchoredPosition.x,tmpRect.anchoredPosition.y+baselineOffset);return;}if(component is not Text text)return;text.fontSize=Mathf.RoundToInt(iconSize);text.alignment=opticalCentering?TextAnchor.MiddleCenter:TextAnchor.UpperCenter;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;var rect=RectOf(text);if(opticalCentering&&Mathf.Approximately(baselineOffset,0f)&&boxHeight>iconSize){baselineOffset=-1f;}rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,rect.anchoredPosition.y+baselineOffset);}
    private static void ApplyTextMetrics(Component component,float? lineSpacing,bool wrapText){if(component is TMP_Text tmp){if(lineSpacing.HasValue)tmp.lineSpacing=lineSpacing.Value;tmp.textWrappingMode=wrapText?TextWrappingModes.Normal:TextWrappingModes.NoWrap;tmp.overflowMode=TextOverflowModes.Overflow;return;}if(component is Text text){if(lineSpacing.HasValue)text.lineSpacing=lineSpacing.Value;text.horizontalOverflow=wrapText?HorizontalWrapMode.Wrap:HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;return;}if(component is Button button){if(TryTmpLabel(button.gameObject,out var tmpLabel)){if(lineSpacing.HasValue)tmpLabel.lineSpacing=lineSpacing.Value;tmpLabel.textWrappingMode=wrapText?TextWrappingModes.Normal:TextWrappingModes.NoWrap;tmpLabel.overflowMode=TextOverflowModes.Overflow;return;}if(TryLegacyLabel(button.gameObject,out var label)){if(lineSpacing.HasValue)label.lineSpacing=lineSpacing.Value;label.horizontalOverflow=wrapText?HorizontalWrapMode.Wrap:HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;return;}}if(component is Toggle toggle){if(TryTmpLabel(toggle.gameObject,out var tmpToggleLabel)){if(lineSpacing.HasValue)tmpToggleLabel.lineSpacing=lineSpacing.Value;tmpToggleLabel.textWrappingMode=wrapText?TextWrappingModes.Normal:TextWrappingModes.NoWrap;tmpToggleLabel.overflowMode=TextOverflowModes.Overflow;return;}if(TryLegacyLabel(toggle.gameObject,out var toggleLabel)){if(lineSpacing.HasValue)toggleLabel.lineSpacing=lineSpacing.Value;toggleLabel.horizontalOverflow=wrapText?HorizontalWrapMode.Wrap:HorizontalWrapMode.Overflow;toggleLabel.verticalOverflow=VerticalWrapMode.Overflow;return;}}if(component is InputField input&&input.textComponent!=null){if(lineSpacing.HasValue)input.textComponent.lineSpacing=lineSpacing.Value;input.textComponent.horizontalOverflow=wrapText?HorizontalWrapMode.Wrap:HorizontalWrapMode.Overflow;input.textComponent.verticalOverflow=VerticalWrapMode.Overflow;}}
    private static void ApplyEnabled(Component component,bool enabled){if(component is Selectable selectable){selectable.interactable=enabled;return;}component.gameObject.SetActive(enabled);}
    private static void SetButtonText(Button button,string? value){if(TryTmpLabel(button.gameObject,out var tmpLabel)){tmpLabel.text=value??string.Empty;return;}if(TryLegacyLabel(button.gameObject,out var label))label.text=value??string.Empty;}
    private static void SetToggleText(Toggle toggle,string? value){if(TryTmpLabel(toggle.gameObject,out var tmpLabel)){tmpLabel.text=value??string.Empty;return;}if(TryLegacyLabel(toggle.gameObject,out var label))label.text=value??string.Empty;}
    private static void SetImage(Image image,string? path){image.sprite=string.IsNullOrWhiteSpace(path)?null:Resources.Load<Sprite>(path);}
    private static bool TryTmpLabel(GameObject go,out TMP_Text label){label=go.GetComponentInChildren<TMP_Text>(true);return label!=null;}
    private static bool TryLegacyLabel(GameObject go,out Text label){label=go.GetComponentInChildren<Text>(true);return label!=null;}
    private static void ApplyTmpTextStyle(TMP_Text text,string? fg,string? fontFamily,int? fontSize,bool treatAsIcon){if(!string.IsNullOrWhiteSpace(fg))text.color=ParseColor(fg,text.color);if(!string.IsNullOrWhiteSpace(fontFamily)&&TryTmpFont(fontFamily,out var fontAsset))text.font=fontAsset;if(fontSize.HasValue)text.fontSize=fontSize.Value;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;text.extraPadding=false;text.margin=Vector4.zero;if(treatAsIcon){text.alignment=TextAlignmentOptions.Center;}}
    private static bool TryTmpFont(string familyName,out TMP_FontAsset fontAsset){var resourcePath=familyName switch{"Press Start 2P"=>"BoomHudFonts/PressStart2P-Regular","lucide"=>"BoomHudFonts/lucide",_=>familyName};fontAsset=Resources.Load<TMP_FontAsset>(resourcePath)??Resources.Load<TMP_FontAsset>(familyName)??TryCreateTmpFontAsset(resourcePath)??TryCreateTmpFontAsset(familyName)??LoadFallbackTmpFontAsset();return fontAsset!=null;}
    private static TMP_FontAsset? LoadFallbackTmpFontAsset()=>Resources.Load<TMP_FontAsset>("BoomHudFonts/PressStart2P-Regular")??TryCreateTmpFontAsset("BoomHudFonts/PressStart2P-Regular")??Resources.Load<TMP_FontAsset>("BoomHudFonts/lucide")??TryCreateTmpFontAsset("BoomHudFonts/lucide")??TryGetDefaultTmpFontAsset();
    private static TMP_FontAsset? TryCreateTmpFontAsset(string resourcePath){if(string.IsNullOrWhiteSpace(resourcePath))return null;if(RuntimeTmpFontCache.TryGetValue(resourcePath,out var cached))return cached;var sourceFont=Resources.Load<Font>(resourcePath);if(sourceFont==null)return null;try{var created=TMP_FontAsset.CreateFontAsset(sourceFont);RuntimeTmpFontCache[resourcePath]=created;return created;}catch{return null;}}
    private static TMP_FontAsset? TryGetDefaultTmpFontAsset(){try{return TMP_Settings.defaultFontAsset;}catch{return null;}}
    private static bool TryFont(string familyName,out Font font){var resourcePath=familyName switch{"Press Start 2P"=>"BoomHudFonts/PressStart2P-Regular","lucide"=>"BoomHudFonts/lucide",_=>familyName};font=Resources.Load<Font>(resourcePath)??Resources.Load<Font>(familyName);return font!=null;}
    private static void ApplyBorder(GameObject go,Color color,float width){if(width<=0f)return;if(go.TryGetComponent<Outline>(out var outline))outline.enabled=false;if(!go.TryGetComponent<RectTransform>(out var rect))return;var borderRoot=go.transform.Find("__Border") as RectTransform??CreateRect("__Border",go.transform);borderRoot.SetParent(go.transform,false);ApplyLayoutSizing(borderRoot,true,null,null,null,null);Stretch(borderRoot);borderRoot.SetAsLastSibling();ConfigureBorderSegment(EnsureBorderSegment(borderRoot,"Top",color),new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(0.5f,1f),new Vector2(0f,0f),new Vector2(0f,width));ConfigureBorderSegment(EnsureBorderSegment(borderRoot,"Bottom",color),new Vector2(0f,0f),new Vector2(1f,0f),new Vector2(0.5f,0f),new Vector2(0f,0f),new Vector2(0f,width));ConfigureBorderSegment(EnsureBorderSegment(borderRoot,"Left",color),new Vector2(0f,0f),new Vector2(0f,1f),new Vector2(0f,0.5f),new Vector2(0f,0f),new Vector2(width,0f));ConfigureBorderSegment(EnsureBorderSegment(borderRoot,"Right",color),new Vector2(1f,0f),new Vector2(1f,1f),new Vector2(1f,0.5f),new Vector2(0f,0f),new Vector2(width,0f));}
    private static RectTransform EnsureBorderSegment(RectTransform parent,string name,Color color){var existing=parent.Find(name);if(existing!=null&&existing.TryGetComponent<Image>(out var image)){image.color=color;image.raycastTarget=false;return RectOf(image);}var created=CreateImage(name,parent);created.color=color;created.raycastTarget=false;return RectOf(created);}
    private static void ConfigureBorderSegment(RectTransform rect,Vector2 anchorMin,Vector2 anchorMax,Vector2 pivot,Vector2 anchoredPosition,Vector2 sizeDelta){rect.anchorMin=anchorMin;rect.anchorMax=anchorMax;rect.pivot=pivot;rect.anchoredPosition=anchoredPosition;rect.sizeDelta=sizeDelta;}
    private static Image EnsureImage(GameObject go){var image=go.GetComponent<Image>();if(image==null){if(go.GetComponent<CanvasRenderer>()==null)go.AddComponent<CanvasRenderer>();image=go.AddComponent<Image>();}return image;}
    private static RectTransform RectOf(Component component)=>component.GetComponent<RectTransform>();
    private static RectTransform RectOf(RectTransform rect)=>rect;
    private static string ResolveIconText(string? value)=>value switch{"cross"=>"\uE1E5","flame"=>"\uE0D2","flask-conical"=>"\uE0D5","moon"=>"\uE11E","shield"=>"\uE158","sparkles"=>"\uE412","sword"=>"\uE2B3","swords"=>"\uE2B4","wand"=>"\uE246","wand-2"=>"\uE357","wand-sparkles"=>"\uE357",_=>value??string.Empty};
    private static bool AsBool(object? value,bool fallback)=>value is bool b?b:value is string s&&bool.TryParse(s,out var parsed)?parsed:fallback;
    private static float AsFloat(object? value)=>value switch{float f=>f,double d=>(float)d,int i=>i,long l=>l,decimal m=>(float)m,string s when float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var parsed)=>parsed,_=>0f};
    private static string AsString(object? value)=>value?.ToString()??string.Empty;
    private static Color ParseColor(string? value,Color fallback)=>!string.IsNullOrWhiteSpace(value)&&ColorUtility.TryParseHtmlString(value,out var color)?color:fallback;
""";

    private sealed class Planner
    {
        public static PlanDocument Create(HudDocument document, List<Diagnostic> diagnostics, GeneratorRuleSet? ruleSet, VisualToUGuiPlan? visualPlan, UGuiBuildProgram? buildProgramOverride)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var props = new Dictionary<string, ViewModelProperty>(StringComparer.Ordinal);
            var ruleResolver = new RuleResolver(ruleSet, "ugui");
            var buildProgramResolver = UGuiBuildProgramOverrideResolver.Create(buildProgramOverride);
            var root = CreateNode(document.Root, document.Name + "Root", document, diagnostics, names, props, ruleResolver, buildProgramResolver, document.Name, ComponentInstanceOverrideSupport.RootPath, parent: null, grandparent: null, siblingIndex: 0, visualPlan, forceRoot: true);
            return new PlanDocument
            {
                Root = root,
                Nodes = Flatten(root).Where(static n => n.FieldName != "Root").ToList(),
                Properties = props.Values.OrderBy(static p => p.Identifier, StringComparer.Ordinal).ToList()
            };
        }

        private static PlannedNode CreateNode(ComponentNode source, string fallbackName, HudDocument document, List<Diagnostic> diagnostics, HashSet<string> names, Dictionary<string, ViewModelProperty> props, RuleResolver ruleResolver, UGuiBuildProgramOverrideResolver buildProgramResolver, string documentName, string relativePath, ComponentNode? parent, ComponentNode? grandparent, int siblingIndex, VisualToUGuiPlan? visualPlan, bool forceRoot = false)
        {
            Track(source, props);
            var baseName = forceRoot ? document.Name + "Root" : Pascal(source.Id ?? fallbackName);
            var fieldName = forceRoot ? "Root" : Unique(baseName, names);
            var visualResolved = visualPlan?.Resolve(source.Id, documentName, relativePath);
            var policy = buildProgramResolver.Apply(
                ruleResolver.Resolve(
                    documentName,
                    source,
                    new RuleSelectionContext(parent, grandparent, siblingIndex),
                    includeMetricProfiles: visualResolved?.MetricProfile == null),
                visualResolved?.Node?.StableId);
            var fieldType = ResolveFieldType(source, document, diagnostics, policy, out var componentView);
            var referencedComponent = source.ComponentRefId != null && document.Components.TryGetValue(source.ComponentRefId, out var componentDefinition)
                ? componentDefinition
                : null;
            var referencedRoot = componentView != null && referencedComponent != null
                ? CreateReferencedComponentNode(referencedComponent, document, diagnostics, ruleResolver, buildProgramResolver, visualPlan)
                : null;
            var children = componentView == null
                ? source.Children.Select((child, index) => CreateNode(child, child.Id ?? child.Type + (index + 1).ToString(CultureInfo.InvariantCulture), document, diagnostics, names, props, ruleResolver, buildProgramResolver, documentName, ComponentInstanceOverrideSupport.ChildPath(relativePath, index), source, parent, index, visualPlan)).ToList()
                : [];
            return new PlannedNode { Name = forceRoot ? document.Name + "Root" : fieldName, RelativePath = relativePath, FieldName = fieldName, FieldType = fieldType, ComponentView = componentView, ReferencedComponent = referencedComponent, ReferencedRoot = referencedRoot, Source = source, Policy = policy, VisualNode = visualResolved?.Node, MetricProfile = visualResolved?.MetricProfile, Children = children };
        }

        private static PlannedNode CreateReferencedComponentNode(HudComponentDefinition component, HudDocument document, List<Diagnostic> diagnostics, RuleResolver ruleResolver, UGuiBuildProgramOverrideResolver buildProgramResolver, VisualToUGuiPlan? visualPlan)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var props = new Dictionary<string, ViewModelProperty>(StringComparer.Ordinal);
            return CreateNode(
                component.Root,
                component.Name + "Root",
                document,
                diagnostics,
                names,
                props,
                ruleResolver,
                buildProgramResolver,
                component.Name,
                ComponentInstanceOverrideSupport.RootPath,
                parent: null,
                grandparent: null,
                siblingIndex: 0,
                visualPlan,
                forceRoot: true);
        }

        private static void Track(ComponentNode node, Dictionary<string, ViewModelProperty> props)
        {
            if (node.Visible.IsBound) Add(props, node.Visible.BindingPath!);
            if (node.Enabled.IsBound) Add(props, node.Enabled.BindingPath!);
            foreach (var binding in node.Bindings) Add(props, binding.Path);
            foreach (var property in node.Properties.Values.Where(static p => p.IsBound)) Add(props, property.BindingPath!);
        }

        private static void Add(Dictionary<string, ViewModelProperty> props, string path)
        {
            if (!props.ContainsKey(path)) props[path] = new ViewModelProperty { Path = path, Identifier = ToPropertyIdentifier(path) };
        }

        private static string ResolveFieldType(ComponentNode source, HudDocument document, List<Diagnostic> diagnostics, ResolvedGeneratorPolicy policy, out string? componentView)
        {
            componentView = null;
            if (source.ComponentRefId != null && source.Children.Count == 0 && document.Components.TryGetValue(source.ComponentRefId, out var component))
            {
                componentView = component.Name;
                return "RectTransform";
            }

            if (!string.IsNullOrWhiteSpace(policy.ControlType))
            {
                if (TryMapControlOverride(policy.ControlType!, out var overrideFieldType))
                {
                    return overrideFieldType;
                }

                diagnostics.Add(Diagnostic.Warning(
                    $"Unity uGUI control override '{policy.ControlType}' is not recognized; using default mapping.",
                    source.Id,
                    "BHUG1003"));
            }

            return source.Type switch
            {
                ComponentType.Label or ComponentType.Badge or ComponentType.Icon => "TextMeshProUGUI",
                ComponentType.Button or ComponentType.MenuItem => "Button",
                ComponentType.TextInput or ComponentType.TextArea => "InputField",
                ComponentType.Checkbox or ComponentType.RadioButton => "Toggle",
                ComponentType.ProgressBar or ComponentType.Slider => "Slider",
                ComponentType.Image => "Image",
                ComponentType.ScrollView or ComponentType.ListBox or ComponentType.ListView or ComponentType.TreeView or ComponentType.DataGrid or ComponentType.Timeline => "ScrollRect",
                _ => "RectTransform"
            };
        }

        private static bool TryMapControlOverride(string controlType, out string fieldType)
        {
            fieldType = controlType.Trim() switch
            {
                "Label" or "Text" => "TextMeshProUGUI",
                "Button" => "Button",
                "TextField" or "InputField" => "InputField",
                "Toggle" => "Toggle",
                "ProgressBar" or "Slider" => "Slider",
                "Image" => "Image",
                "ScrollView" or "ScrollRect" => "ScrollRect",
                "Container" or "RectTransform" => "RectTransform",
                _ => string.Empty
            };

            return fieldType.Length > 0;
        }

        private static string Unique(string baseName, HashSet<string> names)
        {
            var candidate = baseName;
            var suffix = 2;
            while (!names.Add(candidate))
            {
                candidate = baseName + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            return candidate;
        }

        private static string Pascal(string value)
            => SanitizeIdentifier(Pascalize(value), "Node");

        private static IEnumerable<PlannedNode> Flatten(PlannedNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var descendant in Flatten(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private sealed record PlanDocument
    {
        public required PlannedNode Root { get; init; }
        public required IReadOnlyList<PlannedNode> Nodes { get; init; }
        public required IReadOnlyList<ViewModelProperty> Properties { get; init; }
    }

    private sealed record PlannedNode
    {
        public required string Name { get; init; }
        public required string RelativePath { get; init; }
        public required string FieldName { get; init; }
        public required string FieldType { get; init; }
        public string? ComponentView { get; init; }
        public HudComponentDefinition? ReferencedComponent { get; init; }
        public PlannedNode? ReferencedRoot { get; init; }
        public required ComponentNode Source { get; init; }
        public required ResolvedGeneratorPolicy Policy { get; init; }
        public VisualNode? VisualNode { get; init; }
        public MetricProfileDefinition? MetricProfile { get; init; }
        public required List<PlannedNode> Children { get; init; }
    }

    private sealed record ViewModelProperty
    {
        public required string Path { get; init; }
        public required string Identifier { get; init; }
    }

    private sealed class UGuiBuildProgramOverrideResolver
    {
        private readonly IReadOnlyDictionary<string, GeneratorRuleAction> _actionsByStableId;

        private UGuiBuildProgramOverrideResolver(IReadOnlyDictionary<string, GeneratorRuleAction> actionsByStableId)
        {
            _actionsByStableId = actionsByStableId;
        }

        public static UGuiBuildProgramOverrideResolver Create(UGuiBuildProgram? buildProgram)
        {
            if (buildProgram == null)
            {
                return new UGuiBuildProgramOverrideResolver(new Dictionary<string, GeneratorRuleAction>(StringComparer.Ordinal));
            }

            var catalogs = buildProgram.CandidateCatalogs.ToDictionary(static catalog => catalog.StableId, StringComparer.Ordinal);
            var actions = new Dictionary<string, GeneratorRuleAction>(StringComparer.Ordinal);
            foreach (var selection in buildProgram.AcceptedCandidates)
            {
                if (!catalogs.TryGetValue(selection.StableId, out var catalog))
                {
                    continue;
                }

                var candidate = catalog.Candidates.FirstOrDefault(entry => string.Equals(entry.CandidateId, selection.CandidateId, StringComparison.Ordinal));
                if (candidate == null)
                {
                    continue;
                }

                actions[selection.StableId] = candidate.Action;
                foreach (var descendant in candidate.DescendantActions)
                {
                    if (string.IsNullOrWhiteSpace(descendant.StableId))
                    {
                        continue;
                    }

                    actions[descendant.StableId] = descendant.Action;
                }
            }

            return new UGuiBuildProgramOverrideResolver(actions);
        }

        public ResolvedGeneratorPolicy Apply(ResolvedGeneratorPolicy policy, string? stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId) || !_actionsByStableId.TryGetValue(stableId, out var action))
            {
                return policy;
            }

            return policy.Apply(action);
        }
    }
}
