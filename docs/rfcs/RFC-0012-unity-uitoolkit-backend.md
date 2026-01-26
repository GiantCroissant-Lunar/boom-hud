# RFC-0012: Unity UI Toolkit Backend

- **Status**: Draft
- **Created**: 2025-12-29
- **Authors**: BoomHud Contributors

## Summary

This RFC proposes adding a **Unity UI Toolkit** backend to BoomHud. This generator will produce UXML (structure), USS (style), and C# (logic) files compatible with Unity's UI Toolkit (formerly UIElements) for runtime UI.

## Motivation

Unity is a primary target for game development. Currently, bridging the gap between design tools (Figma) and Unity UI is manual and error-prone. While Unity has "UI Builder", it doesn't solve the "design-to-code" pipeline from external tools like Figma.

By supporting Unity UI Toolkit, BoomHud becomes a viable pipeline for Unity developers to import HUDs directly from Figma without manual reconstruction.

## Goals

- Generate `.uxml` files representing the component hierarchy.
- Generate `.uss` files for styling.
- Generate `.cs` controller classes to bind ViewModels to the UI.
- Support both **Runtime** UI (primary focus) and Editor UI (secondary).
- Map BoomHud Layout (Flex-like) to Unity's Flexbox layout engine.

## Non-Goals

- Supporting the legacy Unity UI (uGUI / Canvas).
- Supporting immediate mode GUI (IMGUI).
- Full animation export (Unity's animation system is complex; this will be limited/future work).

## Design

### 1. Architecture

The generator will produce three artifacts per component:

1.  **`MyComponent.uxml`**: The structural definition.
2.  **`MyComponent.uss`**: The visual styles.
3.  **`MyComponent.gen.cs`**: A C# class wrapping the `VisualElement` to provide typed access and binding logic.

### 2. Component Mapping

Unity UI Toolkit provides a set of standard controls that map well to BoomHud primitives:

| BoomHud Component | Unity UI Toolkit Element | Notes |
|-------------------|--------------------------|-------|
| `container`       | `VisualElement`          | |
| `label`           | `Label`                  | |
| `button`          | `Button`                 | |
| `textInput`       | `TextField`              | |
| `checkbox`        | `Toggle`                 | |
| `progressBar`     | `ProgressBar`            | |
| `scrollView`      | `ScrollView`             | |
| `image`           | `Image` / `VisualElement`| `Image` for textures, `VisualElement` for background sprites |
| `icon`            | `Label` (text) or `Image`| Depending on if emoji or sprite |
| `listView`        | `ListView`               | Requires `makeItem` and `bindItem` logic in C# |

### 3. Layout Mapping

Unity UI Toolkit uses a Flexbox-based layout system (Yoga), which is very similar to web standards.

- **BoomHud `stack`** -> `flex-direction: column` or `row`.
- **BoomHud `dock`** -> `position: absolute` with anchors (top/left/bottom/right).
- **BoomHud `grid`** -> Unity 6 has limited Grid support; earlier versions may need nested Flexbox emulation.

The generator will emit USS classes for layout properties defined in the IR.

### 4. Styling (USS)

BoomHud styles will be generated into a USS file.

```css
/* MyComponent.uss */
.status-bar {
    flex-direction: row;
    height: 40px;
    background-color: #2b2b2b;
}

.health-bar {
    width: 150px;
    color: red;
}
```

The UXML will reference these classes:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xsi="http://www.w3.org/2001/XMLSchema-instance">
    <ui:VisualElement class="status-bar">
        <!-- ... -->
    </ui:VisualElement>
</ui:UXML>
```

### 5. Data Binding & C# Wrapper

Unity's data binding system has evolved. For broad compatibility (Unity 2021+), we will generate a "View Controller" pattern similar to the Terminal.Gui backend, rather than relying solely on the new Unity 6 Data Binding system (though we could support that as a flag later).

Generated C# class:

```csharp
// Generated
using UnityEngine.UIElements;

public class StatusBarView
{
    public VisualElement Root { get; }
    
    // Typed access to elements
    public ProgressBar HealthBar { get; }
    public Label HealthLabel { get; }

    public StatusBarView(VisualElement root)
    {
        Root = root;
        HealthBar = root.Q<ProgressBar>("healthBar");
        HealthLabel = root.Q<Label>("healthLabel");
    }

    // Binding Refresh Logic
    public void Refresh(IStatusBarViewModel vm)
    {
        if (vm == null) return;
        
        HealthBar.value = vm.HealthPercent;
        HealthLabel.text = vm.HealthText;
    }
}
```

This approach is robust and works across most Unity versions supporting UI Toolkit.

### 6. Editor vs Runtime Considerations

UI Toolkit is unique in that it powers both the Unity Editor interface and Runtime game UI.

-   **Runtime**: Hosted via `UIDocument` component on a GameObject.
-   **Editor**: Hosted via `EditorWindow` or Custom Inspectors.

The generated **View Controller** (`StatusBarView` in the example above) is designed to be **hosting-agnostic**. It wraps a `VisualElement` root, meaning it can be instantiated in:

1.  **Runtime**: Inside a `MonoBehaviour` that references a `UIDocument`.
2.  **Editor**: Inside `EditorWindow.CreateGUI()`.

**Key Difference - Asset Loading**:
-   **Editor**: We have direct access to `AssetDatabase`.
-   **Runtime**: Must use `Resources`, Addressables, or direct references assigned in the Inspector.

To address "more control in Editor", the generator can optionally emit **Editor-specific helpers** (e.g., `[MenuItem]` to open a preview window) if a `--target-context editor` flag is provided, while keeping the core UI definition portable.

## Project Structure

New project: `BoomHud.Gen.Unity`

- `UnityGenerator.cs`: Main entry point.
- `UxmlEmitter.cs`: Writes XML.
- `UssEmitter.cs`: Writes CSS-like styles.
- `CsControllerEmitter.cs`: Writes the C# glue code.

## Open Questions

1.  **Asset Management**: How to reference fonts and images?
    *   *Proposed*: Expect assets to be in `Resources` or Addressables, or simply use relative paths and let Unity import them.
2.  **Unity Version Compatibility**: UI Toolkit changes frequently.
    *   *Proposed*: Target LTS 2022.3 baseline.

## Alternatives Considered

- **uGUI Generator**: uGUI is still popular but "legacy". UI Toolkit is the future of Unity UI.
- **Immediate Mode (IMGUI)**: Too performant-heavy for complex HUDs, mostly for debug tools.
