import React from "react";
import { AbsoluteFill } from "remotion";
import { z } from "zod";
import { FontReadyGate } from "./FontReadyGate";
import {
  MotionScene,
  getRequiredMotionSequence,
  getSequenceDurationFrames,
  parseMotionDocument,
  resolveSequenceStateAtFrame,
} from "./motion";
import { ExploreHudView } from "./generated/ExploreHudView";
import type { ExploreHudViewModel } from "./generated/ExploreHudView";
import motionJson from "./motion-samples/char-portrait.motion.json";

const motionDocument = parseMotionDocument(motionJson);

export const generatedFullPenDemoSequence = getRequiredMotionSequence(motionDocument);

export const generatedFullPenDemoDurationInFrames = getSequenceDurationFrames(
  motionDocument,
  generatedFullPenDemoSequence,
);

export const generatedFullPenDemoFramesPerSecond = motionDocument.framesPerSecond;

const finalMotionTargets = resolveSequenceStateAtFrame(
  motionDocument,
  generatedFullPenDemoSequence,
  generatedFullPenDemoDurationInFrames,
);

export const GeneratedFullPenDemoSchema = z.object({
  animated: z.boolean().default(false),
});
export type GeneratedFullPenDemoSchema = z.infer<typeof GeneratedFullPenDemoSchema>;

const demoViewModel: ExploreHudViewModel = {};

export const GeneratedFullPenDemo: React.FC<GeneratedFullPenDemoSchema> = ({
  animated = false,
}) => {
  return (
    <FontReadyGate>
      <AbsoluteFill
        style={{
          background: "#050505",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        {animated ? (
          <MotionScene
            document={motionDocument}
            sequence={generatedFullPenDemoSequence}
            component={ExploreHudView}
            viewModel={demoViewModel}
          />
        ) : (
          <ExploreHudView {...demoViewModel} motionTargets={finalMotionTargets} />
        )}
      </AbsoluteFill>
    </FontReadyGate>
  );
};
