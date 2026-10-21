# Checkpoint271 — sparse/dense shader work

2026-10-21. Local candidate; installed QA .250 unchanged. No game launch. Independent Astra inspection of actual DXBC found five `movc` selections after both paths had executed. Uniform `[branch]` now selects sparse or dense before sampling. Formulas, addition order, effect history and24lighting steps are unchanged. New DXBC contains branches; baseline DXBC is byte-identical. DXBC temporaries27→28 do not measure hardware registers.

- PublishedOnly passed14frozen RGB comparisons with maximum delta0, page/boundary and negative checks. This is a scoped stand, not native house appearance.
- ABBA uses old bundle `5B93CD4E…` versus candidate `0BF38794…`, identical inputs across four arms. Each has48measured renders,28procedural objects/32resident;784field/ledger and560scalar comparisons passed in total.
- Mean A5657.92825ms versus B5411.3768ms per48renders: **4.35763% less**. A range112.5257ms;B136.6478ms;both B arms beat both A arms. Two arms per variant do not establish statistical certainty.
- Timing includes CPU+GPU+readback drain. Synchronous maxQueue1 is imposed by the test; native service headroom, pure GPU time and house FPS remain unproved. ABBA does not compare A/B images; separate PublishedOnly provides the scoped pixel parity above.

Evidence: `artifacts/checkpoint-271/{compiler/,published-parity.log,timing-summary.json,verified-source/}`; full ABBA: `artifacts/checkpoint-269/shader-branch-measurement-20261021-092758-37b7fbd6/`. Old bundle/source retained in271/before-bundle and271/before. Independent correctness/complexity review passed. Candidate neither embedded in a DLL nor deployed.

Next: separate GPU work from CPU/wait, then a bounded asynchronous service check. Do not repeat the full52s house after two prior overflows without measured headroom. Native v11 appearance/walking/house,completion/full paid cancellation and networking remain open. [Checklist](LIVE_CHECKLIST_RU.md),[269](CHECKPOINT_269.md).
