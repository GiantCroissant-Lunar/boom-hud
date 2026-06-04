using BoomHud.Abstractions.Capabilities;

namespace BoomHud.Gen.Pencil;

/// <summary>
/// Capability manifest for Pencil .pen output.
/// </summary>
public sealed class PencilCapabilities : ICapabilityManifest
{
    public static readonly PencilCapabilities Instance = new();

    private PencilCapabilities() { }

    public string TargetFramework => "Pencil";

    public IReadOnlySet<string> SupportedComponents { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "label", "badge", "button", "textinput", "textarea", "checkbox", "radiobutton",
        "progressbar", "slider", "icon", "image",
        "menubar", "menu", "menuitem", "timeline",
        "container", "scrollview", "panel", "tabview", "splitview",
        "listbox", "listview", "treeview", "datagrid",
        "stack", "grid", "dock", "spacer"
    };

    public IReadOnlySet<string> SupportedLayouts { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "vertical", "horizontal", "grid", "stack", "dock", "absolute"
    };

    public IReadOnlyDictionary<string, CapabilityLevel> Features { get; } = new Dictionary<string, CapabilityLevel>
    {
        [Capabilities.DataBinding] = CapabilityLevel.Native,
        [Capabilities.TwoWayBinding] = CapabilityLevel.Limited,
        [Capabilities.CompiledBindings] = CapabilityLevel.Unsupported,
        [Capabilities.PixelLayout] = CapabilityLevel.Native,
        [Capabilities.CellLayout] = CapabilityLevel.Unsupported,
        [Capabilities.FlexLayout] = CapabilityLevel.Native,
        [Capabilities.GridLayout] = CapabilityLevel.Native,
        [Capabilities.RichText] = CapabilityLevel.Limited,
        [Capabilities.Images] = CapabilityLevel.Native,
        [Capabilities.SvgIcons] = CapabilityLevel.Limited,
        [Capabilities.Animation] = CapabilityLevel.Unsupported,
        [Capabilities.Scrolling] = CapabilityLevel.Native,
        [Capabilities.Tooltips] = CapabilityLevel.Limited,
        [Capabilities.MouseInput] = CapabilityLevel.Unsupported,
        [Capabilities.KeyboardInput] = CapabilityLevel.Unsupported,
        [Capabilities.TouchInput] = CapabilityLevel.Unsupported,
        [Capabilities.DragAndDrop] = CapabilityLevel.Unsupported,
        [Capabilities.Spatial3D] = CapabilityLevel.Unsupported
    };

    public CapabilityLevel GetCapabilityLevel(string feature)
        => Features.TryGetValue(feature, out var level) ? level : CapabilityLevel.Unsupported;

    public bool SupportsComponent(string componentType)
        => SupportedComponents.Contains(componentType);

    public bool SupportsLayout(string layoutType)
        => SupportedLayouts.Contains(layoutType);
}
