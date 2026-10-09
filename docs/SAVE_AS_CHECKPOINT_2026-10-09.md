# Save As checkpoint — 2026-10-09

**English** | [Русский](SAVE_AS_CHECKPOINT_2026-10-09_RU.md)

## Implemented in source

The editor has a separate **Save As…** button and **Ctrl+Shift+S**. It is enabled
for a nonempty, even unchanged document. The existing metadata dialog chooses
name and category. Success creates a new store ID and keeps the editor open on
that copy. Ordinary Save/Ctrl+S updates the currently open blueprint.

Cancellation, invalid metadata and a refused write leave the original store
record intact. Proposed copy metadata stays provisional until persistence
succeeds. Cancelling a failed copy followed by ordinary Save cannot apply the
abandoned copy name to the original. Retry retains copy mode and proposed fields.

Copies retain the supported parts, transforms, material seed, groups, supports,
custom snap points, source evidence and preview camera. The external import cache
identity is deliberately omitted: editing an independent copy must not redefine
which native copy belongs to an external library entry. Retaining materialSeed
is data preservation, not completed material-variation rendering or gameplay.

No format migration, host placement, resource, stability or permission logic was
changed by this step. Assembly version remains .116 while the pending converter
work is unfinished. This is an undeployed source checkpoint, not a .116 update
installed in Valheim.

## Evidence

- Final normal and QA builds: zero warnings/errors. Normal DLL SHA-256
  `F0E97DB6ADDDDF46F50FA25304463C09087F1B71F1D44A8CC84272EF9E248283`;
  QA DLL `8EDD83C9CDCBF8151ABD8A2EA79FD0A52E3CF29CB39412EF8242DD83FCCA9A80`.
- Actual Unity editor/native UI acceptance passed with 360 screenshots at four
  resolutions and 100/120/140% UI scale; completed UTC 2026-10-09T11:11:42.
  Tests cover clean-document copy, button and shortcut, cancellation, invalid
  name, preserved supported data/new ID, subsequent ordinary Save, refused
  write/cancel/ordinary Save, retry and disk reopen. The Russian copy dialog
  and compact header were also visually inspected at 1280×720/140%.
- Final Controller/store bridge, actual Session root-composition 17 checks,
  localization 878 keys/embedded resources, compiled QA isolation and launch
  profile preflight passed. dotnet format exited 0 with the existing workspace
  load warning. Earlier in this same step Geometry, Store353, localization unit
  and HostContract checks passed; they did not simulate real game placement.
- Initial sandbox failures: Store/bridge access denied, dotnet format named-pipe
  UnauthorizedAccessException and Unity exit -536870911. Isolated reruns outside
  the sandbox passed. A new test fixture initially used the wrong source-document
  constructor (CS1729); the fixture was corrected before acceptance.
- Independent read-only review found the failed-copy metadata bug. The fix and
  targeted regression passed re-review; Ponytail review found no remaining
  blocking issue or unnecessary layer. The verify helper is unavailable.

Local evidence: `artifacts/ui-runtime/verification-status.json`,
`artifacts/ui-runtime/save-as-ru-1280x720-140.png`,
`artifacts/ui-runtime/source-sha256.txt` and
`artifacts/fix-0.19.22/world-layout-checks/result.txt`.

Installed QA DLL was rechecked and remains
`867E5E474A3C1B335500E47F1B2DC3A6250AC1FAA36E6D92E766E904F33DCF3A`.
No deployment, Valheim launch or save writes were performed. Save As has no
in-game acceptance yet. Existing owner acceptance remains scoped as recorded in
[the feedback report](feedback-2026-10-09.md).

## World snap investigation and next dependency

No concrete world-snap defect has been reproduced. Native-piece snap points are
converted to piece-local coordinates, scaled by the resolved prefab root scale,
rotated/translated into the blueprint frame, and rebased with the same frame as
the visuals. Inspection of the installed host shows native Player selects the
closest eligible pair within its radius and translates the ghost to that pair.
This establishes the calculation path, not which points the owner actually saw.

Next discriminator: a supported fixture on stable native supports, explicit
source/target snap IDs and world coordinates, and native chosen-pair/ghost pose.
Keep stability enabled. Existing Session/Unity checks do not prove this native
selection path. Independent XYZ controls, clearer delete/replacement/filter
actions and the interior camera remain open. Complete pending material variation
before claiming the main import path or installing this combined source state.

## Future in-game copy check — not requested on installed .116

After a separately verified deployment, open the existing **QA116 Source Position**
card through **РЕДАКТИРОВАТЬ**. If absent, use the editor catalogue's
**Импорт файла** →
`Default/BuildWorks/tests/fixtures/placement-headers/15-source-position.blueprint`
→ **Подтвердить импорт**, then **РЕДАКТИРОВАТЬ**. Start with this saved native
blueprint and no modal dialog open. Then choose
**Сохранить как…**, enter `QA_SaveAs_Copy` and confirm **Сохранить**. The editor
must stay on the copy; edit and use ordinary **Сохранить**, then reopen both
entries. Only the copy should change. Cancel a second **Сохранить как…** with
**Отмена** and
verify that no third entry appears. A changed original or extra cancelled copy
is a failure. This check needs no world placement, resources or recipe unlocks.
