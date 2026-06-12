// Public aliases for the quicktype-generated nodeGraph dataModel.graph contract.

import type {
  InputElement as QuicktypeRuntimeNodeGraphPort,
  NodeElement as QuicktypeRuntimeNodeGraphNode,
  ParameterElement as QuicktypeRuntimeNodeGraphParameter,
  RuntimeNodeGraphSnapshot,
  Something as QuicktypeRuntimeNodeGraphJsonNode,
  WireElement as QuicktypeRuntimeNodeGraphWire,
} from "./node-graph.generated";

export type RuntimeNodeGraphJsonNode = QuicktypeRuntimeNodeGraphJsonNode;
export type RuntimeNodeGraphNode = QuicktypeRuntimeNodeGraphNode;
export type RuntimeNodeGraphParameter = QuicktypeRuntimeNodeGraphParameter;
export type RuntimeNodeGraphPort = QuicktypeRuntimeNodeGraphPort;
export type RuntimeNodeGraphWire = QuicktypeRuntimeNodeGraphWire;
export type { RuntimeNodeGraphSnapshot };
