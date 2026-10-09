# E09 — interior editor camera

2026-10-09. Source and automated evidence only; not deployed. QA0.19.116
remains in TerrainRamp-1.0-Test, SHA256
`867E5E474A3C1B335500E47F1B2DC3A6250AC1FAA36E6D92E766E904F33DCF3A`.
No game/world/save mutation occurred.

Camera menu adds Inside building / Return to overview and0.1–30m/s speed.
Interior position/yaw/pitch are independent from the overview; entry keeps
the current position, uses perspective and0.02 near clip. Return restores
the original overview pose/projection/clip. This is an editor fly camera,
not a player body, administrator privilege or collision-aware walking mode.
Camera state does not modify or extend blueprint serialization. Speed lasts
for the current controller session; disk preference persistence is not claimed.

RMB immediately acquires center aiming, mouse/WASD/QE move/look, Shift triples
speed, wheel adjusts speed. Releasing RMB restores pointer/UI interaction.
With a catalogue piece selected, RMB+LMB commits the center-ray preview;
normal overview flight keeps its existing confirmation guard. Undo remains
available. Existing Escape cancellation/modal priorities remain. Frame all
or selection returns to overview and frames the target. Modals, menus, text
entry and focus loss stop flight; editor cleanup restores previous cursor
state. No world cost, inventory, permission or network rule is changed.

Normal/QA zero warnings/errors, localization930, Store fixture21 import and
roundtrip, HostContract Steam25185596 passed. Unity6000.0.61f1 completed492
screenshots at `2026-10-09T17:17:41.7934306Z`, four resolutions,100/120/140%.
Actual entry/speed/exit buttons, stationary RMB capture, movement/look/speed,
wheel, modal/focus interruption, center-ray placement/Undo, unchanged overview
and document during navigation, exact view restoration and active-flight
editor closing passed. Matrix clicks wait for the opened panel to render;
native EventSystem/raycast verification stays enabled. The parent inspected
the Russian1280×720/140% menu. Workbench input is substituted and does not
prove physical host input or native Valheim behavior.

Evidence/reviews/commit: `artifacts/checkpoint-0.19.117/interior-*`.
The .117 artifact directory is not an installed .117 release. The verify
helper is unavailable; direct checks and independent read-only reviews apply.

Ready file `tests/fixtures/metadata/21-camera-room.buildworks.json`, card
**QA E09 Interior Camera**, contains four native walls and two floor pieces
in one group. It is an editor fixture, not construction/stability proof.
Owner acceptance waits for a newly installed QA build. No world construction
or materials are required. Exact initial state, Russian buttons, actions,
expected results and failure signs are in
[the Russian checkpoint](INTERIOR_CAMERA_CHECKPOINT_2026-10-09_RU.md).
Native game cursor/input, multiplayer and world reload remain unverified.
