<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .147 — PlanBuild decimal commas, offline checkpoint

Source `ca8c5f7`. Normal and QA0.19.147 built; installed QA remains0.19.146.
No Valheim, Unity or input session was started while Ostrix uses the computer.

Pinned PlanBuild commit `e12ed86ff2498bbd4a9c0f5d81cb388e3aaeb549`
PieceEntry.FromBlueprint accepts decimal commas in transforms. A new regression
failed before the fix. BuildWorks now normalizes only position2–4, quaternion5–8
and optional scale10–12. Exact source/BOM/CRLF/row binding, identifiers, signs and
unknown Info/ZDO/chance remain intact. It does not copy upstream's whole-line
punctuation replacement. Empty fields, finite/bounds/quaternion/scale validation,
atomic refusal, strict snap points and semicolon vbuild retain their boundaries.
No source format, prefab scale math, gameplay or authority rules changed.

Full Store/native+portable reopen/cultures and353 BiomeBlueprints passed:
351 parser-compatible,2 blocked;133738 exact-EOF ZDO packets. Normal/QA0/0,
Geometry, localization tests/973 keys, EditorBridge, Host25185596, QA isolation
and compiled channel passed. The unchanged pinned reader independently passed
comma TRS and strict snap tests under en-US/ru-RU using documented JSON/Unity
numeric shims. Both independent reviews returned Ship.

Global format verification still reports15 pre-existing WHITESPACE diagnostics
in three unrelated files, verified identical to previous HEAD. Changed C# files
passed scoped formatting. The verify helper is unavailable. Unity WorldLayout
and Workbench deferred; no UI or geometry code changed. Game placement, resources,
reload/network and end-to-end import performance remain open. Existing public
corpus contains no comma examples; this is not claimed as the cause of old failures.

Evidence: `artifacts/checkpoint-0.19.147`; primary provenance remains in
`artifacts/planbuild-reader-0.19.140/provenance.json`, no upstream code in production.
Owned fixture: `tests/fixtures/planbuild/26-planbuild-decimal-comma.blueprint`,
card **QA147 PlanBuild Comma**. After future installation of .147+, import it from
the editor, open, Save As and reopen. Expect one sign at(3.25,-4.5,.125), yaw90°,
root scale(1.5,.5,2.25) and text **Привет, мир**, unchanged source. Editor percentages
may show100% because base root scale is separate. Failure: rejected/missing/extra
part, wrong shape/rotation, altered punctuation or changed reopened result.
No resources required. See the [Russian steps](PLANBUILD_COMMA_CHECKPOINT_2026-10-11_RU.md).
