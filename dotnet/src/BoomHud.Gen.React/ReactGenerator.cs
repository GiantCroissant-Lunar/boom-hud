using System.Globalization;
using System.Text;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Gen.React;

public sealed class ReactGenerator : IBackendGenerator
{
    public string TargetFramework => "React";
    public ICapabilityManifest Capabilities => ReactCapabilities.Instance;

    public GenerationResult Generate(HudDocument document, GenerationOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var files = new List<GeneratedFile>();

        if (options.EmitCompose)
        {
            diagnostics.Add(Diagnostic.Warning("React compose helpers are not implemented yet.", code: "BHR2000"));
        }

        try
        {
            foreach (var component in document.Components.Values)
            {
                EmitArtifacts(new HudDocument
                {
                    Name = component.Name,
                    Metadata = component.Metadata,
                    Root = component.Root,
                    Styles = document.Styles,
                    Components = document.Components
                }, options, diagnostics, files);
            }

            EmitArtifacts(document, options, diagnostics, files);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Diagnostic.Error($"Generation failed: {ex.Message}"));
        }

        return new GenerationResult { Files = files, Diagnostics = diagnostics };
    }

    private static void EmitArtifacts(HudDocument document, GenerationOptions options, List<Diagnostic> diagnostics, List<GeneratedFile> files)
    {
        var props = CollectProps(document.Root).OrderBy(static x => x, StringComparer.Ordinal).ToList();
        files.Add(new GeneratedFile { Path = $"{document.Name}View.tsx", Content = GenerateTsx(document, props, diagnostics), Type = GeneratedFileType.SourceCode });
        if (options.EmitViewModelInterfaces)
        {
            files.Add(new GeneratedFile { Path = $"I{document.Name}ViewModel.g.ts", Content = GenerateContract(document.Name, props), Type = GeneratedFileType.SourceCode });
        }
    }

    private static string GenerateTsx(HudDocument document, IReadOnlyList<string> props, List<Diagnostic> diagnostics)
    {
        var imports = CollectRefs(document.Root, document.Components, diagnostics, document.Name);
        var builder = new StringBuilder();
        builder.AppendLine("import React from 'react';");
        foreach (var import in imports)
        {
            builder.Append("import { ").Append(import).Append("View } from './").Append(import).AppendLine("View';");
        }

        if (imports.Count > 0) builder.AppendLine();
        builder.Append(GenerateContract(document.Name, props));
        builder.AppendLine();
        builder.AppendLine("type BoomHudMotionProperty = 'opacity' | 'positionX' | 'positionY' | 'positionZ' | 'scaleX' | 'scaleY' | 'scaleZ' | 'rotation' | 'rotationX' | 'rotationY' | 'width' | 'height' | 'visibility' | 'text' | 'spriteFrame' | 'color';");
        builder.AppendLine("type BoomHudMotionScalar = number | boolean | string;");
        builder.AppendLine("type BoomHudMotionTargetState = Partial<Record<BoomHudMotionProperty, BoomHudMotionScalar>>;");
        builder.AppendLine("type BoomHudMotionTargets = Record<string, BoomHudMotionTargetState>;");
        builder.AppendLine("const asBool = (value: unknown, fallback = true) => typeof value === 'boolean' ? value : fallback;");
        builder.AppendLine("const asText = (value: unknown, fallback = '') => value == null ? fallback : String(value);");
        builder.AppendLine("const resolveMotionId = (scope: string | undefined, id?: string) => !id ? undefined : scope ? `${scope}/${id}` : id;");
        builder.AppendLine("const resolveIconText = (value: unknown, familyName?: string) => {");
        builder.AppendLine("  const text = asText(value, '');");
        builder.AppendLine("  if (!text || familyName?.trim().toLowerCase() !== 'lucide') return text;");
        builder.AppendLine("  switch (text) {");
        builder.AppendLine("    case 'sword': return '†';");
        builder.AppendLine("    case 'swords': return '⚔';");
        builder.AppendLine("    case 'sparkles': return '✦';");
        builder.AppendLine("    case 'wand-sparkles': return '✦';");
        builder.AppendLine("    case 'shield': return '⛨';");
        builder.AppendLine("    case 'flask-conical': return '⚗';");
        builder.AppendLine("    case 'flame': return '✹';");
        builder.AppendLine("    case 'moon': return '☾';");
        builder.AppendLine("    case 'cross': return '✚';");
        builder.AppendLine("    default: return text;");
        builder.AppendLine("  }");
        builder.AppendLine("};");
        builder.AppendLine("const formatValue = (value: unknown, format?: string, fallback = '') => !format ? asText(value, fallback) : format.replace(/\\{0(?:\\:[^}]*)?\\}/g, asText(value, fallback));");
        builder.AppendLine("const clampPercent = (value: unknown) => `${Math.max(0, Math.min(100, typeof value === 'number' ? value : 0))}%`;");
        builder.AppendLine("const asMotionNumber = (value: BoomHudMotionScalar | undefined) => typeof value === 'number' ? value : undefined;");
        builder.AppendLine("const asMotionText = (value: BoomHudMotionScalar | undefined) => typeof value === 'string' ? value : undefined;");
        builder.AppendLine("const getMotionTarget = (targets: BoomHudMotionTargets | undefined, id?: string) => id ? targets?.[id] : undefined;");
        builder.AppendLine("const getMotionText = (targets: BoomHudMotionTargets | undefined, id?: string) => asMotionText(getMotionTarget(targets, id)?.text);");
        builder.AppendLine("const getMotionSpriteFrame = (targets: BoomHudMotionTargets | undefined, id?: string) => asMotionText(getMotionTarget(targets, id)?.spriteFrame);");
        builder.AppendLine("const getMotionStyle = (targets: BoomHudMotionTargets | undefined, id?: string): React.CSSProperties => {");
        builder.AppendLine("  const target = getMotionTarget(targets, id);");
        builder.AppendLine("  if (!target) return {};");
        builder.AppendLine("  const style: React.CSSProperties = {};");
        builder.AppendLine("  const transform: string[] = [];");
        builder.AppendLine("  const opacity = asMotionNumber(target.opacity);");
        builder.AppendLine("  if (opacity !== undefined) style.opacity = opacity;");
        builder.AppendLine("  const width = asMotionNumber(target.width);");
        builder.AppendLine("  if (width !== undefined) style.width = width;");
        builder.AppendLine("  const height = asMotionNumber(target.height);");
        builder.AppendLine("  if (height !== undefined) style.height = height;");
        builder.AppendLine("  const color = asMotionText(target.color);");
        builder.AppendLine("  if (color !== undefined) style.color = color;");
        builder.AppendLine("  if (typeof target.visibility === 'boolean') style.visibility = target.visibility ? 'visible' : 'hidden';");
        builder.AppendLine("  if (typeof target.visibility === 'string') style.visibility = target.visibility === 'hidden' || target.visibility === 'collapse' ? target.visibility : 'visible';");
        builder.AppendLine("  const positionX = asMotionNumber(target.positionX) ?? 0;");
        builder.AppendLine("  const positionY = asMotionNumber(target.positionY) ?? 0;");
        builder.AppendLine("  const positionZ = asMotionNumber(target.positionZ) ?? 0;");
        builder.AppendLine("  if (positionX !== 0 || positionY !== 0 || positionZ !== 0) transform.push(`translate3d(${positionX}px, ${positionY}px, ${positionZ}px)`);");
        builder.AppendLine("  const scaleX = asMotionNumber(target.scaleX);");
        builder.AppendLine("  if (scaleX !== undefined) transform.push(`scaleX(${scaleX})`);");
        builder.AppendLine("  const scaleY = asMotionNumber(target.scaleY);");
        builder.AppendLine("  if (scaleY !== undefined) transform.push(`scaleY(${scaleY})`);");
        builder.AppendLine("  const scaleZ = asMotionNumber(target.scaleZ);");
        builder.AppendLine("  if (scaleZ !== undefined) transform.push(`scale3d(1, 1, ${scaleZ})`);");
        builder.AppendLine("  const rotation = asMotionNumber(target.rotation);");
        builder.AppendLine("  if (rotation !== undefined) transform.push(`rotate(${rotation}deg)`);");
        builder.AppendLine("  const rotationX = asMotionNumber(target.rotationX);");
        builder.AppendLine("  if (rotationX !== undefined) transform.push(`rotateX(${rotationX}deg)`);");
        builder.AppendLine("  const rotationY = asMotionNumber(target.rotationY);");
        builder.AppendLine("  if (rotationY !== undefined) transform.push(`rotateY(${rotationY}deg)`);");
        builder.AppendLine("  if (transform.length > 0) style.transform = transform.join(' ');");
        builder.AppendLine("  return style;");
        builder.AppendLine("};");
        builder.AppendLine();
        builder.Append("export function ").Append(document.Name).Append("View(props: ").Append(document.Name).AppendLine("ViewModel): React.JSX.Element {");
        builder.AppendLine("  return (");
        builder.Append(RenderNode(document.Root, 2, null, document.Components, diagnostics));
        builder.AppendLine("  );");
        builder.AppendLine("}");
        builder.Append("export default ").Append(document.Name).AppendLine("View;");
        return builder.ToString();
    }

    private static string GenerateContract(string name, IReadOnlyList<string> props)
    {
        var builder = new StringBuilder();
        builder.Append("export interface ").Append(name).AppendLine("ViewModel {");
        builder.AppendLine("  motionTargets?: Record<string, Partial<Record<'opacity' | 'positionX' | 'positionY' | 'positionZ' | 'scaleX' | 'scaleY' | 'scaleZ' | 'rotation' | 'rotationX' | 'rotationY' | 'width' | 'height' | 'visibility' | 'text' | 'spriteFrame' | 'color', number | boolean | string>>>;");
        builder.AppendLine("  motionScope?: string;");
        foreach (var prop in props)
        {
            builder.Append("  ").Append(prop).AppendLine("?: unknown;");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string RenderNode(ComponentNode node, int indentLevel, LayoutType? parentLayout, IReadOnlyDictionary<string, HudComponentDefinition> components, List<Diagnostic> diagnostics)
    {
        var indent = new string(' ', indentLevel * 2);
        if (node.Visible.IsBound)
        {
            return indent + $"asBool(props.{PropName(node.Visible.BindingPath!)}) ? (\n" +
                RenderCore(node, indentLevel + 1, parentLayout, components, diagnostics) +
                indent + ") : null\n";
        }

        if (node.Visible.Value == false) return indent + "null\n";
        return RenderCore(node, indentLevel, parentLayout, components, diagnostics);
    }

    private static string RenderCore(ComponentNode node, int indentLevel, LayoutType? parentLayout, IReadOnlyDictionary<string, HudComponentDefinition> components, List<Diagnostic> diagnostics)
    {
        var indent = new string(' ', indentLevel * 2);
        var style = BuildStyle(node, parentLayout);
        var motionIdExpression = string.IsNullOrWhiteSpace(node.Id)
            ? null
            : $"resolveMotionId(props.motionScope, {Ts(node.Id)})";
        var motionStyle = motionIdExpression == null ? string.Empty : $"getMotionStyle(props.motionTargets, {motionIdExpression})";
        var mergedStyle = style.Length > 0
            ? (motionStyle.Length > 0 ? $" style={{ {{ {style}, ...{motionStyle} }} }}" : $" style={{ {{ {style} }} }}")
            : (motionStyle.Length > 0 ? $" style={{{motionStyle}}}" : string.Empty);
        var title = node.Tooltip?.IsBound == true ? $" title={{asText(props.{PropName(node.Tooltip!.Value.BindingPath!)})}}" :
            node.Tooltip?.Value is { } tooltip ? $" title={Ts(tooltip)}" : string.Empty;
        var disabled = node.Enabled.IsBound ? $" disabled={{!asBool(props.{PropName(node.Enabled.BindingPath!)})}}" : node.Enabled.Value == false ? " disabled" : string.Empty;
        var common = $" className={Ts(ClassName(node))}" + mergedStyle + title +
            (motionIdExpression == null ? string.Empty : $" data-boomhud-id={{{motionIdExpression}}}");

        if (ShouldComposeComponentRef(node) && node.ComponentRefId != null && components.TryGetValue(node.ComponentRefId, out var component))
        {
            if (node.InstanceOverrides.Count > 0)
            {
                diagnostics.Add(Diagnostic.Warning($"React backend does not apply instance overrides for '{component.Name}' yet.", node.Id, "BHR1002"));
            }

            var childMotionScope = motionIdExpression ?? "props.motionScope";
            return indent + $"<div{common}>\n" + indent + $"  <{component.Name}View motionTargets={{props.motionTargets}} motionScope={{{childMotionScope}}} />\n" + indent + "</div>\n";
        }

        var tag = node.Type switch
        {
            ComponentType.Label or ComponentType.Badge or ComponentType.Icon => "span",
            ComponentType.Button or ComponentType.MenuItem => "button",
            ComponentType.Panel => "section",
            ComponentType.MenuBar => "nav",
            ComponentType.Image => "img",
            ComponentType.TextInput => "input",
            ComponentType.TextArea => "textarea",
            ComponentType.Checkbox or ComponentType.RadioButton => "input",
            _ => "div"
        };

        if (node.Type == ComponentType.Image)
        {
            var sourceExpr = TextExpr(node, ["source", "src", "value"], "''");
            var srcExpr = motionIdExpression == null
                ? sourceExpr
                : $"getMotionSpriteFrame(props.motionTargets, {motionIdExpression}) ?? ({sourceExpr})";
            return indent + $"<img{common} src={{{srcExpr}}} alt={Ts(node.Id ?? "image")} />\n";
        }

        if (node.Type == ComponentType.TextInput)
        {
            return indent + $"<input{common} type=\"text\"{disabled} defaultValue={{{TextExpr(node, ["text", "content", "value"], "''")}}} />\n";
        }

        if (node.Type == ComponentType.TextArea)
        {
            return indent + $"<textarea{common}{disabled} defaultValue={{{TextExpr(node, ["text", "content", "value"], "''")}}} />\n";
        }

        if (node.Type is ComponentType.Checkbox or ComponentType.RadioButton)
        {
            var inputType = node.Type == ComponentType.Checkbox ? "checkbox" : "radio";
            return indent + $"<input{common} type=\"{inputType}\" checked={{asBool({ValueExpr(node, ["checked", "value"], "false")}, false)}} readOnly{disabled} />\n";
        }

        if (node.Type == ComponentType.ProgressBar)
        {
            return indent + $"<div{common}>\n" +
                indent + $"  <div style={{ {{ width: clampPercent({ValueExpr(node, ["value"], "0")}), height: '100%', backgroundColor: 'currentColor' }} }} />\n" +
                indent + "</div>\n";
        }

        var text = TextExpr(node, ["text", "content", "value"], null);
        var builder = new StringBuilder();
        builder.Append(indent).Append('<').Append(tag).Append(common).Append(disabled);
        if (node.Children.Count == 0 && text == null)
        {
            builder.AppendLine(" />");
            return builder.ToString();
        }

        builder.AppendLine(">");
        if (text != null)
        {
            var finalText = motionIdExpression == null
                ? text
                : $"getMotionText(props.motionTargets, {motionIdExpression}) ?? ({text})";
            if (node.Type == ComponentType.Icon)
            {
                finalText = $"resolveIconText({finalText}, {Ts(node.Style?.FontFamily ?? string.Empty)})";
            }
            builder.Append(indent).Append("  {").Append(finalText).AppendLine("}");
        }
        foreach (var child in node.Children) builder.Append(RenderNode(child, indentLevel + 1, node.Layout?.Type, components, diagnostics));
        builder.Append(indent).Append("</").Append(tag).AppendLine(">");
        return builder.ToString();
    }

    private static string BuildStyle(ComponentNode node, LayoutType? parentLayout)
    {
        var style = new List<string>();
        var layout = node.Layout;
        if (layout != null)
        {
            if (layout.Type == LayoutType.Horizontal) style.Add("display: 'flex', flexDirection: 'row'");
            if (layout.Type is LayoutType.Vertical or LayoutType.Stack or LayoutType.Dock) style.Add("display: 'flex', flexDirection: 'column'");
            if (layout.Type == LayoutType.Grid) style.Add("display: 'grid'");
            if (layout.Gap is { } gap) style.Add($"gap: {Ts(SpacingToCss(gap))}");
            if (layout.Padding is { } padding) style.Add($"padding: {Ts(SpacingToCss(padding))}");
            if (layout.Margin is { } margin) style.Add($"margin: {Ts(SpacingToCss(margin))}");
            AppendDimension(style, "width", layout.Width, parentLayout);
            AppendDimension(style, "height", layout.Height, parentLayout);
            AppendDimension(style, "minWidth", layout.MinWidth, parentLayout);
            AppendDimension(style, "minHeight", layout.MinHeight, parentLayout);
            AppendDimension(style, "maxWidth", layout.MaxWidth, parentLayout);
            AppendDimension(style, "maxHeight", layout.MaxHeight, parentLayout);
            if (layout.Align is { } align) style.Add($"alignItems: {Ts(align switch { Alignment.Start => "flex-start", Alignment.Center => "center", Alignment.End => "flex-end", _ => "stretch" })}");
            if (layout.Justify is { } justify) style.Add($"justifyContent: {Ts(justify switch { Justification.Start => "flex-start", Justification.Center => "center", Justification.End => "flex-end", Justification.SpaceBetween => "space-between", Justification.SpaceAround => "space-around", Justification.SpaceEvenly => "space-evenly", _ => "flex-start" })}");
            if (layout.Weight is { } weight) style.Add($"flexGrow: {weight.ToString(CultureInfo.InvariantCulture)}");
        }

        if (node.Style is { } visual)
        {
            if (visual.Foreground is { } foreground) style.Add($"color: {Ts(foreground.ToHex())}");
            if (visual.Background is { } background) style.Add($"backgroundColor: {Ts(background.ToHex())}");
            if (visual.BackgroundImage is { } backgroundImage) AppendBackgroundImageStyle(style, backgroundImage);
            if (visual.FontSize is { } fontSize) style.Add($"fontSize: {Ts($"{fontSize.ToString("0.##", CultureInfo.InvariantCulture)}px")}");
            if (!string.IsNullOrWhiteSpace(visual.FontFamily)) style.Add($"fontFamily: {Ts(visual.FontFamily)}");
            if (visual.FontWeight is { } weight) style.Add($"fontWeight: {Ts(weight == FontWeight.Bold ? "700" : weight == FontWeight.Light ? "300" : "400")}");
            if (visual.FontStyle is { } fontStyle) style.Add($"fontStyle: {Ts(fontStyle == FontStyle.Italic ? "italic" : "normal")}");
            if (visual.LetterSpacing is { } spacing) style.Add($"letterSpacing: {Ts($"{spacing.ToString("0.##", CultureInfo.InvariantCulture)}px")}");
            if (visual.Opacity is { } opacity) style.Add($"opacity: {opacity.ToString(CultureInfo.InvariantCulture)}");
            if (visual.BorderRadius is { } borderRadius) style.Add($"borderRadius: {Ts($"{borderRadius.ToString("0.##", CultureInfo.InvariantCulture)}px")}");
            if (visual.Border is { } border)
            {
                if (border.Style != BorderStyle.None) style.Add($"borderStyle: {Ts(border.Style == BorderStyle.Dashed ? "dashed" : "solid")}, borderWidth: {Ts($"{border.Width.ToString("0.##", CultureInfo.InvariantCulture)}px")}");
                if (border.Color is { } borderColor) style.Add($"borderColor: {Ts(borderColor.ToHex())}");
            }
        }

        var hasExplicitAbsolutePlacement = HasExplicitAbsolutePlacement(node, layout);
        if (layout?.Type == LayoutType.Absolute && parentLayout != LayoutType.Absolute && !hasExplicitAbsolutePlacement)
        {
            style.Add("position: 'relative'");
        }

        if (parentLayout == LayoutType.Absolute || hasExplicitAbsolutePlacement)
        {
            style.Add("position: 'absolute'");
            var left = ResolveAbsoluteOffset(node, layout, static currentLayout => currentLayout.Left, BoomHudMetadataKeys.PencilLeft);
            var top = ResolveAbsoluteOffset(node, layout, static currentLayout => currentLayout.Top, BoomHudMetadataKeys.PencilTop);
            AppendPositionDimension(style, "left", left);
            AppendPositionDimension(style, "top", top);

            if (hasExplicitAbsolutePlacement && parentLayout == null)
            {
                if (left == null) style.Add("left: '0px'");
                if (top == null) style.Add("top: '0px'");
            }
        }

        if (layout?.ClipContent == true || BoolMetadata(node, BoomHudMetadataKeys.PencilClip) is true)
        {
            style.Add("overflow: 'hidden'");
        }

        if (FindBinding(node, "style.foreground", "foreground") is { } foregroundBinding) style.Add($"color: asText(props.{PropName(foregroundBinding.Path)}, {Ts(foregroundBinding.Fallback?.ToString() ?? string.Empty)})");
        if (FindBinding(node, "style.background", "background") is { } backgroundBinding) style.Add($"backgroundColor: asText(props.{PropName(backgroundBinding.Path)}, {Ts(backgroundBinding.Fallback?.ToString() ?? string.Empty)})");
        return string.Join(", ", style);
    }

    private static string SpacingToCss(Spacing spacing)
    {
        if (spacing.Top == spacing.Right && spacing.Right == spacing.Bottom && spacing.Bottom == spacing.Left)
        {
            return CssPixels(spacing.Top);
        }

        if (spacing.Top == spacing.Bottom && spacing.Left == spacing.Right)
        {
            return $"{CssPixels(spacing.Top)} {CssPixels(spacing.Left)}";
        }

        return $"{CssPixels(spacing.Top)} {CssPixels(spacing.Right)} {CssPixels(spacing.Bottom)} {CssPixels(spacing.Left)}";
    }

    private static string CssPixels(double value) => value == 0d
        ? "0"
        : $"{value.ToString("0.##", CultureInfo.InvariantCulture)}px";

    private static void AppendBackgroundImageStyle(List<string> style, BackgroundImageSpec image)
    {
        style.Add($"backgroundImage: {Ts($"url('{image.Url}')")}");

        switch (image.Mode)
        {
            case BackgroundImageMode.Fill:
                style.Add("backgroundSize: 'cover'");
                style.Add("backgroundPosition: 'center'");
                style.Add("backgroundRepeat: 'no-repeat'");
                break;
            case BackgroundImageMode.Contain:
                style.Add("backgroundSize: 'contain'");
                style.Add("backgroundPosition: 'center'");
                style.Add("backgroundRepeat: 'no-repeat'");
                break;
            case BackgroundImageMode.Stretch:
                style.Add("backgroundSize: '100% 100%'");
                style.Add("backgroundPosition: 'center'");
                style.Add("backgroundRepeat: 'no-repeat'");
                break;
            case BackgroundImageMode.Tile:
                style.Add("backgroundSize: 'auto'");
                style.Add("backgroundRepeat: 'repeat'");
                break;
            case BackgroundImageMode.Original:
                style.Add("backgroundSize: 'auto'");
                style.Add("backgroundPosition: 'center'");
                style.Add("backgroundRepeat: 'no-repeat'");
                break;
        }
    }

    private static void AppendDimension(List<string> style, string key, Dimension? dimension, LayoutType? parentLayout)
    {
        if (dimension == null) return;
        switch (dimension.Value.Unit)
        {
            case DimensionUnit.Pixels: style.Add($"{key}: {Ts($"{dimension.Value.Value.ToString("0.##", CultureInfo.InvariantCulture)}px")}"); break;
            case DimensionUnit.Percent: style.Add($"{key}: {Ts($"{dimension.Value.Value.ToString(CultureInfo.InvariantCulture)}%")}"); break;
            case DimensionUnit.Auto: style.Add($"{key}: 'auto'"); break;
            case DimensionUnit.Fill:
                style.Add(FillDimensionStyle(key, parentLayout));
                break;
            case DimensionUnit.Star: style.Add($"flexGrow: {dimension.Value.Value.ToString(CultureInfo.InvariantCulture)}"); break;
        }
    }

    private static void AppendPositionDimension(List<string> style, string key, Dimension? dimension)
    {
        if (dimension == null) return;

        switch (dimension.Value.Unit)
        {
            case DimensionUnit.Pixels:
                style.Add($"{key}: {Ts($"{dimension.Value.Value.ToString("0.##", CultureInfo.InvariantCulture)}px")}");
                break;
            case DimensionUnit.Percent:
                style.Add($"{key}: {Ts($"{dimension.Value.Value.ToString(CultureInfo.InvariantCulture)}%")}");
                break;
            case DimensionUnit.Cells:
                style.Add($"{key}: {Ts($"{dimension.Value.Value.ToString("0.##", CultureInfo.InvariantCulture)}px")}");
                break;
        }
    }

    private static string FillDimensionStyle(string key, LayoutType? parentLayout)
    {
        if (key == "width")
        {
            return parentLayout == LayoutType.Horizontal ? "flex: '1 1 0'" : "alignSelf: 'stretch'";
        }

        if (key == "height")
        {
            return parentLayout is LayoutType.Vertical or LayoutType.Stack or LayoutType.Dock ? "flex: '1 1 0'" : "alignSelf: 'stretch'";
        }

        return key.Contains("Width", StringComparison.Ordinal) ? "alignSelf: 'stretch'" : "flexGrow: 1";
    }

    private static string? TextExpr(ComponentNode node, IReadOnlyList<string> names, string? fallback)
    {
        foreach (var name in names)
        {
            if (FindBinding(node, name) is { } binding)
            {
                var prop = $"props.{PropName(binding.Path)}";
                return string.IsNullOrWhiteSpace(binding.Format) ? $"asText({prop}, {Ts(binding.Fallback?.ToString() ?? string.Empty)})" : $"formatValue({prop}, {Ts(binding.Format)}, {Ts(binding.Fallback?.ToString() ?? string.Empty)})";
            }

            if (TryGetProperty(node, name, out var bindable))
            {
                if (bindable.IsBound) return $"asText(props.{PropName(bindable.BindingPath!)})";
                if (bindable.Value != null) return bindable.Value is string text ? Ts(text) : $"asText({Literal(bindable.Value)})";
            }
        }

        return fallback;
    }

    private static string ValueExpr(ComponentNode node, IReadOnlyList<string> names, string fallback)
    {
        foreach (var name in names)
        {
            if (FindBinding(node, name) is { } binding) return $"props.{PropName(binding.Path)}";
            if (TryGetProperty(node, name, out var bindable))
            {
                if (bindable.IsBound) return $"props.{PropName(bindable.BindingPath!)}";
                if (bindable.Value != null) return Literal(bindable.Value);
            }
        }

        return fallback;
    }

    private static BindingSpec? FindBinding(ComponentNode node, params string[] names)
        => node.Bindings.FirstOrDefault(binding => names.Any(name => string.Equals(binding.Property, name, StringComparison.OrdinalIgnoreCase)));

    private static bool TryGetProperty(ComponentNode node, string name, out BindableValue<object?> bindable)
    {
        foreach (var pair in node.Properties)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                bindable = pair.Value;
                return true;
            }
        }

        bindable = default;
        return false;
    }

    private static string Literal(object value) => value switch
    {
        bool boolValue => boolValue ? "true" : "false",
        int intValue => intValue.ToString(CultureInfo.InvariantCulture),
        long longValue => longValue.ToString(CultureInfo.InvariantCulture),
        float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
        double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
        decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
        _ => Ts(value.ToString() ?? string.Empty)
    };

    private static HashSet<string> CollectProps(ComponentNode node)
    {
        var props = new HashSet<string>(StringComparer.Ordinal);
        CollectProps(node, props);
        return props;
    }

    private static void CollectProps(ComponentNode node, HashSet<string> props)
    {
        foreach (var binding in node.Bindings) props.Add(PropName(binding.Path));
        if (node.Visible.IsBound) props.Add(PropName(node.Visible.BindingPath!));
        if (node.Enabled.IsBound) props.Add(PropName(node.Enabled.BindingPath!));
        if (node.Tooltip?.IsBound == true) props.Add(PropName(node.Tooltip!.Value.BindingPath!));
        foreach (var property in node.Properties.Values.Where(static property => property.IsBound)) props.Add(PropName(property.BindingPath!));
        foreach (var child in node.Children) CollectProps(child, props);
    }

    private static HashSet<string> CollectRefs(ComponentNode node, IReadOnlyDictionary<string, HudComponentDefinition> components, List<Diagnostic> diagnostics, string documentName)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);
        CollectRefs(node, components, diagnostics, documentName, refs);
        return refs;
    }

    private static void CollectRefs(ComponentNode node, IReadOnlyDictionary<string, HudComponentDefinition> components, List<Diagnostic> diagnostics, string documentName, HashSet<string> refs)
    {
        if (ShouldComposeComponentRef(node) && node.ComponentRefId != null)
        {
            if (components.TryGetValue(node.ComponentRefId, out var component))
            {
                if (!string.Equals(component.Name, documentName, StringComparison.Ordinal)) refs.Add(component.Name);
            }
            else
            {
                diagnostics.Add(Diagnostic.Warning($"Component reference '{node.ComponentRefId}' was not found for React generation.", node.Id, "BHR1001"));
            }
        }

        foreach (var child in node.Children) CollectRefs(child, components, diagnostics, documentName, refs);
    }

    private static bool ShouldComposeComponentRef(ComponentNode node)
        => node.ComponentRefId != null && node.Children.Count == 0;

    private static double? NumericMetadata(ComponentNode node, string key)
    {
        if (!node.InstanceOverrides.TryGetValue(key, out var raw) || raw == null) return null;
        return raw switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            long longValue => longValue,
            _ => null
        };
    }

    private static bool? BoolMetadata(ComponentNode node, string key)
    {
        if (!node.InstanceOverrides.TryGetValue(key, out var raw) || raw == null) return null;
        return raw switch
        {
            bool boolValue => boolValue,
            _ => null
        };
    }

    private static bool HasExplicitAbsolutePlacement(ComponentNode node, LayoutSpec? layout)
    {
        if (layout?.IsAbsolutePositioned == true)
        {
            return true;
        }

        if (!node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.PencilPosition, out var raw) || raw == null)
        {
            return false;
        }

        return raw is string stringValue
            && string.Equals(stringValue, "absolute", StringComparison.OrdinalIgnoreCase);
    }

    private static Dimension? ResolveAbsoluteOffset(
        ComponentNode node,
        LayoutSpec? layout,
        Func<LayoutSpec, Dimension?> selector,
        string metadataKey)
    {
        if (layout != null && selector(layout) is { } dimension)
        {
            return dimension;
        }

        return NumericMetadata(node, metadataKey) is { } value
            ? Dimension.Pixels(value)
            : null;
    }

    private static string PropName(string path)
    {
        var parts = path.Split(['.', ':', '-', '/', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static part => new string(part.Where(char.IsLetterOrDigit).ToArray()))
            .Where(static part => part.Length > 0)
            .Select(static part => char.ToUpperInvariant(part[0]) + part[1..])
            .ToList();
        if (parts.Count == 0) return "value";
        var first = parts[0];
        return char.ToLowerInvariant(first[0]) + first[1..] + string.Concat(parts.Skip(1));
    }

    private static string ClassName(ComponentNode node)
        => "boomhud-node boomhud-" + string.Concat((node.Id ?? node.Type.ToString()).Select(static ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')).Trim('-');

    private static string Ts(string value)
        => "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "'";
}
