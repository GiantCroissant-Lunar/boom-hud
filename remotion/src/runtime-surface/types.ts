// TypeScript mirror of the BoomHud RUNTIME surface wire contract (protocol 0.1,
// catalog "boomhud.runtime.basic.v1"). Source of truth:
// dotnet/src/BoomHud.Abstractions/Runtime/RuntimeSurfaceContracts.cs
// JSON property names below are exactly the JsonPropertyName values on the C# records.

export type JsonNode =
  | null
  | boolean
  | number
  | string
  | JsonNode[]
  | { [key: string]: JsonNode };

export type JsonObject = { [key: string]: JsonNode };

export type RuntimeSurfaceDocument = {
  protocolVersion: string; // "0.1"
  surfaceId: string;
  catalogId: string; // "boomhud.runtime.basic.v1"
  revision: number;
  root: RuntimeComponentNode;
  dataModel?: JsonObject | null;
  metadata?: Record<string, string>;
};

export type RuntimeComponentNode = {
  id: string;
  type: string; // badge|button|container|label|list|nodeGraph|panel|progressBar|spacer
  layout?: RuntimeLayoutSpec | null;
  properties?: Record<string, RuntimeValue>;
  bindings?: RuntimeBinding[];
  actions?: RuntimeActionDescriptor[];
  children?: RuntimeComponentNode[];
  metadata?: Record<string, string>;
};

export type RuntimeLayoutSpec = {
  type?: string; // vertical|horizontal|grid|absolute (default vertical)
  width?: number | null;
  height?: number | null;
  minWidth?: number | null;
  minHeight?: number | null;
  maxWidth?: number | null;
  maxHeight?: number | null;
  gap?: number | null;
  padding?: number | null;
  align?: string | null;
  justify?: string | null;
};

export type RuntimeValue = {
  literal?: JsonNode;
  binding?: RuntimeBinding;
};

export type RuntimeBinding = {
  property?: string | null;
  path: string; // JSON Pointer into dataModel, e.g. "/items/0/name"
  mode?: string; // oneWay|twoWay|oneTime (this renderer is read-only)
  format?: string | null; // .NET-style "{0}" pattern
  fallback?: JsonNode;
};

export type RuntimeActionDescriptor = {
  event: string;
  command: string;
  payload?: JsonObject | null;
};

export type RuntimeSurfaceActionInvocation = {
  surfaceId: string;
  componentId: string;
  action: RuntimeActionDescriptor;
};
