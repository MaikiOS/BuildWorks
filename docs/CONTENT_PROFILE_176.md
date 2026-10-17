<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .176 — canonical Local character inventory readback

2026-10-13. QA **0.19.176** deployed to **TerrainRamp-1.0-Test**.

A completed in-memory transfer can prepare and verify an ephemeral expectation against the canonical Local `.fch`. The adapter binds the current Player/Profile identity, exact native Inventory.Save bytes, completed v3 journal, persistent target marker, both inventory digests and raw ZDO state. It checks custody before opening and after parsing one FileShare.Read handle. Shared bounded serialization serves both container capture and the player's own current items; no archived attributes are loaded.

The pure parser validates exact outer framing/EOF and SHA-512, current profile46, nested Player33 prefix and a complete retained Inventory109 byte span. Limits: profile32MiB, inventory1MiB/4096 items, profile string32KiB, dictionary65536 keys and1024 worlds, also bounded by remaining section bytes before allocation/iteration. Cloud/Legacy/Auto, temporary/backup paths, changed identity/custody and unsupported/corrupt data fail closed. Post-save native source changes must invalidate the expectation.

The evidence proves only the current canonical Local inventory slice. An old canonical file containing identical bytes can satisfy that narrow fact; this is not proof of a newly requested save. Player33 tail is opaque, SHA-512 is unkeyed integrity rather than authenticity, and full Player.Load/world/cloud/server durability and cross-save atomicity remain unproven. No save is requested, no journal/marker is retired, no hold/refund is released. Populated Container placement remains disabled.

Validation: CPU51 new parser/adapter/real fixture filesystem,240 transfer/271 return/114 recovery/36 journal/31 reservation; full Geometry; normal/QA0 errors/warnings; Host25730771 with four additional native layout fingerprints; localization990; headless Unity compile/contact and actual shared capture/cancellation guard. CPU native APIs use inert substitutes. Initial InvalidDataException, sandbox NuGet and loader-version mismatch were corrected and final checks passed. Correctness PASSED/Ponytail Ship; no installed verify helper, direct checks used.

Installed BuildWorks SHA256 CA1D1F860158311789BE641D37DD877E09D6BE7E5366DFA6CA56288667BCF334; Geometry08E29183B670459393BAE0D8D8660148D9FE8632E089FDF3A973B6D5CA765775. All158 config/token/provision files unchanged. No game/model/input/user saves executed or changed. Private logs/native evidence remain in artifacts/checkpoint-0.19.176, excluded from source Git.

Next: authoritative persisted-world marker/s_items evidence correlated with this operation, durable settlement/release, uncertain-child reconciliation and a separate post-payment Container pending-state. Native save RPC lacks a correlated success acknowledgement. [Manual .176](MANUAL_QA_176_RU.md) retains18 fixtures and accepted results.
