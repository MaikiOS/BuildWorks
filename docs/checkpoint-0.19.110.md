# .110 — persisted prefab scale basis

Automated checks and independent correctness/ponytail review passed.
QA .110 deployed by the guarded script; DLL hashes match,48 QA files and library
unchanged. Checkpoint:fa66b1d. No Valheim execution.

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
CoffeeTable source already contains yaw-33.75°; a distinct axis conversion defect
is not proven. See import-axis-evidence.md and private deployment evidence at
artifacts/checkpoint-0.19.110/verification.json.

## Follow-up: axes and corpus, tests only

Workbench252 passed2026-10-08T16:19:41Z, followed by independent correctness and
ponytail review. Actual editor poses retain the literal tilted-first-row rotation
and displacement with both source row orders. Shared placement helpers match
independent identity/no-support and inverseRy30/saved-support expectations.
The original SHA-pinned CoffeeTable retains its source yaw-33.75 and displacement.
Native registry/FramePartIndex/Session/world execution remain outside this proof;
cloned child order in the shared helper test is fixed. Production DLLs unchanged.

Full local corpus StoreTests passed:353 visible entries,350 format-compatible,
3 explicitly blocked,133738 typed ZDO exact EOF,115057 known states converted,
25951 state records still blocked. Unit-base scale probe refused0 plans; this is
not host prefab availability, complete import or native build/reload acceptance.
Logs: artifacts/ui-axes-110-current.log and artifacts/corpus-110-current.log.
