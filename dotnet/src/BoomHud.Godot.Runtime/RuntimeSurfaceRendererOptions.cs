using BoomHud.Abstractions.Runtime;

namespace BoomHud.Godot.Runtime;

public sealed record RuntimeSurfaceRendererOptions
{
    public RuntimeSurfaceCatalog? Catalog { get; init; }

    public RuntimeSurfaceValidatorOptions? ValidatorOptions { get; init; }

    public RuntimeSurfaceActionHandler? ActionHandler { get; init; }
}

public delegate void RuntimeSurfaceActionHandler(RuntimeSurfaceActionInvocation invocation);

public sealed record RuntimeSurfaceActionInvocation(
    string SurfaceId,
    string ComponentId,
    RuntimeActionDescriptor Action);

