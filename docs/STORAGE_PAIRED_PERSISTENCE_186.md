<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .186 — paired Local character and world persistence evidence

A completed exact item transfer can now request the current Local character save and exactly the next Local world generation, then append their verified evidence together to WAL4. This internal mechanism does not yet enable populated storage placement, settlement, refunds or remote acknowledgement.

The coordinator permits only an idle private solo Local host, the same owning thread/player/profile/world and unchanged completed custody. Save session prohibitions, connected peers and overlapping requests refuse. Native asynchronous world saving may begin its generation on the worker after Save returns; acceptance requires the committed generation to be exactly before+1.

Character freshness uses scoped read-only Harmony observations of the current FileWriter.Finish and FileHelpers.ReplaceOldFile. Exact Local paths, thread, successful current writer, full Player.Save byte equality, temporary profile integrity, actual replacement and canonical SHA equality must all agree. A prior matching character file or SavingFinished callback alone is insufficient. No player metadata is changed. The rejected nonce implementation left a persisted custom-data key and was removed.

The detached world filesystem worker reads the pinned generation. Final acceptance rechecks world generation, character SHA, both inventories and persistent marker on the owning thread. WAL4 records both generations, world/player/marker and profile/inventory hashes once, with Flush(true). WAL1–3 remain readable. Corruption, partial results, cancellation or changed custody keep reconciliation required. Checksums are not authenticated server receipts.

CPU tests exercise actual installed Harmony patches over inert native-shaped save/writer substitutes and real temporary files. They cover stale temporary files with failed writers, repeated/already-finished writers, wrong paths, replacement failures, callbacks, context changes, generations, cancellation, paired WAL validation and unchanged persisted player metadata. Builds, Geometry, HostContract and localization are separate checks. No native game save or full reload/network acceptance is claimed.

Native character replacement is not atomic. Observation detects refusal but does not roll back the game's files or guarantee power-loss durability. Partial-state reconciliation, native save/reload proof, remote acknowledgement, settlement and Container placement remain required before filled construction is enabled.

Final checks:221 paired assertions plus journal36/return271/recovery114/profile51/world92/transfer276/reservation31; full Geometry, normal/QA builds0/0, Host186 and1100 localization keys. Independent correctness PASSED/Ponytail Ship.125 existing whitespace diagnostics map to unchanged baseline lines; no new diagnostics. The verify helper is unavailable. Sandbox File.Move/MSBuild-pipe refusals were resolved by approved fixture/formatter retries; unchanged dependencies allowed a --no-restore build after NuGet access refusal.

Guarded QA0.19.186.0 deployment followed an authoritative closed-game CIM check and WhatIf. Both hashes match;111 previous config/save files are unchanged and catalogue/import counts remain0/0. BuildWorks SHA `C543F90579C88120D2F7BA5E805D81EF5CB522FB42A20864D8164113B3064CA6`; Geometry SHA `DDFB02FE409816DE74BADFF042BC2141FAA282C07E41DF2297CBB86AA15639B1`. No game was launched.

Local evidence: `artifacts/storage-paired-persistence/{cpu-observer-elevated,normal-final,qa-final,geometry-final,host-final,localization-final}.log`, `format-baseline.json`, `installation.json`, `deploy-final.log`. Native extracts, game files and personal data are excluded from the source checkpoint.
