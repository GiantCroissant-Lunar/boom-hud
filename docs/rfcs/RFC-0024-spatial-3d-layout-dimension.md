# RFC-0024: Spatial (3D) Layout Dimension

- **Status**: Draft
- **Created**: 2026-06-02
- **Authors**: BoomHud Contributors

## Summary

Add a **spatial (3D) layout dimension** to the BoomHud IR that **arranges existing 2D component faces in
3D space** — rather than introducing a 3D scene-graph component vocabulary. A new `SpatialSpec` on
`ComponentNode` describes how a container places its children in 3D (e.g. a `Cylinder` shape mapping
`track -> angle` and `time -> depth`). Backends render each child as a **2D component face on a positioned 3D
surface**, gated by a new `Spatial3D` capability whose level varies per backend (Native for Godot/Unity,
WebGL for React, fallback/Unsupported for 2D-only backends). The first consumer is the fantasim-app-godot
"tunnel timeline" (a planar timeline wrapped onto the inner wall of a cylinder).

## Motivation

BoomHud's IR is, by construction, a 2D-UI model: `ComponentNode` carries a 2D `LayoutSpec`
(`vertical/horizontal/grid/stack/dock/absolute`) and 2D `StyleSpec`; every backend (Godot, Terminal.Gui,
Avalonia, Unity UI, React) is a 2D-UI framework. There is no spatial/transform/mesh/camera concept anywhere
in the IR.

But real product surfaces want depth. The driving case is a **3D "tunnel timeline"**: the world's history as
a forward-looking tube, the planet at the mouth (the world at the current canonical tick), clips riding tracks
that recede into depth (`depth = canonical tick`). This is not expressible today, and hand-authoring it would
sit entirely outside the generation pipeline — contrary to BoomHud's "build-time generation over runtime
indirection" principle.

The key realization: **a tunnel is a planar timeline wrapped onto a cylinder.** The *content* (clip cards,
labels, swatches) is ordinary 2D UI; only the *placement* is 3D. So depth belongs in the **layout** dimension,
not in a new component vocabulary. That keeps the entire 2D component model reusable and the same definition
renders flat-2D or spatial-3D depending only on the layout.

## Goals

- A single IR concept (`SpatialSpec`) that places **existing 2D components** in 3D, parameterized by shape
  (cylinder first) and **data-driven transform mappings** (`track -> angle`, `tick -> depth`).
- Reuse the existing capability system to gate 3D per backend (`Spatial3D` capability with `Native` /
  `Emulated` / `Unsupported` levels).
- A backend contract: **"render a 2D component face onto a positioned surface in 3D"** — implemented natively
  by Godot first, then Unity / React (WebGL); 2D-only backends fall back to a flat projection.
- Fully additive and backward compatible: a node with no `SpatialSpec` behaves exactly as today.
- Unblock the fantasim-app-godot tunnel timeline as the first real consumer, with the cylinder layout's math
  feeding from a reloadable view-model at runtime (see Backward Compatibility / consumer notes).

## Non-Goals

- A general 3D scene graph (arbitrary meshes, lights, physics). BoomHud composes UI; app-specific 3D objects
  (a planet globe, a glowing ring) are **injected via host slots**, not generated.
- Camera *framing logic* (orbit, fly-through, scrub). BoomHud emits at most named camera anchors/modes; the
  framing behavior lives in the host/VM.
- Replacing the 2D layout engine. Spatial layout is additive and only applies where a `SpatialSpec` is present.
- Terminal.Gui "real" 3D. It is `Unsupported`; it renders a documented 2D fallback.

## Design

### Overview

```
ComponentNode (2D component, unchanged)
   + Spatial: SpatialSpec?        <-- NEW, optional. Present => children placed in 3D.

SpatialSpec
   - Shape: SpatialShape          (Cylinder | Radial | Helix | Grid3D | Free)
   - Item axis mappings (BindingSpec-driven): which item field -> angle / depth / radius / lateral
   - Shape params: radius, angularSpacing, depthScale, facing, curvature
   - (repeater) the children are a template instantiated per bound collection item

Capability: "spatial3D" (+ "perspectiveCamera")
   Godot=Native, Unity=Native, React=Native(WebGL), Avalonia=Emulated/Unsupported, TerminalGui=Unsupported

Backend contract: render a 2D component FACE onto a positioned 3D SURFACE.
   Godot:  SubViewport texture -> quad (MeshInstance3D) at Transform3D
   Unity:  world-space canvas / mesh
   React:  Three.js / CSS3D plane
   2D-only: flat projection fallback
```

### Detailed Design

#### 1. IR additions (`BoomHud.Abstractions/IR/`)

New file `SpatialSpec.cs`:

```csharp
namespace BoomHud.Abstractions.IR;

public enum SpatialShape { Cylinder, Radial, Helix, Grid3D, Free }

/// <summary>Where a child's face points.</summary>
public enum SpatialFacing { Inward, Outward, Camera }

/// <summary>
/// Describes how a container arranges its (2D) children in 3D space. Present on a ComponentNode whose
/// children are placed spatially; absent => normal 2D layout. The children are a repeater template
/// instantiated per item of the bound collection (reuses BindingSpec / the data-binding machinery).
/// </summary>
public sealed record SpatialSpec
{
    public required SpatialShape Shape { get; init; }
    public SpatialFacing Facing { get; init; } = SpatialFacing.Camera;

    // Data-driven axis mappings: expressions over the repeated item (e.g. item field -> value).
    // Reuses BindingSpec so the same expression layer covers 2D and 3D.
    public BindingSpec? AngleFrom { get; init; }   // -> angle around the axis (cylinder/radial/helix)
    public BindingSpec? DepthFrom { get; init; }   // -> position along the axis (cylinder/helix); time/tick
    public BindingSpec? RadiusFrom { get; init; }  // -> radial distance (radial; optional cylinder jitter)
    public BindingSpec? LateralFrom { get; init; } // -> across-track offset within a lane

    // Shape parameters (static or token-bound).
    public double Radius { get; init; } = 1.0;
    public double AngularSpacingDeg { get; init; } = 30.0;
    public double DepthScale { get; init; } = 1.0;
    public double Curvature { get; init; } = 0.0; // 0 = flat quad, 1 = fully curved to the wall
}
```

Add to `ComponentNode`:

```csharp
/// <summary>Optional spatial (3D) arrangement of this node's children. Null => standard 2D layout.</summary>
public SpatialSpec? Spatial { get; init; }
```

(`ComponentNode` stays otherwise unchanged; components remain 2D.)

#### 2. Capability constants (`BoomHud.Abstractions/Capabilities/ICapabilityManifest.cs`)

```csharp
public const string Spatial3D = "spatial3D";
public const string PerspectiveCamera = "perspectiveCamera";
```

Each backend's `ICapabilityManifest` declares its level. Initial matrix:

| Backend | `spatial3D` | Rationale |
|---|---|---|
| Godot | `Native` | Node3D + SubViewport-textured quads |
| Unity | `Native` | world-space canvas / mesh |
| React | `Native` | Three.js / CSS3D (WebGL) |
| Avalonia | `Unsupported` (later `Emulated` via OpenGL surface) | no native 3D UI host |
| Terminal.Gui | `Unsupported` | renders a 2D flat fallback |

A node with a `SpatialSpec` whose backend reports `Unsupported` must **degrade gracefully** to the equivalent
2D layout (flat list / projection) and emit a diagnostic — never fail generation.

#### 3. Schema + DSL (`schemas/json/boom-hud.schema.json`, `BoomHud.Dsl`)

Add a `spatial` object to the component schema mirroring `SpatialSpec` (shape enum, facing, the `*From`
binding expressions, shape params). Extend the DSL parser (`IHudParser` implementations) to read `spatial`
into `ComponentNode.Spatial`. Schema-first per BoomHud conventions: update `schemas/json/boom-hud.schema.json`
**before** the parser.

#### 4. Backend contract: 2D face on a 3D surface

Add to the backend abstraction an explicit notion that a spatial child is rendered as a **2D component face on
a positioned surface**. Each 3D-capable generator implements:

- compute each item's `Transform3D` from `SpatialSpec` + the item's bound axis values;
- render the child component's 2D face to a texture/surface (Godot: `SubViewport` -> `ViewportTexture` on a
  `QuadMesh` `MeshInstance3D`; Unity: world-space canvas; React: CSS3D/textured plane);
- place the surface at the transform, oriented per `Facing`.

`Gen.Godot` (RFC-0013 tscn backend) gains a spatial-emission path: when a node has `Spatial`, emit a `Node3D`
root hosting a generated **face scene** per item template plus the placement code (driven at runtime by the
bound collection + a transform provider). The cylinder math is deterministic from `SpatialSpec`, but the
*animated/scrub* parameters (e.g. current depth) are runtime inputs (see consumer notes).

#### 5. Host slots for app 3D objects

App-specific 3D objects BoomHud does not generate (the planet globe, the now-ring) are declared as **host
slots** — a `ComponentNode` with a slot marker (reusing `SlotKey` / a `hostSlot` component type) that the
backend emits as a named empty 3D anchor. The host injects its own `Node3D` there at runtime. This is the
formalization of the escape-hatch: BoomHud owns the composed UI; the app owns its bespoke 3D content.

#### 6. Camera / stage (minimal)

Optionally emit a `stage` with **named camera anchors/modes** (e.g. `hero`, `flythrough`, `scrub`) as data;
framing behavior (transitions, scrub coupling) stays in the host/VM. No camera *logic* is generated.

### MSBuild / CLI Integration

- `BoomHud.Cli generate ... --target godot` honors `spatial` nodes when the Godot manifest reports
  `spatial3D = Native`; for `Unsupported` targets it emits the 2D fallback + a diagnostic.
- No new MSBuild properties required for Phase 1; the MVVM generator is unaffected (spatial is a view/layout
  concern, not a VM concern).

### Backward Compatibility

Fully additive. `ComponentNode.Spatial` defaults to `null`; all existing documents, generators, and snapshots
are byte-for-byte unaffected. Capability defaults to `Unsupported` for any manifest that does not opt in.

**Consumer note (fantasim-app-godot tunnel timeline):** the generated spatial scene is the *view*; the
**cylinder layout math that changes at runtime** (which clip is at the mouth, scrub depth) is supplied by the
reloadable view-model, consistent with the app's resident-view + reloadable-service (`IWorldGlobeMeshService`)
pattern. BoomHud generates the card faces, the spatial host scaffold, and the HUD chrome; the VM stays the
live-tunable, hot-reloadable layer.

### Security / Performance Considerations

- Retained-mode (one surface per item) is bounded by the data window; generators should support an item cap /
  LOD-fade hint in `SpatialSpec` (future). Distant-surface fade is a backend concern.
- `SubViewport`-per-card (Godot) can be costly; backends may batch identical faces or atlas textures. Phase 1
  may use a single shared face material with per-instance params before optimizing.

## Alternatives Considered

1. **3D scene-graph vocabulary in the IR** (new `Node3D`/`Mesh`/`Camera` component types). Rejected: doubles
   the IR surface, makes every backend reason about a scene graph, and abandons the "compose 2D UI" identity.
   The spatial-layout approach reuses the entire 2D component model.
2. **Keep 3D out of BoomHud; hand-author the tunnel.** Rejected by project direction: we want generation as
   the default, and the 2D parts (cards, HUD) plus the VM are squarely generatable.
3. **A separate `Gen.Godot3D` backend.** Rejected for Phase 1: spatial is a layout dimension shared across
   backends, not a Godot-only output format; it belongs in the shared IR + each backend's capability level.

## Open Questions

1. `SpatialSpec` as a dedicated record (this RFC) vs folding into `LayoutSpec`. Proposed: dedicated, since
   `LayoutSpec` is all 2D `Dimension`s.
2. Curved vs flat card faces (`Curvature`) — Phase 1 may ship flat quads only.
3. How rich the `stage`/camera-anchor model should be, or whether to defer it entirely to the host for v1.
4. Whether `hostSlot` is a new `ComponentType` or a reuse of `SlotKey` + a marker capability.
5. Face-rendering strategy per backend (SubViewport-per-card vs atlas) and the perf cap defaults.

## Phased Rollout

- **Phase 1 (IR foundation):** `SpatialSpec` + `SpatialShape`/`SpatialFacing` enums + `ComponentNode.Spatial`;
  `spatial3D`/`perspectiveCamera` capability constants + per-backend levels; schema + DSL parse;
  unit tests (DSL -> IR round-trip, capability gating). **No generator emission yet.** This is the bounded
  first dispatch.
- **Phase 2 (Godot spatial emission):** `Gen.Godot` emits the `Node3D` host + per-item face (SubViewport
  quad) + placement for `Shape = Cylinder`; capability fallback for unsupported targets; golden/snapshot test.
- **Phase 3 (host slots + camera anchors):** `hostSlot` + named camera anchors.
- **Phase 4 (other backends):** Unity native, then React/WebGL; Terminal.Gui 2D fallback.
- **Phase 5 (consumer):** wire the fantasim-app-godot tunnel timeline onto the generated spatial scene + the
  reloadable VM.

## Related RFCs

- RFC-0002: Component Model
- RFC-0003: Layout System
- RFC-0004: Data Binding (the `*From` axis mappings reuse `BindingSpec`)
- RFC-0012: Unity UI Toolkit Backend
- RFC-0013: Native Godot Scene (.tscn) Generation (the spatial-emission path extends this)
