#!/usr/bin/env python3
"""Use Ollama Cloud vision models to produce a structured UI mismatch analysis."""

from __future__ import annotations

import argparse
import base64
import json
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path

from PIL import Image


DEFAULT_SCHEMA = {
    "type": "object",
    "properties": {
        "summary": {"type": "string"},
        "overallAssessment": {
            "type": "object",
            "properties": {
                "layoutSimilarityPercent": {"type": "number"},
                "structureSimilarityPercent": {"type": "number"},
                "majorFailureReason": {"type": "string"},
            },
            "required": [
                "layoutSimilarityPercent",
                "structureSimilarityPercent",
                "majorFailureReason",
            ],
            "additionalProperties": False,
        },
        "regions": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "name": {"type": "string"},
                    "priority": {"type": "integer"},
                    "referenceRole": {"type": "string"},
                    "candidateIssue": {"type": "string"},
                    "expectedChange": {"type": "string"},
                    "approxBoundsPercent": {
                        "type": "object",
                        "properties": {
                            "left": {"type": "number"},
                            "top": {"type": "number"},
                            "right": {"type": "number"},
                            "bottom": {"type": "number"},
                        },
                        "required": ["left", "top", "right", "bottom"],
                        "additionalProperties": False,
                    },
                },
                "required": [
                    "name",
                    "priority",
                    "referenceRole",
                    "candidateIssue",
                    "expectedChange",
                    "approxBoundsPercent",
                ],
                "additionalProperties": False,
            },
        },
        "actions": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "priority": {"type": "integer"},
                    "target": {"type": "string"},
                    "editType": {"type": "string"},
                    "instruction": {"type": "string"},
                    "confidence": {"type": "number"},
                },
                "required": ["priority", "target", "editType", "instruction", "confidence"],
                "additionalProperties": False,
            },
        },
    },
    "required": ["summary", "overallAssessment", "regions", "actions"],
    "additionalProperties": False,
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", required=True)
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--model", default="gemma4:31b")
    parser.add_argument("--api-url", default="https://ollama.com/api/chat")
    parser.add_argument("--cv-analysis")
    parser.add_argument("--timeout-seconds", type=int, default=180)
    parser.add_argument("--use-schema", action="store_true")
    return parser.parse_args()


def encode_image(path: Path) -> str:
    return base64.b64encode(path.read_bytes()).decode("ascii")


def get_image_size(path: Path) -> tuple[int, int]:
    with Image.open(path) as image:
        return image.size


def load_cv_summary(path: Path) -> str:
    if not path.exists():
        return ""
    data = json.loads(path.read_text(encoding="utf-8"))
    top_cells = data.get("grid", {}).get("topMismatchCells", [])[:8]
    top_boxes = data.get("componentBoxes", [])[:8]
    summary = {
        "uiCoveragePercent": data.get("uiCoveragePercent"),
        "candidateDiff": data.get("candidateDiff"),
        "componentBoxes": top_boxes,
        "topMismatchCells": top_cells,
    }
    return json.dumps(summary, ensure_ascii=True)


def build_prompt(cv_summary: str) -> str:
    cv_text = (
        f"Supplemental computer-vision hints from a local script: {cv_summary}\n"
        if cv_summary
        else ""
    )
    return (
        "You are comparing two game UI screenshots.\n"
        "Image 1 is the REFERENCE from the real game.\n"
        "Image 2 is the CURRENT PEN RENDER candidate.\n"
        "Focus on layout structure, panel grouping, component count, spacing, alignment, and density.\n"
        "Do not reward similar dark background areas. Judge visible UI composition only.\n"
        "Assume the candidate currently fails visual parity.\n"
        "Return strict JSON only.\n"
        "Required top-level keys: summary, overallAssessment, regions, actions.\n"
        "overallAssessment must include layoutSimilarityPercent, structureSimilarityPercent, majorFailureReason.\n"
        "Each region must include name, priority, referenceRole, candidateIssue, expectedChange, approxBoundsPercent.\n"
        "Each action must include priority, target, editType, instruction, confidence.\n"
        "Use percentages conservatively. If the screenshots are very different, say so.\n"
        "Actions must be concrete pen-edit instructions, not generic advice.\n"
        f"{cv_text}"
    )


def send_request(api_url: str, api_key: str, payload: dict, timeout_seconds: int) -> dict:
    body = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        api_url,
        data=body,
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {api_key}",
        },
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
        return json.loads(response.read().decode("utf-8"))


def extract_json_content(response: dict) -> dict:
    content = response.get("message", {}).get("content", "")
    if not content:
        raise RuntimeError("Ollama response did not include message.content")

    try:
        return json.loads(content)
    except json.JSONDecodeError:
        pass

    fenced = re.search(r"```(?:json)?\s*(\{.*\}|\[.*\])\s*```", content, re.DOTALL)
    if fenced:
        return json.loads(fenced.group(1))

    inline = re.search(r"(\{.*\}|\[.*\])", content, re.DOTALL)
    if inline:
        return json.loads(inline.group(1))

    raise RuntimeError("Ollama response content was not valid JSON")


def normalize_percent(value: object, scale: float) -> float:
    try:
        number = float(value)
    except Exception:
        return 0.0
    if number <= 100.0:
        return round(number, 4)
    if scale <= 0:
        return round(number, 4)
    return round((number / scale) * 100.0, 4)


def normalize_priority(value: object) -> int:
    if isinstance(value, (int, float)):
        return int(value)
    mapping = {
        "critical": 1,
        "high": 2,
        "medium": 3,
        "low": 4,
    }
    return mapping.get(str(value).strip().lower(), 99)


def normalize_bounds(bounds: dict, image_width: int, image_height: int) -> dict:
    if {"left", "top", "right", "bottom"}.issubset(bounds.keys()):
        return {
            "left": normalize_percent(bounds.get("left"), image_width),
            "top": normalize_percent(bounds.get("top"), image_height),
            "right": normalize_percent(bounds.get("right"), image_width),
            "bottom": normalize_percent(bounds.get("bottom"), image_height),
        }

    left = normalize_percent(bounds.get("x", 0), image_width)
    top = normalize_percent(bounds.get("y", 0), image_height)
    width = normalize_percent(bounds.get("width", 0), image_width)
    height = normalize_percent(bounds.get("height", 0), image_height)
    return {
        "left": left,
        "top": top,
        "right": round(left + width, 4),
        "bottom": round(top + height, 4),
    }


def coerce_dict(value: object) -> dict:
    if isinstance(value, dict):
        return value
    return {}


def normalize_analysis(analysis: dict, image_width: int, image_height: int) -> dict:
    overall = dict(analysis.get("overallAssessment", {}))
    overall["layoutSimilarityPercent"] = float(overall.get("layoutSimilarityPercent", 0))
    overall["structureSimilarityPercent"] = float(overall.get("structureSimilarityPercent", 0))
    overall["majorFailureReason"] = str(overall.get("majorFailureReason", ""))

    normalized_regions = []
    for region in analysis.get("regions", []):
        region_dict = coerce_dict(region)
        bounds = normalize_bounds(coerce_dict(region_dict.get("approxBoundsPercent", {})), image_width, image_height)
        normalized_regions.append(
            {
                "name": str(region_dict.get("name", "")),
                "priority": normalize_priority(region_dict.get("priority")),
                "referenceRole": str(region_dict.get("referenceRole", "")),
                "candidateIssue": str(region_dict.get("candidateIssue", "")),
                "expectedChange": str(region_dict.get("expectedChange", "")),
                "approxBoundsPercent": bounds,
            }
        )

    normalized_actions = []
    for action in analysis.get("actions", []):
        action_dict = coerce_dict(action)
        confidence = action_dict.get("confidence", 0)
        try:
            confidence = float(confidence)
        except Exception:
            confidence = 0.0
        normalized_actions.append(
            {
                "priority": normalize_priority(action_dict.get("priority")),
                "target": str(action_dict.get("target", "")),
                "editType": str(action_dict.get("editType", "")),
                "instruction": str(action_dict.get("instruction", "")),
                "confidence": confidence,
            }
        )

    return {
        "summary": str(analysis.get("summary", "")),
        "overallAssessment": overall,
        "regions": normalized_regions,
        "actions": normalized_actions,
    }


def main() -> int:
    args = parse_args()
    api_key = os.environ.get("OLLAMA_API_KEY")
    if not api_key:
        print("OLLAMA_API_KEY is not set.", file=sys.stderr)
        return 2

    reference_path = Path(args.reference).resolve()
    candidate_path = Path(args.candidate).resolve()
    output_path = Path(args.output).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)

    cv_summary = ""
    if args.cv_analysis:
        cv_summary = load_cv_summary(Path(args.cv_analysis).resolve())

    image_width, image_height = get_image_size(reference_path)

    payload = {
        "model": args.model,
        "stream": False,
        "messages": [
            {
                "role": "user",
                "content": build_prompt(cv_summary),
                "images": [
                    encode_image(reference_path),
                    encode_image(candidate_path),
                ],
            }
        ],
    }
    if args.use_schema:
        payload["format"] = DEFAULT_SCHEMA

    try:
        response = send_request(args.api_url, api_key, payload, args.timeout_seconds)
        parsed = extract_json_content(response)
        parsed = normalize_analysis(parsed, image_width=image_width, image_height=image_height)
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        print(detail, file=sys.stderr)
        return 1
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        return 1

    artifact = {
        "model": args.model,
        "apiUrl": args.api_url,
        "referencePath": str(reference_path),
        "candidatePath": str(candidate_path),
        "analysis": parsed,
    }
    output_path.write_text(json.dumps(artifact, indent=2), encoding="utf-8")
    print(str(output_path))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
