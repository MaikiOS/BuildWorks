# Project Purpose

Develop the existing BuildWorks precision-construction plugin: reusable assemblies,
blueprints, projects, and later agreed guides/original adaptive pieces. The current
module is supported external/world-captured blueprint
→ editor → correct ghost/native paid construction → reload, with server authority.
Current owner priority is approved v11 on a whole house, then a durable unfinished
project and stake/board. Preserve existing tools and the full PROJECT_CONTRACT.md.
The current blueprint goal is one module, not the whole product roadmap. Preserve
the later assembly/guide/adaptive directions without starting them prematurely.
docs/ROADMAP.md is the forward plan; docs/DEVELOPMENT_JOURNAL.md records history;
docs/CURRENT_STATUS.md separates source/tests/deployment/game/owner/network proof.

# Project Map

- `PROJECT_CONTRACT.md` - confirmed scope, boundaries, risks, and success proof.
- `ROADMAP.md` - ordered workstreams and milestone sequence.
- `specs/spec-precision-piece-placement.md` - acceptance criteria for the first slice.
- `specs/spec-construction-guides.md` - accepted later guide/deformation direction.
- `specs/spec-parametric-roof.md` - superseded whole-roof research, not current scope.
- `src/BuildWorks.Geometry` - engine-independent precision and curve mathematics.
- `src/BuildWorks/PrecisionPlacementSession.cs` - world placement state and F9 orchestration.
- `src/BuildWorks/BuildWorksLocalization.cs` - embedded English fallback and Russian overlay.
- `docs/ARCHITECTURE.md` - responsibility map and safe extension points.
- `docs/LOCALIZATION.md` - stable-key and translation workflow.
- `CONTRIBUTING.md` - contributor checks and pull-request contract.
- `src/BuildWorks/TransformGizmoView.cs` - mouse-driven runtime XYZ gizmo.
- `src/BuildWorks/UnifiedHammerCatalog.cs` - optional OdinArchitect merge,
  material ordering, and native Hammer catalog decorations.
- `src/BuildWorks/BuildWorksPlugin.cs` - BepInEx lifecycle; no Jotunn dependency.
- `tests/BuildWorks.GeometryTests` - runnable deterministic geometry checks.
- `tools/BuildWorks.UiWorkbench` - isolated Unity 6000.0.61f1 project for
  Blueprint Editor layout, interaction, viewport, and screenshot checks without
  launching Valheim.
- `artifacts/bin` - local build output; not an installed mod.
- `scripts/Deploy-TestBuild.ps1` - guarded, hash-verifying test deployment.

# Architecture Invariants

- Own all BuildWorks gameplay code, meshes, textures, icons, and product design.
  Use BepInEx only as the loader and Valheim/Unity as the host platform.
- Do not reference Jotunn or copy code or assets from other gameplay mods.
- Keep one in-process plugin with focused modules. Do not introduce network
  microservices into the Unity host process.
- Extend the selected Valheim piece instead of replacing it with a hidden carrier
  or monolithic generated structure. Native placement must receive the exact
  transformed ghost that the player approved. The first slice keeps the captured
  ray hit as attachment context and supports only ordinary structural pieces;
  special surface-dependent pieces require later transformed re-raycasting.
- Keep generated geometry independent from Unity. Preview, placement, colliders,
  persistence, and network reconstruction of later adaptive pieces must consume
  the same validated parameter set and generated mesh data.
- Make multiplayer state server-authoritative. Clients may request edits but may
  not bypass costs, permissions, supported ranges, or ownership.
- Treat construction guides as an accepted later capability. A guide supplies
  snap frames for individual pieces; it does not place a finished building.
  Rigid pieces align without deformation. Only explicitly adaptive original
  pieces may regenerate mesh, UV, collider, and snap data along a guide.
- Keep BuildWorks separate from EarthWorks and TerrainRamp. Compatibility may be
  added later, but neither is a runtime dependency.
- Install development DLLs and run game tests only in `TerrainRamp-1.0-Test`,
  as confirmed by Ostrix on 2026-09-30, and only through
  `scripts/Deploy-TestBuild.ps1`. The script must refuse `Default`, reject every
  other target (including `Default-Compat-Test`), check for stray Default DLLs,
  and verify SHA-256. Do not modify other profiles or other mods without a
  separate owner request. Existing installations elsewhere are historical;
  do not remove them automatically.
- `Default\BuildWorks` is the source and artifact tree, not an installed mod.
  `Default\BepInEx\plugins` must never contain `BuildWorks.dll`,
  `BuildWorks.Geometry.dll`, or `Ostrix-BuildWorks`.
- Do not launch Valheim, modify saves, or publish a package without Ostrix's
  explicit authorization.
- Authorized QA launches use Thunderstore profile `TerrainRamp-1.0-Test` and its
  `Modded` button. Do not bypass the manager with a direct Steam/game launch.

# Visible Progress

Ostrix requested a clean QA blueprint catalog on 2026-10-13. Do not repopulate it
with bulk libraries or automatic test blueprints. Test fixture/source folders
remain separate; cards must result from explicit import or user creation.
Preserve the catalog-reset backup in artifacts. Do not reset QA provisioning,
tokens, worlds, or saves to recreate removed cards.

Maintain `docs/LIVE_CHECKLIST_RU.md` for Ostrix. Update its current action at meaningful
step starts, after verification, and when direction changes. Separate source/test
completion, deployment and user game acceptance; mark only evidenced results.
List pending user checks with their prerequisites. Keep the full requirements in
`docs/WORK_STATUS_RU.md`; this checklist does not replace or update the goal API.

# Working Commands

Run from the `BuildWorks` directory:

```powershell
dotnet run --project .\tests\BuildWorks.GeometryTests\BuildWorks.GeometryTests.csproj -c Release
dotnet build .\src\BuildWorks\BuildWorks.csproj -c Release
.\scripts\Test-Localization.ps1 -RequireBuiltAssembly
.\scripts\Capture-UiWorkbench.ps1
.\scripts\Test-HostContract.ps1
.\scripts\Deploy-TestBuild.ps1 -TargetProfile TerrainRamp-1.0-Test -WhatIf
```

Remove `-WhatIf` only for an authorized test deployment. Deployment does not
authorize launching Valheim or changing worlds, characters, or saves.

For another local layout, override `ProfileRoot` and `ValheimManagedDir` with
MSBuild `-p:` arguments rather than editing the project file.

# Verification by Change Type

- Precision placement: validate step quantization and angle normalization in
  executable tests, then prove transformed host validation and native placement
  in game.
- Adaptive geometry: validate exact coordinates, triangle indices, finite values,
  UV/collider/snap consistency, and invalid input.
- Curve geometry: validate exact endpoints and deterministic samples before any
  curved placement feature consumes it.
- Placement: prove the displayed transformed ghost matches the placed piece;
  verify confirm and cancel do not leak objects or consume resources incorrectly.
- Persistence: save, exit, reload, and compare every parameter and reconstructed
  vertex before claiming completion.
- Multiplayer: host plus remote client must see identical geometry after place,
  edit, remove, reconnect, and server restart.
- Visual assets: inspect rendered and in-game results at gameplay distance and
  close range; run the UI Workbench screenshots first, then the real host smoke.
  Workbench proof does not replace Valheim runtime proof.

# Known Traps

- Direct Valheim APIs can change after game updates because BuildWorks does not
  use Jotunn's compatibility wrappers. Rebuild and smoke-test after every host
  update.
- Arbitrary rotations and generated colliders may expose assumptions in
  Valheim's support and `WearNTear` systems; verify them in the real host.
- Runtime mouse gizmos must not bypass Valheim placement validation or leave the
  cursor unlocked after cancel, menu changes, selected-piece changes, or teardown.
- Shared vertices are insufficient for final hard-edge normals and production
  UV seams. Adaptive pieces must produce face-appropriate normals, tangents, UVs,
  colliders, and regenerated snap points.
- Curved construction does not mean scaling one prefab around a bend. Pieces or
  generated sections must follow sampled frames without texture stretching.

# Definition of Done

For current ordered work, follow `docs/ROADMAP.md` (English) and its Russian
duplicate. After every verified checkpoint, update both status tables, commit
the source-only GitHub checkout, and push normally to `MaikiOS/BuildWorks`.
Ostrix authorized this synchronization on 2026-09-30. Never force-push, include
game DLLs/user data, or publish binary releases automatically. Preserve unrelated
changes. Stop at owner UI/icon approval or an in-game acceptance series; prepare
the exact test instructions and installed version before requesting it.
Curves, including Line A/B, require a renewed discussion of tool semantics
before implementation. A helper/test for curve math is not tool approval.

The first slice is done only when tests and plugin build pass, Ostrix snaps one
ordinary piece to an existing block, edits its position and all three rotation
axes with the mouse gizmo, places exactly that transformed piece through native
Valheim behavior, and sees the same transform after save/reload. Construction
guides, adaptive pieces, multiplayer, and the reference house remain incomplete
until their own roadmap evidence passes.
