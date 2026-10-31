# BuildWorks0.19.328 — private alpha

[Русский](ALPHA_0.19.328_RU.md) · [Controls](CONTROLS.md) · [Installation](INSTALL.md) · [Roadmap](ROADMAP.md)

**Known issue in alpha 0.19.328:** 27 producer prefab names differ in letter case from the native registry. This is not fixed in the issued package; review missing parts on import. The correction and successful 251/502-part catalogue checks belong only to the internal 0.19.329 candidate. Eight bounded native data checks are accepted for that candidate. Four direct producer-profile checks (.blueprint/.vbuild) have passed independent review. Acceptance of 2.17 covers codec/profile data only in the internal 0.19.329 candidate. Gameplay sections 3–13 and multiplayer remain open.


The current owner-issued manual-test package is **0.19.328**, not a public beta or Thunderstore upload. Its filename is `BuildWorks-0.19.328-alpha-test.zip`; obtain the authorized private copy from the owner. This documentation update does not publish a binary or create a release download. The internal 0.19.329 candidate has passed eight bounded native data checks. Four direct producer-profile checks (.blueprint/.vbuild) have passed independent review. Acceptance of 2.17 covers codec/profile data only in the internal 0.19.329 candidate. Strict player-state endpoints differ; the cause is not established. Inventory, source files and catalogue content hashes were preserved; the ordinary-save comparison covered metadata only. This run does not establish UI, world construction, new visual frames, networking or a cold-process test. The next requirement is 2.18; the 2.16 HOLD is unchanged.

## Included changes

- Support-first/retry ordering and restored inactive placement ghosts retain final native validation and ordinary payment. Real wall/beam contact may support a chest; this does not allow floating placement.
- Hold **Alt** and drag a Move arrow/plane to preview a copy from its original pose. No movement creates no copy. Release LMB commits once; Esc cancels; Ctrl+Z/Ctrl+Y undo/redo.
- **Display → Ground level Y (m)** sets a fixed reference plane, initially0, accepting negative/fractional values. Edits/Undo/Redo do not move it or change world terrain.
- **Display → Check structural support** is optional and off by default. Blue means maximum support; red can still meet the minimum. Grey/Unknown means unverified, not safe. Read selected support/minimum/source or uncertainty. Editing/saving remain available; the game validates actual terrain, station, cost and permission conditions separately.
- The327 startup/F9/build-camera/furniture/snap fixes and approved ghost surface/scatter/recovery remain. Separate white volume haze stays off. No administrator no-support mode is included.

## Accepted evidence and remaining work

Two owner blueprints have bounded paid placement/cold-reload evidence: nine chests for90Wood and five pieces for10Wood. These are earlier placement checks, not a repeat in the final structural smoke.

**5.8:** targeted Unity input covers axes/planes, no-op/tiny/cancel/Undo/Redo/group hierarchy; earlier native checks cover one wall, X/XY gestures. Full native groups/axes, metadata/source bindings, save/reopen, long documents and focus/menu/camera acceptance remain open.

**5.7:** accepted R14g smoke used one real woodwall profile100maximum/10minimum and assumed ground0→−20→0, producing support100→0→100 without document revision change. Toggle/negativeY used UnityEvent/TMP callback; returnY used a setter. It does not prove physical mouse/text entry or a native colour frame. Exit/Discard released editor/input/analysis; recorded inventory,168catalogue file hashes and5063ordinary-save metadata records were unchanged. Closure is a historical QA receipt, not a current process check.

No new structural cold-process test, all-prefab/all-mod/performance/full-corpus or host+remote acceptance is implied. The 8 ms scheduling budget is soft, not a frame-time guarantee. Full sections1–14 remain required:137 stable IDs,34 scoped accepted and103 open. Current order is 2.18–2.23, then later sections; unresolved2.16 strict-state/legacy-consumer work remains mandatory before release. Guides, assemblies, adaptive pieces and EarthWorks stay future milestones. Docs do not complete mechanics.

## Files and proof

The normal-only ZIP has10entries and includes installation EN/RU, changes, QA_SCOPE_EN, license/SHA256 and the runtime folder. It includes no QA bridge/helper/controller, token, world, character or configuration. Verified host: Valheim Steam build26249040 with BepInEx5; other stacks and multiplayer need their own tests. Follow [installation](INSTALL.md), keep backups and do not mix the supplied files. For a failure provide a fresh log, exact versions, permitted blueprint and placement context; retain normal protections and costs.

Repository .280 native editor/import images remain historical controlled camera/Canvas captures. Concept cover/workflow drawings are illustrations. Existing R14e/R9b Workbench images are laboratory fixtures, not328 native screenshots or329 proof. No new native colour frames/GIFs were made for this update. Private author originals/component-state/QA identities are not published. Unknown raw data is preserved inert; archive preservation does not prove gameplay restoration.
