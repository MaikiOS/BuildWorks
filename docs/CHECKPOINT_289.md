# 289 — paid close before pickup: persistence slice

21 October2026. Local **0.19.266**, not deployed; QA remains265. No Valheim/Unity/GPU, player input, installed DLL or save changes during owner play. [Full Russian evidence](CHECKPOINT_289_RU.md).

The existing plan rev1/rev2 bytes stay intact. CancelPaid first binds immutable saved issuance under the existing project lock, then commits rev2. A second-commit failure leaves a frozen recoverable intent. Removal/issuance mutation is active-only; exact journal reading, collection and cleanup remain supported after closure. Optional payout metadata is absent from old serialized records; mutable collection data is excluded from the issuance fingerprint. New operations respect the latest known world generation; historical cleanup and exact replay retain their original generations.

Targeted172 CPU assertions, full StoreTests, normal/QA builds0/0, host contract and localization1122keys/1008references PASS. Independent correctness and Ponytail reviews SHIP. Review-found historical cleanup9→10/close11 regression was fixed and tested. Sandbox intermediate-file access and uint default compilation failures were corrected and affected checks repeated. The verify skill is absent; direct diff, executable CPU checks and independent reviews were used.

The local fallback mask is prepared but not GPU compiled/executed. RenderProbe helper CPU build0/0 does not prove the shader. Owner confirmed rain in288; weather was unpinned, so no causal weather estimate or clean cross-run performance ratio is claimed. Candidate shader/helper stay local research files.

Native async finalization, fresh witnesses, startup recovery, board/fog removal and the paid UI flow remain unwired. Full paid cancellation, physical interaction, networking and approved v11 appearance/speed remain open. Restore3 and ordinary-save metadata audit288 await owner game closure. All115 checklist IDs/check states are preserved;8.8 remains partial. Evidence: local artifacts/checkpoint-289 and the Russian checkpoint.
