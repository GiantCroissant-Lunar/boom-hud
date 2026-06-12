// Pure value-resolution semantics for runtime surface documents, ported from
// dotnet/src/BoomHud.Godot.Runtime/RuntimeValueResolver.cs (+ the JSON Pointer
// resolver it delegates to). Keep behavior identical so the same document renders
// the same on Godot and React.

import type { JsonNode, JsonObject, RuntimeValue } from "./types";

export type PointerResult = { found: boolean; value: JsonNode | undefined };

const unescapeSegment = (segment: string): string =>
  segment.replace(/~1/g, "/").replace(/~0/g, "~");

export const resolvePointer = (
  dataModel: JsonObject | null | undefined,
  path: string
): PointerResult => {
  if (dataModel === null || dataModel === undefined) {
    return { found: false, value: undefined };
  }

  if (path === "" || path === "/") {
    return { found: true, value: dataModel };
  }

  const segments = (path.startsWith("/") ? path.slice(1) : path)
    .split("/")
    .map(unescapeSegment);

  let current: JsonNode = dataModel;
  for (const segment of segments) {
    if (Array.isArray(current)) {
      const index = Number(segment);
      if (!Number.isInteger(index) || index < 0 || index >= current.length) {
        return { found: false, value: undefined };
      }
      current = current[index];
    } else if (current !== null && typeof current === "object") {
      if (!Object.prototype.hasOwnProperty.call(current, segment)) {
        return { found: false, value: undefined };
      }
      current = (current as JsonObject)[segment];
    } else {
      return { found: false, value: undefined };
    }
  }

  return { found: true, value: current };
};

export const resolveValue = (
  value: RuntimeValue | undefined,
  dataModel: JsonObject | null | undefined
): JsonNode | undefined => {
  if (value === undefined) {
    return undefined;
  }
  if (value.literal !== null && value.literal !== undefined) {
    return value.literal;
  }
  if (value.binding) {
    const result = resolvePointer(dataModel, value.binding.path);
    return result.found ? result.value : value.binding.fallback;
  }
  return undefined;
};

export const toText = (
  node: JsonNode | undefined,
  fallback: string,
  format?: string | null
): string => {
  if (node === null || node === undefined) {
    return fallback;
  }

  let scalar: string;
  if (typeof node === "string") {
    scalar = node;
  } else if (typeof node === "number" || typeof node === "boolean") {
    scalar = String(node);
  } else {
    scalar = JSON.stringify(node);
  }

  if (format !== null && format !== undefined) {
    return format.replace("{0}", scalar);
  }

  return scalar;
};

export const toNumber = (node: JsonNode | undefined, fallback: number): number => {
  if (typeof node === "number") {
    return node;
  }
  if (typeof node === "string") {
    const parsed = Number.parseFloat(node);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }
  return fallback;
};

export const toBoolean = (node: JsonNode | undefined, fallback: boolean): boolean => {
  if (typeof node === "boolean") {
    return node;
  }
  if (typeof node === "string") {
    if (node.toLowerCase() === "true") {
      return true;
    }
    if (node.toLowerCase() === "false") {
      return false;
    }
  }
  return fallback;
};

export const resolveText = (
  properties: Record<string, RuntimeValue> | undefined,
  name: string,
  dataModel: JsonObject | null | undefined,
  fallback = ""
): string => {
  const value = properties?.[name];
  if (value === undefined) {
    return fallback;
  }
  return toText(resolveValue(value, dataModel), fallback, value.binding?.format);
};

export const resolveNumber = (
  properties: Record<string, RuntimeValue> | undefined,
  name: string,
  dataModel: JsonObject | null | undefined,
  fallback = 0
): number => {
  const value = properties?.[name];
  if (value === undefined) {
    return fallback;
  }
  return toNumber(resolveValue(value, dataModel), fallback);
};

export const resolveBoolean = (
  properties: Record<string, RuntimeValue> | undefined,
  name: string,
  dataModel: JsonObject | null | undefined,
  fallback = true
): boolean => {
  const value = properties?.[name];
  if (value === undefined) {
    return fallback;
  }
  return toBoolean(resolveValue(value, dataModel), fallback);
};

export const resolveStringList = (
  properties: Record<string, RuntimeValue> | undefined,
  name: string,
  dataModel: JsonObject | null | undefined
): string[] => {
  const value = properties?.[name];
  if (value === undefined) {
    return [];
  }
  const node = resolveValue(value, dataModel);
  if (Array.isArray(node)) {
    return node.map((item) => toText(item, ""));
  }
  const text = toText(node, "");
  return text === "" ? [] : [text];
};
