#!/usr/bin/env python3

import shutil
from pathlib import Path
from typing import Any

AGENT_DIR = Path(".agent")
ADAPTERS_DIR = AGENT_DIR / "adapters"
RULES_SOURCE = AGENT_DIR / "rules"


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


def ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def main() -> int:
    if not RULES_SOURCE.exists():
        print(f"Nothing to sync: {RULES_SOURCE} does not exist")
        return 0

    configs = load_adapter_configs()

    for tool, cfg in configs.items():
        rules_cfg = cfg.get("rules")
        if not isinstance(rules_cfg, dict):
            continue

        if rules_cfg.get("strategy") != "directory":
            continue

        target = rules_cfg.get("target")
        if not isinstance(target, str) or not target:
            continue

        ext = rules_cfg.get("extension", ".md")
        if not isinstance(ext, str) or not ext:
            ext = ".md"

        target_dir = Path(target)
        ensure_dir(target_dir)

        for existing in target_dir.glob(f"*{ext}"):
            if existing.is_file():
                existing.unlink()

        for src in RULES_SOURCE.glob("*.md"):
            dst = target_dir / (src.stem + ext)
            shutil.copy2(src, dst)

    print("Synced rules")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
