# BoomHud Kilo Rules

## Base context

- Treat [AGENTS.md](/C:/lunar-horse/plate-projects/boom-hud/AGENTS.md) as the primary project instruction file.
- For BoomHud pen-to-Unity work, prefer the `unity-component-fidelity` skill when it clearly applies.

## Source of truth

- Treat generator code as the source of truth for Unity output.
- Do not hand-edit generated files under `samples/UnityFullPenCompare/Assets/Resources/BoomHudGenerated/`.
- Prefer fixing `dotnet/src/BoomHud.Gen.Unity/UnityGenerator.cs` or other non-generated sources over patching generated UXML, USS, or `.gen.cs` files.

## Working style

- Preserve existing user changes in the worktree.
- Keep exploration proportional to the task. For narrow fidelity work, do not read broad docs or unrelated code without a concrete reason.
- Prefer one explicit hypothesis and one focused patch over broad speculative edits.
