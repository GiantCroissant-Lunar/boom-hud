using System.Text;
using System.Security.Cryptography;
using System.IO;
using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;

namespace BoomHud.Gen.Godot;

/// <summary>
/// Code generator for Godot 4.x C#.
/// </summary>
public sealed class GodotGenerator : IBackendGenerator
{
    public string TargetFramework => "Godot 4.x";

    public ICapabilityManifest Capabilities => GodotCapabilities.Instance;

    public GenerationResult Generate(HudDocument document, GenerationOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var files = new List<GeneratedFile>();
        var prepared = GenerationDocumentPreprocessor.Prepare(document, options, "godot");
        document = prepared.Document;
        diagnostics.AddRange(prepared.Diagnostics);

        try
        {
            // 1. Generate Component Views (Partial Classes)
            foreach (var component in document.Components.Values)
            {
                var componentDoc = new HudDocument
                {
                    Name = component.Name,
                    Metadata = component.Metadata,
                    Root = component.Root,
                    Styles = document.Styles,
                    Components = document.Components
                };

                var code = GenerateViewClass(componentDoc, options, diagnostics, document.Components);
                files.Add(new GeneratedFile
                {
                    Path = $"{component.Name}View.cs",
                    Content = code,
                    Type = GeneratedFileType.SourceCode
                });

                if (options.EmitCompose)
                {
                    var composeCode = GenerateCompose(componentDoc, options, document.Components);
                    files.Add(new GeneratedFile
                    {
                        Path = $"{component.Name}View.Compose.g.cs",
                        Content = composeCode,
                        Type = GeneratedFileType.SourceCode
                    });
                }

                if (options.EmitTscn)
                {
                    var tscn = GenerateTscn(componentDoc, options, document.Components);
                    files.Add(new GeneratedFile
                    {
                        Path = $"{component.Name}View.tscn",
                        Content = tscn,
                        Type = GeneratedFileType.Other
                    });
                }

                // ViewModel Interfaces
                if (options.EmitViewModelInterfaces)
                {
                    var vmCode = GenerateViewModelInterface(componentDoc, options);
                    files.Add(new GeneratedFile
                    {
                        Path = $"I{component.Name}ViewModel.g.cs",
                        Content = vmCode,
                        Type = GeneratedFileType.SourceCode
                    });
                }
            }

            // 2. Generate Main Document View
            var mainCode = GenerateViewClass(document, options, diagnostics, document.Components);
            files.Add(new GeneratedFile
            {
                Path = $"{document.Name}View.cs",
                Content = mainCode,
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

            if (options.EmitTscn)
            {
                var tscn = GenerateTscn(document, options, document.Components);
                files.Add(new GeneratedFile
                {
                    Path = $"{document.Name}View.tscn",
                    Content = tscn,
                    Type = GeneratedFileType.Other
                });
            }

            // Main ViewModel Interface
            if (options.EmitViewModelInterfaces)
            {
                var mainVmCode = GenerateViewModelInterface(document, options);
                files.Add(new GeneratedFile
                {
                    Path = $"I{document.Name}ViewModel.g.cs",
                    Content = mainVmCode,
                    Type = GeneratedFileType.SourceCode
                });
            }

            if (options.Motion != null)
            {
                var motionResult = GodotMotionExporter.Generate(document, options.Motion, options);
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

    private static string GenerateTscn(HudDocument document, GenerationOptions options, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        // Scene-first Godot 4 scene generation.
        // - Root node attaches the generated C# script (so instances are the generated view type).
        // - Component references are emitted as PackedScene instances (so Compose helpers still work).
        // - Non-component nodes are emitted inline as a node tree.

        var sb = new StringBuilder();

        var nodeNames = AssignUniqueNames(document.Root);

        var rootType = MapComponentType(document.Root.Type);
        if (document.Root.Type == ComponentType.Container)
        {
            rootType = GetContainerType(document.Root.Layout?.Type ?? LayoutType.Absolute);
        }

        var rootNodeName = document.Name + "View";

        var extResources = new List<(string Type, string ResPath, string Id)>();

        var rootScriptResPath = options.EmitTscnAttachScript
            ? TryComputeGodotResPath(options.OutputDirectory, document.Name + "View.cs")
            : null;
        if (options.EmitTscnAttachScript && !string.IsNullOrWhiteSpace(rootScriptResPath))
        {
            extResources.Add(("Script", rootScriptResPath!, "1_root_script"));
        }

        // Component scene instances (PackedScene)
        var componentInstances = new List<(ComponentNode Node, HudComponentDefinition Def)>();
        CollectComponentInstances(document.Root, components, componentInstances);

        var uniqueComponentNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, def) in componentInstances)
        {
            if (!uniqueComponentNames.Add(def.Name))
            {
                continue;
            }

            var componentSceneResPath = TryComputeGodotResPath(options.OutputDirectory, def.Name + "View.tscn");
            if (!string.IsNullOrWhiteSpace(componentSceneResPath))
            {
                // Use a stable id string; it only needs to be unique within this file.
                extResources.Add(("PackedScene", componentSceneResPath!, "2_" + def.Name + "_scene"));
            }
        }

        sb.AppendLine("[gd_scene load_steps=" + (extResources.Count + 1).ToString(global::System.Globalization.CultureInfo.InvariantCulture) + " format=3]");
        sb.AppendLine();

        foreach (var res in extResources)
        {
            sb.AppendLine("[ext_resource type=\"" + EscapeString(res.Type) + "\" path=\"" + EscapeString(res.ResPath) + "\" id=\"" + EscapeString(res.Id) + "\"]");
        }

        if (extResources.Count > 0)
        {
            sb.AppendLine();
        }

        // Root node
        sb.AppendLine("[node name=\"" + EscapeString(rootNodeName) + "\" type=\"" + EscapeString(rootType) + "\"]");
        if (options.EmitTscnAttachScript && !string.IsNullOrWhiteSpace(rootScriptResPath))
        {
            sb.AppendLine("script = ExtResource(\"1_root_script\")");
        }

        AppendCommonNodeProperties(sb, document.Root);

        // Inline children
        AppendChildNodesTscn(
            sb,
            parentNode: document.Root,
            parentPath: ".",
            nodeNames,
            components,
            extResources);

        return sb.ToString();
    }

    private static void AppendChildNodesTscn(
        StringBuilder sb,
        ComponentNode parentNode,
        string parentPath,
        Dictionary<ComponentNode, string> nodeNames,
        IReadOnlyDictionary<string, HudComponentDefinition> components,
        List<(string Type, string ResPath, string Id)> extResources)
    {
        foreach (var child in parentNode.Children)
        {
            // Menu items are not nodes in Godot; they are items on an OptionButton.
            if (child.Type == ComponentType.MenuItem)
            {
                continue;
            }

            var childNodeName = GetSceneNodeName(child, nodeNames, components);

            sb.AppendLine();

            if (child.ComponentRefId != null && components.TryGetValue(child.ComponentRefId, out var def))
            {
                // Emit as a PackedScene instance.
                var instanceId = FindPackedSceneId(extResources, def.Name);
                if (instanceId != null)
                {
                    sb.AppendLine("[node name=\"" + EscapeString(childNodeName) + "\" parent=\"" + EscapeString(parentPath) + "\" instance=ExtResource(\"" + EscapeString(instanceId) + "\")]");
                    AppendCommonNodeProperties(sb, child);
                    continue;
                }
            }

            var childType = MapComponentType(child.Type);
            if (child.Type == ComponentType.Container)
            {
                childType = GetContainerType(child.Layout?.Type ?? LayoutType.Vertical);
            }

            sb.AppendLine("[node name=\"" + EscapeString(childNodeName) + "\" type=\"" + EscapeString(childType) + "\" parent=\"" + EscapeString(parentPath) + "\"]");
            AppendCommonNodeProperties(sb, child);

            var nextParentPath = parentPath == "." ? childNodeName : parentPath + "/" + childNodeName;
            AppendChildNodesTscn(sb, child, nextParentPath, nodeNames, components, extResources);
        }
    }

    private static void AppendCommonNodeProperties(StringBuilder sb, ComponentNode node)
    {
        if (!node.Visible.IsBound && node.Visible.Value is false)
        {
            sb.AppendLine("visible = false");
        }

        if (!node.Enabled.IsBound && node.Enabled.Value is false)
        {
            // Emit opportunistically; Godot ignores unknown properties.
            sb.AppendLine("disabled = true");
        }

        // Keep static text in the scene to make it editable in Godot if desired.
        if (TryGetUnboundStringProperty(node, "Text", out var text))
        {
            if (node.Type == ComponentType.Label || node.Type == ComponentType.Button)
            {
                sb.AppendLine("text = \"" + EscapeString(text) + "\"");
            }
        }

        AppendAbsoluteLayoutProperties(sb, node.Layout);
    }

    private static void AppendAbsoluteLayoutProperties(StringBuilder sb, LayoutSpec? layout)
    {
        if (layout == null || layout.Type != LayoutType.Absolute)
        {
            return;
        }

        AppendTscnOffset(sb, layout.Left, "anchor_left", "offset_left");
        AppendTscnOffset(sb, layout.Top, "anchor_top", "offset_top");
    }

    private static void AppendTscnOffset(StringBuilder sb, Dimension? dimension, string anchorProperty, string offsetProperty)
    {
        if (dimension == null)
        {
            sb.Append(anchorProperty).AppendLine(" = 0.0");
            sb.Append(offsetProperty).AppendLine(" = 0");
            return;
        }

        if (dimension.Value.Unit == DimensionUnit.Percent)
        {
            sb.Append(anchorProperty)
                .Append(" = ")
                .AppendLine(((double)(dimension.Value.Value / 100.0)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(offsetProperty).AppendLine(" = 0");
            return;
        }

        sb.Append(anchorProperty).AppendLine(" = 0.0");
        sb.Append(offsetProperty)
            .Append(" = ")
            .AppendLine(((float)dimension.Value.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static bool TryGetUnboundStringProperty(ComponentNode node, string key, out string value)
    {
        foreach (var kvp in node.Properties)
        {
            if (!string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var prop = kvp.Value;
            if (prop.IsBound || prop.Value is not string s)
            {
                continue;
            }

            value = s;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? FindPackedSceneId(List<(string Type, string ResPath, string Id)> extResources, string componentName)
    {
        var expected = "2_" + componentName + "_scene";
        foreach (var res in extResources)
        {
            if (string.Equals(res.Id, expected, StringComparison.Ordinal))
            {
                return res.Id;
            }
        }

        return null;
    }

    private static string GetSceneNodeName(ComponentNode node, Dictionary<ComponentNode, string> nodeNames, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        if (node.ComponentRefId != null && components.TryGetValue(node.ComponentRefId, out var def))
        {
            // Compose expects to find component instances by SlotKey (or Id as fallback).
            return node.SlotKey ?? node.Id ?? def.Name;
        }

        var typeName = MapComponentType(node.Type);
        if (node.Type == ComponentType.Container)
        {
            typeName = GetContainerType(node.Layout?.Type ?? LayoutType.Vertical);
        }

        // Prefer slot key / ID; fall back to unique name (best-effort).
        if (!string.IsNullOrWhiteSpace(node.SlotKey))
        {
            return node.SlotKey!;
        }

        if (!string.IsNullOrWhiteSpace(node.Id))
        {
            return node.Id!;
        }

        return nodeNames.TryGetValue(node, out var unique) ? unique : typeName;
    }

    private static string? TryComputeGodotResPath(string outputDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return null;
        }

        var projectRoot = TryFindGodotProjectRoot(outputDirectory);
        if (projectRoot == null)
        {
            return null;
        }

        var diskPath = Path.Combine(outputDirectory, fileName);
        if (!File.Exists(diskPath))
        {
            // The file may be emitted in the same generation run; still compute the res path.
        }

        var rel = Path.GetRelativePath(projectRoot.FullName, diskPath);
        rel = rel.Replace('\\', '/');
        return "res://" + rel;
    }

    private static DirectoryInfo? TryFindGodotProjectRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            var projectFile = Path.Combine(dir.FullName, "project.godot");
            if (File.Exists(projectFile))
            {
                return dir;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string GenerateViewClass(HudDocument document, GenerationOptions options, List<Diagnostic> diagnostics, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        var cb = new CodeBuilder();

        var viewModelNamespace = options.ViewModelNamespace ?? options.Namespace;
        var sourceId = ComputeSourceId(document);
        var contractId = options.ContractId ?? string.Empty;

        var normalizedPseudoNodes = CollectNormalizedPseudoNodes(document);

        // Pre-calculation: Assign unique variable names to all nodes to avoid collisions
        var nodeNames = AssignUniqueNames(document.Root);

        if (options.IncludeComments)
        {
            cb.AppendLine("// <auto-generated>");
            cb.AppendLine($"// Generated by BoomHud.Gen.Godot from {document.Name}");
            cb.AppendLine("// </auto-generated>");
            cb.AppendLine();
        }

        if (options.UseNullableAnnotations)
        {
            cb.AppendLine("#nullable enable");
            cb.AppendLine();
        }

        cb.AppendLine("using Godot;");
        cb.AppendLine("using System;");
        cb.AppendLine("using System.ComponentModel;");

        if (!string.Equals(viewModelNamespace, options.Namespace, StringComparison.Ordinal))
        {
            cb.AppendLine($"using {viewModelNamespace};");
        }
        cb.AppendLine();

        cb.AppendLine($"namespace {options.Namespace};");
        cb.AppendLine();

        var baseClass = MapComponentType(document.Root.Type);
        // If root is a generic container, default to Control or specific layout container
        if (document.Root.Type == ComponentType.Container)
        {
            baseClass = GetContainerType(document.Root.Layout?.Type ?? LayoutType.Absolute);
        }

        cb.AppendLine($"public partial class {document.Name}View : {baseClass}");
        cb.OpenBlock();

        cb.AppendLine($"public const string BoomHudSourceId = \"{sourceId}\";");
        cb.AppendLine($"public const string BoomHudContractId = \"{EscapeString(contractId)}\";");
        cb.AppendLine($"public static readonly string[] BoomHudNormalizedPseudoNodes = {FormatStringArrayLiteral(normalizedPseudoNodes)};");
        cb.AppendLine();

        // ViewModel field
        cb.AppendLine($"private I{document.Name}ViewModel? _viewModel;");
        cb.AppendLine();

        // Control references
        // Only generate fields for nodes that have IDs (even if we uniquified them, only original named nodes typically get fields, 
        // but here we generate fields for everything that was named in Figma/Schema)
        // Actually, AssignUniqueNames assigns names to everything. 
        // We should generate fields for everything that had an original ID, using the NEW unique name.
        var componentFields = new List<(string OriginalId, string UniqueName, string Type)>();
        CollectComponentFields(document.Root, nodeNames, componentFields);

        if (componentFields.Count > 0)
        {
            cb.AppendLine("// Control references");
            foreach (var (_, uniqueName, type) in componentFields)
            {
                cb.AppendLine($"private {type} _{uniqueName} = null!;");
            }
            cb.AppendLine();
        }

        // _Ready implementation
        cb.AppendLine("public override void _Ready()");
        cb.OpenBlock();
        cb.AppendLine("base._Ready();");
        if (options.EmitTscn)
        {
            cb.AppendLine("BindUiFromScene();");
            cb.AppendLine("InitializeUiFromScene();");
        }
        else
        {
            cb.AppendLine("BuildUi();");
        }
        cb.CloseBlock();

        if (options.EmitTscn)
        {
            // Scene-backed binding
            cb.AppendLine();
            cb.AppendLine("private void BindUiFromScene()");
            cb.OpenBlock();

            // Root field (if present) can safely point to this
            if (nodeNames.TryGetValue(document.Root, out var rootUniqueName))
            {
                cb.AppendLine($"_{rootUniqueName} = this;");
            }

            var sceneBindings = new List<(ComponentNode Node, string Path, string TypeName, string UniqueName)>();

            void TraverseForBindings(ComponentNode node, string parentPath)
            {
                foreach (var child in node.Children)
                {
                    if (child.Type == ComponentType.MenuItem)
                    {
                        continue;
                    }

                    if (!nodeNames.TryGetValue(child, out var uniqueName))
                    {
                        continue;
                    }

                    var segment = GetSceneNodeName(child, nodeNames, components);
                    var path = string.IsNullOrEmpty(parentPath) ? segment : parentPath + "/" + segment;

                    var typeName = MapComponentType(child.Type);
                    if (child.ComponentRefId != null && components.TryGetValue(child.ComponentRefId, out var componentDef))
                    {
                        typeName = $"{componentDef.Name}View";
                    }
                    else if (child.Type == ComponentType.Container)
                    {
                        typeName = GetContainerType(child.Layout?.Type ?? LayoutType.Vertical);
                    }

                    sceneBindings.Add((child, path, typeName, uniqueName));

                    // Recurse only for non-component references; child view handles its own structure.
                    if (child.ComponentRefId == null)
                    {
                        TraverseForBindings(child, path);
                    }
                }
            }

            TraverseForBindings(document.Root, parentPath: string.Empty);

            foreach (var b in sceneBindings)
            {
                cb.AppendLine($"_{b.UniqueName} = GetNode<{b.TypeName}>(\"{EscapeString(b.Path)}\");");
            }

            cb.CloseBlock();

            cb.AppendLine();
            cb.AppendLine("private void InitializeUiFromScene()");
            cb.OpenBlock();
            GenerateRootSetup(cb, document.Root, nodeNames, diagnostics);
            GenerateChildrenSetupFromScene(cb, document.Root, "this", nodeNames, diagnostics, components);
            cb.CloseBlock();
        }

        // BuildUi implementation
        cb.AppendLine();
        cb.AppendLine("private void BuildUi()");
        cb.OpenBlock();

        // Root setup (this)
        GenerateRootSetup(cb, document.Root, nodeNames, diagnostics);

        // Children setup
        // Note: For the root node, we are adding children to *this*
        GenerateChildrenSetup(cb, document.Root, "this", nodeNames, diagnostics, components);

        cb.CloseBlock();

        // SetViewModel implementation
        cb.AppendLine();
        cb.AppendLine($"public void SetViewModel(I{document.Name}ViewModel? viewModel)");
        cb.OpenBlock();
        cb.AppendLine("if (_viewModel != null)");
        cb.OpenBlock();
        cb.AppendLine("_viewModel.PropertyChanged -= OnViewModelPropertyChanged;");
        cb.CloseBlock();
        cb.AppendLine();
        cb.AppendLine("_viewModel = viewModel;");
        cb.AppendLine();
        cb.AppendLine("if (_viewModel != null)");
        cb.OpenBlock();
        cb.AppendLine("_viewModel.PropertyChanged += OnViewModelPropertyChanged;");
        cb.AppendLine("UpdateAllBindings();");
        cb.CloseBlock();
        cb.CloseBlock();

        // PropertyChanged Handler
        cb.AppendLine();
        cb.AppendLine("private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)");
        cb.OpenBlock();
        cb.AppendLine("if (_viewModel == null) return;");
        cb.AppendLine();

        var bindings = new List<(string PropertyName, List<BindingOp> Ops)>();
        CollectBindings(document.Root, nodeNames, bindings);

        if (bindings.Count > 0)
        {
            cb.AppendLine("switch (e.PropertyName)");
            cb.OpenBlock();

            foreach (var group in bindings)
            {
                cb.AppendLine($"case \"{group.PropertyName}\":");
                cb.Indent();
                foreach (var op in group.Ops)
                {
                    GenerateBindingAssignment(cb, op);
                }
                cb.AppendLine("break;");
                cb.Outdent();
            }

            cb.CloseBlock();
        }

        cb.CloseBlock();

        // UpdateAllBindings
        cb.AppendLine();
        cb.AppendLine("private void UpdateAllBindings()");
        cb.OpenBlock();
        cb.AppendLine("if (_viewModel == null) return;");
        cb.AppendLine();

        foreach (var group in bindings)
        {
            foreach (var op in group.Ops)
            {
                GenerateBindingAssignment(cb, op);
            }
        }

        cb.CloseBlock();

        // ApplyVmJson - for snapshot rendering without full ViewModel
        cb.AppendLine();
        cb.AppendLine("/// <summary>");
        cb.AppendLine("/// Apply VM state from JSON for snapshot rendering.");
        cb.AppendLine("/// This allows setting UI state without a full ViewModel implementation.");
        cb.AppendLine("/// </summary>");
        cb.AppendLine("public void ApplyVmJson(string json)");
        cb.OpenBlock();
        cb.AppendLine("if (string.IsNullOrEmpty(json)) return;");
        cb.AppendLine();
        cb.AppendLine("try");
        cb.OpenBlock();
        cb.AppendLine("var doc = System.Text.Json.JsonDocument.Parse(json);");
        cb.AppendLine("ApplyVmFromJsonElement(doc.RootElement);");
        cb.CloseBlock();
        cb.AppendLine("catch (System.Text.Json.JsonException ex)");
        cb.OpenBlock();
        cb.AppendLine("GD.PrintErr($\"ApplyVmJson failed to parse JSON: {ex.Message}\");");
        cb.CloseBlock();
        cb.CloseBlock();

        // ApplyVmFromJsonElement - recursive helper
        cb.AppendLine();
        cb.AppendLine("private void ApplyVmFromJsonElement(System.Text.Json.JsonElement element, string prefix = \"\")");
        cb.OpenBlock();
        cb.AppendLine("if (element.ValueKind != System.Text.Json.JsonValueKind.Object) return;");
        cb.AppendLine();
        cb.AppendLine("foreach (var prop in element.EnumerateObject())");
        cb.OpenBlock();
        cb.AppendLine("var path = string.IsNullOrEmpty(prefix) ? prop.Name : $\"{prefix}.{prop.Name}\";");
        cb.AppendLine();
        cb.AppendLine("if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object)");
        cb.OpenBlock();
        cb.AppendLine("ApplyVmFromJsonElement(prop.Value, path);");
        cb.CloseBlock();
        cb.AppendLine("else");
        cb.OpenBlock();
        cb.AppendLine("ApplyVmProperty(path, prop.Value);");
        cb.CloseBlock();
        cb.CloseBlock();
        cb.CloseBlock();

        // ApplyVmProperty - applies a single property value
        cb.AppendLine();
        cb.AppendLine("private void ApplyVmProperty(string path, System.Text.Json.JsonElement value)");
        cb.OpenBlock();

        if (bindings.Count > 0)
        {
            cb.AppendLine("// Map VM property paths to control updates");
            cb.AppendLine("switch (path)");
            cb.OpenBlock();

            // Group bindings by their full path (including nested like "Debug.Enabled")
            var pathBindings = new Dictionary<string, List<BindingOp>>();
            foreach (var group in bindings)
            {
                foreach (var op in group.Ops)
                {
                    // The ViewModelProperty is the key used in switch, but we need the full path
                    // For nested properties like "Debug.Enabled", the key is just "Enabled"
                    // We need to reconstruct the path from the binding
                    var key = op.ViewModelProperty;
                    if (!pathBindings.ContainsKey(key))
                        pathBindings[key] = new List<BindingOp>();
                    pathBindings[key].Add(op);
                }
            }

            foreach (var (path, ops) in pathBindings)
            {
                cb.AppendLine($"case \"{path}\":");
                cb.Indent();
                foreach (var op in ops)
                {
                    GenerateJsonValueAssignment(cb, op);
                }
                cb.AppendLine("break;");
                cb.Outdent();
            }

            cb.CloseBlock();
        }
        else
        {
            cb.AppendLine("// No bindings defined");
            cb.AppendLine("_ = path; _ = value;");
        }

        cb.CloseBlock();

        // Public accessors
        if (componentFields.Count > 0)
        {
            cb.AppendLine();
            cb.AppendLine("// Component accessors");
            foreach (var (originalId, uniqueName, type) in componentFields)
            {
                // Use the UNIQUE name for the property to ensure uniqueness, 
                // but maybe we want to preserve the original ID for the property name if possible?
                // No, if original IDs collided, property names would collide.
                // So use UniqueName (PascalCased).
                cb.AppendLine($"public {type} {ToPascalCase(uniqueName)} => _{uniqueName};");
            }
        }

        cb.CloseBlock(); // class

        return cb.ToString();
    }

    private static void GenerateJsonValueAssignment(CodeBuilder cb, BindingOp op)
    {
        // Generate code to assign a JsonElement value to a control property
        var targetProp = op.TargetProperty;

        switch (targetProp)
        {
            case "Text":
            case "content": // Pencil DSL convention maps to Text in Godot
                cb.AppendLine($"{op.ControlName}.Text = value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() ?? \"\" : value.ToString();");
                break;
            case "Visible":
            case "visible":
                cb.AppendLine($"{op.ControlName}.Visible = value.ValueKind == System.Text.Json.JsonValueKind.True;");
                break;
            case "Value":
            case "value":
                cb.AppendLine($"{op.ControlName}.Value = value.TryGetDouble(out var dval) ? dval : 0;");
                break;
            case "ButtonPressed": // Godot CheckBox
            case "checked":
                cb.AppendLine($"{op.ControlName}.ButtonPressed = value.ValueKind == System.Text.Json.JsonValueKind.True;");
                break;
            default:
                // Generic string assignment for unknown properties
                cb.AppendLine($"// Unsupported binding target: {targetProp}");
                cb.AppendLine($"// {op.ControlName}.{targetProp} = ...;");
                break;
        }
    }

    private static Dictionary<ComponentNode, string> AssignUniqueNames(ComponentNode root)
    {
        var names = new Dictionary<ComponentNode, string>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Helper to traverse
        void Traverse(ComponentNode node)
        {
            var baseName = node.Id;
            if (string.IsNullOrEmpty(baseName))
            {
                // Use type name as fallback
                baseName = MapComponentType(node.Type);
            }

            baseName = ToCamelCase(SanitizeIdentifier(baseName)); // Ensure valid identifier start

            var candidate = baseName;
            int counter = 1;
            while (usedNames.Contains(candidate))
            {
                candidate = $"{baseName}{counter++}";
            }

            usedNames.Add(candidate);
            names[node] = candidate;

            foreach (var child in node.Children)
            {
                Traverse(child);
            }
        }

        Traverse(root);
        return names;
    }

    private static string SanitizeIdentifier(string name)
    {
        // Simple sanitization
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
        }
        var s = sb.ToString();
        if (string.IsNullOrEmpty(s)) return "elem";
        if (char.IsDigit(s[0])) return "_" + s;
        return s;
    }

    private static void GenerateRootSetup(CodeBuilder cb, ComponentNode node, Dictionary<ComponentNode, string> nodeNames, List<Diagnostic> diagnostics)
    {
        // Setup properties on 'this' (the root control)
        GenerateLayoutSetup(cb, node.Layout, "this", null); // Root has no parent layout
        GenerateStyleSetup(cb, node.Style, "this");
        GenerateComponentProperties(cb, node, "this", isRoot: true);
    }

    private static void GenerateChildrenSetup(CodeBuilder cb, ComponentNode parentNode, string parentVar, Dictionary<ComponentNode, string> nodeNames, List<Diagnostic> diagnostics, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        var parentLayoutType = parentNode.Layout?.Type ?? LayoutType.Vertical; // Default logic

        foreach (var child in parentNode.Children)
        {
            GenerateChildComponent(cb, child, parentVar, nodeNames, diagnostics, parentLayoutType, components);
        }
    }

    private static void GenerateChildrenSetupFromScene(CodeBuilder cb, ComponentNode parentNode, string parentVar, Dictionary<ComponentNode, string> nodeNames, List<Diagnostic> diagnostics, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        var parentLayoutType = parentNode.Layout?.Type ?? LayoutType.Vertical;

        foreach (var child in parentNode.Children)
        {
            GenerateChildComponentFromScene(cb, child, parentVar, nodeNames, diagnostics, parentLayoutType, components);
        }
    }

    private static void GenerateChildComponentFromScene(CodeBuilder cb, ComponentNode node, string parentVar, Dictionary<ComponentNode, string> nodeNames, List<Diagnostic> diagnostics, LayoutType parentLayoutType, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        if (node.Type == ComponentType.MenuItem)
        {
            var text = "Item";
            if (node.Properties.TryGetValue("Text", out var textProp) && textProp.Value is string s)
            {
                text = s;
            }

            var itemId = node.Id?.GetHashCode() ?? Guid.NewGuid().GetHashCode();
            cb.AppendLine(parentVar + ".AddItem(\"" + EscapeString(text) + "\", (int)" + itemId.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + ");");
            return;
        }

        var typeName = MapComponentType(node.Type);
        if (node.ComponentRefId != null && components.TryGetValue(node.ComponentRefId, out var componentDef))
        {
            typeName = componentDef.Name + "View";
        }
        if (node.Type == ComponentType.Container)
        {
            typeName = GetContainerType(node.Layout?.Type ?? LayoutType.Vertical);
        }

        if (!nodeNames.TryGetValue(node, out var uniqueName))
        {
            uniqueName = string.Concat("c", Guid.NewGuid().ToString("N").AsSpan(0, 8));
        }

        // In scene-backed mode, we only configure existing nodes; the fields are bound via BindUiFromScene().
        var varName = "_" + uniqueName;

        cb.AppendLine();

        // Layout
        GenerateLayoutSetup(cb, node.Layout, varName, parentLayoutType);

        // Style
        GenerateStyleSetup(cb, node.Style, varName);

        // Properties (includes command/event hookups)
        GenerateComponentProperties(cb, node, varName, isRoot: false);

        // Recurse (skip for component references; child view handles its own structure)
        if (node.ComponentRefId == null)
        {
            GenerateChildrenSetupFromScene(cb, node, varName, nodeNames, diagnostics, components);
        }
    }

    private static void GenerateChildComponent(CodeBuilder cb, ComponentNode node, string parentVar, Dictionary<ComponentNode, string> nodeNames, List<Diagnostic> diagnostics, LayoutType parentLayoutType, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        if (node.Type == ComponentType.MenuItem)
        {
            var text = "Item";
            if (node.Properties.TryGetValue("Text", out var textProp) && textProp.Value is string s)
            {
                text = s;
            }

            // Use a stable ID if possible, otherwise random is risky for regeneration but okay for runtime init
            var itemId = node.Id?.GetHashCode() ?? Guid.NewGuid().GetHashCode();

            cb.AppendLine($"{parentVar}.AddItem(\"{text}\", (int){itemId});");
            return;
        }

        var typeName = MapComponentType(node.Type);
        if (node.ComponentRefId != null && components.TryGetValue(node.ComponentRefId, out var componentDef))
        {
            typeName = $"{componentDef.Name}View";
        }
        if (node.Type == ComponentType.Container)
        {
            typeName = GetContainerType(node.Layout?.Type ?? LayoutType.Vertical);
        }

        if (!nodeNames.TryGetValue(node, out var uniqueName))
        {
            uniqueName = $"c{Guid.NewGuid():N}".Substring(0, 8);
        }

        var isField = node.Id != null;
        var varName = isField ? $"_{uniqueName}" : uniqueName;

        cb.AppendLine();
        if (isField)
        {
            cb.AppendLine($"{varName} = new {typeName}();");
        }
        else
        {
            cb.AppendLine($"var {varName} = new {typeName}();");
        }

        cb.AppendLine($"{varName}.Name = \"{node.SlotKey ?? node.Id ?? typeName}\";");

        // Layout
        GenerateLayoutSetup(cb, node.Layout, varName, parentLayoutType);

        // Style
        GenerateStyleSetup(cb, node.Style, varName);

        // Properties
        GenerateComponentProperties(cb, node, varName, isRoot: false);

        // Add to parent
        cb.AppendLine($"{parentVar}.AddChild({varName});");

        // Recurse (skip for component references; child view handles its own structure)
        if (node.ComponentRefId == null)
        {
            GenerateChildrenSetup(cb, node, varName, nodeNames, diagnostics, components);
        }
    }

    private static string GenerateCompose(HudDocument document, GenerationOptions options, IReadOnlyDictionary<string, HudComponentDefinition> components)
    {
        var cb = new CodeBuilder();
        var viewModelNamespace = options.ViewModelNamespace ?? options.Namespace;

        cb.AppendLine("#nullable enable");
        cb.AppendLine();
        cb.AppendLine("using Godot;");
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
        cb.AppendLine("root.SetViewModel(vm);");
        cb.AppendLine("d.Add(new DisposableAction(() => root.SetViewModel(null)));");

        var nodeNames = AssignUniqueNames(document.Root);
        var childInstances = new List<(ComponentNode Node, HudComponentDefinition Def)>();
        CollectComponentInstances(document.Root, components, childInstances);

        foreach (var (node, def) in childInstances)
        {
            var slotKey = node.SlotKey ?? node.Id ?? def.Name;
            cb.AppendLine();
            cb.AppendLine($"var childView = root.GetNodeOrNull<{def.Name}View>(\"{EscapeString(slotKey)}\");");
            cb.AppendLine($"if (childView == null) throw new InvalidOperationException(\"Could not find child node: \" + \"{EscapeString(slotKey)}\");");
            cb.AppendLine($"var childVm = resolver.Resolve<I{def.Name}ViewModel>(vm, \"{EscapeString(slotKey)}\");");
            cb.AppendLine("childView.SetViewModel(childVm);");
            cb.AppendLine("d.Add(new DisposableAction(() => childView.SetViewModel(null)));");
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

        return "new[] { " + string.Join(", ", items.Select(s => "\"" + EscapeString(s) + "\"")) + " }";
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
            hex.Append(b.ToString("x2", global::System.Globalization.CultureInfo.InvariantCulture));
        }
        return hex.ToString();
    }

    private static void GenerateLayoutSetup(CodeBuilder cb, LayoutSpec? layout, string varName, LayoutType? parentLayoutType)
    {
        if (layout == null) return;
        var isAbsoluteLayout = layout.Type == LayoutType.Absolute;

        // Godot sizing logic:
        // SizeFlags: Used inside Containers (VBox, HBox, Grid)
        // Anchors/Offsets: Used inside Control (Absolute, Dock)

        if (isAbsoluteLayout)
        {
            cb.AppendLine($"if ((object){varName} is Control c)");
            cb.OpenBlock();
            AppendAbsoluteOffset(cb, "c", layout.Left, "AnchorLeft", "OffsetLeft");
            AppendAbsoluteOffset(cb, "c", layout.Top, "AnchorTop", "OffsetTop");
            cb.CloseBlock();
        }

        // 1. Min/Custom Size + 2. Size Flags (Control-only properties)
        var needsControlLayout =
            (layout.Width != null && layout.Width.Value.Unit == DimensionUnit.Pixels)
            || (layout.Height != null && layout.Height.Value.Unit == DimensionUnit.Pixels)
            || (parentLayoutType != null && parentLayoutType != LayoutType.Absolute && parentLayoutType != LayoutType.Dock);

        if (needsControlLayout)
        {
            cb.AppendLine("{");
            cb.AppendLine($"if ((object){varName} is Control c)");
            cb.OpenBlock();

            if (layout.Width != null && layout.Width.Value.Unit == DimensionUnit.Pixels)
            {
                cb.AppendLine($"c.CustomMinimumSize = new Vector2({(float)layout.Width.Value.Value}, c.CustomMinimumSize.Y);");
            }
            if (layout.Height != null && layout.Height.Value.Unit == DimensionUnit.Pixels)
            {
                cb.AppendLine($"c.CustomMinimumSize = new Vector2(c.CustomMinimumSize.X, {(float)layout.Height.Value.Value});");
            }

            // Size Flags (if inside a Container)
            if (parentLayoutType != null && parentLayoutType != LayoutType.Absolute && parentLayoutType != LayoutType.Dock)
            {
                // Horizontal Flags
                var hFlag = "SizeFlags.ShrinkCenter"; // Default?
                if (layout.Width?.Unit == DimensionUnit.Fill || layout.Width?.Unit == DimensionUnit.Star)
                {
                    hFlag = "SizeFlags.ExpandFill";
                }
                else if (layout.Align == Alignment.Start || layout.Justify == Justification.Start) hFlag = "SizeFlags.ShrinkBegin";
                else if (layout.Align == Alignment.End || layout.Justify == Justification.End) hFlag = "SizeFlags.ShrinkEnd";
                else if (layout.Align == Alignment.Center || layout.Justify == Justification.Center) hFlag = "SizeFlags.ShrinkCenter";
                else if (layout.Align == Alignment.Stretch) hFlag = "SizeFlags.Fill";

                cb.AppendLine($"c.SizeFlagsHorizontal = Control.{hFlag};");

                // Vertical Flags
                var vFlag = "SizeFlags.ShrinkCenter";
                if (layout.Height?.Unit == DimensionUnit.Fill || layout.Height?.Unit == DimensionUnit.Star)
                {
                    vFlag = "SizeFlags.ExpandFill";
                }
                // Logic differs slightly depending on VBox vs HBox but ExpandFill usually what we want for 'Fill'
                cb.AppendLine($"c.SizeFlagsVertical = Control.{vFlag};");

                // Flex weight (Stretch Ratio)
                if (layout.Weight.HasValue)
                {
                    cb.AppendLine($"c.SizeFlagsStretchRatio = {layout.Weight.Value}f;");
                }
            }

            cb.CloseBlock();
            cb.AppendLine("}");
        }

        // 3. Gap (Separation)
        // In Godot, separation is a property of the PARENT container, but here we are configuring the CHILD node/layout.
        // Wait, 'Gap' in BoomHud LayoutSpec usually refers to the gap *this container* applies to its children.
        // So this logic belongs on the container setup, not the child.
        if (layout.Gap != null)
        {
            var gap = (int)layout.Gap.Value.Top; // Simplify to uniform/primary axis for now
                                                 // We need to check if 'varName' is actually a BoxContainer or GridContainer
                                                 // Since we don't have the type info easily here without casting, we can try adding an override or setting property if we know the type.
                                                 // But MapComponentType/GetContainerType is string-based.
                                                 // Use 'AddThemeConstantOverride' which works on any Control, though it only affects Containers if they look for it.
                                                 // VBox/HBox look for "separation", Grid looks for "h_separation"/"v_separation".

            cb.AppendLine($"{varName}.AddThemeConstantOverride(\"separation\", {gap});");
            cb.AppendLine($"{varName}.AddThemeConstantOverride(\"h_separation\", {gap});");
            cb.AppendLine($"{varName}.AddThemeConstantOverride(\"v_separation\", {gap});");
        }

        // 4. Padding (MarginContainer logic?)
        // If this node IS a container, we might want to wrap its children or use ContentMargin overrides.
        // Godot Controls don't support Padding natively except MarginContainer.
        // This is tricky without wrapping. For now, we might skip padding or assume the user uses a MarginContainer explicitly?
        // OR, we assume 'varName' IS a MarginContainer if padding is present? 
        // BoomHud 'Container' + Padding -> MarginContainer?
        // Let's rely on AddThemeConstantOverride("margin_...") which works for MarginContainer.
        if (layout.Padding != null)
        {
            var p = layout.Padding.Value;
            cb.AppendLine($"{varName}.AddThemeConstantOverride(\"margin_left\", {(int)p.Left});");
            cb.AppendLine($"{varName}.AddThemeConstantOverride(\"margin_top\", {(int)p.Top});");
            cb.AppendLine($"{varName}.AddThemeConstantOverride(\"margin_right\", {(int)p.Right});");
            cb.AppendLine($"{varName}.AddThemeConstantOverride(\"margin_bottom\", {(int)p.Bottom});");
        }
    }

    private static void AppendAbsoluteOffset(CodeBuilder cb, string controlName, Dimension? offset, string anchorProperty, string positionProperty)
    {
        if (offset == null)
        {
            cb.AppendLine($"{controlName}.{anchorProperty} = 0f;");
            cb.AppendLine($"{controlName}.{positionProperty} = 0f;");
            return;
        }

        switch (offset.Value.Unit)
        {
            case DimensionUnit.Percent:
                cb.AppendLine($"{controlName}.{anchorProperty} = {(float)(offset.Value.Value / 100.0)}f;");
                cb.AppendLine($"{controlName}.{positionProperty} = 0f;");
                break;

            case DimensionUnit.Pixels:
            case DimensionUnit.Cells:
            default:
                cb.AppendLine($"{controlName}.{anchorProperty} = 0f;");
                cb.AppendLine($"{controlName}.{positionProperty} = {(float)offset.Value.Value}f;");
                break;
        }
    }

    private static void GenerateStyleSetup(CodeBuilder cb, StyleSpec? style, string varName)
    {
        if (style == null) return;

        // Foreground (Font Color)
        if (style.Foreground != null)
        {
            var c = style.Foreground.Value;
            cb.AppendLine($"{varName}.AddThemeColorOverride(\"font_color\", new Color({c.R / 255f}f, {c.G / 255f}f, {c.B / 255f}f, {c.A / 255f}f));");
        }

        // Background - Tricky, usually requires a StyleBox.
        // For simple controls like ColorRect, it's Color.
        // For others, we might need a StyleBoxFlat.
        if (style.Background != null)
        {
            // Simple approach: Check if it is a ColorRect (unlikely unless we mapped it so)
            // Or create a new StyleBoxFlat and assign it to "panel" or "normal" override.
            // var c = style.Background.Value;
            // cb.AppendLine($"var styleBox = new StyleBoxFlat {{ BgColor = new Color({c.R/255f}f, {c.G/255f}f, {c.B/255f}f, {c.A/255f}f) }};");
            // cb.AppendLine($"{varName}.AddThemeStyleboxOverride(\"panel\", styleBox);"); // for PanelContainer
            // cb.AppendLine($"{varName}.AddThemeStyleboxOverride(\"normal\", styleBox);"); // for Button/Label
        }
    }

    private static void GenerateComponentProperties(CodeBuilder cb, ComponentNode node, string varName, bool isRoot)
    {
        // Static properties (Text, etc.)
        if (node.Properties.TryGetValue("text", out var textProp) && !textProp.IsBound)
        {
            cb.AppendLine($"{varName}.Set(\"text\", \"{EscapeString(textProp.Value?.ToString() ?? "")}\");");
        }
        else if (node.Properties.TryGetValue("value", out var valueProp) && !valueProp.IsBound)
        {
            // Fallback for Label/Button
            cb.AppendLine($"{varName}.Set(\"text\", \"{EscapeString(valueProp.Value?.ToString() ?? "")}\");");
        }

        // Command Binding (Button)
        var commandPath = TryGetCommandBindingPath(node);
        if (commandPath != null && node.Type == ComponentType.Button)
        {
            var propName = commandPath.Replace(".", "");
            cb.AppendLine($"{varName}.Pressed += () =>");
            cb.OpenBlock();
            cb.AppendLine($"if (_viewModel?.{propName} is System.Windows.Input.ICommand cmd && cmd.CanExecute(null))");
            cb.AppendLine("{");
            cb.AppendLine("    cmd.Execute(null);");
            cb.AppendLine("}");
            cb.CloseBlock(";");
        }
    }

    private static void GenerateBindingAssignment(CodeBuilder cb, BindingOp op)
    {
        var valueExpr = $"_viewModel.{op.ViewModelProperty}";

        // Format
        if (!string.IsNullOrEmpty(op.Format))
        {
            valueExpr = $"string.Format(\"{EscapeString(op.Format)}\", {valueExpr})";
        }
        else
        {
            // Basic ToString handling
            valueExpr = $"Convert.ToString({valueExpr}) ?? \"\"";
        }

        var targetProp = op.TargetProperty.ToLowerInvariant();

        if (targetProp == "text")
        {
            cb.AppendLine($"{op.ControlName}.Set(\"text\", {valueExpr});");
        }
        else if (targetProp == "visible")
        {
            // bool conversion
            cb.AppendLine($"{op.ControlName}.Visible = Convert.ToBoolean(_viewModel.{op.ViewModelProperty});");
        }
        else if (targetProp == "enabled")
        {
            // disabled = !enabled
            cb.AppendLine($"{op.ControlName}.Set(\"disabled\", !Convert.ToBoolean(_viewModel.{op.ViewModelProperty}));");
        }
        else if (targetProp == "value")
        {
            // For Range-based controls (ProgressBar, Slider)
            // Ensure double conversion
            cb.AppendLine($"{op.ControlName}.Value = Convert.ToDouble(_viewModel.{op.ViewModelProperty});");
        }
        else if (targetProp == "checked")
        {
            // For CheckBox/BaseButton
            cb.AppendLine($"{op.ControlName}.ButtonPressed = Convert.ToBoolean(_viewModel.{op.ViewModelProperty});");
        }
        // Add more property mappings as needed
    }

    private static void CollectComponentFields(ComponentNode node, Dictionary<ComponentNode, string> nodeNames, List<(string OriginalId, string UniqueName, string Type)> fields)
    {
        if (node.Id != null && nodeNames.TryGetValue(node, out var uniqueName))
        {
            var type = MapComponentType(node.Type);
            if (node.Type == ComponentType.Container)
                type = GetContainerType(node.Layout?.Type ?? LayoutType.Vertical);

            fields.Add((node.Id, uniqueName, type));
        }
        foreach (var child in node.Children) CollectComponentFields(child, nodeNames, fields);
    }

    private static void CollectBindings(ComponentNode node, Dictionary<ComponentNode, string> nodeNames, List<(string PropertyName, List<BindingOp> Ops)> bindings)
    {
        // Recursively collect all bindings
        var nodeOps = new List<BindingOp>();

        // Use the mapped unique variable name. If it wasn't a field, we still used a local var with the unique name.
        // Wait, binding updates need to access the control. If it's NOT a field, we can't update it from PropertyChanged handler!
        // So BOUND controls MUST be fields. 
        // Our 'CollectComponentFields' ensures node.Id != null.
        // If a node has bindings but NO ID, we have a problem. 
        // Figma parser usually assigns IDs to everything derived from node names or generic names.
        // AssignUniqueNames creates names for everything.
        // BUT field generation only happens if `node.Id != null`. 
        // `AssignUniqueNames` uses node.Id or fallback.
        // Does `node.Id` ever come as null from FigmaParser? 
        // Looking at `FigmaParser.ConvertNode`:
        // Id = SanitizeId(figmaNode.Name) -> if null/empty returns "element".
        // So node.Id is essentially never null coming from Figma.
        // So EVERY node becomes a field? That's a lot of fields.
        // But `SanitizeId` returns camelCase.

        // `nodeNames.TryGetValue(node, out var varName)` should succeed for all nodes.
        // But we need to use `_varName` convention if it's a field.

        // If `node.Id` is not null, it is a field `_uniqueName`.

        var isField = node.Id != null;
        string? varName = null;

        if (isField && nodeNames.TryGetValue(node, out var uniqueName))
        {
            varName = $"_{uniqueName}";
        }

        if (varName == null)
        {
            // If we can't target it as a field, we can't bind it.
            // But as established, FigmaParser ensures IDs.
        }
        else
        {
            foreach (var b in node.Bindings)
            {
                var member = !string.IsNullOrWhiteSpace(b.Key) ? b.Key! : b.Path;
                nodeOps.Add(new BindingOp { ControlName = varName, TargetProperty = b.Property, ViewModelProperty = member.Replace(".", ""), Format = b.Format });
            }
        }

        // Group by VM property
        foreach (var op in nodeOps)
        {
            var group = bindings.FirstOrDefault(g => g.PropertyName == op.ViewModelProperty);
            if (group.PropertyName == null)
            {
                group = (op.ViewModelProperty, new List<BindingOp>());
                bindings.Add(group);
            }
            group.Ops.Add(op);
        }

        foreach (var child in node.Children) CollectBindings(child, nodeNames, bindings);
    }

    private struct BindingOp
    {
        public string ControlName;
        public string TargetProperty;
        public string ViewModelProperty;
        public string? Format;
    }

    private static string MapComponentType(ComponentType type) => type switch
    {
        ComponentType.Label => "Label",
        ComponentType.Button => "Button",
        ComponentType.TextInput => "LineEdit",
        ComponentType.TextArea => "TextEdit",
        ComponentType.Checkbox => "CheckBox",
        ComponentType.ProgressBar => "ProgressBar",
        ComponentType.Slider => "HSlider", // or VSlider based on layout
        ComponentType.Icon => "Label",
        ComponentType.Badge => "Label",
        ComponentType.Image => "TextureRect",
        ComponentType.MenuBar => "MenuBar",
        ComponentType.Menu => "PopupMenu",
        ComponentType.Timeline => "HBoxContainer",
        ComponentType.Container => "Control", // Default, specific types handled elsewhere
        ComponentType.ScrollView => "ScrollContainer",
        ComponentType.Panel => "PanelContainer",
        ComponentType.TabView => "TabContainer",
        ComponentType.SplitView => "HSplitContainer",
        ComponentType.ListBox => "ItemList",
        ComponentType.Spacer => "Control",
        _ => "Control"
    };

    private static string GetContainerType(LayoutType type) => type switch
    {
        LayoutType.Vertical => "VBoxContainer",
        LayoutType.Horizontal => "HBoxContainer",
        LayoutType.Grid => "GridContainer",
        LayoutType.Stack => "PanelContainer", // Closest fit?
        LayoutType.Dock => "Control",
        LayoutType.Absolute => "Control",
        _ => "Control"
    };

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

    private static string EscapeString(string value) => value.Replace("\"", "\\\"").Replace("\n", "\\n");

    private static string? TryGetCommandBindingPath(ComponentNode node)
    {
        foreach (var binding in node.Bindings)
        {
            if (string.Equals(binding.Property, "command", StringComparison.OrdinalIgnoreCase))
                return !string.IsNullOrWhiteSpace(binding.Key) ? binding.Key : binding.Path;
        }
        return node.Command;
    }

    private static string GenerateViewModelInterface(HudDocument document, GenerationOptions options)
    {
        var cb = new CodeBuilder();

        var viewModelNamespace = options.ViewModelNamespace ?? options.Namespace;

        if (options.IncludeComments)
        {
            cb.AppendLine("// <auto-generated>");
            cb.AppendLine($"// Generated by BoomHud.Gen.Godot from {document.Name}");
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
        cb.AppendLine("using System.ComponentModel;");
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

        foreach (var path in bindingPaths.OrderBy(p => p))
        {
            var propertyName = path.Replace(".", "");
            cb.AppendLine($"object? {propertyName} {{ get; }}");
        }

        cb.CloseBlock();
        return cb.ToString();
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
}
