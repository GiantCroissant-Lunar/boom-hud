# RFC-0024 Phase 1 Implementation Report

## Summary

Phase 1 (additive IR foundation) of RFC-0024 has been implemented and all builds/tests pass.

## Files Added

- `dotnet/src/BoomHud.Abstractions/IR/SpatialSpec.cs` -- new IR types:
  - `SpatialShape` enum: `Cylinder`, `Radial`, `Helix`, `Grid3D`, `Free`
  - `SpatialFacing` enum: `Inward`, `Outward`, `Camera`
  - `SpatialSpec` sealed record with:
    - `Shape` (required)
    - `Facing` (default `Camera`)
    - `AngleFrom` / `DepthFrom` / `RadiusFrom` / `LateralFrom` (each `BindingSpec?`)
    - `Radius` (default `1.0`)
    - `AngularSpacingDeg` (default `30.0`)
    - `DepthScale` (default `1.0`)
    - `Curvature` (default `0.0`)

- `dotnet/tests/BoomHud.Tests.Unit/IR/SpatialSpecTests.cs` -- unit tests covering:
  - JSON round-trip with a populated `spatial` cylinder block
  - JSON round-trip with no `spatial` => `Spatial == null`
  - JSON round-trip with minimal `spatial` (defaults applied)
  - Capability gating: Godot = Native, Terminal.Gui = Unsupported
  - Unknown feature fallback = Unsupported

## Files Changed

- `dotnet/src/BoomHud.Abstractions/IR/ComponentNode.cs` -- added `public SpatialSpec? Spatial { get; init; }`
- `dotnet/src/BoomHud.Abstractions/Capabilities/ICapabilityManifest.cs` -- added:
  - `public const string Spatial3D = "spatial3D";`
  - `public const string PerspectiveCamera = "perspectiveCamera";`
- `schemas/json/boom-hud.schema.json` -- added optional `spatial` object to `componentNode` and new `$defs/spatialSpec` definition
- Backend capability manifests (added `[Capabilities.Spatial3D]` entry only):
  - `dotnet/src/BoomHud.Gen.Godot/GodotCapabilities.cs` -- `Native`
  - `dotnet/src/BoomHud.Gen.Unity/UnityCapabilities.cs` -- `Native`
  - `dotnet/src/BoomHud.Gen.React/ReactCapabilities.cs` -- `Native`
  - `dotnet/src/BoomHud.Gen.Avalonia/AvaloniaCapabilities.cs` -- `Unsupported`
  - `dotnet/src/BoomHud.Gen.TerminalGui/TerminalGuiCapabilities.cs` -- `Unsupported`
  - `dotnet/src/BoomHud.Gen.UGui/UGuiCapabilities.cs` -- `Unsupported`
  - `dotnet/src/BoomHud.Gen.Remotion/RemotionCapabilities.cs` -- `Unsupported`
  - `dotnet/src/BoomHud.Gen.Pencil/PencilCapabilities.cs` -- `Unsupported`

## Schema + Parser Extension

The schema was updated first (schema-first per project conventions). The `spatial` object was added to `componentNode` as an optional property referencing the new `$defs/spatialSpec` definition, which mirrors `SpatialSpec` exactly:
- `shape` (required string enum)
- `facing` (string enum, default `camera`)
- `angleFrom` / `depthFrom` / `radiusFrom` / `lateralFrom` (each `$ref` to `binding`)
- `radius` / `angularSpacingDeg` / `depthScale` / `curvature` (numbers with defaults)

The IR-JSON loader (`JsonSerializer.Deserialize<HudDocument>` in `BoomHud.Cli`) deserializes `SpatialSpec` automatically because the existing `JsonSerializerOptions` uses `PropertyNameCaseInsensitive = true` and `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`. No explicit parser code changes were required for the IR-JSON loader.

The Figma and Pencil DSL parsers construct `ComponentNode` via object initializers and do not set `Spatial`; therefore `Spatial` remains `null` for documents parsed through those paths. This satisfies the "absent spatial => Spatial stays null" requirement without code changes.

## Build / Test Results

```
dotnet build dotnet/BoomHud.sln -c Release
  -> Build succeeded (0 errors, 0 warnings)

dotnet test dotnet/BoomHud.sln -c Release --no-build
  -> BoomHud.Tests.Backends: 316 passed, 0 failed
  -> BoomHud.Tests.Unit: 487 passed, 0 failed
```

All new tests pass. No pre-existing test regressions were introduced.

## Deviations / Notes

- `Curvature` initial value was written as `= 0.0` in the first draft to match the RFC literally, but CA1805 (init to default) fired because `0.0` is the CLR default for `double`. The initializer was removed; the semantic default remains `0.0`.
- `BindingSpec.Property` is `required` in the IR type even though the schema `binding` definition only marks `path` as required. This is a pre-existing schema/IR mismatch. The test JSON includes a dummy `property` value on axis-mapping bindings to satisfy the deserializer.
- No generator emission logic was modified. Only capability manifest dictionaries were updated.
- No git operations were performed.
