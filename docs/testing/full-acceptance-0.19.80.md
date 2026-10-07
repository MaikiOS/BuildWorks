# BuildWorks 0.19.80 combined game acceptance

**English** | [Русский](full-acceptance-0.19.80_RU.md)

Installed on 2026-10-07 only in TerrainRamp-1.0-Test, main DLL 0.19.80.0.
Both installed SHA256 hashes match the build. The agent did not launch Valheim or edit saves.
This consolidates changes since .67, including exchange and free-support regression checks.

Use a disposable test world/character and normal resource costs, not free construction.
Reply by number: works, failed (action/result/screenshot), or not tested.
Start with 15–19. Item/resource loss or duplication stops acceptance.

1. Hammer → Blueprints: category scrolling, page changes, correct thumbnails and no overflowing categories. Report slow opening time.
2. With hints expanded, test 1280×720 and your normal resolution at UI scales 100/120/140%. All actions remain accessible.
3. Import file: Downloads/Documents/Desktop, folder navigation/back, no remembered full path needed or stale folder rows. Repeat from the editor catalog.
4. For available .blueprint/.vbuild/.buildworks.json/ZIP files, review the whole input before confirmation. Back/close imports nothing; confirmation creates a separate copy and preserves the original.
5. For native chest/mount contents, test all four independent keep/clear combinations on import and export. Reopening respects the choices; already-cleared contents never reappear. Unreadable state explains refusal rather than silently discarding data.
6. Export: choose folder, name and BuildWorks JSON/ZIP. No file before final confirmation; persistent result/copy path afterwards. Duplicate name must not overwrite; fix it without losing other settings. Test Hammer and editor entry points.
7. Round-trip a small blueprint with rotated/scaled parts, a group, Russian multiline sign and support. Compare dimensions, transforms and state. For installed mod parts, compare against the original without double scaling or oversized pieces.
8. Corrupt files/missing parts explain failure without partial construction. Dialog typing/click/wheel never controls the scene; Esc closes the dialog, and the closing click never places a part.
9. Editor selection → Space/F3 Tools → create support. Tree contains the support object, not create/delete buttons; selecting it shows its gizmo without moving parts.
10. Move support with mouse and G/X/1/Enter; rotate R/Y/90/Enter. Only support changes. Undo works, Esc cancels, clicking a part returns part editing.
11. Assign transformation centre: selected parts rotate/scale around stationary support. Reset through Tools. Assign placement origin, save/reopen and compare pose/name/role. Delete only support; Undo restores it and its role.
12. Snap both a catalog ghost and an already placed editor part via G to an unusually rotated target. Orientation follows the target and preview matches commit without an axis jump.
13. Cyrillic/multiline/empty signs survive editor save, exchange, supported world placement and world reload. Empty signs never inherit stale text.
14. Select one native wall/horizontal ItemStand and attach a compatible item through Tools. Replace/remove require confirmation, cancel preserves state, Undo restores it. Item follows holder transforms and survives save/reopen/thumbnail rendering; it is not a free scene object.
15. Start with a newly authored holder recipe and a simple compatible quality-1 item. Bring the item and construction materials; place via Hammer. Exactly one real item and normal build resources are consumed, with actual item properties retained. Test both holder types and single/stack inventory.
16. Remove the required item before starting: clear refusal and no construction. Restore and retry. Captured full snapshots require exact native properties, including inventory grid/flags; a merely similar item may correctly be refused under the current strict rule.
17. Place two filled holders, counting inventory items, mounted items and construction resources: exactly two real items, no free copies/double consumption.
18. Provide materials for the filled holder but not a later construction part. After the sequence pauses, Esc performs native removal/refund and drops the mounted item once. Count ground and inventory items. For uncertain/pending rollback, do not invent a manual refund; report retained objects/message.
19. Save/exit/reload successful filled-holder placements. Contents/properties/orientation persist; normal removal drops contents once. Cleared mounts remain empty.
20. Capture a native chest with known inventory and a filled native holder. Capture/edit/export do not change originals or take world items. Data persists without bypassing access/wards. Chest contents restoration in the world is not implemented acceptance.
21. If a second player is available: host/client see identical filled mounts; test client place/remove/reconnect and denied access/wards. Otherwise record not tested.
22. Large imported blueprint thumbnails work; opening more than 512 editor parts explains the current limit rather than hanging. Unsupported foreign state/armour stands explain refusal without silently losing state or altering source files.

Not ready: world chest restoration, complete foreign-state decoding, armour stands/free items,
interactive editor scenes above 512, durable world sockets after reload/network, and foreign-format export.
Output choices currently are BuildWorks JSON/ZIP. Preserved foreign bytes are not proof of understood runtime state.
Deployment and isolated automated tests do not replace Valheim acceptance.
