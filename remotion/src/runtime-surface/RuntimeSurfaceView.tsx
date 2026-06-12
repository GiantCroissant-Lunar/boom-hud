// React renderer for BoomHud RUNTIME surface documents (protocol 0.1, catalog
// "boomhud.runtime.basic.v1"). Mirrors the per-component semantics of
// dotnet/src/BoomHud.Godot.Runtime/RuntimeSurfaceRenderer.cs so the same document
// renders equivalently on Godot and React. Read-only: bindings resolve against the
// document's dataModel; button actions surface through the onAction callback only.

import React from "react";
import type {
  JsonObject,
  RuntimeComponentNode,
  RuntimeLayoutSpec,
  RuntimeSurfaceActionInvocation,
  RuntimeSurfaceDocument,
} from "./types";
import {
  resolveBoolean,
  resolveNumber,
  resolveStringList,
  resolveText,
} from "./resolve";

export type RuntimeSurfaceViewProps = {
  document: RuntimeSurfaceDocument;
  onAction?: (invocation: RuntimeSurfaceActionInvocation) => void;
  style?: React.CSSProperties;
};

const TEXT_COLOR = "#edf2ef";
const MUTED_COLOR = "#9aa6a0";
const BORDER_COLOR = "rgba(255, 255, 255, 0.18)";
const PANEL_BG = "rgba(20, 23, 22, 0.85)";
const ACCENT = "#4a9eff";

const layoutStyle = (layout: RuntimeLayoutSpec | null | undefined): React.CSSProperties => {
  const style: React.CSSProperties = {};
  if (!layout) {
    return style;
  }
  if (layout.width !== null && layout.width !== undefined) style.width = layout.width;
  if (layout.height !== null && layout.height !== undefined) style.height = layout.height;
  if (layout.minWidth !== null && layout.minWidth !== undefined) style.minWidth = layout.minWidth;
  if (layout.minHeight !== null && layout.minHeight !== undefined) style.minHeight = layout.minHeight;
  if (layout.maxWidth !== null && layout.maxWidth !== undefined) style.maxWidth = layout.maxWidth;
  if (layout.maxHeight !== null && layout.maxHeight !== undefined) style.maxHeight = layout.maxHeight;
  if (layout.padding !== null && layout.padding !== undefined) style.padding = layout.padding;
  return style;
};

const containerStyle = (layout: RuntimeLayoutSpec | null | undefined): React.CSSProperties => {
  const style = layoutStyle(layout);
  const type = layout?.type ?? "vertical";
  if (type === "grid") {
    style.display = "grid";
    style.gridTemplateColumns = "repeat(auto-fit, minmax(120px, 1fr))";
  } else {
    style.display = "flex";
    style.flexDirection = type === "horizontal" ? "row" : "column";
  }
  if (layout?.gap !== null && layout?.gap !== undefined) {
    style.gap = layout.gap;
  }
  if (layout?.align) {
    style.alignItems = layout.align;
  }
  if (layout?.justify) {
    style.justifyContent = layout.justify;
  }
  return style;
};

type NodeProps = {
  node: RuntimeComponentNode;
  document: RuntimeSurfaceDocument;
  onAction?: (invocation: RuntimeSurfaceActionInvocation) => void;
};

const NodeChildren: React.FC<NodeProps> = ({ node, document, onAction }) => (
  <>
    {(node.children ?? []).map((child, index) => (
      <RuntimeNode
        key={`${child.id}-${index}`}
        node={child}
        document={document}
        onAction={onAction}
      />
    ))}
  </>
);

const RuntimeNode: React.FC<NodeProps> = ({ node, document, onAction }) => {
  const dataModel: JsonObject | null | undefined = document.dataModel;
  const visible = resolveBoolean(node.properties, "visible", dataModel, true);
  if (!visible) {
    return null;
  }

  const tooltip = resolveText(node.properties, "tooltip", dataModel);
  const enabled = resolveBoolean(node.properties, "enabled", dataModel, true);
  const disabledStyle: React.CSSProperties = enabled
    ? {}
    : { pointerEvents: "none", opacity: 0.6 };
  const title = tooltip === "" ? undefined : tooltip;

  switch (node.type) {
    case "container": {
      return (
        <div title={title} style={{ ...containerStyle(node.layout), ...disabledStyle }}>
          <NodeChildren node={node} document={document} onAction={onAction} />
        </div>
      );
    }

    case "panel": {
      const panelTitle = resolveText(node.properties, "title", dataModel);
      return (
        <div
          title={title}
          style={{
            border: `1px solid ${BORDER_COLOR}`,
            borderRadius: 6,
            padding: 10,
            background: PANEL_BG,
            display: "flex",
            flexDirection: "column",
            gap: node.layout?.gap ?? 6,
            ...layoutStyle(node.layout),
            ...disabledStyle,
          }}
        >
          {panelTitle !== "" ? (
            <div style={{ fontWeight: "bold", color: TEXT_COLOR }}>{panelTitle}</div>
          ) : null}
          <NodeChildren node={node} document={document} onAction={onAction} />
        </div>
      );
    }

    case "label": {
      return (
        <div title={title} style={{ color: TEXT_COLOR, ...layoutStyle(node.layout), ...disabledStyle }}>
          {resolveText(node.properties, "text", dataModel)}
        </div>
      );
    }

    case "badge": {
      return (
        <span
          title={title}
          style={{
            display: "inline-block",
            borderRadius: 999,
            padding: "1px 8px",
            background: "rgba(255, 255, 255, 0.12)",
            color: TEXT_COLOR,
            fontSize: "0.85em",
            ...disabledStyle,
          }}
        >
          {resolveText(node.properties, "text", dataModel)}
        </span>
      );
    }

    case "button": {
      const pressedActions = (node.actions ?? []).filter(
        (action) => action.event.toLowerCase() === "pressed"
      );
      return (
        <button
          type="button"
          title={title}
          disabled={!enabled}
          onClick={() => {
            for (const action of pressedActions) {
              onAction?.({
                surfaceId: document.surfaceId,
                componentId: node.id,
                action,
              });
            }
          }}
          style={{
            background: "rgba(255, 255, 255, 0.08)",
            color: TEXT_COLOR,
            border: `1px solid ${BORDER_COLOR}`,
            borderRadius: 4,
            padding: "4px 10px",
            cursor: enabled ? "pointer" : "default",
            font: "inherit",
            ...layoutStyle(node.layout),
          }}
        >
          {resolveText(node.properties, "text", dataModel)}
        </button>
      );
    }

    case "list": {
      const items = resolveStringList(node.properties, "items", dataModel);
      const emptyText = resolveText(node.properties, "emptyText", dataModel);
      const selectedItem = resolveText(node.properties, "selectedItem", dataModel);
      const rows = items.length === 0 && emptyText !== "" ? [emptyText] : items;
      const emptyOnly = items.length === 0;
      return (
        <div
          title={title}
          style={{
            display: "flex",
            flexDirection: "column",
            ...layoutStyle(node.layout),
            ...disabledStyle,
          }}
        >
          {rows.map((row, index) => {
            const selected = !emptyOnly && selectedItem !== "" && row === selectedItem;
            return (
              <div
                key={`${node.id}-row-${index}`}
                style={{
                  padding: "2px 6px",
                  color: emptyOnly ? MUTED_COLOR : TEXT_COLOR,
                  background: selected ? "rgba(74, 158, 255, 0.25)" : undefined,
                }}
              >
                {row}
              </div>
            );
          })}
        </div>
      );
    }

    case "progressBar": {
      const minimum = resolveNumber(node.properties, "minimum", dataModel, 0);
      const maximum = resolveNumber(node.properties, "maximum", dataModel, 100);
      const value = resolveNumber(node.properties, "value", dataModel, 0);
      const range = maximum - minimum;
      const fraction = range > 0 ? Math.min(1, Math.max(0, (value - minimum) / range)) : 0;
      return (
        <div
          title={title}
          style={{
            height: 10,
            borderRadius: 5,
            background: "rgba(255, 255, 255, 0.1)",
            overflow: "hidden",
            ...layoutStyle(node.layout),
            ...disabledStyle,
          }}
        >
          <div
            style={{
              width: `${fraction * 100}%`,
              height: "100%",
              background: ACCENT,
            }}
          />
        </div>
      );
    }

    case "spacer": {
      return <div style={{ flexGrow: 1, ...layoutStyle(node.layout) }} />;
    }

    case "nodeGraph": {
      const nodes = resolveStringList(node.properties, "nodes", dataModel);
      return (
        <div
          title={title}
          style={{
            border: `1px dashed ${BORDER_COLOR}`,
            borderRadius: 6,
            padding: 10,
            color: MUTED_COLOR,
            display: "flex",
            flexDirection: "column",
            gap: 4,
            ...layoutStyle(node.layout),
            ...disabledStyle,
          }}
        >
          <div style={{ color: TEXT_COLOR }}>nodeGraph: {node.id}</div>
          {nodes.map((name, index) => (
            <div key={`${node.id}-node-${index}`} style={{ padding: "1px 6px" }}>
              {name}
            </div>
          ))}
        </div>
      );
    }

    default: {
      // Forward-compatible: unknown component types render their children.
      return (
        <div title={title} style={{ ...layoutStyle(node.layout), ...disabledStyle }}>
          <NodeChildren node={node} document={document} onAction={onAction} />
        </div>
      );
    }
  }
};

export const RuntimeSurfaceView: React.FC<RuntimeSurfaceViewProps> = ({
  document,
  onAction,
  style,
}) => (
  <div
    style={{
      color: TEXT_COLOR,
      fontFamily: "monospace",
      fontSize: 14,
      ...style,
    }}
  >
    <RuntimeNode node={document.root} document={document} onAction={onAction} />
  </div>
);
