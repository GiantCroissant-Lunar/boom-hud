You are running a bounded Pen fidelity worker inside this repo.

Objective:
- Improve the mining inventory slot crop score in `{{PenPath}}`.

Context:
- Repo root: `{{RepoRoot}}`
- Pen file: `{{PenPath}}`
- Main screen node: `{{ScreenNodeId}}`
- Live target slot instance: `{{TargetNodeId}}`
- Reusable source slot component: `{{ComponentNodeId}}`
- Reference image: `{{ReferenceImage}}`
- Crop rectangle: `x={{CropX}} y={{CropY}} width={{CropWidth}} height={{CropHeight}}`
- Current baseline score to beat: `{{BaselineScore}}`
- Working output directory: `{{OutputDir}}`
- Backup file path: `{{BackupPenPath}}`

Rules:
- Edit only `{{PenPath}}`.
- Focus only on the mining slot silhouette and its internal layout.
- Do not touch top nav, right queue, center workspace, or bottom trays.
- Keep the slot shell and cost row stable unless they are directly part of the score loss.
- Prefer a single coherent silhouette change over many tiny nudges.
- Make at most one edit pass, then score it.
- Do not start a second redesign iteration inside the same run.
- If the first scored edit does not beat the baseline, restore from backup and stop.
- Use the `pencil` MCP server for inspection, editing, screenshots, and export.
- Use shell commands only for file copy and scoring.

Bounded loop:
1. Copy `{{PenPath}}` to `{{BackupPenPath}}` before making edits.
2. Inspect the current mining slot and the reference target.
3. Make one focused edit pass.
4. Export `{{ScreenNodeId}}` to `{{ExportedScreenPath}}` at scale 1.
5. Run this exact score command:
   ```pwsh
   {{ShellExecutable}} -ExecutionPolicy Bypass -File "{{MeasureScriptPath}}" `
     -ReferenceImage "{{ReferenceImage}}" `
     -CandidateImage "{{ExportedScreenPath}}" `
     -CropX {{CropX}} -CropY {{CropY}} `
     -CropWidth {{CropWidth}} -CropHeight {{CropHeight}} `
     -OutputDir "{{ScoreOutputDir}}"
   ```
6. Read `{{ScoreSummaryPath}}` and inspect `tolerancePixelIdentityPercent`.
7. Keep the change only if the score is strictly greater than `{{BaselineScore}}`.
8. If the score is not better, restore `{{PenPath}}` from `{{BackupPenPath}}`.
9. Stop after at most `{{AttemptCount}}` focused attempts.

Output requirements:
- End with a short final message only.
- Report:
  - whether you kept or discarded the edit
  - final mining slot score
  - what silhouette change you tried
  - whether the Pen file currently differs from the backup
