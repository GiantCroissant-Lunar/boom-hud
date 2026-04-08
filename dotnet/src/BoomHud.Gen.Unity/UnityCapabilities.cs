using BoomHud.Abstractions.Capabilities;

namespace BoomHud.Gen.Unity;

/// <summary>
/// Capability manifest for the Unity UI Toolkit backend.
/// </summary>
public sealed class UnityCapabilities : ICapabilityManifest
{
    public static UnityCapabilities Instance { get; } = new();

    private UnityCapabilities()
    {
    }

    public string TargetFramework => "Unity UI Toolkit";

    public IReadOnlySet<string> SupportedComponents { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "label", "badge", "button", "textinput", "textarea", "checkbox", "radiobutton",
        "progressbar", "slider", "icon", "image",
        "container", "scrollview", "panel", "spacer",
        "stack", "grid", "dock"
    };

    public IReadOnlySet<string> SupportedLayouts { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "horizontal", "vertical", "stack", "absolute"
    };

    public IReadOnlyDictionary<string, CapabilityLevel> Features { get; } = new Dictionary<string, CapabilityLevel>
    {
        [Capabilities.DataBinding] = CapabilityLevel.Emulated,
        [Capabilities.TwoWayBinding] = CapabilityLevel.Emulated,
        [Capabilities.CompiledBindings] = CapabilityLevel.Native,
        [Capabilities.PixelLayout] = CapabilityLevel.Native,
        [Capabilities.CellLayout] = CapabilityLevel.Unsupported,
        [Capabilities.FlexLayout] = CapabilityLevel.Native,
        [Capabilities.GridLayout] = CapabilityLevel.Limited,
        [Capabilities.RichText] = CapabilityLevel.Limited,
        [Capabilities.Images] = CapabilityLevel.Native,
        [Capabilities.SvgIcons] = CapabilityLevel.Limited,
        [Capabilities.Animation] = CapabilityLevel.Limited,
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
        => SupportedLayouts.Contains(layoutType);
}