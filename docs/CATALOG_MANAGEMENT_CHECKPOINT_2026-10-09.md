<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Catalogue management — source checkpoint, not deployed

Both catalogues expose owned **Delete blueprint**, external **Hide from catalogue**
and **Hidden / Restore to catalogue**. Deletion/hiding require confirmation and
support cancellation. Editor controls remain available for unavailable entries;
Hammer exposes both context and inspector actions and a separate hidden section.
Source files/ZIPs, local copies and already built native objects remain independent.

Deleting the current owned record keeps an unsaved editor draft, its history and
source provenance. Closing prompts; saving creates a new ID. Failed writes keep
the original record. Visibility uses stable `importSource`, never converted IDs.

Native library21 adds optional `hiddenSources`, bounded to2000 unique nonblank
entries. Old20 without the field loads and upgrades on the next successful write;
invalid/future libraries refuse writes. Older DLLs cannot read21, so a downgrade
needs a compatible library backup. Portable10 contains no visibility preferences.

Normal/QA builds, Geometry, Store, EditorBridge, localization895 and HostContract
passed. Store covers immutable source/ZIP bytes, independent copies, host ID
changes, reload, restore, rollback, migration and portable isolation. Actual editor
button/controller tests cover cancel, unavailable restore, write refusal, open
draft/history/close prompt, new-ID save and disk reopen. Russian management and
confirmation windows extend the screenshot matrix to420: four resolutions and
three scales. Final run/review evidence is in
`artifacts/checkpoint-0.19.117/catalog-management-verification.json`.

`verify` is unavailable; direct checks and independent reviews were used. Hammer
has source/IL/layout proof, no native game pixels or construction proof. Installed
QA remains116 SHA867E5E47…DCF3A; no deployment, Valheim, save or network operation.

The owner scenario with exact fixture/path/Russian buttons is in the Russian
duplicate. It is deferred until a separately hash-verified QA installation:
TEST16 -> Save As `QA U09 Copy` -> cancel/confirm owned deletion while open ->
save draft as a new record -> hide/cancel/restore external in both catalogues ->
restart Modded and verify persistence plus already built objects. No manual stand
construction or material collection is required. Do not repeat accepted unrelated
cost/refund/persistence cases.
