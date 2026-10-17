<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .127 — PlanBuild legacy sign and seven coordinate refusals

PlanBuild's [PieceEntry.FromBlueprint](https://raw.githubusercontent.com/sirskunkalot/PlanBuild/master/PlanBuild/Blueprints/PieceEntry.cs)
tries JSON additionalInfo, then preserves the literal on failure (verified2026-10-10).
BuildWorks now permits that fallback only for `.blueprint` prefab `sign`.
Source-row binding of signs with ZDO uses the same literal decoder;
known ZDO text retains authority, unknown ZDO remains inert. Other callers retain strict decoding; foreign Info/ZDO/chance, native/portable
JSON validation, forbidden controls and4096-character bound remain enforced.
Native22/portable11 unchanged.

Both ordinary-sign and source-bound ZDO-sign regressions reproduced before their fixes. Tests cover JSON/plain/leading-quote
literals, invalid controls/length, atomic refusal, strict non-sign calls,
exact source/row binding, native save/reopen and portable roundtrip.
Actual `TheDecay CornerStairwell - Apollyon.blueprint` retains467 rows and
the exact sign on row174. Inert unit-scale bases are not prefab/game proof.
Pinned unchanged DarkTrooper/2v2t5.0.0 corpus:202/209 parsed,7 refused, no new refusal.

Read-only coordinate audit found no whole-blueprint translation to repair.
Dragonsreach_vanilla and _2 each have183/8793 pieces around Y2514–2521;
PortalPlatform has7/317 guard_stones around Y2501–2507; DoorCapper2 and
TheDecay Full each have one LocationProxy around Y−4936/−4998; both Tyr
Dragonsreach_v3 variants have255/9683 pieces in two detached high groups.
No Coordinates/Rotation placement headers occur. Author intent is unknown;
do not rebase, shrink or drop records automatically. ±512 refusal remains.
Full per-file table and boundaries: [Russian checkpoint](LEGACY_SIGN_CHECKPOINT_2026-10-10_RU.md).

Normal/QA0/0, complete StoreTests/corpus, HostContract Steam25185596,
localization931, compiled QA isolation and whitespace PASS. Geometry/Unity492
passed .125; unchanged geometry/UI were not rerun. Independent review results
are recorded in local verification.json. Initial HostContract expected .126;
pin updated and rerun passed. Initial log rename was denied; host.log retained,
successful output in host-final.log.

Local evidence: artifacts/checkpoint-0.19.127/{verification.json,store-repro.log,
store-zdo-repro.log,store-final-delta.log,coordinate-refusals.json,Audit-CoordinateRefusals.ps1}.
Archive SHA4A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88 unchanged;
no extraction or author-file redistribution. InstalledQA.117 unchanged;
no deployment, Valheim, saves or network execution. Import-to-build/reload remains open.
