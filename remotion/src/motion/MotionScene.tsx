import React from "react";
import { useMotionSceneState, type MotionSequence } from "./runtime";
import type { MotionDocument } from "./schema";

type MotionEnabledViewModel = {
  motionTargets?: Record<string, unknown>;
};

type MotionSceneBaseProps<TViewModel extends MotionEnabledViewModel> = {
  document: MotionDocument;
  component: React.ComponentType<TViewModel>;
  viewModel: Omit<TViewModel, "motionTargets">;
};

type MotionSceneClipProps<TViewModel extends MotionEnabledViewModel> =
  MotionSceneBaseProps<TViewModel> & {
    clipId: string;
    sequence?: never;
  };

type MotionSceneSequenceProps<TViewModel extends MotionEnabledViewModel> =
  MotionSceneBaseProps<TViewModel> & {
    clipId?: never;
    sequence: MotionSequence;
  };

type MotionSceneProps<TViewModel extends MotionEnabledViewModel> =
  | MotionSceneClipProps<TViewModel>
  | MotionSceneSequenceProps<TViewModel>;

export const MotionScene = <TViewModel extends MotionEnabledViewModel>({
  document,
  clipId,
  sequence,
  component: Component,
  viewModel,
}: MotionSceneProps<TViewModel>): React.JSX.Element => {
  const motionTargets = useMotionSceneState(document, { clipId, sequence });
  return <Component {...(viewModel as TViewModel)} motionTargets={motionTargets} />;
};
