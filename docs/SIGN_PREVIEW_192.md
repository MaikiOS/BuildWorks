<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .192 — sign text in previews

Saved text previously reached the placed native Sign but was absent from editor, thumbnail and ghost visuals because the renderer-only clone omitted the TMP widget. BlueprintSignPreview now bakes owned glyph meshes from the native widget's font/material/layout and nested transforms. No Sign, ZNetView or Canvas is cloned into the final preview; no SetText/ZDO operation runs.

Editor, thumbnail, small and batched ghosts consume the adapter. Text changes invalidate visuals; explicit blank stays blank. The active native ghost widget is temporarily suppressed and restored on close. Effective world scale drives SDF shader factors, including batched draws. Retired text templates and dependent batches/variants are pruned. A 513-part /12-edit regression reproduced the old cache growth and passed after the fix.

UGC privilege gates display; unknown-author imports use the pure native CensorShittyWords.Filter. Raw blueprint text is unchanged. Permission changes clear memory caches; disk cache keys include permission, and invalidation removes both versions. Imported preview images do not replace generated previews of decoded signs. Missing fonts/widgets leave the editor model visible with a warning.

Actual-source isolated Unity Play Mode checks geometry/UV/nested TRS/pivot, fixture Cyrillic/rich text, source/ZDO immutability, blank/filter/permission behavior, small/current-ghost regeneration and teardown, heavy keys/ownership/SDF/caller MPB and thumbnail cache invalidation. Rendered TMP fixture versus baked mesh: **zero pixel difference**, missing-text negative control341997. Actual editor scene text changes and missing-widget fallback pass. Normal/QA builds0/0, Geometry/Store, HostContract, QA isolation and localization1103 pass; independent correctness/Ponytail reviews found no remaining blocker.

Final-source Workbench624 passed at2026-10-15T15:57:01.9931786Z. Guarded QA .192 deployment completed after Valheim exited; both DLL hashes match and104 existing user-data/QA save/token files are unchanged. No game launch. Private evidence: artifacts/sign-preview; exact source manifest: artifacts/ui-runtime/source-sha256.txt.

Limits: fixture font and inert host do not prove installed native font pixels, live platform permission, construction/payment, reload or networking. Isolated current bundle reading cannot resolve the TMP script and reports `Installed sign has no readable TMP widget`; this is a laboratory limitation. Native API signatures were checked against current game assemblies and by compiling .192. Costs, resource debit, native sign persistence and blueprint format are unchanged. Full goal remains incomplete.

The verify helper is absent from both skill roots. Direct diff review, the listed builds/tests and independent reviews were used; no verify run is claimed.
