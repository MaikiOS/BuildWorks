# BWPP01 compatibility vectors

`bwpp01-legacy-vectors.tsv` was captured on 2026-10-25 before any journal v2
implementation, using the unchanged production journal from functional checkpoint
`e15a58a16eee8fff582721482ac99335e88eebad` (also present in documentation
checkpoint `4cf9d3ca3f8296219a1dc17eaa0259290fb2ccbe`). Its SHA-256 was
`0076F67F4B0E76421540662969FE9F2721D492D3A29E7477785D632525783A64`.

The TSV contains one synthetic plan, before/after native-format v109 inventory
packets, a detached item packet, and six complete framed BWPP01 records. The
records cover intent, observed/committed paid placement, no-effect, and
observed/committed cost-free placement. Each record row also pins its canonical
fingerprint and receipt bytes. The fingerprint hashes the canonical payload,
not the complete framed file.

`ConstructionPartJournalLegacyChecks` loads the fixed plan and re-executes the
real production journal transitions in a new temporary directory. It compares
complete file bytes, canonical fingerprints and receipts, reopens each record,
then verifies idempotent replay. Both the full StoreTests run and the existing
`--construction-projects` command run these checks. Do not regenerate the vectors
to accommodate a serialization change: legacy bytes and bindings must survive.

All identities, inventory contents and save hashes are synthetic test inputs.
These vectors prove neither native payment/save/reload nor permissions,
all-source conservation, provider compatibility, refund settlement or multiplayer.
They cover one v109 Wood stack and cost-free records; existing property/v110 and
cancellation/payout tests remain necessary.

Fixture SHA-256:
`416F987460F2FED8B6441ED25471D0D665024B5540332F6E613C921B646AD898`.
The one-time capture helper was removed from the executable after capture;
provenance and its source are retained in local checkpoint-355 artifacts.
