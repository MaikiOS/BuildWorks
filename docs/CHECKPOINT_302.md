# 302: board cancellation and refund routing

22 October2026. Source **0.19.270** connects the real board button to the existing
durable cancellation/removal/payout operations. Volume fog is unchanged; no speed
fix or v11 acceptance is claimed. Disabling the volume remains a proposal.

Before every action the router checks the current private Local context, live local
player, usable hammer, exact open registered board, distance≤5m and idle operations.
Unpaid cancellation grants nothing. Paid removal uses the existing protocol;
Prepared recovery retains its saved operation. Saved removal enables a separate
board confirmation to issue the refund. Uncertain operations refuse replay;
IssuedSaved never issues again. Continuing construction is disabled after removal
starts. Preflight failure leaves the view open to display the error.

Remaining construction costs are explicitly labelled and are not a refund quote.
Raw Paid() alone is unsuitable for a quote because Observed rows may contain Paid.
The existing issuance backend retains actual supplied properties and one-shot guards.
Native acceptance and remote authority remain unverified.

Normal/QA builds0warnings/0errors; actual router/resume with inert native boundaries30;
file cancellation64/payout175; HostContract, finalizer56/interaction61; localization1127;
compiled normal/QA fog-hook boundary pass. File fixtures require approved access:
the ordinary sandbox reports access denied; the approved temporary-directory run passes.
No game save is used. The verify skill is absent; direct checks and independent
review are used. Evidence: `artifacts/checkpoint-302/`; review initially BLOCKED on the false unpaid prompt, then corrected/PASSED. QA .270 was hash-verified on installation, then superseded by .271; see [303](CHECKPOINT_303.md). Native302 lifecycle was not run.

Prior301: native88/88 passes and normal exit/restore3,97project hashes/4141ordinary
save metadata unchanged. Comparison analysis fails because TestTools truncates each
large receipt at3000characters before saving, losing route origin and player position.
Copied audit count/ablation flags do not prove missing measurements. Originals remain
intact. A compact RouteStatus helper builds but has not been replayed in game.

QA launch arguments now pin monitor1/fullscreen/borderless3440×1440, the DPI-aware
physical primary DISPLAY1. Argument evaluation passes without launching; actual
window placement remains unverified. Do not mix new1440p and previous1351p samples.

Next: authorized native E/UI paid cancellation, saved absence without native drops,
exact sealed refund, reload/pickup and no duplicate. Existing paid objects were not
removed, no grants/resets/EarthWorks changes. v11, sustained load and networking stay open.
