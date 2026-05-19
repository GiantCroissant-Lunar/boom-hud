# BoomHud Project Skills Index

This directory contains project-specific Agent Skills (https://agentskills.io) for the BoomHud project. Workspace-shared skills (autoloop, build, commit, etc.) are NOT mirrored here — they live at `<lunar-horse>/.agent/skills/<NN-category>/<name>/` and are deployed into per-provider directories (`.cline/skills/`, `.cursor/skills/`, etc.) via adapter configs at `.agent/adapters/<provider>/config.yaml`. See `<lunar-horse>/.agent/skills/INDEX.md` for the workspace-level index.

Numbered categories create an implicit dependency hierarchy — lower numbers are more foundational and consumed by higher-numbered layers. Categories mirror the workspace convention.

## 03-presentation — UI, Input & Visual Scripting

| Skill | Description |
|---|---|
| `ui-ux-pro-max` | UI/UX design intelligence (styles, colors, typography, UX guidelines). |

## 04-tooling — MCP, Packages & Reference

| Skill | Description |
|---|---|
| `unity-component-fidelity` | Component-first pen-to-Unity UI Toolkit fidelity loop for BoomHud, using the Unity component lab before full-screen layout comparison. |

## Conventions

- **Cross-references** use `@skill-name` notation.
- **Layout**: `<category>/<skill>/SKILL.md` mirroring workspace layout.
- **Adopting a workspace skill in this project** — list it in the relevant adapter's `skills.workspace` array; the sync deploys it without it appearing under `.agent/skills/`.
- **Lifting a project skill up** — copy from `.agent/skills/<NN-cat>/<name>/` into `<lunar-horse>/.agent/skills/<NN-cat>/<name>/`, generalize project-specific paths, then remove the project copy and reference via `skills.workspace`.
