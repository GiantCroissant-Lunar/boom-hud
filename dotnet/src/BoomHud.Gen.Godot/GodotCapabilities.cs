using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.IR;

namespace BoomHud.Gen.Godot;

/// <summary>
/// Capability manifest for Godot 4.x.
/// </summary>
public sealed class GodotCapabilities : ICapabilityManifest
{
    public static readonly GodotCapabilities Instance = new();

    private GodotCapabilities() { }

    public string TargetFramework => "Godot 4.x";

    public IReadOnlySet<string> SupportedComponents { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "label", "button", "textinput", "textarea", "checkbox", "radiobutton",
        "progressbar", "slider", "icon", "image",
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
        [Capabilities.DataBinding] = CapabilityLevel.Emulated,      // Generated code handles subscription
        [Capabilities.TwoWayBinding] = CapabilityLevel.Emulated,    // Generated code handles subscription
        [Capabilities.CompiledBindings] = CapabilityLevel.Native,   // It's C#
        [Capabilities.PixelLayout] = CapabilityLevel.Native,
        [Capabilities.CellLayout] = CapabilityLevel.Unsupported,
        [Capabilities.FlexLayout] = CapabilityLevel.Native,         // Containers
        [Capabilities.GridLayout] = CapabilityLevel.Native,
        [Capabilities.RichText] = CapabilityLevel.Native,           // RichTextLabel
        [Capabilities.Images] = CapabilityLevel.Native,
        [Capabilities.SvgIcons] = CapabilityLevel.Native,
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
