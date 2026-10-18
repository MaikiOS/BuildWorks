# .225 — durable unpaid projects and stake interaction

2026-10-18 · [Русский](CHECKPOINT_225_RU.md) · [Full scope](LIVE_CHECKLIST_RU.md)

QA **0.19.225.0** is installed only in TerrainRamp-1.0-Test, SHA `09C32F8D6BC01B2A13C611B3583C6869557B6E39E34D2D99C841A3C309911F2A`. This is a verified intermediate stage. Ordinary construction does not create these projects yet; fog and paid continuation are not connected to the board.

## Implementation and proof

`BlueprintConstructionProjectStore` persists an immutable portable snapshot, world identity, exact root/part TRS and fresh stable part IDs. Writes are checksummed, flushed, reread and atomically committed. Unpaid cancellation records a terminal revision and operation ID; replay returns that result without granting anything. Targeted tests cover identity/revision conflicts, aliasing, reload, enumeration, corruption/truncation/extra paid data and failed replacement.

`BlueprintConstructionProjectRuntime` owns local-host world lifetime and reconstructs boards from these files. `BlueprintConstructionProjectBoard/View` provide original stake geometry, native Hoverable/Interactable, a usable-hammer gate, a resource/inventory summary, close-without-cancel and confirmed unpaid cancellation. Opening errors cannot escape into native Interact; cancellation errors remain visible. The board currently has **no ZNetView/Piece**; it is a local presentation, not a completed network prefab. Creation is QA-only until the paid journal is connected. Remote/dedicated/payment operations are refused by this stage.

Normal/QA builds:0 errors/0 warnings; targeted StoreTests passed; localization1114 keys/1001 static references and host/QA-control/isolation contracts passed. `verify` is unavailable; direct checks and independent correctness/Ponytail reviews were used. The interaction exception finding was fixed and reviewed again.

**Real Valheim, final installed DLL:** `ValheimQA/artifacts/20261018-174209`,19/19 +14/14, optional failures0. Four unpaid woodwall records, no native construction; one enabled board collider, no Piece/ZNetView; hammer interaction, modal input ownership, close/reopen, stale-call refusal, runtime restoration, logout/login with exact snapshot/TRS/IDs, terminal cancellation/replay and no resurrected board. Bridge-returned inventory lists matched before/after both series; this is not an audit of every hidden ItemData attribute.

Normal logout/Quit and restoration of all3 bridge DLLs completed. Across all3 runs,4137 regular save files retained size and exact UTC ticks, not content-hash proof. Configuration changes were ordinary QA saves/control, version header and the exact verified cancellation of the owned project record.

## Independent fog gate

`artifacts/fog-matched-reference-225/{Gamma,Linear}/verification.json`: same scene/camera/geometry/source/inputs;108 exact Smoke/Flow/Surface comparisons and36 frozen/dense/published image comparisons per color space. Independent review inspected10 PNGs. Maximum published/dense mean:1.173e−6 Gamma/1.758e−6 Linear; local0.027451. Frozen/dense mean stays below the existing0.005 gate; local reaches0.313725/0.490196 and is recorded but not bounded by that gate. This is not pixel-exact image parity.

Gamma is the original laboratory branch; Linear is a derived same-space comparison, not owner approval of a changed look. The approved archive is unchanged; original ProjectSettings bytes were restored after Unity reserialized platform defaults. Native-prefab look, house and FPS remain open.

## Preserved failures and next work

The first game attempt failed on unavailable `Shader.Find("Standard")` after the project file had committed. Nothing was reset: a material from the loaded native woodwall fixed the board, and the byte-identical pending record recovered in `20261018-173544` with19/19 +14/14. Only equipping an existing hammer changed that initial inventory list; final-run lists are identical. Assigning TMP fonts before activation removed the new warnings; two initial optional Backpack API warnings remain separate.

A deployment wrapper misread stale `$LASTEXITCODE` after a successful PowerShell script; installed hashes proved success without redeployment. A report analyzer had a PowerShell ParserError and was corrected. Audit initially flagged the owned cancellation: only the known record's exact revision/cancel-ID/checksum change was accepted after byte comparison. Original failures and successful reports are retained.

Next: durable payment/new-object intent and confirmation → board continuation → fog only for unfinished parts → refunds/reload/server. Native raycast/E, physical UI input, full HUD/resolution checks, complete server restart and multiplayer are unverified. The full goal remains active.
