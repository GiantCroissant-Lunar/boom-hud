# Unity Autotune Loop

## Scope

- Default writable target for Unity fidelity autotune is `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs` unless the user explicitly widens scope.
- The compare sample lives under `samples/UnityFullPenCompare`.

## Inner-loop constraints

- In an autoresearch-style iteration, make one concrete patch and stop.
- Do not edit screenshots, prompt files, generated assets, scenes, or tests during the inner patch step.
- Do not inspect `.png`, `.jpg`, or other image binaries unless the user explicitly asks for visual analysis.

## Evaluation contract

- Treat the existing PowerShell harness and BoomHud scorer as authoritative for acceptance.
- If the task is already being run under the harness, do not spend time on extra build, test, or capture commands inside the patching step.
- Optimize for measurable score improvement without regressing test pass status or capture validity.