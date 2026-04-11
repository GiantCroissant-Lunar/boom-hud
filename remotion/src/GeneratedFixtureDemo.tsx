import React from "react";
import { AbsoluteFill } from "remotion";
import { z } from "zod";
import { FontReadyGate } from "./FontReadyGate";
import { CombatToastStackView } from "./generated/CombatToastStackView";
import { PartyStatusStripView } from "./generated/PartyStatusStripView";
import { QuestSidebarView } from "./generated/QuestSidebarView";

const fixtureIds = [
  "PartyStatusStrip",
  "QuestSidebar",
  "CombatToastStack",
] as const;

export const GeneratedFixtureDemoSchema = z.object({
  fixtureId: z.enum(fixtureIds).default("QuestSidebar"),
  isolated: z.boolean().default(true),
  canvasWidth: z.number().int().positive().default(840),
  canvasHeight: z.number().int().positive().default(1920),
  renderScale: z.number().positive().default(2),
});
export type GeneratedFixtureDemoSchema = z.infer<typeof GeneratedFixtureDemoSchema>;

const renderFixture = (
  fixtureId: GeneratedFixtureDemoSchema["fixtureId"],
): React.JSX.Element => {
  switch (fixtureId) {
    case "PartyStatusStrip":
      return <PartyStatusStripView />;
    case "QuestSidebar":
      return <QuestSidebarView />;
    case "CombatToastStack":
      return <CombatToastStackView />;
  }
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
