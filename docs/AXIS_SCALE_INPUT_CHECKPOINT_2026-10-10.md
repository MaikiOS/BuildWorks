# .129 — axis-constrained mouse and numeric scaling

Source0.19.129, checkpoint `ab2868b`; installed QA remains0.19.117.
No deployment or Valheim acceptance for this step.

S → X/Y/Z now selects a scale axis through keys or the existing buttons.
Mouse motion changes that axis; typed1.5 replaces the mouse delta with a factor
from the action start. No-axis S stays uniform; repeated axis switches coordinates.
Preview, anchors, edge guide, modifiers and commit consume the vector and frame.
The shared pure `TransformPart` validates poses before any preview mutation;
shear/range rejection retains the last valid preview and blocks commit.
Undo/Redo and the storage format are unchanged.

Baseline regression: `Scale ignores the selected axis: X`.
Tests cover all three axes, mouse/number, native Y button dispatch, rotated local
scale, world shear refusal, preview/commit and history. Geometry checks pure-pose
parity with group commits and existing native/root-scale validation.

Normal/QA0/0, Geometry, Store202/209 (7 explicit refusals), Host25185596,
localization931, compiled isolation and launch preflight passed.
Unity492 completed2026-10-10T10:55:04.4316655Z.
Correctness PASSED; Ponytail Ship after removing the pointless quadratic scan
from the new axis path. Preview-list capacity is reused and references cleared
on success/failure. Evidence: `artifacts/checkpoint-0.19.129/`.

`verify` unavailable. No game, saves or network execution; no50000-part FPS claim.
The old uniform mouse clamp remains unchanged. Future owner check is described
in the Russian companion; do not run it on installed.117.
