# BuildWorks: verified continuation baseline and dependencies

## QA .100 — automated checks passed; native acceptance remains open

Native .99 refused before plan/terrain: bounded4096 search found minimum dry span18.5402069m.
.100 changes both native terrain bounds only for explicitly marked ZDO terrain in the
original isolated QA world: +/-16m during preparation and reload. Original token,
world name/UID, Local closed server and no peers are required. Normal build omits the
patch; unmarked QA terrain retains +/-8m. The requested192x192m footprint is unchanged.
Both builds0/0, QA82, host and localization853 passed; old99 fails the new regression guard.
This QA terrain requires the QA binary after reload; it is not a product terrain feature.
Native backup precedes terrain changes; supplies and previous receipts are never reset.
CoffeeTable construction and world-reload acceptance remain open.

## QA .99 search follow-up

Native .98 enabled noon but found no qualifying192m yard within +/-1024m; it refused
before writing a yard plan/receipt or changing terrain. .99 expands only the bounded
search/plan extent to +/-4096m (4225 candidate centres); size, dry Meadows and native
height limits remain. Both builds0/0, QA80, host, localization853 and affected format
passed. Native .99 and imported construction/reload remain open. Earlier statuses are history.

## Current evidence 2026-10-08 — QA yard .98 source verified; native creation pending

.97 native QA confirmed stations, no supply reissue, CoffeeTable import/edit/save/reopen
and cancellation. Construction refused in spawn protection; after moving out, the
workbench was out of range. No imported construction/reload success is claimed.

.98 adds an explicitly requested 192x192m distant yard and midday only to the isolated
QA build. Back up native world files before terrain changes, recheck session/ground/
objects after waits, protect crossing objects, and use separate one-shot yard/station
receipts. Same world/token/supplies retained. Both builds0/0, QA80 plus compiled guards,
HostContract, Localization853, formatting and independent/Ponytail review passed.
verify unavailable. .98 installed with matching DLL hashes; all32 existing QA files unchanged. Native yard acceptance pending.
Details and exact test sequence: docs/testing/qa-yard-0.19.98.md (Russian duplicate exists).
Earlier checkpoint/deployment paragraphs below are historical.

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


.95 update: .94 created isolated identities and reached world entry; provisioning refused
identity matching during native respawn. .95 waits for respawn/nonzero ID; source checks PASS,
QA .95 installed after exit, QA file hashes unchanged. Keep saves; grants/stations/build/reload pending.

**English** | [Русский](continuation-audit-2026-10-07_RU.md)
Current .94 update: QA .93 was installed; first launch reproduced an early platform getter NRE
before QA identities/grants/stations. The .94 guard and regression pass; QA .94 is installed/hash-verified.
Retry ONLY manager TerrainRamp-1.0-Test Modded; native construction/reload remain pending.


Audit: 2026-10-07, updated 2026-10-08; current source checkpoint .97. This is not full product acceptance.
Read AGENTS, PROJECT_CONTRACT, current HANDOFF and docs/ROADMAP. CONTINUE_PROMPT.txt targets
.13; current-pass targets the historical .56 catalogue, not imported construction acceptance.

Same-day addition: [two real external files](testing/external-path-2026-10-07.md) pass reviewed import/
catalogue → editor/edit/support → disk reopen → JSON export/reimport → world renderer with installed prefabs.
252 UI screenshots +2 real-building images and independent reviews PASS. Product DLL was .87 then, installed .82.
Native construction/world reload/network remain untested.

[Checkpoint .88](testing/missing-model-0.19.88.md) preserves missing foreign records and unknown absolute
scale basis in Store15/JSON6; the actual editor edits/saves/reopens incomplete blueprints. Markers supply
no invented geometry. World planning refuses pending basis; full restoration remains unproven.

[Checkpoint .89](testing/missing-catalogue-0.19.89.md) removes missing-card insertion blocks and makes
unknown costs explicit. Actual UI/controller/Undo/save pass; native Session producer has compiled wiring
proof only. Early native save boundary is researched; isolation from other plugin Awake remains unproven.
No safe-stand preparation is implemented.

[Checkpoint .90](testing/qa-save-isolation-0.19.90.md): QA-only root/cloud isolation and fresh Local identities
are source-prepared. BCL paths/compiled wiring PASS; native save unexecuted. Known bootstrap writer disabled
with explicit owner authorization; preflight PASS. Resources/stations/ready survival stand remain open.

Checkpoint .91 adds a loaded Local identity/closed survival gate to the QA coroutine: exact world UID/profile ID/
ZDO player ID and names, no peers/open server/cheats. 25 BCL receipt/identity cases and compiled wiring PASS.
Native loaded lifecycle remains unexecuted; no resource/item grants or stations yet. This source-only check does
not require repeating accepted historical game checks. Installed .82 remains unchanged.

Checkpoint .92 implements QA-only one-shot actual-native materials/knowledge from7 pinned files.
Started receipt before grants, exact stack/prefab delta, profile save before complete; partial/replay never reissues.
25identity+11once+13stack BCL PASS; native compiled wiring only, no actual grants/save executed.
Physical stations/external staging/ready stand remain absent; installed .82 unchanged.

## Verified baseline

Source/artifacts initially .85. Installed DLL is **.82**, only TerrainRamp-1.0-Test:
`40ADA20A5492855B79956E1EC712C1104B999AFFBDDB584B795532D4A6923DEC`; installed Geometry:
`DD9F2A795396853D0FFE8DFE1BD227E759AA395F21C58D79A3BA1A66212591EA`.
Default plugins contain no BuildWorks DLL. Source directory is not a Git repository;
the clean source-only mirror artifacts/github-alpha-0.19.33 initially points at `23d7b4f`.
No push, release, game launch or save mutation is part of this audit.

OstrixMemory session `01a08155-1418-7e42-8099-716fbd53cdf9` and MemPalace
`BuildWorks/code-tools/SocratiCode-index-root-2026-10-06` identified the parent **Default** index.
Live SocratiCode: green,26003 chunks, active watcher, graph1968/3480; catalogue search resolves actual code.
Filtered empty search and zero C# impact do not prove absence; rg confirmed callers. No indexing started.
An existing active full-cycle goal was confirmed by get_goal; create_goal refused a second unfinished goal.
Its text was not updated. A document edit is not a goal update.

## Evidence matrix

| Block | Implemented | Automated evidence | Owner/game evidence | Unfinished | Design only |
|---|---|---|---|---|---|
| QA preparation | .96 native persisted primary; async-write boundary; strict IDs | Before/after regression, QA72 BCL, builds/API/localization/format/review | .94/.95 menu and isolated world entry | .95 refused before grants on slot0/1; .96 installed, stand/build/reload pending | Chest if bounded backpack plan is exceeded |
| Editor | Transform/group/Array/Contour/Undo/save | Geometry/bridge/Unity252 | Older .41/.44 and later scoped series recorded as accepted; do not repeat wholesale | Scenes above512 | New guide tools |
| Exchange | Store15/portable6, .blueprint/.vbuild/ZIP, exact archives, separate contents options | Store353;209 parser-only;2 real files reviewed import/edit/support/disk/export/world renderer | Imported construction through reload unproven | Header dialects, RandMatSeed, some ZDO/inventory, export only our JSON/ZIP | Unspecified third-party formats |
| Scale/preview | Intrinsic prefab scale, nested XYZ, world50000; .87 supplied-item world appearance | Unity transforms; ordinary/native-holder/passive/heavy attachments | Particular giant lamp remains unproven | Native place/reload/FPS; unknown item effects | Further measured optimization |
| Supports/snaps | Independent supports, distinct pivot/placement, durable world points | Store/controller/Unity/layout | Older group pivots accepted; new world snaps unaccepted | Reload/reconnect/remote client | Quiet unselected markers |
| Missing prefabs | .88 records/absolute basis; editor open/save/reopen/export; .89 warning-card insertion and unknown costs | Store/actual Unity gestures/catalogue/Undo/disk; late normalization; atomic world refusal; Session producer HostContract only | None | Source/type/local bounds, mod names, replacement1→N and native entry-path acceptance | Explicit multi-object substitution |
| Items/authority | ItemStand capture/authoring/native spending; exchange retain/clear; .87 bound supplied item | .86 28 inert cases; .87 refuses changed/construction-spent bindings without fallback | Native placement/drop/reload/network unaccepted | Containers/armour/free items, placement choices, server admin authorization | Full admin restore |
| Capture/heavy | Screen rectangle/manual edits, world50000 | Geometry/layout/shared-template Unity | Older capture partially accepted; no heavy imported construction | Area+height bounds, editor/capture512, FPS/prepared test world | Planned building/terrain integration |

Original owner JSONL was read: 2026-10-01 18:55:35Z and 2026-10-03 17:18:33Z/19:06:57Z
contain both scoped confirmations and remaining experimental-point defects. They do not prove .86
import/save/reload. Historical assistant readiness statements are not runtime evidence.

## Ordered dependencies

1. .86 supplied ItemStand properties/counts → .87 ordinary/native-holder/passive/heavy world projection of own
   variant/quality. Confirmation binds own item reference/native packet; changed/construction-spent items pause
   without selecting a replacement. Remaining visuals do not shift after spending earlier items. Inert fixtures only.
2. Small external full cycle: typed dialect headers/source binding/material variation → shared placement
   frame → prepared file/resources → edit/place/cancel/reload. Never globally shrink to hide scale errors.
3. Missing/heavy editor: .88 retains unknown scale/bounds and exact records; editor allowed, world blocked
   pending model resolution/replacement → metadata/mod cards/replacement1→N/remaining entries →
   budgeted/cancellable scenes above512 and measurements.
4. Independent placement contents choices, legacy inventories/armour/free items; ordinary supplied state
   vs server-authorized admin restoration. Text/design differ from wear/fuel/inventory; new network identities.
5. Area plus upper/lower capture volume, intersection/loaded-area rules and manual list; review visuals first.
6. Prepared disposable survival test profile/world/character, no manual material gathering; small/heavy
   imports, missing mods, failure/cancel/resource accounting, reload and host+remote client. Main saves untouched.
   Planned construction and terrain integration remain later work.

Fresh .85 checks: Release/no-restore, Geometry, Store353, Localization848, bridge, layout14, host and deploy guards PASS.
Sandbox blocked NuGet/atomic writes/Roslyn pipes; the same checks were repeated outside it. Long artifact
TEMP caused a path failure; Store used normal short TEMP. Six existing converter whitespace diagnostics
were fixed without semantic changes. verify is absent in both skill roots and PATH; direct checks/reviews apply.
Workbench runs actual editor/adapters on inert fixtures; native CUA control is disabled. No Workbench/DLL/parser
result establishes game construction, reload, authority or no-dupe proof. A prepared survival world/character
does not yet exist; old .80 instructions require manual setup and do not satisfy this request. Installed .82 stays distinct.
