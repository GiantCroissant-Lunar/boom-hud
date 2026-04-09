import React from "react";
import { AbsoluteFill } from "remotion";
import { z } from "zod";
import {
  MotionScene,
  getSequenceDurationFrames,
  getRequiredMotionSequence,
  parseMotionDocument,
} from "./motion";
import { CharPortraitView } from "./generated/CharPortraitView";
import type { CharPortraitViewModel } from "./generated/CharPortraitView";
import motionJson from "./motion-samples/char-portrait.motion.json";

const motionDocument = parseMotionDocument(motionJson);

export const generatedMotionDemoSequence = getRequiredMotionSequence(motionDocument);

export const generatedMotionDemoDurationInFrames = getSequenceDurationFrames(
  motionDocument,
  generatedMotionDemoSequence,
);

export const generatedMotionDemoFramesPerSecond = motionDocument.framesPerSecond;

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
        sequence={generatedMotionDemoSequence}
        component={CharPortraitView}
        viewModel={demoViewModel}
      />
    </AbsoluteFill>
  );
};
