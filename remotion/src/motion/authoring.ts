import {
  MotionChannelSchema,
  MotionClipSchema,
  MotionDocumentSchema,
  MotionKeyframeSchema,
  MotionSchemaUrl,
  MotionSchemaVersion,
  MotionTrackSchema,
  MotionValueSchema,
  type MotionChannel,
  type MotionClip,
  type MotionDocument,
  type MotionKeyframe,
  type MotionTrack,
  type MotionValue,
} from "./schema";

export const defineMotionDocument = (
  input: Omit<MotionDocument, "$schema" | "version"> &
    Partial<Pick<MotionDocument, "$schema" | "version">>,
): MotionDocument =>
  MotionDocumentSchema.parse({
    $schema: MotionSchemaUrl,
    version: MotionSchemaVersion,
    ...input,
  });

export const defineMotionClip = (input: MotionClip): MotionClip =>
  MotionClipSchema.parse(input);

export const defineMotionTrack = (input: MotionTrack): MotionTrack =>
  MotionTrackSchema.parse(input);

export const defineMotionChannel = (input: MotionChannel): MotionChannel =>
  MotionChannelSchema.parse(input);

export const defineMotionKeyframe = (input: MotionKeyframe): MotionKeyframe =>
  MotionKeyframeSchema.parse(input);

export const defineMotionValue = (input: MotionValue): MotionValue =>
  MotionValueSchema.parse(input);

export const parseMotionDocument = (input: string | unknown): MotionDocument => {
  const payload =
    typeof input === "string" ? (JSON.parse(input) as unknown) : input;
  return MotionDocumentSchema.parse(payload);
};

export const stringifyMotionDocument = (
  input: MotionDocument,
  space = 2,
): string => JSON.stringify(MotionDocumentSchema.parse(input), null, space);

export const numberValue = (value: number): MotionValue =>
  defineMotionValue({ kind: "number", number: value });

export const booleanValue = (value: boolean): MotionValue =>
  defineMotionValue({ kind: "boolean", boolean: value });

export const textValue = (value: string): MotionValue =>
  defineMotionValue({ kind: "text", text: value });

export const vectorValue = (...value: number[]): MotionValue =>
  defineMotionValue({ kind: "vector", vector: value });
