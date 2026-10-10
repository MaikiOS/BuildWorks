# QA0.19.136 — paired transform axes

G/S followed by two different axes selects XY/XZ/YZ, including reverse order.
Both axis buttons are highlighted. Move uses the selected plane; scale changes
only those axes. Numeric movement applies the value per selected axis. Repeating
the primary axis changes local/world frame without losing the pair; a third axis
selects a single axis. Rotation and Shift exclusion-plane semantics stay intact.

Geometry, normal/QA builds (0 warnings/errors), Host25185596, localization945,
compiled isolation, preflight and final Unity Workbench492 passed at
2026-10-10T14:05:55.8691308Z. Tests include mouse/numeric/native Z-button input,
pair bounds, cancellation, Undo/Redo, mode changes and focus loss. Store/format
are unchanged; full Store passed .135 and was not repeated. Initial overlapping
builds hit CS2012 on Geometry.dll; sequential builds passed. verify unavailable.

No Valheim, world save or network test. Continuous cursor remains pending:
installed Unity exposes Input.mousePositionDelta but no public cursor warp.
Physical-gizmo-only capture would leave keyboard G/S edge handling unresolved.
Manual instructions: docs/QA_0.19.136_RU.md.

Correctness: PASSED; Ponytail: Ship. Local code commit cdafadfc0d6dc4b59cb0ed458a416e544fef6361.
Installed only TerrainRamp-1.0-Test;137 existing config/import/save files unchanged.
BuildWorks.dll:8E8EB0C31CE1E60D5B99CE91C99F2E40F2330B903EF39449F352C1265C77737A.
Geometry.dll:AE5341BC76B709870E2CC822A78AC982DFAF330411E858E989438798C92E7A8D.
