<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Исторический снимок: версии, галочки, «установлено/следом» и ручные команды ниже относятся к исходному этапу. [Текущее состояние и очередь](CURRENT_STATUS_RU.md).

# Local agent real-Valheim pilot — 2026-10-12

Authorized after Ostrix entered QA world. Process29660, loader0.19.164, fresh
nonce1dc7485f4b6b480d803aab942fa6ec0d and BW_QA_43d3719a verified. Player is
alive at spawn after the prior death, inventory empty, log confirms no reissue.
No DLL changes, grants, movement, build or item transfer in this pilot.

Two autonomous native inventory open/close series completed:
20261012-164326-83be126b 11.890s, model7.641/.735s;
20261012-164900-3d8e69fa 9.282s, model5.078/.688s.
First response load_duration6.636/4.244s: neither full series was proven warm.
Native true→false receipts and screenshots match; three first-series images
visually inspected. Inventory summary stays stacks=0, position within15cm.
Reports/JSONL/images: artifacts/local-qa-agent/<series>.

Stop test20261012-164750-6ee966b8 writes existing host stop.flag after
model_started, stopRevision3→4 before the decision can execute. Model says
inventory but fresh native guard rejects it; game_input=false and no batch.
Its failed status with 'QA was stopped' is the expected negative-test refusal,
not task completion. Physical F12 remains untested; same host branch is exercised.
Earlier stop attempt20261012-164635-80ab0586 failed before input because the
monitor ReadAllText denied Python append access to events.jsonl on Windows.
Monitor corrected to FileShare.ReadWrite|Delete; all failed evidence retained.

Before input, live DescribeInventory exposed a script bug: frame prefix changed
every observation. Compare now removes only exact leading frame=[0-9]+ followed
by space, preserving all inventory content/identity/count text. Regression covers
frame-only vs actual summary changes and full mocked cycle with changing frames.
CPU4 tests pass; model_started event added. No runtime DLL rebuild needed.

Evidence covers local native UI loop and stop during inference, not pure visual
autonomy/navigation/hammer/snap/build/contents/reload/network/FPS. Grave and TEST24
not visited; main saves untouched by the scripts. Next bounded scenario should
measure look/move independently before hammer work with the empty inventory.
