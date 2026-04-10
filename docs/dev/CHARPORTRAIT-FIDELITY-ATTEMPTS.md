# CharPortrait Fidelity Attempts

This log tracks the component-first Unity UI Toolkit fidelity loop for `CharPortrait` against the Pencil component export at [build/_artifacts/latest/screenshots/j8BT0.png](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/screenshots/j8BT0.png).

It exists because the raw attempt folders under `build/_artifacts/latest/` are useful for reproduction but poor for later review. This file is the curated history: what was tried, whether the measurement was trustworthy, and what we learned.

## Current trusted baseline

- Date: 2026-04-10
- Reference: [j8BT0.png](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/pen/static/j8BT0.png)
- Unity capture: Play-mode game-view capture at 2x, cropped to the isolated `ComponentCharPortrait` region in `ComponentLab`
- Overall similarity: `82.33%`
- Report: [charportrait-pen-vs-unity-static-generator-classicon-neg1.json](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/reports/charportrait-pen-vs-unity-static-generator-classicon-neg1.json)
- Diff: [charportrait-pen-vs-unity-static-generator-classicon-neg1.diff.png](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/diffs/charportrait-pen-vs-unity-static-generator-classicon-neg1.diff.png)

## Preconditions that changed the results

1. `ComponentLabPresenter.cs` was aligned to the Pencil reference sample values:
   - `Name`
   - `ATK 10`
   - `DEF 8`
   - HP `90`
   - MP `60`
2. `BoomHudUiToolkitHost.cs` was fixed so `Rebind()` no longer binds before `UIDocument.rootVisualElement` is ready after refresh/domain reload.
3. Edit-mode Game-view capture is still less trustworthy than Play-mode capture. Validated comparisons for `CharPortrait` should currently prefer Play mode.

## Valid attempts

### 2026-04-10: Component Lab width recovery

- Attempt type: compare-harness repair
- Summary: the isolated `CharPortrait` preview in `ComponentLabPresenter.cs` was still forcing a `122px` width, while the generated `CharPortrait` component is `130px` wide in both the pen source and generated USS.
- Patch:
  - align the isolated preview sample values to the pen reference
  - restore `PartyMemberWidth` to `130f`
  - stop overriding the generated root into a narrower layout
- Outcome: accepted
- Evidence:
  - live Unity bounds probe for `ComponentCharPortrait`: `130 x 160`
  - [component-lab-char-portrait-gameview-2x-cropped.png](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/unity/static/component-lab-char-portrait-gameview-2x-cropped.png)
  - [charportrait-pen-vs-unity-static-gameview-2x.json](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/reports/charportrait-pen-vs-unity-static-gameview-2x.json)
- Notes: this is the new trusted starting point. The remaining diff is concentrated in icon rendering and exact edge placement, not broad layout collapse.

### 2026-04-10: small-text line-height removal

- Attempt type: generator hypothesis
- Summary: tested removing the `line-height: 12px` override for `Press Start 2P` labels at `<= 8px`.
- Outcome: rejected
- Evidence:
  - [charportrait-pen-vs-unity-static-lineheight-test.json](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/reports/charportrait-pen-vs-unity-static-lineheight-test.json)
- Reason: true zero-delta on the trusted capture path. The component stayed at the same `82.26%` score.

### 2026-04-10: raw TTF font swap

- Attempt type: runtime rendering experiment
- Summary: temporarily swapped `CharPortrait` labels from the bundled `FontAsset` path to the raw `lucide.ttf` and `PressStart2P-Regular.ttf` fonts at runtime.
- Outcome: rejected
- Evidence:
  - [charportrait-pen-vs-unity-static-ttf-test.json](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/reports/charportrait-pen-vs-unity-static-ttf-test.json)
- Reason: score dropped to `81.88%`, so the current SDF font path is better than the raw TTF fallback for this component.

### 2026-04-10: large face-icon vertical nudge

- Attempt type: generator hypothesis
- Summary: tested a `-1px` vertical nudge only for large icon labels (`32px` boxes) in `ApplyIconLabelStyle`.
- Patch: in [UnityGenerator.cs](/C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs), add `label.style.marginTop = -1f;` when `boxWidth >= 32f && boxHeight >= 32f`.
- Outcome: accepted
- Evidence:
  - [component-lab-char-portrait-generator-classicon-neg1-2x-cropped.png](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/unity/static/component-lab-char-portrait-generator-classicon-neg1-2x-cropped.png)
  - [charportrait-pen-vs-unity-static-generator-classicon-neg1.json](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/reports/charportrait-pen-vs-unity-static-generator-classicon-neg1.json)
- Notes: this is a small but real gain on the trusted play-mode path, moving the component from `82.26%` to `82.33%`.

### 2026-04-10: deterministic tweak-capture hook

- Attempt type: tooling improvement
- Summary: added tweak fields and a public entry point to [BoomHudFidelityCapture.cs](/C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Editor/BoomHudFidelityCapture.cs) so capture manifests can apply icon-margin experiments without manual runtime pokes.
- Outcome: partial
- Evidence:
  - manifests under [build/fidelity/icon-loop/manifests](/C:/lunar-horse/plate-projects/boom-hud/build/fidelity/icon-loop/manifests)
- Notes: the tweak hook works, but the current edit-mode crop path still returns the full Game View instead of an element-aligned crop for `CharPortrait`, so it is not yet a replacement for the trusted play-mode scorer.

### 2026-04-09: host-fix baseline recovery

- Attempt type: pipeline repair
- Summary: fixed `BoomHudUiToolkitHost.cs` delayed rebind timing so the Component Lab survived refresh again.
- Outcome: restored a valid component-lab capture path and produced a trustworthy edit-mode score snapshot at `57.36%`.
- Evidence:
  - [componentlab-window-after-host-fix.png](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/screenshots/componentlab-window-after-host-fix.png)
  - [componentlab-charportrait-after-host-fix.png](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/screenshots/componentlab-charportrait-after-host-fix.png)
  - [charportrait-after-host-fix-report.json](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/screenshots/charportrait-after-host-fix-report.json)
- Notes: this fixed a real Unity lifecycle failure, but later runs showed edit-mode capture can still regress after some refresh sequences.

### 2026-04-09 07:51: icon inset hypothesis

- Attempt dir: [manual-charportrait-kilo/20260409-075135](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135)
- Prompt: [prompt.md](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135/prompt.md)
- Kilo output: [kilo-output.txt](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135/kilo-output.txt)
- Hypothesis: icon glyphs were too large for their boxes because `ApplyIconLabelStyle` used the full minimum box dimension.
- Patch: change `iconSize` to `Mathf.Max(1f, Mathf.Min(boxWidth, boxHeight) - 4f)`.
- Validation:
  - `142/142` unit tests passed
  - candidate report: [score-report-play-candidate.json](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135/score-report-play-candidate.json)
  - baseline report after restore: [score-report-play-baseline.json](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135/score-report-play-baseline.json)
- Result: rejected
- Reason: baseline `57.36%`, candidate `57.36%`, true zero-delta on the same Play-mode capture path.

### 2026-04-09 08:12: text-label chrome hypothesis

- Attempt dir: [manual-charportrait-kilo/20260409-081242](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-081242)
- Prompt: [prompt.md](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-081242/prompt.md)
- Kilo output: [kilo-output.txt](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-081242/kilo-output.txt)
- Hypothesis: `ApplyTextLabelStyle` still left default UI Toolkit label padding or margin that was shifting the `Name` and `ATK/DEF` rows away from the Pencil reference.
- Patch: add explicit zero margins and zero paddings on all four sides in `ApplyTextLabelStyle`.
- Validation:
  - `142/142` unit tests passed
  - candidate report: [score-report-play-candidate.json](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-081242/score-report-play-candidate.json)
  - baseline report: [score-report-play-baseline.json](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-081242/score-report-play-baseline.json)
- Result: rejected
- Reason: baseline `57.36%`, candidate `52.35%`. The patch made the component materially worse, so default label chrome was not the hidden cause of the remaining drift.

## Invalid or stale attempts

These runs are still worth remembering, but they should not be used as evidence for or against a generator change.

### 2026-04-09 06:50: initial component-only baseline

- Attempt dir: [kilo-unity-autotune/20260409-065004](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/kilo-unity-autotune/20260409-065004)
- Reported score: `56.93%`
- Status: superseded
- Why it is not the current baseline: it predates the host fix and later Play-mode validation.

### 2026-04-09 06:55: fill-axis sizing hypothesis

- Attempt dir: [kilo-unity-autotune/20260409-065528](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/kilo-unity-autotune/20260409-065528)
- Status: invalid
- Why: scored on a stale pre-refresh image path after regeneration, so the zero-delta result is not trustworthy.

### 2026-04-09 06:58: small-text line-height hypothesis

- Attempt dir: [kilo-unity-autotune/20260409-065821](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/kilo-unity-autotune/20260409-065821)
- Status: invalid
- Why: same stale pre-refresh capture problem as the fill-axis attempt.

### 2026-04-09 07:00: early icon inset hypothesis

- Attempt dir: [kilo-unity-autotune/20260409-070021](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/kilo-unity-autotune/20260409-070021)
- Status: invalid
- Why: same stale pre-refresh capture problem as the earlier `CharPortrait` runs.

## Working conclusions

1. The experiment loop is now trustworthy only when baseline and candidate are measured through the same validated Play-mode capture path.
2. `ApplyIconLabelStyle` size inset by itself is not enough to move the `CharPortrait` score.
3. Removing text label padding and margins in `ApplyTextLabelStyle` makes the component worse, so the remaining text drift is not explained by default label chrome alone.
4. The old `57.36%` baseline should no longer be treated as the current component state. After fixing the compare harness and landing the large-icon nudge, the trusted isolated static baseline is `82.33%`.
5. The remaining visible misses are dominated by icon rendering treatment and small alignment offsets, not large structural layout collapse.
6. The new `BoomHudFidelityCapture` tweak hook is useful for structured experiments, but its crop path still needs repair before it can replace the manual play-mode scorer.
7. Future attempts should continue as one-hypothesis patches and append their outcome here.
