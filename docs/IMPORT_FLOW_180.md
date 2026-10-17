<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Import .180 — phases, result and recovery

Implemented: a phase bar, localized operation name and elapsed seconds. Phases
are not a remaining-time estimate. Successful import closes the dialog and selects
the actual published card by ID/importSource on the correct Hammer/editor page.
A multi-document archive names its initial destination before confirmation.

The editor retains the dirty document, selection and Undo history. The exchange
restores its old focus before the owner navigates to the result. A closed catalogue,
dead player or deliberately closed exchange is not reopened automatically. A
started write continues in the background and remains discoverable afterwards.

Failure before the atomic ZIP commit is not success. Catalogue preparation failure
after commit preserves the file receipt and offers only Refresh catalogue. This
retry reads the existing archive and cannot create a second copy. An older window
cannot consume the result of a different newer import operation.

Verification: normal/QA builds have zero warnings/errors; Geometry/full Store,
Localization runtime, EditorBridge, WorldLayout, actual Exchange Tick, Host and
1013 translation keys pass. Final Unity Workbench:492 screenshots,
2026-10-13T19:21:50Z. Independent correctness PASSED, Ponytail Ship. Formatter still
fails inherited WHITESPACE:135 normalized diagnostics match .179, no new findings.
The verify skill is unavailable; direct checks were performed. No Valheim, game
input, models or user worlds were run.

Real failures are retained in artifacts/checkpoint-0.19.180: an experimental
deferred start relied on EditMode Time.frameCount and was removed as unnecessary.
The standalone test also omitted view.Tick between Back and the next click;
the game loop performs it. The missing tick was restored and fresh missing-review
assertions strengthened. Localization found a dynamic exchange.stage_ prefix;
the nine keys are now explicit. Tests were not bypassed.

Guarded installation0.19.180.0: BuildWorks SHA256
90F126D1E7DCDEF12F3878BD8713A21D2DD9F5A8EB3AFFEDEBEEB49B0C2A084C;
Geometry SHA256 7BAD79188E5BA5274492506F638F2AF3A6D21BC4DB7A74C4A6F7C4D21EB38B5F.
All103 config/token/provision-receipt files were preserved. Zero saved blueprints
and zero import files remain after deployment; no catalog reseeding.

The separate artifacts/Blueprints-for-import folder contains576 blueprint source
files and28 previous archives. Ostrix chooses imports and adds libraries. Source
package SHA, attribution, README and provenance are retained. These local test
copies are excluded from Git/releases.

Limits: no conversion speedup is established. Heavy libraries need measurement
and optimization. Multi-file selection, batch export, pre-import categories,
automatic Share files, server library and player-to-player transfer are not
implemented. Forest Hut/native availability still needs game acceptance.
Accepted scale/Save As/camera/auto-snap tests need no repeat without cause.
[Manual queue](MANUAL_QA_180.md).
