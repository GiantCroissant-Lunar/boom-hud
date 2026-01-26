#!/usr/bin/env python3

import shutil
from pathlib import Path
from typing import Any

AGENT_DIR = Path(".agent")
ADAPTERS_DIR = AGENT_DIR / "adapters"
COMMANDS_SOURCE = AGENT_DIR / "commands"


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
    if not COMMANDS_SOURCE.exists():
        print(f"Nothing to sync: {COMMANDS_SOURCE} does not exist")
        return 0

    configs = load_adapter_configs()

    for tool, cfg in configs.items():
        cmd_cfg = cfg.get("commands")
        if not isinstance(cmd_cfg, dict):
            continue

        if not cmd_cfg.get("enabled", False):
            continue

        target = cmd_cfg.get("target")
        if not isinstance(target, str) or not target:
            continue

        target_dir = Path(target)
        ensure_dir(target_dir)

        fmt = cmd_cfg.get("format", "md")

        if fmt == "toml":
            for existing in target_dir.glob("*.toml"):
                if existing.is_file():
                    existing.unlink()
            for src in COMMANDS_SOURCE.glob("*.md"):
                dst = target_dir / (src.stem + ".toml")
                dst.write_text(
                    "\n".join(
                        [
                            f"# Auto-generated pointer to .agent/commands/{src.name}",
                            "",
                            "[command]",
                            f'name = "{src.stem}"',
                            f'description = ""',
                            "",
                            "[command.prompt]",
                            'text = """',
                            f"This command is defined in .agent/commands/{src.name}",
                            "Please read and execute the instructions from that file.",
                            '"""',
                            "",
                        ]
                    ),
                    encoding="utf-8",
                )
            continue

        for existing in target_dir.glob("*.md"):
            if existing.is_file():
                existing.unlink()

        for src in COMMANDS_SOURCE.glob("*.md"):
            dst = target_dir / src.name
            shutil.copy2(src, dst)

    print("Synced commands")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
