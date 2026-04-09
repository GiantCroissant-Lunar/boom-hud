using System.Linq;

namespace BoomHud.Abstractions.IR;

public static class HudDocumentRootSelector
{
    public static HudDocument SelectRoot(HudDocument document, string? rootComponentName)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(rootComponentName)
            || string.Equals(document.Name, rootComponentName, StringComparison.Ordinal)
            || string.Equals(document.Root.Id, rootComponentName, StringComparison.Ordinal))
        {
            return document;
        }

        var selectedComponent = document.Components.Values.FirstOrDefault(component =>
            string.Equals(component.Name, rootComponentName, StringComparison.Ordinal)
            || string.Equals(component.Id, rootComponentName, StringComparison.Ordinal));

        if (selectedComponent == null)
        {
            var availableRoots = string.Join(", ",
                document.Components.Values
                    .Select(component => component.Name)
                    .Prepend(document.Name)
                    .Distinct(StringComparer.Ordinal));

            throw new InvalidOperationException(
                $"Root component '{rootComponentName}' not found in document '{document.Name}'. Available: {availableRoots}");
        }

        var remainingComponents = document.Components
            .Where(entry => !string.Equals(entry.Value.Id, selectedComponent.Id, StringComparison.Ordinal))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        return new HudDocument
        {
            Name = selectedComponent.Name,
            Metadata = selectedComponent.Metadata,
            Root = selectedComponent.Root,
            Styles = document.Styles,
            Components = remainingComponents
        };
    }
}