using BoomHud.Abstractions.Capabilities;

namespace BoomHud.Gen.Avalonia;

/// <summary>
/// Capability manifest for Avalonia.
/// </summary>
public sealed class AvaloniaCapabilities : ICapabilityManifest
{
    public static AvaloniaCapabilities Instance { get; } = new();

    public string TargetFramework => "Avalonia";

    public IReadOnlySet<string> SupportedComponents { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "label", "button", "textinput", "textarea", "checkbox", "radiobutton",
        "progressbar", "slider", "icon", "image",
        "container", "scrollview", "panel", "tabview", "splitview",
        "listbox", "listview", "treeview", "datagrid",
        "stack", "grid", "dock", "spacer"
    };

    public IReadOnlySet<string> SupportedLayouts { get; } = new HashSet<string>
    {
        "horizontal", "vertical", "stack", "grid", "dock", "absolute"
    };

    public IReadOnlyDictionary<string, CapabilityLevel> Features { get; } = new Dictionary<string, CapabilityLevel>
    {
        [Capabilities.DataBinding] = CapabilityLevel.Native,
        [Capabilities.TwoWayBinding] = CapabilityLevel.Native,
        [Capabilities.CompiledBindings] = CapabilityLevel.Native,
        [Capabilities.PixelLayout] = CapabilityLevel.Native,
        [Capabilities.CellLayout] = CapabilityLevel.Emulated,       // Via fixed-width fonts
        [Capabilities.FlexLayout] = CapabilityLevel.Native,         // DockPanel, Grid stars
        [Capabilities.GridLayout] = CapabilityLevel.Native,
        [Capabilities.RichText] = CapabilityLevel.Native,
        [Capabilities.Images] = CapabilityLevel.Native,
        [Capabilities.SvgIcons] = CapabilityLevel.Native,           // Via Avalonia.Svg
        [Capabilities.Animation] = CapabilityLevel.Native,
        [Capabilities.Scrolling] = CapabilityLevel.Native,
        [Capabilities.Tooltips] = CapabilityLevel.Native,
        [Capabilities.MouseInput] = CapabilityLevel.Native,
        [Capabilities.KeyboardInput] = CapabilityLevel.Native,
        [Capabilities.TouchInput] = CapabilityLevel.Native,
        [Capabilities.DragAndDrop] = CapabilityLevel.Native
    };

    public CapabilityLevel GetCapabilityLevel(string feature)
        => Features.TryGetValue(feature, out var level) ? level : CapabilityLevel.Unsupported;

    public bool SupportsComponent(string componentType)
        => SupportedComponents.Contains(componentType);

    public bool SupportsLayout(string layoutType)
        => SupportedLayouts.Contains(layoutType.ToLowerInvariant());
}
