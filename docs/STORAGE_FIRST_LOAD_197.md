<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .197 — exact first-load player inventory comparison

Verified 2026-10-16 on Valheim Build25909357. v5 WAL retains bounded immutable
native player-inventory bytes from paired persistence, bound to the verified
inventory hash and source process session/Player instance. Old v1–v4 files remain
unchanged, with their original exact checks and holds.

On manifest mismatch, the new fallback requires a different Player, identical
current Local canonical-profile inventory bytes, and exact direct fields matching
one native Load. Cells/counts/durability/quality/variant/flags/crafter/custom data
remain exact, including prefab-dependent validity. Context/profile are checked
again after readback. No item mutation, Save/Load, repair, replay or global game
serialization patch is introduced.

The first .197 native probe FAILED despite green .NET fixtures: the product
comparison lacked Single rounding after multiplication on Mono. Explicit outer
float casting now emits Mul(.01f)→Conv_R4, matching native Load's Single field
store. HostContract pins that instruction. No epsilon is accepted.

Final guarded QA deployment preserved131 protected files:
BuildWorks SHA25632780B019012EA1318BEB82E65F707678B3E8E6D49C6E1303F9F842FDF6882F3;
Geometry SHA2566E44B56C1E247356D29061EBFE1B84EE9845DDE31EA5E37D1B76B79749E0EB9B.
Thunderstore Modded/exact QA identity, all14 real items and canonical profile
passed the actual comparator/parser. Wrong expected durability was rejected.
Entire visible inventory remained identical, Wood1, legacy WAL hash unchanged.
No grants/moves/construction/new operation. Protections restored; normal logout,
game exit and exact own helper cleanup verified.

Normal/QA0/0; first-load24/paired222/recovery116/journal36/return271/profile54/
world104/transfer276/reservation31; Geometry/Store/Host/control/isolation/loc1104;
independent correctness PASSED/Ponytail Ship re-reviewed after rounding fix.
UI unchanged; Workbench not repeated; verify helper unavailable. Private receipts
in `artifacts/storage-recovery-197/rounded-native/`; raw saves/WAL stay out of Git.
Expected negative control is passed:false, separately asserted as refusal.

Native evidence covers the comparison only, NOT a new v5 operation, transfer,
return or settlement. Repeated generations, worn target contents, production
filled placement, mounts/armor stands and server/network authority remain open.
The aggregate-only legacy hold is not released. Next: justified durable completion
after paired persistence without new items/refunds, with crash/replay checks;
never delete/reset existing evidence or repeat the old QA start. Full goal ACTIVE.
