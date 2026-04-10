#!/usr/bin/env node

import { spawnSync } from "child_process";
import * as fs from "fs";
import * as path from "path";
import { fileURLToPath } from "url";

type RemotionRenderTarget = {
  compositionId: string;
  output?: string;
  outputDir?: string;
  frame?: number;
  props?: Record<string, unknown>;
};

type FidelitySurface = {
  id: string;
  remotion?: RemotionRenderTarget;
};

type FidelityTimeline = {
  id: string;
  sampleFrames?: number[];
  remotion?: RemotionRenderTarget;
};

type FidelityManifest = {
  artifactsRoot?: string;
  surfaces?: FidelitySurface[];
  timelines?: FidelityTimeline[];
};

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDirectory, "..");

const parseManifestPath = (): string => {
  const args = process.argv.slice(2);
  for (let index = 0; index < args.length - 1; index++) {
    if (args[index] === "--manifest") {
      return path.isAbsolute(args[index + 1])
        ? args[index + 1]
        : path.resolve(repoRoot, args[index + 1]);
    }
  }

  return path.join(repoRoot, "fidelity", "pen-remotion-unity.fullpen.json");
};

const ensureParentDirectory = (filePath: string): void => {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
};

const renderStill = (
  compositionId: string,
  outputPath: string,
  frame: number,
  props: Record<string, unknown> | undefined,
): void => {
  ensureParentDirectory(outputPath);
  const propsFilePath = path.join(
    path.dirname(outputPath),
    `${path.basename(outputPath, path.extname(outputPath))}.props.json`,
  );
  fs.writeFileSync(propsFilePath, JSON.stringify(props ?? {}, null, 2));

  const npxCommand = "npx";
  const result = spawnSync(
    npxCommand,
    [
      "remotion",
      "still",
      compositionId,
      outputPath,
      `--frame=${frame}`,
      "--image-format=png",
      `--props=${propsFilePath}`,
    ],
    {
      cwd: scriptDirectory,
      shell: process.platform === "win32",
      stdio: "inherit",
    },
  );

  if (result.status !== 0) {
    throw new Error(
      `Failed to render '${compositionId}' to '${outputPath}' (exit code ${result.status ?? -1}).`,
    );
  }

  fs.rmSync(propsFilePath, { force: true });
};

const manifestPath = parseManifestPath();
const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf-8")) as FidelityManifest;
const artifactsRoot = path.resolve(repoRoot, manifest.artifactsRoot ?? "build/fidelity/latest");

for (const surface of manifest.surfaces ?? []) {
  if (!surface.remotion?.output) {
    continue;
  }

  const outputPath = path.resolve(artifactsRoot, surface.remotion.output);
  renderStill(
    surface.remotion.compositionId,
    outputPath,
    surface.remotion.frame ?? 0,
    surface.remotion.props,
  );
}

for (const timeline of manifest.timelines ?? []) {
  if (!timeline.remotion?.outputDir) {
    continue;
  }

  const outputDir = path.resolve(artifactsRoot, timeline.remotion.outputDir);
  fs.mkdirSync(outputDir, { recursive: true });

  for (const frame of timeline.sampleFrames ?? []) {
    renderStill(
      timeline.remotion.compositionId,
      path.join(outputDir, `frame-${frame.toString().padStart(4, "0")}.png`),
      frame,
      timeline.remotion.props,
    );
  }
}
