# Schemas Layout

The `schemas/` folder is organized by artifact type:

- `json/`: BoomHud JSON schemas used by the CLI, tests, DTO generation, and documentation examples
- `yaml/`: upstream YAML sources used to derive JSON schemas, such as the Figma OpenAPI input
- `scripts/`: helper scripts that convert or maintain schema assets

Current notable files:

- `json/boom-hud.schema.json`: core BoomHud DSL schema
- `json/compose.schema.json`: compose manifest schema
- `json/pencil.schema.json`: Pencil `.pen` schema
- `json/motion.schema.json`: portable motion contract schema
- `json/states.schema.json`: snapshot states schema
- `json/tokens.schema.json`: token registry schema
- `yaml/figma-openapi.yaml`: upstream Figma OpenAPI source
- `scripts/convert_openapi.py`: helper for converting OpenAPI YAML to JSON Schema

Published `$id` URLs remain under `https://boom-hud.dev/schemas/...` for contract stability even though the repo files now live under subfolders.
