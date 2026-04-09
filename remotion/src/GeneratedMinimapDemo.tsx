import React from "react";
import { AbsoluteFill } from "remotion";
import { z } from "zod";
import {
  MotionScene,
  getRequiredMotionSequence,
  getSequenceDurationFrames,
  parseMotionDocument,
  resolveSequenceStateAtFrame,
} from "./motion";
import { MinimapView } from "./generated/MinimapView";
import type { MinimapViewModel } from "./generated/MinimapView";
import motionJson from "./motion-samples/minimap.motion.json";

const motionDocument = parseMotionDocument(motionJson);

export const generatedMinimapDemoSequence = getRequiredMotionSequence(motionDocument);

export const generatedMinimapDemoDurationInFrames = getSequenceDurationFrames(
  motionDocument,
  generatedMinimapDemoSequence,
);

export const generatedMinimapDemoFramesPerSecond = motionDocument.framesPerSecond;

const finalMotionTargets = resolveSequenceStateAtFrame(
  motionDocument,
  generatedMinimapDemoSequence,
  generatedMinimapDemoDurationInFrames,
);

export const GeneratedMinimapDemoSchema = z.object({
  animated: z.boolean().default(false),
});
export type GeneratedMinimapDemoSchema = z.infer<typeof GeneratedMinimapDemoSchema>;

const demoViewModel: MinimapViewModel = {};

const viewportStyle: React.CSSProperties = {
  background:
    "radial-gradient(circle at top, #2a2a2a 0%, #101010 52%, #030303 100%)",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
};

const stageStyle: React.CSSProperties = {
  transform: "scale(2.2)",
  transformOrigin: "center center",
};

export const GeneratedMinimapDemo: React.FC<GeneratedMinimapDemoSchema> = ({
  animated = false,
}) => {
  return (
    <AbsoluteFill style={viewportStyle}>
      <div style={stageStyle}>
        {animated ? (
          <MotionScene
            document={motionDocument}
            sequence={generatedMinimapDemoSequence}
            component={MinimapView}
            viewModel={demoViewModel}
          />
        ) : (
          <MinimapView {...demoViewModel} motionTargets={finalMotionTargets} />
        )}
      </div>
    </AbsoluteFill>
  );
};