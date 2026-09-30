# BuildWorks contributor instructions

Read `docs/ROADMAP.md`, `HANDOFF.md`, `docs/ARCHITECTURE.md` and `CONTRIBUTING.md`
before editing. English is the primary public language; Russian is a complete
duplicate. Keep internal IDs independent of translated labels.

## Current owner workflow

Ostrix authorized ordered development and verified-checkpoint synchronization
to `MaikiOS/BuildWorks` on 2026-09-30. Development installations, upgrades and
game tests use only `TerrainRamp-1.0-Test`. Other profiles and mods are out of
scope; do not remove historical installations automatically. The local source
tree under `Default/BuildWorks` is not a mod installation.

Use the guarded `Deploy-TestBuild.ps1` only from the owner's source tree with
Valheim closed. It rejects Default and every target other than TerrainRamp-1.0-Test.
Deployment does not authorize launching Valheim or modifying saves.

Pause for UI/icon design approval, a prepared game-test series, and renewed guide
tool discussion before Line A/B or any curves. Historical status/STOP is not the
current work order. Work one bounded stage at a time.

## Invariants

- BepInEx is the loader; no required Jotunn, copied gameplay-mod code or assets.
- Keep deterministic math/document operations in BuildWorks.Geometry, without Unity.
- Document is authoritative; one operation is one Undo; source prefabs stay immutable.
- Separate world placement anchor, local group pivots and temporary transform center.
- Keep native resource, permission, ownership, validation and networking checks.
- Store migrations and atomic writes must preserve unknown data or block safely.
- Preview/commit must use the same validated result. Adaptive pieces need their
  own mesh/UV/collider/snap/persistence/network contract.
- Do not split large orchestrators merely for line counts; extract independently
  testable boundaries only when they remove duplicated ownership.

## Verification and synchronization

Use checks from CONTRIBUTING, plus `tests/BuildWorks.DeploymentChecks/Run.ps1`.
Build with explicit `-p:ProfileRoot` and `-p:ValheimManagedDir` when needed.
Run `scripts/Test-HostContract.ps1 -ProfileRoot <dedicated-profile-path>` from
this checkout; its local inference is for the owner's source layout.

For non-trivial diffs obtain independent correctness and complexity review before
commit. Record automated results separately from Workbench/game/save/reload/network
acceptance. Current source is 0.19.38; future recipe/curve code is not implemented.

After an owner-authorized verified checkpoint update EN/RU statuses, commit and
push normally. Preserve unrelated changes and never force-push. Do not publish
game DLLs, saves, personal logs/paths, secrets or binary releases automatically.
On network/access failure retain the checkpoint and report the actual blocker.
External contributors use the pull-request process, not direct owner-branch pushes.
