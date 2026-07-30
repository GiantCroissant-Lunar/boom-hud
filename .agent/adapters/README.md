# Agent adapters

Adapter configuration is grouped by responsibility:

- `targets/` describes generated agent-resource surfaces. Each target owns its
  own deployment: `agents` writes `.agents/skills/` and `AGENTS.md`, and
  `claude`, `codex`, `gemini`, `kilocode`, `kiro`, and `opencode` each write
  their own native skill (and where applicable rule) directories.
- `runners/` records CLI invocation and context metadata for tools that consume
  an existing target without requiring their own generated resource surface.

Run `python tools/sync-agent-resources.py` to synchronize every adapter. Pass a
leaf name such as `--provider agents` or a qualified path such as
`--provider targets/agents` to synchronize one adapter.
