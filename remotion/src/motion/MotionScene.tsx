import React from "react";
import { useMotionClipState } from "./runtime";
import type { MotionDocument } from "./schema";

type MotionEnabledViewModel = {
  motionTargets?: Record<string, unknown>;
};

type MotionSceneProps<TViewModel extends MotionEnabledViewModel> = {
  document: MotionDocument;
  clipId: string;
  component: React.ComponentType<TViewModel>;
  viewModel: Omit<TViewModel, "motionTargets">;
};

export const MotionScene = <TViewModel extends MotionEnabledViewModel>({
  document,
  clipId,
  component: Component,
  viewModel,
}: MotionSceneProps<TViewModel>): React.JSX.Element => {
  const motionTargets = useMotionClipState(document, clipId);
  return <Component {...(viewModel as TViewModel)} motionTargets={motionTargets} />;
};
