namespace BoomHud.Abstractions.Runtime;

public sealed record RuntimeSurfaceCatalog
{
    public required string CatalogId { get; init; }

    public IReadOnlyDictionary<string, RuntimeComponentSpec> Components { get; init; } =
        new Dictionary<string, RuntimeComponentSpec>(StringComparer.OrdinalIgnoreCase);

    public static RuntimeSurfaceCatalog Basic { get; } = new()
    {
        CatalogId = RuntimeSurfaceProtocol.BasicCatalogId,
        Components = new Dictionary<string, RuntimeComponentSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["container"] = Component(
                "container",
                properties: Set("variant", "tooltip"),
                bindableProperties: Set("visible", "enabled", "tooltip")),
            ["panel"] = Component(
                "panel",
                properties: Set("title", "variant", "tooltip"),
                bindableProperties: Set("title", "visible", "enabled", "tooltip")),
            ["label"] = Component(
                "label",
                properties: Set("text", "variant", "tooltip"),
                bindableProperties: Set("text", "visible", "enabled", "tooltip")),
            ["badge"] = Component(
                "badge",
                properties: Set("text", "variant", "tooltip"),
                bindableProperties: Set("text", "visible", "enabled", "tooltip")),
            ["button"] = Component(
                "button",
                properties: Set("text", "variant", "tooltip"),
                bindableProperties: Set("text", "visible", "enabled", "tooltip"),
                events: Set("pressed")),
            ["progressBar"] = Component(
                "progressBar",
                properties: Set("value", "minimum", "maximum", "label", "variant", "tooltip"),
                bindableProperties: Set("value", "minimum", "maximum", "label", "visible", "enabled", "tooltip")),
            ["list"] = Component(
                "list",
                properties: Set("items", "selectedItem", "emptyText", "variant", "tooltip"),
                bindableProperties: Set("items", "selectedItem", "emptyText", "visible", "enabled", "tooltip"),
                events: Set("selected")),
            ["nodeGraph"] = Component(
                "nodeGraph",
                properties: Set("items", "wires", "variant", "tooltip"),
                bindableProperties: Set("items", "wires", "visible", "enabled", "tooltip"),
                events: Set("connectionRequested", "disconnectionRequested", "deleteNodesRequested")),
            ["spacer"] = Component(
                "spacer",
                properties: Set("size"),
                bindableProperties: Set("visible"))
        }
    };

    private static RuntimeComponentSpec Component(
        string type,
        StringSet properties,
        StringSet bindableProperties,
        StringSet? events = null)
        => new()
        {
            Type = type,
            Properties = properties,
            BindableProperties = bindableProperties,
            Events = events ?? EmptySet(),
        };

    private static HashSet<string> EmptySet()
        => new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Set(params string[] values)
        => new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
}

public sealed record RuntimeComponentSpec
{
    public required string Type { get; init; }

    public StringSet Properties { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public StringSet BindableProperties { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public StringSet Events { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool AllowUnknownProperties { get; init; }
}
