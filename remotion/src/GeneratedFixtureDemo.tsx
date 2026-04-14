import React from "react";
import { AbsoluteFill } from "remotion";
import { z } from "zod";
import { FontReadyGate } from "./FontReadyGate";

const generatedViewModules = import.meta.glob("./generated/*View.tsx", {
  eager: true,
});

const generatedFixtures = new Map<string, React.ComponentType>();

for (const [modulePath, moduleExports] of Object.entries(generatedViewModules)) {
  const match = /\/([^/]+)View\.tsx$/u.exec(modulePath);
  if (!match) {
    continue;
  }

  const fixtureId = match[1];
  const componentName = `${fixtureId}View`;
  const exportedComponent = (moduleExports as Record<string, unknown>)[componentName];
  if (typeof exportedComponent === "function") {
    generatedFixtures.set(fixtureId, exportedComponent as React.ComponentType);
  }
}

export const GeneratedFixtureDemoSchema = z.object({
  fixtureId: z.string().min(1).default("QuestSidebar"),
  isolated: z.boolean().default(true),
  canvasWidth: z.number().int().positive().default(840),
  canvasHeight: z.number().int().positive().default(1920),
  renderScale: z.number().positive().default(2),
});
export type GeneratedFixtureDemoSchema = z.infer<typeof GeneratedFixtureDemoSchema>;

const renderFixture = (
  fixtureId: string,
): React.JSX.Element => {
  const FixtureView = generatedFixtures.get(fixtureId);
  if (!FixtureView) {
    throw new Error(`Unknown generated fixture '${fixtureId}'.`);
  }

  return <FixtureView />;
};

export const GeneratedFixtureDemo: React.FC<GeneratedFixtureDemoSchema> = ({
  fixtureId = "QuestSidebar",
  isolated = true,
  canvasWidth = 840,
  canvasHeight = 1920,
  renderScale = 2,
}) => {
  const content = (
    <div
      style={{
        width: canvasWidth,
        height: canvasHeight,
        overflow: "hidden",
      }}
    >
      <div
        style={{
          display: "inline-flex",
          transform: `scale(${renderScale})`,
          transformOrigin: "top left",
        }}
      >
        {renderFixture(fixtureId)}
      </div>
    </div>
  );

  if (isolated) {
    return <FontReadyGate>{content}</FontReadyGate>;
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
