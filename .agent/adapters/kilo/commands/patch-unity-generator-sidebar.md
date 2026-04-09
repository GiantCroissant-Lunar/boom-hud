---
description: Produce one focused UnityGenerator.cs patch for the BoomHud sidebar fidelity problem
agent: code
---

# Patch Unity Generator Sidebar

You are making one tightly scoped BoomHud Unity UI Toolkit fidelity attempt.

Use the `unity-component-fidelity` skill if it is available in this Kilo session.

Objective:

- Make one explicit pilot patch in the Unity generator for the sidebar fidelity problem.

Hard constraints:

- Edit only `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`.
- Do not edit generated files, scenes, screenshots, prompt files, scripts, docs, or tests.
- Do not hand-tune output under `samples/UnityFullPenCompare/Assets/Resources/BoomHudGenerated`.
- Do not open, attach, or inspect `.png`, `.jpg`, or other binary screenshot artifacts.
- Do not read `README.md`, `docs/`, planner files, generated outputs, or tests.
- Read only `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs`.
- Patch immediately after reading that file once.
- Do not run `bash`, `build`, `test`, or any validation commands.
- Stop after one focused patch.

Context:

- For this attempt, focus on one exact hot spot only: `MapJustification` and its use from `AppendLayoutStyles`.
- Working hypothesis: horizontal party rows in the sidebar are landing too compressed in Unity because `Justification.Center` preserves a centered cluster instead of distributing two fill or weighted child cards across the row.
- Prefer this exact change:
  - add a helper like `AllChildrenAreFillOrWeighted(ComponentNode source)` that returns true only when every direct child has `layout.Width` set to `Fill` or `Star` or a positive `layout.Weight`
  - in `AppendLayoutStyles`, when `layout.Justify` is present, detect the special case:
    `layout.Type == LayoutType.Horizontal && justify == Justification.Center && AllChildrenAreFillOrWeighted(source)`
  - emit `justify-content: space-between` for only that special case
  - otherwise keep `MapJustification(justify)` exactly as it is
  - do not make unrelated changes
- If the code already does this, make no patch and say so quickly.

Done condition:

- Patch quickly.
- Leave a brief final note describing exactly what changed in `UnityGenerator.cs` and why it should improve sidebar fidelity.