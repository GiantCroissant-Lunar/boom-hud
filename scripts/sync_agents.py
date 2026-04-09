#!/usr/bin/env python3

import shutil
from pathlib import Path
from typing import Any

AGENT_DIR = Path(".agent")
ADAPTERS_DIR = AGENT_DIR / "adapters"


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


def ensure_empty_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    for item in path.iterdir():
        if item.is_dir():
            shutil.rmtree(item)
        else:
            item.unlink()


def copy_contents(src: Path, dst: Path) -> None:
    for item in src.iterdir():
        target = dst / item.name
        if item.is_dir():
            shutil.copytree(item, target)
        else:
            shutil.copy2(item, target)


def main() -> int:
    configs = load_adapter_configs()

    for tool, cfg in configs.items():
        agents_cfg = cfg.get("agents")
        if not isinstance(agents_cfg, dict):
            continue

        if not agents_cfg.get("enabled", False):
            continue

        source = agents_cfg.get("source")
        target = agents_cfg.get("target")
        if not isinstance(source, str) or not source:
            continue
        if not isinstance(target, str) or not target:
            continue

        source_dir = Path(source)
        if not source_dir.exists():
            continue

        target_dir = Path(target)
        ensure_empty_dir(target_dir)
        copy_contents(source_dir, target_dir)

    print("Synced agents")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())