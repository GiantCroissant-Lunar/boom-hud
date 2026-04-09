import React from "react";
import { AbsoluteFill } from "remotion";
import { z } from "zod";
import { MotionScene, parseMotionDocument } from "./motion";
import { CharPortraitView } from "./generated/CharPortraitView";
import type { CharPortraitViewModel } from "./generated/CharPortraitView";
import motionJson from "./motion-samples/char-portrait.motion.json";

const motionDocument = parseMotionDocument(motionJson);
const introClip = motionDocument.clips.find((clip) => clip.id === "intro");

if (!introClip) {
  throw new Error("Expected intro clip in char-portrait.motion.json");
}

export const GeneratedMotionDemoSchema = z.object({});
export type GeneratedMotionDemoSchema = z.infer<typeof GeneratedMotionDemoSchema>;

const demoViewModel: CharPortraitViewModel = {};

export const GeneratedMotionDemo: React.FC<GeneratedMotionDemoSchema> = () => {
  return (
    <AbsoluteFill
      style={{
        background: "#050505",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <MotionScene
        document={motionDocument}
        clipId={introClip.id}
        component={CharPortraitView}
        viewModel={demoViewModel}
      />
    </AbsoluteFill>
  );
};
