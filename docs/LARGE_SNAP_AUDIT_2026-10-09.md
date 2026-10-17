<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Large snap sets — measurement before changing limits

Source0.19.118; installed QA0.19.117 remains unchanged (both hashes checked).
No production limits or gameplay code changed. The four imports still fail.

| File | Parts | Points | Distinct exact rows | Maximum assigned to one part | Distance evaluations |
| --- | ---: | ---: | ---: | ---: | ---: |
| Arani_Forte_Negro.blueprint | 5062 | 6596 | 6584 | 46 | 33388952 |
| Big_Bridge_1.blueprint | 770 | 2012 | 1955 | 14 | 1549240 |
| Decay.blueprint | 4070 | 11790 | 11790 | 24 | 47985300 |
| Flabellum.blueprint | 6265 | 17125 | 17102 | 17 | 107288125 |

All source snap and part-origin XYZ values are inside±512. Distinct rows use
exact text equality, not approximate coordinate merging. Removing duplicates
would not solve the512 limit; no points were removed.

The read-only C# measurement reproduces SnapLocalSets float squared distance and
strict `<`: equal distances choose the first source part. Self-checks cover
duplicates, both source-order ties, bounds, NaN refusal and assignment count
conservation. Independent review confirmed correspondence with production code.
This standalone assignment took about7–497ms here; it is not Unity/placement/FPS
proof. Prefab rotation/scale and transformed-coordinate codec validity were not
tested. The largest calculated snap payload is560bytes, not a full network packet.

Parser, Store, Geometry document and portable validation each enforce512
blueprint-level points. World codec separately permits512 **per part**, not per
blueprint; the measured four distributions do not exceed that limit. Keep its
preflight, size validation and owner guard. World Append creates a child object
per persisted point; total loaded-world cost still needs measurement.

SnapLocalSets performs up to107288125 distance comparisons and is used during
validation and plan preparation. ApplyBlueprintAnchors adds every custom point;
UpdateAnchorWorldPoints, TransformGizmoView projection/visibility/picking, and
auto-align collider/native comparisons traverse the arrays. Merely increasing
storage capacity would expose that frequent path to17125 points.
EnsureAnchorHandleCount also creates one LineRenderer per point before visibility
filtering; bound that allocation path before raising storage capacity.

Next implementation, still open: separate a bounded custom-point storage ceiling
from ordinary anchors across parser/Store/Geometry/portable/insertion/Undo/SaveAs
with explicit version compatibility; preserve every accepted point. Accelerate
exact nearest-part assignment and avoid repeated computation. Bound runtime
selection/render/alignment work while keeping all saved points reachable, not
just the first512. Retain per-part codec refusal before resource consumption.
Then test large native/portable roundtrips, malformed inputs, Unity/runtime cost,
deployment and Valheim/reload/network. This audit completes none of those gates.

Eight other refusals remain. One quoted sign is rejected because BuildWorks
requires valid JSON for a leading quote, while
[PlanBuild PieceEntry](https://github.com/sirskunkalot/PlanBuild/blob/master/PlanBuild/Blueprints/PieceEntry.cs)
falls back to the exact source token on JSON failure. A dialect boundary and
regressions are needed before changing that decoder; it has not been weakened.

Evidence: local artifacts/checkpoint-0.19.118/Measure-SourceSnapLimits.ps1 and
snap-limit-audit.json. Repeat in a new PowerShell7 process from BuildWorks:

```powershell
pwsh.exe -NoLogo -NoProfile -File ./artifacts/checkpoint-0.19.118/Measure-SourceSnapLimits.ps1
```

Script SHA2560580FEB6A883F0C12FA8796FEC686027E7E9BCC56AE7F24990C532A66A3256E1;
archive SHA2564A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88
checked before/after. Only four named ZIP entries (each at most8MiB) read; no
extraction, archive rewrite, third-party asset redistribution or game launch.
Builds/full tests not repeated because gameplay code did not change.

MemPalace verified the Default index root via SocratiCode-index-root-2026-10-06;
SocratiCode was green with26300chunks and an active watcher, returning canonical
symbols. OstrixMemory returned an older snap-assets note, not current cap evidence.
No reindex was requested. See the Russian companion for the full implementation
dependency list. The active goal and installed .117 acceptance remain open.
