# Checkpoint275 — outward-facing ground board and native hammer interaction

2026-10-21. QA **0.19.255** addresses the owner's failed .252 board inspection. Visual-only native sign/pole clones preserve meshes, UVs and materials; no Piece, Sign, ZNetView or inventory is cloned. The upright stake samples loaded terrain one metre beyond the conservative world footprint, with its written side away from the building. Unknown/unloaded terrain hides presentation rather than forcing zone loading.

A narrow Player.UpdateHover postfix restores only an owned registered board through the native search, retaining obstacle/reach, existing hover, hammer/alive/local/UI/transport guards. Native Interact and Runtime.Open still own opening. No resource/rights changes. Old paid plans are preserved; terminal cancellation remains open.

Normal/QA0/0;host/localization1122/1008;geometry135;StoreTests62 PASS. The sandbox file-lock check initially failed with Access denied; normal Windows execution passed. Independent read-only review found no blocking source defect. Native fixture failures (missing displayName; an invalid exact-tight-bounds expectation) are retained in runs104751/105641, both normally exited/restored. Native254 run110509 passed21/21; dark native vertex tint and an oversized stake then failed visual readability. Owned label colors/stake width were corrected. Final255 run111953 passed23/23 with inventory unchanged; board-front-255.png shows readable outward-facing lettering. Native hover/Interact dispatch is distinct from a physical E-key test and full paid construction.

See the [Russian report](CHECKPOINT_275_RU.md) for paths/hashes. Fog input overflow, v11 owner acceptance, complete paid cancellation and multiplayer remain open. Local QA uses unaccepted shader271; publishing the board fix is not promotion of that shader.


Published .255 source carries the board fix over the previous Git fog baseline. Local installed QA .255 additionally contains experimental bundle271; shader candidates267/271 are not promoted by this source checkpoint. The source checkout was built separately and passed host/localization.


Final .255 cleanup: normal logout → main_menu → Application.Quit;no remaining game process;restore.json verifies3/3 bridge DLLs. All97 project-file hashes unchanged,inventory unchanged;final-audit.json confirms installed .255/hash. No game session remains running.
