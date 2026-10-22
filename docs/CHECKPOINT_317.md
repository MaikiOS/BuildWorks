# 317 — durable local accepted-copy receipts

Candidate **0.19.280 is NOT installed**. Verified installed QA **0.19.279** retains SHA256
`C7708CC1152E458AB7816C0E0AD4D7691A0B4F90090F838B385248C26C470D9B`.
No game/network317 run. All14 goal sections remain required.

The previous TrySaveAcceptedPeerCopy validated exact bytes/hash/ID/version but a repeated
offer created another card. A regression test reproduced that failure before the fix.
Native27 adds private LibraryData.peerReceipts (owned origin snapshot/local copyId),
committed atomically with the new card through the existing Save. Portable remains15.

An exact server/world/offer retry returns the original ID without Save/Revision/share
rewrite, retaining later edits. Other origin values conflict. Deleted original copies
remain deleted, even if a SaveAs survives or passive provenance was cleared. Receipts
are never exported. Capacity4096 refuses new acceptance without evicting history;
existing retries still work at full ledger/catalogue. Invalid receipt keys/IDs/fields,
corrupt library and old headers refuse safely, including old-field null/empty forms.
The temporary-file readback compares every origin/copyId exactly. Both initial and
existing-history save failures roll back card/receipt together.

50 targeted assertions PASS; full Store regression passed with the initial45 and the
extra5 then passed in the focused run. Geometry/EditorBridge/normal+QA2800warnings0errors/
HostContract/localization1135 PASS. Independent correctness/Ponytail review PASS for
this local file scope, including a separate50-check run; verify is unavailable. Logs retain sandbox access failures, successful
same-command elevated repeats, original duplicate repro and the corrected EditorBridge
reflection arity16→17 failure. Only source/tests/docs may be published.

The limited Git checkout independently passed normal/QA2800/0 and full Store regression
with the final50 acceptance checks. Local normal SHA256
`644446A0CACE53E02281FC7B385CC6E197CDBA62EEF20EA20CD8E72C405F982A`, QA candidate
`A8C36329034CB64EC87B639E77D9DDCDFEF80F776CED8FD3493DAAF779D612ED`.
These are local build artifacts, not deployment proof or binary equivalence between Git
and local visual materials. SocratiCode located the method with zero callers; direct
source review confirmed no working wire/UI caller. Focused memory results supplied no
current peer-transfer decision and were not used as implementation evidence.

This is local single-Store duplicate protection. It does not authenticate sender/recipient,
establish server authority/global replay protection, handle concurrent writers/file rollback,
or prove power-loss behavior. There is still no working wire/UI acceptance caller.
Server-authenticated offers, recipient selection/confirmation, cancellation/reconnect and
host+remote remain open13.7 requirements.

Finish316 on the still-installed279 FIRST: after owner game exits restore bridge run
20261022-185641 before any new Prepare, resume own existing316 project, E full-refusal,
free cell/retry/normal exit+restore/fresh replay. The316 helper is pinned279. Do not deploy280
over this pending scenario or migrate/reset the QA catalogue to make a test pass.
