using System.Text;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;
using BoomHud.Generators;

namespace BoomHud.Gen.Unity;

internal sealed class UnityBackendPlanner
{
    private readonly GenerationOptions _options;
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly HashSet<string> _usedNodeNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _bindingIdentifiersByPath = new(StringComparer.Ordinal);
    private readonly HashSet<string> _usedBindingIdentifiers = new(StringComparer.Ordinal);
    private string _documentName = string.Empty;
    private RuleResolver? _ruleResolver;
    private int _generatedNodeIndex;

    private UnityBackendPlanner(GenerationOptions options)
    {
        _options = options;
    }

    public static UnityBackendPlan CreatePlan(HudDocument document, GenerationOptions options, List<Diagnostic> diagnostics)
    {
        var planner = new UnityBackendPlanner(options);
        var plan = planner.Build(document);
        diagnostics.AddRange(planner._diagnostics);
        return plan;
    }

    private UnityBackendPlan Build(HudDocument document)
    {
        _documentName = document.Name;
        _ruleResolver = new RuleResolver(_options.RuleSet, "unity");
        var rootBaseName = document.Root.Id;
        if (string.IsNullOrWhiteSpace(rootBaseName))
        {
            rootBaseName = document.Name + "Root";
        }

        var root = PlanNode(document.Root, rootBaseName);

        var viewModelProperties = _bindingIdentifiersByPath
            .OrderBy(static pair => pair.Value, StringComparer.Ordinal)
            .Select(static pair => new UnityViewModelProperty
            {
                Path = pair.Key,
                Identifier = pair.Value
            })
            .ToList();

        return new UnityBackendPlan
        {
            Namespace = _options.Namespace,
            ViewModelNamespace = _options.ViewModelNamespace ?? _options.Namespace,
            Root = root,
            ViewModelProperties = viewModelProperties
        };
    }

    private UnityPlannedNode PlanNode(ComponentNode node, string? baseName)
    {
        RegisterBindingPaths(node);

        var plannedName = ReserveNodeName(baseName);
        var policy = _ruleResolver?.Resolve(_documentName, node) ?? new ResolvedGeneratorPolicy();
        var mapping = MapElement(node, policy);
        var children = node.Children
            .Select((child, index) =>
            {
                var childBaseName = child.Id ?? child.SlotKey ?? child.Type.ToString() + index.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
                return PlanNode(child, childBaseName);
            })
            .ToList();

        return new UnityPlannedNode
        {
            Source = node,
            Name = plannedName,
            ElementType = mapping.ElementType,
            UxmlTag = mapping.UxmlTag,
            CssClass = "boomhud-" + ToKebabCase(plannedName),
            IsFallback = mapping.IsFallback,
            Policy = policy,
            Children = children
        };
    }

    private void RegisterBindingPaths(ComponentNode node)
    {
        foreach (var binding in node.Bindings)
        {
            RegisterBindingPath(binding.Path);
        }

        foreach (var property in node.Properties.Values)
        {
            if (property.IsBound && property.BindingPath != null)
            {
                RegisterBindingPath(property.BindingPath);
            }
        }

        if (node.Visible.IsBound && node.Visible.BindingPath != null)
        {
            RegisterBindingPath(node.Visible.BindingPath);
        }

        if (node.Enabled.IsBound && node.Enabled.BindingPath != null)
        {
            RegisterBindingPath(node.Enabled.BindingPath);
        }

        if (node.Tooltip is { IsBound: true, BindingPath: not null })
        {
            RegisterBindingPath(node.Tooltip.Value.BindingPath);
        }
    }

    private void RegisterBindingPath(string path)
    {
        if (_bindingIdentifiersByPath.ContainsKey(path))
        {
            return;
        }

        var baseIdentifier = ToIdentifier(path);
        var identifier = baseIdentifier;
        var suffix = 2;

        while (!_usedBindingIdentifiers.Add(identifier))
        {
            identifier = baseIdentifier + suffix.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            suffix++;
        }

        _bindingIdentifiersByPath[path] = identifier;
    }

    private string ReserveNodeName(string? candidate)
    {
        var baseName = ToIdentifier(candidate);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            _generatedNodeIndex++;
            baseName = "Node" + _generatedNodeIndex.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        }

        var name = baseName;
        var suffix = 2;
        while (!_usedNodeNames.Add(name))
        {
            name = baseName + suffix.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            suffix++;
        }

        return name;
    }

    private (string ElementType, string UxmlTag, bool IsFallback) MapElement(ComponentNode node, ResolvedGeneratorPolicy policy)
    {
        if (!string.IsNullOrWhiteSpace(policy.ControlType))
        {
            var overrideMapping = TryMapControlOverride(policy.ControlType!);
            if (overrideMapping != null)
            {
                return overrideMapping.Value;
            }

            _diagnostics.Add(Diagnostic.Warning(
                $"Unity UI Toolkit control override '{policy.ControlType}' is not recognized; using default mapping.",
                node.Id,
                code: "BHU1003"));
        }

        if (node.Layout?.Type == LayoutType.Grid)
        {
            _diagnostics.Add(Diagnostic.Warning(
                "Unity UI Toolkit grid layout is not fully implemented yet; emitting flex-based fallback.",
                node.Id,
                code: "BHU1001"));
        }

        if (node.Layout?.Type == LayoutType.Dock)
        {
            _diagnostics.Add(Diagnostic.Warning(
                "Unity UI Toolkit dock layout is not fully implemented yet; emitting flex-based fallback.",
                node.Id,
                code: "BHU1002"));
        }

        return node.Type switch
        {
            ComponentType.Label or ComponentType.Badge or ComponentType.Icon
                => ("Label", "Label", false),
            ComponentType.Button
                => ("Button", "Button", false),
            ComponentType.TextInput or ComponentType.TextArea
                => ("TextField", "TextField", false),
            ComponentType.Checkbox or ComponentType.RadioButton
                => ("Toggle", "Toggle", false),
            ComponentType.ProgressBar
                => ("ProgressBar", "ProgressBar", false),
            ComponentType.Slider
                => ("Slider", "Slider", false),
            ComponentType.ScrollView
                => ("ScrollView", "ScrollView", false),
            ComponentType.Image
                => ("Image", "Image", false),
            ComponentType.Container or ComponentType.Panel or ComponentType.Stack or ComponentType.Grid or ComponentType.Dock or ComponentType.Spacer
                => ("VisualElement", "VisualElement", false),
            _ => CreateFallbackMapping(node)
        };
    }

    private (string ElementType, string UxmlTag, bool IsFallback) CreateFallbackMapping(ComponentNode node)
    {
        _diagnostics.Add(Diagnostic.Warning(
            $"Unity UI Toolkit support for component type '{node.Type}' is not implemented yet; emitting VisualElement fallback.",
            node.Id,
            code: "BHU1000"));

        return ("VisualElement", "VisualElement", true);
    }

    private static (string ElementType, string UxmlTag, bool IsFallback)? TryMapControlOverride(string controlType)
    {
        return controlType.Trim() switch
        {
            "Label" => ("Label", "Label", false),
            "Button" => ("Button", "Button", false),
            "TextField" => ("TextField", "TextField", false),
            "Toggle" => ("Toggle", "Toggle", false),
            "ProgressBar" => ("ProgressBar", "ProgressBar", false),
            "Slider" => ("Slider", "Slider", false),
            "ScrollView" => ("ScrollView", "ScrollView", false),
            "Image" => ("Image", "Image", false),
            "VisualElement" => ("VisualElement", "VisualElement", false),
            _ => null
        };
    }

    private static string ToIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var capitalize = true;
        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                capitalize = true;
                continue;
            }

            var next = capitalize ? char.ToUpperInvariant(ch) : ch;
            builder.Append(next);
            capitalize = false;
        }

        if (builder.Length == 0)
        {
            return string.Empty;
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, 'N');
        }

        return builder.ToString();
    }

    private static string ToKebabCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var ch = value[index];
            if (char.IsUpper(ch) && index > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }
}
