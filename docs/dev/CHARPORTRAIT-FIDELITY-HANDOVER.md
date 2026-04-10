# CharPortrait Fidelity Handover

This handover is for the Unity UI Toolkit `CharPortrait` fidelity loop in the BoomHud compare project.

## Start here

1. Read [CHARPORTRAIT-FIDELITY-ATTEMPTS.md](C:/lunar-horse/plate-projects/boom-hud/docs/dev/CHARPORTRAIT-FIDELITY-ATTEMPTS.md).
2. Treat `82.33%` as the current trusted `CharPortrait` static baseline for the isolated Component Lab surface.
3. Use the same Play-mode capture path for both baseline and candidate until edit-mode capture becomes reliable again.

## Trusted baseline

- Reference image: [j8BT0.png](C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/screenshots/j8BT0.png)
- Trusted baseline score: `82.33%`
- Baseline report: [charportrait-pen-vs-unity-static-generator-classicon-neg1.json](C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/reports/charportrait-pen-vs-unity-static-generator-classicon-neg1.json)
- Baseline diff: [charportrait-pen-vs-unity-static-generator-classicon-neg1.diff.png](C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/diffs/charportrait-pen-vs-unity-static-generator-classicon-neg1.diff.png)

## Important conclusions already established

1. The score is only trustworthy when baseline and candidate are measured through the same validated Play-mode capture path.
2. `ApplyIconLabelStyle` inset by itself is a true zero-delta change for `CharPortrait`.
3. Zeroing label margins and paddings in `ApplyTextLabelStyle` is actively harmful. It dropped the score from `57.36%` to `52.35%`.
4. The 2026-04-10 harness fix proved the recent “much worse than yesterday” state was not a UIToolkit layout collapse. The isolated Component Lab surface had been forced to `122px` width in presenter code, while the generated component contract is `130px`.
5. A generator-side `-1px` vertical nudge on `32px` icon labels is a real, measured improvement for the `CharPortrait` face icon.
6. The remaining visible misses look more like icon rendering treatment and small offset/alignment drift than a large structural collapse.

## Intentional code and script changes currently in workspace

These are real changes from this session and should not be treated as accidental noise.

### Component sample alignment

- File: [ComponentLabPresenter.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/ComponentLabPresenter.cs)
- Purpose: align the Component Lab sample values to the Pencil reference
- Current sample values:
  - `Name`
  - `ATK 10`
  - `DEF 8`
  - HP `90`
  - MP `60`
- The isolated `CharPortrait` preview now also uses the real component width (`130px`) instead of the old compare-harness override (`122px`).

### UI Toolkit host refresh fix

- File: [BoomHudUiToolkitHost.cs](C:/lunar-horse/plate-projects/boom-hud/unity-packages/com.boomhud.unity/Runtime/UIToolkit/BoomHudUiToolkitHost.cs)
- Purpose: avoid binding before `UIDocument.rootVisualElement` is ready after refresh/domain reload
- Why it matters: before this fix, the Component Lab could go black after Unity refresh because presenter binding ran too early

### CharPortrait preset in the Kilo harness

- File: [run-kilo-unity-autotune.ps1](C:/lunar-horse/plate-projects/boom-hud/scripts/run-kilo-unity-autotune.ps1)
- Purpose: add `charportrait` reference selection and crop ratios
- Important note: the script still uses direct window capture and does not yet own the full validated Play-mode baseline/candidate loop we used manually

### Durable review log

- File: [CHARPORTRAIT-FIDELITY-ATTEMPTS.md](C:/lunar-horse/plate-projects/boom-hud/docs/dev/CHARPORTRAIT-FIDELITY-ATTEMPTS.md)
- Purpose: curated history of valid and invalid attempts

## Current generator state

- File to edit for experiments: [UnityGenerator.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs)
- The last rejected `ApplyTextLabelStyle` patch has already been restored.
- There is still an existing workspace diff in `UnityGenerator.cs` around small `Press Start 2P` handling:
  - `line-height: 12px` for font sizes `<= 8px`
- Do not assume that diff came from the latest `CharPortrait` experiment. It predates the last valid attempt and was left intentionally in workspace.

## Latest valid attempts

### 2026-04-10: isolated preview width recovery

- Attempt type: compare-harness repair
- Hypothesis: the large drop from the earlier baseline was being caused by the preview surface compressing `CharPortrait`, not by a new UIToolkit layout failure.
- Patch: in [ComponentLabPresenter.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/BoomHudCompare/Scripts/ComponentLabPresenter.cs), restore the isolated and party-slot width to `130px`, matching the generated `CharPortraitView.uss`.
- Validation:
  - live Unity bounds for `ComponentCharPortrait`: `130 x 160`
  - Component Lab preview values now match the pen reference: `Name`, `shield`, `ATK 10`, `DEF 8`
  - baseline report: [charportrait-pen-vs-unity-static-gameview-2x.json](C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/reports/charportrait-pen-vs-unity-static-gameview-2x.json)
- Result: accepted
- Reason: this lifted the trusted isolated static score to `82.26%` and removed the obvious width compression from the Unity preview.

### 2026-04-10: large-icon vertical nudge

- Attempt type: generator hypothesis
- Hypothesis: the `32px` face icon glyph sits slightly low inside its box relative to the Pencil export.
- Patch: in [UnityGenerator.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs), apply `label.style.marginTop = -1f;` inside `ApplyIconLabelStyle(...)` only when the icon box is at least `32 x 32`.
- Validation:
  - generated `CharPortrait` runtime probe showed `ClassIcon` margin top of `-1`
  - candidate report: [charportrait-pen-vs-unity-static-generator-classicon-neg1.json](C:/lunar-horse/plate-projects/boom-hud/build/fidelity/current/reports/charportrait-pen-vs-unity-static-generator-classicon-neg1.json)
- Result: accepted
- Reason: this moved the trusted isolated static score from `82.26%` to `82.33%` on the same Play-mode capture path.

### Zero-delta icon inset

- Attempt folder: [20260409-075135](C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135)
- Hypothesis: icon glyphs were too large for their boxes
- Patch: inset `ApplyIconLabelStyle` font size by `4px`
- Result: baseline `57.36%`, candidate `57.36%`
- Conclusion: reject, no measurable gain

### Harmful text-label chrome removal

- Attempt folder: [20260409-081242](C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-081242)
- Hypothesis: default `Label` margins or paddings were shifting the `Name` and `ATK/DEF` rows
- Patch: add zero margins and zero paddings in `ApplyTextLabelStyle`
- Candidate report: [score-report-play-candidate.json](C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-081242/score-report-play-candidate.json)
- Result: baseline `57.36%`, candidate `52.35%`
- Conclusion: reject, default label chrome is not the hidden cause

## What to ignore

- Older `56.93%` `CharPortrait` runs under `build/_artifacts/latest/kilo-unity-autotune/20260409-*`
- Those were measured on stale pre-refresh captures before the validated Play-mode path was used
- Keep them as history, not as evidence

## Recommended next hypotheses

Run these one at a time, with the same baseline/candidate Play-mode loop:

1. Icon rendering strategy for Lucide labels inside fixed `32px` and `16px` boxes
2. Small vertical offset treatment for `16px` action-row icons after establishing a trustworthy per-frame capture for micro-adjustments
3. Lucide font asset or glyph-source verification before further generator-wide text changes
4. Tighten the editor capture path in [BoomHudFidelityCapture.cs](C:/lunar-horse/plate-projects/boom-hud/samples/UnityFullPenCompare/Assets/Editor/BoomHudFidelityCapture.cs) so its cropped output matches the trusted Play-mode manual scorer

Avoid retrying these without a new reason:

1. `ApplyIconLabelStyle` size inset only
2. Zeroing `ApplyTextLabelStyle` margins and paddings
3. Removing the small `Press Start 2P` line-height override by itself
4. Raw TTF fallback instead of the current `FontAsset` path for `lucide` or `Press Start 2P`

## Minimal replay loop

1. Back up [UnityGenerator.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs).
2. Create a one-hypothesis prompt in a new folder under `build/_artifacts/latest/manual-charportrait-kilo/<timestamp>/`.
3. Run `kilo run --dir C:\lunar-horse\plate-projects\boom-hud --auto --format json <prompt>`.
4. Run `dotnet test` for [BoomHud.Tests.Unit.csproj](C:/lunar-horse/plate-projects/boom-hud/dotnet/tests/BoomHud.Tests.Unit/BoomHud.Tests.Unit.csproj).
5. Regenerate Unity output with [BoomHud.Cli.csproj](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Cli/BoomHud.Cli.csproj).
6. Refresh Unity, enter Play mode, capture the live Unity window, crop the `CharPortrait` region, and score against [j8BT0.png](C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/screenshots/j8BT0.png).
7. If the patch loses or ties, restore the backup and regenerate.
8. Append the result to [CHARPORTRAIT-FIDELITY-ATTEMPTS.md](C:/lunar-horse/plate-projects/boom-hud/docs/dev/CHARPORTRAIT-FIDELITY-ATTEMPTS.md).

## Workspace caution

- The repo is already dirty in many unrelated places.
- Do not mass-revert workspace changes.
- Limit experiments to [UnityGenerator.cs](C:/lunar-horse/plate-projects/boom-hud/dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs) unless there is a specific reason to widen scope.
