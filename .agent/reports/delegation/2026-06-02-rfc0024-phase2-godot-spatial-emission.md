# RFC-0024 Phase 2 Implementation Report -- Godot Spatial Emission (Cylinder)

## Summary

Implemented Phase 2 of RFC-0024: `BoomHud.Gen.Godot` now emits a 3D spatial scene
when a `ComponentNode` has `Spatial != null` with `Shape == Cylinder` and the Godot
capability manifest reports `spatial3D == Native`. All other cases fall back to 2D
layout with a diagnostic. Scope is Cylinder only; no host slots, camera, Unity,
React, or app wiring.

## Files Changed

1. `dotnet/src/BoomHud.Generators/SpatialCompatibilityChecker.cs` (new)
   - Shared helper that walks a `HudDocument` and emits diagnostics for spatial
     nodes that the backend cannot handle (unsupported capability or non-Cylinder
     shape).

2. `dotnet/src/BoomHud.Gen.Godot/GodotGenerator.cs`
   - Added `SpatialCompatibilityChecker.CheckDocument` call at generation start.
   - `GenerateTscn`: root type becomes `Node3D` when `Spatial.Shape == Cylinder`.
   - `AppendChildNodesTscn`: delegates to `AppendSpatialChildTscn` when parent is
     a supported Cylinder spatial node.
   - `AppendSpatialChildTscn`: emits each child as a `MeshInstance3D` face node
     containing a `SubViewport` with the 2D child inside.
   - Static placement: emits `transform = Transform3D(...)` in `.tscn` computed
     from cylinder math (angle = index * AngularSpacingDeg, radius = Radius,
     depth = 0). Data-driven children (any `*From` binding present) skip the
     static transform so runtime placement can set it.
   - `GenerateViewClass`: emits `SetupSpatialFaces()` and
     `ComputeCylinderTransform(...)` helper methods when the document contains
     spatial nodes.
   - `SetupSpatialFaces`: at runtime, finds all `MeshInstance3D` face nodes,
     creates `QuadMesh`, wires `ViewportTexture` from the child `SubViewport`,
     and applies an unshaded transparent `StandardMaterial3D`.
   - `ComputeCylinderTransform`: deterministic cylinder math per RFC:
     - `angleRad = angleValue * PI / 180`
     - `position = (radius*cos(angle), radius*sin(angle), depth)`
     - `Facing.Inward` => `zAxis` points toward axis; `Outward` => away;
       `Camera` => `(0,0,1)` (billboard refinement deferred).

3. `dotnet/src/BoomHud.Gen.TerminalGui/TerminalGuiGenerator.cs`
   - Added `SpatialCompatibilityChecker.CheckDocument` so unsupported backends
     emit a diagnostic (`BHG3001`) when encountering spatial nodes instead of
     silently ignoring them.

4. `dotnet/tests/BoomHud.Tests.Unit/Generation/GodotGeneratorTests.cs`
   - `Generate_WithCylinderSpatial_EmitsNode3DHostAndQuadFaces`
   - `Generate_WithSpatialUnsupportedShape_FallsBackTo2DWithDiagnostic`
   - `Generate_WithoutSpatial_Generates2DLayoutAsBefore`
   - `TerminalGuiGenerator_WithSpatial_FallsBackTo2DWithDiagnostic`

## Emission Approach

### SubViewport-Quad (target, not placeholder)

The emitted `.tscn` contains:
- A `Node3D` spatial root.
- Per child: a `MeshInstance3D` with a `SubViewport` child that hosts the 2D
  component tree.

The C# companion script completes the wiring:
- Creates `ViewportTexture` from the `SubViewport`.
- Assigns it to a `StandardMaterial3D` (unshaded, alpha transparent).
- Sets `MeshInstance3D.Mesh = new QuadMesh()`.
- Applies the material via `SetSurfaceOverrideMaterial(0, material)`.

This is the SubViewport-quad path described in the RFC, not a `Sprite3D`
placeholder.

### Static vs Runtime Placement

- **Static children** (no `AngleFrom`/`DepthFrom`/`RadiusFrom`/`LateralFrom`):
  `transform` is baked into the `.tscn` at generation time using the cylinder
  math above.
- **Data-driven children** (any `*From` binding is present): the `.tscn` omits
  the static `transform`. The companion script emits `ComputeCylinderTransform`
  for runtime use by the host. Full `_Ready`/collection-changed instantiation
  and per-item transform binding is partially in place via the helper, but
  complete automated collection wiring is blocked by the absence of an explicit
  `ItemsSource`-style binding in the current IR. The host supplies the bound
  collection and calls the helper, consistent with the RFC consumer note that
  "the cylinder layout math that changes at runtime ... is supplied by the
  reloadable view-model."

## Cylinder Math as Implemented

```csharp
private static Transform3D ComputeCylinderTransform(
    double angleValue, double depthValue, double radiusValue, SpatialFacing facing)
{
    double angleRad = angleValue * Math.PI / 180.0;
    float x = (float)(radiusValue * Math.Cos(angleRad));
    float y = (float)(radiusValue * Math.Sin(angleRad));
    float z = (float)depthValue;
    var position = new Vector3(x, y, z);

    Vector3 zAxis = facing switch
    {
        SpatialFacing.Inward  => new Vector3(-(float)Math.Cos(angleRad), -(float)Math.Sin(angleRad), 0),
        SpatialFacing.Outward => new Vector3((float)Math.Cos(angleRad),  (float)Math.Sin(angleRad), 0),
        _                     => new Vector3(0, 0, 1)
    };

    zAxis = zAxis.Normalized();
    var up = new Vector3(0, 0, 1);
    var xAxis = up.Cross(zAxis).Normalized();
    if (xAxis.LengthSquared() < 0.001f) xAxis = new Vector3(1, 0, 0);
    var yAxis = zAxis.Cross(xAxis);
    var basis = new Basis(xAxis, yAxis, zAxis);
    return new Transform3D(basis, position);
}
```

`Curvature` is ignored (flat quads) as permitted by the RFC for Phase 2.
`Camera` facing is a stub (identity basis); billboard behavior is deferred.

## Fallback Behavior

| Condition | Behavior |
|-----------|----------|
| Backend `spatial3D != Native` | 2D layout emitted; diagnostic `BHG3001` warning. |
| Godot + `Spatial.Shape != Cylinder` | 2D layout emitted; diagnostic `BHG3002` warning. |
| Node without `Spatial` | Byte-for-byte same as before (regression-guarded). |

The fallback never throws and never emits broken 3D artifacts.

## Tests Added

All 4 new tests pass. Existing test suite unchanged (811 total tests pass).

- `Generate_WithCylinderSpatial_EmitsNode3DHostAndQuadFaces`
  - Asserts `.tscn` contains `Node3D`, `MeshInstance3D`, `SubViewport`,
    `Transform3D`.
  - Asserts C# contains `SetupSpatialFaces`, `ComputeCylinderTransform`,
    `QuadMesh`, `ViewportTexture`, `StandardMaterial3D`.

- `Generate_WithSpatialUnsupportedShape_FallsBackTo2DWithDiagnostic`
  - Uses `Radial` shape on Godot target.
  - Asserts warning diagnostic with "not supported".
  - Asserts `.tscn` has no `Node3D` and no `MeshInstance3D`.

- `Generate_WithoutSpatial_Generates2DLayoutAsBefore`
  - Regression guard.
  - Asserts no 3D nodes and no spatial helper methods in output.

- `TerminalGuiGenerator_WithSpatial_FallsBackTo2DWithDiagnostic`
  - Uses Terminal.Gui (Unsupported) backend with a Cylinder spatial node.
  - Asserts warning diagnostic with "Spatial layout is not supported".
  - Asserts generated code contains no 3D types.

## Build/Test Outcome

```
dotnet build dotnet/BoomHud.sln -c Release   -> SUCCESS (0 errors, 1 pre-existing warning)
dotnet test  dotnet/BoomHud.sln -c Release   -> 811 passed, 0 failed, 0 skipped
```

No pre-existing tests were broken.

## Blockers / Deviations

- **Data-driven collection wiring**: The IR does not yet carry an explicit
  `ItemsSource`-style binding for spatial repeaters. `ComputeCylinderTransform`
  is emitted and `SetupSpatialFaces` handles material wiring, but automatic
  per-collection-item instantiation and transform updates at runtime rely on
  the host/VM to drive the loop and call the helper. This is consistent with
  the RFC consumer note that runtime-scrub parameters live in the VM, but it
  is a partial implementation of the full "emit a companion script that
  instantiates one face per item" requirement.

- **Camera facing**: `SpatialFacing.Camera` returns identity basis in
  `ComputeCylinderTransform`. True billboard (e.g. material billboard mode or
  `_Process` LookAt) is deferred.

- **Curvature**: Flat quads only; `Curvature` is ignored per Phase 2 scope.

## Notes

- Did not touch Pencil/baseline WIP (`Gen.Pencil/`, `Commands/Pencil/`,
  `Handlers/Pencil/`, baseline files, scripts).
- Did not add host slots, camera anchors, Unity, React, or app wiring.
- ASCII only, no emoji in any committed output.
