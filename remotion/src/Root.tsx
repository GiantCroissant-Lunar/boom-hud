import { Composition, getInputProps } from "remotion";
import { SnapshotsComposition, SnapshotsSchema } from "./Snapshots";
import { CompareComposition, CompareSchema } from "./Compare";
import {
  GeneratedMotionDemo,
  GeneratedMotionDemoSchema,
  generatedMotionDemoDurationInFrames,
  generatedMotionDemoFramesPerSecond,
} from "./GeneratedMotionDemo";
import {
  GeneratedFullPenDemo,
  GeneratedFullPenDemoSchema,
  generatedFullPenDemoDurationInFrames,
  generatedFullPenDemoFramesPerSecond,
} from "./GeneratedFullPenDemo";
import {
  GeneratedMinimapDemo,
  GeneratedMinimapDemoSchema,
  generatedMinimapDemoDurationInFrames,
  generatedMinimapDemoFramesPerSecond,
} from "./GeneratedMinimapDemo";
import {
  GeneratedComponentDemo,
  GeneratedComponentDemoSchema,
} from "./GeneratedComponentDemo";

export const RemotionRoot: React.FC = () => {
  // Default props for preview - will be overridden by CLI input
  const defaultProps: SnapshotsSchema = {
    snapshotsDir: "../ui/snapshots",
    fps: 30,
    secondsPerState: 1.5,
    showTitleOverlay: true,
    snapshots: [],
  };

  const defaultCompareProps: CompareSchema = {
    fps: 30,
    secondsPerState: 1.5,
    showTitleOverlay: true,
    baselineSnapshots: [],
    currentSnapshots: [],
  };

  // Get input props from CLI or use defaults
  const inputProps = getInputProps() as Partial<SnapshotsSchema & CompareSchema>;
  const props = { ...defaultProps, ...inputProps };
  const compareProps = { ...defaultCompareProps, ...inputProps };

  // Calculate duration based on snapshots
  const snapshotCount = props.snapshots?.length || 1;
  const durationInFrames = Math.ceil(snapshotCount * props.secondsPerState * props.fps);

  // Calculate compare duration (union of baseline + current states)
  const compareStates = new Set([
    ...(compareProps.baselineSnapshots?.map((s) => s.state) || []),
    ...(compareProps.currentSnapshots?.map((s) => s.state) || []),
  ]);
  const compareDuration = Math.ceil(
    Math.max(compareStates.size, 1) * compareProps.secondsPerState * compareProps.fps
  );
  return (
    <>
      <Composition
        id="Snapshots"
        component={SnapshotsComposition}
        durationInFrames={durationInFrames}
        fps={props.fps}
        width={1280}
        height={720}
        schema={SnapshotsSchema}
        defaultProps={props}
      />
      <Composition
        id="Compare"
        component={CompareComposition}
        durationInFrames={compareDuration}
        fps={compareProps.fps}
        width={1920}
        height={1080}
        schema={CompareSchema}
        defaultProps={compareProps}
      />
      <Composition
        id="GeneratedMotionDemo"
        component={GeneratedMotionDemo}
        durationInFrames={generatedMotionDemoDurationInFrames}
        fps={generatedMotionDemoFramesPerSecond}
        width={1280}
        height={720}
        schema={GeneratedMotionDemoSchema}
        defaultProps={{ animated: true, isolated: false }}
      />
      <Composition
        id="GeneratedFullPenDemo"
        component={GeneratedFullPenDemo}
        durationInFrames={generatedFullPenDemoDurationInFrames}
        fps={generatedFullPenDemoFramesPerSecond}
        width={1280}
        height={960}
        schema={GeneratedFullPenDemoSchema}
        defaultProps={{ animated: false }}
      />
      <Composition
        id="GeneratedMinimapDemo"
        component={GeneratedMinimapDemo}
        durationInFrames={generatedMinimapDemoDurationInFrames}
        fps={generatedMinimapDemoFramesPerSecond}
        width={1280}
        height={720}
        schema={GeneratedMinimapDemoSchema}
        defaultProps={{ animated: false }}
      />
      <Composition
        id="GeneratedComponentDemo"
        component={GeneratedComponentDemo}
        durationInFrames={1}
        fps={30}
        width={512}
        height={512}
        schema={GeneratedComponentDemoSchema}
        defaultProps={{ componentId: "CharPortrait", isolated: true }}
      />
    </>
  );
};
