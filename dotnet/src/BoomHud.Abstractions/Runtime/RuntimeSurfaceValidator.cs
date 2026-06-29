namespace BoomHud.Abstractions.Runtime;

public sealed record RuntimeSurfaceValidatorOptions
{
    public int MaxNodeCount { get; init; } = 512;

    public int MaxDepth { get; init; } = 32;

    public bool RequireCatalogMatch { get; init; } = true;
}

public sealed record RuntimeSurfaceValidationResult(IReadOnlyList<RuntimeSurfaceValidationDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.All(diagnostic => diagnostic.Severity != RuntimeSurfaceValidationSeverity.Error);
}

public sealed record RuntimeSurfaceValidationDiagnostic(
    RuntimeSurfaceValidationSeverity Severity,
    string Code,
    string Path,
    string Message);

public enum RuntimeSurfaceValidationSeverity
{
    Error,
    Warning
}

public static class RuntimeSurfaceValidator
{
    private static readonly HashSet<string> BindingModes = new(StringComparer.Ordinal)
    {
        RuntimeBindingModes.OneWay,
        RuntimeBindingModes.TwoWay,
        RuntimeBindingModes.OneTime
    };

    private static readonly HashSet<string> LayoutTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "absolute",
        "dock",
        "grid",
        "horizontal",
        "stack",
        "vertical"
    };

    private static readonly HashSet<string> DataModelOperations = new(StringComparer.Ordinal)
    {
        RuntimeDataModelOperations.Replace,
        RuntimeDataModelOperations.Merge,
        RuntimeDataModelOperations.Remove
    };

    public static RuntimeSurfaceValidationResult Validate(
        RuntimeSurfaceDocument document,
        RuntimeSurfaceCatalog catalog,
        RuntimeSurfaceValidatorOptions? options = null)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        options ??= new RuntimeSurfaceValidatorOptions();

        var diagnostics = new List<RuntimeSurfaceValidationDiagnostic>();
        if (string.IsNullOrWhiteSpace(document.SurfaceId))
        {
            AddError(diagnostics, "BHRT001", "/surfaceId", "Surface id is required.");
        }

        if (string.IsNullOrWhiteSpace(document.CatalogId))
        {
            AddError(diagnostics, "BHRT002", "/catalogId", "Catalog id is required.");
        }

        if (options.RequireCatalogMatch &&
            !string.IsNullOrWhiteSpace(document.CatalogId) &&
            !string.Equals(document.CatalogId, catalog.CatalogId, StringComparison.Ordinal))
        {
            AddError(
                diagnostics,
                "BHRT003",
                "/catalogId",
                $"Surface catalog '{document.CatalogId}' does not match trusted catalog '{catalog.CatalogId}'.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var nodeCount = 0;
        ValidateNode(document.Root, catalog, options, diagnostics, ids, "/root", depth: 0, ref nodeCount);

        return new RuntimeSurfaceValidationResult(diagnostics);
    }

    public static RuntimeSurfaceValidationResult Validate(
        RuntimeDataModelUpdate update,
        string? expectedSurfaceId = null)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));

        var diagnostics = new List<RuntimeSurfaceValidationDiagnostic>();
        if (string.IsNullOrWhiteSpace(update.SurfaceId))
        {
            AddError(diagnostics, "BHRT001", "/surfaceId", "Surface id is required.");
        }

        if (!string.IsNullOrWhiteSpace(expectedSurfaceId) &&
            !string.Equals(update.SurfaceId, expectedSurfaceId, StringComparison.Ordinal))
        {
            AddError(
                diagnostics,
                "BHRT004",
                "/surfaceId",
                $"Update surface id '{update.SurfaceId}' does not match expected surface '{expectedSurfaceId}'.");
        }

        if (!IsValidJsonPointer(update.Path))
        {
            AddError(diagnostics, "BHRT070", "/path", $"Data-model update path '{update.Path}' is not a valid JSON Pointer.");
        }

        if (!DataModelOperations.Contains(update.Operation))
        {
            AddError(diagnostics, "BHRT071", "/operation", $"Data-model operation '{update.Operation}' is not supported.");
        }

        return new RuntimeSurfaceValidationResult(diagnostics);
    }

    public static bool IsValidJsonPointer(string? path)
    {
        if (path is null)
        {
            return false;
        }

        if (path.Length == 0)
        {
            return true;
        }

        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] != '~')
            {
                continue;
            }

            if (i == path.Length - 1 || path[i + 1] is not ('0' or '1'))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateNode(
        RuntimeComponentNode node,
        RuntimeSurfaceCatalog catalog,
        RuntimeSurfaceValidatorOptions options,
        List<RuntimeSurfaceValidationDiagnostic> diagnostics,
        HashSet<string> ids,
        string path,
        int depth,
        ref int nodeCount)
    {
        nodeCount++;
        if (nodeCount > options.MaxNodeCount)
        {
            AddError(diagnostics, "BHRT060", path, $"Runtime surface exceeds the maximum node count of {options.MaxNodeCount}.");
            return;
        }

        if (depth > options.MaxDepth)
        {
            AddError(diagnostics, "BHRT061", path, $"Runtime surface exceeds the maximum depth of {options.MaxDepth}.");
            return;
        }

        if (string.IsNullOrWhiteSpace(node.Id))
        {
            AddError(diagnostics, "BHRT010", $"{path}/id", "Component id is required for runtime surfaces.");
        }
        else if (!ids.Add(node.Id))
        {
            AddError(diagnostics, "BHRT011", $"{path}/id", $"Component id '{node.Id}' is duplicated.");
        }

        RuntimeComponentSpec? spec = null;
        if (string.IsNullOrWhiteSpace(node.Type))
        {
            AddError(diagnostics, "BHRT012", $"{path}/type", "Component type is required.");
        }
        else if (!TryGetComponentSpec(catalog, node.Type, out spec))
        {
            AddError(diagnostics, "BHRT013", $"{path}/type", $"Component type '{node.Type}' is not in catalog '{catalog.CatalogId}'.");
        }

        if (node.Layout is not null)
        {
            ValidateLayout(node.Layout, diagnostics, $"{path}/layout");
        }

        foreach (var (propertyName, value) in node.Properties)
        {
            var propertyPath = $"{path}/properties/{EscapePathSegment(propertyName)}";
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                AddError(diagnostics, "BHRT020", propertyPath, "Property name is required.");
            }
            else if (spec is not null &&
                     !spec.AllowUnknownProperties &&
                     !Contains(spec.Properties, propertyName) &&
                     !Contains(spec.BindableProperties, propertyName))
            {
                AddError(
                    diagnostics,
                    "BHRT021",
                    propertyPath,
                    $"Property '{propertyName}' is not allowed on component type '{spec.Type}'.");
            }

            ValidateValue(value, diagnostics, propertyPath);
        }

        for (var i = 0; i < node.Bindings.Count; i++)
        {
            ValidateBinding(node.Bindings[i], spec, diagnostics, $"{path}/bindings/{i}");
        }

        for (var i = 0; i < node.Actions.Count; i++)
        {
            ValidateAction(node.Actions[i], spec, diagnostics, $"{path}/actions/{i}");
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            ValidateNode(node.Children[i], catalog, options, diagnostics, ids, $"{path}/children/{i}", depth + 1, ref nodeCount);
        }
    }

    private static void ValidateValue(
        RuntimeValue value,
        List<RuntimeSurfaceValidationDiagnostic> diagnostics,
        string path)
    {
        if (value.Literal is null && value.Binding is null)
        {
            AddError(diagnostics, "BHRT030", path, "Runtime value must provide either a literal or a binding.");
            return;
        }

        if (value.Literal is not null && value.Binding is not null)
        {
            AddError(diagnostics, "BHRT031", path, "Runtime value cannot provide both a literal and a binding.");
            return;
        }

        if (value.Binding is not null)
        {
            ValidateBinding(value.Binding, component: null, diagnostics, $"{path}/binding");
        }
    }

    private static void ValidateBinding(
        RuntimeBinding binding,
        RuntimeComponentSpec? component,
        List<RuntimeSurfaceValidationDiagnostic> diagnostics,
        string path)
    {
        if (binding.Property is not null && string.IsNullOrWhiteSpace(binding.Property))
        {
            AddError(diagnostics, "BHRT040", $"{path}/property", "Binding property cannot be empty.");
        }

        if (component is not null &&
            !string.IsNullOrWhiteSpace(binding.Property) &&
            !Contains(component.BindableProperties, binding.Property!))
        {
            AddError(
                diagnostics,
                "BHRT041",
                $"{path}/property",
                $"Property '{binding.Property}' is not bindable on component type '{component.Type}'.");
        }

        if (!IsValidJsonPointer(binding.Path))
        {
            AddError(diagnostics, "BHRT042", $"{path}/path", $"Binding path '{binding.Path}' is not a valid JSON Pointer.");
        }

        if (!BindingModes.Contains(binding.Mode))
        {
            AddError(diagnostics, "BHRT043", $"{path}/mode", $"Binding mode '{binding.Mode}' is not supported.");
        }
    }

    private static void ValidateAction(
        RuntimeActionDescriptor action,
        RuntimeComponentSpec? component,
        List<RuntimeSurfaceValidationDiagnostic> diagnostics,
        string path)
    {
        if (string.IsNullOrWhiteSpace(action.Event))
        {
            AddError(diagnostics, "BHRT050", $"{path}/event", "Action event is required.");
        }
        else if (component is not null && !Contains(component.Events, action.Event))
        {
            AddError(
                diagnostics,
                "BHRT051",
                $"{path}/event",
                $"Event '{action.Event}' is not allowed on component type '{component.Type}'.");
        }

        if (string.IsNullOrWhiteSpace(action.Command))
        {
            AddError(diagnostics, "BHRT052", $"{path}/command", "Action command is required.");
        }
    }

    private static void ValidateLayout(
        RuntimeLayoutSpec layout,
        List<RuntimeSurfaceValidationDiagnostic> diagnostics,
        string path)
    {
        if (!LayoutTypes.Contains(layout.Type))
        {
            AddError(diagnostics, "BHRT080", $"{path}/type", $"Layout type '{layout.Type}' is not supported.");
        }
    }

    private static bool TryGetComponentSpec(
        RuntimeSurfaceCatalog catalog,
        string componentType,
        out RuntimeComponentSpec? spec)
    {
        if (catalog.Components.TryGetValue(componentType, out spec))
        {
            return true;
        }

        foreach (var candidate in catalog.Components.Values)
        {
            if (string.Equals(candidate.Type, componentType, StringComparison.OrdinalIgnoreCase))
            {
                spec = candidate;
                return true;
            }
        }

        spec = null;
        return false;
    }

    private static bool Contains(StringSet set, string value)
        => set.Contains(value) || set.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));

    private static string EscapePathSegment(string segment)
        => segment.Replace("~", "~0").Replace("/", "~1");

    private static void AddError(
        List<RuntimeSurfaceValidationDiagnostic> diagnostics,
        string code,
        string path,
        string message)
        => diagnostics.Add(new RuntimeSurfaceValidationDiagnostic(RuntimeSurfaceValidationSeverity.Error, code, path, message));
}
