You are making one tightly scoped BoomHud Unity UI Toolkit fidelity attempt.

Use the `unity-component-fidelity` skill from the project skills if it is available in this Kilo session.
Use the `unity-generator-patcher` subagent for the actual code edit if it is available in this Kilo session.

Objective:
- Improve the cropped compare score above the current baseline of {{BASELINE_SCORE}}%.
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
- The reference and candidate screenshots already exist, but you must rely on the numeric baseline and repo text context instead of reading image files.
- The compare scene still has a black main viewport, so avoid optimizing editor chrome, logs, or unrelated surfaces.
- For this attempt, focus on one exact hot spot only: `MapJustification` and its use from `AppendLayoutStyles`.
- Working hypothesis: horizontal party rows in the sidebar are landing too compressed in Unity because `Justification.Center` preserves a centered cluster instead of distributing two fill/weighted child cards across the row.
- Prefer this exact change:
  - add a helper like `AllChildrenAreFillOrWeighted(ComponentNode source)` that returns true only when every direct child has `layout.Width` set to `Fill`/`Star` or a positive `layout.Weight`
  - in `AppendLayoutStyles`, when `layout.Justify` is present, detect the special case:
    `layout.Type == LayoutType.Horizontal && justify == Justification.Center && AllChildrenAreFillOrWeighted(source)`
  - emit `justify-content: space-between` for only that special case
  - otherwise keep `MapJustification(justify)` exactly as it is
  - do not make unrelated changes
- If the code already does this, make no patch and say so quickly.

Useful files:
- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`

Local validation you may run:
- none required inside this step; the outer harness will validate

Done condition:
- Patch quickly.
- Leave a brief final note describing exactly what changed in `UnityGenerator.cs` and why it should improve fidelity.