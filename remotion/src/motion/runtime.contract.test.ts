import assert from "node:assert/strict";
import test from "node:test";
import { parseMotionDocument } from "./authoring";
import { resolveClipStateAtFrame, resolveTargetStateAtFrame, toAnimatedStyle } from "./runtime";
import { portableContractMotionPayload } from "./test-fixtures/portable-contract.fixture";

test("resolveClipStateAtFrame keeps scoped target ids stable", () => {
  const document = parseMotionDocument(portableContractMotionPayload);
  const state = resolveClipStateAtFrame(document, "intro", 12);

  assert.deepEqual(Object.keys(state).sort(), [
    "char1",
    "char1/attackButton",
    "char1/name",
    "root",
  ]);

  assert.equal(state.root.opacity, 1);
  assert.equal(state["char1"].positionX, 18);
  assert.equal(state["char1/name"].text, "Aelric");
  assert.equal(state["char1/attackButton"].opacity, 1);
});

test("toAnimatedStyle uses the same resolved target state contract as Unity export", () => {
  const document = parseMotionDocument(portableContractMotionPayload);

  const rootState = resolveTargetStateAtFrame(document, "intro", "root", 12);
  const portraitState = resolveTargetStateAtFrame(document, "intro", "char1", 12);
  const nameState = resolveTargetStateAtFrame(document, "intro", "char1/name", 12);
  const attackButtonState = resolveTargetStateAtFrame(
    document,
    "intro",
    "char1/attackButton",
    12,
  );

  assert.equal(rootState.opacity, 1);

  assert.equal(portraitState.positionX, 18);
  assert.equal(toAnimatedStyle(portraitState).transform, "translate3d(18px, 0px, 0px)");

  assert.equal(nameState.text, "Aelric");

  assert.equal(attackButtonState.opacity, 1);
  assert.equal(toAnimatedStyle(attackButtonState).opacity, 1);
});
