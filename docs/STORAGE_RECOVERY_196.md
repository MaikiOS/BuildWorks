<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .196 — native storage recovery refusal identified

Verified 2026-10-16, current Valheim Build25909357. This checkpoint adds precise
diagnostics, not a recovery fix or enabled production filled-storage placement.

The existing full .187 fixture ran once on .195: one native chest,10 Wood paid,
2 actual provided Wood transferred, paired profile/next-world-generation save
verified. Player retained1 Wood. Normal reload refused strict custody matching.
This fixture directly invokes PlacePiece/ConsumeResources, not full TryPlacePiece.

Guarded .196 deployment preserved131 protected files; DLL SHA-256
66A3CDAEB19148B4C029C3D462B91C3E06310E5CB043DE4B9A74989367C8CD04.
Thunderstore Modded and exact isolated QA identity verified. Read-only checking
of the retained operation identified `Player inventory manifest changed.`
No grant, construction, transfer, journal rewrite or compensation was repeated.

Current native ItemData.Save truncates `m_durability * 100f`; Load multiplies
the integer by0.01f. Re-saving certain loaded floats loses another hundredth.
SHA-512-validated retained profiles and the SHA-256-validated five-frame WAL
prove: the old inventory hash matches SavedPair.PlayerInventoryHash; its full
manifest matches Observation.SourceManifest. The next saved inventory differs
only in Hammer cell(1,0), durability1887→1886. The current live float is exactly
18.85999870300293, the native load of1886; re-saving writes1885. Seven assertions
pass. The complete old profile hash DOES NOT match SavedPair.ProfileHash, so
the evidence supports inventory-level identity only. Independent review agrees.

MatchesRecovery predicates and order are unchanged. Normal/QA builds0/0,
recovery116, journal36/return271/profile54/world104/paired221/transfer276/
reservation31, Host/localization1104 and independent correctness PASSED/Ponytail
Ship passed. UI unchanged; Workbench not repeated. Verify helper unavailable;
direct checks used. A reviewer HTTP403 was recovered with another reviewer.

Private local evidence: `artifacts/storage-native-qa-195/` and
`artifacts/storage-recovery-196/`; raw profiles, WAL and logs stay out of Git.
The diagnostic script succeeded; recovery itself remained refused.

Next design: bounded exact paired-save inventory evidence and a version-pinned
single Save→Load projection, compared directly with loaded fields instead of
re-serializing durability. Keep every other attribute/cell/count and authority
check exact; no epsilon, arbitrary wear tolerance or global game patch. This is
not implemented yet. Multiple save cycles need separate provenance. Retain the
legacy hold; do not replay grants/moves, delete WAL, infer settlement or refund.
Recovery/return/durable settlement, production filled placement, failure paths,
mounts/armor stands and networking remain open. Full goal ACTIVE/incomplete.

God/ghost restored false, normal logout/game exit completed, process absence
verified and the exact own temporary GABS process stopped. Cleanup's first
DateTime.Parse failed after automatic JSON-date conversion; DateKind String
fixed the check, and the corrected receipt verifies actual helper termination.
