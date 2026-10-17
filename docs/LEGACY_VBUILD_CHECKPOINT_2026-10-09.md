<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Legacy .vbuild numeric slots — source0.19.118

Source-only checkpoint. Installed QA remains0.19.117, BuildWorks.dll SHA256
`C43966C8FFB5BC83D5B725BACD04E4C30F8BA7BD497B58D4D52C853AC9C701C6`.
The existing [QA.117 acceptance](QA_0.19.117_RU.md) remains valid; this fix is not installed.

## Confirmed cause and change

The compatible PlanBuild reader preserves positional whitespace slots and treats
empty numeric slots as zero. It accepts decimal commas:
[FromVBuild](https://github.com/sirskunkalot/PlanBuild/blob/master/PlanBuild/Blueprints/PieceEntry.cs#L621-L659),
[InvariantFloat](https://github.com/sirskunkalot/PlanBuild/blob/master/PlanBuild/Blueprints/PieceEntry.cs#L737-L757).
The original BuildShare writer was not located; no claim about its emission path.

BuildWorks discarded empty fields before quaternion/position parsing. This shifted
slots or rejected legitimate rows. A shared field reader now preserves those slots
in both import and source-bound state conversion. Only excess empty padding after
the maximum10 slots is removed; a trailing empty Z coordinate still means zero.
Numeric comma normalization applies only to legacy whitespace .vbuild. Semicolon
.vbuild, .blueprint and snap coordinates retain their existing strict parsing.
Finite values, bounds, normalized quaternion, field count and source/payload binding
remain required. Exact original source text, BOM, line endings and row references
survive native/portable save and reopen. No source file or format schema changed. The chance field retains its existing strict barrier; this change handles quaternion/position numeric slots.

## Evidence

- A new test failed before the fix with fields/line2; it now passes under en-US/ru-RU.
- Tests include blank quaternion slots, a trailing zero, comma decimals, native and
  portable reopen, known state conversion, a different valid state packet blocked
  by row binding, malformed input and strict dialect separation.
- Independent review found excess trailing padding rejection. A second regression
  reproduced it before correction; final Store and both builds passed afterwards.
- Normal/QA builds0warnings/0errors; Store including readyfixture22 passed.
- The existing353-file corpus remains350 parsed/3 blocked,133738 exact ZDO packets.
- CurrentHEAD0b6b5b8 baseline vs final code on209 local2v2t files:156→197 parsed,
  53→12 blocked,41 improved and0 regressed. This is parser coverage, not prefab,
  editor, survival construction, world reload or network acceptance.
- HostContractSteam25185596, localization930 and compiled QA isolation passed.
- Correctness and corrected Ponytail review have no remaining blocker.

Local receipts: artifacts/checkpoint-0.19.118. External archives retain their prior
hashes and attribution; no author files or third-party source are redistributed.
No Unity rerun was required for this parser-only change; Valheim was not launched.
The verify helper is unavailable; direct checks and independent reviews were used.

## Remaining limits and ready fixture

The12 remaining2v2t parser refusals are4 documents beyond the512 custom-snap limit,
7 documents with positions outside±512 (including objects thousands of metres
below/above the main structure), and1 object-payload refusal. They were not silently
trimmed or dropped. Named #Center remains blocked: verified readers disagree about
marker retention and centering. Unknown state and .vbuild ZDO scale also retain
their explicit barriers. The full conversion/build goal remains incomplete.

Owned synthetic file: tests/fixtures/vbuild/22-legacy-zero-comma.vbuild;
card **QA118 Legacy Vbuild**, two ordinary woodwall records. After a future.118
installation: Import file → Confirm import → Edit. Expect one wall atX3.25 rotated
90° aboutY, another atX-2, with unit scale; save a copy and reopen. Wrong count,
shifted position/rotation or loss after reopen is failure. Editor-only fixture;
no materials or world construction needed. Do not run this acceptance on.117.
