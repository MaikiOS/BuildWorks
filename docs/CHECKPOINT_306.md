# 306 — source pause reasons; recovery test prepared

22 October2026. **Local0.19.274 candidate, NOT installed.** Installed and natively
verified QA remains0.19.273/[305](CHECKPOINT_305.md), SHA
`2A5F8CBAB7D540592BDB0F8F2712913D6FDFE772B9AAAFEEEF5AFA029155CBD4`.
[Detailed Russian scope](CHECKPOINT_306_RU.md).

After the unchanged native HaveRequirements(CanBuild) refusal, the candidate now
distinguishes missing crafting station (including NoWorkbench), unavailable DLC,
and material shortage in native order. Station names localize. Costs and placement
permission do not change. Actual-method9PASS, normal/QA builds0warnings/0errors,
HostContract/localization1129 and independent correctness/Ponytail reviews pass.
The first pre-build localization check detected a stale embedded catalog; the
post-build check passes. Actual UI/native306 is unverified; verify is unavailable.

A separate local ValheimQA helper prepares a one-shot exact-entry interruption
after durable profile credit but before journal confirmation. Actual prefix/status/
disarm23PASS, including wrong proofs, one-shot replay, cleanup without a world and
unpatch failure. Independent review passes. It changes no production payout code.
Native injection/restart has NOT run and no kill/power-loss proof is claimed.
The future scenario must observe triggers1, retained CreditIntent, exact saved
AfterBytes/receipt, then fresh-process E recovery without another insertion,
cleanup and another replay. Never resurrect already settled sources.

Owner plays Default-Compat-Test; his process/profile/world/input were untouched.
No new QA launch/Prepare/Restore/Deploy. Parallel-mode investigation found that
DirectPath can omit stopProcessName and pin its own spawned PID+starttime, while
the current name scan can adopt an unrelated Valheim as external. Exact endpoint/
PID proof, denying both generic stop/kill, and shared Steam/Unity preferences/
display/load checks are still needed. Parallel mode is not implemented or accepted.

The full115IDs/16checks/14sections remain. Installed surface303 is unchanged.
Other item properties/fault windows, UX/networking and the full goal remain open.
