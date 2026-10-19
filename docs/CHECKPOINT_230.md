# .230 — embedded v11 and independent part presentation

19 October 2026 · [Русский](CHECKPOINT_230_RU.md)

The verified Linear bundle138133B, SHA `6F2A58531F61A3E12FCE61A549546092B699815B85E2457DA31C3F261F8F6F3B`, is embedded without shader/solver changes. LegacyDD9576… stays pinned.

`BlueprintFogBuilding` accepts frozen authoritative world poses; visibility masks preserve source state, retirement removes individual parts without resetting neighbours. Suspended preparation closes at a yield boundary; source textures survive pending GPU work. Mesh and volume share compact visible-owner lists; empty presentation clears bindings/passes the image through. Status counts update immediately. QA probes load the installed DLL resource. No payment/journal/refund/permission logic changed.

Passed: normal/QA0/0; full Geometry; host1.0.20/25909357/resource hash; localization1118;1748 verifier negatives/32 metadata. GPU:270ticks/18 exact fields; published87 captures/62 comparisons,16 legacy captures/pixel delta0,23 preparation including8 lifecycle cases; recovery90frames/fault6; window168 comparisons/3cycles and98,041,636B pool. Window was a Gamma numeric check, not Linear visual acceptance. Lifecycle includes pending removal/source retention, exact neighbour fields/identity/generation/clocks, hide/show/empty/final repeated removal, frozen distant world poses and invalid inputs.

Native Valheim: **33/33 in5.1s**,0 optional failures,5assets/28kernels from `LoadEmbedded`, exact velocity paging, transient4-section native wall, SpatialLedger16384 and per-part transitions. Initial27/27 had0 automatic camera frames; preserved as state-only evidence. The stronger run explicitly calls `Camera.Render`5 times, asserts a positive render counter and checks failures. This is render execution, not recorded appearance or passage through a house. Inventory JSON unchanged; no deep byte snapshot in this scenario. No construction/resources/catalog/terrain/player movement/reset.

Installed QA **0.19.230**, SHA `A7C9DCA2DD303BF77CF0771EF057D93A0DAD7282D8266E23C33983E88780EF44`. Own PID42324 exited normally,3bridge DLLs restored/verified;4137 regular-save size/time entries unchanged, not content-hashed. Evidence under Default: `BuildWorks/artifacts/checkpoint-230`, `ValheimQA/artifacts/20261019-100650`. Previous GPU receipts were archived before replacement.

Independent correctness/Ponytail review caught/fixed a local64m check incorrectly applied to world coordinates and a stale visibility status count. Distant GPU and immediate native status regressions cover both. `verify` skill unavailable; actual checks above used instead.

Still open: persisted-project renderer hook, journal-derived visibility, ordinary Create path, cancellation/recovery, whole-house appearance/performance and network. Next read-only phase mapping: null/NoEffect→show, Intent/Observed→hide, Committed→retire. GPU failure must preserve construction/board; use bounded shared resources. No EarthWorks integration. The full14-block goal remains active.
