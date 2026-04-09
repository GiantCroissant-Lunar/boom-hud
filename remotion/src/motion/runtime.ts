import type React from "react";
import { Easing, interpolate, useCurrentFrame } from "remotion";
import type {
  MotionClip,
  MotionDocument,
  MotionEasing,
  MotionKeyframe,
  MotionProperty,
  MotionSequenceDefinition,
  MotionSequenceFillModeValue,
  MotionValue,
} from "./schema";

export type ResolvedMotionScalar =
  | number
  | boolean
  | string
  | readonly number[];

export type MotionTargetState = Partial<
  Record<MotionProperty, ResolvedMotionScalar>
>;

export type MotionClipState = Record<string, MotionTargetState>;

export type MotionSequenceFillMode = MotionSequenceFillModeValue;

export type MotionSequenceItem = {
  clipId: string;
  startFrame?: number;
  durationFrames?: number;
  fillMode?: MotionSequenceFillMode;
};

export type MotionSequence = readonly MotionSequenceItem[];

export const findMotionSequence = (
  document: MotionDocument,
  sequenceId: string,
): MotionSequenceDefinition | undefined =>
  document.sequences?.find((sequence) => sequence.id === sequenceId);

export const getDefaultMotionSequenceId = (
  document: MotionDocument,
): string | undefined => document.defaultSequenceId ?? document.sequences?.[0]?.id;

export const getRequiredMotionSequence = (
  document: MotionDocument,
  sequenceId?: string,
): MotionSequence => {
  const resolvedSequenceId = sequenceId ?? getDefaultMotionSequenceId(document);
  if (!resolvedSequenceId) {
    throw new Error(`Motion document '${document.name}' does not define a sequence.`);
  }

  const sequence = findMotionSequence(document, resolvedSequenceId);
  if (!sequence) {
    throw new Error(
      `Motion sequence '${resolvedSequenceId}' was not found in '${document.name}'.`,
    );
  }

  return sequence.items;
};

export const findMotionClip = (
  document: MotionDocument,
  clipId: string,
): MotionClip | undefined => document.clips.find((clip) => clip.id === clipId);

export const resolveClipStateAtFrame = (
  document: MotionDocument,
  clipId: string,
  frame: number,
): MotionClipState => {
  const clip = findMotionClip(document, clipId);
  if (!clip) {
    throw new Error(`Motion clip '${clipId}' was not found in '${document.name}'.`);
  }

  const localFrame = clamp(frame - clip.startFrame, 0, clip.durationFrames);
  return resolveClipStateAtLocalFrame(clip, localFrame);
};

export const resolveSequenceStateAtFrame = (
  document: MotionDocument,
  sequence: MotionSequence,
  frame: number,
): MotionClipState => {
  const state: MotionClipState = {};

  for (const item of sequence) {
    mergeMotionState(state, resolveSequenceItemStateAtFrame(document, item, frame));
  }

  return state;
};

export const resolveTargetStateAtFrame = (
  document: MotionDocument,
  clipId: string,
  targetId: string,
  frame: number,
): MotionTargetState =>
  resolveClipStateAtFrame(document, clipId, frame)[targetId] ?? {};

export const useMotionClipState = (
  document: MotionDocument,
  clipId: string,
): MotionClipState => {
  const frame = useCurrentFrame();
  return resolveClipStateAtFrame(document, clipId, frame);
};

export const useMotionSequenceState = (
  document: MotionDocument,
  sequence: MotionSequence,
): MotionClipState => {
  const frame = useCurrentFrame();
  return resolveSequenceStateAtFrame(document, sequence, frame);
};

export const useMotionSceneState = (
  document: MotionDocument,
  options: {
    clipId?: string;
    sequence?: MotionSequence;
  },
): MotionClipState => {
  const frame = useCurrentFrame();

  if (options.sequence) {
    return resolveSequenceStateAtFrame(document, options.sequence, frame);
  }

  if (options.clipId) {
    return resolveClipStateAtFrame(document, options.clipId, frame);
  }

  throw new Error("Motion scene requires either a clipId or a sequence.");
};

export const useMotionTargetState = (
  document: MotionDocument,
  clipId: string,
  targetId: string,
): MotionTargetState => {
  const frame = useCurrentFrame();
  return resolveTargetStateAtFrame(document, clipId, targetId, frame);
};

export const toAnimatedStyle = (
  targetState: MotionTargetState,
): React.CSSProperties => {
  const style: React.CSSProperties = {};
  const transform: string[] = [];

  const opacity = asNumber(targetState.opacity);
  if (opacity !== undefined) {
    style.opacity = opacity;
  }

  const width = asNumber(targetState.width);
  if (width !== undefined) {
    style.width = width;
  }

  const height = asNumber(targetState.height);
  if (height !== undefined) {
    style.height = height;
  }

  const color = asText(targetState.color);
  if (color !== undefined) {
    style.color = color;
  }

  const visibility = targetState.visibility;
  if (typeof visibility === "boolean") {
    style.visibility = visibility ? "visible" : "hidden";
  } else if (typeof visibility === "string") {
    style.visibility =
      visibility === "hidden" || visibility === "collapse"
        ? visibility
        : "visible";
  }

  const translateX = asNumber(targetState.positionX) ?? 0;
  const translateY = asNumber(targetState.positionY) ?? 0;
  const translateZ = asNumber(targetState.positionZ) ?? 0;
  if (translateX !== 0 || translateY !== 0 || translateZ !== 0) {
    transform.push(`translate3d(${translateX}px, ${translateY}px, ${translateZ}px)`);
  }

  const scaleX = asNumber(targetState.scaleX);
  if (scaleX !== undefined) {
    transform.push(`scaleX(${scaleX})`);
  }

  const scaleY = asNumber(targetState.scaleY);
  if (scaleY !== undefined) {
    transform.push(`scaleY(${scaleY})`);
  }

  const scaleZ = asNumber(targetState.scaleZ);
  if (scaleZ !== undefined) {
    transform.push(`scale3d(1, 1, ${scaleZ})`);
  }

  const rotation = asNumber(targetState.rotation);
  if (rotation !== undefined) {
    transform.push(`rotate(${rotation}deg)`);
  }

  const rotationX = asNumber(targetState.rotationX);
  if (rotationX !== undefined) {
    transform.push(`rotateX(${rotationX}deg)`);
  }

  const rotationY = asNumber(targetState.rotationY);
  if (rotationY !== undefined) {
    transform.push(`rotateY(${rotationY}deg)`);
  }

  if (transform.length > 0) {
    style.transform = transform.join(" ");
  }

  return style;
};

export const getAnimatedText = (targetState: MotionTargetState): string | undefined =>
  asText(targetState.text);

export const getAnimatedSpriteFrame = (
  targetState: MotionTargetState,
): string | undefined => asText(targetState.spriteFrame);

export const getSequenceDurationFrames = (
  document: MotionDocument,
  sequence: MotionSequence,
): number => {
  if (sequence.length === 0) {
    return 0;
  }

  let maxFrame = 0;

  for (const item of sequence) {
    const clip = findMotionClip(document, item.clipId);
    if (!clip) {
      throw new Error(`Motion clip '${item.clipId}' was not found in '${document.name}'.`);
    }

    const startFrame = item.startFrame ?? clip.startFrame;
    const durationFrames = Math.min(item.durationFrames ?? clip.durationFrames, clip.durationFrames);
    maxFrame = Math.max(maxFrame, startFrame + durationFrames);
  }

  return maxFrame;
};

const resolveSequenceItemStateAtFrame = (
  document: MotionDocument,
  item: MotionSequenceItem,
  frame: number,
): MotionClipState => {
  const clip = findMotionClip(document, item.clipId);
  if (!clip) {
    throw new Error(`Motion clip '${item.clipId}' was not found in '${document.name}'.`);
  }

  const startFrame = item.startFrame ?? clip.startFrame;
  const durationFrames = Math.min(item.durationFrames ?? clip.durationFrames, clip.durationFrames);
  const endFrame = startFrame + durationFrames;
  const fillMode = item.fillMode ?? "none";

  if (frame < startFrame) {
    if (fillMode === "holdStart" || fillMode === "holdBoth") {
      return resolveClipStateAtLocalFrame(clip, 0);
    }

    return {};
  }

  if (frame > endFrame) {
    if (fillMode === "holdEnd" || fillMode === "holdBoth") {
      return resolveClipStateAtLocalFrame(clip, durationFrames);
    }

    return {};
  }

  return resolveClipStateAtLocalFrame(clip, clamp(frame - startFrame, 0, durationFrames));
};

const resolveClipStateAtLocalFrame = (
  clip: MotionClip,
  localFrame: number,
): MotionClipState => {
  const state: MotionClipState = {};

  for (const track of clip.tracks) {
    const target = state[track.targetId] ?? {};
    for (const channel of track.channels) {
      target[channel.property] = resolveChannelValue(channel.keyframes, localFrame);
    }
    state[track.targetId] = target;
  }

  return state;
};

const mergeMotionState = (
  target: MotionClipState,
  overlay: MotionClipState,
): void => {
  for (const [targetId, overlayState] of Object.entries(overlay)) {
    target[targetId] = {
      ...(target[targetId] ?? {}),
      ...overlayState,
    };
  }
};

const resolveChannelValue = (
  rawKeyframes: readonly MotionKeyframe[],
  frame: number,
): ResolvedMotionScalar => {
  const keyframes = [...rawKeyframes].sort((left, right) => left.frame - right.frame);
  if (keyframes.length === 1) {
    return unwrapMotionValue(keyframes[0].value);
  }

  if (frame <= keyframes[0].frame) {
    return unwrapMotionValue(keyframes[0].value);
  }

  const last = keyframes[keyframes.length - 1];
  if (frame >= last.frame) {
    return unwrapMotionValue(last.value);
  }

  for (let index = 0; index < keyframes.length - 1; index++) {
    const start = keyframes[index];
    const end = keyframes[index + 1];
    if (frame >= start.frame && frame <= end.frame) {
      return interpolateKeyframes(start, end, frame);
    }
  }

  return unwrapMotionValue(last.value);
};

const interpolateKeyframes = (
  start: MotionKeyframe,
  end: MotionKeyframe,
  frame: number,
): ResolvedMotionScalar => {
  if (start.easing === "step") {
    return frame >= end.frame
      ? unwrapMotionValue(end.value)
      : unwrapMotionValue(start.value);
  }

  if (start.value.kind === "number" && end.value.kind === "number") {
    return interpolate(frame, [start.frame, end.frame], [start.value.number, end.value.number], {
      easing: mapEasing(start.easing),
      extrapolateLeft: "clamp",
      extrapolateRight: "clamp",
    });
  }

  if (start.value.kind === "vector" && end.value.kind === "vector") {
    const startVector = start.value.vector;
    const endVector = end.value.vector;
    const size = Math.min(startVector.length, endVector.length);
    return Array.from({ length: size }, (_, index) =>
      interpolate(
        frame,
        [start.frame, end.frame],
        [startVector[index], endVector[index]],
        {
          easing: mapEasing(start.easing),
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
        },
      ),
    );
  }

  return frame >= end.frame
    ? unwrapMotionValue(end.value)
    : unwrapMotionValue(start.value);
};

const unwrapMotionValue = (value: MotionValue): ResolvedMotionScalar => {
  switch (value.kind) {
    case "number":
      return value.number;
    case "boolean":
      return value.boolean;
    case "text":
      return value.text;
    case "vector":
      return value.vector;
  }
};

const mapEasing = (easing: MotionEasing) => {
  switch (easing) {
    case "easeIn":
      return Easing.in(Easing.ease);
    case "easeOut":
      return Easing.out(Easing.ease);
    case "easeInOut":
      return Easing.inOut(Easing.ease);
    case "linear":
    default:
      return Easing.linear;
  }
};

const asNumber = (value: ResolvedMotionScalar | undefined): number | undefined =>
  typeof value === "number" ? value : undefined;

const asText = (value: ResolvedMotionScalar | undefined): string | undefined =>
  typeof value === "string" ? value : undefined;

const clamp = (value: number, min: number, max: number): number =>
  Math.max(min, Math.min(max, value));
