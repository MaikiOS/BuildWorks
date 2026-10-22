# Checkpoint308 — saved credit recovery and process visibility

22 October2026. No production/version change. QA.273 stays installed; .274 remains candidate306.

`SavedCreditRecovery.ps1` compiles the actual saved-receipt constructor branch with journal/profile/context boundaries substituted.17 checks PASS: sparse-index recovery requires full saved inventory proof; settled replay confirms existing credit. Missing/conflicting disk/loaded receipts, absent slots, changed exact inventory summary/bytes and failed capture/readback refuse confirmation; receipt/custody changes during capture are also rejected. Removing `Matches(credited)` is detected by the negative control. HostContract now includes this check and passes against the current host/.274. `verify` is absent; independent correctness/Ponytail review PASS; reviewer reran17 checks and the negative control.

Evidence: `artifacts/checkpoint-308/recovery.json`, `recovery.log`, `recovery-negative.log`, `host.log`. These substituted boundaries do not prove native profile save/restart, source identity or context. No production insertion/bypass was added.

Attempted .274 preparation stopped before token read or Deploy: `An existing game session must not be changed.` Sandbox Get-Process reported0; full-visibility check found the original PID57236/start2026-10-22T17:51:00.7863531+03:00. The prior handoff claim of owner-game exit was incorrect and is corrected. No process/input/profile actions, bridge preparation, launch, DLL or save writes occurred.

Check process absence with full visibility; sandbox absence is insufficient. Keep guarded Deploy/Prepare. Next after actual owner exit: deploy/hash .274, native missing-station message and prepared306 controlled saved-credit interruption → normal Quit → fresh process without hook → exact inventory/receipt/cleanup → fresh replay. Requirements8.7–8.8 remain open.
