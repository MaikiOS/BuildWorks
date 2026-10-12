# All-missing import — source checkpoint .170

2026-10-12. Installed .169 is unchanged. No deployment, game input or local-model run. [Manual .169 checklist](MANUAL_QA_169_RU.md).

The default keep-missing path previously accepted a document with zero available prefabs, including a member of a mixed ZIP. The shared import guard now uses its existing prefab-scale snapshot to refuse the whole batch before writing. Confirmation is disabled with an explicit explanation. Partial missing documents, recipe-less present prefabs and backup export remain supported. Unknown/unloaded hosts do not invent availability.

Regression tests cover standalone/mixed ZIP refusal, immutable source/catalog/previous result, no background import after refusal, existing-record backup export, and actual UI Tick import/export boundaries. Store and Tick failed before the fix, then the full Store suite and Tick passed. Both builds have zero warnings/errors; HostContract passes against Steam25185596/0.19.170.0; localization990 passes. Independent correctness review: PASSED. Ponytail: Ship. Initial UI logs missed Add-Type Console.WriteLine; red/green were repeated in child pwsh and both nonempty receipts saved. No verify skill is available; direct diff review, builds, tests and independent reviews were used.

The first implementation repeated prefab resolution; the existing unique-snapshot test caught it, and the final guard reuses `scales`. Restricted test/build runs encountered access denial; tests passed using the normal short Windows temporary directory outside that restriction. The long artifact temporary path was correctly refused by export path limits. Logs remain local under `artifacts/checkpoint-0.19.170/`.

Runtime refusal/UI are unverified. World auto-snap was not changed without a new reproducible cause. Populated storage, admin restoration, persistent construction projects/stakes/fogv11 and networking remain unfinished.
