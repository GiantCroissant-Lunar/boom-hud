import { z } from "zod";

export const MotionSchemaUrl =
  "https://boom-hud.dev/schemas/motion.schema.json" as const;
export const MotionSchemaVersion = "1.0" as const;

export const MotionTargetKindSchema = z.enum(["element", "component", "root"]);
export type MotionTargetKind = z.infer<typeof MotionTargetKindSchema>;

export const MotionPropertySchema = z.enum([
  "opacity",
  "positionX",
  "positionY",
  "positionZ",
  "scaleX",
  "scaleY",
  "scaleZ",
  "rotation",
  "rotationX",
  "rotationY",
  "width",
  "height",
  "visibility",
  "text",
  "spriteFrame",
  "color",
]);
export type MotionProperty = z.infer<typeof MotionPropertySchema>;

export const MotionEasingSchema = z.enum([
  "linear",
  "easeIn",
  "easeOut",
  "easeInOut",
  "step",
]);
export type MotionEasing = z.infer<typeof MotionEasingSchema>;

export const MotionValueSchema = z.discriminatedUnion("kind", [
  z.object({
    kind: z.literal("number"),
    number: z.number(),
  }),
  z.object({
    kind: z.literal("boolean"),
    boolean: z.boolean(),
  }),
  z.object({
    kind: z.literal("text"),
    text: z.string(),
  }),
  z.object({
    kind: z.literal("vector"),
    vector: z.array(z.number()).min(2).max(4),
  }),
]);
export type MotionValue = z.infer<typeof MotionValueSchema>;

export const MotionKeyframeSchema = z.object({
  frame: z.number().int().min(0),
  value: MotionValueSchema,
  easing: MotionEasingSchema.default("linear"),
});
export type MotionKeyframe = z.infer<typeof MotionKeyframeSchema>;

export const MotionChannelSchema = z.object({
  property: MotionPropertySchema,
  keyframes: z.array(MotionKeyframeSchema).min(1),
});
export type MotionChannel = z.infer<typeof MotionChannelSchema>;

export const MotionTrackSchema = z.object({
  id: z.string().min(1),
  targetId: z.string().min(1),
  targetKind: MotionTargetKindSchema.default("element"),
  channels: z.array(MotionChannelSchema).min(1),
});
export type MotionTrack = z.infer<typeof MotionTrackSchema>;

export const MotionClipSchema = z.object({
  id: z.string().min(1),
  name: z.string().min(1),
  startFrame: z.number().int().min(0).default(0),
  durationFrames: z.number().int().min(1),
  tracks: z.array(MotionTrackSchema).min(1),
});
export type MotionClip = z.infer<typeof MotionClipSchema>;

export const MotionSequenceFillModeValueSchema = z.enum([
  "none",
  "holdStart",
  "holdEnd",
  "holdBoth",
]);
export type MotionSequenceFillModeValue = z.infer<
  typeof MotionSequenceFillModeValueSchema
>;

export const MotionSequenceEntrySchema = z.object({
  clipId: z.string().min(1),
  startFrame: z.number().int().min(0).optional(),
  durationFrames: z.number().int().min(1).optional(),
  fillMode: MotionSequenceFillModeValueSchema.default("none"),
});
export type MotionSequenceEntry = z.infer<typeof MotionSequenceEntrySchema>;

export const MotionSequenceDefinitionSchema = z.object({
  id: z.string().min(1),
  name: z.string().min(1),
  items: z.array(MotionSequenceEntrySchema).min(1),
});
export type MotionSequenceDefinition = z.infer<
  typeof MotionSequenceDefinitionSchema
>;

export const MotionDocumentSchema = z.object({
  $schema: z.string().default(MotionSchemaUrl),
  version: z.string().default(MotionSchemaVersion),
  name: z.string().min(1),
  framesPerSecond: z.number().int().min(1).default(30),
  defaultSequenceId: z.string().min(1).optional(),
  clips: z.array(MotionClipSchema).min(1),
  sequences: z.array(MotionSequenceDefinitionSchema).optional(),
});
export type MotionDocument = z.infer<typeof MotionDocumentSchema>;
