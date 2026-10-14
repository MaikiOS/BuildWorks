# Manual queue .184 — server library and personal copies

**English** | [Русский](MANUAL_QA_184_RU.md)

Installed QA0.19.184.0. Launch only Thunderstore → TerrainRamp-1.0-Test → Modded → existing QA world when convenient. No local-model or automatic computer control. No materials/test-stand construction required. Accepted scale/camera/snap checks are not repeated. Pending [batch queue .183](MANUAL_QA_183.md) also works on .184.

Source: artifacts/Blueprints-for-import/BuildWorks-tests/Batch-183/01-floor.blueprint (BW183 Floor, one wooden part). Import it, open editor and Save As QA184 Floor; reuse an existing owned card if available.

.184 reads older files and writes native23/portable12; .183 cannot read the new formats. Use .184 on both ends of the future network checks.

1. Select saved QA184 Floor, open Server library from Hammer or Server from editor blueprint catalog. Prepare publication then cancel: no server entry/personal copy/build. Prepare again with All players and confirm: one selected server entry, one part, revision1, full metadata.
2. Open QA184 Floor in editor, move a part without saving. Receive server entry then close review: card count/draft/Undo unchanged. Receive again, confirm Save personal copy and return to catalog: one new independent card from saved server bytes; no build or draft replacement.
3. Save an edit to QA184 Floor, select the correct local source and existing server entry, review/confirm Update server. Same server ID, revision2, no duplicate. Exit/reenter same QA world via Modded: entry and copies persist.
4. Cancel server-delete review: entry remains. Confirm server deletion: entry disappears, personal cards/previously built objects remain.

Errors: mutations before confirmation, duplicate cards/entries, lost draft/Undo, unsaved draft published, unexpected build, missing persisted entry, server updates/deletion altering personal copies. Report the exact visible failure instead of repeated blind publication.

Local host ownership cannot verify remote player/admin filtering. Host+remote, role revocation, reconnect and dedicated restart remain a separate pending queue with the mod on both ends; no second-PC setup is required now. Provenance insertion/Undo/save/Save As/conflict/16-limit already have CPU and actual-controller coverage; no manual mass seeding required.

Personal transferable files: TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/share. Server archives: config/BuildWorks/server/world_<UID>. Do not edit archives/ACL manually.
