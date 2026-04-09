# CharPortrait Fidelity Handover

This handover is for the Unity UI Toolkit `CharPortrait` fidelity loop in the BoomHud compare project.

## Start here

1. Read [CHARPORTRAIT-FIDELITY-ATTEMPTS.md](C:/lunar-horse/plate-projects/boom-hud/docs/dev/CHARPORTRAIT-FIDELITY-ATTEMPTS.md).
2. Treat `57.36%` as the current trusted `CharPortrait` baseline.
3. Use the same Play-mode capture path for both baseline and candidate until edit-mode capture becomes reliable again.

## Trusted baseline

- Reference image: [j8BT0.png](C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/screenshots/j8BT0.png)
- Trusted baseline score: `57.36%`
- Baseline report: [score-report-play-baseline.json](C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135/score-report-play-baseline.json)
- Baseline diff: [score-diff-play-baseline.png](C:/lunar-horse/plate-projects/boom-hud/build/_artifacts/latest/manual-charportrait-kilo/20260409-075135/score-diff-play-baseline.png)

## Important conclusions already established

1. The score is only trustworthy when baseline and candidate are measured through the same validated Play-mode capture path.
2. `ApplyIconLabelStyle` inset by itself is a true zero-delta change for `CharPortrait`.
3. Zeroing label margins and paddings in `ApplyTextLabelStyle` is actively harmful. It dropped the score from `57.36%` to `52.35%`.
4. The remaining visible misses look more like text/icon rendering treatment and small offset/alignment drift than a large structural collapse.

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

1. Icon/text rendering strategy for tiny labels instead of label box chrome
2. Absolute placement or offset treatment for face/icon boxes inside `CharPortrait`
3. A compare-only placeholder strategy for tiny text or icons, but only if both sides of the comparison can be transformed consistently

Avoid retrying these without a new reason:

1. `ApplyIconLabelStyle` size inset only
2. Zeroing `ApplyTextLabelStyle` margins and paddings

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
