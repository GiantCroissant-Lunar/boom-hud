using BoomHud.Abstractions.Runtime;

namespace BoomHud.Godot.Runtime;

public sealed record RuntimeSurfaceRendererOptions
{
    public RuntimeSurfaceCatalog? Catalog { get; init; }

    public RuntimeSurfaceValidatorOptions? ValidatorOptions { get; init; }

    public RuntimeSurfaceActionHandler? ActionHandler { get; init; }

    /// <summary>
    /// Optional visual theme: maps a node's <c>variant</c> property to fills/borders/fonts. Null (the
    /// default) means no styling — components render with the stock Godot theme, exactly as before.
    /// </summary>
    public RuntimeSurfaceTheme? Theme { get; init; }
}

public delegate void RuntimeSurfaceActionHandler(RuntimeSurfaceActionInvocation invocation);

public sealed record RuntimeSurfaceActionInvocation(
    string SurfaceId,
    string ComponentId,
    RuntimeActionDescriptor Action);

