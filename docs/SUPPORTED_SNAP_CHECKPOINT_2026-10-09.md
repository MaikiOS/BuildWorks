<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Supported native snap fixture — source checkpoint, not deployed

Three original fixtures are ready in `tests/fixtures/player-exchange`:
`16-supported-snap.blueprint` is a four-piece reference with two wooden floors
and two native poles. `17-supported-snap-a.blueprint` and
`18-supported-snap-b.blueprint` are independent supported assemblies for testing
an actual moving world ghost against a placed floor.

Installed native `wood_floor`/`wood_pole` assets from bundle `c4210710` supply
the sockets, unit intrinsic scales and collider bounds. Floor A has root scale
`(1.25,1,0.75)`, B `(1,1,1)`, with source yaws30/120. The reference translation
`(1.82355714,0,-1.34150636)` produces exactly one coincident native corner pair.
Pole collider Y is `[-0.5,0.5]`; floor Y is
`[-0.0328695476,0.0968835056]`. Floor roots atY`0.5228695` overlap their supporting
pole colliders by0.01m. Pole bottoms meet the visual placement base within0.05m.
The complete footprint is under8m and fits the existing64m QA yard. Native game
stability has not been executed or established by these measurements.

An isolated supplemental cohort derives native resource costs for all eight
fixture pieces, with no second Hammer. Original seven fixture entries and their
supply stamp remain unchanged. `qa-supported-snap-supplies.txt` has separate
started/complete replay protection. Existing local/session/identity/network,
weight/slots, exact grant, recipe knowledge and native-save ordering guards remain.
Normal builds contain none of this provisioning. No game grants/saves occurred.
Deployment checks three SHA constants from the QA DLL before profile writes and
refuses changed fixtures or occupied destinations;11 local files are now prepared.

Normal/QA builds0/0, Store and Unity372 pass; final Unity UTC is
`2026-10-09T14:30:37.2103424Z`. Exact native calibration, three source files,
actual import/editor/native-copy save/reopen/export and effective BaseScale×Scale
roundtrip pass. The captured native visual was inspected. QA isolation passes
8 persisted-slot,25 identity,15 one-shot,13 stack,15 ground/save-ack,10 yard plane,
11 location cases. Deployment passes13 cases in a workspace replica only.
The unavailable `verify` helper is not counted as verification; independent review
and evidence are recorded in `artifacts/checkpoint-0.19.117/supported-verification.json`.

Test preparation corrected a constructor argument, exporting an unsaved raw
external record, and comparing editable Scale without its pinned BaseScale.
Final checks passed. Installed QA remains0.19.116 with SHA
`867E5E474A3C1B335500E47F1B2DC3A6250AC1FAA36E6D92E766E904F33DCF3A`.
No deployment, Valheim execution, live profile or save writes occurred.

The exact deferred player procedure is in
`SUPPORTED_SNAP_CHECKPOINT_2026-10-09_RU.md`: after a separately verified deployment,
use Thunderstore Modded and isolated BW_QA_43d3719a, import16, edit independent X,
Save As/reopen, place on the prepared yard, then import/place17 and snap the ghost18
at90° relative to it. Wait30seconds, save/reload and inspect unchanged geometry
and no reissue. Automatic supplies/stations are prerequisites; the player does
not hand-build supports or gather resources. Main worlds remain excluded.
World snap/stability/reload/network remain open, as do the rest of the module andv11.
