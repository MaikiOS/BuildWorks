# Two real external blueprints: verified pipeline segment

**English** | [Русский](external-path-2026-10-07_RU.md)

Product source remains 0.19.87; only acceptance code changes. Installed version remains .82.
No Valheim launch, deployment, world or character mutation.

Local originals: `artifacts/external-blueprints/BiomeBlueprints-1.0.3/smoke`.
`hs_ashlands_madzobuild_coffeetable.blueprint` (Madzobuild,4 parts) and
`hs_blackforest_hut.blueprint` (Biome Blueprints,28 parts) come from
[OverDrive/BiomeBlueprints 1.0.3](https://thunderstore.io/c/valheim/p/OverDrive/BiomeBlueprints/v/1.0.3/).
Archive/member hashes and attribution are pinned in local provenance.json.
No redistribution license established: archive, source copies and original previews stay outside Git/releases.
Source and archive remain byte-identical. Terrain source is retained but never executed.

Run `./scripts/Capture-UiWorkbench.ps1` in PowerShell7 from BuildWorks with Unity6000.0.61f1,
installed native asset bundles and the pinned local archive. Modified inputs fail instead of being overwritten.
Actual reviewed import/background catalogue, controller editing, native prefab scale/meshes,
disk/editor reopen, our JSON export/reimport and world renderer are exercised.
First part moves +0.25X; independent placement support is authored at (1.25,-0.25,0.5),yaw37.
Every part pose/scale/source row and authored support pose are checked after persistence and exchange.
World plan places support at (25,4,-15),yaw73; each instance matrix and visible nonempty geometry must match.
No whole-building scale correction. Native wood_roof intrinsic scale is (1.00000048,1,1.00000048).

Final result:252 UI screenshots plus two real-building renderer images. Protruding Forest Hut beams also
appear in its original preview and were not corrected. Different camera/light; no pixel parity claim.
Evidence: `artifacts/external-path-2026-10-07/verification.json`, actual-divider-check.txt,
unity-status.json, two real-*-world-preview.png files and acceptance-source hashes.
Mandatory PASS token required; BLOCKED/FAILED, changed poses/scales/source/support or empty geometry fails.

Native construction/costs/stability/world reload/cancel/failure/network remain untested. Disk/editor reopen
is not world reload. Admin restoration is not implemented. Prepared survival world/character does not yet
exist: do not ask the owner to gather materials, unlock recipes or build setup objects manually.
Before manual instructions, prepare isolated world/character, native-recipe resources, hammer/station and
ordinary mode without debug/no-cost bypass. Then supply exact Russian button sequence, starting state,
expected result and errors for each named fixture, cancellation and reload; host/remote client separately.
AGENTS.md requires explicit permission for game launch/save creation; native CUA control is unavailable.
