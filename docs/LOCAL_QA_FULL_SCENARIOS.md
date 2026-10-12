# Complete local-agent scenarios — 2026-10-12

Game tests are paused by the owner. No model, GPU, game input, deployment or save
mutation is authorized by offline preparation.

Canonical source: [full-suite.json](../specs/qa/scenarios/full-suite.json).
[Russian contract and scenario index](LOCAL_QA_FULL_SCENARIOS_RU.md).
`Prepare-LocalQAScenarios.py` produces12 whole task packets with49 evidence
milestones and29 hashed input files, human-readable instructions, preflight and
an offline-preparation event. It never calls the model/game. Own fixtures30–32
cover all-missing rejection,1024 supported-layout parts and malformed JSON.
The larger supported layout is not runtime stability proof; probe cells and a
clear loaded QA footprint are mandatory before construction.

Coverage: full import/edit/build/reload; format/export/scale/text; editor tools;
missing replacements; world selection; rotated world snap; inventory/containers/
mounts and costs; cancellation/failure/recovery; admin/non-recipe objects; heavy
performance/UI; persistent v11 projects; server/remote-client persistence.

Each scenario is given whole to the model; it chooses actions from fresh evidence.
Completion requires every milestone, typed authoritative observations and an
append-only action/outcome log. Unsupported evidence is unverified; absent
capabilities/features/environment are blocked. Unknown outcomes are never replayed.
Reload needs a durable checkpoint and a newly authorized session, not nonce reuse.

The present mission runner only proves small whole inventory/camera tasks:
16.312s and19.844s, yaw20.204°/20.074°, unchanged inventory. Earlier malformed
model actions were rejected and retained. All12 full scenarios are correctly
reported blocked until safe UI actions, full document/object/item observations,
evidence evaluation and reload resume exist. Admin/project/network cases also
require their product features or separately authorized environment. No fallback
to the small pilot can establish full-scenario completion.

Third-party blueprints/provenance and extracted lab resources remain local;
they are not redistributed. Existing saves and supply receipts are preserved.
Offline package CPU3 passed; mission CPU4 and pilot CPU5 are separate checks.
The absent verify helper and untested full runtime remain explicit limitations.
