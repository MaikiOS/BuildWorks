# BuildWorks goal amendment — 2026-10-09

**English** | [Русский](GOAL_AMENDMENT_2026-10-09_RU.md)

Append the following to the existing objective. These are requirements, not
implementation claims. This file does not update the goal mechanism.

## 13. Copies, scale, tree and camera

Add explicit Save As: a new blueprint identity, name/category selection and all
supported data retained. Save updates the current blueprint. Cancel/write failure
must preserve the source and leave no extra copies. Distinguish repeat import,
copy and update in the interface.

Provide discoverable confirmed deletion for stored native blueprints, including
imported copies. Provide separate hiding for an external library entry without
deleting or modifying its source file. Neither action removes built world objects.

Add explicit independent X/Y/Z scale and proportional mode, keeping prefab base
scale separate. Ghost, snaps, bounds, colliders, native placement and reload must
agree. Reject zero, negative, nonfinite and unsupported values; do not approximate
unrepresentable group transforms with incorrect geometry. Check rotated and
nonuniformly scaled connections in editor/world, preserving native stability.

Expose missing replacement with its prerequisites: one individually selected
missing record and the owner's replacement pieces. Deletion remains distinct and
is not replacement. Add clear tree filters for problems, name, ID, known source,
type and other reliably known attributes/purpose. Retain ancestor group context;
search/filter must not split groups, reparent objects or modify the blueprint.
Label unknown source/type honestly.

For incomplete blueprints, show available pieces and honest missing markers, or
an explicit thumbnail-unavailable explanation rather than an unexplained blank.
Retain the missing-parts warning.

Add a discoverable first-person interior editor-camera mode for complex buildings
and item placement, reusing existing flight: free movement/look, explicit entry
and exit, camera-directed aiming/placement, speed adjustment and return to the
normal view. Support tight interiors and restore cursor/game input on exit, close,
unload and disable. This mode grants no admin rights and bypasses no costs or
construction validation.

Supply a stable scale/snap fixture whose pieces have necessary support, without
owner-built scaffolding. Retain the owner's 2026-10-09 acceptance of spending,
destruction refunds, modified-piece persistence and no noticeable catalogue delay
for the tested case. Do not generalize it to every format, item, heavy build or
network scenario.
