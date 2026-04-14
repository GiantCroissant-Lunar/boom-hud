# RFC-0023 Fidelity Handover - 2026-04-14

## Scope

This handover covers the current RFC-0023 implementation state for the `uGUI`/UITK fidelity work, the source-semantic enrichment slice, the expanded optimizer surface, and the latest `TheAltersCrafting` checkpoint results as of April 14, 2026.

The work in this batch was not a single narrow fix. It includes:

- RFC-0023 and source-semantic enrichment plumbing
- narrower shared semantic classes and backend-calibrated metric profiles
- subtree/local-repair scaffolding and sweep expansion
- micro-fixture corpus for compact text/icon failure modes
- multiple `uGUI` backend-native realization slices
- checkpoint runs against dense fixtures, especially `the-alters-crafting-ugui`

## What Landed

### 1. RFC-0023 scaffold is now real code, not just planning

Added the RFC document:

- `docs/rfcs/RFC-0023-hybrid-source-semantic-fidelity-pipeline.md`

Added a source-semantic layer and threaded it into generation and diagnostics:

- `dotnet/src/BoomHud.Generators/SourceSemantics/`
- `dotnet/src/BoomHud.Generators/GenerationDocumentPreprocessor.cs`
- `dotnet/src/BoomHud.Generators/VisualIR/VisualDocument.cs`
- `dotnet/src/BoomHud.Generators/VisualIR/VisualDocumentBuilder.cs`
- `dotnet/src/BoomHud.Generators/VisualIR/VisualRefinementPlanner.cs`
- `dotnet/src/BoomHud.Cli/Handlers/Baseline/ImageSimilarityHandler.cs`
- `dotnet/src/BoomHud.Abstractions/Generation/IBackendGenerator.cs`

Source semantics now carry richer per-node evidence such as:

- source semantic role
- asset realization
- sibling/row context
- right-edge and compact-row cues
- shell hints used by `uGUI`

Measured-layout and refinement reports can now summarize by semantic class and source-semantic role, not only by surface.

### 2. Phase-1 fidelity surface expansion landed

Shared semantic classes were expanded in the generator/rule path, including:

- `compact-numeric-readout`
- `compact-label`
- `button-label`
- `tab-label`
- `right-aligned-quantity`
- `inline-icon`
- `leading-icon`
- `badge-icon`
- `value-row`

Relevant files:

- `dotnet/src/BoomHud.Generators/RuleResolver.cs`
- `dotnet/src/BoomHud.Generators/VisualIR/VisualDocumentBuilder.cs`
- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`

The sweep/action library was expanded to target these classes:

- `scripts/run-fixture-rule-sweep.ps1`

The `uGUI` subtree scaffolder now creates bounded repair catalogs for:

- leaf text
- leaf icons
- `value-row` containers
- right-edge quantity/compact row cases

Relevant file:

- `dotnet/src/BoomHud.Cli/Handlers/Rules/UGuiSubtreeCandidateScaffoldHandler.cs`

### 3. Micro-fixture corpus landed

Added under `samples/pencil`:

- `compact-numeric-counter.pen`
- `tab-strip-mini.pen`
- `wrapped-body-micro.pen`
- `icon-label-row.pen`
- `right-aligned-quantity-row.pen`

Supporting manifests:

- `fidelity/micro-corpus/`
- `build/fixture-manifests/micro-fixture-compare.json`

### 4. `uGUI` backend realization is more source-aware than before

The following `uGUI` slices landed:

- right-aligned quantity width preservation
- `value-row` width and spacing preservation
- source image asset behavior
- source icon glyph intrinsic sizing
- icon-shell centering
- compact-row label no-wrap defaults
- compact-row label width hugging
- tab-button/tab-label width and no-wrap behavior
- semantic-row trailing carrier/right-edge alignment
- chip-shell text hugging/centering
- TMP letter-spacing support
- limited TMP rendering hints for pixel-font text

Most of this work is in:

- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UGuiGeneratorTests.cs`

## Scores and Checkpoints

### Dense `uGUI` progress

The source-aware work did improve the dense `uGUI` winner, but only modestly.

Representative checkpoint:

- `build/fixture-rule-sweeps/cem-primary-isolated-post-source-semantics-2026-04-14-r1/optimizer-summary.json`

Reported movement:

- `the-alters-crafting-ugui`: `85.55 -> 85.91`
- `cyberpunk-crafting-ugui`: `83.43 -> 83.69`
- `fortnite-inventory-ugui`: `83.27 -> 83.27`
- average dense `uGUI`: `84.0833 -> 84.29`
- average all-backend: `84.3833 -> 84.4867`

### Biggest real jump in this batch

The only clearly meaningful structural jump came from the fixed vertical panel-stack work around `AssemblyStage`.

Relevant run:

- `build/fixture-rule-sweeps/cem-the-alters-post-assembly-stack-2026-04-14-r1/optimizer-summary.json`

Key result:

- baseline `the-alters-crafting-ugui`: `85.55 -> 88.41`
- best selected candidate: `88.62`

Important nuance:

- the `88.62` result was achieved by a combination of actions
- `assemblystage-tight-layout` was present in the search frontier, but it was not the sole lever

### Current strict baseline state after the latest `uGUI` slices

Latest narrow strict checks:

- `build/fixture-rule-sweeps/strict-the-alters-post-letter-spacing-2026-04-14-r1`
- `build/fixture-rule-sweeps/strict-the-alters-post-tmp-hints-v2-2026-04-14-r1`

Current `the-alters-crafting-ugui` strict baseline remains:

- `88.42%`

The TMP hint experiment briefly regressed to `88.38%` when `extraPadding` was enabled for pixel-font text; that was rolled back. The trimmed version returned to `88.42%`.

## What Did Not Work

These slices changed real output but did not meaningfully improve the score plateau:

- `TopBar` child-width hugging
- `TopBar` right-edge carrier behavior
- chip-shell text hugging/centering
- TMP letter-spacing alone
- TMP text-rendering hints alone

Interpretation:

- the remaining gap is no longer dominated by a single missing wrap/spacing flag
- many local regions are now "reasonable"
- score movement has entered diminishing returns for one-by-one heuristics

## Current Read of the Problem

The repo is now in a different state than at the beginning of the batch.

What is no longer the main blocker:

- broad optimizer changes
- trivial text wrap toggles
- basic compact-row width preservation

What still looks like the blocker:

- backend-native text rasterization/material behavior for pixel fonts
- stronger source-semantic-to-backend realization, especially for dense text regions
- possibly a more explicit source-to-realization strategy layer for `uGUI`, not just more helper conditions

The practical read is:

- local geometry is much less wrong than before
- remaining visible drift is largely "text renders differently" and "dense screen still does not feel authored the same way"

## DA Assets Comparison Takeaway

We also reviewed:

- `samples/UnityFullPenCompare/Assets/D.A. Assets`

The conclusion was:

- do not replace BoomHud’s architecture
- do copy the early source-semantic and backend-native realization ideas

BoomHud still has the stronger long-term architecture because it already has:

- UI IR
- Motion IR
- Visual IR
- rules/actions/planning
- automated scoring/refinement

The RFC-0023 direction remains correct:

- keep BoomHud’s IR-centric architecture
- add stronger source-semantic enrichment
- make backend realization more native

## Known Issues

### 1. Remotion still fails in the sweep/review loop

This remains unresolved and is unrelated to the `uGUI` score path.

Current error source:

- `remotion/src/GeneratedFixtureDemo.tsx`

Current runtime failure:

- `import.meta.glob`
- `TypeError: {}.glob is not a function`

This happens after Unity capture/scoring, so it does not block the `uGUI` numeric checkpoints, but it does keep the full motion/remotion leg noisy.

### 2. The worktree includes broad generated and sample-scene churn

This batch touched more than just the few files from the last two `uGUI` slices. The worktree includes:

- source-semantic infrastructure
- RFC-0023
- expanded tests
- sweep tooling
- generated Unity sample outputs
- updated Unity scenes/assets

This is expected for the fidelity batch, but the next session should avoid assuming that only `UGuiGenerator.cs` changed.

## Recommended Next Session

The next session should not continue the same one-by-one heuristic loop blindly.

Preferred order:

1. Decide whether to stay on `uGUI` text rendering or move to a stronger source-semantic realization slice.
2. If staying on text, investigate TMP asset/material/raster behavior for `Press Start 2P`, not more generic layout tweaks.
3. If moving structurally, start an explicit `uGUI` realization strategy layer for dense semantic text regions and shells, instead of adding more helper conditions in `UGuiGenerator`.
4. Fix the Remotion `import.meta.glob` issue separately so fidelity runs stop failing late in the pipeline.

What I would avoid next:

- more tiny compact-label/no-wrap heuristics
- more `TopBar`-only tweaks
- more subtree action proliferation without a new backend-native realization seam

## Suggested Starting Files for the Next Session

For backend-native text/rendering work:

- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`
- `samples/UnityFullPenCompare/Assets/D.A. Assets/Figma-Converter-for-Unity/Runtime/Scripts/Drawers/Canvas/TextDrawers/TextMeshDrawer.cs`
- `samples/UnityFullPenCompare/Assets/Resources/BoomHudFonts/PressStart2P-Regular.asset`

For structural RFC-0023 realization work:

- `dotnet/src/BoomHud.Generators/SourceSemantics/`
- `dotnet/src/BoomHud.Generators/VisualIR/VisualDocumentBuilder.cs`
- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`

For sweep/checkpoint validation:

- `scripts/run-fixture-rule-sweep.ps1`
- `build/fixture-rule-sweeps/strict-the-alters-post-tmp-hints-v2-2026-04-14-r1/baseline-no-rules/scores/the-alters-crafting-ugui.json`
- `build/fixture-rule-sweeps/cem-the-alters-post-assembly-stack-2026-04-14-r1/optimizer-summary.json`

## Verification Performed In This Batch

Repeatedly run during the session:

- `dotnet test dotnet/tests/BoomHud.Tests.Unit/BoomHud.Tests.Unit.csproj -p:UseSharedCompilation=false --filter "FullyQualifiedName~UGuiGeneratorTests"`
- `dotnet run --project dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj -- generate samples/pencil/the-alters-crafting.pen --target ugui --output samples/UnityFullPenCompare/Assets/BoomHudGeneratedUGui --namespace Generated.Hud`
- strict TheAlters Unity-backed checkpoints via `scripts/run-fixture-rule-sweep.ps1`

Latest strict result worth carrying forward:

- `build/fixture-rule-sweeps/strict-the-alters-post-tmp-hints-v2-2026-04-14-r1/baseline-no-rules/scores/the-alters-crafting-ugui.json`
- `88.42%`
