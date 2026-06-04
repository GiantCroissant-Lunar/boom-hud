using BoomHud.Abstractions.Runtime;

namespace BoomHud.Godot.Runtime;

public static class GodotRuntimeSurfaceCatalog
{
    public static RuntimeSurfaceCatalog Catalog => RuntimeSurfaceCatalog.Basic;

    public static bool TryGetControlType(
        string runtimeComponentType,
        string? layoutType,
        out string controlType)
    {
        if (!Catalog.Components.ContainsKey(runtimeComponentType))
        {
            controlType = string.Empty;
            return false;
        }

        controlType = runtimeComponentType switch
        {
            "badge" => "Label",
            "button" => "Button",
            "container" => MapContainer(layoutType),
            "label" => "Label",
            "list" => "ItemList",
            "panel" => "PanelContainer",
            "progressBar" => "ProgressBar",
            "spacer" => "Control",
            _ => "Control"
        };

        return true;
    }

    private static string MapContainer(string? layoutType)
        => layoutType switch
        {
            "grid" => "GridContainer",
            "horizontal" => "HBoxContainer",
            "vertical" => "VBoxContainer",
            _ => "Control"
        };
}

