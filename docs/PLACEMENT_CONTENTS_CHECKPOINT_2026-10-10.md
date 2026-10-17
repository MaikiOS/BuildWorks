<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# QA0.19.143 — placement contents review

Catalog placement, normal Hammer clicks and F9 share an explicit review gate for independent storage and mounted-item choices. The existing exchange review UI prepares a detached transient recipe. Saved blueprints and source files retain their contents. Existing external placement-copy creation happens only after confirmation and does not persist the contents policy.

Review eligibility may accept a known populated container whose contents can be explicitly omitted. Actual placement still requires strict validation. Unsupported Keep choices fail visibly without automatic omission. Existing resource, supplied-item, rights and network checks remain in place. Cancel, changed context/revision and pending-worker closure do not start placement. Session completion and QA input abort clear pending policy. Catalog closure is deferred until the modal has released its input guard.

Automated checks passed: normal/QA builds with no warnings/errors, localization972, HostContract Steam25185596, Store, isolation, EditorBridge, native/compiled QA input contracts, protocol28 and Unity Workbench492. UI checks cover four independent combinations, exact source/file preservation, failed confirmation, stale context and close during preparation using native UI raycasts. Independent review results and installation evidence are recorded separately under `artifacts/checkpoint-0.19.143`.

Not proven in Valheim: the new modal's Hammer/F9 flow, construction/payment/refund/reload, and the first QA input session. A ready native contents fixture remains to be prepared. Populated storage restoration, ArmorStand, loose items and administrator restore remain open; I03/I05 are not complete. Start with the isolated channel smoke described in QA_CONTROL_RU.md. Prior accepted user scenarios do not need repetition without a concrete reason.
