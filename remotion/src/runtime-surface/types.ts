// Runtime surface types are generated from schemas/json/runtime-surface.schema.json
// into types.generated.ts via quicktype. This file stays hand-written only for
// stable public aliases and renderer-local helper types.

import type {
  ActionElement as QuicktypeRuntimeActionDescriptor,
  BindingElement as QuicktypeRuntimeBinding,
  LayoutClass as QuicktypeRuntimeLayoutSpec,
  Root as QuicktypeRuntimeComponentNode,
  RuntimeSurfaceDocument as QuicktypeRuntimeSurfaceDocument,
  Something as QuicktypeJsonNode,
} from "./types.generated";

export type JsonNode = QuicktypeJsonNode;
export type JsonObject = { [key: string]: JsonNode };

export type RuntimeBinding = Omit<QuicktypeRuntimeBinding, "fallback" | "mode"> & {
  mode?: string;
  fallback?: JsonNode;
};

export type RuntimeActionDescriptor = Omit<QuicktypeRuntimeActionDescriptor, "payload"> & {
  payload?: JsonObject | null;
};

export type RuntimeValue = {
  literal?: JsonNode;
  binding?: RuntimeBinding | null;
};

export type RuntimeLayoutSpec = QuicktypeRuntimeLayoutSpec;

export type RuntimeComponentNode = Omit<
  QuicktypeRuntimeComponentNode,
  "actions" | "bindings" | "children" | "layout" | "properties"
> & {
  layout?: RuntimeLayoutSpec | null;
  properties?: Record<string, RuntimeValue>;
  bindings?: RuntimeBinding[];
  actions?: RuntimeActionDescriptor[];
  children?: RuntimeComponentNode[];
};

export type RuntimeSurfaceDocument = Omit<
  QuicktypeRuntimeSurfaceDocument,
  "dataModel" | "protocolVersion" | "root"
> & {
  protocolVersion: string;
  root: RuntimeComponentNode;
  dataModel?: JsonObject | null;
};

export type RuntimeSurfaceActionInvocation = {
  surfaceId: string;
  componentId: string;
  action: RuntimeActionDescriptor;
};
