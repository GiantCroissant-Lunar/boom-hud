import React from "react";
import { AbsoluteFill } from "remotion";
import { z } from "zod";
import { FontReadyGate } from "./FontReadyGate";
import {
  MotionScene,
  getSequenceDurationFrames,
  getRequiredMotionSequence,
  parseMotionDocument,
  resolveSequenceStateAtFrame,
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

const finalMotionTargets = resolveSequenceStateAtFrame(
  motionDocument,
  generatedMotionDemoSequence,
  generatedMotionDemoDurationInFrames,
);

export const GeneratedMotionDemoSchema = z.object({
  animated: z.boolean().default(true),
  isolated: z.boolean().default(false),
});
export type GeneratedMotionDemoSchema = z.infer<typeof GeneratedMotionDemoSchema>;

const demoViewModel: CharPortraitViewModel = {};

export const GeneratedMotionDemo: React.FC<GeneratedMotionDemoSchema> = ({
  animated = true,
  isolated = false,
}) => {
  const content = animated ? (
    <MotionScene
      document={motionDocument}
      sequence={generatedMotionDemoSequence}
      component={CharPortraitView}
      viewModel={demoViewModel}
    />
  ) : (
    <CharPortraitView {...demoViewModel} motionTargets={finalMotionTargets} />
  );

  if (isolated) {
    return (
      <FontReadyGate>
        <div style={{ display: "inline-flex" }}>{content}</div>
      </FontReadyGate>
    );
  }

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
        {content}
      </AbsoluteFill>
    </FontReadyGate>
  );
};
