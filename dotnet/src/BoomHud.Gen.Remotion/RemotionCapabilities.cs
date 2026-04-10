using BoomHud.Abstractions.Capabilities;

namespace BoomHud.Gen.Remotion;

/// <summary>
/// Capability manifest for Remotion output.
/// </summary>
public sealed class RemotionCapabilities : ICapabilityManifest
{
    public static readonly RemotionCapabilities Instance = new();

    private RemotionCapabilities() { }

    public string TargetFramework => "Remotion";

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
        [Capabilities.DataBinding] = CapabilityLevel.Emulated,
        [Capabilities.TwoWayBinding] = CapabilityLevel.Emulated,
        [Capabilities.CompiledBindings] = CapabilityLevel.Native,
        [Capabilities.PixelLayout] = CapabilityLevel.Native,
        [Capabilities.CellLayout] = CapabilityLevel.Unsupported,
        [Capabilities.FlexLayout] = CapabilityLevel.Native,
        [Capabilities.GridLayout] = CapabilityLevel.Native,
        [Capabilities.RichText] = CapabilityLevel.Limited,
        [Capabilities.Images] = CapabilityLevel.Native,
        [Capabilities.SvgIcons] = CapabilityLevel.Native,
        [Capabilities.Animation] = CapabilityLevel.Native,
        [Capabilities.Scrolling] = CapabilityLevel.Native,
        [Capabilities.Tooltips] = CapabilityLevel.Native,
        [Capabilities.MouseInput] = CapabilityLevel.Native,
        [Capabilities.KeyboardInput] = CapabilityLevel.Native,
        [Capabilities.TouchInput] = CapabilityLevel.Native,
        [Capabilities.DragAndDrop] = CapabilityLevel.Limited
    };

    public CapabilityLevel GetCapabilityLevel(string feature)
        => Features.TryGetValue(feature, out var level) ? level : CapabilityLevel.Unsupported;

    public bool SupportsComponent(string componentType)
        => SupportedComponents.Contains(componentType);

    public bool SupportsLayout(string layoutType)
        => SupportedLayouts.Contains(layoutType.ToLowerInvariant());
}
