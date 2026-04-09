# CharPortrait Fidelity Attempts

This log tracks the component-first Unity UI Toolkit fidelity loop for `CharPortrait` against the Pencil component export at [build/_artifacts/latest/screenshots/j8BT0.png](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/screenshots/j8BT0.png).

It exists because the raw attempt folders under `build/_artifacts/latest/` are useful for reproduction but poor for later review. This file is the curated history: what was tried, whether the measurement was trustworthy, and what we learned.

## Current trusted baseline

- Date: 2026-04-09
- Reference: [j8BT0.png](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/screenshots/j8BT0.png)
- Unity capture: Play-mode live window capture cropped to the `CharPortrait` region
- Overall similarity: `57.36%`
- Report: [score-report-play-baseline.json](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135/score-report-play-baseline.json)
- Diff: [score-diff-play-baseline.png](/C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135/score-diff-play-baseline.png)

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
4. The remaining visible misses are still dominated by text/icon rendering treatment and small alignment offsets, not large structural layout collapse.
5. Future attempts should continue as one-hypothesis patches and append their outcome here.
