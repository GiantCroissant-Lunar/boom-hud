#!/usr/bin/env node
/**
 * BoomHud Video Stitcher CLI
 *
 * Reads snapshots.manifest.json and renders a preview video.
 *
 * Usage:
 *   npx ts-node render.ts --snapshots ../ui/snapshots --out preview.mp4
 *
 * Comparison mode:
 *   npx ts-node render.ts --snapshots ../ui/snapshots --baseline ../ui/snapshots-baseline --out compare.mp4
 */

import { execSync } from "child_process";
import * as fs from "fs";
import * as path from "path";

interface SnapshotManifest {
  version: string;
  target: string;
  viewport: { width: number; height: number; scale: number };
  snapshots: Array<{ state: string; path: string; sha256: string }>;
}

interface RenderOptions {
  snapshotsDir: string;
  baselineDir: string | null;
  outPath: string;
  fps: number;
  secondsPerState: number;
  showTitleOverlay: boolean;
}

function parseArgs(): RenderOptions {
  const args = process.argv.slice(2);
  const options: RenderOptions = {
    snapshotsDir: "",
    baselineDir: null,
    outPath: "preview.mp4",
    fps: 30,
    secondsPerState: 1.5,
    showTitleOverlay: true,
  };

  for (let i = 0; i < args.length; i++) {
    switch (args[i]) {
      case "--snapshots":
        options.snapshotsDir = args[++i];
        break;
      case "--baseline":
        options.baselineDir = args[++i];
        break;
      case "--out":
        options.outPath = args[++i];
        break;
      case "--fps":
        options.fps = parseInt(args[++i], 10);
        break;
      case "--seconds-per-state":
        options.secondsPerState = parseFloat(args[++i]);
        break;
      case "--title-overlay":
        options.showTitleOverlay = args[++i] !== "off";
        break;
      case "--help":
        console.log(`
BoomHud Video Stitcher

Usage:
  npx ts-node render.ts --snapshots <dir> --out <file.mp4>
  npx ts-node render.ts --snapshots <dir> --baseline <dir> --out compare.mp4

Options:
  --snapshots <dir>         Directory containing current snapshots and manifest
  --baseline <dir>          Directory containing baseline snapshots (enables comparison mode)
  --out <file>              Output video file (default: preview.mp4)
  --fps <number>            Frames per second (default: 30)
  --seconds-per-state <n>   Duration per state in seconds (default: 1.5)
  --title-overlay on|off    Show state name overlay (default: on)
`);
        process.exit(0);
    }
  }

  if (!options.snapshotsDir) {
    console.error("Error: --snapshots is required");
    process.exit(1);
  }

  return options;
}

function loadManifest(snapshotsDir: string): SnapshotManifest | null {
  const manifestPath = path.join(snapshotsDir, "snapshots.manifest.json");
  if (!fs.existsSync(manifestPath)) {
    return null;
  }

  const content = fs.readFileSync(manifestPath, "utf-8");
  return JSON.parse(content) as SnapshotManifest;
}

function copySnapshotsToPublic(
  snapshotsDir: string,
  manifest: SnapshotManifest,
  prefix: string = ""
): string[] {
  // Remotion's staticFile() requires files in public/
  const publicDir = path.join(__dirname, "public");
  if (!fs.existsSync(publicDir)) {
    fs.mkdirSync(publicDir, { recursive: true });
  }

  const copiedPaths: string[] = [];

  for (const snapshot of manifest.snapshots) {
    const srcPath = path.join(snapshotsDir, snapshot.path);
    const destFileName = prefix ? `${prefix}_${snapshot.path}` : snapshot.path;
    const destPath = path.join(publicDir, destFileName);
    
    if (fs.existsSync(srcPath)) {
      fs.copyFileSync(srcPath, destPath);
      copiedPaths.push(destFileName);
    } else {
      console.warn(`Warning: Snapshot not found: ${srcPath}`);
      copiedPaths.push(""); // Mark as missing
    }
  }

  return copiedPaths;
}

function renderSingle(options: RenderOptions, manifest: SnapshotManifest): void {
  const { fps, secondsPerState, showTitleOverlay, outPath } = options;

  // Copy snapshots to public/
  copySnapshotsToPublic(options.snapshotsDir, manifest);

  // Prepare input props for Remotion
  const inputProps = {
    snapshotsDir: options.snapshotsDir,
    fps,
    secondsPerState,
    showTitleOverlay,
    snapshots: manifest.snapshots.map((s) => ({
      state: s.state,
      path: s.path,
    })),
  };

  // Calculate dimensions from manifest
  const width = manifest.viewport.width;
  const height = manifest.viewport.height;

  // Calculate duration
  const durationInFrames = Math.ceil(
    manifest.snapshots.length * secondsPerState * fps
  );

  console.log(`Rendering ${manifest.snapshots.length} snapshots to ${outPath}`);
  console.log(`  Resolution: ${width}x${height}`);
  console.log(`  FPS: ${fps}`);
  console.log(`  Duration: ${(durationInFrames / fps).toFixed(1)}s`);

  // Invoke Remotion render
  const propsJson = JSON.stringify(inputProps).replace(/"/g, '\\"');
  const cmd = [
    "npx",
    "remotion",
    "render",
    "Snapshots",
    outPath,
    `--props="${propsJson}"`,
    `--width=${width}`,
    `--height=${height}`,
  ].join(" ");

  try {
    execSync(cmd, { stdio: "inherit", cwd: __dirname });
    console.log(`✓ Video rendered: ${outPath}`);
  } catch (error) {
    console.error("Error rendering video");
    process.exit(1);
  }
}

function renderComparison(
  options: RenderOptions,
  currentManifest: SnapshotManifest,
  baselineManifest: SnapshotManifest
): void {
  const { fps, secondsPerState, showTitleOverlay, outPath } = options;

  // Copy snapshots to public/ with prefixes to avoid collisions
  const currentPaths = copySnapshotsToPublic(options.snapshotsDir, currentManifest, "current");
  const baselinePaths = copySnapshotsToPublic(options.baselineDir!, baselineManifest, "baseline");

  // Build snapshot arrays with prefixed paths
  const currentSnapshots = currentManifest.snapshots.map((s, i) => ({
    state: s.state,
    path: currentPaths[i] || null,
  })).filter((s) => s.path);

  const baselineSnapshots = baselineManifest.snapshots.map((s, i) => ({
    state: s.state,
    path: baselinePaths[i] || null,
  })).filter((s) => s.path);

  // Prepare input props for Remotion Compare composition
  const inputProps = {
    fps,
    secondsPerState,
    showTitleOverlay,
    baselineSnapshots,
    currentSnapshots,
  };

  // Use wider resolution for side-by-side
  const width = 1920;
  const height = 1080;

  // Calculate duration (union of states)
  const allStates = new Set([
    ...currentSnapshots.map((s) => s.state),
    ...baselineSnapshots.map((s) => s.state),
  ]);
  const durationInFrames = Math.ceil(allStates.size * secondsPerState * fps);

  console.log(`Comparing ${baselineSnapshots.length} baseline vs ${currentSnapshots.length} current snapshots`);
  console.log(`  Total states: ${allStates.size}`);
  console.log(`  Resolution: ${width}x${height}`);
  console.log(`  FPS: ${fps}`);
  console.log(`  Duration: ${(durationInFrames / fps).toFixed(1)}s`);

  // Invoke Remotion render with Compare composition
  const propsJson = JSON.stringify(inputProps).replace(/"/g, '\\"');
  const cmd = [
    "npx",
    "remotion",
    "render",
    "Compare",
    outPath,
    `--props="${propsJson}"`,
    `--width=${width}`,
    `--height=${height}`,
  ].join(" ");

  try {
    execSync(cmd, { stdio: "inherit", cwd: __dirname });
    console.log(`✓ Comparison video rendered: ${outPath}`);
  } catch (error) {
    console.error("Error rendering comparison video");
    process.exit(1);
  }
}

// Main
const options = parseArgs();

// Load current manifest (required)
const currentManifest = loadManifest(options.snapshotsDir);
if (!currentManifest) {
  console.error(`Error: Manifest not found in ${options.snapshotsDir}`);
  process.exit(1);
}

// Check for comparison mode
if (options.baselineDir) {
  const baselineManifest = loadManifest(options.baselineDir);
  if (!baselineManifest) {
    console.error(`Error: Baseline manifest not found in ${options.baselineDir}`);
    process.exit(1);
  }
  renderComparison(options, currentManifest, baselineManifest);
} else {
  renderSingle(options, currentManifest);
}
