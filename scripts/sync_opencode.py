#!/usr/bin/env python3

import json
import subprocess
import sys
from pathlib import Path
from typing import Any

AGENT_OPENCODE_DIR = Path(".agent/adapters/opencode")
PERMISSIONS_FILE = AGENT_OPENCODE_DIR / "permissions.yaml"
OPENCODE_JSON = Path("opencode.json")


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


def run_script(path: str) -> None:
    subprocess.run([sys.executable, path], check=True)


def main() -> int:
    if not PERMISSIONS_FILE.exists():
        return 0

    perms = parse_simple_yaml(PERMISSIONS_FILE.read_text(encoding="utf-8").splitlines())

    config: dict[str, Any] = {}
    schema = perms.get("schema")
    if isinstance(schema, str) and schema:
        config["$schema"] = schema
    else:
        config["$schema"] = "https://opencode.ai/config.json"

    permission = perms.get("permission")
    if isinstance(permission, dict):
        config["permission"] = permission

    OPENCODE_JSON.write_text(json.dumps(config, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    run_script("scripts/sync_commands.py")
    run_script("scripts/sync_skills.py")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
