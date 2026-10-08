# .113 — explicit copy without missing parts

Import and native JSON/ZIP export now offer a separate copy without unavailable
prefabs. Before confirmation, the dialog lists source file, exact prefab ID and
count. The default preserves records; Back resets the choice. Unknown provenance
is labelled as unknown rather than inventing a required mod.

Availability means presence in the loaded host prefab registry, independently
of recipes or permissions. An unavailable registry disables omission. Confirmation
rechecks the registry context and missing-ID set; changed availability requires
another review/confirmation. Any document becoming empty refuses the entire ZIP
before publication. Source bytes/evidence remain intact. Misleading source preview
images are not attached to an omitted-part copy.

Retained parts keep their transforms and pinned scale. Groups, including previously
empty nested groups, supports, anchors and custom snap points remain. Only invalid
pivot/primary references are cleared. Independent inventory/mount/armor choices
operate on retained contents. Other unsupported state is still not restored.

Automated checks passed: normal/QA builds (0 warnings/errors), GeometryTests,
StoreTests with 353-source corpus, HostContract, localization 869/LocalizationTests,
QASaveIsolation, EditorBridge, WorldLayout 17 actual Session cases, full dotnet
format/whitespace, DeploymentChecks and Unity Workbench 300 screenshots.
Actual UI clicks cover default/count/IDs, Back reset, stale-context refusal,
second confirmation and opening the resulting single-part document.

The initial review blocked silent deletion of empty groups; corrected with an
empty nested-group/source/round-trip regression. Test-only corrections close the
editor before reopening and account for fresh IDs in detached copies.
The verify helper is absent; direct checks and read-only review are required.

See QA_0.19.113_RU.md. M02 retains pending native-game acceptance. No Valheim
launch, resource debit/completion, world reload or multiplayer proof. Fog v11 is
still planned. Deployment and final review are recorded separately.

QA .113 SHA `B9CCE4B59154E3265BB3D2DAE85099B830DE681E74D4E83CACC5FC3C43F9FECD`; guarded deployment verified. All48 QA files and
library unchanged. Independent correctness and complexity review passed.
