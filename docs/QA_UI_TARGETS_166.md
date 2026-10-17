<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# QA UI targets .166 — offline, 2026-10-12

QA responses now describe up to32 active, interactable native Selectables whose
centres win the first EventSystem raycast. A12000-byte compact JSON budget bounds the
target payload; truncation is explicit. Controls covered by a non-selectable
graphic are excluded. Stable unchanged controls retain their opaque IDs across
observations; movement, labels, full values, screen size and EventSystem changes
invalidate them. Exported labels/values are bounded; validation uses full strings.

An optional `uiTarget` in batch requires exactly one left click or focused text
step and one neutral release, with stopRevision. Native coordinates come from the
validated target. The actual control is checked before dispatch and while held,
including before release. Text also requires the observed writable field to be
selected and actually focused. Existing input-module/device/cancellation paths
remain in use; no direct Button.onClick dispatch. Abort does not undo completed
native actions. This code is absent from the ordinary build.

Normal/QA .166 builds are clean; protocol43, inert production UI40, publisher,
text-submit14, actual Respond12, compiled QA isolation/native bindings, HostContract,
localization989, Python mission4/scenarios3 passed; independent PASSED/Ponytail Ship.
Sandbox File.Replace/journal tests initially
failed; CPU reruns outside the sandbox passed. The verify helper is absent.

Not deployed: installed QA remains .165. No game, model or GPU run. Live raycast,
focus, click/release and latency remain unverified. No pagination, drag or IMGUI
support. UI-control identity does not establish the selected blueprint/item ID.
The model pilot still forbids click/text; task-scoped integration and data/resource
snapshots/reload are next. Full scenarios stay blocked; `safe_ui_targets` is not
advertised as an available full-executor capability. See the [Russian contract](QA_UI_TARGETS_166_RU.md).
