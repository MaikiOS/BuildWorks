# QA .246 — saved receipt lookup independent of old inventory

2026-10-20. Checklist8.7 remains open. Receipt writing, collection, old-drop cleanup and terminal paid cancellation are not implemented by this step. v11 house appearance is not accepted.

New explicit TryReadCustomData checks the canonical file/checksum, player identity and supported schema without an old inventory hash or fresh Player.Save match. Distinct immutable CustomDataEvidence contains only Player/ProfileHash/CustomData, never inventory proof. Legacy exact readers retain their contracts.

Shared SkipItem decoding locates the inventory boundary without loading/resolving prefabs or returning synthetic items. Structural discard permits unknown/zero/absent native prefab hashes (minimum9-byte item); strict container/mount readers still require resolved prefabs. The same bounded PlayerData33/34 tail reaches EOF. The Local profile wrapper holds FileShare.Read and validates identity/path/context before and after; it does not save or trust mutable live inventory/map state. Exact receipt/world/operation authorization remains the future collector's duty.

952 recovery assertions within2030 profile assertions pass, covering synthetic spending/empty inventory, unresolved hashes, immutable typed evidence, malformed bytes and identity/context/path/source rejection. Prior1023 fresh-map,222 paired-persistence,170 generation and full content harness pass. Store/Geometry, normal/QA builds (0warnings/errors), current host contract, localization1122 and deployment isolation guards pass. Independent correctness/ponytail review passes; verify skill is absent, not claimed.

An existing native profile47/PlayerData34 was inspected with the new API without a live snapshot or file changes; its empty map matches independent253 extraction. Nonempty receipts/spending are synthetic; no game was launched. Guarded .246 installation into TerrainRamp-1.0-Test verifies both DLL hashes and177 protected QA files unchanged. Initial test-fixture global Game/ZNet leakage is retained and fixed through proper fixture isolation, then the full suite passes.

Final review initially blocked commit because SkipItem retained discarded item fields. Fixed bounded strict-UTF8 buffers now retain no item strings/maps/Item; >1MiB/split-character/invalid UTF8 cases pass. Affected suites and source review were repeated, corrected .246 redeployed; original BLOCKED/deployment evidence is retained. Structural skipping does not check item-map key uniqueness or prove Inventory.Load; the Player customData map remains strict.

Local evidence: BuildWorks/artifacts/checkpoint-254 build/test logs, retained-profile-verification.json, deployment.json, recovery-reader-contract.md, collection-safety-contract.md and profile-source-review.md. No native IL, saves or binary DLLs are committed. Last in-game evidence remains [252](CHECKPOINT_252.md).

Next is one row per transaction: intent, guarded exact empty-cell credit plus permanent receipt, verified profile, matching sealed-drop cleanup, world save/full absence, row settlement. Save guard, insertion/coordinator/journal/cleanup and fault/reload/replay remain open, then8.8. Spending never revokes the receipt. Existing four sealed stacks/8Wood are unchanged and still owed. No new owner test is assigned; full recovery, user cancellation, multiplayer and house visual acceptance remain open.
