# .236 — native ArmorStand pose/slot archive

2026-10-19. Source/build .236; installed QA .233. This completes a data-preservation slice, not armor restoration.

Native selection capture now dispatches to an ArmorStand adapter. It reads actual private view, pose with m_pose fallback and ordinal i_item/i_variant/i_itemData; it rechecks view/ZDO identity, revision, queue, pose and slot layout. It never writes inventory/world state. Inactive ID0 packets stay inactive; unknown bytes remain inert.

Own `valheim-armorstand-v1`: Info=pose:count, envelope1 ordered VisSlot/ID/nullable name/variant/nullable exact bytes. Limits:64 slots,128 name chars,24576 bytes/item,1MiB envelope, unchanged32MiB document. Strict UTF-8/Base64/bounds/EOF; immutable packages, null/empty preserved. Existing v109/110 reader reused. Known active name/hash dependencies survive opaque packages; uninspected records are reported separately. Excluding equipment retains pose and bytes.

Local25/portable14 reject this discriminator under older declared versions; legacy files still load. Older plugin versions cannot read new documents. External PlanBuild omission remains a separate source-bound policy.

Verification: normal/QA0 warnings/errors; full Geometry/Store PASS;28 actual-source CPU capture checks on a fake read-side host; current host1.0.20/Steam25909357/SHA94998885…F3A; localization1122; compiled QA separation; offline Unity Roslyn RuntimeHost+Editor compilation; two independent source reviews PASSED. Dependency gap found by review was fixed and regression-tested. These are not native capture or rendered UI tests.

Receipts: `artifacts/checkpoint-236/verification.json`, capture-checks.json, geometry/store/build/host/localization/separation logs, review.md, source-manifest.json and offline-compile/. Initial compiler/stub and version-expectation failures were corrected, not counted as success.

Pose/equipment preview and placement remain unavailable, including equipment-disabled and administrative placement. Mod-specific fields beyond known native slots are not claimed supported. Native capture, visual preview, payment/restoration/reload/network still require implementation or game proof.

No game/GPU launch/deployment: Ostrix is playing and guarded deployment forbids replacement. Installed QA .233 unchanged. `artifacts/BuildWorks-0.19.236-manual.zip` is a normal two-DLL development package, not a public release. Next free-GPU priority remains the fog PollingCandidate and demonstrated queue cause, then native whole house. Paid cancellation/refund and the full14-block goal remain open.
