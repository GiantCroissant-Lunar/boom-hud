# RFC-0024 Phase 2 -- Fix Cylinder Math (2026-06-02)

## Path Chosen

**Baked `.tscn` transform** (Approach 1). `ComputeCylinderTransformString` now computes the real position + basis using the same formula that the dead `ComputeCylinderTransform` had. The runtime `ComputeCylinderTransform` method in the companion C# script was deleted because it was never called.

## Transform Formula (as implemented in `ComputeCylinderTransformString`)

For each face index `i`:

```
angleDeg = i * spatial.AngularSpacingDeg
angleRad = angleDeg * PI / 180
radius   = spatial.Radius
depth    = i * spatial.DepthScale        // TODO(RFC-0024 Phase 5): replace with DepthFrom data

// Position on cylinder
x = radius * cos(angleRad)
y = radius * sin(angleRad)
z = depth

// Basis: facing-oriented orthonormal frame
zAxis = facing == Inward ? (-cosA, -sinA, 0) : (cosA, sinA, 0)  // direction face points
zAxis = normalize(zAxis)
up    = (0, 0, 1)                                               // cylinder-space up
xAxis = normalize(cross(up, zAxis))                             // fallback (1,0,0) if degenerate
yAxis = cross(zAxis, xAxis)                                     // already normalized

// Godot Transform3D(Basis(xAxis, yAxis, zAxis columns), origin(x, y, z))
```

The emitted `Transform3D(...)` string in the `.tscn` now contains all 12 values (9 basis + 3 origin) instead of the previous identity basis + hardcoded z=0.

## Dead/Duplicate Math Removal

- **Deleted** the ~25-line `ComputeCylinderTransform(double, double, double, SpatialFacing)` method from the companion script emission block in `GenerateViewClass`. It was defined but never called from `SetupSpatialFaces()` or anywhere else.
- The single source of truth is now `ComputeCylinderTransformString(int, SpatialSpec)` in the generator, which bakes the correct transform directly into the `.tscn` file.
- The test assertion `csFile.Content.Should().Contain("ComputeCylinderTransform")` was updated to `.NotContain("private static Transform3D ComputeCylinderTransform")` to guard against regression (dead code reappearing).

## Strengthened Test Assertions

### In existing `Generate_WithCylinderSpatial_EmitsNode3DHostAndQuadFaces`:
- **Depth varies**: Asserts `tscnFile.Content.Should().Contain(", 0, 0)")` (card1 index 0 => z=0) AND `tscnFile.Content.Should().Contain(", 1)")` (card2 index 1 => z=1 with default DepthScale=1.0). A flat ring would have both at z=0.
- **Non-identity basis**: Asserts `tscnFile.Content.Should().NotContain("Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1,")` -- the old baked transform was identity basis.

### New test `Generate_WithCylinderSpatialOutward_ProducesDifferentBasisFromInward`:
- Generates two documents identical except `Facing = Inward` vs `Facing = Outward`.
- Asserts both produce non-identity basis.
- Extracts the `Transform3D(...)` string from each `.tscn` via regex helper `ExtractTransform3D` and asserts they differ -- proving the facing parameter actually changes the basis.

## Files Touched

| File | Change |
|------|--------|
| `dotnet/src/BoomHud.Gen.Godot/GodotGenerator.cs` | Rewrote `ComputeCylinderTransformString` (~18 lines -> ~50 lines) with full facing-aware basis computation; deleted dead `ComputeCylinderTransform` runtime method (~28 lines) |
| `dotnet/tests/BoomHud.Tests.Unit/Generation/GodotGeneratorTests.cs` | Strengthened existing cylinder test with depth/facing assertions; added new `Generate_WithCylinderSpatialOutward_ProducesDifferentBasisFromInward` test; added `ExtractTransform3D` helper |

## Build/Test Outcome

- `dotnet build dotnet/BoomHud.sln -c Release` -- **SUCCESS** (0 errors)
- `dotnet test dotnet/BoomHud.sln -c Release` -- **813 tests, 0 failures** (including all Godot generator tests)
