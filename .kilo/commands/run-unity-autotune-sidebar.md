---
description: Run the BoomHud sidebar autotune harness and summarize the latest result
agent: code
---

# Run Unity Autotune Sidebar

Follow these steps:

1. Run the existing harness from the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-kilo-unity-autotune.ps1 -RegionPreset sidebar -KiloTimeoutSeconds 120
```

2. Find the newest attempt directory under `build/_artifacts/latest/kilo-unity-autotune/`.

3. Read its `summary.json`.

4. Summarize:

- baseline score
- candidate score, if present
- whether the patch was accepted, rejected, or restored
- any `KiloWarning` or `Failure`
- one concise next-hypothesis suggestion if the run did not improve the score
