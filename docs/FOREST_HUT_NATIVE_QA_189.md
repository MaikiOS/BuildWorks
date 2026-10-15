# Forest Hut native QA .189 — 2026-10-15

QA 0.19.189.0 on Valheim 1.0.20/build25909357 was launched through Thunderstore TerrainRamp-1.0-Test Modded with the existing one-shot GABS bridge permission. Fresh logs and the native save list confirmed QA isolation.

The existing 28-part QA187 Forest Hut copy was continued without reimport or provisioning. Four legacy wood_wall_roof records were explicitly replaced through the real catalogue with the selected wood_wall_roof_a. Preview cancellation, Apply, Undo/Redo and save/reopen passed; positions, rotations, scale and the edited door survived reopening. This establishes explicit replacement, not automatic prefab equivalence.

A visible ghost and native Valid status were observed after aiming at the yard. One bounded ordinary-input click started construction with NoCostCheat=false and an existing workbench nearby. At first completion: 28 TryPlacePiece and 28 ConsumeResources calls, no exceptions; Wood64→6 matched the displayed cost58. Tool wear and automatic stamina waits were observed.

All28 first-house world objects were matched by prefab/position. One common translation matched the document; maximum position error0.000013m, quaternion component error0, scale error<0.000001. Distinct native ZDO identities were read.

Actual logout/reload remains pending. Later, a second house started elsewhere and remaining Wood disappeared; input origin is awaiting owner clarification, so the first measurement is explicitly bounded to the first completion. No second automated placement click was sent. Initial horizontal aiming produced a no-visible-ghost message; the later screenshot/native Valid check, not merely a GameObject name, proves the ghost. U+25BE is missing from the Valheim font.

This does not prove arbitrary-library conversion, populated construction, admin restore, persistent project/v11 or multiplayer. Full local reports: artifacts/host-1.0.20 and QA BepInEx/ValAgentBridge/test-results; exact pre-reload data: forest-hut-before-reload-evidence.json. Third-party source and saves remain private.
