using BoomHud.Abstractions.Capabilities;
using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

/// <summary>
/// Checks whether a document's spatial nodes are compatible with a backend's capabilities.
/// Emits diagnostics for unsupported configurations.
/// </summary>
public static class SpatialCompatibilityChecker
{
    /// <summary>
    /// Walks the document and emits diagnostics for any spatial nodes that the backend cannot handle.
    /// Returns true if all spatial nodes are fully supported.
    /// </summary>
    public static bool CheckDocument(HudDocument document, ICapabilityManifest capabilities, List<Diagnostic> diagnostics)
    {
        var allSupported = true;
        foreach (var component in document.Components.Values)
        {
            if (!CheckNode(component.Root, capabilities, diagnostics))
            {
                allSupported = false;
            }
        }

        if (!CheckNode(document.Root, capabilities, diagnostics))
        {
            allSupported = false;
        }

        return allSupported;
    }

    private static bool CheckNode(ComponentNode node, ICapabilityManifest capabilities, List<Diagnostic> diagnostics)
    {
        var supported = true;

        if (node.Spatial != null)
        {
            var level = capabilities.GetCapabilityLevel(Capabilities.Spatial3D);
            if (level != CapabilityLevel.Native)
            {
                diagnostics.Add(Diagnostic.Warning(
                    $"Spatial layout is not supported by {capabilities.TargetFramework}; falling back to 2D layout.",
                    code: "BHG3001"));
                supported = false;
            }
            else if (node.Spatial.Shape != SpatialShape.Cylinder)
            {
                diagnostics.Add(Diagnostic.Warning(
                    $"Spatial shape '{node.Spatial.Shape}' is not supported in Phase 2; falling back to 2D layout.",
                    code: "BHG3002"));
                supported = false;
            }
        }

        foreach (var child in node.Children)
        {
            if (!CheckNode(child, capabilities, diagnostics))
            {
                supported = false;
            }
        }

        return supported;
    }
}
