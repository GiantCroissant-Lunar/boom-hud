#!/usr/bin/env python3

import os
import re
import shutil
from pathlib import Path
from typing import Any

AGENT_DIR = Path(".agent")
ADAPTERS_DIR = AGENT_DIR / "adapters"
SKILLS_SOURCE = AGENT_DIR / "skills"


def parse_simple_yaml(lines: list[str]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    stack: list[tuple[int, dict[str, Any]]] = [(-1, result)]

    for raw in lines:
        if not raw.strip() or raw.strip().startswith("#"):
            continue

        indent = len(raw) - len(raw.lstrip())
        line = raw.strip()

        while stack and stack[-1][0] >= indent:
            stack.pop()

        current = stack[-1][1] if stack else result

        if ":" not in line:
            continue

        key, _, value = line.partition(":")
        key = key.strip()
        value = value.strip()

        if not value:
            current[key] = {}
            stack.append((indent, current[key]))
            continue

        if value.lower() == "true":
            current[key] = True
        elif value.lower() == "false":
            current[key] = False
        elif value.isdigit():
            current[key] = int(value)
        else:
            if (value.startswith('"') and value.endswith('"')) or (value.startswith("'") and value.endswith("'")):
                value = value[1:-1]
            current[key] = value

    return result


def load_adapter_configs() -> dict[str, dict[str, Any]]:
    configs: dict[str, dict[str, Any]] = {}
    if not ADAPTERS_DIR.exists():
        return configs

    for cfg in ADAPTERS_DIR.glob("*/config.yaml"):
        tool = cfg.parent.name
        content = cfg.read_text(encoding="utf-8")
        configs[tool] = parse_simple_yaml(content.splitlines())

    return configs


def extract_frontmatter(content: str) -> dict[str, str]:
    match = re.match(r"^---\r?\n(.+?)\r?\n---", content, re.DOTALL)
    if not match:
        return {}

    frontmatter: dict[str, str] = {}
    for line in match.group(1).split("\n"):
        if ":" not in line:
            continue
        k, v = line.split(":", 1)
        frontmatter[k.strip()] = v.strip()

    return frontmatter


def ensure_empty_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    for item in path.iterdir():
        if item.is_dir():
            shutil.rmtree(item)
        else:
            item.unlink()


def copy_tree_overwrite(src: Path, dst: Path) -> None:
    if dst.exists():
        shutil.rmtree(dst)
    shutil.copytree(src, dst)


def create_skill_stub(skill_name: str, name: str, description: str, source_skill_md: Path, target_skill_md: Path) -> str:
    title = " ".join(w.capitalize() for w in name.split("-"))
    rel = Path(os.path.relpath(source_skill_md, start=target_skill_md.parent))

    return "\n".join(
        [
            "---",
            f"name: {name}",
            f"description: {description}",
            "---",
            "",
            f"# {title}",
            "",
            "**This is a stub that references the shared skill definition.**",
            "",
            f"Read the full skill instructions from: [{rel.as_posix()}]({rel.as_posix()})",
            "",
            "## Instructions",
            "",
            f"When this skill is invoked, read and follow the complete instructions at:",
            f"`.agent/skills/{skill_name}/SKILL.md`",
            "",
        ]
    )


def main() -> int:
    if not SKILLS_SOURCE.exists():
        print(f"Nothing to sync: {SKILLS_SOURCE} does not exist")
        return 0

    configs = load_adapter_configs()

    targets: list[tuple[str, Path, str]] = []
    for tool, cfg in configs.items():
        skills_cfg = cfg.get("skills")
        if not isinstance(skills_cfg, dict):
            continue
        target = skills_cfg.get("target")
        if not isinstance(target, str) or not target:
            continue
        strategy = skills_cfg.get("strategy", "stub")
        if not isinstance(strategy, str) or not strategy:
            strategy = "stub"
        targets.append((tool, Path(target), strategy))

    for _, target_dir, _ in targets:
        ensure_empty_dir(target_dir)

    for skill_dir in sorted(SKILLS_SOURCE.iterdir()):
        if not skill_dir.is_dir():
            continue

        source_skill_md = skill_dir / "SKILL.md"
        if not source_skill_md.exists():
            continue

        content = source_skill_md.read_text(encoding="utf-8")
        fm = extract_frontmatter(content)
        name = fm.get("name", "")
        description = fm.get("description", "")

        if not name or name != skill_dir.name:
            continue

        for _, target_dir, strategy in targets:
            target_skill_dir = target_dir / skill_dir.name
            target_skill_dir.mkdir(parents=True, exist_ok=True)

            if strategy == "copy_full":
                copy_tree_overwrite(skill_dir, target_skill_dir)
                continue

            target_skill_md = target_skill_dir / "SKILL.md"
            stub = create_skill_stub(skill_dir.name, name, description, source_skill_md, target_skill_md)
            target_skill_md.write_text(stub, encoding="utf-8")

    print("Synced skills")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
