namespace BoomHud.Abstractions.IR;

/// <summary>
/// A node in the component tree representing a UI element.
/// </summary>
public sealed record ComponentNode
{
    /// <summary>
    /// Optional unique identifier for this component instance.
    /// </summary>
    public string? Id { get; init; }

    public string? SlotKey { get; init; }

    /// <summary>
    /// Type of component (label, button, container, etc.).
    /// </summary>
    public required ComponentType Type { get; init; }

    /// <summary>
    /// Whether this component is visible.
    /// </summary>
    public BindableValue<bool> Visible { get; init; } = true;

    /// <summary>
    /// Whether this component is enabled/interactive.
    /// </summary>
    public BindableValue<bool> Enabled { get; init; } = true;

    /// <summary>
    /// Layout specification for this component.
    /// </summary>
    public LayoutSpec? Layout { get; init; }

    /// <summary>
    /// Style specification for this component.
    /// </summary>
    public StyleSpec? Style { get; init; }

    /// <summary>
    /// Child components (for containers).
    /// </summary>
    public IReadOnlyList<ComponentNode> Children { get; init; } = [];

    /// <summary>
    /// Type-specific properties as bindable values.
    /// </summary>
    public IReadOnlyDictionary<string, BindableValue<object?>> Properties { get; init; } = new Dictionary<string, BindableValue<object?>>();

    /// <summary>
    /// Explicit bindings defined on this component.
    /// </summary>
    public IReadOnlyList<BindingSpec> Bindings { get; init; } = [];

    /// <summary>
    /// Capabilities required by this component.
    /// </summary>
    public IReadOnlySet<string> RequiredCapabilities { get; init; } = new HashSet<string>();

    /// <summary>
    /// Optional tooltip text.
    /// </summary>
    public BindableValue<string>? Tooltip { get; init; }

    /// <summary>
    /// Command binding path for interactive components.
    /// </summary>
    public string? Command { get; init; }

    /// <summary>
    /// Parameter to pass to the command.
    /// </summary>
    public object? CommandParameter { get; init; }

    /// <summary>
    /// Optional reference to a reusable component definition (e.g., Figma componentId).
    /// When set, generators should prefer composition (instantiating the referenced component view) over inline expansion.
    /// </summary>
    public string? ComponentRefId { get; init; }

    /// <summary>
    /// Overrides applied to a component instance.
    /// The interpretation of keys/values is generator-specific and may be limited.
    /// </summary>
    public IReadOnlyDictionary<string, object?> InstanceOverrides { get; init; } = new Dictionary<string, object?>();
}

/// <summary>
/// Enumeration of supported component types.
/// </summary>
public enum ComponentType
{
    // Primitives
    Label,
    Button,
    TextInput,
    TextArea,
    Checkbox,
    RadioButton,
    ProgressBar,
    Slider,
    Icon,
    Image,

    // Menus
    MenuBar,
    Menu,
    MenuItem,

    // Timeline
    Timeline,

    // Containers
    Container,
    ScrollView,
    Panel,
    TabView,
    SplitView,

    // Lists
    ListBox,
    ListView,
    TreeView,
    DataGrid,

    // Layout
    Stack,
    Grid,
    Dock,
    Spacer
}
