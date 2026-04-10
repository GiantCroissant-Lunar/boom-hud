import React from "react";
import { AbsoluteFill } from "remotion";
import { z } from "zod";
import { ActionButtonView } from "./generated/ActionButtonView";
import { CharPortraitView } from "./generated/CharPortraitView";
import { MessageLogView } from "./generated/MessageLogView";
import { MinimapView } from "./generated/MinimapView";
import { StatBarView } from "./generated/StatBarView";
import { StatusIconView } from "./generated/StatusIconView";

const componentIds = [
  "ActionButton",
  "StatusIcon",
  "StatBar",
  "MessageLog",
  "Minimap",
  "CharPortrait",
] as const;

export const GeneratedComponentDemoSchema = z.object({
  componentId: z.enum(componentIds).default("CharPortrait"),
  isolated: z.boolean().default(true),
});
export type GeneratedComponentDemoSchema = z.infer<typeof GeneratedComponentDemoSchema>;

const renderComponent = (
  componentId: GeneratedComponentDemoSchema["componentId"],
): React.JSX.Element => {
  switch (componentId) {
    case "ActionButton":
      return <ActionButtonView />;
    case "StatusIcon":
      return <StatusIconView />;
    case "StatBar":
      return <StatBarView />;
    case "MessageLog":
      return <MessageLogView />;
    case "Minimap":
      return <MinimapView />;
    case "CharPortrait":
      return <CharPortraitView />;
  }
};

export const GeneratedComponentDemo: React.FC<GeneratedComponentDemoSchema> = ({
  componentId = "CharPortrait",
  isolated = true,
}) => {
  const content = <div style={{ display: "inline-flex" }}>{renderComponent(componentId)}</div>;

  if (isolated) {
    return content;
  }

  return (
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
  );
};
