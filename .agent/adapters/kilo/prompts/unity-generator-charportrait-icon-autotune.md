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
- The score is based only on the `CharPortrait` component from the Component Lab.
- The presenter sample data already matches the Pencil component reference, so focus on generator rendering behavior.
- For this attempt, focus on one exact hot spot only: `ApplyIconLabelStyle` inside the generated controller helper text in `UnityGenerator.cs`.
- Working hypothesis: the current icon glyphs are visually too large for their boxes in `CharPortrait`, especially the face badge and action buttons, because `iconSize` uses the full minimum box dimension with no inset.
- Prefer this exact change:
  - in `ApplyIconLabelStyle`, keep the existing label width/height assignments
  - change the `iconSize` calculation from the full min dimension to a slightly inset size, using `Mathf.Max(1f, Mathf.Min(boxWidth, boxHeight) - 4f)`
  - do not change any other behavior in that helper
  - do not make unrelated changes
- If the code already does this, make no patch and say so quickly.

Useful files:
- `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`

Local validation you may run:
- none required inside this step; the outer harness will validate

Done condition:
- Patch quickly.
- Leave a brief final note describing exactly what changed in `UnityGenerator.cs` and why it should improve `CharPortrait` fidelity.