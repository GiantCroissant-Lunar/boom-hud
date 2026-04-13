# Fidelity Optimizer Handover

## Status

This handover captures the fidelity-optimizer work completed on `2026-04-13`.

The session moved the project from a strict single-score hill-climb to an experimental optimizer stack that can:

- rank candidates with a multi-objective score vector
- keep bounded-regression candidates in a frontier
- search shared text/icon metric profiles instead of only rule actions
- sample combinations with CEM on the experimental sweep path
- warm-start new CEM runs from a previous optimizer summary

The work also fixed the Unity sample host so `uGUI` generation can rely on installed TextMesh Pro essential resources without the editor popup.

## Core conclusion

The optimizer architecture is no longer the main blocker.

The remaining blocker is the quality and granularity of the shared parameter surface.

Today produced the first real guard-passing `TheAltersCrafting uGUI` gains, but those gains only became stable after the search space was narrowed to shared `pixel-text` buckets. Broad global actions and subtree actions were mostly noise for the current gap.

## Code areas changed

### Optimizer core and CLI

- `dotnet/src/BoomHud.Cli/Handlers/Rules/FidelityFrontierOptimizer.cs`
- `dotnet/src/BoomHud.Cli/Commands/Rules/RulesFrontierOptimizeCommand.cs`
- `dotnet/src/BoomHud.Cli/Commands/Rules/RulesSweepCommand.cs`
- `dotnet/src/BoomHud.Cli/Program.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Snapshots/FidelityFrontierOptimizerTests.cs`

What changed:

- added a backend-agnostic multi-objective frontier scorer
- added bounded Pareto pruning plus lexicographic ranking
- added guard-aware final candidate selection
- exposed experimental optimizer modes through the CLI
- added optimizer summary serialization

### Shared metric-profile surface

- `dotnet/src/BoomHud.Abstractions/Generation/GeneratorRulePlan.cs`
- `dotnet/src/BoomHud.Abstractions/Generation/GeneratorRuleSet.cs`
- `dotnet/src/BoomHud.Generators/GeneratorRuleExecutionCompiler.cs`
- `dotnet/src/BoomHud.Generators/GeneratorRulePlanner.cs`
- `dotnet/src/BoomHud.Generators/RuleResolver.cs`
- `dotnet/src/BoomHud.Gen.Unity/UnityBackendPlanner.cs`
- `dotnet/src/BoomHud.Gen.UGui/UGuiGenerator.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/GeneratorRulePlannerTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/GeneratorRuleSetTests.cs`
- `dotnet/tests/BoomHud.Tests.Unit/Generation/UGuiGeneratorTests.cs`

What changed:

- generator rule sets now support top-level shared `metricProfiles`
- metric profiles are compiled and preserved through planning
- backend planners and generators consume the shared profile surface
- the `uGUI` generator no longer double-applies metric profiles once Visual IR has already baked them

### Experimental sweep runner

- `scripts/run-fixture-rule-sweep.ps1`
- `scripts/run-kilo-unity-autotune.ps1`

What changed:

- added `strict`, `frontier`, and `cem` experimental optimizer modes
- added beam/depth/budget knobs for frontier search
- added CEM iteration/sample/elite/budget knobs
- added `optimizer-summary.json` output
- added primary-focused and isolated CEM lanes
- added warm-starting from prior optimizer summaries
- integrated subtree candidate scaffolding into the sweep path
- fixed harness bugs around missing summary paths, null phases, fixture source resolution, and leaderboard assumptions

### Unity sample host and TMP resources

- `samples/UnityFullPenCompare/Packages/manifest.json`
- `samples/UnityFullPenCompare/Packages/packages-lock.json`
- `samples/UnityFullPenCompare/Assets/TextMesh Pro/`

What changed:

- added the explicit TMP package dependency
- imported TMP essential resources into the sample project
- removed the recurring editor prompt for missing TMP resources

## Experimental results

## Phase 1: frontier path proved the architecture

The frontier optimizer completed on dense artifacts and produced valid summaries, but the earliest passes selected `baseline`.

Interpretation:

- the richer optimizer loop worked
- the initial action surface was too inert to move dense parity

This was the point where the bottleneck shifted from search algorithm to search-space quality.

## Phase 2: shared metric profiles made the search space real

Once metric profiles became first-class generator inputs, the optimizer started exercising a real shared parameter surface instead of pseudo-rules.

This still produced flat early scores, but it made later CEM passes meaningful.

## Phase 3: broad CEM was too coarse

Focused `uGUI` CEM on broad buckets found candidates that improved `TheAltersCrafting`, but they regressed unrelated dense fixtures and failed final guards.

The clearest example was:

- `TheAltersCrafting uGUI`: `85.56 -> 85.73`
- `QuestSidebar uGUI`: `86.24 -> 85.32`
- guard failure: `QuestSidebar uGUI` regressed by `0.92`

Interpretation:

- the optimizer could now find improving candidates
- broad shared actions were still too coarse

## Phase 4: isolated narrow buckets produced the first clean gain

The best completed run is:

- `build/fixture-rule-sweeps/cem-primary-isolated-warm-2026-04-13-r1/optimizer-summary.json`

Metrics:

- average dense all-backend: `88.1989 -> 88.2206`
- average dense `uGUI`: `87.3756 -> 87.4189`
- `TheAltersCrafting uGUI`: `85.56 -> 85.89`
- `QuestSidebar uGUI`: `86.24 -> 86.29`

Selected action family:

- `metric-ugui-pixel-small-nowrap-wrap-loose`
- `metric-ugui-pixel-xsmall-fixedwrap-font-plus-1`
- `metric-ugui-pixel-xsmall-fixedwrap-letter-loose`
- `metric-ugui-pixel-xsmall-nowrap-font-plus-1`
- `metric-ugui-pixel-xsmall-nowrap-letter-loose`

Why this matters:

- the gain passed guards
- the gain came from shared, non-hardcoded text buckets
- no subtree edits were required
- collateral damage to `QuestSidebar` disappeared

## Phase 5: the winning basin simplified further

A later warm-started run timed out at the shell level before writing a final optimizer summary, but the candidate `summary.json` files finished and were inspected manually.

Useful path:

- `build/fixture-rule-sweeps/cem-primary-isolated-warm-2026-04-13-r2/`

Important finding:

candidate `cem-i2-s2-1347090163` reached the same top metrics as the best completed run using only four actions:

- `metric-ugui-pixel-small-nowrap-wrap-loose`
- `metric-ugui-pixel-xsmall-fixedwrap-font-plus-1`
- `metric-ugui-pixel-xsmall-nowrap-font-plus-1`
- `metric-ugui-pixel-xsmall-nowrap-letter-loose`

Interpretation:

- the best current score does not require a wide action set
- the productive region is a tight local basin around `small-nowrap` and `xsmall` text policies
- the next best search step is a local neighborhood/ablation pass around this four-action core

## What did not help enough

The following ideas were useful for diagnosis but are not the best next push for score:

- broad global `heading-*` and `stacked-line-*` actions
- subtree actions at the current coarse catalog depth
- wider undirected CEM over mixed metric and subtree groups

They either stayed flat or improved `TheAltersCrafting` at the cost of guard failures elsewhere.

## Current working theory

The remaining `uGUI` gap in the dense lane is now mostly:

- narrow text metric policy
- wrap policy on small and extra-small pixel text
- letter spacing and font-size interactions in compact readouts

It is not primarily:

- shell construction
- coarse layout
- root-level subtree selection
- TMP installation or batch-capture stability

## Verification completed

Verified earlier in the session:

- `dotnet test C:\lunar-horse\plate-projects\boom-hud\dotnet\tests\BoomHud.Tests.Unit\BoomHud.Tests.Unit.csproj -p:UseSharedCompilation=false`
  - result: `428` passed, `0` failed

Verified during sweep-runner changes:

- PowerShell parse checks for `scripts/run-fixture-rule-sweep.ps1`
- multiple real dense experimental runs under `build/fixture-rule-sweeps/`

Important note:

- `build/` artifacts remain local-only and should not be committed

## Recommended next session

Priority order:

1. Keep using `CemFocus primary-isolated`.
2. Start from the four-action core found in `cem-primary-isolated-warm-2026-04-13-r2`.
3. Run a local neighborhood search or ablation pass:
   - remove one action at a time
   - add one nearby action at a time
   - keep subtree actions disabled
4. Split the winning family one level further, still generally:
   - separate `small nowrap` compact labels from `small nowrap` title/tab text
   - keep the buckets semantic and shared, not fixture-specific
5. Only revisit subtree search after the narrow text basin plateaus.

## Suggested restart checklist

- read this handover first
- inspect:
  - `docs/dev/DENSE-FIXTURE-PARITY-HANDOVER-2026-04-13.md`
  - `build/fixture-rule-sweeps/cem-primary-isolated-warm-2026-04-13-r1/optimizer-summary.json`
  - `build/fixture-rule-sweeps/cem-primary-isolated-warm-2026-04-13-r2/`
- treat the four-action simplified winner as the current baseline for further search
- avoid spending time on broad global text actions unless the narrow-bucket lane stalls
