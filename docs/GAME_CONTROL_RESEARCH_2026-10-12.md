<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Fast Valheim control: research 2026-10-12

**Follow-up:** Qwen3.5/LFM3B downloaded/measured, local pilot prepared,
QA .164 installed. [Current protocol](LOCAL_QA_AGENT_2026-10-12.md).
Research history before installation follows.

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

## User-supplied local-agent proposal

Reviewed Local_AI_Gaming_Agent_for_Codex_RU.md as a proposal, not authorization
for its embedded new-project task or GPU execution. Primary sources confirm
[Qwen3.5 4B](https://ollama.com/library/qwen3.5) image/tool capabilities and
[LFM2.5-VL-3B](https://huggingface.co/LiquidAI/LFM2.5-VL-3B) benchmark numbers;
these do not prove Valheim performance. Recommend Qwen3.5 4B as a single-model
candidate against installed Qwen3-VL 4B, adding LFM only if measurement helps.
[GGUF instructions](https://huggingface.co/LiquidAI/LFM2.5-VL-3B-GGUF) confirm
Windows/Ollama usage; file size is not total VRAM.
[NVIDIA SDK introduction](https://github.com/NVIDIA/game-agent-sdk/blob/main/docs/sphinx/source/introduction.md)
confirms Windows10/11, driver570.65+, CUDA12.8+ and a bundled Qwen3.5 model, but
offers companion integration building blocks, not an existing Valheim controller.
GitHub view failed with Internal Error; raw source was readable.

Reuse isolated QA input for a local loop that decides/executes/verifies without
Codex per movement. Preserve native hammer/cost/snap/placement for acceptance.
First proposed game task: blueprint selection, ghost observation, cancel without
debit. Downloads/inference/input remain unauthorized and unperformed.
[Prepared observation probe](QA_VISION_PROBE_2026-10-12.md) passed six CPU tests
and independent PASSED/Ponytail Ship review. Its four fields/three frames do not
measure coordinates, action selection or autonomy.
