---
name: unity-component-fidelity
description: Component-first pen-to-Unity UI Toolkit fidelity loop for BoomHud. Use when generated components do not match the .pen design and you want to tune components in code before tackling overall layout.
license: MIT
compatibility: Intended to be used from the boom-hud repo root with the UnityFullPenCompare sample project and a running Unity MCP endpoint.
---

# Unity Component Fidelity

Component-first workflow for BoomHud's pen-to-Unity UI Toolkit pipeline.

This skill is for cases where overall screen layout is still noisy, but the real problem is that the generated components themselves are wrong. The goal is to fix component generation and code-side wiring first, then return to screen-level layout.

## When to use this skill

Use this skill when you are:

- comparing `full.pen` against Unity output and the widgets themselves are visibly wrong
- seeing raw icon token names like `swords` or `flask-conical` instead of usable UI affordances
- trying to debug a generated component in isolation before reasoning about the full HUD
- working in `samples/UnityFullPenCompare`

## Preconditions

- Unity `6000.4.1f1` is available locally
- the compare sample exists at `samples/UnityFullPenCompare`
- the external source pen file is available at:
  `C:\Users\User\project-ultima-magic\ultima-magic\docs\assets\hud\full.pen`
- Unity MCP is reachable

## Source of truth

- The converter and generated controller code are the primary targets for fixes.
- Prefer fixing generation or code-side runtime wiring before hand-editing generated UXML or USS.
- For visual validation, use the component lab first, then the full compare scene.

## Component-first workflow

### 1. Regenerate the Unity sample from the real `.pen`

```powershell
dotnet run --project dotnet/src/BoomHud.Cli -- generate "C:\Users\User\project-ultima-magic\ultima-magic\docs\assets\hud\full.pen" --target unity --output "samples\UnityFullPenCompare\Assets\Resources\BoomHudGenerated" --namespace Generated.Hud
```

### 2. Open the component lab scene in Unity

Preferred path inside Unity:

```text
Tools/BoomHud/Setup Component Lab Scene
```

This creates and opens:

- `Assets/BoomHudCompare/Scenes/ComponentLab.unity`

The lab mounts generated components individually in code through:

- `Assets/BoomHudCompare/Scripts/ComponentLabPresenter.cs`

### 3. Inspect components individually

Focus on these first:

1. `ActionButtonView`
2. `StatusIconView`
3. `CharPortraitView`
4. `StatBarView`
5. `MessageLogView`
6. `MinimapView`

### 4. Prefer code-side fixes in this order

1. Fix generator logic in `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`
2. Regenerate `Assets/Resources/BoomHudGenerated/*.gen.cs`
3. If needed, adjust sample presenter code in `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/*.cs`
4. Only after component behavior is correct, revisit generated USS/layout behavior

## Known validation paths

### Reliable

- Unity MCP `manage_ui` with `get_visual_tree`
- direct inspection of generated `.gen.cs`, `.uxml`, and `.uss`
- compare scene hierarchy and element sizes through MCP

### Less reliable

- Unity MCP `render_ui` for the component lab scene may return a black image even when the visual tree is present

For the lab scene, trust the live visual tree first. Use screenshot capture mainly on the full compare scene.

## High-value targets to fix in code

### Icons

Generated controller code should not leave icon-like nodes as raw token strings when a more readable representation is possible.

Look for:

- `sword`
- `swords`
- `sparkles`
- `wand-sparkles`
- `shield`
- `flask-conical`
- `flame`
- `moon`
- `cross`

Primary fix location:

- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`

### Absolute placement vs flow

If a component or group collapses inside a row or column, check whether Pencil coordinates are incorrectly removing the node from parent flow.

Primary fix location:

- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`

### Component composition

If a component's internal parts overlap or collapse, inspect the generated UXML and controller output first, then inspect the component root in the lab scene.

## Recommended iteration loop

1. Regenerate Unity output from the real pen file.
2. Refresh Unity.
3. Open the component lab scene.
4. Inspect a single component subtree via Unity MCP `get_visual_tree`.
5. Fix generator or presenter code.
6. Re-run unit tests.
7. Regenerate again.
8. Re-check the component lab.
9. Only after the component looks right, validate the full compare scene.

## Useful files

- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UnityGeneratorTests.cs`
- `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/ComponentLabPresenter.cs`
- `samples/UnityFullPenCompare/Assets/Editor/BoomHudComponentLabSetup.cs`
- `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/ExploreHudPresenter.cs`
- `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scenes/ComponentLab.unity`
- `samples/UnityFullPenCompare/Assets/BoomHudCompare/Scenes/ExploreHudCompare.unity`

## Exit criteria

Do not move on to global layout until the target component satisfies all of these:

- no obvious overlap or collapse inside the component
- text/icon treatment is intentional rather than placeholder/raw token output
- the component subtree in Unity MCP has plausible dimensions
- generator tests still pass
- regeneration does not introduce new Unity console errors