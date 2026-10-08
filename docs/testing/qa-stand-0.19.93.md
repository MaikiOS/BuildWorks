# QA package 0.19.93–0.19.97: startup/respawn/primary-slot corrections, native stand pending

QA .97 is now installed: all22 QA-root hashes unchanged, including complete supplies receipt. Same world/token; Modded restart pending.

## Checkpoint 0.19.97 — canonical QA station resolution; source verified, installation pending

Native .96 loaded the same isolated Local world/character and saved supplies with a complete
receipt. Station preflight refused before its receipt/placement: "QA station is not a registered
native root prefab." The exact failing field/prefab was not logged; no cause beyond that is proven.
.97 resolves the exact registered station prefab, includes inactive Piece parents, and validates
root Piece/CraftingStation/ZNetView plus native CraftingStation.m_name. It removes serialized
reference equality and adds field diagnostics, without similar-name substitutions. Existing
supplies receipt stays complete: no reset, reissue, station receipt deletion or terrain changes.
The compiled resolver regression failed .96 before the fix. .97 QA72 BCL + compiled wiring,
both Release builds0/0, HostContract, Localization853, both formats and independent correctness
review PASS. Ponytail review: one focused resolver, existing guards retained; no new dependency.
verify unavailable; UI/corpus/deployment replica not repeated. Compiled checks do not execute
real prefab resolution. Installed QA remains .96 while Valheim runs; close normally before the
guarded same-token .97 deployment. Continue ONLY through manager TerrainRamp-1.0-Test Modded.
Proof: artifacts/checkpoint-0.19.96/loaded-world-refusal.json and checkpoint-0.19.97/verification.json.
Native stations, imported construction, world reload and multiplayer remain unverified.


**English** | [Русский](qa-stand-0.19.93_RU.md)

Source and installed QA .96 correct native persisted-primary replay and async slot rotation; previous .95
refused replay before grants despite complete identities and existing saved world. Do not reset saves/receipts.
Guarded installation preserved all21 QA-root file hashes. See current HANDOFF and .96/verification.json; game retry pending.

QA .94 reached menu and created isolated identities; world entry refused preparation before grants.
Source .95 waits for native respawn completion/nonzero player ID without relaxing identity matching.
After game exit QA .95 was installed and all21 QA-root file hashes preserved; keep the same saves/token. This is not proof of a working
stand or complete import. See HANDOFF for current hashes/checks and native log evidence.

QA-only preparation waits for the fresh player's intro flight to finish; the loaded
guard also refuses InIntro. Stations come from actual requirements of the seven pinned blueprints.
All sites are preflighted before objects: loaded terrain, nine height samples with
at most0.12m variation, obstacles and proposed station overlap. Footprints combine
visual bounds and Box/Mesh collider geometry, child transforms and native root scale.
Unknown colliders, foreign MonoBehaviour and TerrainModifier/TerrainOp/TerrainComp
(including inactive children) refuse preparation. No terrain edits; unsuitable sites refuse.
Nine height samples do not prove native station stability.

Started receipt precedes free native QA station placement. After Start, new station,
owner/ZDO/creator, prefab, transform and build range are checked. Native profile Save()
persists knowledge. World Save(false,false,false) must finish !IsSaving and advance the
committed save number exactly once, including uint wrap, before complete. Partial attempts
never retry automatically. Complete prevents duplicate preparation, not proof of reload.
Blueprint construction still uses the existing survival resource/placement path.

After owner authorization only, from Default/BuildWorks in PowerShell7:

```powershell
./scripts/Deploy-TestBuild.ps1 -TargetProfile TerrainRamp-1.0-Test -QaStandToken 43d3719a7ee649d184e26ef1daa441a3
```

Authorized guarded .93 deployment and the .94 correction have run against the live profile.
QA deployment validates host/known-bootstrap
and all files, selects the separate QA DLL pair, stages six owned plus two external local
files, then writes the isolation marker last. It creates no game saves. Foreign tokens,
changed files and reparse-point DLL paths refuse; normal deployment over an armed QA identity
also refuses. Cleanup/library additions are forbidden in QA mode. External files remain local
and outside Git/releases; [provenance and conditions](external-path-2026-10-07.md).

Planned world/character name: **BW_QA_43d3719a**. Native Valheim assigns fresh actual IDs.
Save root: `TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/qa-saves/43d3719a7ee649d184e26ef1daa441a3`.
Authorization for this isolated QA launch/save creation is already given. Launch ONLY with
Thunderstore profile TerrainRamp-1.0-Test, button Modded; never direct Steam/game arguments.
Native UI control is unavailable here; the user clicks, then fresh logs/receipts are inspected.
Only QA identities should be visible; if regular saves
appear or QA identities are absent, stop without loading any world. Select the QA identities,
keep server closed with no clients or cheats. Hammer/resources/stations must be prepared
without farming/unlocking/building preparation objects. Inspect logs/complete receipts before
calling it prepared. Do not delete failed receipts or blindly reissue supplies.

Manual cases use the current Russian button labels; see the parallel Russian protocol for
exact file finding, starting state, actions, expected results and failure signs:

1. Hammer → «Чертежи» → «Импорт файла» → «Папка BuildWorks» → «Папка: test-fixtures».
2. `hs_ashlands_madzobuild_coffeetable.blueprint` → «Импорт файла» → «Подтвердить импорт».
   CoffeeTable card → ⋯ → «РЕДАКТИРОВАТЬ». Move one part along X; save `QA93 CoffeeTable`
   through «СОХРАНИТЬ И ВЫЙТИ». Reopen; all4parts and edit must remain, original unchanged.
3. Record resource counts, select placement, F9 → «ОТМЕНА»: no pieces/costs/items may change.
4. Select the edited copy; place on suitable clear ground near stations using F9 → «ПРИМЕНИТЬ».
   Ghost and four placed parts must match; exactly one native cost, no free contents/items.
5. Repeat import/editor/save as `QA93 Forest Hut` for `hs_blackforest_hut.blueprint`.
   All28parts, including original projecting beams, must retain native scale and placement.
6. Normal exit/save then reopen the same QA identities. Compare both buildings/stations and
   remaining inventory: unchanged transforms, no loss/duplication/reissue/new stations.

Unsuitable terrain/preparation refusal is a failed preparation, not a request that the owner
terraform or construct a test stand. Admin restoration, containers/mounts, heavy files and
multiplayer remain separate open work. Native construction/reload have not been executed.

Proof: `artifacts/checkpoint-0.19.93/verification.json`: both Release0/0, HostContract,
Localization853, QA64BCL cases, replica deployment12cases, forbidden profiles4, independent
correctness review. Normal DLL excludes all QA modules. File symbolic-link creation required
Windows administrator privileges; executed a DLL-path junction and reviewed the common
ReparsePoint guard. verify unavailable; direct review/build/targeted checks used.
UI252/corpus353 are previous evidence, not rerun. No Valheim window-control tools available;
Workbench does not establish in-game behavior. Live deployment/game/saves/reload/network/push pending.
