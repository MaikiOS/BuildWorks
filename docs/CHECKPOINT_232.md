# .232 — ordinary blueprint confirmation creates a construction project

2026-10-19 · [Русский](CHECKPOINT_232_RU.md)

An ordinary structural blueprint confirmation now saves its frozen plan and board/v11 before queuing existing native paid construction. This is a bounded private Local host result on two walls, not completion of the module.

`ArmPlacement` consumes confirmation before debit. Store failure cannot fall through to legacy placement; an uncertain write keeps its ID, and changed snapshot/TRS conflicts rather than creating a new project. A confirmed durable record survives board presentation failure; automatic start requires a board. Special surfaces, foreign data/contents, Repeat/Guide and network contexts retain their previous path.

The native test reproduced a real first-part stall: selecting a blueprint changes an inactive Session to `BlueprintWorldPlacement`, while the next ProjectPart required `None`. Both passive modes are now admitted; active editing/F9/selection, pending catalog, UI and gameplay guards remain.

| Evidence | Result |
| --- | --- |
| Builds/CPU | Normal/QA0 errors/warnings; Geometry, Store/journal, HostContract Valheim1.0.20/Steam25909357, localization1121keys |
| Failures/retry | Native21/21: unavailable Store, post-commit observation failure/same-ID retry, board failure/restore, unpaid cancellation; exact inventory/valid ZNetView unchanged before queued native work, duplicate same-frame patched `Player.UpdatePlacement` consumed |
| Fog/payment | 17/17 in4.2s: real unpaid plan shows2 parts, committed2/2 shows0; Wood73→69,2 journal rows/4paid and real unique native objects with exact TRS/receipts |
| Board continuation | Native Interactable via `player/interact_object` returnedtrue at4.4m, actual OpenBoard.ID checked. Then9/9 through production Continue on the open board with blueprint selected: retained1/2→2/2, Wood69→67, committed, no blocked input |
| World reload | Save/logout to menu and re-enter the same world **in the same process**;8/8. New2/2 retain IDs, paid4, exact/nativeReceipts; Wood67→67, fog0. Retained partial2/2; previous4-wall fixture still present/exact/receiptMatches/paid2 each |
| Data/cleanup | No grants, terrain or catalog changes. Unpaid inventory unchanged; paid work changes Wood and native Hammer durability95.1→93.2→92.2. Own PID56652 quit normally;3 bridge DLLs restored |
| Review | Independent source correctness/Ponytail and full-JSON evidence reviews; bounded PASS |

Evidence: `ValheimQA/artifacts/20261019-132918/{confirm-failures-report.json,confirm-paid-report.json,board-continue-report.json,reload-report.json,mcp.jsonl,player.log,bepinex.log,restore.json,audit.json}`; build/CPU/manifest: `BuildWorks/artifacts/checkpoint-232`.

Earlier failures remain in `20261019-130123` (ground-camera prerequisite, repeated setup in a passive selection, wrong registry origin in the QA helper; no debit) and `20261019-131423` (real1/2 stall/90s timeout, Wood75→73). The saved wall was retained and its project continued after the production fix. Deliberate fault injection produces expected errors; these are not error-free logs.

Installed QA0.19.232 SHA256 `9BA4E5610650794B2D724E28FCDFEE77AB28598527C9C34D3B03751B5A3675A3`. Geometry `2265E3…BE5B0` and embedded v11 bundle `6F2A58…F6F3B` unchanged. No new isolated GPU comparison: shader/solver unchanged; prior270/18 belongs to .231.

The helper sets the native ghost before the real Hammer handler: camera ray/auto-snap and end-user input are not proved. Board Interactable/OpenBoard and Continue are proved separately, not physical aiming/E or a UGUI click. The retained partial has journal/debit evidence, not a full expanded TRS check of both parts; the new project and original4-wall fixture do have native TRS/receipt checks.

Next: observable v11 passages through a long4-section wall and house, then paid cancellation/refund and general recovery. Resource shortage, all constructive/special contents, crash/replay, heavy performance, remote authority/network and owner visual acceptance remain open. No EarthWorks/terrain work; the full14-block goal remains active.
