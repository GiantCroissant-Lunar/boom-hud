using BoomHud.Abstractions.Runtime;

namespace BoomHud.Godot.Runtime;

public sealed class RuntimeSurfaceRenderException : InvalidOperationException
{
    public RuntimeSurfaceRenderException(IReadOnlyList<RuntimeSurfaceValidationDiagnostic> diagnostics)
        : base(BuildMessage(diagnostics))
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<RuntimeSurfaceValidationDiagnostic> Diagnostics { get; }

    private static string BuildMessage(IReadOnlyList<RuntimeSurfaceValidationDiagnostic> diagnostics)
        => diagnostics.Count == 0
            ? "Runtime surface validation failed."
            : $"Runtime surface validation failed: {string.Join("; ", diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Path}"))}";
}

