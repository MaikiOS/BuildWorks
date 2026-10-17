<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Incomplete blueprint thumbnails — 2026-10-09

U10 source/automated checkpoint, not deployed or accepted in Valheim.
Installed QA .116 SHA256 remains
`867E5E474A3C1B335500E47F1B2DC3A6250AC1FAA36E6D92E766E904F33DCF3A`.
No Valheim launch or world/save mutation.

Previously one missing prefab prevented resolving any thumbnail sources, and
rendering required every model. PNG and rotating preview now share one visual
path: available models retain poses/intrinsic scale; missing records use the
recorded offset local bbox, or a constant uncertainty marker at the known position.
The marker does not claim a model or exact size. Both catalogues explain absent
images and remove that text when an image arrives. Missing warnings remain;
the editor also explains marker uncertainty.

Nullable preview sources are separate from strict construction sources. Costs,
permissions, inventory and native21/portable10 stay unchanged. Partial images
are memory-only, bypass complete imported/disk images, and do not overwrite
complete PNG caches. Complete/partial/complete transitions are tested. Hot prefab
replacement without the normal game restart is not claimed. Unsupported shader
or content diagnostics remain limited; world objects without Piece belong to I09.

Normal/QA0/0, localization899, HostContract25185596 and Store passed.
Unity6000.0.61f1 completed444 screenshots at `2026-10-09T16:10:31.1990776Z`,
four resolutions and100/120/140%. Real partial rendering checks available model
basis/pose, offset rotated bbox, constant unknown marker, transparent background,
inert absolute-scale record, no colliders, disposal and disk-cache isolation.
Actual editor catalogue checks fallback/update/warning and text overflow.
Parent inspected partial-thumbnail.png. Native Hammer UI is only compiled/read,
and remains game acceptance. Correctness/Ponytail reviews found no blocker;
review/checkpoint recorded in evidence JSON.
The verify helper is unavailable; direct checks and independent review are used.

Evidence: `artifacts/checkpoint-0.19.117/partial-thumbnail-*` and
`artifacts/ui-runtime/partial-thumbnail-check.txt`. Artifact folder .117 does
not represent installed .117. Full goal remains active.

Ready fixtures: `tests/fixtures/replacement/14-replace-missing.buildworks.json`
(QA115 Replace Missing, two walls plus recorded synthetic bbox) and
`tests/fixtures/metadata/19-unknown-preview.buildworks.json`
(QA U10 Unknown Preview, two walls plus unknown-size marker at z3).
Store validates file19 decode/roundtrip without invented metadata.

After a new version/SHA is installed: Thunderstore TerrainRamp-1.0-Test → Modded,
isolated BW_QA_43d3719a. Import both files retaining missing records; inspect
editor and Hammer cards, warnings, fallback removal, scene poses and reopening.
No construction/resources needed. Exact Russian actions, expected results and
error signs: [Russian scenario](PARTIAL_THUMBNAIL_CHECKPOINT_2026-10-09_RU.md).
Do not run against old QA .116. Game construction/reload/network/v11 remain open.
