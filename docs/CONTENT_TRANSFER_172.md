# .172 — provided-item transfer foundation (source only)

2026-10-13. Installed QA remains .170. Populated container placement is still blocked; the new primitive is not connected to world placement.

`BlueprintStorageTransfer` requires a recorded construction payment bound to the same player inventory and complete shared reservation. It validates real empty native grid/capacity, owner/creator/access/ward, local player/world/new target identity and rejects quest items. A move removes equip actions/unequips, then invokes native positional transfer once. Supplied attributes are preserved; archived state is never loaded. Canonical property comparison ignores only grid/equipped, quantities are checked separately.

Both inventories, prior deliveries, future held reservations and local ZDO revision/capture are observed after true/false/exception. A zero-stack reference left in source is uncertain. Ambiguous or unchanged operations are never replayed automatically. Entry observations are in memory only. Local `Transferred`/`LocalSaveConfirmed` are not server, disk or character-save acknowledgements.

Both cancellation paths now require verified empty own supported storage; filled/unverified storage is retained. Native destruction may drop only some items before throwing and is not a transactional refund.

Evidence: normal/QA0/0; production-source CPU104+31; ignore-source-debit negative control fails then clean passes; Host native signatures/fingerprints/compiled guards and existing native negative controls; localization990; QA isolation; headless Unity actual editor-source compilation/contact acceptance and separate actual storage capture/cancellation acceptance on inert external APIs. No visuals, native gameplay, network or persistence proof. Logs: artifacts/checkpoint-0.19.172. CPU receipt carries four source hashes and false native/game flags. Correctness PASSED; Ponytail Ship. No verify skill installed.

Actual Steam manifest build25730771; assembly_valheim SHA256 25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE. The old printed25185596 was a constant, not manifest evidence; prior IL checks remain historical evidence. Host output now reads manifest and prints hash. Initial NuGet sandbox access denial was resolved using existing restored dependencies; Unity sandbox HttpListener crash and missing fixture API errors were corrected/rechecked; QA junction test needed normal unsandboxed execution. No game/model/deploy/saves changed.

Remaining: persisted item custody/operation states before mutation, restart-safe replay prevention, exact return/compensation including equipment, source/target saves and server acknowledgement/permissions; then a dedicated Container postpayment integration, native next-piece cost coordination, pause/cancel/UI and ready game fixtures. Manual .170 remains current; no populated-storage manual test is ready. Persistent projects/stake/fogv11 depend on this accounting.
