# BuildWorks current development handoff

**English** | [Русский](HANDOFF_RU.md)

## Checkpoint 0.19.67 — usable file exchange and exact foreign scale; test deployment

Editor and Hammer share a folder/file browser, output filename/location and native JSON/ZIP choice.
Import retains a separate original copy; export is atomic and never overwrites existing files.
Results remain open with the full path and Copy path. Errors retain inputs and allow retry;
loading a folder removes stale selectable rows. Modals block world input and stay above hints.
Foreign PlanBuild/vbuild scale is absolute prefab-root localScale, converted once to the native
relative multiplier on the Unity thread. Missing/invalid prefab bases reject the whole preparation;
raw source bytes/scales stay untouched. Derived thumbnail identities include actual prefab scales.

Release zero warnings/errors; 803 Localization keys, Store, 108 Geometry, six Bridge,
12 WorldLayout, HostContract, deployment guard and format PASS. Independent correctness/Ponytail PASS.
Isolated Unity: 180 states plus actual file selection, locked-file error/retry, named ZIP/collision,
non-unit prefab visual size and modal close raycasts. gameHostTested=false; no game/save changes.
The verify helper is absent; direct checks and independent review were used.
Installed only in TerrainRamp-1.0-Test through the guarded script, SHA-256 verified for both DLLs.
[Player checks](docs/testing/catalog-exchange-0.19.67.md). No Valheim launch or save changes.
Full opaque foreign-data preservation, heavy interactive scenes
above 512 parts and world-support reload/network acceptance remain unfinished; the goal is active.
Ostrix extended the goal with ordinary items and horizontal/wall/armour stand contents;
the editor/format/world requirement is recorded in blueprint-format.md, not implemented.

## Source checkpoint 0.19.66 — support tools / scroll; not deployed

Hammer category/actions/resource scroll sensitivity is 132. Support Create/Delete/Reset centre
reuse the existing Space/F3 Tools menu; tree rows represent existing supports only. Delete uses
the currently selected support and preserves retained part selection. Search-hidden commands
cannot execute through number keys. The inspector keeps pose/name and centre/placement properties.

Release: zero warnings/errors; HostContract, 784 Localization keys, Store, 108 Geometry,
six Bridge, 12 WorldLayout, deployment guard and whole-project format checks PASS.
Independent correctness/Ponytail PASS. Final isolated Unity: 156 states, 2026-10-06T13:08:30Z,
native support Tools clicks/Undo PASS; gameHostTested=false. `verify` helper unavailable;
direct tests/diff/review used. ExternalBlueprintLibrary changes are whitespace only (`diff -w` empty).
First sandbox Unity attempt failed license198; unsandboxed run used the existing license.
Immediate same-frame popup test was corrected to wait one UI frame, without disabling raycast checks.

Owner .65 feedback: target XYZ snapping works, previews appear; category scrolling slow,
manual file paths / transient export feedback inadequate, tree action rows unwanted.
[Updated conversion contract](specs/roadmap/blueprint-format.md): foreign-to-native without lost
data, exact prefab scale semantics, no guessed shrink/clamp. This is pending implementation;
current v11/v2 still cannot preserve/apply every foreign object/header field. Source file archive
alone is not behavioral compatibility. Arbitrary mesh imports/terrain execution remain out of scope.
Installed .65, world/save files unchanged. No source push (await current explicit authorization).

## Checkpoint 0.19.65 — library UI, exchange and editor snapping

Installed in TerrainRamp-1.0-Test via the guarded script on 2026-10-06.
Both DLL SHA-256 values verified; Default has no installed BuildWorks DLLs.
Final Unity evidence: 2026-10-06T12:10:16Z; game was not launched.

Hammer categories/actions now use clipped native scroll views. External parsing runs
off the main thread; snapshots and thumbnail images are reused. Paired bounded PNG
previews in ZIP libraries are read without extracting packages. Cards place on one
click; the menu exposes Import file and Export file. The editor has the same exchange
buttons and a pinned + Support action above the object tree.

File import keeps a separate copy in BuildWorks/imports. Export writes a new portable
.buildworks.json in BuildWorks/exports without overwriting inputs. Accepted external
documents export through detached identities; missing game prefabs do not block data
export. XYZ scale, signs, supports and preview framing remain intact. Unchanged rounded
inspector fields preserve exact scale; explicit resizing preserves XYZ proportions.
Ghost placement and G reuse the target's full XYZ frame and retain the chosen grip
through wheel turns and confirmation.

Release: zero warnings/errors. HostContract, Localization (784 keys), Store,
108 Geometry, six EditorBridge, 12 WorldLayout and deployment guard checks PASS.
Independent correctness/Ponytail reviews PASS. Final isolated Unity: 156 states,
native exchange/support button dispatch, XYZ snap and exact-scale input PASS.
No Valheim/save/reload/network proof. Interactive budget remains 512; larger valid
documents can be exported but are not yet interactive. Unknown foreign data is still
rejected, never silently discarded. [Player checks](docs/testing/catalog-exchange-0.19.65_RU.md).

## Source checkpoint 0.19.64 — independent support editor; not deployed

Independent supports now have their own tree branch and inspector. G/R and pointer
drag edit only the selected support; S does not interrupt rotation. Name/pose apply
as one Undo operation. Transform centre is transient; placement default is separately
persisted. F frames the support; model-space handles have a 0.4 m minimum and the
inspector uses native scrolling at small resolutions.
Release has zero warnings/errors; 108 Geometry, Store/Localization, six Bridge,
12 WorldLayout, HostContract/Deployment/format PASS. Independent correctness/Ponytail
PASS. 156 Unity states PASS (2026-10-06T10:57:09Z, gameHostTested=false), including
native clicks, pointer drag, numeric G/R, unchanged parts and Undo.
[Six player acceptance cases](docs/testing/free-supports-0.19.64.md).
Quiet unselected viewport markers and world support persistence/reload/network remain
pending. Installed .56, worlds and saves unchanged. This is source/isolated Unity
proof, not native game acceptance.

## Source checkpoint 0.19.63 — Hammer support frame; not deployed

Saved placement support now rebases Hammer visuals, collider bounds and native sockets
with the same inverse pose used to recover the geometric root before placement plans.
Nested/mirrored XYZ-scale Unity visuals agree with root math; 50/100/200% support-offset
compensation, actual Session composition/commit/history and legacy fallback pass.
Placement origin remains separate from world-F9 bounds pivot and geometry/socket filters.
Release, 108 Geometry, six Bridge, 12 WorldLayout, Store/Localization/HostContract/
Deployment/format checks and fresh 144-state Unity PASS (2026-10-06T10:17:52Z).
Native Quaternion.Inverse is checked inside Unity, not simulated outside the engine.
Independent correctness and Ponytail reviews PASS. Tree/G/R/explicit transform-centre and world durability
remain; installed .56 and saves unchanged. No Valheim placement/reload/network proof.

## Source checkpoint 0.19.62 — support storage/exchange; not deployed

Store v11 / portable v2 preserve root support pose/name/ID and placement default,
with v10/v1 migration, strict version/field validation, shared IDs, bounded poses,
legacy-preserve vs explicit-clear and atomic rollback. Detached import copies remap
supports/default; real Controller save/open roundtrip passes, including rotation.
108 Geometry, six actual EditorBridge, Store(+353 archive), Localization, HostContract,
WorldLayout, DeploymentChecks, dotnet format and read-only correctness/Ponytail PASS.
Fresh 144-state Unity PASS (2026-10-06T10:00:04Z, gameHostTested=false).
Next: placement-frame composition + tree/G/R/explicit transform centre. World sockets,
reload/network, heavy-scene rendering and export UI remain. Installed .56/saves unchanged.

## Source checkpoint 0.19.61 — support data foundation; not deployed

Independent root-owned supports, pose/default and separate selection scope now
live in the document; validated atomic history and root-space insertion pass.
Controller copy preserves custom points/supports without aliasing. Store v10 and
portable v1 unchanged: no UI or durable support authoring is exposed yet.
Release/targeted tests, read-only correctness/Ponytail and 144 Unity states PASS
(2026-10-06T09:35:25Z, gameHostTested=false). Next: v11/v2 + tree/G/R + placement pose;
world persistence and heavy rendering/export remain. Installed .56/saves unchanged.

## Source checkpoint 0.19.60 — not deployed; development remains active

2026-10-06: [shared pick geometry](specs/roadmap/blueprint-render-performance.md).
512 walls: unique pick-array payload 50071344 → 107808 bytes, sync 550 → 223 ms
in single-machine samples; renderer count unchanged. Persistent exact mesh/matrix
cache preserves nested transforms; transient previews cannot grow it.
Release/targeted tests, independent review and 144 real-size Unity states PASS
(2026-10-06T09:15:01Z, gameHostTested=false). Budget 512 and installed .56 unchanged.
Owner approved free root-owned support frames; implementation/world persistence,
heavy batching and export remain. No game/world/save changes.

## Source checkpoint 0.19.59 — not deployed; development remains active

2026-10-06: [bounded rendering measurements](specs/roadmap/blueprint-render-performance.md)
share scene-owned materials only for document visuals; tinted previews remain isolated.
512 actual walls: 10752 → 2 materials, sync 906 → 520 ms in single-machine samples.
Renderer hierarchy and most memory remain; interactive budget 512 stays unchanged.
Release/targeted checks and fresh 144 Unity states PASS (2026-10-05T21:49:45Z,
gameHostTested=false). Free supports/heavy-scene batching/export/world acceptance remain.
Installed .56 and worlds/saves unchanged. Earlier sections are historical.

## Source checkpoint 0.19.58 — not deployed; development remains active

2026-10-06: [native signs](specs/roadmap/blueprint-signs.md) now follow each placement
step and use native permission-gated SetText; capture and legacy workspace preserve
raw text. Exact foreign `sign` info is decoded conservatively. Archive 109/353 format
compatible; host prefab/world behavior remains separate. Release/targeted checks and
144 Unity states PASS (2026-10-05T21:14:51Z, gameHostTested=false); independent review PASS.
Owner clarified [free support frames](specs/roadmap/blueprint-support-frames.md): researched
only, no new point-authoring UI/world persistence yet. Heavy rendering/export UI remain.
Installed .56, worlds/saves unchanged. Older checkpoint sections below are historical.


## Source checkpoint 0.19.57 — not deployed; development remains active

2026-10-05: [format and large-construction contract](specs/roadmap/blueprint-format.md)
records v10/sign/socket payload, portable v1, strict conversion and measured CPU
indexes. 96/353 archive entries now pass format checks; prefab availability is separate.
Storage 50000 parts is not rendering capacity: the synchronous budget stays 512.
Sign/socket world placement is explicitly blocked until native integration is ready.
Installed .56, worlds and saves remain unchanged. The .56 figures below are historical.

## Verified test build 0.19.56

2026-10-05: approved construction-only external library adapter reads .blueprint,
both .vbuild dialects and inert ZIP without extraction. External Edit opens a detached
draft; one editor card click inserts the whole construction; Hammer Place copy saves
a native copy before existing per-piece world validation. Originals remain untouched;
Store v9 unchanged, no terrain execution or foreign DLL/assets installed.

Release, Geometry(105), Store, Localization, EditorBridge, WorldLayout, DeploymentChecks,
HostContract and 144 exact-source Unity UI states PASS; independent correctness and
complexity review complete. verify skill absent. Hub activation resolved exit198;
fixture local-name collisions CS0128/CS0841 fixed. UI includes import/edit/save-copy/
insert/Cancel and thumbnail updates without resetting picker/page.
Final whitespace verification also passes; old Scene/Gizmo and new reader spacing fixed.
.56 and source ZIP installed only TerrainRamp-1.0-Test with verified SHA256.
Default/worlds/saves unchanged. gameHostTested=false; no Valheim launch.

353 library entries: 62 format-compatible, 291 blocked. Displayed reasons: 104 limit,
124 #SnapPoints, 63 object data. Reasons overlap: 253 exceed128, maximum39,487.
Large-size/snap/object-data support and BW-04/BW-05 full exchange remain pending.
See specs/roadmap/blueprint-exchange-research.md. No library/package assets enter Git.

## Historical verified source candidate 0.19.55

2026-10-05: .54 owner feedback accepts catalog checks 2,3,5,6,7; changes 1,4,8
and adds focused gizmo/save checks 9,10 in [the single current series](specs/roadmap/current-pass.md).
Catalog defaults to 80% contents and fits between header and hints; short viewports
temporarily compact hints and restore their expanded preference afterwards.
Lighting is independent from Decor; Furniture has seating/beds/storage/tables/rugs/other.
Material filtering uses actual recipe ingredients (including mixed recipes), basic
wood first; building order follows resource tiers, not personal historical unlock order.
The current native hammer inventory remains authoritative; no recipe unlock bypass.

Details show resource icons/counts plus the construction station or explicit none,
separate from the StationExtension target used for upgrade grouping. Long cost lists
receive native scrolling. Saves request name and existing/new category; Cancel/Esc
do not write, SaveAndExit retains its success-only close path. Handle-family buttons
are labelled Show, close settings immediately and survive the first part click;
same-family click restores Combined. Both sizing modes respect the chosen family.

Release build, Geometry(105), Store, Localization, EditorBridge, WorldLayout,
DeploymentChecks, HostContract pass; 737 paired keys / 656 static references.
Unity 6000.0.61f1 checks 144 cases at 1280×720,1920×1080,2560×1440,3440×1440
and 100/120/140%, including save metadata. Actual overlay raycasts and source hashes
are checked. Fixtures do not establish Valheim inventory/placement acceptance.
Independent correctness review PASSED. ponytail removed duplicate catalog rebuild.
No verify skill is installed; direct checks and independent review were used instead.
Final Workbench proof: 2026-10-05T18:01:03Z. Installed only TerrainRamp-1.0-Test
using Deploy-TestBuild.ps1 with Valheim closed; SHA256 matches both built DLLs.
BuildWorks: `67135798B6E5C14FFF52D430CE8F9A7CEFF7A10204F39C51839D1438242F3C5C`.
Geometry: `9C29006B05007C608DEDCB0EFCF275B0F04F9673EE423FD94DCFDF6C4C258D86`.
Verified .54 DLL backup: `artifacts/checkpoint-0.19.55/preinstall`.
Valheim not launched, saves untouched; owner game acceptance remains open.

External research: downloaded BiomeBlueprints 1.0.3 as data only; 353 files, 100
within 2–128 parts, all those have terrain markers and 27 have extended object data.
No third-party DLL/art installed or bundled. See
[exchange research](specs/roadmap/blueprint-exchange-research.md). Import/export
is not implemented; first-stage terrain/object-data scope awaits Ostrix's answer.
SocratiCode indexes BuildWorks through parent Default; no separate index is needed.

## Verified source candidate 0.19.54

2026-10-05: Ostrix accepts the .53 chair series. Catalog implementation uses
the real View/Controller and native hammer inventory: Atlas/Quick, immediate
card placement, five sections, building families, named cards, native icons,
material/source pickers and actual StationExtension target-station groups.
Unknown mod categories stay in All/search. Favorites/recent are editor-session
only; filters are shared between views and separate between tabs. Reserved
user category names cannot collide with navigation IDs. Own star icon added;
eye/reset/close reuse existing art. No external plugin/dependency added.

Release zero warnings/errors. Geometry (105), Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks and HostContract pass (Steam build 25185596).
717 paired keys / 646 static references, embedded DLL resources verified.
Unity 6000.0.61f1: 132 states, 2026-10-05T13:56:26Z; 1280×720, 1920×1080,
2560×1440, 3440×1440 at 100/120/140%. Native overlay raycasts run at matching
GameView sizes; camera mode is screenshot composition only. Original camera
controller regressions also pass. Long preview text is bounded. Screenshot
items/stations are fixtures, not a game inventory/correctness claim.
Independent correctness review PASSED; ponytail-review: Lean already.
Valheim not launched, saves untouched. Installed only in TerrainRamp-1.0-Test
with Valheim closed via Deploy-TestBuild.ps1; both SHA256 hashes match.
Separate preinstall DLL backup was not created because the caller used the wrong
profile-parent path. The deploy script's transactional guard and installed hashes
passed; .53 source remains recoverable at checkpoint b00af77, not as a saved DLL pair.

BuildWorks SHA256: E52529D4E9E76CBDD19C58609724AC02752BF78C682856AAA6FE76765DA58A8A
Geometry SHA256: 2AB3EAD788D610EE5DED3DA3C64C1DE98FC9EAC52257920950372CDF849D26B2
Owner acceptance: one [catalog series 1–8](specs/roadmap/current-pass.md).
.53 is archived/accepted. No BW-02/curves advance.

## Historical verified candidate 0.19.53

Ostrix accepted .52 checks 1–2; check 3 isolated the small yellow experimental
mesh-corner dots on the actual wooden reclining chair `piece_chair02`, not the
ordinary ivory bounds helpers. Native `Custom/Piece` uses `_RippleDistance=0.05`
on this chair: shader displacement made the rendered corners differ from mesh
vertices. Ostrix approved exact geometry in the Blueprint Editor.

Owned editor preview materials now set only Custom/Piece `_RippleDistance` to
zero. Textures and colour noise remain; source/world materials, world/F9,
Store v9, costs and networking are unchanged. Other shader deformation is not
covered. Ordinary bounds points and the .52 experimental extraction are unchanged.

Release build: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks and HostContract pass (Steam build 25185596).
678 paired keys / 648 static references; embedded localization verified.
Format exits 0 with the existing workspace-loading warning. Unity 6000.0.61f1
passed 81 cases at 2026-10-03T19:44:28.2984593Z. Native piece_chair02 regression
checks 38 exact dots, real vertex/draw centres, point picking, helper magnet and
unchanged original materials. Maximum draw-centre error: 0.000000238 m.
Independent correctness review PASSED; ponytail-review: Lean already. Ship.
Valheim was not launched; owner runtime acceptance remains pending.

BuildWorks SHA256: E45F2942817A06679E2507D0361C681DEB23DBB9F6ECAF57720387F3CA19AD43
Geometry SHA256: 9280D140DC1DE633A14AE4C3633A9CDF14A2F862D1966CD7B13CED747B1E7626
Installed only in TerrainRamp-1.0-Test with Valheim closed via the guarded script;
both hashes match. .52 backup: artifacts/checkpoint-0.19.53/preinstall.
Source-only synchronization remains authorized; no binary release published.
Owner acceptance: one [chair series 1–2](specs/roadmap/current-pass.md).
The .52 series is archived; no BW-02/curves advance.

## Historical verified candidate 0.19.52

Ostrix accepted .51 checks 3–6 on 2026-10-03; 1–2 reopened ordinary helper restoration
and experimental rendering. Ordinary bounds corners/middles/centre and generated
snap middles are restored separately from the opt-in mesh-corner set. Amber native
and ivory ordinary signs remain. Extra sharp corners use small translucent warm dots,
depth fade and a selected collar, not the large knot texture. Exact coordinate
overlaps within 5 mm of ordinary/native points receive no second mark.

Experimental capture is lazy per visual. Welded feature corners require a turn of
at least 45°; up to 128 real coordinates are sampled spatially. Corners strictly
inside closed connected components are removed, including combined meshes.
Open/non-manifold components conservatively do not hide corners; large meshes can
omit some corners. The experimental range stays frozen during transform previews.
Save/Array use their unchanged historical anchor layout. Source selection cannot
relabel an ordinary target after alignment; screen-mode dot picking has its own radius.
Accepted .51 magnet/depth controls, world/F9, Store v9, costs and networking stay unchanged.

Release: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks and HostContract pass (Steam build 25185596).
678 paired localization keys / 648 static references, embedded resources verified.
Format exits 0 with the existing workspace-loading warning. Unity 6000.0.61f1 passed
81 UI cases at 2026-10-03T17:58:28.2261582Z: separate/combined chair geometry,
buried/open/overlap behavior, sharp bend versus ordinary midpoint, actual screen
hit/miss, experimental source → ordinary target identity and legacy save/Array.
Two isolated chair renders at different distances use actual runtime drawing.
Independent correctness review PASSED after hit-radius and provenance fixes;
affected checks reran. Ponytail review: Lean already. No Valheim launch.

BuildWorks SHA256: BC066E78A81A9C8D84212C694E1C598A6D12D4FE1B6590AAC0CE8EF9BD2147A7
Geometry SHA256: 9280D140DC1DE633A14AE4C3633A9CDF14A2F862D1966CD7B13CED747B1E7626
Installed only in TerrainRamp-1.0-Test with Valheim closed via the guarded script;
both hashes match. .51 backup: artifacts/checkpoint-0.19.52/preinstall.
Source-only synchronization remains authorized; no binary release published.
Owner acceptance is pending the single [points series 1–3](specs/roadmap/current-pass.md).
The .51 series is archived; accepted checks are not repeated. No BW-02/curves advance.

## Historical verified candidate 0.19.51

Ostrix accepted .50 checks 1–2, 4–5, 7 and 10 on 2026-10-03. This focused fix
addresses incorrect mesh helpers, unreliable G capture and inaccessible View Z.
Bounds corners and fabricated snap middles are no longer shown/used by editor
helpers. Feature-edge topology yields actual corners; 128 coordinates per visual
are spatially sampled, with 512 helper/512 native selection budgets. The detailed
opt-in set includes shallower turns, uses the same ivory sign and stays off by
default. Large meshes can omit corners; overlapping component vertices remain real.

Legacy Array/span/save anchor calculations explicitly retain their previous set.
G prioritizes actual pointer intent (24 px, up to 2 m from a source) rather than
unconditionally preferring a native pair; unaimed capture stays 0.55 m/0.7 m retained.
Adjacent targets can take over; the captured target stays visible at preview limit.
The approved View Z depth badge uses up/down for away/towards camera, preserving
G Z exact numbers, operation-start snapshots, one Undo and full Escape rollback.
World/F9, Store v9, game costs, permissions and networking are unchanged.

Release build: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks and HostContract pass (Steam build 25185596).
678 paired keys / 648 static references; embedded resources verified.
Format exits 0 with the pre-existing workspace-loading warning.
Unity 6000.0.61f1 passed 81 screenshots/UI cases at
2026-10-03T16:42:15.7114349Z, including compound-chair exact corners, detailed-set
cache/provenance, >24-target winner visibility, aimed helpers/adjacent switching,
native filtering, depth drag/numeric/cancel/Undo and historical serialization.
Independent correctness review PASSED. Ponytail found obsolete range state;
it was removed, tests updated and affected checks rerun successfully.

BuildWorks SHA256: E010B86605AC641A525B2112231B0E262193C1ABDDBFD5466DE0CCCDD4AB9EE5
Geometry SHA256: 9280D140DC1DE633A14AE4C3633A9CDF14A2F862D1966CD7B13CED747B1E7626
Installed only in TerrainRamp-1.0-Test with Valheim closed via the guarded script;
SHA-256 matches both built DLLs. .50 backup: artifacts/checkpoint-0.19.51/preinstall.
Source-only GitHub synchronization is authorized; no binary release was published.
Valheim was not launched; game readability/native mesh behavior remain owner checks
[1–6](specs/roadmap/archive/pass-0.19.51.md). The .50 list is archived; no BW-02/curves advance.

## Historical verified candidate 0.19.50

Ostrix approved this focused revision on 2026-10-03 after the .49 game report.
Model-relative 3D sizing is now the editor default; screen mode remains available.
Native points keep amber clasps; ordinary helpers use a new ivory Nordic joining
knot. Selected A/source gets a carved locking collar without losing provenance.
Original model materials, near-cursor reveal and depth feedback remain.

One Native/Native+ours header filter owns G source choices, Q/E and target scans.
The old independent helper toggle is removed; furniture without native sockets
requires Native+ours for helpers. Explicit helper A is an intentional exception.
Optional B is in Attachment, not the primary header; A remains the pivot and
A→B defines working X. Geometry feature endpoints are opt-in/off by default,
bounded at 32 per visual and 64 extras per selection, with a small amber helper
knot. They never change prefabs or serialized blueprint anchors. Changes to this
set clear remembered source indices; preview retains frozen point provenance.
Geometry/B controls are disabled while transforming.

Native catalogue types/materials resolve localized labels by stable ID; external
mod/source names and user categories are preserved. Three individual transparent
1254 px PNGs, exact prompts and game-versus-concept limitations are in
[the artwork record](docs/artwork/snap-point-assets.md). Five obsolete point PNGs
remain historical source files but are excluded from the shipped assembly.
No world/F9, Store v9, costs, input bindings or network behavior changed.

Release: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks and HostContract pass (Steam build 25185596);
677 matching localization keys, 647 static references and embedded resources
verified. Format exits 0 with the existing workspace-loading warning.
Unity 6000.0.61f1 passed 81 actual UI cases at 2026-10-03T15:23:00.0325437Z,
including exact resource imports, native/helper overlap, selected collar,
default sizing, shared source filter, disabled pending controls, optional B,
experimental coordinates/targets/cache reset, frozen preview provenance and
unchanged serialized anchors. Read-only correctness and Ponytail reviews pass.
Reviewer-found stale source index and preview colour regressions were fixed
and covered before deployment.

- BuildWorks SHA256: 42242C80D2D8E508EC0E9CA43DF285365737C741CF7F517C41DEFDE381AD16D7
- Geometry SHA256: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Installed with Valheim closed only in TerrainRamp-1.0-Test through the guarded
script; SHA256 matches. Previous .49 DLL pair is retained locally at
artifacts/checkpoint-0.19.50/preinstall. No game launch, save mutation,
other-profile deployment or new binary release. Real Valheim readability/input,
costs/refunds, reload and networking remain separate owner gates.
BW-01 awaits the single [owner series 1–10](specs/roadmap/current-pass.md),
starting with 1–3. The .49 list is archived, not a second answer list.
Stop at owner feedback.

## Verified candidate 0.19.49

Ostrix approved the point-purpose workflow and two sizing modes on 2026-10-03.
Editor points now have only native gold and generated blue provenance. Default
near-cursor reveal, persistent selected A, manual All/Native-only and depth dimming
replace the idle multi-colour cloud. Existing thematic PNGs are reused, not replaced
by a concept. Explicit helper A remains visible/usable under Native-only; hidden
helpers are excluded from Auto/Q/E and target scans.

LMB selects A without starting a drag. The gizmo moves to A; G uses A as source
grip, R/S use it as pivot. Target capture never replaces A. Optional B defines
working X along A→B; coincident points are rejected and antiparallel direction
uses deterministic original-up roll. This is not a guide/curve or automatic edge
alignment. Local/World/View/Edge frames and their visible hints agree; repeat axis
and Esc restore the original working frame. Operation-start numbers, atomic Undo,
MMB behavior, Array/Contour and document/store format remain unchanged.

Manipulator → Size switches fixed screen pixels or model-relative 3D dimensions.
Model sizing uses rotated-local renderer dimensions and actual part scale, not
camera distance/FOV/projection. Reference freezes during previews; independent
size sliders still work. Drawing and projected point picking share the same size.
World/F9 retains its old sizing, colours and occlusion contract.

Release: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks and HostContract pass; 672 matching localization
keys and 643 static references, embedded resources checked. Format exits 0 with
the existing workspace-loading warning. Unity 6000.0.61f1 passed 81 UI captures at
2026-10-03T13:18:22.5621315Z, actual native-model near/far renders, A/B/axis/reset,
visible frame text, Native-only choices, fixed model metres/FOV/projection,
projected hit areas and appended helper-A provenance. Independent correctness
and Ponytail reviews pass; the final text blocker was corrected and re-tested.

- BuildWorks SHA256: 317B3D15F5B0AFF315D152C03D928BCEEFE9017550093B1EA2609F0FA40EECBC
- Geometry SHA256: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Installed with Valheim closed only in TerrainRamp-1.0-Test through the guarded
script; hashes match. Previous .48 DLL pair: artifacts/checkpoint-0.19.49/preinstall.
No game launch, save change, other-profile mutation or binary release. Workbench
substitutes external host/physical input; actual Valheim readability, costs/refunds,
reload and networking remain owner gates. BW-01 awaits the single numbered
[current series 1–10](specs/roadmap/current-pass.md), starting with readability 1–3.
The .48 list is archived, not a second answer list. Stop at owner feedback.

## Verified candidate 0.19.48

Ostrix approved trying compact nested point signs on 2026-10-03. Six individually
generated transparent PNGs replace .47 artwork: native clasp, corner, midpoint,
centre, pin and active brackets. Native/helper screen radii are 16/9 px; exact
coordinates stay unchanged. A pin is appended state, not a new native socket.
Pin/active overlays preserve original typed points; idle hover names the point.
Scale and Alt-copy priority also gate that caption, with a controller regression.

Snap RGB no longer exceeds white. Embedded icons use mipmaps/trilinear filtering;
Workbench imports keep the same 1254 px dimensions without compression or NPOT
resizing. The blue inside elbow is registered at (0.43,0.42); other pivots (0.5,0.5).
[Assets, exact prompts and concept-to-runtime explanation](docs/artwork/snap-point-assets.md).
These are raster sprites, not vectors. World F9, snap coordinates, input bindings,
costs, Store v9, Array/Contour and the accepted .47 HUD layout are unchanged.

Release: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks, HostContract and built localization pass
(654 keys, 631 static references). Format exits 0 with the existing workspace
loading warning. Unity 6000.0.61f1 passed 81 UI captures at
2026-10-03T10:35:17.6722987Z, plus actual wood overlap/pin captures.
Native contrast and helper colours remain visible at coincident coordinates;
the pin render has 10 contrast pixels under the neutral-colour palette test.
Independent correctness and Ponytail review pass after the hover-priority fix.

- BuildWorks SHA256: 1C4D56285740E5CDCEA301698B55BD19810A32FF9468C3CEBDB7BE263335B91F
- Geometry SHA256: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Installed with Valheim closed only in TerrainRamp-1.0-Test via the guarded script;
hashes match. Previous .47 DLL pair and artwork are backed up locally at
artifacts/checkpoint-0.19.48/preinstall and artwork-before. No Valheim launch,
save change or binary release. Workbench substitutes the external host/input;
game readability, physical input, refunds, reload and networking remain unproven.
BW-01 awaits the owner: reply only to point 1 of the
[current pass](specs/roadmap/current-pass.md), then resume 2–8 after visual approval.

## Verified candidate 0.19.47

Owner screenshots of .46 reopened two visual defects. Editor snap signs now
have larger screen radii (native 21.25 px, helper 11.25 px at default settings)
and a higher idle opacity floor (0.96/0.88); combined manipulator idle opacity
is 0.64. Artwork, pivots, source coordinates and selected materials are unchanged.
Hover stays stronger; dragging dims unused handles. World F9 is unchanged.

The blueprint HUD measures wrapped title/description height and places native
resource/station cards below that header with a 16 px gap. Native thumbnail size,
labels, costs and availability logic are retained. Parent, sibling, geometry and
text wrapping restore on ordinary selection/teardown. A regression covers the
owner's two-resource/one-station panel and a nested native header.

Release: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks, HostContract and the built localization catalog
pass (647 keys, 624 static references). Format exits 0 with a workspace-loading
warning. Unity 6000.0.61f1 passed 81 UI captures at
2026-10-02T20:14:19.1510813Z plus focused idle-art/HUD-layout captures.
Independent correctness and Ponytail review found no blockers.
Host/physical input are substituted on the Workbench; actual Valheim readability,
native hierarchy, costs/refunds, reload and networking are not proven by it.

- BuildWorks SHA256: 7D3355E79D700719359FEB85B5E7C4C98BC50179BE0260A372159D43E5807303
- Geometry SHA256: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Installed with the game closed only in TerrainRamp-1.0-Test through the guarded
script; hashes match. Previous .46 is backed up at
artifacts/checkpoint-0.19.47/preinstall. First inspect readability and HUD spacing
in [the same numbered series](specs/roadmap/current-pass.md), before continuing
the full 1–8 run. No Valheim launch or binary release. BW-01 stays awaiting owner;
later stages and curves remain gated.

## Verified candidate 0.19.46

Ostrix approved artwork and controls on 2026-10-02. Six individual transparent
PNGs are implemented: native clasp, corner, midpoint, centre, Shift pin and
active source. Coordinates are unchanged; a coincident helper is picked in the
centre, its native socket on the outer rim. Selected model materials stay untinted.
[Assets and generation prompts](docs/artwork/snap-point-assets.md).

G follows scene surfaces/ground like the placement ghost. Auto is default;
Q/E cycles native-only, with opt-in helpers and explicit furniture fallback.
Attachment previews/chooses real points without confirmation; an explicit
source survives G → R → G. Native pair priority, 2 m previews, 0.55 m capture
and 0.7 m winner retention are implemented. Accepted R/S/axes, Esc and one Undo remain.

Blueprint HUD uses native resource cells and station icons. Resources aggregate;
stations deduplicate; missing availability accounts for paid requirements.
Cards widen/wrap; long lists have screen-bounded scrolling, including an active
Canvas resize. Ordinary selection/errors/teardown restore original cell parents
and geometry. Display-only: marker m_resources stays empty; costs are not changed.

Release has zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks and HostContract pass. 647 matching keys and
624 static references; embedded resources verified. Format exits 0 with a
workspace-loading warning. Fresh Unity 6000.0.61f1: 81 captures and actual
Controller/EventSystem, imported game meshes and HUD adapter checks pass at
2026-10-02T19:44:17.4464874Z. Independent correctness/complexity review has no
blockers. External host/physical input is substituted; native HUD hierarchy,
in-game readability, costs/refunds, world reload and networking remain unproven.

Artifacts:

- BuildWorks SHA256: 5EABBC77C5D0ED0D0F7DC9A8F9CA56C5541F3293E723108F72F42368FA8AE9F6
- Geometry SHA256: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Installed only in TerrainRamp-1.0-Test with the game closed, through the
guarded script; hashes match. Previous .45: artifacts/checkpoint-0.19.46/preinstall.
Next: one [owner series 1–8](specs/roadmap/current-pass.md).
Accepted .45 evidence is preserved below and archived. BW-01 awaits game
acceptance; later stages and curves stay gated. No binary release is published.

## Verified candidate 0.19.45 — 2026-10-01

Active gate: BW-01 owner Valheim acceptance in TerrainRamp-1.0-Test.
Owner accepted .44 exact move/scale, MMB click versus drag, tool cleanup and UI
input ownership. This revision addresses the remaining visual, rotation and snap
feedback, not a new tool workflow. Selection keeps original materials and still
shows the combined gizmo immediately.

Plain gold native diamonds, compact blue corner circles and green midpoint crosses
retain their true coordinates. No coincidence suppression; native pin keeps its
gold rim with a purple accent inside. Pink centre, purple pin and white active
helper are explained alongside gold/blue/green in English and Russian.
Arrow/ring/scale artwork stays engraved. World/F9 sizing and Store v9 unchanged.

Choosing an axis resets mouse preview to the immutable action-start pose; typed
values replace mouse deltas. Q/E reevaluates attachment immediately at a stationary
cursor. Game targets native sockets, including ones inside their own mesh; Mesh
also offers generated helpers. Other parts still occlude native sockets.

World blueprint HUD description refreshes with language and shows aggregate
resources plus deduplicated stations. It reuses registry resource aggregation;
marker requirements remain empty, so no second aggregate cost is charged.
This text-only integration has compiled host-contract proof, not game HUD layout
or actual resource-consumption proof.

Release: zero warnings/errors. Geometry, Store, Localization (639 matching keys
and built resources), HostContract, EditorBridge, WorldLayout and profile guards
pass. Format exits 0 with a workspace-loading warning. Fresh Unity 6000.0.61f1:
81 captures and actual Controller/EventSystem checks pass at
2026-10-01T19:42:07.8611315Z, including coincident glyphs, blue/green shapes,
axis reset, same-frame Q/E, native interior socket and Game/Mesh midpoint checks.
Independent correctness and complexity reviews pass. Workbench physical input
is substituted; game acceptance, save/reload and multiplayer are not claimed.

Installed only in TerrainRamp-1.0-Test with Valheim closed through the guarded
script. Hashes match the tested build:
- BuildWorks: F1C0F506D4A2897A6435877E20175870FB6B8489486B11DDA73C54585AC93F3F
- Geometry: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Previous .44 pair: artifacts/checkpoint-0.19.45/preinstall.
Next: only steps 1–12 in [the current owner test series](specs/roadmap/current-pass.md).
Step 12 checks world language/resource display without placing a blueprint.
No game launch or binary release; source-only GitHub synchronization.

## Historical candidate 0.19.44 — 2026-10-01

Active gate: BW-01 owner Valheim acceptance in TerrainRamp-1.0-Test.
Selection now keeps a clear gold tint and immediately shows Combined handles,
without a first-G gate. G starts Surface Move; R/S/XYZ remain exact actions from
the original operation pose. Select keeps MMB click deletion and drag navigation;
additive selection takes priority over handles on an unselected part.

Editor gizmo is 25% larger. Native snap points are larger, brighter closed gold
diamonds with existing engraving; generated bounds/midpoints are smaller open
colored markers. Classification uses captured prefab provenance, not the name
or position of a point. The 128-part budget reserves all 512 native exterior
points before generated helpers. World/F9 sizing and document/store format are
unchanged. The purple pinned point stays visible above its engraving.

Release build: zero warnings/errors. Geometry, Store, EditorBridge, WorldLayout,
Localization (637 keys and built resources), HostContract and forbidden-profile
deployment checks pass. Format verification exits 0 with a workspace-loading
warning. Unity 6000.0.61f1: 81 captures; actual installed asset rendering,
Controller.Update/LateUpdate and EventSystem checks pass, completed
2026-10-01T16:57:24.9419102Z. Independent correctness review PASS; complexity
review found no actionable cuts. Workbench substitutes physical input:
game-host/save/reload/resource/network acceptance is not claimed.

Installed only in TerrainRamp-1.0-Test with Valheim closed through the guarded
script. Installed hashes match the checked build:
- BuildWorks: 9EAC47B84796A25C345FEA3D31737209B0B8E4E4B0B5F9832825D493578729AE
- Geometry: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Previous .43 pair: artifacts/checkpoint-0.19.44/preinstall.
Next: only steps 1–12 in [the current owner test series](specs/roadmap/current-pass.md).
Older test lists are frozen separately in testing-history.md; they are not an
additional answer series. No game launch or automatic binary release.

## Historical candidate 0.19.43 — 2026-10-01

Active gate: BW-01 owner Valheim acceptance. .43 supersedes .42 editor input,
not world/F9 controls. Select hides handles; G opens Combined, repeated G starts
Surface Move. R/S/XYZ edit with numbers relative to the original operation pose.
Ctrl+1–4 selects tools and cancels pending previews. MMB click removes a placed
part in Select/adding; drag, modifiers, other buttons or UI takeover never remove.
The adding ghost remains. Esc rolls back; ready-gizmo Esc hides retaining selection.

Six original editable Nordic SVGs and transparent exported textures add engraving,
metal gradients and rivets. Idle controls dim; screen proximity brightens; winning
hover/active control is emphasized. Filled scale has whole-silhouette picking.
Pinned point retains its purple meaning. Existing icon export/loader is reused.

Release zero warnings/errors; Geometry, Store, EditorBridge, WorldLayout,
Localization (637 matching keys/built resources), HostContract and forbidden-profile
deployment checks pass. Format verification exits 0 with workspace-loading warnings.
Unity 6000.0.61f1: 81 fresh UI captures, real installed asset rendering and actual
Controller.Update/LateUpdate plus EventSystem checks pass, completed
2026-10-01T15:54:50.0544740Z. Independent read-only review and final MMB follow-up
PASS; ponytail review finds no worthwhile new abstraction to remove.
Workbench uses substituted physical input; this is not Valheim OS-key interception,
save/reload, resource accounting or multiplayer proof.

Installed only in TerrainRamp-1.0-Test with Valheim closed through the guarded
deployment script. Installed hashes match the build:
- BuildWorks: C85E2247EA8167BA636A2154387496B4C92AB4B6C2DC028B4E2FA849DA6DE167
- Geometry: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Previous .42 pair: artifacts/checkpoint-0.19.43/preinstall.
Next: six focused owner steps in specs/roadmap/current-pass.md (Russian duplicate).
Source-only synchronization targets MaikiOS/BuildWorks main; no binary release,
Valheim launch, save/other-profile/other-mod mutation is part of this checkpoint.


## Historical handoff before .43

Updated 2026-10-01. Active work: **BW-01 owner game acceptance**; BW-00 accounting/network
gates remain open. Source candidate 0.19.42, Store v9,
Valheim 1.0.16. Current authorized development/test profile: TerrainRamp-1.0-Test
only. Earlier integration-profile permission is superseded.

The [roadmap](docs/ROADMAP.md) and [owner test series](specs/roadmap/current-pass.md)
are the work order. Automated build/geometry/store/localization/bridge/world-layout/
host/deployment-guard checks pass. Formatting verification exits successfully with
workspace-loading warnings. Unity Workbench passes 81 captures and actual input
regressions for stationary contour guides, cancel/tool switching/deletion, F7/button,
occluder drawing restoration and picking, camera FOV and outliner overflow.
English/Russian catalogs contain 633 matching keys. The focused 0.19.41 game series
is accepted by Ostrix's report on 2026-10-01, not independently observed.
Cost/refund accounting and multiplayer remain untested. No new binary release is published.

0.19.42 is installed in TerrainRamp-1.0-Test through the guarded script with
Valheim closed; its game series is pending. Previous pair: artifacts/checkpoint-0.19.42/preinstall.
Installed SHA-256:
- BuildWorks.dll: `C9DFFEF5A9E989AA00A084906CF6BCCB0617FDA15EB964C4DFC883DB092570D0`
- BuildWorks.Geometry.dll: `3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16`

Independent correctness re-review passed after anchor-tooltip and Russian handoff fixes. Complexity review:
Lean already. Ship. Other profiles/mods/saves were not modified.

Latest owner report: held-RMB menu, furniture Q/E, nearby targets, below-grid
placement, group pivots, Array, two-part save/reopen, world reload and camera pass.
0.19.39 owner feedback accepts native tab language refresh, menu hover and F framing;
selection/box is provisionally accepted; .40 contour cleanup is accepted.
0.19.41 implements the approved editor changes: stationary full-edge contour guides,
fully hidden occluders (off by default; F7/viewport button), six grouped shortcut hints,
camera FOV 10–120 with a game-setting reset (disabled in orthographic view), and
compact 30-unit outliner rows with separate anchor badges, striped rows and a native
scrollbar. Wheel scrolling is 1.5 times faster. See-through uses seven sampled mesh
rays per candidate, restores original drawing flags, permits picking through temporary
occluders, and does not change materials, manual visibility, blueprint data or the world.
Resources/stations in the native panel, category chooser, shortcut rebinding,
double-click framing and external translations await their
UI/format gates. One-piece save remains blocked by the two-part contract.
Esc removes partial placement and restores its ghost; dropped-resource amounts
still need accounting. Multiplayer is deferred; no other profile transfer authorized.
The five focused checks remain a regression reference, not a request to repeat them.

The approved editor design is implemented in .42: explicit Move/Rotate/Points/Scale
families and Nordic forms, temporary G/R/S with exact cancel/one Undo, 1–4 tools,
Space/F3 search, compact dropdowns, collapsing hints with next-action strip, Focus,
resizable tree split, measured tooltips and active hidden-target outline.
MMB is editor camera-only, Delete removes; world/F9 and Store remain unchanged.
Unity: 81 fresh captures, current-frame Enter validation, surface/source/camera
rebasing, return-to-origin, hidden hit-test and EN/RU tooltip scale regressions pass;
completed 2026-10-01T13:48:43.1597938Z. Release zero warnings/errors, final format
exit 0 and two independent read-only reviews PASS. No independently observed
Valheim, networking or full narrow-window tooltip proof. Next: six focused .42
owner checks. Interior snap access remains separate. Discuss guides/curves again
before Line A/B implementation.
After each verified step update EN/RU docs, checkpoint commit and normal GitHub push.
Keep game binaries, saves and personal logs out of the repository.
