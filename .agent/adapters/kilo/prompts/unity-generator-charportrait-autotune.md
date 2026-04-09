You are making one tightly scoped BoomHud Unity UI Toolkit fidelity attempt.

Use the `unity-component-fidelity` skill from the project skills if it is available in this Kilo session.
Use the `unity-generator-patcher` subagent for the actual code edit if it is available in this Kilo session.

Objective:
- Improve the cropped `CharPortrait` compare score above the current baseline of {{BASELINE_SCORE}}%.
- Make one explicit pilot patch in the Unity generator instead of exploring broadly.

Hard constraints:
- Edit only `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`.
- Do not edit generated files, scenes, screenshots, prompt files, scripts, docs, or tests.
- Do not hand-tune output under `samples/UnityFullPenCompare/Assets/Resources/BoomHudGenerated`.
- Do not open, attach, or inspect `.png`, `.jpg`, or other binary screenshot artifacts.
- Do not read `README.md`, `docs/`, planner files, generated outputs, or tests.
- Read only `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`.
- Patch immediately after reading that file once.
- Do not run `bash`, `build`, `test`, or any validation commands. The outer harness will do that.
- Stop after one focused patch.

Context:
- The score is now based only on the `CharPortrait` component from the Component Lab, not the full HUD.
- The presenter sample data already matches the Pencil component reference, so focus on generator layout behavior.
- For this attempt, focus on one exact hot spot only: `AppendFillDimensionStyles` and its use from `AppendDimensionStyles`.
- Working hypothesis: the `CharPortrait` action row uses fill/weighted children in a horizontal container, but emitting only `flex-grow` leaves UI Toolkit sizing biased by intrinsic icon content instead of distributing the cells like the Pencil component.
- Prefer this exact change:
  - in `AppendFillDimensionStyles`, when `propertyName == "width"` and `parentLayoutType == LayoutType.Horizontal`, keep the existing `flex-grow` behavior and also emit `flex-basis: 0px` and `min-width: 0px`
  - when `propertyName == "height"` and the parent layout is vertical/stack/grid/dock, keep the existing `flex-grow` behavior and also emit `flex-basis: 0px` and `min-height: 0px`
  - do not change any other branches in that method
  - do not make unrelated changes
- If the code already does this, make no patch and say so quickly.

Useful files:
- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`

Local validation you may run:
- none required inside this step; the outer harness will validate

Done condition:
- Patch quickly.
- Leave a brief final note describing exactly what changed in `UnityGenerator.cs` and why it should improve `CharPortrait` fidelity.