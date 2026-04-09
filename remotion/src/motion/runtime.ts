import type React from "react";
import { Easing, interpolate, useCurrentFrame } from "remotion";
import type {
  MotionClip,
  MotionDocument,
  MotionEasing,
  MotionKeyframe,
  MotionProperty,
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
