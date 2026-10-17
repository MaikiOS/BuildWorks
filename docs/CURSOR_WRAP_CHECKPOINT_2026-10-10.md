<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .137 — transform pointer wrap

Ordinary physical transform gizmos and constrained G/R/S wrap to the opposite
viewport edge. Ctrl reaches native panels/axis buttons. Surface-targeted G,
anchor picking and Array layout retain ordinary targeting. No cursor lock or
global hooks, new package, forced backend setting or game/world data change.

BlueprintEditorInput reflects the installed Input System1.19 public
WarpMousePositionCommand.Create / InputDevice.ExecuteCommand path, identical to
Mouse.WarpCursorPosition except that the native command result is checked.
Requires focused window, unlocked pointer, on-screen source/target and a
native/enabled/added current Mouse. Unsupported/missing backend retains numeric
input and shows a limitation. Controller preserves a virtual offset, absorbs
the deferred warp event and sends no duplicate command while pending. A250ms
unacknowledged warp cancels preview. Escape/focus loss cancel; Return or physical
release can commit the last valid preview while pending. Reset/cleanup clear
state. Existing range/shear/cost/persistence/network rules are unchanged.

Baseline reproduced the viewport-edge failure. Final results belong to
artifacts/checkpoint-0.19.137/verification.json; a running capture is not passed.
Test harness corrections and retained failure logs are described in the Russian
checkpoint. Real Input System device/backend and legacy Input.mousePosition
observation of the native warp remain Valheim acceptance. API presence and
simulated physical input are insufficient proof. Unsupported backend means the
requested continuous pointer is still unavailable in that configuration.

Independent review found false acknowledgement when manual movement merely became closer to the destination. Now arrival requires8px proximity; actual controller regression checks55% manual motion remains pending with unchanged preview and one command, then exact arrival. Fast motion beyond the allowance safely times out/cancels rather than applying a false offset.
