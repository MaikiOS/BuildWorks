# Fast Valheim control: research 2026-10-12

Ostrix requested tools for faster game control. No tools installed, game launched
or local performance measured. [Russian detailed report](GAME_CONTROL_RESEARCH_2026-10-12_RU.md).

- [NitroGen](https://github.com/MineDojo/NitroGen): Windows image-to-gamepad model;
  authors explicitly limit it to last-frame reactions, without long-horizon
  planning. Valheim/BuildWorks compatibility is unverified. Its current
  [environment](https://raw.githubusercontent.com/MineDojo/NitroGen/main/nitrogen/game_env.py)
  uses xspeedhack and pauses/unpauses game time per action; unsuitable unchanged
  for native physics, building and networking acceptance.
- [Airtest](https://github.com/AirtestProject/Airtest): local image-based game UI
  test scenarios, not an autonomous player.
- [DXcam](https://github.com/ra1nty/DXcam): continuous Windows capture; capture
  FPS does not establish model decision speed or performance on this PC.
- [PyDirectInput](https://github.com/learncodebygaming/pydirectinput): scan-code
  SendInput and relative mouse movement; input only, no scene understanding.
- [Poco Unity SDK](https://github.com/AirtestProject/Poco-SDK/tree/master/Unity3D)
  requires Unity project integration; no verified Valheim BepInEx drop-in.

Recommendation: reuse the isolated QA channel with a bounded local feedback
executor. Airtest is a candidate for UI assertions; adopt capture/input libraries
only if measurement justifies them. Existing in-process input is already local;
the per-command PNG/result/model round trip remains, but its time breakdown is
unmeasured. Another mouse MCP alone does not fix that round trip.

Pilot acceptance: safe camera/hammer/UI scenario, full elapsed-time measurements,
stop/key-release/focus/death guards and unchanged game time, then new .163 snap
cases. Native cost, placement and authority rules remain intact. Implementation
and dependency installation are pending.
