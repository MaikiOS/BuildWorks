<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# PlanBuild export — QA0.19.140

QA0.19.140 installed only in TerrainRamp-1.0-Test, code535ba31. Normal/QA0/0,
full Store, Host25185596, localization960, isolation/preflight, Unity492, pinned
reader of two files under two cultures and both reviews PASSED/Ship. Installed SHA
pair verified;137 protected files unchanged; Default clean. Valheim not launched.
Current installation is tracked in LIVE_CHECKLIST_RU.md. No game compatibility claim.

Editor/Hammer export adds PlanBuild as a third format. Choose parent folder/name,
scroll the losses, confirm explicitly. A new named directory contains both
`name.blueprint` and the full `name.buildworks.json`. Existing paths are refused.
Back/cancel write no bundle. Backup is encoded before foreign omissions; external
catalog cache IDs become stable portable GUIDs, construction IDs remain intact.
Local placement-cache identity follows the existing portable11 omission rule.

Contract pinned to PlanBuild commit e12ed86ff2498bbd4a9c0f5d81cb388e3aaeb549:
PieceEntry, Blueprint, SnapPointEntry, Components/PlacementComponent. Primary links
and detailed evidence are in the Russian counterpart. Headers and thirteen fields,
invariant numeric formatting, root absolute scale, JSON sign strings and custom
snap positions only. No own14/15 extensions. Poses remain in document coordinates;
saved placement frame is explicitly lost. Unknown intrinsic scale is never assumed
to be one; known source absolute scales remain exportable without the prefab.

PlanBuild mutates raw commas and decoded semicolons and does not apply empty text.
Only nonempty sign text without those characters is transferred. Other overrides
are listed by record ID and retained in backup. Prefab IDs containing semicolon,
comma, opening parenthesis or starting with hash are refused. Known safe creator
headers remain; absent authorship is explicit. Complete credits/terms/provenance
remain in original source archives in backup.

Loss review covers groups, stable IDs/labels, visibility/locks, anchors, free
supports/placement/pivot frames, metadata/mod dependencies, source archives, preview
settings, material seeds and all foreign object state. No storage/mount/armor/raw
ZDO/admin behavior is promised. The receiving mod controls prefab availability,
costs, stability and persistence; non-ZNetView scale/persistence is not guaranteed.
No .vbuild exporter or blanket PlanBuild/IH/EWD extension support.

Two closed/byte-checked files publish in one sibling Directory.Move. Source/basis
changes require fresh review. Windows Unity Mono paths>=260 characters fail clearly
before writing; choose a shorter name/folder. This is not long-path support.
Only owned known temporary files and the empty staging directory are removed.
Review and confirmed writing run on a worker after prefab-scale capture on the host
thread. Close/Escape/unload cancels an unpublished bundle. Cancellation and final
directory publication share a lock; an already published bundle is not deleted.

Evidence: artifacts/checkpoint-0.19.140 and artifacts/planbuild-reader-0.19.140.
Unmodified pinned readers plus verbatim FromArray use documented JSON/Unity numeric
shims. Actual-writer reading proves grammar/fields only. Store covers nonunit basis,
sign loss, full backup/source/credits, stale/collision/failure, caller-thread prefab
capture and asynchronous stale-review/cancellation. Unity exercises actual UI
review/scroll/Back/long-path refusal/retry and native Close with a controlled pending
task. Final checks/reviews/install are recorded in verification.json and
installation.json. verify skill absent; direct checks and two
reviews replace it. Geometry math unchanged. Valheim/PlanBuild building/reload/network
remain unverified. Manual steps: QA_0.19.140_RU.md.
