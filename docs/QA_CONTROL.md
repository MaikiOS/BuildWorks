# Native Valheim control channel — QA0.19.141

Current2026-10-12: QA .164 installed, stopRevision guards local decisions across
idle F12/focus loss; native inventory observation added. Local inventory pilot
prepared, first runtime pending. [Protocol](LOCAL_QA_AGENT_2026-10-12.md).

[Русский](QA_CONTROL_RU.md)

The bridge is compiled only into the QA BuildWorks DLL. It applies bounded native
Unity keyboard/mouse sequences inside Valheim and returns state/screenshots.
It is not Windows automation or Valheim running inside Unity Editor. Normal builds
contain neither the bridge types nor their Unity.InputSystem reference. No ports
or network services are opened.

## Evidence

`Test-QAControlContract.ps1` now also reads the real installed host assemblies:
PlayerController movement/camera call ZInput; mouse delta/position/scroll bind to
InputSystem; low-level keys use current Keyboard/Mouse; action callbacks reach
Press/Release. An in-memory missing-camera-binding negative control must fail,
then the restored positive control passes. Game assemblies are never written.
This checks known calls/bindings, not complete data flow. Runtime KBM settings,
active UI action bindings, FixedUpdate scheduling, clicks and release still need
Valheim smoke. Log: artifacts/checkpoint-0.19.142/native-input-contract.log.
Current installed DLL receipt: artifacts/checkpoint-0.19.142/installation.json.

Normal/QA builds pass without warnings; HostContract25185596, localization961,
compiled isolation and the control contract pass. The real protocol reader passes
28 assertions including identity/replay/expiry and execution-time deadlines.
Unity Workbench492 checks shared editor cancellation plus native Button/Slider/
ScrollRect release: no click/drop, repeat stop harmless, next normal click works.
Independent source/installed-IL reviews pass. This is not native game-input proof.
Deployment receipt, when installed: `artifacts/checkpoint-0.19.141/installation.json`.

## First host smoke

Use Thunderstore **TerrainRamp-1.0-Test → Modded**, then load only the closed local
world and character **BW_QA_43d3719a**. Keep the game foreground. First observe state
and frame, then a short camera turn and a short step with another frame. Do not
build before confirming world, coordinates and visible result.

PowerShell7 from `Default`:

```powershell
./BuildWorks/scripts/Invoke-QAControl.ps1 -Action state -Capture
./BuildWorks/scripts/Invoke-QAControl.ps1 -Action batch -StepsJson '[{"milliseconds":200,"lookX":100}]' -Capture
./BuildWorks/scripts/Invoke-QAControl.ps1 -Action batch -StepsJson '[{"milliseconds":200,"keys":["W"]}]' -Capture
./BuildWorks/scripts/Invoke-QAControl.ps1 -Stop
```

`state` supplies no input. Reply includes ID/status, focus/readiness, world, player
and camera positions, selected UI, current UI module and resolution; session.json
contains build version/PID/nonce. Screenshots have a maximum long edge of1280px.
Use only the screenshot named in the fresh reply for the requested ID. An old file
does not prove the latest operation succeeded. `completed` confirms sequence
completion, not placement, resource consumption or persistence.

Each step has `milliseconds`20..2000; optional `keys` (Unity KeyCode names),
`buttons` (1left,2right,4middle), `pointer` plus normalized `x/y` from bottom left,
`lookX/lookY` in pixels/sec, `scroll`−5..5 and `text` up to256 characters. Omitted
keys/buttons are released. `text` sets only an already selected writable TMP/UGUI
field; it is not system typing or arbitrary code execution. For drag, press at the
start, hold while moving to the next pointer, then release. Pointer motion is
interpolated. Maximum32steps and10seconds of actual execution, including transition
frames; split long workflows into observed short batches. Normal completion
delivers a release edge before removing virtual devices and preserves UI focus.

## Stop and boundaries

Physical F12, physical keyboard/mouse activity, focus loss or `-Stop` abort input.
Pending editor/world-selection gestures cancel; active automatic construction
pauses. Completed native actions are not undone. UI receives pointerUp/endDrag
with original context and no click/drop; original override/current devices return.
Test stop while walking, button/drag, world selection and focus loss. Expect no
continued walking/drag/selection and working ordinary controls afterward.

On timeout the result is unknown: inspect response.json or stop. Never replay
automatically. A new session nonce and one-use IDs reject stale/duplicate input.
Batch requires the exact living, focused, isolated local session: no public server,
peers, respawn/teleport/intro, debug/no-cost or unknown/changed UI module layout.

Mailbox is restricted to
`TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/qa-saves/43d3719a7ee649d184e26ef1daa441a3/qa-control`.
The channel does not directly grant items, teleport, write ZDO, place objects or
consume resources. Existing native cost/permission/network rules remain in force.
Game reload and multiplayer need separate proof. Physical cursor wrap still needs
its own check: virtual Mouse may not support the native warp command.
Primary saves/other mods are untouched. Installed QA .142 also includes the verified empty-container rejection fix; see [scope and proof](EMPTY_CONTAINER_CHECKPOINT_2026-10-10.md). Native channel/container smoke remains pending.
