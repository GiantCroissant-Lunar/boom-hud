# UI + Motion IR Hardening Handover

This handover covers the recent contract-hardening work for shared UI IR and motion IR across Pencil import, React/Remotion, Unity, and Godot.

## Start here

1. Read `docs/rfcs/RFC-0020-ui-motion-ir-contract-hardening.md`.
2. Treat the UI IR and motion IR changes in this handoff as one coordinated contract update, not isolated backend tweaks.
3. Keep backend ownership boundaries intact:
   - C# tests validate C# importers and generators
   - Remotion tests validate Remotion runtime behavior in TS
   - Shared fixture shape can match, but test harnesses should stay native to each backend/runtime

## What was fixed

### UI IR layout semantics

- `LayoutSpec` now carries neutral layout semantics for:
  - `Left`
  - `Top`
  - `IsAbsolutePositioned`
  - `ClipContent`
- Pencil conversion maps absolute placement and clipping into those IR fields instead of relying only on backend-specific metadata.
- React and Unity now prefer neutral IR layout fields for absolute placement and clipping, with Pencil metadata kept only as compatibility fallback.

### Pencil component instance semantics

- Pencil `ref` nodes now preserve `ComponentRefId` in the IR.
- Expanded descendant trees are still materialized so overrides remain visible to builders.
- Descendant ids inside expanded instances are scoped under the instance id, which prevents target collisions across repeated component instances.

### Motion contract portability

- Portable motion values are now scalar-only for the shared contract:
  - `number`
  - `boolean`
  - `text`
- Shared motion validation now flags non-portable values through the abstractions layer.
- Re-emitting non-portable motion JSON is rejected instead of silently round-tripping unsupported authoring patterns.
- Unity and Godot exporters now consume the shared portability rule instead of carrying drift-prone local checks.
- React/Remotion types were tightened to the same scalar-only contract.

### Motion target alignment

- Motion target lookup now consistently keys on `track.TargetId`.
- Shared fixtures cover:
  - root target
  - component target
  - nested element target
  - nested component target

### Test ownership cleanup

- The temporary C# to Node bridge test was removed.
- Generator contract coverage remains in .NET.
- Remotion runtime contract coverage now lives in native TypeScript tests under `remotion/src/motion`.

## Main files touched

### Core abstractions and import

- `dotnet/src/BoomHud.Abstractions/IR/LayoutSpec.cs`
- `dotnet/src/BoomHud.Abstractions/Motion/MotionDocument.cs`
- `dotnet/src/BoomHud.Abstractions/Diagnostics/BoomHudDiagnostic.cs`
- `dotnet/src/BoomHud.Dsl.Pencil/PenToIrConverter.cs`

### Backends

- `dotnet/src/BoomHud.Gen.React/ReactGenerator.cs`
- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`
- `dotnet/src/BoomHud.Gen.Unity/UnityMotionExporter.cs`
- `dotnet/src/BoomHud.Gen.Godot/GodotMotionExporter.cs`
- `remotion/src/motion/schema.ts`
- `remotion/src/motion/authoring.ts`
- `remotion/src/motion/runtime.ts`
- `schemas/json/motion.schema.json`

### Tests

- `dotnet/tests/BoomHud.Tests.Unit/Dsl/PenParserTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/ReactGeneratorTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UnityGeneratorTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Integration/PencilEndToEndTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Motion/MotionDocumentTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Motion/UnityMotionExporterTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Motion/GodotMotionExporterTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Motion/MotionContractFixture.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Motion/GeneratorMotionContractTests.cs`
- `remotion/src/motion/test-fixtures/portable-contract.fixture.ts`
- `remotion/src/motion/runtime.contract.test.ts`

## Verification already run

### .NET

- `dotnet test dotnet\BoomHud.sln --filter "FullyQualifiedName~PenParserTests|FullyQualifiedName~ReactGeneratorTests|FullyQualifiedName~UnityGeneratorTests|FullyQualifiedName~PencilEndToEndTests"`
- `dotnet test dotnet\BoomHud.sln --filter "FullyQualifiedName~PenParserTests|FullyQualifiedName~PencilEndToEndTests|FullyQualifiedName~ReactGeneratorTests|FullyQualifiedName~UnityGeneratorTests|FullyQualifiedName~UnityMotionExporterTests|FullyQualifiedName~MotionDocumentTests"`
- `dotnet test dotnet\BoomHud.sln --filter "FullyQualifiedName~MotionDocumentTests|FullyQualifiedName~UnityMotionExporterTests|FullyQualifiedName~GodotMotionExporterTests"`
- `dotnet test dotnet\BoomHud.sln --filter "FullyQualifiedName~GeneratorMotionContractTests|FullyQualifiedName~ReactGeneratorTests|FullyQualifiedName~UnityMotionExporterTests|FullyQualifiedName~MotionDocumentTests"`

### Remotion

- `npx tsc --noEmit`
- `npm test`

## Important decisions

1. Neutral IR should own shared semantics. Backend-specific metadata can exist for compatibility, but not as the only source of meaning.
2. Expanded Pencil instances are acceptable in the IR as long as reusable identity is preserved with `ComponentRefId` and descendant target ids remain stable.
3. Portable motion v1 is scalar-only. If vector motion is needed later, it should return as an explicit versioned contract change with backend support and tests.
4. Shared fixtures are good. Shared harnesses across runtimes are not.

## Remaining high-value work

1. Add more cross-backend fixture cases for sequences, fill modes, and overlapping clips.
2. Add fixture coverage for repeated component instances with the same source component but different override paths.
3. Verify whether Godot should also get a native runtime-side contract test or whether exporter-level coverage is sufficient for now.
4. If a future Pen/component pipeline introduces richer override semantics, confirm the scoped descendant id rule still matches builder expectations.

## Workspace caution

- The repo is dirty in many unrelated files outside this handoff.
- Do not mass-revert workspace changes.
- If you cherry-pick or split follow-up work, scope it to the files listed above plus this handoff doc and the RFC.
