# .241 — exact shader state reuse and native wall; house queue still fails

2026-10-19 · [Русский](CHECKPOINT_241_RU.md)

**QA .241 installed:** shader reuses validated state/clock;14 RGB comparisons against the previous path are exactly zero. ValheimQA passed7/7+59/59:4 passes through a four-section wall,482 frames,467456 exact inner texels; all16 recovered parts have zero fields/flags. Inventory unchanged, own fixture closed, normal exit and bridge3/3 restored. **Whole-house fog remains unfixed:** two48s GPU attempts failed at queue64 after37.01s/39.85s; repetitions stopped. RAM reuse remains disabled in the game. [Evidence](CHECKPOINT_241.md).

Validated metadata/clock is reused for the same owner, with all original buffer guards and v11 arithmetic retained. The immediate-return prototype failed a strict float gate and was rejected; tolerance was not widened. Runtime shader baseline keyword supports exact A/B. Default normal/QA retains the previous RAM allocator; Editor-only RAM reuse and async recorder remain experimental.

Unity14 RGB A/B comparisons returned0 and existing published/legacy controls passed. Two48s attempts still failed queue64 at37.011s and39.848s (35.937hostFPS);140scalar/196field were not reached. These are not native FPS or GPU timing measurements. No repeat or full-house success claim.

ValheimQA .241:7 preparation+59 route checks,75.6s,4 directions through4 real wall sections,482 timestamped frames/4 MP4,467456 exact inner texels. All16 recovered parts have zero flags/empty smoke and surface. Hold bits1/2 indicate active flow/displacement. Inventory unchanged; own unpaid project/stake removed. Normal logout/Quit, PID59004 exited, bridge3/3 restored.4137 regular-save files unchanged by size/mtime (not content hashes). Only config version comment changed. Preparation logged2 optional Backpacks type warnings; the actual route logged none. No grants, terrain or catalog population; no house/payment/refund/reload/network acceptance.

Normal/QA builds0/0, host,1122-key localization, separation and8 normal fog IL/handlers unchanged against .240 passed. CPU evidence1760 negatives and production receipts8modes/1manifest/84 negatives passed. New steady-tail guard rejects growing final5s queue envelopes. Validator footprints now match existing .233/source proof4061136/18540048. Independent correctness/Ponytail review passed; verify skill unavailable.

Installed QA SHA `4E1B7227DDDAF5E179850594928B905A1E077FEF4AED13B477D05135804D8DE9`, normal `82971C16C84BA2F1CC93DD3AA854EA13845D24F1735D7C620D8080F8AC64A537`, shader bundle `E734631FB7DE49C00237C1F0F4DBB28DFA491DF6DF177C7D79353BE354C4AA78`. Guarded QA-only deployment; no public binary release. Source-checkout build and push recorded in local final verification. Raw evidence: `artifacts/checkpoint-241/`, `../ValheimQA/artifacts/20261019-191840/`, and both named production-frame runs in the Russian report.

Next, test bounded two-commit residency and delayed speculative velocity readback with exact parity/fairness/empty/remove/reject cases before another48s run. This is an architectural proposal, not implemented performance work. Paid cancellation/physical refund, special objects and multiplayer remain open; all14 goal blocks and active Goal API retained.
