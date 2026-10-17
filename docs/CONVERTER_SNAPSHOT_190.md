<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .190 converter snapshot checkpoint

Conflicting native itemData and loose ItemStand properties/custom data are now retained as unsupported source evidence instead of silently overwriting the snapshot. Matching duplicates preserve exact native bytes. Native109 consumes but ignores the cheated bit;110 applies it. The reader follows that version boundary; source bytes stay intact. Legacy loose cheated=true emits110.

The editor catalog now retains the resolver's Hammer-unavailability reason alongside preview warnings, without blocking editing or weakening native placement guards. No automatic prefab aliases were added.

Geometry reproduced both defects before their fixes and passes afterward. Store covers unknown/source preservation through portable rereading. Normal/QA builds, host/isolation/localization checks pass. Independent correctness and lean review are required before checkpointing. The verify helper is unavailable. Three targeted formatter diagnostics match an unchanged baseline block.

Native .190 UI acceptance remains pending; Workbench uses a Session stub and cannot prove this integration. Populated construction, administrative recovery, persistent project/v11 and multiplayer remain incomplete. Deployment is tracked separately. [Russian evidence and limits](CONVERTER_SNAPSHOT_190_RU.md), [native manual scenario](MANUAL_QA_190_RU.md).
