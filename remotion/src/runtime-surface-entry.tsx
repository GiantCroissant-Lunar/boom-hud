// Standalone Remotion entry for the runtime-surface demo. The main src/index.tsx
// root pulls in generated demos that use import.meta.glob (Vite-only, not
// webpack-bundlable), so the runtime-surface composition gets its own entry to
// stay renderable via `remotion still/render` regardless.

import React from "react";
import { Composition, registerRoot } from "remotion";
import {
  RuntimeSurfaceDemo,
  runtimeSurfaceDemoDurationInFrames,
  runtimeSurfaceDemoFps,
  runtimeSurfaceDemoHeight,
  runtimeSurfaceDemoWidth,
} from "./RuntimeSurfaceDemo";

const RuntimeSurfaceRoot: React.FC = () => (
  <Composition
    id="RuntimeSurfaceDemo"
    component={RuntimeSurfaceDemo}
    durationInFrames={runtimeSurfaceDemoDurationInFrames}
    fps={runtimeSurfaceDemoFps}
    width={runtimeSurfaceDemoWidth}
    height={runtimeSurfaceDemoHeight}
  />
);

registerRoot(RuntimeSurfaceRoot);
