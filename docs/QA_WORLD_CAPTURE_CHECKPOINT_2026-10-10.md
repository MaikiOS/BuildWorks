<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# 0.19.126 — isolated QA world-selection capacity

Source only; installed QA remains0.19.117. Selection and TrySaveWorldSelection
reuse EditorPartBudget: normal512, QA50000. The QA guard runs before adding
piece513, once at the crossing, and again before capture/save above512.
Refusal keeps existing selection/source and reports a localized message.
The current area batch stops on refusal or capacity; a later attempt is allowed.
This fixes the review's repeated-denial case: otherwise count stays512 and up to
49500 remaining candidates repeat safety checks/exceptions/status updates.

RequireLoadedSession verifies pinned token/Local QA world+character+player
identities and closed local server without peers, dedicated/open mode or cheats.
Normal DLL contains no QA path. Costs/permissions/formats22/11/coordinates±512,
aggregate snaps20000/anchors512/per-piece codec512 unchanged.

Normal/QA0 warnings/0 errors, Host25185596, localization931, compiled QA isolation
PASS. IL assertions pin normal512/QA50000, one threshold guard before mutation,
catch/refusal handling, and batch bool/branch consumption. Initial test matched
TrySave instead of actual TrySaveDocument; corrected the test, not the guard.
Independent reviews/commit: `artifacts/checkpoint-0.19.126/verification.json`.
Missing `verify` helper is disclosed; direct checks/reviews used.

Geometry/Store/Unity492 passed .125 and are unchanged, not rerun in .126.
Actual PrecisionPlacementSession is not exercised by the inert Unity fixture.
Large game capture, failure-path responsiveness, save/reload/network remain
unproven. Existing selection is loaded, screen/raycast-based within128m;
area plus adjustable vertical volume UX is still pending. IndexOf/sort/visibility/
highlight/capture performance still needs measurement.50000 is a bounded QA
ceiling, not an ordinary server feature or performance claim. Eight parser
refusals remain. No deployment, Valheim launch or save mutation.
