# Prepared local vision probe for Valheim frames

Prepared only: CPU self-tests and archived-frame preflight passed. No real model
inference, game input/launch, speed/accuracy/FPS or autonomy proof. Installed QA
.163 unchanged. [Detailed Russian protocol](QA_VISION_PROBE_2026-10-12_RU.md).

`scripts/Measure-QAVision.py` uses Python standard library. Default makes no API
call. Three SHA-pinned local .162 screenshots (native19/22/23) have visually
reviewed labels in `specs/qa/vision-162.json`; screenshots are not distributed in
source-only Git. The model gets image/prompt, never expected labels or hidden
state. Output is four observation fields, never executable actions.

```powershell
python.exe scripts/Measure-QAVision.py --self-test
python.exe scripts/Measure-QAVision.py
```

Six CPU tests cover strict JSON, no default API, modified frames, duplicate keys,
HTTP redirects, and mocked six-request success / immediate truncated-answer
generation-stage failure with saved partial report. Mocked responses prove no
inference quality.

Only for an explicitly authorized compute run:

```powershell
python.exe scripts/Measure-QAVision.py --run-inference
```

Default existing model qwen3-vl:4b-instruct, fixed loopback Ollama, no proxy or
redirects/downloads, context4096/output128/temperature0. Model may remain loaded
60s after last request; other models are not unloaded. Timeout may leave server
computation active; no automatic retry. Do not run alongside production GPU work.

Unique artifacts/qa-vision/<time>-<id>/report.json records model/digest/loaded state,
prompt/schema, raw answers, per-field accuracy and full frame-read-to-answer time.
Generation-stage failures save a partial report with complete=false. Errors in
/api/tags, /api/ps, model lookup or output-directory creation happen before a
report exists and terminate preparation with an error.
First/repeated requests are not assumed cold/warm. Six observations across three
frames do not prove navigation/building/new-scene recognition or reliable latency
percentiles. Next stage requires actual inference measurement then bounded QA
executor; TEST24 post-respawn/reload and native .163 snap remain open.
