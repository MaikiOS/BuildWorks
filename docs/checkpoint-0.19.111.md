# .111 — explicit ArmorStand state omission

This is a limited conversion policy, not full ArmorStand support. Native17/portable7
remain unchanged. The default retains unsupported state and its placement blocker.
Import and export offer a separate copy without equipment and pose, with affected
source/prefab counts before confirmation. Back resets the destructive choice.

Admission requires an exact source-bound `ArmorStand` `.blueprint` Info row with
valid pose/slot-count/item/variant grammar, no ZDO or chance, and no opaque headers.
Other unknown Info, ZDO, connections, probability and header transforms remain
blocked. Empty/missing prefab scale is never fabricated. Known storage and mounted
contents choices stay independent; unsafe clear choices remain disabled.

The copy retains exact source text and the imported original bytes. Removing active
state does not resurrect it on reopen/reimport. Changed-content imports omit the
old paired PNG so it cannot depict removed equipment. The UI loss list is grouped
by source/prefab, clipped and scrollable. Source evidence is parsed once per review.

Primary evidence: PlanBuild's [capture](https://raw.githubusercontent.com/sirskunkalot/PlanBuild/master/PlanBuild/Blueprints/Blueprint.cs)
and [placement](https://raw.githubusercontent.com/sirskunkalot/PlanBuild/master/PlanBuild/Blueprints/Components/PlacementComponent.cs)
handle ArmorStand equipment/pose separately from root transforms. BuildWorks does
not copy their gameplay code or execute their restoration path.

Verification: normal/QA0/0, GeometryTests, full StoreTests/corpus, HostContract,
localization862, full QA isolation, whitespace, Unity Workbench276 and independent
correctness/ponytail review. Corpus:401 eligible Info-only armor records;3 documents
have armor as their only unknown state. This is not host placement acceptance.
Default corpus still has350/353 format-compatible documents and25951 blocked state
records; no blanket unknown-state bypass was introduced.

New self-authored fixture: tests/fixtures/armor-state/10-armor-state.blueprint.
See QA_0.19.111_RU.md. No Valheim launch or world writes. Native empty-stand build,
resources/cancel/reload/network and full equipment restoration remain open.

Verification pitfalls resolved: new tests initially omitted the preparation/frame/
detached-copy steps; a short list did not exercise scrolling. The wrapper still
expected252 images until both new states were added. Formatter named-pipe access
was denied in the sandbox; the identical read-only check passed with IPC access.

Additional required gates: LocalizationTests, DeploymentChecks, EditorBridge and
WorldLayoutChecks passed. The latter now runs the real Session assembly against
inactive native Piece/ZNetView in isolated Unity, not fake-null uninitialized
Unity objects. Real50000-part plans and atomic missing/persistence refusal passed;
this is not Valheim lifecycle/build/save proof. EditorBridge pins and round-trips
its fixture basis instead of dereferencing an absent scene. These pre-existing
fixture failures were fixed without changing production guards. The first bridge
rerun was denied artifact writes in the sandbox; the identical artifact-only run
passed with permission. The verify helper is unavailable; direct checks were used.
