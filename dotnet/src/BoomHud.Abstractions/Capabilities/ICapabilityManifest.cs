namespace BoomHud.Abstractions.Capabilities;

/// <summary>
/// Manifest declaring the capabilities of a backend.
/// </summary>
public interface ICapabilityManifest
{
    /// <summary>
    /// Name of the target framework.
    /// </summary>
    string TargetFramework { get; }

    /// <summary>
    /// Supported component types.
    /// </summary>
    IReadOnlySet<string> SupportedComponents { get; }

    /// <summary>
    /// Supported layout types.
    /// </summary>
    IReadOnlySet<string> SupportedLayouts { get; }

    /// <summary>
    /// Feature capability levels.
    /// </summary>
    IReadOnlyDictionary<string, CapabilityLevel> Features { get; }

    /// <summary>
    /// Gets the capability level for a specific feature.
    /// </summary>
    CapabilityLevel GetCapabilityLevel(string feature);

    /// <summary>
    /// Checks if a component type is supported.
    /// </summary>
    bool SupportsComponent(string componentType);

    /// <summary>
    /// Checks if a layout type is supported.
    /// </summary>
    bool SupportsLayout(string layoutType);
}

/// <summary>
/// Level of support for a capability.
/// </summary>
public enum CapabilityLevel
{
    /// <summary>
    /// Framework supports this capability natively.
    /// </summary>
    Native,

    /// <summary>
    /// Capability can be emulated with some overhead.
    /// </summary>
    Emulated,

    /// <summary>
    /// Partial support with caveats.
    /// </summary>
    Limited,

    /// <summary>
    /// Not available in this framework.
    /// </summary>
    Unsupported
}

/// <summary>
/// Well-known capability names.
/// </summary>
public static class Capabilities
{
    // Binding capabilities
    public const string DataBinding = "dataBinding";
    public const string TwoWayBinding = "twoWayBinding";
    public const string CompiledBindings = "compiledBindings";

    // Layout capabilities
    public const string PixelLayout = "pixelLayout";
    public const string CellLayout = "cellLayout";
    public const string FlexLayout = "flexLayout";
    public const string GridLayout = "gridLayout";

    // Component capabilities
    public const string RichText = "richText";
    public const string Images = "images";
    public const string SvgIcons = "svgIcons";
    public const string Animation = "animation";
    public const string Scrolling = "scrolling";
    public const string Tooltips = "tooltips";

    // Interaction capabilities
    public const string MouseInput = "mouseInput";
    public const string KeyboardInput = "keyboardInput";
    public const string TouchInput = "touchInput";
    public const string DragAndDrop = "dragAndDrop";
}
