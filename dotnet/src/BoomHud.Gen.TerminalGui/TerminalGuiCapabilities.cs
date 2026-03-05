using BoomHud.Abstractions.Capabilities;

namespace BoomHud.Gen.TerminalGui;

/// <summary>
/// Capability manifest for Terminal.Gui v2.
/// </summary>
public sealed class TerminalGuiCapabilities : ICapabilityManifest
{
    public static TerminalGuiCapabilities Instance { get; } = new();

    public string TargetFramework => "Terminal.Gui";

    public IReadOnlySet<string> SupportedComponents { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "label", "badge", "button", "textinput", "textarea", "checkbox", "radiobutton",
        "progressbar", "slider", "icon", // icon via label with emoji
        "menubar", "menu", "menuitem",
        "container", "scrollview", "panel", "tabview",
        "listbox", "listview", "treeview", "datagrid",
        "spacer"
    };

    public IReadOnlySet<string> SupportedLayouts { get; } = new HashSet<string>
    {
        "horizontal", "vertical", "absolute"
    };

    public IReadOnlyDictionary<string, CapabilityLevel> Features { get; } = new Dictionary<string, CapabilityLevel>
    {
        [Capabilities.DataBinding] = CapabilityLevel.Emulated,      // Manual refresh pattern
        [Capabilities.TwoWayBinding] = CapabilityLevel.Emulated,
        [Capabilities.CompiledBindings] = CapabilityLevel.Unsupported,
        [Capabilities.PixelLayout] = CapabilityLevel.Unsupported,
        [Capabilities.CellLayout] = CapabilityLevel.Native,
        [Capabilities.FlexLayout] = CapabilityLevel.Limited,        // Pos.Percent, Dim.Fill
        [Capabilities.GridLayout] = CapabilityLevel.Limited,        // Manual positioning
        [Capabilities.RichText] = CapabilityLevel.Limited,          // Basic formatting
        [Capabilities.Images] = CapabilityLevel.Unsupported,
        [Capabilities.SvgIcons] = CapabilityLevel.Unsupported,
        [Capabilities.Animation] = CapabilityLevel.Unsupported,
        [Capabilities.Scrolling] = CapabilityLevel.Native,
        [Capabilities.Tooltips] = CapabilityLevel.Unsupported,      // v2 may add this
        [Capabilities.MouseInput] = CapabilityLevel.Native,
        [Capabilities.KeyboardInput] = CapabilityLevel.Native,
        [Capabilities.TouchInput] = CapabilityLevel.Unsupported,
        [Capabilities.DragAndDrop] = CapabilityLevel.Unsupported
    };

    public CapabilityLevel GetCapabilityLevel(string feature)
        => Features.TryGetValue(feature, out var level) ? level : CapabilityLevel.Unsupported;

    public bool SupportsComponent(string componentType)
        => SupportedComponents.Contains(componentType);

    public bool SupportsLayout(string layoutType)
        => SupportedLayouts.Contains(layoutType.ToLowerInvariant());
}
