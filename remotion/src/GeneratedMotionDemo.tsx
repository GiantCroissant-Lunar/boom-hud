import React from "react";
import { AbsoluteFill } from "remotion";
import { z } from "zod";
import { MotionScene, parseMotionDocument } from "./motion";
import { DebugOverlayView } from "./generated/DebugOverlayView";
import type { DebugOverlayViewModel } from "./generated/DebugOverlayView";
import motionJson from "./motion-samples/debug-overlay.motion.json";

const motionDocument = parseMotionDocument(motionJson);
const introClip = motionDocument.clips.find((clip) => clip.id === "intro");

if (!introClip) {
  throw new Error("Expected intro clip in debug-overlay.motion.json");
}

export const GeneratedMotionDemoSchema = z.object({});
export type GeneratedMotionDemoSchema = z.infer<typeof GeneratedMotionDemoSchema>;

const demoViewModel: DebugOverlayViewModel = {
  version: "v0.9.7",
  fps: 60,
  memoryUsage: "412 MB",
  playerPosition: "123, 64, -18",
  currentChunk: "14,07",
};

export const GeneratedMotionDemo: React.FC<GeneratedMotionDemoSchema> = () => {
  return (
    <AbsoluteFill
      style={{
        background:
          "radial-gradient(circle at top left, #1f2937 0%, #0f172a 45%, #020617 100%)",
        display: "flex",
        alignItems: "flex-start",
        justifyContent: "flex-start",
        padding: 48,
      }}
    >
      <MotionScene
        document={motionDocument}
        clipId={introClip.id}
        component={DebugOverlayView}
        viewModel={demoViewModel}
      />
    </AbsoluteFill>
  );
};
