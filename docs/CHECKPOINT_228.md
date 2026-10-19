# .228 — preserve F9 rotation; owner snap stand received

19 October 2026 · [Русский](CHECKPOINT_228_RU.md)

This is a bounded F9 correction, not completion of the world snapping issue.

The pointed-target wrapper now calls `TryAutoAlignToPiece`. All three existing callers—entry, movement and alignment toggle—search using the current player rotation. A successful pair translates its source anchor without replacing rotation. No pair leaves the pose unchanged. The former implicit collider-rotation fallback is removed; explicit Ctrl magnetic movement remains. Ordinary passive snapping already preserved native rotation before this change. Point selection, added midpoints, camera effects and a held tilt mode remain separate work. Editor, payment, rights and persistence are unchanged.

Normal/QA builds pass with zero warnings/errors; full Geometry/Store tests, host contract and localization1118/1004 pass. Independent source review passes after removing the unintended surface-attraction branch from intermediate .227. The QA-only native regression is prepared for164 temporary-object cases and exact inventory preservation but **has not run in Valheim**. Initial sandbox file operations failed; the same full Store tests passed in the normal short temporary directory outside the sandbox. No game or catalog changes were made by these CPU tests.

Ostrix supplied an immutable portable13 stand with18 parts:8 beams,5 poles,5 diagonal prefabs. Its13 identity and5 yaw−90° rotations do not cover custom root X/Z tilt. The strict file import path passed twice into temporary stores; this is not Unity/UI/native construction proof. Local evidence is in `artifacts/checkpoint-228`; the owner payload remains local in `artifacts/user-snap-stand`, outside the public source checkout.

The `verify` skill is absent in both configured local roots. Verification used the listed builds, targeted/full checks and independent review, not a claimed `verify` result.

Installed QA is intermediate **.227**, SHA `09C8C234F6C8B680158E27706FAE52AFC2FD2D86DF759B586667948E139434F0`. Corrected **.228 is built but not installed**. GABS rejected an external manually started QA process (PID45148); the owner then requested work without the game. Do not use intermediate .227 as final acceptance: its extra surface-attraction branch is removed in .228. The prepared bridge must be restored after normal player exit (`ValheimQA/artifacts/20261019-081806`); its client is closed, cleanup is pending.

Parallel read-only agents audited world snapping and project/v11 integration. They found an embedded-vs-tested-candidate bundle gap and the need for per-part visual retirement without resetting neighbours. No fog hook or EarthWorks runtime integration is claimed. Next: install/test .228 once allowed, exercise the supplied stand, and continue the approved v11 unfinished-part path. Paid cancellation, ordinary Create, general recovery and remote authority remain open. Goal API was verified active.
