#!/usr/bin/env python3
"""Extract structural UI hints from a reference image and compare them to a candidate."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import cv2
import numpy as np


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", required=True)
    parser.add_argument("--candidate")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--grid-cols", type=int, default=12)
    parser.add_argument("--grid-rows", type=int, default=12)
    parser.add_argument("--tolerance", type=int, default=16)
    parser.add_argument("--max-boxes", type=int, default=16)
    return parser.parse_args()


def load_image(path: Path) -> np.ndarray:
    image = cv2.imread(str(path), cv2.IMREAD_COLOR)
    if image is None:
        raise FileNotFoundError(f"Could not read image: {path}")
    return image


def percentile_uint8(values: np.ndarray, value: float) -> int:
    return int(np.clip(np.percentile(values, value), 0, 255))


def percentile_uint8_nonzero(values: np.ndarray, value: float, fallback: int) -> int:
    nonzero = values[values > 0]
    if nonzero.size == 0:
        return fallback
    return int(np.clip(np.percentile(nonzero, value), 0, 255))


def build_ui_mask(image: np.ndarray) -> tuple[np.ndarray, dict[str, int]]:
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
    saturation = hsv[:, :, 1]
    value = hsv[:, :, 2]

    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blurred, 60, 140)
    gradient_x = cv2.Sobel(blurred, cv2.CV_32F, 1, 0, ksize=3)
    gradient_y = cv2.Sobel(blurred, cv2.CV_32F, 0, 1, ksize=3)
    gradient = cv2.magnitude(gradient_x, gradient_y)
    gradient_u8 = cv2.normalize(gradient, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)

    sat_threshold = max(18, percentile_uint8(saturation, 88))
    val_threshold = max(42, percentile_uint8(value, 74))
    gradient_threshold = max(8, percentile_uint8_nonzero(gradient_u8, 84, 16))

    bright_or_saturated = ((saturation >= sat_threshold) & (value >= max(48, val_threshold - 4))).astype(np.uint8) * 255
    high_contrast = ((gradient_u8 >= gradient_threshold) & (value >= 24)).astype(np.uint8) * 255
    combined = cv2.bitwise_or(edges, bright_or_saturated)
    combined = cv2.bitwise_or(combined, high_contrast)

    kernel_small = cv2.getStructuringElement(cv2.MORPH_RECT, (3, 3))
    kernel_merge = cv2.getStructuringElement(cv2.MORPH_RECT, (7, 7))
    mask = cv2.morphologyEx(combined, cv2.MORPH_CLOSE, kernel_small, iterations=1)
    mask = cv2.dilate(mask, kernel_small, iterations=1)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel_merge, iterations=1)
    mask = cv2.erode(mask, kernel_small, iterations=1)
    mask = cv2.medianBlur(mask, 5)

    return mask, {
        "satThreshold": sat_threshold,
        "valueThreshold": val_threshold,
        "gradientThreshold": gradient_threshold,
    }


def extract_boxes(mask: np.ndarray, max_boxes: int) -> list[dict[str, float]]:
    height, width = mask.shape
    min_area = max(250, int(width * height * 0.0015))
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    boxes: list[dict[str, float]] = []
    for contour in contours:
        x, y, w, h = cv2.boundingRect(contour)
        area = int(cv2.contourArea(contour))
        if area < min_area:
            continue
        if (w / float(width)) >= 0.9 and (h / float(height)) >= 0.9:
            continue
        boxes.append(
            {
                "x": int(x),
                "y": int(y),
                "width": int(w),
                "height": int(h),
                "area": int(area),
                "coveragePercent": round((area / float(width * height)) * 100.0, 4),
            }
        )

    boxes.sort(key=lambda item: item["area"], reverse=True)
    return boxes[:max_boxes]


def smooth_projection(values: np.ndarray, kernel_size: int = 21) -> np.ndarray:
    kernel = np.ones(kernel_size, dtype=np.float32) / kernel_size
    return np.convolve(values, kernel, mode="same")


def extract_bands(projection: np.ndarray, axis: str) -> list[dict[str, float]]:
    threshold = float(projection.mean() + projection.std() * 0.35)
    active = projection >= threshold
    bands: list[dict[str, float]] = []
    start = None

    for index, enabled in enumerate(active):
        if enabled and start is None:
            start = index
        elif not enabled and start is not None:
            end = index
            bands.append(make_band(start, end, projection, axis))
            start = None

    if start is not None:
        bands.append(make_band(start, len(projection), projection, axis))

    return [band for band in bands if band["span"] >= 12]


def make_band(start: int, end: int, projection: np.ndarray, axis: str) -> dict[str, float]:
    segment = projection[start:end]
    return {
        "axis": axis,
        "start": int(start),
        "end": int(end),
        "span": int(end - start),
        "meanDensity": round(float(segment.mean()), 4),
        "peakDensity": round(float(segment.max()), 4),
    }


def cell_bounds(index: int, count: int, size: int) -> tuple[int, int]:
    start = int(round(index * size / count))
    end = int(round((index + 1) * size / count))
    return start, end


def build_grid_report(
    reference: np.ndarray,
    mask: np.ndarray,
    candidate: np.ndarray | None,
    grid_cols: int,
    grid_rows: int,
    tolerance: int,
) -> tuple[list[dict[str, float]], list[dict[str, float]]]:
    height, width = mask.shape
    cells: list[dict[str, float]] = []
    mismatch_cells: list[dict[str, float]] = []
    resized_candidate = None
    if candidate is not None:
        resized_candidate = cv2.resize(candidate, (width, height), interpolation=cv2.INTER_AREA)

    for row in range(grid_rows):
        y0, y1 = cell_bounds(row, grid_rows, height)
        for col in range(grid_cols):
            x0, x1 = cell_bounds(col, grid_cols, width)
            mask_cell = mask[y0:y1, x0:x1]
            ref_cell = reference[y0:y1, x0:x1]
            cell = {
                "row": row,
                "col": col,
                "x": x0,
                "y": y0,
                "width": x1 - x0,
                "height": y1 - y0,
                "uiDensity": round(float(mask_cell.mean() / 255.0), 4),
                "referenceMeanBgr": [round(float(v), 2) for v in ref_cell.mean(axis=(0, 1))],
            }

            if resized_candidate is not None:
                cand_cell = resized_candidate[y0:y1, x0:x1]
                channel_diff = np.max(np.abs(ref_cell.astype(np.int16) - cand_cell.astype(np.int16)), axis=2)
                mismatch = channel_diff > tolerance
                pixel_identity = 1.0 - float(mismatch.mean())
                mean_abs = float(channel_diff.mean())
                cell["candidatePixelIdentityPercent"] = round(pixel_identity * 100.0, 4)
                cell["candidateMeanAbsDelta"] = round(mean_abs, 4)
                if cell["uiDensity"] >= 0.05:
                    mismatch_cells.append(
                        {
                            "row": row,
                            "col": col,
                            "uiDensity": cell["uiDensity"],
                            "candidatePixelIdentityPercent": cell["candidatePixelIdentityPercent"],
                            "candidateMeanAbsDelta": cell["candidateMeanAbsDelta"],
                            "x": x0,
                            "y": y0,
                            "width": x1 - x0,
                            "height": y1 - y0,
                        }
                    )

            cells.append(cell)

    mismatch_cells.sort(
        key=lambda item: (
            item["candidatePixelIdentityPercent"],
            -item["uiDensity"],
            -item["candidateMeanAbsDelta"],
        )
    )
    return cells, mismatch_cells[:12]


def create_overlay(image: np.ndarray, mask: np.ndarray, boxes: list[dict[str, float]], output_path: Path) -> None:
    overlay = image.copy()
    color_mask = np.zeros_like(image)
    color_mask[:, :, 1] = mask
    overlay = cv2.addWeighted(overlay, 1.0, color_mask, 0.25, 0)

    for index, box in enumerate(boxes, start=1):
        x = int(box["x"])
        y = int(box["y"])
        w = int(box["width"])
        h = int(box["height"])
        cv2.rectangle(overlay, (x, y), (x + w, y + h), (0, 220, 255), 2)
        cv2.putText(
            overlay,
            str(index),
            (x + 4, max(18, y + 18)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.6,
            (0, 220, 255),
            2,
            cv2.LINE_AA,
        )

    cv2.imwrite(str(output_path), overlay)


def create_diff_heatmap(reference: np.ndarray, candidate: np.ndarray, output_path: Path) -> dict[str, float]:
    height, width = reference.shape[:2]
    resized_candidate = cv2.resize(candidate, (width, height), interpolation=cv2.INTER_AREA)
    delta = np.max(np.abs(reference.astype(np.int16) - resized_candidate.astype(np.int16)), axis=2).astype(np.uint8)
    heatmap = cv2.applyColorMap(delta, cv2.COLORMAP_INFERNO)
    blended = cv2.addWeighted(reference, 0.55, heatmap, 0.45, 0)
    cv2.imwrite(str(output_path), blended)
    return {
        "meanAbsDelta": round(float(delta.mean()), 4),
        "p95AbsDelta": round(float(np.percentile(delta, 95)), 4),
        "maxAbsDelta": int(delta.max()),
    }


def main() -> None:
    args = parse_args()
    reference_path = Path(args.reference).resolve()
    candidate_path = Path(args.candidate).resolve() if args.candidate else None
    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    reference = load_image(reference_path)
    candidate = load_image(candidate_path) if candidate_path else None

    mask, thresholds = build_ui_mask(reference)
    boxes = extract_boxes(mask, args.max_boxes)

    x_projection = smooth_projection(mask.mean(axis=0) / 255.0)
    y_projection = smooth_projection(mask.mean(axis=1) / 255.0)
    vertical_bands = extract_bands(x_projection, "x")
    horizontal_bands = extract_bands(y_projection, "y")

    grid_cells, top_mismatch_cells = build_grid_report(
        reference,
        mask,
        candidate,
        args.grid_cols,
        args.grid_rows,
        args.tolerance,
    )

    mask_path = output_dir / "ui-mask.png"
    overlay_path = output_dir / "ui-overlay.png"
    cv2.imwrite(str(mask_path), mask)
    create_overlay(reference, mask, boxes, overlay_path)

    diff_summary = None
    diff_path = None
    if candidate is not None:
        diff_path = output_dir / "candidate-diff-heatmap.png"
        diff_summary = create_diff_heatmap(reference, candidate, diff_path)

    report = {
        "referencePath": str(reference_path),
        "candidatePath": str(candidate_path) if candidate_path else None,
        "imageSize": {
            "width": int(reference.shape[1]),
            "height": int(reference.shape[0]),
        },
        "uiCoveragePercent": round(float(mask.mean() / 255.0) * 100.0, 4),
        "thresholds": thresholds,
        "dominantBands": {
            "vertical": vertical_bands,
            "horizontal": horizontal_bands,
        },
        "componentBoxes": boxes,
        "grid": {
            "cols": args.grid_cols,
            "rows": args.grid_rows,
            "cells": grid_cells,
            "topMismatchCells": top_mismatch_cells,
        },
        "artifacts": {
            "maskPath": str(mask_path),
            "overlayPath": str(overlay_path),
            "diffHeatmapPath": str(diff_path) if diff_path else None,
        },
        "candidateDiff": diff_summary,
    }

    report_path = output_dir / "analysis.json"
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(str(report_path))


if __name__ == "__main__":
    main()
