using System.Globalization;
using System.Text;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;

namespace BoomHud.Gen.TerminalGui;

/// <summary>
/// Code generator for Terminal.Gui v2.
/// </summary>
public sealed class TerminalGuiGenerator : IBackendGenerator
{
    public string TargetFramework => "Terminal.Gui";

    public ICapabilityManifest Capabilities => TerminalGuiCapabilities.Instance;

    public GenerationResult Generate(HudDocument document, GenerationOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var files = new List<GeneratedFile>();
        var prepared = GenerationDocumentPreprocessor.Prepare(document, options, "terminalgui");
        document = prepared.Document;
        diagnostics.AddRange(prepared.Diagnostics);

        try
        {
            SpatialCompatibilityChecker.CheckDocument(document, Capabilities, diagnostics);

            foreach (var component in document.Components.Values)
            {
                var componentDocument = new HudDocument
                {
                    Name = component.Name,
                    Metadata = component.Metadata,
                    Root = component.Root,
                    Styles = document.Styles
                };

                var componentViewCode = GenerateViewClass(componentDocument, options, diagnostics, document.Components);
                files.Add(new GeneratedFile
                {
                    Path = $"{componentDocument.Name}View.g.cs",
                    Content = componentViewCode,
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

            // Generate the main view class
            var viewCode = GenerateViewClass(document, options, diagnostics, document.Components);
            files.Add(new GeneratedFile
            {
                Path = $"{document.Name}View.g.cs",
                Content = viewCode,
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

            // ViewModel interface
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

    private string GenerateViewClass(
        HudDocument document,
        GenerationOptions options,
        List<Diagnostic> diagnostics,
        IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        var cb = new CodeBuilder();

        var viewModelNamespace = options.ViewModelNamespace ?? options.Namespace;
        var sourceId = GeneratorSourceId.ComputeSourceId(document);
        var contractId = options.ContractId ?? string.Empty;

        var normalizedPseudoNodes = GeneratorSourceId.CollectNormalizedPseudoNodes(document);

        // File header
        if (options.IncludeComments)
        {
            cb.AppendLine("// <auto-generated>");
            cb.AppendLine($"// Generated by BoomHud.Gen.TerminalGui from {document.Name}");
            cb.AppendLine("// </auto-generated>");
            cb.AppendLine();
        }

        // Nullable directive
        if (options.UseNullableAnnotations)
        {
            cb.AppendLine("#nullable enable");
            cb.AppendLine();
        }

        // Usings
        cb.AppendLine("using System;");
        cb.AppendLine("using System.Collections.Generic;");
        cb.AppendLine("using Terminal.Gui;");
        if (!options.TerminalGuiFlatNamespace)
        {
            // Post-2.0.1 namespace split (default).
            cb.AppendLine("using Terminal.Gui.Drawing;");
            cb.AppendLine("using Terminal.Gui.Views;");
            cb.AppendLine("using Terminal.Gui.ViewBase;");
            cb.AppendLine("using View = Terminal.Gui.ViewBase.View;");
        }
        else
        {
            // Flat namespace (Terminal.Gui 2.0.0). Alias View for symmetry
            // with the split-namespace path so downstream emission stays uniform.
            cb.AppendLine("using View = Terminal.Gui.View;");
        }

        if (!string.Equals(viewModelNamespace, options.Namespace, StringComparison.Ordinal))
        {
            cb.AppendLine($"using {viewModelNamespace};");
        }
        cb.AppendLine();

        // Namespace
        cb.AppendLine($"namespace {options.Namespace};");
        cb.AppendLine();

        // Class documentation
        if (options.IncludeComments && document.Metadata?.Description != null)
        {
            var description = ApplyDescriptionReplacements(document.Metadata.Description, options.DescriptionReplacements);
            cb.AppendLine("/// <summary>");
            cb.AppendLine($"/// {description}");
            cb.AppendLine("/// </summary>");
        }

        // Class declaration
        var baseTypeName = options.TerminalGuiFlatNamespace
            ? "Terminal.Gui.View"
            : "Terminal.Gui.ViewBase.View";
        cb.AppendLine($"public partial class {document.Name}View : {baseTypeName}");
        cb.OpenBlock();

        cb.AppendLine($"public const string BoomHudSourceId = \"{sourceId}\";");
        cb.AppendLine($"public const string BoomHudContractId = \"{EscapeString(contractId)}\";");
        cb.AppendLine($"public static readonly string[] BoomHudNormalizedPseudoNodes = {GeneratorSourceId.FormatStringArrayLiteral(normalizedPseudoNodes)};");
        cb.AppendLine();

        cb.AppendLine("private readonly Dictionary<string, View> _slots = new(StringComparer.Ordinal);");
        cb.AppendLine();
        cb.AppendLine("private void RegisterSlot(string slotKey, View view)");
        cb.OpenBlock();
        cb.AppendLine("_slots[slotKey] = view;");
        cb.CloseBlock();
        cb.AppendLine();
        cb.AppendLine("public T FindSlot<T>(string slotKey) where T : View");
        cb.OpenBlock();
        cb.AppendLine("if (!_slots.TryGetValue(slotKey, out var view))");
        cb.OpenBlock();
        cb.AppendLine("throw new InvalidOperationException(\"Could not find slot: \" + slotKey);");
        cb.CloseBlock();
        cb.AppendLine("return (T)view;");
        cb.CloseBlock();
        cb.AppendLine();

        // ViewModel property
        cb.AppendLine($"private I{document.Name}ViewModel? _viewModel;");
        cb.AppendLine();
        cb.AppendLine($"public I{document.Name}ViewModel? ViewModel");
        cb.OpenBlock();
        cb.AppendLine("get => _viewModel;");
        cb.AppendLine("set");
        cb.OpenBlock();
        cb.AppendLine("_viewModel = value;");
        cb.AppendLine("RefreshBindings();");
        cb.CloseBlock();
        cb.CloseBlock();
        cb.AppendLine();

        // Collect all component fields
        var componentFields = new List<(string Id, string Type)>();
        CollectComponentFields(document.Root, componentFields, components);
        var nameContext = BuildNameContext(componentFields);

        // Generate field declarations
        foreach (var (id, type) in componentFields)
        {
            cb.AppendLine($"private {type} {ResolveFieldName(nameContext, id)} = null!;");
        }

        if (componentFields.Count > 0)
            cb.AppendLine();

        // Constructor
        cb.AppendLine($"public {document.Name}View()");
        cb.OpenBlock();
        cb.AppendLine("InitializeComponents();");
        cb.CloseBlock();
        cb.AppendLine();

        // InitializeComponents method
        cb.AppendLine("private void InitializeComponents()");
        cb.OpenBlock();

        // Generate root component setup
        GenerateComponentSetup(cb, document.Root, "this", diagnostics, options, null, components, nameContext);

        cb.CloseBlock();
        cb.AppendLine();

        // RefreshBindings method
        GenerateRefreshBindingsMethod(cb, document, componentFields, nameContext);

        // Generate public accessors for named components
        if (componentFields.Count > 0)
        {
            cb.AppendLine();
            if (options.IncludeComments)
            {
                cb.AppendLine("// Component accessors");
            }

            foreach (var (id, type) in componentFields)
            {
                cb.AppendLine($"public {type} {ResolveAccessorName(nameContext, id)} => {ResolveFieldName(nameContext, id)};");
            }
        }

        cb.CloseBlock(); // class

        return cb.ToString();
    }

    private static string GenerateCompose(HudDocument document, GenerationOptions options, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        var cb = new CodeBuilder();
        var viewModelNamespace = options.ViewModelNamespace ?? options.Namespace;

        cb.AppendLine("#nullable enable");
        cb.AppendLine();
        cb.AppendLine("using System;");
        cb.AppendLine("using System.Collections.Generic;");

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
        cb.AppendLine("root.ViewModel = vm;");
        cb.AppendLine("d.Add(new DisposableAction(() => root.ViewModel = null));");

        var childInstances = new List<(ComponentNode Node, HudComponentDefinition Def)>();
        CollectComponentInstances(document.Root, components, childInstances);
        foreach (var (node, def) in childInstances)
        {
            var slotKey = node.SlotKey ?? node.Id ?? def.Name;
            if (string.IsNullOrWhiteSpace(slotKey))
                continue;

            cb.AppendLine();
            cb.AppendLine($"var childView = root.FindSlot<{def.Name}View>(\"{EscapeString(slotKey)}\");");
            cb.AppendLine($"var childVm = resolver.Resolve<I{def.Name}ViewModel>(vm, \"{EscapeString(slotKey)}\");");
            cb.AppendLine("childView.ViewModel = childVm;");
            cb.AppendLine("d.Add(new DisposableAction(() => childView.ViewModel = null));");
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
            cb.AppendLine($"// Generated by BoomHud.Gen.TerminalGui from {document.Name}");
            cb.AppendLine("// </auto-generated>");
            cb.AppendLine();
        }

        if (options.UseNullableAnnotations)
        {
            cb.AppendLine("#nullable enable");
            cb.AppendLine();
        }

        cb.AppendLine($"namespace {viewModelNamespace};");
        cb.AppendLine();

        if (options.IncludeComments)
        {
            cb.AppendLine("/// <summary>");
            cb.AppendLine($"/// ViewModel interface for {document.Name}.");
            cb.AppendLine("/// </summary>");
        }

        cb.AppendLine($"public interface I{document.Name}ViewModel");
        cb.OpenBlock();

        // Collect all binding paths and generate properties
        var bindingPaths = new HashSet<string>();
        CollectBindingPaths(document.Root, bindingPaths);

        foreach (var path in bindingPaths.OrderBy(p => p))
        {
            var propertyName = path.Replace(".", "");
            cb.AppendLine($"object? {propertyName} {{ get; }}");
        }

        cb.CloseBlock();

        return cb.ToString();
    }

    private void GenerateComponentSetup(
        CodeBuilder cb,
        ComponentNode node,
        string parentVar,
        List<Diagnostic> diagnostics,
        GenerationOptions options,
        LayoutType? parentLayoutType,
        IReadOnlyDictionary<string, HudComponentDefinition> components,
        Dictionary<string, (string FieldName, string AccessorName)> nameContext)
    {
        var isRoot = parentVar == "this";

        if (node.Layout != null && isRoot)
        {
            GenerateRootLayoutSetup(cb, node.Layout, "this");
        }

        if (node.Style != null && isRoot)
        {
            GenerateStyleSetup(cb, node.Style, "this", options.TerminalGuiFlatNamespace);
        }

        string? previousChildVar = null;
        var layoutType = node.Layout?.Type ?? LayoutType.Vertical;
        var gap = (int)(node.Layout?.Gap?.Top ?? 0); // Assuming uniform gap for now

        // Initial offset from padding (handled by View.Padding now)
        // var paddingX = (int)(node.Layout?.Padding?.Left ?? 0);
        // var paddingY = (int)(node.Layout?.Padding?.Top ?? 0);

        // Scale gap if it looks like pixels
        if (gap > 5)
        {
            if (layoutType == LayoutType.Horizontal) gap /= 12;
            else gap /= 24;
        }

        foreach (var child in node.Children)
        {
            var childVar = GenerateChildComponent(cb, child, parentVar, diagnostics, options, layoutType, components, nameContext);

            if (layoutType == LayoutType.Vertical)
            {
                // Vertical Layout: Stack Y, Align X (default Left/0)
                if (previousChildVar == null)
                {
                    cb.AppendLine($"{childVar}.Y = 0;");
                }
                else
                {
                    if (gap > 0)
                        cb.AppendLine($"{childVar}.Y = Pos.Bottom({previousChildVar}) + {gap};");
                    else
                        cb.AppendLine($"{childVar}.Y = Pos.Bottom({previousChildVar});");
                }

                cb.AppendLine($"{childVar}.X = 0;");
            }
            else if (layoutType == LayoutType.Horizontal)
            {
                // Horizontal Layout: Stack X, Align Y (default Top/0)
                if (previousChildVar == null)
                {
                    cb.AppendLine($"{childVar}.X = 0;");
                }
                else
                {
                    if (gap > 0)
                        cb.AppendLine($"{childVar}.X = Pos.Right({previousChildVar}) + {gap};");
                    else
                        cb.AppendLine($"{childVar}.X = Pos.Right({previousChildVar});");
                }

                cb.AppendLine($"{childVar}.Y = 0;");
            }
            else
            {
                // Absolute or other: Default to 0,0 relative to parent padding
                cb.AppendLine($"{childVar}.X = 0;");
                cb.AppendLine($"{childVar}.Y = 0;");
            }

            if (child.Children.Count > 0)
            {
                GenerateComponentSetup(cb, child, childVar, diagnostics, options, child.Layout?.Type, components, nameContext);
            }

            previousChildVar = childVar;
        }
    }

    private string GenerateChildComponent(
        CodeBuilder cb,
        ComponentNode node,
        string parentVar,
        List<Diagnostic> diagnostics,
        GenerationOptions options,
        LayoutType? parentLayoutType,
        IReadOnlyDictionary<string, HudComponentDefinition> components,
        Dictionary<string, (string FieldName, string AccessorName)> nameContext)
    {
        var terminalGuiType = MapComponentType(node.Type);
        HudComponentDefinition? componentDef = null;
        if (node.ComponentRefId != null && components.TryGetValue(node.ComponentRefId, out var resolvedComponentDef))
        {
            componentDef = resolvedComponentDef;
            terminalGuiType = $"{componentDef.Name}View";
        }

        var varName = node.Id != null ? ResolveFieldName(nameContext, node.Id) : $"_component{Guid.NewGuid():N}".Substring(0, 16);

        // Check capability
        if (!Capabilities.SupportsComponent(node.Type.ToString()))
        {
            if (options.MissingCapabilityPolicy == MissingCapabilityPolicy.Error)
            {
                diagnostics.Add(Diagnostic.Error($"Component type '{node.Type}' is not supported by Terminal.Gui", node.Id));
                return varName;
            }
            else if (options.MissingCapabilityPolicy == MissingCapabilityPolicy.Warn)
            {
                diagnostics.Add(Diagnostic.Warning($"Component type '{node.Type}' has limited support in Terminal.Gui", node.Id));
            }
            else if (options.MissingCapabilityPolicy == MissingCapabilityPolicy.Skip)
            {
                return varName;
            }
        }

        cb.AppendLine();

        // Create component
        if (node.Id != null)
        {
            cb.AppendLine($"{varName} = new {terminalGuiType}();");
        }
        else
        {
            cb.AppendLine($"var {varName} = new {terminalGuiType}();");
        }

        // Set ID if present
        if (node.Id != null)
        {
            cb.AppendLine($"{varName}.Id = \"{node.Id}\";");
        }

        // Apply per-instance property overrides for synthetic/explicit component
        // instances. The IR records overrides as a path-keyed map produced by
        // ComponentInstanceOverrideSupport — paths are rooted at "$" (the
        // component definition's Root) and step into children by index, e.g.
        // "$/0" = root.Children[0]. We resolve each path against componentDef
        // to find the target node's Id, then look up the accessor name that the
        // component's own View class will expose (computed via the same
        // CollectComponentFields + BuildNameContext used inside the View).
        if (componentDef != null)
        {
            EmitInstanceOverrides(cb, node, componentDef, components, varName, diagnostics);
        }

        // Apply layout
        if (node.Layout != null)
        {
            // For labels, we usually want to let them auto-size unless explicitly constrained
            if (node.Type != ComponentType.Label)
            {
                GenerateLayoutSetup(cb, node.Layout, varName, parentLayoutType);
            }
            else
            {
                // Labels need special handling. 
                // If explicit width is provided (e.g. from Figma fixed width), use it.
                // Otherwise let it auto-size.
                if (node.Layout.Width != null)
                {
                    var dimExpr = DimensionToTerminalGui(node.Layout.Width.Value, "Width");
                    cb.AppendLine($"{varName}.Width = {dimExpr};");
                }
            }
        }
        else
        {
            // No layout spec, but we should apply defaults based on parent layout
            if (node.Type != ComponentType.Label)
            {
                // Create dummy layout to force defaults
                GenerateLayoutSetup(cb, new LayoutSpec(), varName, parentLayoutType);
            }
        }

        // Apply style
        if (node.Style != null)
        {
            GenerateStyleSetup(cb, node.Style, varName, options.TerminalGuiFlatNamespace);
        }

        // Apply component-specific properties
        GenerateComponentProperties(cb, node, varName);

        // Add to parent
        cb.AppendLine($"{parentVar}.Add({varName});");

        if (!string.IsNullOrWhiteSpace(node.SlotKey))
        {
            cb.AppendLine($"RegisterSlot(\"{EscapeString(node.SlotKey)}\", {varName});");
        }

        return varName;
    }

    private static void EmitInstanceOverrides(
        CodeBuilder cb,
        ComponentNode instanceNode,
        HudComponentDefinition componentDef,
        IReadOnlyDictionary<string, HudComponentDefinition> components,
        string varName,
        List<Diagnostic> diagnostics)
    {
        var propertyOverrides = ComponentInstanceOverrideSupport.GetPropertyOverrides(instanceNode);
        if (propertyOverrides.Count == 0)
        {
            return;
        }

        // The component's own View class builds its name context from
        // CollectComponentFields(componentDef.Root). We rebuild the same
        // context here so the accessor names we emit (e.g. `.TitleAlpha`)
        // exactly match what the View exposes.
        var componentFields = new List<(string Id, string Type)>();
        CollectComponentFields(componentDef.Root, componentFields, components);
        var componentNameContext = BuildNameContext(componentFields);

        foreach (var (path, properties) in propertyOverrides)
        {
            var targetNode = ResolveOverridePath(componentDef.Root, path);
            if (targetNode is null)
            {
                diagnostics.Add(Diagnostic.Warning(
                    $"Override path '{path}' did not resolve against component '{componentDef.Name}'.",
                    instanceNode.Id));
                continue;
            }

            // For path "$" the target is the View itself (varName). For deeper
            // paths the target is an inner field exposed via its accessor name.
            string memberExpr;
            if (string.Equals(path, ComponentInstanceOverrideSupport.RootPath, StringComparison.Ordinal))
            {
                memberExpr = varName;
            }
            else if (!string.IsNullOrEmpty(targetNode.Id))
            {
                var accessorName = ResolveAccessorName(componentNameContext, targetNode.Id);
                memberExpr = $"{varName}.{accessorName}";
            }
            else
            {
                diagnostics.Add(Diagnostic.Warning(
                    $"Override path '{path}' targets an unnamed node inside component '{componentDef.Name}'; no accessor to emit.",
                    instanceNode.Id));
                continue;
            }

            // Pencil's parser temporarily writes both `text` and `Text` to the
            // IR during the casing migration (boom-hud commit da17967e). The
            // componentizer then records overrides for BOTH keys with identical
            // values — emitting both yields a duplicated `.Text = "…"` line. We
            // dedupe by the normalized property name so each effective property
            // is assigned at most once per path.
            var emittedNormalizedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (propertyName, value) in properties)
            {
                var normalizedName = ComponentInstanceOverrideSupport.NormalizePropertyName(propertyName);
                if (!emittedNormalizedNames.Add(normalizedName))
                {
                    continue;
                }

                var assignment = FormatOverrideAssignment(targetNode, memberExpr, propertyName, value);
                if (assignment is null)
                {
                    diagnostics.Add(Diagnostic.Warning(
                        $"Override property '{propertyName}' on '{componentDef.Name}'[{path}] ({targetNode.Type}) is not emittable by the TerminalGui backend.",
                        instanceNode.Id));
                    continue;
                }

                cb.AppendLine(assignment);
            }
        }
    }

    private static ComponentNode? ResolveOverridePath(ComponentNode root, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (string.Equals(path, ComponentInstanceOverrideSupport.RootPath, StringComparison.Ordinal))
        {
            return root;
        }

        var prefix = ComponentInstanceOverrideSupport.RootPath + "/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var current = root;
        var segments = path.Substring(prefix.Length).Split('/');
        foreach (var segment in segments)
        {
            if (!int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
            {
                return null;
            }

            if (current is null || idx < 0 || idx >= current.Children.Count)
            {
                return null;
            }

            current = current.Children[idx];
        }

        return current;
    }

    private static string? FormatOverrideAssignment(
        ComponentNode targetNode,
        string memberExpr,
        string propertyName,
        object? value)
    {
        var normalized = ComponentInstanceOverrideSupport.NormalizePropertyName(propertyName);
        return normalized switch
        {
            "text" or "content" => FormatTextOverride(targetNode, memberExpr, value),
            "value" => FormatValueOverride(targetNode, memberExpr, value),
            _ => null
        };
    }

    private static string? FormatTextOverride(ComponentNode targetNode, string memberExpr, object? value)
    {
        // Most text-bearing node types render to TGui View.Text. ProgressBar
        // (which IsSupportedProperty accepts for "text") has no Text property
        // in Terminal.Gui — skip to a diagnostic.
        return targetNode.Type switch
        {
            ComponentType.Label
                or ComponentType.Badge
                or ComponentType.Button
                or ComponentType.TextInput
                or ComponentType.TextArea
                or ComponentType.Checkbox
                or ComponentType.RadioButton
                or ComponentType.Icon
                => $"{memberExpr}.Text = \"{EscapeString(value?.ToString() ?? string.Empty)}\";",
            _ => null
        };
    }

    private static string? FormatValueOverride(ComponentNode targetNode, string memberExpr, object? value)
    {
        switch (targetNode.Type)
        {
            case ComponentType.Label:
            case ComponentType.Badge:
            case ComponentType.Button:
            case ComponentType.TextInput:
            case ComponentType.TextArea:
            case ComponentType.Checkbox:
            case ComponentType.RadioButton:
            case ComponentType.Icon:
                return $"{memberExpr}.Text = \"{EscapeString(value?.ToString() ?? string.Empty)}\";";
            case ComponentType.ProgressBar:
                {
                    var fraction = Convert.ToSingle(value ?? 0, CultureInfo.InvariantCulture);
                    return $"{memberExpr}.Fraction = {fraction.ToString("R", CultureInfo.InvariantCulture)}f;";
                }
            case ComponentType.Slider:
                {
                    var slider = Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);
                    return $"{memberExpr}.Value = {slider.ToString(CultureInfo.InvariantCulture)};";
                }
            default:
                return null;
        }
    }

    private static void GenerateLayoutSetup(CodeBuilder cb, LayoutSpec layout, string varName, LayoutType? parentLayoutType)
    {
        // Width
        if (layout.Width != null)
        {
            var dimExpr = DimensionToTerminalGui(layout.Width.Value, "Width");
            cb.AppendLine($"{varName}.Width = {dimExpr};");
        }

        // Height
        if (layout.Height != null)
        {
            var dimExpr = DimensionToTerminalGui(layout.Height.Value, "Height");
            cb.AppendLine($"{varName}.Height = {dimExpr};");
        }

        // If no explicit size was provided...
        // NEW LOGIC: Default to Fill for cross-axis stretch, Auto for main-axis.

        if (layout.Width == null)
        {
            if (parentLayoutType == LayoutType.Vertical)
            {
                // Cross-axis stretch for Vertical parent
                cb.AppendLine($"{varName}.Width = Dim.Fill();");
            }
            else
            {
                // Main-axis or unknown: Auto
                cb.AppendLine($"{varName}.Width = Dim.Auto();");
            }
        }

        if (layout.Height == null)
        {
            if (parentLayoutType == LayoutType.Horizontal)
            {
                // Cross-axis stretch for Horizontal parent
                cb.AppendLine($"{varName}.Height = Dim.Fill();");
            }
            else
            {
                // Main-axis or unknown: Auto
                cb.AppendLine($"{varName}.Height = Dim.Auto();");
            }
        }

        // Margin
        if (layout.Margin != null)
        {
            var m = layout.Margin.Value;
            cb.AppendLine($"{varName}.Margin.Thickness = new Thickness({(int)m.Left}, {(int)m.Top}, {(int)m.Right}, {(int)m.Bottom});");
        }

        // Padding — Terminal.Gui's `View.Padding` is an Adornment; you cannot
        // assign a Rect to it. Use `Padding.Thickness = new Thickness(...)` to
        // match the GenerateRootLayoutSetup path below. The old `new Rect(...)`
        // emit was a long-standing typo that only bit consumers when a nested
        // (non-root) frame carried a `padding` attribute in the design.
        if (layout.Padding != null)
        {
            var p = layout.Padding.Value;
            cb.AppendLine($"{varName}.Padding.Thickness = new Thickness({(int)p.Left}, {(int)p.Top}, {(int)p.Right}, {(int)p.Bottom});");
        }
    }

    private static void GenerateRootLayoutSetup(CodeBuilder cb, LayoutSpec layout, string varName)
    {
        if (layout.Width != null)
        {
            var dimExpr = DimensionToTerminalGui(layout.Width.Value, "Width");
            cb.AppendLine($"{varName}.Width = {dimExpr};");
        }
        else
        {
            cb.AppendLine($"{varName}.Width = Dim.Fill();");
        }

        if (layout.Height != null)
        {
            var dimExpr = DimensionToTerminalGui(layout.Height.Value, "Height");
            cb.AppendLine($"{varName}.Height = {dimExpr};");
        }
        else
        {
            cb.AppendLine($"{varName}.Height = Dim.Fill();");
        }

        if (layout.Margin != null)
        {
            var m = layout.Margin.Value;
            cb.AppendLine($"{varName}.Margin.Thickness = new Thickness({(int)m.Left}, {(int)m.Top}, {(int)m.Right}, {(int)m.Bottom});");
        }

        if (layout.Padding != null)
        {
            var p = layout.Padding.Value;
            cb.AppendLine($"{varName}.Padding.Thickness = new Thickness({(int)p.Left}, {(int)p.Top}, {(int)p.Right}, {(int)p.Bottom});");
        }
    }

    private static void GenerateStyleSetup(CodeBuilder cb, StyleSpec style, string varName, bool flatNamespace = false)
    {
        // Type names differ between Terminal.Gui 2.0.0 (flat) and 2.0.1+ (split):
        // - Scheme type:  ColorScheme   (2.0.0)        vs Scheme                    (2.0.1+)
        // - Attribute:    Terminal.Gui.Attribute       vs Terminal.Gui.Drawing.Attribute
        // - Apply:        view.ColorScheme = ...       vs view.SetScheme(...)
        // - Read:         view.ColorScheme            vs view.GetScheme()
        var schemeType = flatNamespace ? "ColorScheme" : "Scheme";
        var attributeType = flatNamespace ? "Terminal.Gui.Attribute" : "Terminal.Gui.Drawing.Attribute";
        string ApplyScheme(string body) => flatNamespace
            ? $"{varName}.ColorScheme = {body};"
            : $"{varName}.SetScheme({body});";
        var readScheme = flatNamespace
            ? $"{varName}.ColorScheme"
            : $"{varName}.GetScheme()";

        // Foreground color
        if (style.Foreground != null)
        {
            var colorExpr = ColorToTerminalGui(style.Foreground.Value);
            var body = $"new {schemeType} {{ Normal = new {attributeType}({colorExpr}, {readScheme}?.Normal.Background ?? Color.Black) }}";
            cb.AppendLine(ApplyScheme(body));
        }

        // Background color
        if (style.Background != null)
        {
            var colorExpr = ColorToTerminalGui(style.Background.Value);
            var body = $"new {schemeType} {{ Normal = new {attributeType}({readScheme}?.Normal.Foreground ?? Color.White, {colorExpr}) }}";
            cb.AppendLine(ApplyScheme(body));
        }

        // Border
        if (style.Border != null)
        {
            var borderStyle = style.Border.Style switch
            {
                BorderStyle.SingleLine => "LineStyle.Single",
                BorderStyle.DoubleLine => "LineStyle.Double",
                BorderStyle.Rounded => "LineStyle.Rounded",
                BorderStyle.Thick => "LineStyle.Heavy",
                _ => "LineStyle.None"
            };
            cb.AppendLine($"{varName}.BorderStyle = {borderStyle};");
        }

        // Width/Height from style (override layout)
        if (style.Width != null)
        {
            var dimExpr = DimensionToTerminalGui(style.Width.Value, "Width");
            cb.AppendLine($"{varName}.Width = {dimExpr};");
        }

        if (style.Height != null)
        {
            var dimExpr = DimensionToTerminalGui(style.Height.Value, "Height");
            cb.AppendLine($"{varName}.Height = {dimExpr};");
        }
    }


    private static void GenerateComponentProperties(CodeBuilder cb, ComponentNode node, string varName)
    {
        // Handle static value property
        if (node.Properties.TryGetValue("value", out var valueProp) && !valueProp.IsBound)
        {
            switch (node.Type)
            {
                case ComponentType.Icon:
                case ComponentType.Label:
                    cb.AppendLine($"{varName}.Text = \"{EscapeString(valueProp.Value?.ToString() ?? "")}\";");
                    break;
                case ComponentType.ProgressBar:
                    cb.AppendLine($"{varName}.Fraction = {valueProp.Value ?? 0}f;");
                    break;
            }
        }

        // Handle static text property
        if (node.Properties.TryGetValue("text", out var textProp) && !textProp.IsBound)
        {
            cb.AppendLine($"{varName}.Text = \"{EscapeString(textProp.Value?.ToString() ?? "")}\";");
        }

        var commandPath = TryGetCommandBindingPath(node);
        if (commandPath != null && node.Type == ComponentType.Button)
        {
            var propertyName = commandPath.Replace(".", "");
            cb.OpenBlock($"{varName}.Accepting += (s, e) =>");
            cb.AppendLine($"if (_viewModel?.{propertyName} is global::System.Windows.Input.ICommand command)");
            using (cb.BlockScope())
            {
                cb.AppendLine("var param = (object?)null;");
                cb.AppendLine("if (command.CanExecute(param)) command.Execute(param);");
            }
            cb.AppendLine("e.Handled = true;");
            cb.AppendLine("RefreshBindings();");
            cb.CloseBlock(";");
        }
    }

    private static string? TryGetCommandBindingPath(ComponentNode node)
    {
        foreach (var binding in node.Bindings)
        {
            if (string.Equals(binding.Property, "command", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(binding.Key) ? binding.Key : binding.Path;
            }
        }

        return node.Command;
    }

    private static void GenerateRefreshBindingsMethod(
        CodeBuilder cb,
        HudDocument document,
        List<(string Id, string Type)> componentFields,
        Dictionary<string, (string FieldName, string AccessorName)> nameContext)
    {
        cb.AppendLine("public void RefreshBindings()");
        cb.OpenBlock();
        cb.AppendLine("if (_viewModel == null) return;");
        cb.AppendLine();

        GenerateBindingRefresh(cb, document.Root, nameContext);

        cb.CloseBlock();
    }

    private static void GenerateBindingRefresh(
        CodeBuilder cb,
        ComponentNode node,
        Dictionary<string, (string FieldName, string AccessorName)> nameContext)
    {
        if (node.Id != null)
        {
            var varName = ResolveFieldName(nameContext, node.Id);

            foreach (var binding in node.Bindings)
            {
                var member = !string.IsNullOrWhiteSpace(binding.Key) ? binding.Key! : binding.Path;
                var propertyName = member.Replace(".", "");
                var valueExpr = $"_viewModel.{propertyName}";

                switch (binding.Property.ToLowerInvariant())
                {
                    case "text":
                        if (!string.IsNullOrEmpty(binding.Format))
                        {
                            cb.AppendLine($"{varName}.Text = string.Format(\"{EscapeString(binding.Format)}\", {valueExpr});");
                        }
                        else
                        {
                            cb.AppendLine($"{varName}.Text = {valueExpr}?.ToString() ?? \"\";");
                        }
                        break;

                    case "value":
                        if (node.Type == ComponentType.ProgressBar)
                        {
                            cb.AppendLine($"{varName}.Fraction = Convert.ToSingle({valueExpr} ?? 0);");
                        }
                        else if (node.Type == ComponentType.Slider)
                        {
                            cb.AppendLine($"{varName}.Value = Convert.ToInt32({valueExpr} ?? 0);");
                        }
                        break;

                    case "visible":
                        cb.AppendLine($"{varName}.Visible = Convert.ToBoolean({valueExpr} ?? true);");
                        break;

                    case "enabled":
                        cb.AppendLine($"{varName}.Enabled = Convert.ToBoolean({valueExpr} ?? true);");
                        break;
                }
            }
        }

        // Recurse into children
        foreach (var child in node.Children)
        {
            GenerateBindingRefresh(cb, child, nameContext);
        }
    }

    private static void CollectComponentFields(
        ComponentNode node,
        List<(string Id, string Type)> fields,
        IReadOnlyDictionary<string, HudComponentDefinition>? components = null)
    {
        if (node.Id != null)
        {
            // When a node references a component definition the field's static
            // type must be the lifted `{componentDef.Name}View`, not the raw
            // node-type mapping (which would be `View` for Container, hiding
            // the synthetic component's accessors from consumers).
            string type;
            if (node.ComponentRefId != null
                && components != null
                && components.TryGetValue(node.ComponentRefId, out var componentDef))
            {
                type = $"{componentDef.Name}View";
            }
            else
            {
                type = MapComponentType(node.Type);
            }

            fields.Add((node.Id, type));
        }

        foreach (var child in node.Children)
        {
            CollectComponentFields(child, fields, components);
        }
    }

    private static readonly HashSet<string> ReservedFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "_viewModel",
        "_slots"
    };

    private static readonly HashSet<string> ReservedAccessorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ViewModel",
        "RefreshBindings",
        "FindSlot",
        "RegisterSlot",
        "InitializeComponents",
        "Dispose",
        "Title"
    };

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    private static Dictionary<string, (string FieldName, string AccessorName)> BuildNameContext(
        List<(string Id, string Type)> componentFields)
    {
        var context = new Dictionary<string, (string FieldName, string AccessorName)>(StringComparer.Ordinal);
        var usedFieldNames = new HashSet<string>(ReservedFieldNames, StringComparer.OrdinalIgnoreCase);
        var usedAccessorNames = new HashSet<string>(ReservedAccessorNames, StringComparer.OrdinalIgnoreCase);

        foreach (var (id, _) in componentFields)
        {
            if (context.ContainsKey(id))
            {
                continue;
            }

            var normalized = NormalizeIdentifier(id);
            var basePascal = ToPascalCase(normalized);
            var baseCamel = ToCamelCase(normalized);

            var fieldCandidate = $"_{baseCamel}";
            var accessorCandidate = basePascal;

            if (CSharpKeywords.Contains(accessorCandidate) || ReservedAccessorNames.Contains(accessorCandidate))
            {
                accessorCandidate = $"{accessorCandidate}Component";
            }

            var fieldName = EnsureUniqueName(fieldCandidate, usedFieldNames);
            var accessorName = EnsureUniqueName(accessorCandidate, usedAccessorNames);

            context[id] = (fieldName, accessorName);
        }

        return context;
    }

    private static string ResolveFieldName(
        Dictionary<string, (string FieldName, string AccessorName)> context,
        string id)
    {
        if (context.TryGetValue(id, out var names))
        {
            return names.FieldName;
        }

        var normalized = NormalizeIdentifier(id);
        return $"_{ToCamelCase(normalized)}";
    }

    private static string ResolveAccessorName(
        Dictionary<string, (string FieldName, string AccessorName)> context,
        string id)
    {
        if (context.TryGetValue(id, out var names))
        {
            return names.AccessorName;
        }

        var normalized = NormalizeIdentifier(id);
        return ToPascalCase(normalized);
    }

    private static string EnsureUniqueName(string baseName, HashSet<string> usedNames)
    {
        var candidate = baseName;
        var suffix = 2;
        while (usedNames.Contains(candidate))
        {
            candidate = $"{baseName}{suffix}";
            suffix++;
        }

        usedNames.Add(candidate);
        return candidate;
    }

    private static string NormalizeIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "component";
        }

        var sb = new StringBuilder(raw.Length);
        var uppercaseNext = false;

        foreach (var ch in raw)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                uppercaseNext = true;
                continue;
            }

            if (sb.Length == 0 && char.IsDigit(ch))
            {
                sb.Append('N');
            }

            if (uppercaseNext && char.IsLetter(ch))
            {
                sb.Append(char.ToUpperInvariant(ch));
            }
            else
            {
                sb.Append(ch);
            }

            uppercaseNext = false;
        }

        return sb.Length == 0 ? "component" : sb.ToString();
    }

    private static void CollectBindingPaths(ComponentNode node, HashSet<string> paths)
    {
        foreach (var binding in node.Bindings)
        {
            var member = !string.IsNullOrWhiteSpace(binding.Key) ? binding.Key : binding.Path;
            if (!string.IsNullOrEmpty(member))
                paths.Add(member);
        }

        if (!string.IsNullOrWhiteSpace(node.Command))
        {
            paths.Add(node.Command);
        }

        foreach (var child in node.Children)
        {
            CollectBindingPaths(child, paths);
        }
    }

    private static string MapComponentType(ComponentType type) => type switch
    {
        ComponentType.Label => "Label",
        ComponentType.Button => "Button",
        ComponentType.TextInput => "TextField",
        ComponentType.TextArea => "TextView",
        ComponentType.Checkbox => "CheckBox",
        ComponentType.RadioButton => "RadioGroup",
        ComponentType.ProgressBar => "ProgressBar",
        ComponentType.Slider => "Slider",
        ComponentType.Icon => "Label", // Icons are labels with emoji
        ComponentType.Image => "Label", // No image support, fallback to label
        ComponentType.MenuBar => "MenuBar",
        ComponentType.Menu => "Menu",
        ComponentType.MenuItem => "MenuItem",
        ComponentType.Container => "View",
        ComponentType.ScrollView => "ScrollView",
        ComponentType.Panel => "FrameView",
        ComponentType.TabView => "TabView",
        ComponentType.SplitView => "View", // No direct equivalent
        ComponentType.ListBox => "ListView",
        ComponentType.ListView => "ListView",
        ComponentType.TreeView => "TreeView",
        ComponentType.DataGrid => "TableView",
        ComponentType.Stack => "View",
        ComponentType.Grid => "View",
        ComponentType.Dock => "View",
        ComponentType.Spacer => "View",
        _ => "View"
    };

    private static string DimensionToTerminalGui(Dimension dim, string context)
    {
        if (dim.Unit == DimensionUnit.Pixels)
        {
            var value = (int)dim.Value;

            // Heuristic: If it's huge (like full screen), maybe Fill is still appropriate?
            // But usually we want scaling.
            // Figma 1440 width -> TUI ~120 chars (1440/12)
            // Figma 1024 height -> TUI ~42 lines (1024/24)

            if (context == "Width")
            {
                var scaled = Math.Max(1, value);
                return $"Dim.Absolute({scaled})";
            }

            if (context == "Height")
            {
                var scaled = Math.Max(1, value);
                return $"Dim.Absolute({scaled})";
            }
        }

        return dim.Unit switch
        {
            DimensionUnit.Pixels => $"Dim.Absolute({(int)dim.Value})", // Fallback, reachable?
            DimensionUnit.Cells => $"Dim.Absolute({(int)dim.Value})",
            DimensionUnit.Percent => $"Dim.Percent({(int)Math.Round(dim.Value)})",
            DimensionUnit.Star => $"Dim.Fill({(int)dim.Value})", // Star isn't exactly Fill(n) in TUI v1 but close enough for now
            DimensionUnit.Auto => "Dim.Auto()",
            DimensionUnit.Fill => "Dim.Fill()",
            _ => $"Dim.Absolute({(int)dim.Value})"
        };
    }

    private static string ColorToTerminalGui(Color color)
    {
        // Map to Terminal.Gui Color enum
        // Terminal.Gui uses 16-color palette primarily
        return (color.R, color.G, color.B) switch
        {
            (0, 0, 0) => "Color.Black",
            (255, 255, 255) => "Color.White",
            (255, 0, 0) => "Color.Red",
            (0, 255, 0) => "Color.Green",
            (0, 0, 255) => "Color.Blue",
            (255, 255, 0) => "Color.Yellow",
            (0, 255, 255) => "Color.Cyan",
            (255, 0, 255) => "Color.Magenta",
            (128, 128, 128) => "Color.Gray",
            (64, 64, 64) => "Color.DarkGray",
            (192, 192, 192) => "Color.White", // Light gray -> white
            _ => $"new Color({color.R}, {color.G}, {color.B})" // True color support
        };
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
