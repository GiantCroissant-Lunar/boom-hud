import { AbsoluteFill, Img, useCurrentFrame, staticFile } from "remotion";
import { z } from "zod";

// Schema for comparison input props
export const CompareSchema = z.object({
  fps: z.number().default(30),
  secondsPerState: z.number().default(1.5),
  showTitleOverlay: z.boolean().default(true),
  baselineSnapshots: z.array(
    z.object({
      state: z.string(),
      path: z.string(),
    })
  ),
  currentSnapshots: z.array(
    z.object({
      state: z.string(),
      path: z.string(),
    })
  ),
});

export type CompareSchema = z.infer<typeof CompareSchema>;

interface FrameData {
  state: string;
  path: string | null;
}

export const CompareComposition: React.FC<CompareSchema> = ({
  fps,
  secondsPerState,
  showTitleOverlay,
  baselineSnapshots,
  currentSnapshots,
}) => {
  const frame = useCurrentFrame();
  const framesPerState = Math.ceil(secondsPerState * fps);

  // Build unified state list from both sets
  const allStates = new Set([
    ...baselineSnapshots.map((s) => s.state),
    ...currentSnapshots.map((s) => s.state),
  ]);
  const stateList = Array.from(allStates);

  // Determine which state to show
  const stateIndex = Math.min(
    Math.floor(frame / framesPerState),
    stateList.length - 1
  );
  const currentState = stateList[stateIndex];

  // Find frames for this state
  const baselineFrame = baselineSnapshots.find((s) => s.state === currentState);
  const currentFrame = currentSnapshots.find((s) => s.state === currentState);

  return (
    <AbsoluteFill style={{ backgroundColor: "#0a0a14" }}>
      {/* Side-by-side container */}
      <div
        style={{
          display: "flex",
          width: "100%",
          height: "100%",
        }}
      >
        {/* Baseline (left) */}
        <div
          style={{
            flex: 1,
            display: "flex",
            flexDirection: "column",
            borderRight: "2px solid #333",
          }}
        >
          <FramePanel
            label="BASELINE"
            frame={baselineFrame ? { state: baselineFrame.state, path: baselineFrame.path } : null}
            labelColor="#6b8afd"
          />
        </div>

        {/* Current (right) */}
        <div
          style={{
            flex: 1,
            display: "flex",
            flexDirection: "column",
          }}
        >
          <FramePanel
            label="CURRENT"
            frame={currentFrame ? { state: currentFrame.state, path: currentFrame.path } : null}
            labelColor="#4ade80"
          />
        </div>
      </div>

      {/* Bottom overlay with state name */}
      {showTitleOverlay && (
        <div
          style={{
            position: "absolute",
            bottom: 0,
            left: 0,
            right: 0,
            backgroundColor: "rgba(0, 0, 0, 0.85)",
            color: "#fff",
            padding: "16px 24px",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            fontFamily: "system-ui, -apple-system, sans-serif",
          }}
        >
          <span style={{ fontSize: 28, fontWeight: 600 }}>{currentState}</span>
          <span style={{ fontSize: 18, opacity: 0.6 }}>
            {stateIndex + 1} / {stateList.length}
          </span>
        </div>
      )}
    </AbsoluteFill>
  );
};

interface FramePanelProps {
  label: string;
  frame: FrameData | null;
  labelColor: string;
}

const FramePanel: React.FC<FramePanelProps> = ({ label, frame, labelColor }) => {
  return (
    <div
      style={{
        flex: 1,
        display: "flex",
        flexDirection: "column",
        position: "relative",
      }}
    >
      {/* Label header */}
      <div
        style={{
          backgroundColor: "rgba(0, 0, 0, 0.7)",
          color: labelColor,
          padding: "8px 16px",
          fontSize: 14,
          fontWeight: 700,
          letterSpacing: "0.1em",
          textAlign: "center",
          fontFamily: "system-ui, -apple-system, sans-serif",
        }}
      >
        {label}
      </div>

      {/* Frame content */}
      <div
        style={{
          flex: 1,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          padding: 8,
        }}
      >
        {frame?.path ? (
          <Img
            src={staticFile(frame.path)}
            style={{
              maxWidth: "100%",
              maxHeight: "100%",
              objectFit: "contain",
            }}
          />
        ) : (
          <MissingPlaceholder label={label} />
        )}
      </div>
    </div>
  );
};

const MissingPlaceholder: React.FC<{ label: string }> = ({ label }) => (
  <div
    style={{
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      justifyContent: "center",
      color: "#666",
      fontFamily: "system-ui, -apple-system, sans-serif",
    }}
  >
    <div
      style={{
        fontSize: 48,
        marginBottom: 16,
        opacity: 0.5,
      }}
    >
      ∅
    </div>
    <div style={{ fontSize: 18, fontWeight: 500 }}>Missing {label.toLowerCase()}</div>
    <div style={{ fontSize: 14, opacity: 0.6, marginTop: 4 }}>
      No snapshot for this state
    </div>
  </div>
);
