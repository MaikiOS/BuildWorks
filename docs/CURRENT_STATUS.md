# Current BuildWorks status

**2026-10-18 · [Русский](CURRENT_STATUS_RU.md) · [Roadmap](ROADMAP.md) · [Documentation](README.md)**

**QA .225 installed: durable unpaid project/stake passed19/19 +14/14 in Valheim, including logout/login and cancellation.** Project fog, payments and ordinary construction wiring remain open. [Evidence](CHECKPOINT_225.md).


Installed isolated QA build: **0.19.225**. Source is ahead by a detached construction journal: StoreTests/Geometry passed; normal/QA builds have 0 errors and 13 CS0649 warnings for fields not yet populated by a native caller. This is not native paid-continuation proof. The owner clarified the [EarthWorks boundary](EARTHWORKS_BOUNDARY.md): initial blueprint-driven terrain preparation, one board/store; architecture remains under discussion. Tested host: Valheim 1.0.20, Steam build25909357. Latest published binary is the historical [0.19.37 alpha prerelease](https://github.com/MaikiOS/BuildWorks/releases/tag/v0.19.37-alpha); its ZIP does not contain the current changes. There is no stable release. Source synchronization is not binary publication.

| Area | Implemented / automated evidence | Valheim or user evidence | Still open |
| --- | --- | --- | --- |
| Precision placement and editor | XYZ, paired axes, 1–1000% component scale, groups, anchors, Undo/Redo, Save As, filters and interior camera | Owner accepted specific scale/save/camera/catalog cases; ordinary snap checked in .193 | Broader rotated/modded contacts, pointer wrapping edge cases and full visual acceptance |
| Imported construction | Explicit prefab migration, supported text/material paths, real native pieces | Forest Hut (28) paid pieces and exact transform/inventory reload in [.195](ROTATION_PERSISTENCE_195.md) | All formats/prefabs, constructive state, large buildings and networking |
| Formats and missing parts | Native store24/portable13; supported .blueprint/.vbuild import, reviewed export, missing records/bounds/copy alignment | Limited import/editor cases accepted | Unknown models/behavior are not restored by archiving; wider native corpus acceptance |
| Catalog and file exchange | Categories, Save As, delete/hide, automatic portable file, Share, batch import/export | Limited batch .197 import/export/reentry proved | Full batch UI/dirty draft/restart; some owner checks remain |
| Server library and sharing | Administrator publication; players/admin visibility, verified personal copies, provenance; in-process checks | Host+remote not accepted | Live access/reconnect/restart; direct player-to-player transfer |
| Items and storage | Separate storage/mount inclusion; supplied-item preservation and journal foundations | Limited chest/inventory comparison only | Durable settlement, cancellation/refund/crash/replay, all holders/armor/admin restore and network authority |
| v11 fog | .224 canonical78+78 exact; .225 Gamma/Linear each108 exact fields/36 images | Native .224:11/11+66/66/4clips/recovery0; .225 matched-scene is Unity-only | Actual-prefab look/scale/tilt/recontact/FPS, house budget, project fog and networking |
| Unfinished construction | .225: durable unpaid snapshot, local stake/panel/cancel; ordinary path unchanged | Valheim19/19 +14/14: runtime/logout-login, exact IDs/TRS, cancellation/replay, equal inventory lists | Payment/continuation, fog, native input/HUD, crash reconciliation and server/remote |
| World selection and heavy chain | Capture/volume foundations and targeted checks | Basic capture accepted; later coordinate/height UX issues reported | Editable volume/gizmos, boundary rules, full loaded-area and performance acceptance |
| World objects and EarthWorks | No terrain integration dependency | None claimed | Admin-priced nonrecipe objects; EarthWorks is a later separate integration |

Evidence levels are distinct: source → automated checks → deployment → native Valheim → owner acceptance → host+remote. A later build number does not upgrade older evidence. [.193](WORLD_SNAP_193_RU.md), [.195](ROTATION_PERSISTENCE_195.md), [.197](BATCH_NATIVE_197.md) and [.206](FOG_ATLAS_206.md) document their exact scope.

Historical .206 proof: QA.206 installed DLL SHA `B1691BB1DAE7668AEBC924A185329F4AE552492B4A57457D6A798A0DF68042FC`;143 protected files retained their hashes/timestamps. Normal/QA builds: 0warnings/0errors, host/control/isolation/localization 1104 and independent reviews passed. .206 has not run in Valheim. Local receipts are under `artifacts/fog-atlas-206` and are not shipped in GitHub.

Next: durable payment/new-object confirmation and board continuation; connect unfinished-part fog and complete house acceptance. [Full scope](../PROJECT_CONTRACT.md), [ordered milestones](ROADMAP.md), [owner live list](LIVE_CHECKLIST_RU.md). Accepted cases need no repeat without a regression reason.

Historical .207 proof: [packing and allocation-failure recovery](FOG_PACKING_207.md) verified; saved28-block dimensions fit128MiB. QA installed, SHA `1A7B711678F1C22886C79B89C5845DCAC94EA1FFF1FB6C82FA87709454566AA5`. This is not a native house: simulation/contact budgets remain open. Native .207 untested.

.208: [contact fields and blueprint adapter](FOG_CONTACT_208.md) verified in Unity:28 synthetic records,5 comparison frames, budget/recovery/cleanup. QA installed, SHA `A7BAC4CCAABB4748DB648BAA2B3144350924B16D28EC7433D0A84A1B750E5822`;143 protected files unchanged. Actual Forest Hut/memory/FPS and persistent board remain open; Later native .208 ran: full traversal FAILED its field budget; see .209.

.209 installed, SHA `50263A0D3F8DA4C08E7600E2FCB8A51B5C0509511EE321DB1F8BE30EBE510927`;143 protected files unchanged. Normal/QA0/0, contracts/localization1104, targeted cutout/Lazy/v11 parity and independent PASSED/Ship passed. .209 has not run in Valheim yet. [Actual .208 house failure and fix](FOG_CUTOUT_209.md).

.210 installed, SHA `A96AC9314A3F30176CD6CC358FE95A439ABB25B8EAD6269F2287AE3358CB734B`;143 protected files unchanged. The full tested house route now fits the field budget; performance/full appearance remain unaccepted. [Exact .210 evidence](FOG_SCRATCH_210.md).

.211 installed, SHA `F748D15F60768E0B6C6A4BB24FF878F66613036432FA2CBD2A7093EEEA2278C2`;143 files unchanged. New/original shader and cache comparisons passed; Unity timings are noisy, native .211 exposed recovery failure after owner passage; full scenario FAILED. [Report .211](FOG_RENDER_CACHE_211.md).

.212 installed, SHA `71E5B6DF7A260E8E9FBBD479F0A4E07908ADBD1D09CAB31028EA0DB00CE2A469`;143 current files unchanged. Cross-frame failure reproduced and fixed in Unity;24 real frames passed. Native .212: the28-part house passed both ways,35/35 steps in44s; contact6/8 owners, both recoveries0/0 bytes, peak87408640; inventory unchanged, no errors. Owner rejected the .212 appearance: dissolution/recovery differs from v11 and passage stutters. Visual integration is BLOCKED. [Report](FOG_RECOVERY_212.md).

.213: [shared building noise frame](FOG_NOISE_FRAME_213.md) passed GPU/regressions/contracts/independent PASSED/Ship and is installed;143 files unchanged. SHA `3421595F8757AE894D7A262AC32BA6BD72B8180E12971C71C12E2F4D3D0BC4A8`. Native .213 is untested. Domain, YZ ownership, FPS/roof and durable board remain open.

[v11 boundary control](FOG_V11_INTEGRATION.md#why-the-smaller-field-did-not-retain-the-effect): absorption was separated from storage outflow; four sizes were checked throughout passage/recovery. Similar total smoke hides surface/ledger differences; the larger per-part candidate is not accepted. Next candidate: independent full-domain states with a shared workspace and split atlases; production sparse storage and spatial return remain unimplemented. Runtime .213 is unchanged.

GPU storage/shared-workspace experiment **passed**: one full wall1444 field/1083 ledger/16 CPU comparisons; eight independent states in alternating order11584/8688/16, including final restoration after another owner used the workspace. Fixtures cover63488 finite half patterns, negative zero/subnormals and four rejection cases. Single-wall encoded peak970240 bytes; counted eight-candidate field peak92268592 of98304000, including maps, ledger and overlapping generations. This private reflection experiment with synchronous reads is **not the game allocator/renderer or house/FPS proof**; reference simulations, source/atlas and driver staging are excluded from that field counter. Next: production storage lifetime, split atlases published before geometry, spatial return and the house. Runtime .213/bundle unchanged; Valheim was not launched.

CPU confirmation control is implemented in `BlueprintFogSnapshotState`: owner/generation/sequence/tick/revision, no resubmit/recovery before confirmation, retirement/late replies and uncertain readback. All128 CPU test groups, normal/QA builds0/0 and independent PASSED/Ship passed. The controller is now exercised with real v11 in the private GPU fixture; production lifetime, input queue and renderer remain open. Installed .213 is unchanged.

Small fixed-pool GPU candidate passed `-ArenaOnly`:8 synthetic8×8×8 owners,47 commits/3 expected rejections,15 full checks —480 exact field and360 ledger comparisons, including atlas gutters and unique block ownership.17 kernels compiled onCPU, C#0/0,71 corrupt receipts rejected; independent PASSED/Ship. This is **not full v11, the production allocator/callback cadence, or house/FPS proof**. The full-physics fixture now passes; next is the production scheduler and shader consumers. Installed .213/bundle unchanged; Valheim was not launched.