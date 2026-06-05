# RFC-0026: Fidelity Reference Annotation Pipeline (Label Studio)

- **Status**: Draft
- **Created**: 2026-06-05
- **Authors**: BoomHud Contributors

## Summary

Adopt [Label Studio](https://github.com/HumanSignal/label-studio) (Apache-2.0) as an
**external, dev-time annotation editor** that produces and corrects the UI-region ground
truth — `uiMaskRects` plus typed region roles — that the BoomHud fidelity scorer already
consumes.

BoomHud owns only two small surfaces: a Label Studio **labeling config** and a **bidirectional
converter** between Label Studio's export JSON and the existing `reference-ui-masks.json`
manifest. The existing OpenCV and vision-LLM reference analyzers are reused as Label Studio
**pre-annotation backends**, so a human corrects auto-drawn boxes instead of hand-typing pixel
coordinates.

Label Studio is **not** vendored into the .NET solution, the NuGet feed, or any runtime path.
It is a local annotation tool, exactly like Pencil and Figma are external design tools today.

## Existing Surfaces Audited

Per the `audit-existing-surfaces` rule, the region-mask surface already exists. This RFC
**extends** it; it does not introduce a parallel annotation surface.

- **`fidelity/reference-ui-masks.json` + `scripts/reference-masks/*.mask.json`** — the manifest
  format: `{ fixtures: [{ fixtureSlug | rootId | key, uiMaskRects: [{ x, y, width, height }] }] }`.
  These rectangles are currently **hand-authored** by eyeballing reference screenshots.
- **`scripts/measure-pen-reference-similarity.ps1`** — the *consumer*. `Get-UiMaskRects`
  resolves rects by fixture key; `Write-MaskedImage` blacks out everything outside them on both
  reference and candidate; the masked **UI-only** score then becomes the **primary** fidelity
  metric (full-screen is the fallback). This RFC changes **none** of this — it only changes how
  the manifest is produced.
- **`scripts/analyze-ui-reference.py`** — an OpenCV heuristic that already auto-extracts
  component bounding boxes (`componentBoxes`) and a 12×12 mismatch grid from a reference image.
  Reused here as a pre-annotation source, not replaced.
- **`scripts/analyze-ui-with-ollama.py`** — a vision-LLM critic that already emits structured
  `regions` (each with `referenceRole`, `candidateIssue`, `approxBoundsPercent`). Its
  `referenceRole` vocabulary seeds the labeling-config role taxonomy.
- **RFC-0021 (Visual Fidelity Architecture)** already defines the closed-loop verifier that
  scores backend output against design references at screen/panel/cluster/motif granularity. It
  is silent on **where reference-region ground truth originates**. This RFC supplies that input;
  it is not a second fidelity engine.

**Why this is not a duplicate.** Label Studio is the *editor* for an artifact two existing
scripts already produce heuristically and one existing script already consumes. The only net-new
BoomHud-owned code is a JSON converter that targets the **existing** manifest format — not a new
schema, scorer, or masking surface.

## Motivation

The masking step is the weakest link in the fidelity loop:

- **Ground truth is hand-typed.** A region on a 1920×1080 screenshot is entered as literal
  `{ x: 18, y: 8, width: 1884, height: 126 }`. This is slow, error-prone, and does not scale to
  a growing corpus.
- **The auto-detectors have no correction UI.** `analyze-ui-reference.py` (CV) and the Ollama
  critic both emit boxes, but there is no human-in-the-loop surface to accept, nudge, or reject
  them. Their output is discarded rather than curated into ground truth.
- **The corpus is untyped and unversioned.** `uiMaskRects` are anonymous rectangles. There is no
  region role (panel / slot / header / hotbar), no labeler attribution, no review state, and no
  diff-friendly history beyond raw JSON.
- **It blocks raising the fidelity floor.** `FIDELITY-CORPUS-POLICY.md` wants to raise the gate
  from 80% → 85% → 90%+, but that requires a trustworthy, growable, *labeled* reference corpus.
  Hand-authoring is the bottleneck.

Label Studio is the canonical open-source tool for exactly this: draw/correct boxes (and
polygons/keypoints/segmentation if needed later) over images, with typed labels, pre-annotation
from model predictions, versioned tasks, and JSON/COCO/YOLO export.

## Goals

- Replace hand-authoring of `uiMaskRects` with a real annotation UI.
- Reuse the existing CV and vision-LLM analyzers as Label Studio pre-annotation backends so
  humans curate rather than draw from scratch.
- Add a typed **region role** to each annotation, reusing the `referenceRole` vocabulary.
- Produce output that is **byte-compatible** with `reference-ui-masks.json` so the existing
  PowerShell scorer and fidelity corpus consume it unchanged.
- Support a **round-trip**: import today's hand-authored masks into Label Studio as
  pre-annotations so no existing work is lost.
- Keep Label Studio entirely external — local/dev-time, never a build, feed, or runtime
  dependency.

## Non-Goals

- **No vendoring of the Label Studio platform** (Django, Postgres/SQLite, React) into
  `dotnet/`, the NuGet feed, or any shipped artifact.
- **No runtime dependency.** Label Studio never runs inside a BoomHud-generated app or the CLI's
  request path. This is build-time/dev-time tooling, consistent with "build-time over runtime."
- **No new fidelity scorer.** Scoring stays in `ImageSimilarityHandler` /
  `measure-pen-reference-similarity.ps1`. This RFC only feeds them better ground truth.
- **No change to the `.pen`/Figma → IR path.** Reference annotation is orthogonal to design-source
  ingestion.
- **No mandatory ML auto-labeling.** Pre-annotation is an accelerator; manual annotation and
  today's hand-authored manifests remain valid.

## Design

### Overview

```text
reference screenshot (.pen render or game UI capture)
   -> Label Studio task
        (pre-annotated by analyze-ui-reference.py [CV] and/or analyze-ui-with-ollama.py [LLM]
         via the Label Studio ML-backend SDK)
   -> human accept / nudge / reject / role-tag           (Label Studio UI)
   -> Label Studio export JSON
   -> converter  (LS JSON  <->  reference-ui-masks.json)  [new, BoomHud-owned]
   -> reference-ui-masks.json  (+ optional typed/labeled corpus sidecar)
   -> measure-pen-reference-similarity.ps1 / fidelity corpus   (unchanged)
   -> RFC-0021 closed-loop verifier
```

### Labeling Config

Label Studio interfaces are declarative XML. The first config is bbox-only:

```xml
<View>
  <Image name="ref" value="$image" zoom="true"/>
  <RectangleLabels name="region" toName="ref">
    <Label value="panel"          background="#1f77b4"/>
    <Label value="slot"           background="#ff7f0e"/>
    <Label value="header"         background="#2ca02c"/>
    <Label value="inventory-grid" background="#d62728"/>
    <Label value="hotbar"         background="#9467bd"/>
    <Label value="readout"        background="#8c564b"/>
    <Label value="other"          background="#7f7f7f"/>
  </RectangleLabels>
</View>
```

The label set is seeded from the `referenceRole` values already emitted by
`analyze-ui-with-ollama.py` and is the single source of truth for the role taxonomy. Polygons,
keypoints, and segmentation are deliberately deferred (see Open Questions).

### Coordinate Mapping

This is the one correctness-critical detail. Label Studio `RectangleLabels` results store
`x/y/width/height` as **percentages (0–100) of the original image dimensions**, with
`original_width`/`original_height` on each result. BoomHud `uiMaskRects` are **absolute
pixels**. The converter must therefore:

```text
px_x      = round(result.value.x      / 100 * result.original_width)
px_y      = round(result.value.y      / 100 * result.original_height)
px_width  = round(result.value.width  / 100 * result.original_width)
px_height = round(result.value.height / 100 * result.original_height)
fixtureSlug  <- task metadata (slug | rootId | key), not the image filename
```

The reverse (manifest → LS pre-annotation) divides by the image dimensions and emits a
`predictions` block so existing hand-authored masks open as editable suggestions.

### The Converter

Two directions, one component:

- **`LS export JSON -> reference-ui-masks.json`** (primary). Pure, deterministic JSON transform.
- **`reference-ui-masks.json -> LS predictions JSON`** (seed). Inverse transform for round-trip.

**Recommendation:** implement the converter as a BoomHud **CLI subcommand** under a new
`fidelity` command group (`fidelity import-labels` / `fidelity export-labels`), following the
extracted command+handler pattern (`Commands/Fidelity/*`, `Handlers/Fidelity/*`). This puts the
correctness-critical mapping inside the disciplined, analyzer-enforced, unit-tested C# codebase
rather than in an untested script. The **pre-annotation ML backend** must be Python (that is the
only Label Studio ML-backend SDK), so it lives under `scripts/` as a thin shim wrapping
`analyze-ui-reference.py`.

### Schema-First

`reference-ui-masks.json` currently has **no** JSON Schema, unlike the other inputs in
`schemas/json/`. This RFC adds `schemas/json/reference-ui-masks.schema.json` and validates both
converter directions against it — closing a schema-first gap regardless of the Label Studio
decision.

### CLI Integration

```powershell
# Seed Label Studio with existing hand-authored masks
dotnet run --project dotnet/src/BoomHud.Cli -- fidelity export-labels `
  --manifest fidelity/reference-ui-masks.json --images <dir> --out build/ls/predictions.json

# Import corrected annotations back into the manifest
dotnet run --project dotnet/src/BoomHud.Cli -- fidelity import-labels `
  --export build/ls/export.json --out fidelity/reference-ui-masks.json --schema-check
```

### Backward Compatibility

Fully additive. `reference-ui-masks.json`, the `*.mask.json` files, and
`measure-pen-reference-similarity.ps1` are untouched; converter output is byte-compatible with
the hand-authored format. Hand-authoring remains a valid path — Label Studio is an accelerator,
not a gate. Typed region roles, if added, live in an **optional sidecar** so the core
`uiMaskRects` array stays exactly as the scorer expects.

### Security / Performance Considerations

- Label Studio runs locally on developer/CI machines; annotation data and reference images stay
  local. No secrets, no inbound network surface in the BoomHud build.
- The converter is an offline, deterministic JSON transform — cheap and CI-safe.
- Reference-image licensing (e.g. `interfaceingame.com` captures used as references) is a
  pre-existing corpus concern, unchanged by this RFC, but worth tracking as the corpus grows.
- The Ollama pre-annotation backend reaches an external API and is therefore **opt-in** and never
  on the CI critical path; the CV backend is fully local.

## Alternatives Considered

### Keep hand-authoring masks (status quo)

Rejected. It is the current bottleneck, does not scale, produces untyped/unversioned rectangles,
and blocks raising the fidelity floor.

### Pure CV/LLM auto-detection with no human loop

Rejected as the *sole* source. `analyze-ui-reference.py` is a heuristic edge/saturation detector
and the LLM critic is approximate; both need human curation to become trustworthy ground truth.
They are valuable as pre-annotations, not as final labels.

### Build a bespoke annotation UI (in Godot/Avalonia via BoomHud itself)

Rejected. It reinvents a mature, Apache-2.0 tool and violates `audit-existing-surfaces`. Label
Studio already provides zoom, multi-label bbox/polygon/keypoint, pre-annotation, task
management, and standard export formats.

### Embed the Label Studio React frontend as an in-app review dashboard

Rejected. It drags a Django/React/DB stack toward the runtime and conflicts with "build-time over
runtime indirection." Out of scope here.

### Use COCO/YOLO as the interchange format instead of LS-native JSON

Considered. Label Studio can export COCO/YOLO, but BoomHud's scorer wants the existing
`uiMaskRects` manifest keyed by fixture slug, and LS-native JSON carries the per-task metadata
(fixture key, role, original dims) the converter needs. COCO/YOLO export remains available for
any future learned UI-region detector trained on the same corpus.

## Open Questions

1. **Converter home** — C# CLI subcommand (recommended, for tests/analyzers) vs a Python script
   alongside the existing `analyze-ui-*.py` (lower friction, same language as the ML backend).
2. **Region-role taxonomy ownership** — keep it in the labeling config, or promote it to a shared
   enum in `BoomHud.Abstractions` so generators and the critic agree on role names?
3. **Annotation geometry for v1** — bbox-only (matches today's `uiMaskRects`), or allow polygons
   for non-rectangular HUD regions now and down-project to bounding boxes for the scorer?
4. **Corpus storage** — commit reference images + Label Studio exports into the repo, or keep
   them in a separate data store and commit only the derived `reference-ui-masks.json`?
5. **Second ML backend** — wire `analyze-ui-with-ollama.py` as a second Label Studio prediction
   source, or keep the CV detector as the only pre-annotator for determinism?
6. **Deployment** — local `pip`/`poetry` vs Docker Compose for the Label Studio instance, and
   whether CI ever runs it (likely not; CI consumes the committed manifest).

## Recommended Implementation Order

1. Add `schemas/json/reference-ui-masks.schema.json` and validate the existing manifests
   (independently useful).
2. Implement the `fidelity import-labels` CLI subcommand (LS export → manifest) with unit tests
   pinning the percentage→pixel mapping on a known fixture.
3. Implement `fidelity export-labels` (manifest → LS predictions) for round-trip seeding.
4. Stand up Label Studio locally; load the labeling config; hand-label **one** existing fixture
   end-to-end and confirm the scorer produces the same UI-only score as the hand-authored mask.
5. Wrap `analyze-ui-reference.py` as a Label Studio ML pre-annotation backend.
6. Migrate the current `reference-ui-masks.json` corpus through the round-trip and grow it with
   typed roles.
7. (Optional) Add the Ollama critic as a second pre-annotation backend; feed roles into RFC-0021
   recursive scoring.

## Related RFCs

- [RFC-0015](./RFC-0015-snapshot-visual-regression.md) — Snapshot Visual Regression
- [RFC-0021](./RFC-0021-visual-fidelity-architecture.md) — Visual Fidelity Architecture
- [RFC-0023](./RFC-0023-hybrid-source-semantic-fidelity-pipeline.md) — Hybrid Source/Semantic Fidelity Pipeline
