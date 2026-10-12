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

## Follow-up: local inference and screenshot candidates

Initial search was incomplete. [Gamini](https://github.com/yumozi/gamini) has a
real pipelined video/infer/action loop, but its client calls Gemini with MP4;
Ollama needs adaptation. Capture FPS is not decision FPS; key release on stop
needs verification. [Valheim AI Agent](https://github.com/itenev/valheim-ai-agent)
README still says Pending, while current agent/valheim_agent.py wires planner
to commands. Its ActionHandler BUILD directly calls Player.PlacePiece and CRAFT
manually debits/adds items, so this does not prove the native hammer/mod path.
[ValAgentBridge](https://github.com/skatefast29/ValAgentBridge) advertises Windows
Unity6 mod QA/state/input/scripts, but needs GABS launch environment; current
Thunderstore Modded launch rule must not be silently bypassed. Source access
was incomplete; compatibility untested. Independent read-only research performed.

Live read-only hardware/API check: RTX4090 24564MiB,2776MiB used with game closed;
Ollama PID8136, /api/tags includes qwen3-vl:4b-instruct (3295636231 bytes),
8b-instruct and2b/4b/8b/30b; /api/ps empty. Sandbox socket denial on initial GET,
successful read-only retry outside sandbox. No inference/download/settings change.

First candidate: existing qwen3-vl:4b-instruct on saved QA frames. Measure full
valid-action latency, accuracy and cold/warm behavior before connecting bounded
QA input. [Official Qwen computer-use capabilities](https://github.com/QwenLM/Qwen3-VL)
do not establish Valheim skill. Local inference removes network waiting but
competes with the game for GPU; speedup remains unmeasured. Reuse existing isolated
QA channel, preserve native costs/authority and guard stop/focus/death. This is
a proposed pilot, not an implemented autonomous player.
