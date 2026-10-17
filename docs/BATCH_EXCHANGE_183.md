<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .183 — batch import and export

**English** | [Русский](BATCH_EXCHANGE_183_RU.md)

Verified and installed QA **0.19.183.0**. The full BuildWorks goal remains active and incomplete; Valheim/network acceptance is not claimed.

| Capability | Implementation | Evidence |
| --- | --- | --- |
| Multiple inputs | File checkboxes, aggregate review and individual ZIP documents | Actual UGUI clicks and Store |
| Categories/subset | Common, per-document or original category; explicit document exclusion | Frozen ordinals survive missing-ID preparation; UI/Store |
| Invalid input | Explicit skipped-file report; corrupt ZIP rejected as a whole | Mixed good/bad members with valid neighboring input |
| Atomic import | One archive with exact source bytes, name manifest, documents and supported previews | Originals/manifest, file-catalog reload and immutable sources |
| Batch export | Saved native/external card selection, full BuildWorks ZIP, name/directory/manifest | Actual UI, reader roundtrip, no native card creation |
| Cancellation/failure | No publication before Move; staging cleanup; committed result retained | Import/export cancellation during writing, pre-cancel, source change/no overwrite |
| Contents | Independent storage/mount policies change copies only | All four combinations in both directions |
| Draft | Open document/history remain intact | Actual controller/UI |

One atomic group avoids repeated full-catalog validation and stale reviews caused by earlier queue commits. The tradeoff is a bounded in-memory group snapshot and cancellation of the whole unpublished group. Invalid files are explicitly excluded before confirmation; valid selected documents publish together. An excluded document creates no working card, while its source file stays in provenance.

Limits:1–32 input files/cards,64 MiB aggregate raw input,128 MiB shared unpacked read budget,100000 aggregate parts; existing per-document, archive-entry, preview and library limits remain. Prepared export documents are bounded to64 MiB. Metadata is snapshotted on the host thread; workers do not call Unity. Damaged-file and memory protection remain enabled.

This stage exports full native BuildWorks ZIP only. Foreign batch formats, server publication and in-game personal transfers remain unimplemented. Preserving sourceDocuments/unknown evidence does not restore foreign behavior. Exact original files stay in the imported archive; export carries supported portable-format data.

Final normal/QA builds0 warnings/errors; full Store, Geometry, Localization runtime, EditorBridge, actual Session WorldLayout, actual exchange Tick, Host Steam25730771 and localization1048 passed. Exact-source Unity588 covered4 resolutions/3 scales and actual batch interactions. Russian review/picker/export visually inspected at1280×720/140%. Core/UI correctness PASSED; Ponytail Ship. `verify` is absent; direct checks/reviews used. Formatter retains135 inherited WHITESPACE diagnostics with0 differences from .182 after fixing13 new findings. Failed development/test runs remain in evidence.

Guarded deployment to TerrainRamp-1.0-Test with Valheim closed verified BuildWorks SHA256 `05BEF1C8D738FBACE6ADCEF4A99E58289AEA67596A568F0B7B19BEDBA961DA79` and Geometry `7BAD79188E5BA5274492506F638F2AF3A6D21BC4DB7A74C4A6F7C4D21EB38B5F`; all103 config/token/receipt files unchanged,0 owned cards/0 import files. No game/model/input/save execution. Evidence:`artifacts/checkpoint-0.19.183`.

[Manual checks/source folders](MANUAL_QA_183.md). Next: player/admin server library and accepted personal copies. Native item transfer, persistent v11 projects, selection/heavy-chain performance, admin and networking remain open primary workstreams.
