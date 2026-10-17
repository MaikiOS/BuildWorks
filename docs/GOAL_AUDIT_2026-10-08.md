<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# BuildWorks active goal audit — 2026-10-08

**English** | [Русский](GOAL_AUDIT_2026-10-08_RU.md)

A complete goal replacement is unnecessary. The outcome and priorities remain
correct. Clean up literal `&#x20;`, escaping and trailing backslashes; replace
the vague reference to "remaining original requirements" with an explicit full
requirements reference. Do not turn implementation status into a narrower goal.

The full active objective was read; get_goal returned active. This report does
not alter the goal mechanism. Last installed checkpoint: e468ca7, .116.
The actual TerrainRamp-1.0-Test DLL hashes were checked again:

- BuildWorks: `867E5E474A3C1B335500E47F1B2DC3A6250AC1FAA36E6D92E766E904F33DCF3A`.
- Geometry: `48B61467D7335E3261F56EAEF1B81AB471B4B55EDE8B593260D0B971C4C44854`.

They match the .116 installation report. No game launch occurred for this audit.
The .116 automatic results were checked against retained logs/manifest, not
claimed as a fresh execution of the full matrix or as game acceptance.

## Active objective points

| Point | Present evidence | Remaining work |
|---|---|---|
| 1. Missing parts | IDs/transforms retained; uncertainty markers or recorded local-bounds frames; automatic checks | Confirmed mod provenance/dependency names and game acceptance |
| 2. Two conversion results | Explicit incomplete copy or separate missing-free copy; empty result refused, original retained; automatic checks | Valheim acceptance |
| 3. Metadata | ID/name/type/host fingerprint/intrinsic scale/static MeshFilter bounds; automatic checks | Owning mod/version, correct skinned bounds, game freshness; hostId is not mod ownership |
| 4. Foreign properties | Exact inert source, some typed ZDO/sign/mount conversion, explicit recognized ArmorStand-state omission; automatic checks | Remaining dialects/state, complete loss report, external RandMatSeed with correct appearance/application |
| 5. Export | Own JSON/ZIP, name/folder selection and strict roundtrip; automatic checks | Specification-compliant foreign exporters and explicit unrepresentable-data warnings |
| 6. Loaded area | Screen rectangle/manual selection of available objects | Area plus vertical volume limits, intersection rules, list/overrides, loaded-area warning |
| 7. Rights | Native survival construction and a narrow supplied ItemStand path | Server-authenticated administrative capture/full restore and server-owned world-object prices |
| 8. Mod boundary | BuildWorks work continues without full EarthWorks integration | Later separate integration discussion; this does not establish OBJ/FBX/glTF support in EarthWorks |
| 9. Main path | CoffeeTable built; Ostrix accepted its shape and observed no new duplicates | Selected formats/scale/axes, exact resource deltas, native world reload and remote client; full outcome unaccepted |
| 10. Fog v11 | One-wall/capsule visual baseline accepted; frozen archive and integration points studied | Host shader/compute/camera, multiple parts/house, returning-density identity, CPU/GPU/memory, durable jobs, unload/reload/network |

## Original obligations that must remain explicit

| Area | Actual state |
|---|---|
| Editor/supports/snaps | Data and tools exist with automatic evidence; world snapping and native reload/reconnect remain open |
| Scale/axes/lava lamps | Pinned prefab scale and visual BakeMesh fixed with Unity evidence; native placement/persistence unaccepted; no arbitrary whole-building shrink |
| Storage/items | Some capture/exchange paths exist; all containers, ordinary items, full ArmorStand and independent pre-build content choices are unfinished |
| Stake/durable construction | Existing Enter/Esc pause; durable job, stake, permanent ghost and server escrow not implemented |
| Cancellation/resources | Transient rollback implemented and automatically checked; game failure/reload/two-client/durable-job conservation unproven |
| Heavy builds | Background conversion, caches, staged editor/virtual tree/shared batching exist; full chain/FPS/finalization/reload and remaining Hammer stalls need measurement |
| QA/yard | Isolated profile, provided character/resources/stations and 64×64m yard exist; 192×192m yard and full survival/admin/network matrix unfinished |

Complete row registry: WORK_STATUS_RU.md, 72 requirements. Each row distinguishes
implementation, automatic evidence and owner acceptance. Previous assistant
claims alone are not acceptance.

## Recommended targeted objective amendment

Keep the main wording and points1–10. Replace the vague original-requirements
reference with the full registry and these explicit obligations:

- Survival requires resources/actual supplied items, preserves those items'
  properties and grants no free repair, upgrade or administrative state.
- Storage/mounts have independent import/export and pre-build choices; disabling
  placement never deletes archived blueprint contents.
- Supports, transform center and user snaps remain independent; temporary A/B
  never replace persisted supports. Preserve working editor tools.
- Durable construction uses an intangible ghost/stake, Hammer interaction,
  authoritative persistent state, cancellation and only actual-payment refunds.
- Measure the entire heavy-build chain. Isolated QA count bypass retains
  file/memory protections and grants no ordinary-player server privileges.
- Use only the isolated test profile/QA saves, leave primary worlds untouched,
  provide materials/stands and exact file/start/Russian-button/result/error steps.

This is an editorial proposal, not a goal API update or a new objective.

## Development continuation

Import remains first. A separate materialSeed storage/editor contract is in
progress, followed by every preview and the new native object's state. Until
appearance/application are implemented, external RandMatSeed remains inert and
blocking. Do not deploy this preparation in place of completed conversion.

Then native build/reload, remaining converters/dependencies, items/server rights,
durable projects with v11, volume selection and the complete heavy/network matrix.
