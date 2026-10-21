# Checkpoint283 — light shortcut rejected; QA .264 retained

2026-10-21. An empty light-candidate list returned transmission1 before the24-step light loop. Exact14+14pixel comparisons and a dedicated empty-list lit control passed; background difference0.007522 retained global haze. These are synthetic render checks, not native contact acceptance.

Actual GPU timing on the same frozen28-object960×540fixture did not improve: lit88.66/88.56ms before versus89.66/90.99ms after. Strict source compiler probe failed twice, exposing `use of potentially uninitialized variable (AtlasLight)`. No warning suppression or relaxed gate.

Candidate rejected without deployment or game launch. Restored .264 shader/mirror/bundle pass the new pixel controls and compiler probe. InstalledQA .264 remains unchanged. Added two missing metric sources to Build/Test mirror lists and made compiler failures report their actual messages;4PowerShell parser/entry checks pass. Evidence in `artifacts/checkpoint-283` retains candidate failures separately from the restored control.

Independent review found no mathematical shortcut bug, but that does not override negative performance/compiler evidence. No owner retest is ready. Next: distinguish clock/input/submission/publication/visible-contact age. A separate reduced-volume-resolution experiment with full-resolution scene/surface and depth-aware composition is a proposal requiring visual/performance gates, not an approved quality reduction. Full v11, board interaction, paid lifecycle and networking remain open.
