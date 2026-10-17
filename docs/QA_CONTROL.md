# Isolated native QA control

[Русский](QA_CONTROL_RU.md) · [Current build/evidence](CURRENT_STATUS.md)

`BUILDWORKS_TEST_STAND` enables the local bounded keyboard/mouse/state channel. Normal builds exclude QA control and its InputSystem dependency. It is actual Valheim input, not Valheim inside Unity Editor. The control channel uses isolated files; optional GABS/ValAgent transport is a separate explicitly armed helper. No always-on public gameplay service is introduced.

Only the existing authorized QA profile/world/character is allowed. Do not publish the personal stand token, create new saves, reset provisioning or replay item grants. Launch through Thunderstore TerrainRamp-1.0-Test **Modded**. First confirm process/profile/isolation, then current world, focus, position and inventory. A historical receipt or session file alone does not prove a live process.

From the canonical BuildWorks source root in PowerShell7:

```powershell
$state = .\scripts\Invoke-QAControl.ps1 -Action state | ConvertFrom-Json
.\scripts\Invoke-QAControl.ps1 -Action batch `
  -SessionNonce $state.nonce -StopRevision $state.stopRevision `
  -StepsJson '[{"milliseconds":100,"lookX":20}]' -Capture
.\scripts\Invoke-QAControl.ps1 -Stop
```

Read fresh state before each dependent sequence. F12/stop.flag, focus loss, physical intervention, world/player changes or stale nonce/revision stop input and release owned devices/UI state. A timeout is not permission to replay a potentially completed action. Inspect the same live request/process and its new reply. Capture output includes a screenshot path after JSON; parse the JSON boundary explicitly.

The local model pilot is stopped by owner request. Current QA uses deterministic/native inspection and scoped scenarios, not a claim of autonomous human-speed play. Native UI/input, limited batch, paid house/reload and two-wall fog have separate receipts; none proves all manual/network tests. `Test-QAControlContract.ps1` and `Test-QASaveIsolation.ps1` are static/compiled gates, not game execution.

See the [live list](LIVE_CHECKLIST_RU.md) before a scenario. Storage grants/WAL operations must not be replayed. Whole-house v11/board is not ready for owner testing yet. [Original channel history](QA_CONTROL_HISTORY_206.md) retains old counters/failures.
