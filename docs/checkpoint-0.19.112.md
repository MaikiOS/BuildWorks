<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .112 — one-part blueprints

Native18/portable8 admit one-part documents. Empty documents remain invalid;
older native<=17/portable<=7 retain their two-part minimum. No fields or scale
semantics changed: pinned baseScale and relative scale remain separate.

The change reaches foreign parsing, editor Save, world capture/workspace Save,
activation/resolution and renderer-only world preview. Chain/contour requirements
for multiple points remain intact. A hidden single native holder retains its
mounted-item preview, including the batched attachment overlay.

Verified: normal/QA builds0/0, GeometryTests, full StoreTests and353-source corpus,
HostContract, localization862 and LocalizationTests, full QASaveIsolation,
EditorBridge, WorldLayout17 actual Session checks, dotnet format, DeploymentChecks,
Unity Workbench276. New regressions cover source bytes, native18/portable8,
legacy17/7, two-to-one update, exact XYZ/rotation/pinned scale, sign, group/pivot,
support/snap references, reviewed import/export, empty atomic refusal and unresolved
prefab basis. Actual editor Save/metadata clicks and disk reopen passed.

The verify helper is absent. Direct checks and independent correctness/ponytail
review are used. Initial failures exposed stale schema expectations, a test that
mistook pinned baseScale for relative scale, and a Save-click fixture outside the
restored small GameView; corrected tests preserve production guards and real raycasts.

This is a prerequisite for explicit conversion without missing parts, not that
feature. M02 remains partial. See QA_0.19.112_RU.md for the single-wall fixture.
No Valheim launch, native spending/final completion, reload or multiplayer proof.
Guarded QA .112 installed in TerrainRamp-1.0-Test; SHA
`BBF449755DC3F156D212B38375EF25B960C524E51C136D95590E4AF2FB84A424`.
All48 QA save paths/hashes and the blueprint library remain unchanged.
