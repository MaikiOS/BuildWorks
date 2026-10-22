# 313 — passive provenance for a received personal copy

The uninstalled .278 file slice is included in .279. Native26/portable15 preserve
bounded peer provenance through store/editor copies, merge and Undo/Redo; old
headers cannot carry the new fields. Old binaries need not read these versions.
The QA catalog was not migrated by this slice.

`TrySaveAcceptedPeerCopy` validates frozen bytes/hash/source ID/version and creates
a fresh personal document. Provenance is passive, not authenticated authority,
an acceptance receipt, or replay protection. Server authorization/idempotency,
recipient/acceptance UI and host/remote/reconnect remain required by goal13.7.

27 targeted assertions, full Store/Geometry, EditorBridge, normal/QA builds,
HostContract,1132 localization keys and independent correctness/Ponytail review
passed for the file scope. Evidence: `artifacts/checkpoint-313`; integrated .279
checks:315. No native or network peer-transfer acceptance is claimed.
