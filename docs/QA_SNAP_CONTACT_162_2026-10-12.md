<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Live .161 evidence and diagnostic .162 — 2026-10-12

QA .162 deployed only to TerrainRamp-1.0-Test. Full construction, world snap,
contents, reload and multiplayer remain unproven.

The live session actually loaded .161: FileVersion/SHA63131110… and installed
MVID e6a9d5e8969b4a46858008d2ca573b17 match the current game stack trace.
PluginVersion incorrectly said .160. The initial older-runtime conclusion was
wrong and corrected; `artifacts/checkpoint-0.19.161/version-mismatch.json` records it.

snap01–11 did not identify the precise pair causing the complaint. Source review
found aimedPiece-only reranking may veto a native pair; that is not a proven
runtime diagnosis. SocratiCode Default collection codebase_fbb51d0bb614 was green,
26612 chunks, active watcher. ApplyPassiveAutoJoin/Respond symbols were found;
zero plugin impact did not replace direct Harmony/reflection caller review.
No reindexing.

TEST24 contents toggled off: native21 built floor/wall2/4 then Invalid before
chest. Wood11+40 became11+36; two swords unchanged. Cancel reported removing2,
but this attempt's isolated refund was not proven. Old tables were nearby;
Invalid must not yet be attributed to collider contact. native32 safety abort
was not replayed; native33 was state-only. Ostrix then collected drops and
dismantled tables. That mixed return cannot prove the fixture-only balance.
No reprovisioning. Normal QA autosave is not reload proof. Receipts/screenshots
and final log: `artifacts/checkpoint-0.19.161/`.

.162 adds QA-only observations: native/chosen snap references, owners and event
frame; pre/post-hook and reply-time positions; contact/normal/native flags;
placementStatus; up to64 inventory stacks. A recorded contact precedes final
native validation and does not prove valid placement. Bounded latest references
are reset between sessions. Snap decisions/costs/permissions are unchanged.
PluginVersion now matches assembly; in-memory stale .160 negative control rejects.

normal/QA builds0 warnings/0 errors, Host25185596/control/isolation/loc989 PASS.
Independent correctness PASSED and Ponytail Ship include final additions.
`verify` unavailable: direct review/build/targeted compiled checks used.
No Unity execution for this observation-only change. Initial sandbox build failed
NuGet.targets(196,5), Access to the path is denied; authorized retry passed.
Evidence: `artifacts/checkpoint-0.19.162/`.

After confirmed exit, guarded dry-run/deploy and independent hash verification
passed. All158 config/import/save files and timestamps unchanged; Default clean.
No receipt/token/save reset. Installed BuildWorks .162.0 SHA256
CB8D05B190E8A4FDFE3FB39A59BD147968A342C4A5E2EA9868CB63659F25EC12;
Geometry .1.0.0 SHA256 EC2315A128CA4AF1B19DD5BE69A93B69F18BB3F558F018C6C36A17D0E22C0261.

Next launch: Thunderstore TerrainRamp-1.0-Test → Modded, existing character/world
BW_QA_43d3719a. Fresh nonce/version/focus/balance; observe snap without building,
then one already imported TEST24, contents off, clear location. Diagnose any
refusal before correction; prove full4/4/costs/cancel/reload subsequently.
Do not replay aborted input or repeat accepted editor tests without cause.
.162 has not run in Valheim; A05/I03/A09 are not closed by deployment.

## Additional offline reproduction

`artifacts/snap-contract-0.19.162/Reproduce.ps1` extracts the unchanged
ApplyPassiveAutoJoin method and compiles it with inert host substitutes.
Six cases: exact0.5m vetoed,0.499m accepted; valid supplied pair with no aimed
piece or a different aimed piece vetoed; manual pair and alignment-disabled
controls accepted. This executes the real branch, not real Unity transforms,
quaternions, native translation or physics. Initial fixture CS0649 was fixed
with an explicit null initialization; the next run completed successfully.

Current assembly_valheim SHA25A0A107… FindClosestSnappoint uses Vector3.Distance,
`bgt.s` for radius and `bge.un.s` for best candidate: inclusive0.5m and first tie.
BuildWorks squared-distance `>=0.25` excludes that boundary. Squared ordering
also does not prove parity for rounded sqrt ties. Existing HammerSnapSearchAcceptance
tests the large-blueprint index, not ApplyPassiveAutoJoin reranking.
No production correction yet; installed .162 unchanged for live diagnosis.
The complaint's cause remains unproven. Native IL/results/source SHA are archived
beside the script. SocratiCode green26622; this turn's symbol lookup returned no
result, so actual method/caller were read directly. No reindex.
