# RFC-0024 Cylinder First-Pixels Smoke Report

Date: 2026-06-02
Agent: Kimi Code CLI

## Summary

SUCCESS: A PNG screenshot was produced from a generated RFC-0024 Cylinder spatial scene. The image validates the ring arrangement of 8 cards around a cylinder. The screenshot is available at:

`.agent/tmp/rfc0024-cylinder-smoke/cylinder.png`

## What Was Done

### 1. Sample HudDocument

Mirrored `GodotGeneratorTests.Generate_WithCylinderSpatial_EmitsNode3DHostAndQuadFaces` exactly:

- `HudDocument.Name = "Tunnel"`
- Root: `ComponentType.Container`
- `Spatial = SpatialSpec { Shape = Cylinder, Radius = 2.0, AngularSpacingDeg = 45.0, DepthScale = 1.0, Facing = Inward }`
- 8 `Label` children with ids `card1`..`card8`

### 2. Generation Method

Used a **tiny throwaway C# harness** (not the CLI) because it was the most reliable path:

- Harness: `.agent/tmp/rfc0024-cylinder-smoke/harness/Harness.csproj`
- References `BoomHud.Abstractions.csproj` and `BoomHud.Gen.Godot.csproj`
- Instantiates `GodotGenerator()`, calls `Generate(doc, options)` with `EmitTscn = true`, `EmitViewModelInterfaces = false`, `EmitTscnAttachScript = false`
- Writes `result.Files` to `.agent/tmp/rfc0024-cylinder-smoke/generated/`

Command:
```powershell
cd .agent\tmp\rfc0024-cylinder-smoke\harness
dotnet run
```

Generated files:
- `TunnelView.tscn`
- `TunnelView.cs`

### 3. Render Harness

Wrote a minimal GDScript `RenderHarness.gd` (extends `SceneTree`) that:
- Loads `res://generated/TunnelView.tscn`
- Iterates `MeshInstance3D` children and assigns `QuadMesh` + colored unshaded `StandardMaterial3D`
- Adds an active `Camera3D` at `(0, 0, -6)` looking down the +Z axis (rotation Y = 180 deg), FOV 60
- Adds a `DirectionalLight3D`
- Captures the root viewport after 6 frames and saves PNG

**Simplification**: The generated `.tscn` contains `MeshInstance3D` nodes with `SubViewport` children but no mesh/material (the companion `.cs` script wires them up in `_Ready()`). Since compiling C# in a throwaway Godot project is fiddly, the harness assigns solid colored `QuadMesh` with unshaded materials directly. This validates geometry (transforms) without needing the C# script or face textures.

### 4. Godot Invocation

Headless rendering on Windows with Godot 4.6.1 falls back to the `dummy` renderer, so the real OpenGL3 driver was used without `--headless`:

```powershell
cd .agent\tmp\rfc0024-cylinder-smoke
& "C:\lunar-horse\tools\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe" `
  --rendering-driver opengl3 `
  --quit-after 120 `
  --script res://RenderHarness.gd `
  -- --out "C:\lunar-horse\plate-projects\boom-hud\.agent\tmp\rfc0024-cylinder-smoke\cylinder.png"
```

The window opens briefly and auto-closes after capture.

## Result

- **PNG location**: `.agent/tmp/rfc0024-cylinder-smoke/cylinder.png` (1280x720, 6260 bytes)
- **Rendered**: YES
- **What it shows**: 8 colored rectangular quads arranged in a clear octagonal ring (cylinder cross-section). Each card is at a different angular position around the ring, matching the 45-degree spacing. The cards are viewed edge-on because the camera looks down the cylinder axis; the ring arrangement is the dominant visible feature.
- **Ring**: Visible - 8 cards in an octagon around the center
- **Recession**: Cards are spaced at Z=0..7; looking straight down the axis makes recession less visible than an angled shot would
- **Facing**: Cards appear as thin rectangles because their generated transforms orient them edge-on to the axial camera

## Generated .tscn Content

```
[gd_scene load_steps=1 format=3]

[node name="TunnelView" type="Node3D"]

[node name="card1Face" type="MeshInstance3D" parent="."]
transform = Transform3D(0, 0, -1, -1, 0, -0, 0, 1, 0, 2, 0, 0)

[node name="SubViewport" type="SubViewport" parent="card1Face"]
size = Vector2i(256, 128)

[node name="card1" type="Label" parent="card1Face/SubViewport"]
text = "Card 1"

[node name="card2Face" type="MeshInstance3D" parent="."]
transform = Transform3D(0.7071067811865476, 0, -0.7071067811865476, -0.7071067811865476, 0, -0.7071067811865476, 0, 1.0000000000000002, 0, 1.4142135623730951, 1.4142135623730951, 1)

[node name="SubViewport" type="SubViewport" parent="card2Face"]
size = Vector2i(256, 128)

[node name="card2" type="Label" parent="card2Face/SubViewport"]
text = "Card 2"

[node name="card3Face" type="MeshInstance3D" parent="."]
transform = Transform3D(1, 0, -6.123233995736766E-17, -6.123233995736766E-17, 0, -1, 0, 1, 0, 1.2246467991473532E-16, 2, 2)

[node name="SubViewport" type="SubViewport" parent="card3Face"]
size = Vector2i(256, 128)

[node name="card3" type="Label" parent="card3Face/SubViewport"]
text = "Card 3"

[node name="card4Face" type="MeshInstance3D" parent="."]
transform = Transform3D(0.7071067811865476, 0, 0.7071067811865475, 0.7071067811865475, 0, -0.7071067811865476, -0, 1, 0, -1.414213562373095, 1.4142135623730951, 3)

[node name="SubViewport" type="SubViewport" parent="card4Face"]
size = Vector2i(256, 128)

[node name="card4" type="Label" parent="card4Face/SubViewport"]
text = "Card 4"

[node name="card5Face" type="MeshInstance3D" parent="."]
transform = Transform3D(1.2246467991473532E-16, 0, 1, 1, 0, -1.2246467991473532E-16, -0, 1, 0, -2, 2.4492935982947064E-16, 4)

[node name="SubViewport" type="SubViewport" parent="card5Face"]
size = Vector2i(256, 128)

[node name="card5" type="Label" parent="card5Face/SubViewport"]
text = "Card 5"

[node name="card6Face" type="MeshInstance3D" parent="."]
transform = Transform3D(-0.7071067811865475, 0, 0.7071067811865477, 0.7071067811865477, -0, 0.7071067811865475, 0, 1, 0, -1.4142135623730954, -1.414213562373095, 5)

[node name="SubViewport" type="SubViewport" parent="card6Face"]
size = Vector2i(256, 128)

[node name="card6" type="Label" parent="card6Face/SubViewport"]
text = "Card 6"

[node name="card7Face" type="MeshInstance3D" parent="."]
transform = Transform3D(-1, 0, 1.8369701987210297E-16, 1.8369701987210297E-16, -0, 1, 0, 1, 0, -3.6739403974420594E-16, -2, 6)

[node name="SubViewport" type="SubViewport" parent="card7Face"]
size = Vector2i(256, 128)

[node name="card7" type="Label" parent="card7Face/SubViewport"]
text = "Card 7"

[node name="card8Face" type="MeshInstance3D" parent="."]
transform = Transform3D(-0.7071067811865477, 0, -0.7071067811865474, -0.7071067811865474, 0, 0.7071067811865477, 0, 1, 0, 1.4142135623730947, -1.4142135623730954, 7)

[node name="SubViewport" type="SubViewport" parent="card8Face"]
size = Vector2i(256, 128)

[node name="card8" type="Label" parent="card8Face/SubViewport"]
text = "Card 8"
```

## Blockers / Notes

1. **Headless dummy renderer**: On Windows, `Godot --headless` uses the dummy rendering server and cannot capture viewport textures. The workaround was to omit `--headless` and use `--rendering-driver opengl3`, which opens a window briefly before `--quit-after` closes it.
2. **look_at in SceneTree script**: `Camera3D.look_at()` fails with "Node not inside tree" when called immediately after `add_child()` in a `SceneTree` script, even though the parent is in the tree. Workaround: set `rotation_degrees` directly.
3. **No C# compilation**: The throwaway render harness uses GDScript and assigns meshes/materials manually instead of compiling/running the generated `.cs` companion script. This is an intentional simplification per the RFC allowance.
4. **Edge-on facing**: The generated transforms produce quads that are edge-on to an axial camera, so the screenshot shows thin rectangular lines rather than full card faces. The ring geometry is clearly validated.
