// Demo composition for the runtime-surface React renderer: renders the sample
// reference-overview document (a realistic IViewSource-style runtime document)
// exactly as the Godot RuntimeSurfaceRenderer would lay it out.

import React from "react";
import { RuntimeSurfaceView } from "./runtime-surface";
import type { RuntimeSurfaceDocument } from "./runtime-surface";
import sampleDocument from "./runtime-surface/fixtures/reference-overview.sample.json";

export const runtimeSurfaceDemoWidth = 1280;
export const runtimeSurfaceDemoHeight = 720;
export const runtimeSurfaceDemoFps = 30;
export const runtimeSurfaceDemoDurationInFrames = 60;

export const RuntimeSurfaceDemo: React.FC = () => {
  const document = sampleDocument as RuntimeSurfaceDocument;
  return (
    <div
      style={{
        width: runtimeSurfaceDemoWidth,
        height: runtimeSurfaceDemoHeight,
        background: "#101312",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <RuntimeSurfaceView document={document} />
    </div>
  );
};
