# 290 — paid finalization after issuance and startup

21 October2026. **Local0.19.267, not deployed; QA remains265.** No game/Unity/GPU, player input, installed DLL/bridge/save changes during owner play. [Russian evidence](CHECKPOINT_290_RU.md).

A small read-only native finalizer checks the same private Local payer/profile/path/inventory/world/generation, fresh canonical profile and async construction-only absence, saved/loaded permanent receipts and unchanged journal revision/issuance fingerprint. It then calls verified CancelPaid289. Post-commit observation failure confirms durable terminal instead of issuing again.

Runtime queues one attempt after saved issuance/on startup before collection/cleanup. Existing pending intents retain their recovery, then requeue closure. PayoutBusy serializes work; Suspend revokes it. Confirmed closure retires fog/board while collection/cleanup remain available. QA-only status adds cancellation state, reason and terminal binding. Paid UI remains gated.

56 actual-code CPU assertions with fake native boundaries,61interaction assertions, negative distance control, cleanup22+31, normal/QA0/0, host contract and localization1122/1008 PASS; independent correctness/Ponytail SHIP. New test is part of Test-HostContract. New fixture shared-reference and accumulating-counter failures were corrected; full host contract repeated successfully. The verify skill is absent. Unchanged store/journal tests172/full suite belong to289, not a new native run.

Native readback/reload, retirement, the full before-pickup lifecycle, explicit user retry, paid UI and multiplayer remain unverified/open. Fog unchanged; diagnostic mask289 remains GPU-unexecuted. Owner play and pending bridge restore/audit288 are respected. All115 checklist IDs/check states retained. Evidence under local artifacts/checkpoint-290.

Resumed22October: final linear receipt traversal synchronized from canonical,56actual-code checks and both267builds0/0 retained; host contract rerun PASS. Still source/CPU only,267not deployed.
