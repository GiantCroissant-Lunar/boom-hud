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
  resolveValue,
} from "./resolve";
import type {
  RuntimeNodeGraphParameter,
  RuntimeNodeGraphPort,
  RuntimeNodeGraphWire,
} from "./node-graph";

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
const NODE_BG = "rgba(18, 23, 26, 0.94)";
const NODE_MUTED_BG = "rgba(255, 255, 255, 0.06)";

type GraphPort = RuntimeNodeGraphPort;

type GraphNodeData = {
  nodeId: string;
  label: string;
  typeId: string;
  category: string;
  summary: string;
  detail: string;
  isSideEffect: boolean;
  isExpensive: boolean;
  inputCount: number;
  outputCount: number;
  inputs: GraphPort[];
  outputs: GraphPort[];
  parameterLines: string[];
};

type GraphWireData = RuntimeNodeGraphWire & {
  fromSlot: number;
  toSlot: number;
};

type PositionedGraphNode = GraphNodeData & {
  x: number;
  y: number;
  rank: number;
  order: number;
};

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

const isJsonObject = (value: unknown): value is JsonObject =>
  value !== null && typeof value === "object" && !Array.isArray(value);

const readString = (source: JsonObject, key: string, fallback = ""): string => {
  const value = source[key];
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  return fallback;
};

const readNumberValue = (source: JsonObject, key: string, fallback = 0): number => {
  const value = source[key];
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string") {
    const parsed = Number.parseFloat(value);
    if (Number.isFinite(parsed)) return parsed;
  }
  return fallback;
};

const readBoolean = (source: JsonObject, key: string): boolean => source[key] === true;

const readStringArray = (value: JsonObject[string] | undefined): string[] =>
  Array.isArray(value)
    ? value
        .map((item) => {
          if (typeof item === "string") return item;
          if (typeof item === "number" || typeof item === "boolean") return String(item);
          return "";
        })
        .filter((item) => item !== "")
    : [];

const readParameterLines = (source: JsonObject): string[] => {
  const lines = readStringArray(source.parameterLines);
  if (lines.length > 0) {
    return lines;
  }

  const parameters = source.parameters;
  if (!Array.isArray(parameters)) {
    return [];
  }

  return parameters.flatMap((item): string[] => {
    if (!isJsonObject(item)) {
      return [];
    }

    const parameter = item as RuntimeNodeGraphParameter;
    const label = readString(item, "label", readString(item, "key"));
    if (label === "") {
      return [];
    }

    const rawValue = parameter.value;
    const value = rawValue === null || rawValue === undefined
      ? ""
      : typeof rawValue === "object"
        ? JSON.stringify(rawValue)
        : String(rawValue);
    return [`${label}: ${value}`];
  });
};

const readPorts = (value: JsonObject[string] | undefined, count: number, prefix: string): GraphPort[] => {
  if (Array.isArray(value)) {
    return value.map((item, index) => {
      if (isJsonObject(item)) {
        return {
          portId: readString(item, "portId", `${prefix}${index}`),
          label: readString(item, "label", `${prefix}${index}`),
          kindHint: readString(item, "kindHint", "value"),
          required: readBoolean(item, "required"),
        };
      }
      return {
        portId: `${prefix}${index}`,
        label: typeof item === "string" ? item : `${prefix}${index}`,
        kindHint: "value",
        required: false,
      };
    });
  }

  return Array.from({ length: Math.max(0, count) }, (_, index) => ({
    portId: `${prefix}${index}`,
    label: `${prefix}${index}`,
    kindHint: "value",
    required: false,
  }));
};

const fallbackNodeId = (label: string, index: number): string => {
  const normalized = label.trim().toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "");
  return normalized === "" ? `node_${index}` : normalized;
};

const readGraphNodes = (value: JsonObject[string] | undefined): GraphNodeData[] => {
  if (!Array.isArray(value)) return [];

  return value.map((item, index) => {
    if (typeof item === "string") {
      return {
        nodeId: fallbackNodeId(item, index),
        label: item,
        typeId: item,
        category: "node",
        summary: "",
        detail: "",
        isSideEffect: false,
        isExpensive: false,
        inputCount: 1,
        outputCount: 1,
        inputs: [{ portId: "in", label: "in", kindHint: "value", required: false }],
        outputs: [{ portId: "out", label: "out", kindHint: "value", required: false }],
        parameterLines: [],
      };
    }

    if (!isJsonObject(item)) {
      const label = `node ${index + 1}`;
      return {
        nodeId: `node_${index}`,
        label,
        typeId: label,
        category: "node",
        summary: "",
        detail: "",
        isSideEffect: false,
        isExpensive: false,
        inputCount: 1,
        outputCount: 1,
        inputs: [{ portId: "in", label: "in", kindHint: "value", required: false }],
        outputs: [{ portId: "out", label: "out", kindHint: "value", required: false }],
        parameterLines: [],
      };
    }

    const nodeId = readString(item, "nodeId", readString(item, "id", `node_${index}`));
    const inputCount = readNumberValue(item, "inputCount", 0);
    const outputCount = readNumberValue(item, "outputCount", 0);
    const inputs = readPorts(item.inputs, inputCount, "in");
    const outputs = readPorts(item.outputs, outputCount, "out");

    return {
      nodeId,
      label: readString(item, "label", readString(item, "typeId", nodeId)),
      typeId: readString(item, "typeId", nodeId),
      category: readString(item, "category", "node"),
      summary: readString(item, "summary"),
      detail: readString(item, "detail"),
      isSideEffect: readBoolean(item, "isSideEffect"),
      isExpensive: readBoolean(item, "isExpensive"),
      inputCount: inputs.length,
      outputCount: outputs.length,
      inputs,
      outputs,
      parameterLines: readParameterLines(item),
    };
  });
};

const readGraphWires = (value: JsonObject[string] | undefined): GraphWireData[] => {
  if (!Array.isArray(value)) return [];

  return value.flatMap((item) => {
    if (!isJsonObject(item)) return [];
    const fromNodeId = readString(item, "fromNodeId");
    const toNodeId = readString(item, "toNodeId");
    if (fromNodeId === "" || toNodeId === "") return [];

    return [{
      fromNodeId,
      fromSlot: readNumberValue(item, "fromSlot", 0),
      toNodeId,
      toSlot: readNumberValue(item, "toSlot", 0),
      kindHint: readString(item, "kindHint", "value"),
    }];
  });
};

const categoryColor = (category: string): string => {
  switch (category) {
    case "source": return "#79b8ff";
    case "geometry": return "#56d6a3";
    case "geosphere": return "#c7a85e";
    case "truth": return "#f0a15a";
    case "truthstream": return "#ff7b68";
    case "materialize": return "#9ab9ff";
    case "cartography": return "#87d96f";
    case "ecs": return "#cf95ff";
    case "timeline": return "#dbc96b";
    default: return "#a8b0bd";
  }
};

const shorten = (value: string, maxLength: number): string => {
  const trimmed = value.trim();
  return trimmed.length <= maxLength ? trimmed : `${trimmed.slice(0, Math.max(1, maxLength - 1))}.`;
};

const layoutGraph = (nodes: GraphNodeData[], wires: GraphWireData[]): PositionedGraphNode[] => {
  if (nodes.length === 0) return [];

  const ids = new Set(nodes.map((node) => node.nodeId));
  const usableWires = wires.filter((wire) => ids.has(wire.fromNodeId) && ids.has(wire.toNodeId));

  if (usableWires.length === 0) {
    const columns = Math.max(1, Math.ceil(Math.sqrt(nodes.length)));
    return nodes.map((node, index) => ({
      ...node,
      rank: index % columns,
      order: Math.floor(index / columns),
      x: ((index % columns) + 0.5) / columns * 100,
      y: (Math.floor(index / columns) + 0.5) / Math.ceil(nodes.length / columns) * 100,
    }));
  }

  const ranks = new Map<string, number>();
  const connected = new Set<string>();
  for (const node of nodes) ranks.set(node.nodeId, 0);
  for (const wire of usableWires) {
    connected.add(wire.fromNodeId);
    connected.add(wire.toNodeId);
  }

  for (let pass = 0; pass < nodes.length; pass++) {
    let changed = false;
    for (const wire of usableWires) {
      const fromRank = ranks.get(wire.fromNodeId) ?? 0;
      const toRank = ranks.get(wire.toNodeId) ?? 0;
      if (toRank <= fromRank) {
        ranks.set(wire.toNodeId, fromRank + 1);
        changed = true;
      }
    }
    if (!changed) break;
  }

  const maxConnectedRank = Math.max(0, ...Array.from(ranks.entries())
    .filter(([nodeId]) => connected.has(nodeId))
    .map(([, rank]) => rank));
  const isolated = nodes.filter((node) => !connected.has(node.nodeId));
  const extraColumns = Math.min(3, Math.max(1, Math.ceil(Math.sqrt(isolated.length))));
  isolated.forEach((node, index) => {
    ranks.set(node.nodeId, maxConnectedRank + 1 + (index % extraColumns));
  });

  const groups = new Map<number, GraphNodeData[]>();
  for (const node of nodes) {
    const rank = ranks.get(node.nodeId) ?? 0;
    const group = groups.get(rank) ?? [];
    group.push(node);
    groups.set(rank, group);
  }

  const sortedRanks = Array.from(groups.keys()).sort((a, b) => a - b);
  const rankIndex = new Map(sortedRanks.map((rank, index) => [rank, index]));
  const rankCount = Math.max(1, sortedRanks.length);
  const maxRows = Math.max(1, ...Array.from(groups.values()).map((group) => group.length));

  return nodes.map((node) => {
    const rank = ranks.get(node.nodeId) ?? 0;
    const group = groups.get(rank) ?? [node];
    const order = Math.max(0, group.findIndex((entry) => entry.nodeId === node.nodeId));
    const displayRank = rankIndex.get(rank) ?? 0;
    return {
      ...node,
      rank: displayRank,
      order,
      x: ((displayRank + 0.5) / rankCount) * 100,
      y: ((order + 0.5) / maxRows) * 100,
    };
  });
};

const RuntimeNodeGraph: React.FC<{
  node: RuntimeComponentNode;
  document: RuntimeSurfaceDocument;
  disabledStyle: React.CSSProperties;
  title?: string;
}> = ({ node, document, disabledStyle, title }) => {
  const dataModel = document.dataModel;
  const itemValue = resolveValue(node.properties?.items ?? node.properties?.nodes, dataModel);
  const wireValue = resolveValue(node.properties?.wires, dataModel);
  const graphNodes = readGraphNodes(itemValue);
  const graphWires = readGraphWires(wireValue);
  const simpleNodes = graphNodes.length === 0
    ? resolveStringList(node.properties, "nodes", dataModel)
    : [];
  const positionedNodes = layoutGraph(graphNodes, graphWires);
  const byId = new Map(positionedNodes.map((entry) => [entry.nodeId, entry]));
  const rankCount = Math.max(1, ...positionedNodes.map((entry) => entry.rank + 1));
  const maxRows = Math.max(1, ...positionedNodes.map((entry) => entry.order + 1));
  const cardWidth = Math.min(28, Math.max(10, 92 / rankCount));
  const graphHeight = Math.max(node.layout?.minHeight ?? 360, maxRows * 86 + 40);

  return (
    <div
      title={title}
      style={{
        border: `1px solid ${BORDER_COLOR}`,
        borderRadius: 6,
        padding: 10,
        color: MUTED_COLOR,
        background: "rgba(8, 10, 12, 0.36)",
        position: "relative",
        overflow: "hidden",
        ...layoutStyle(node.layout),
        minHeight: graphHeight,
        ...disabledStyle,
      }}
    >
      {positionedNodes.length === 0 ? (
        <div style={{ color: TEXT_COLOR }}>
          {simpleNodes.length > 0 ? "nodeGraph" : "No graph nodes"}
        </div>
      ) : null}
      {simpleNodes.map((name, index) => (
        <div key={`${node.id}-node-${index}`} style={{ padding: "1px 6px" }}>
          {name}
        </div>
      ))}
      {positionedNodes.length > 0 ? (
        <>
          <svg
            viewBox="0 0 1000 1000"
            preserveAspectRatio="none"
            style={{
              position: "absolute",
              inset: 0,
              width: "100%",
              height: "100%",
              pointerEvents: "none",
            }}
          >
            <defs>
              <marker
                id={`${node.id}-arrow`}
                markerWidth="8"
                markerHeight="8"
                refX="7"
                refY="4"
                orient="auto"
              >
                <path d="M 0 0 L 8 4 L 0 8 z" fill="rgba(139, 204, 255, 0.68)" />
              </marker>
            </defs>
            {graphWires.map((wire, index) => {
              const from = byId.get(wire.fromNodeId);
              const to = byId.get(wire.toNodeId);
              if (!from || !to) return null;
              const x1 = from.x * 10 + cardWidth * 4;
              const y1 = from.y * 10;
              const x2 = to.x * 10 - cardWidth * 4;
              const y2 = to.y * 10;
              const bend = Math.max(48, Math.abs(x2 - x1) * 0.42);
              return (
                <path
                  key={`${wire.fromNodeId}-${wire.fromSlot}-${wire.toNodeId}-${wire.toSlot}-${index}`}
                  d={`M ${x1} ${y1} C ${x1 + bend} ${y1}, ${x2 - bend} ${y2}, ${x2} ${y2}`}
                  fill="none"
                  stroke="rgba(139, 204, 255, 0.62)"
                  strokeWidth={3}
                  markerEnd={`url(#${node.id}-arrow)`}
                />
              );
            })}
          </svg>
          {positionedNodes.map((entry) => {
            const color = categoryColor(entry.category);
            const facts = [
              entry.category,
              entry.isExpensive ? "expensive" : "",
              entry.isSideEffect ? "side-effect" : "",
            ].filter(Boolean).join(" | ");
            return (
              <div
                key={entry.nodeId}
                style={{
                  position: "absolute",
                  left: `${entry.x}%`,
                  top: `${entry.y}%`,
                  width: `${cardWidth}%`,
                  minWidth: 86,
                  maxWidth: 220,
                  transform: "translate(-50%, -50%)",
                  background: NODE_BG,
                  border: `1px solid rgba(255, 255, 255, 0.18)`,
                  borderLeft: `5px solid ${color}`,
                  borderRadius: 6,
                  color: TEXT_COLOR,
                  boxShadow: "0 8px 22px rgba(0, 0, 0, 0.30)",
                  overflow: "hidden",
                }}
              >
                <div
                  style={{
                    background: color,
                    color: "#111413",
                    fontWeight: 700,
                    padding: "4px 7px",
                    fontSize: 11,
                    lineHeight: 1.15,
                    minHeight: 24,
                  }}
                >
                  {shorten(entry.label, rankCount > 7 ? 14 : 22)}
                </div>
                <div style={{ padding: "6px 7px", display: "flex", flexDirection: "column", gap: 4 }}>
                  <div style={{ color: MUTED_COLOR, fontSize: 9, lineHeight: 1.2 }}>
                    {shorten(facts || entry.typeId, rankCount > 7 ? 20 : 34)}
                  </div>
                  {entry.summary !== "" ? (
                    <div style={{ fontSize: 9, lineHeight: 1.25 }}>
                      {shorten(entry.summary, rankCount > 7 ? 34 : 58)}
                    </div>
                  ) : null}
                  <div style={{ display: "flex", gap: 4, fontSize: 9, color: MUTED_COLOR }}>
                    <span style={{ background: NODE_MUTED_BG, padding: "1px 4px", borderRadius: 3 }}>
                      in {entry.inputs.length}
                    </span>
                    <span style={{ background: NODE_MUTED_BG, padding: "1px 4px", borderRadius: 3 }}>
                      out {entry.outputs.length}
                    </span>
                  </div>
                </div>
              </div>
            );
          })}
        </>
      ) : null}
    </div>
  );
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
      return (
        <RuntimeNodeGraph
          node={node}
          document={document}
          disabledStyle={disabledStyle}
          title={title}
        />
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
