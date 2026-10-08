# .110 — persisted prefab scale basis

Automated checks and independent correctness/ponytail review passed.
Deployment is pending; installed QA remains .109. No Valheim execution.

Native17/portable7 adds optional baseScale. Effective root scale is
(baseScale ?? resolved prefab base) × user multiplier. External absolute rows
pin their raw source size once; world admission derives Hammer-relative scale
and checks native range/persistence before adding parts to the placement plan.
Editor, thumbnails, Hammer visuals, previews, bounds and sockets use the same basis.

Legacy16/6 reads retain null without guessing. Explicit editor saves pin the
already displayed known prefab base; missing records remain unknown. Capture uses
localScale and rejects parent matrices not reproducible by saved rotation/root scale.
Source files remain untouched. Older builds cannot read new17/7 documents.

Passed normal/QA builds0/0, GeometryTests, StoreTests, HostContract, localization857,
full QA isolation, whitespace and Unity Workbench252. Actual controller/thumbnail/
ghost/save/reopen agree for canonical(2,.5,3)/Hammer(.5,2,.25)/source1, then multiplier.5.
Includes rotated nonuniform sockets, nullable/version gates, legacy save pin,
partial updates, overflow and scaled/sheared/mirrored parent checks. Actual Lava
Lantern bounds regression retained. An obsolete exchange multiplier expectation
was updated to assert the full persisted scale contract. Sandbox Unity startup
failed in Mono HttpListener; the same test passed outside the sandbox.

Independent review found a world-range check in the editor resolver. Corrected:
forEditor validates RootScale; world admission uses PlacementScale. Actual thumbnail
root.001 succeeds while world ratio.001/prefab1 is rejected. HostContract proves the
compiled boolean branch; the complete Session resolver is not executed in Workbench.
Builds/HostContract/252 Workbench rerun passed; final independent recheck PASSED.

Compatibility: the new converter identity may create one new .110 placement copy
beside an old v16 copy. Legacy copies are not rewritten or silently reinterpreted;
repeated selection within .110 still reuses one placement copy.

See QA_0.19.110_RU.md. Native build/payment/cancel/reload/network remain unverified.
The reported imported blueprint axis offset remains a separate unresolved symptom.
