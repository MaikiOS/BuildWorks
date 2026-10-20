# 261 — two remaining directions through the fog house

2026-10-20. QA **0.19.249** remains byte-identical; gameplay source/shader unchanged. Real Valheim passed **12+80+80+5=177 checks**, producing **1040 source frames** and two videos. This proves the scoped state/recording path, **not approved v11 appearance or perfect smoothness**. [Русский](CHECKPOINT_261_RU.md).

Only TerrainRamp-1.0-Test and existing BW_QA_43d3719a/world1137745799, cloud=false. PassiveMobs and session god/ghost were verified first. The existing adapter created its own unpaid28-part Forest Hut plan/board and borrowed production fog. No native construction pieces, resources, terrain edits, resets or catalog repopulation. Main run: `../ValheimQA/artifacts/20261020-190028/`.

Directions2/3 extend the earlier0/1 house evidence from250: approach, enter, hold2seconds, exit, recover8seconds. Direction3 starts at the previous exit, so its approach completes immediately and has no capture frame. A bounded pre-route recording prefix is checked separately.

| Observation | Direction2 | Direction3 |
| --- | --- | --- |
| Scenario |80/80, zero errors/optional failures |80/80, zero errors/optional failures |
| Source recording |520PNG/51.903s |520PNG/51.914s |
| Host observation |2110frames/58.862s |1938frames/54.012s |
| Average under temporary36FPS cap |35.83FPS |35.86FPS |
| Maximum host interval |135.01ms |108.93ms |
| Held contact |12parts/129920compared texels |12parts/136576compared texels |
| After recovery |All28: zero pages/nonzero/maximum/ledgerFlags |All28: zero pages/nonzero/maximum/ledgerFlags |
| Maximum queue / velocity CPU-copy |2 /0bytes |2 /0bytes |

Both recordings submitted/read back/wrote all520frames, passed exact first-frame RGB/PNG comparison, restored camera state after every capture and released readback/writer resources. Every PNG was decoded and its byte count reconciled. GPU field metrics run inside the recorded workload: this is neither clean renderer benchmarking nor GPU execution timing. A10Hz capture cannot establish36FPS visual smoothness.

Main-run `house-side2.mp4` and `house-side3.mp4` preserve capture timestamps as VFR:960×540/52.04s/521encoded frames, including concat's closing duplicate;520source frames each. Contact sheet: `artifacts/checkpoint-261/house-contact-sheet.png`. Viewed samples show the house remaining through traversal, but framing is distant and bright background haze obscures detail. Game-environment and mod-effect contributions were not isolated. Approved-reference equivalence and stutter acceptance remain open. Next: closer fixed framing and clear weather in a bounded QA scenario, then comparison with approved v11.

## Rejected preparation and verification fixes

Initial run `20261020-185806` passed12/12 preparation but its runner rejected two HarmonyX missing-type warnings. Source `ValheimQA/vendor/ValAgentBridge/Src/BackpackCompat.cs` and the250 preparation report confirm optional `AdventureBackpacks.API.ABAPI`/`Backpacks.API` presence probes. Only this exact pair of Warning/source/text entries is now accepted during preparation; every other warning/error remains rejected. The original rejected report is retained; recording had not started.

Independent review required draining asynchronous recording before teardown. The private runner now bounds its wait for resourcesReleased, checks cadence restoration and actual fixture Close receipt before logout. Five injected cleanup cases passed. Failed cleanup retains the game for reconciliation and prevents blind bridge restoration. A PowerShell empty-array ProcessId failure at the initial wrapper's end was fixed; the first bridge restore was completed separately after confirming process exit. The second run finished its entire wrapper automatically.

The collector was corrected for capture-before-StartWallPass ordering, immediate reverse approach, and zero comparedTexels when recovery has no allocated pages. Held contact still requires nonzero comparisons. These are evidence-processing corrections, not gameplay fog changes.

## Isolation and remaining scope

Each run creates only its own new plan/lock, then cancels it normally. Saved magic/checksum/ID/world/revision2/nonempty cancellation operation were independently verified.77 prior project files in the first run and79 in the second retained their hashes; existing refund journals were untouched. Inventory projection was unchanged.4141 ordinary save files retained membership/size/mtime; ordinary save contents were not hashed. No unexpected config changes; isolated QA saves/control changed normally. Both logout→main_menu→Quit paths completed, no game/editor remains, and both restores verified3DLLs. Main-run logs contain no exceptions/shader errors/fog queue overflow.

Evidence: `artifacts/checkpoint-261/{native-verification,session-verification,video-verification}.json`, collectors/reviews and the main run's complete reports/MCP/runtime/logs/cleanup receipts/snapshots/frames/metrics. The rejected preparation remains separate.

The primary9.2–9.7 gate remains approved native v11 appearance and acceptable smoothness. Parallel8.7→8.8 review confirms that durable paid terminal cancellation needs fresh composite construction/refund absence evidence; historical CleanupSaved alone is insufficient after world rollback. This is design, not a connected public board action. Faults/rollback, all items/contents, paid/admin construction and networking were not tested here.

No DLL rebuild/deployment for these artifacts/docs. `verify` skill is absent; actual native checks, independent evidence review and private-runner checks were used. All14sections/115stable IDs/16scoped checks remain, with no upgraded requirement status. Goal API stays active; objective text is unchanged.
