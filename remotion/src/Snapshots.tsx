import { AbsoluteFill, Img, useCurrentFrame, staticFile } from "remotion";
import { z } from "zod";

// Schema for input props (validated by Remotion)
export const SnapshotsSchema = z.object({
  snapshotsDir: z.string(),
  fps: z.number().default(30),
  secondsPerState: z.number().default(1.5),
  showTitleOverlay: z.boolean().default(true),
  snapshots: z.array(
    z.object({
      state: z.string(),
      path: z.string(),
    })
  ),
});

export type SnapshotsSchema = z.infer<typeof SnapshotsSchema>;

export const SnapshotsComposition: React.FC<SnapshotsSchema> = ({
  fps,
  secondsPerState,
  showTitleOverlay,
  snapshots,
}) => {
  const frame = useCurrentFrame();
  const framesPerState = Math.ceil(secondsPerState * fps);

  // Determine which snapshot to show
  const snapshotIndex = Math.min(
    Math.floor(frame / framesPerState),
    snapshots.length - 1
  );
  const currentSnapshot = snapshots[snapshotIndex];

  if (!currentSnapshot) {
    return (
      <AbsoluteFill
        style={{
          backgroundColor: "#1a1a2e",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          color: "#fff",
          fontSize: 48,
        }}
      >
        No snapshots provided
      </AbsoluteFill>
    );
  }

  return (
    <AbsoluteFill style={{ backgroundColor: "#1a1a2e" }}>
      {/* Snapshot image */}
      <Img
        src={staticFile(currentSnapshot.path)}
        style={{
          width: "100%",
          height: "100%",
          objectFit: "contain",
        }}
      />

      {/* Title overlay */}
      {showTitleOverlay && (
        <div
          style={{
            position: "absolute",
            bottom: 20,
            left: 20,
            right: 20,
            backgroundColor: "rgba(0, 0, 0, 0.7)",
            color: "#fff",
            padding: "12px 20px",
            borderRadius: 8,
            fontSize: 24,
            fontFamily: "system-ui, -apple-system, sans-serif",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <span>{currentSnapshot.state}</span>
          <span style={{ opacity: 0.6, fontSize: 18 }}>
            {snapshotIndex + 1} / {snapshots.length}
          </span>
        </div>
      )}
    </AbsoluteFill>
  );
};
