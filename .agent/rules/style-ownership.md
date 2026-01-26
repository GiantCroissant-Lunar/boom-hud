# Style Ownership Rule

## Purpose

Prevent nondeterministic-looking composition results from style collisions (BH0110).

## The Rule

**Styles must be token-only, and defined in one of two places:**

1. **Inside the component** (preferred) - component-local styles
2. **In a single theme source file** - one authority for shared styles

## Why This Matters

When composing multiple design files, style names are merged. If the same style name appears in multiple files:
- First file wins (deterministic, but surprising)
- BH0110 warning is emitted

If agents create styles in multiple files with the same name, the "winner" depends on composition order, making results appear random even though they're deterministic.

## Good Pattern

```json
// ui/sources/pencil/debug-overlay.pen
{
  "nodes": [{
    "id": "panel",
    "style": {
      "background": { "$ref": "tokens.colors.debug-bg" },
      "cornerRadius": 4
    }
  }]
}
```

Styles are defined inline using token references. No named style collisions possible.

## Bad Pattern

```json
// ui/sources/pencil/shared-styles.pen
{
  "styles": {
    "PanelStyle": { "background": "#1a1a1a" }
  }
}

// ui/sources/pencil/debug-overlay.pen  
{
  "styles": {
    "PanelStyle": { "background": "#2a2a2a" }  // COLLISION!
  }
}
```

## Enforcement

- BH0110 warnings clearly identify winner/loser files
- CI should fail on BH0110 warnings (future: `--style-collisions error`)
- Code review should catch cross-file style definitions

## Exceptions

If you need shared styles across components:
1. Define them in a single `theme.pen` or `styles.pen` file
2. List that file **first** in `boom-hud.compose.json` sources
3. Document that this file owns all shared styles
