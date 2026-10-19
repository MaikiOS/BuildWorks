# .229 — deterministic equal-distance snap selection

19 October 2026 · [Русский](CHECKPOINT_229_RU.md)

The owner's18-part stand reproduced order-dependent position changes, up to1m, with identical socket coordinates:104 native and64 F9 cases. Native queries included BuildWorks-generated points; this is not an unmodified-game benchmark.

Only automatic native scratch lists are sorted by source world XYZ, then target world XYZ, before enumeration. Native distance/radius remain primary. F9 and composite aimed-pair ties use the same geometric key. No epsilon, surface attraction or rotation inheritance. Manual indices, nonlocal players, invalid radius and foreign snap patches bypass sorting. Prefabs, editor order and the acceleration algorithm remain unchanged.

Passed: normal/QA0 errors/0 warnings; full Geometry; host1.0.20/25909357,97 passive-handler assertions, localization1118; headless Unity Harmony fixture17125 sources ×128 targets including manual/foreign/nested/exception paths. Real Valheim:171 F9/guard cases,4/4 scenario steps and0 optional failures;576 stand variants/2304 queries, native/F9 order changes0/0, unchanged point set and6 manual cases. Exact inventory bytes unchanged. No construction, resource grants or spending.

Installed QA **0.19.229**, SHA `88BA8B7CB6F349C1688AAA1CAD995F7BC648A5C4B8C5DF0F6AD6BF53D41D68D6`, TerrainRamp-1.0-Test only. Own PID38872 exited normally; three bridge DLLs restored. Local evidence: `ValheimQA/artifacts/20261019-092626`, before-fix evidence `20261019-084759/snap-stand-pairs-230.json`. Owner payload remains local/unmodified.4137 regular-save size/time entries unchanged, not content-hashed; expected QA state/saves and one config header changed, all14 BuildWorks setting lines unchanged.

Parallel v11 prerequisite: frozen62061B legacy bundle SHA `DD9576EC6925082A5B59615C7EB813CC8B93067FFCBAB5D926AACE29558C61DD`, both comparisons load the exact verified bytes. Correct/corrupt/candidate negative controls and1748 CPU verifier negatives/32 metadata cases passed; updated Editor tests compiled headlessly. A stale recovery fixture lacked the already-required `faultReceiptCases=6`; corrected without weakening the verifier. No new GPU run, promotion or project-fog integration is claimed.

Still open: camera/ray-induced input changes, full Hammer UX, owner acceptance, explicit held tilt mode, project fog/Create/refund/general recovery/network. This transient run does not prove payment, placement or reload; prior bounded .226 evidence remains separate. Goal API remains active.
