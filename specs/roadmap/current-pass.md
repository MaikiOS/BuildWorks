# BW-00 Dedicated profile acceptance

**English** | [Русский](current-pass_RU.md)

Owner: Ostrix. **TerrainRamp-1.0-Test only**. Candidate 0.19.38 / Valheim 1.0.16.
Automated results and installed hashes go in HANDOFF. No new binary release.

Implementation: enforce/test deployment rejection of Default, Default-Compat-Test,
TerrainRamp-Test and traversal; use lab BepInEx/camera; synchronize source/docs;
run Release, Geometry, Store, Localization, Bridge, WorldLayout, HostContract and
guard checks; install the pair with Valheim closed. No visible redesign/recipes.

## Owner series

Use an existing disposable lab world, not production saves. The owner controls
launching, saving/reloading and the second client.

| Test | Expected result |
| --- | --- |
| 1. Hammer/favorites | First entry works; select does not build; MMB star/count updates immediately |
| 2. Menus/input | Held RMB selected/unselected pieces/groups; typing does not fire shortcuts |
| 3. Contacts | Wall, 26°/45° beam, roof, furniture touch surface; Q/E changes held point |
| 4. Snap | Footer mode; preview within 2 m without early capture; capture 0.55 m; below-grid target reachable |
| 5. Anchors | Two group pivots/one blueprint anchor; tree/viewport/world use intended frame |
| 6. Array/Contour/Undo | New Array Pack/no gap 2×1; wheel extends row; preview=apply; one Undo; Contour works |
| 7. Editor save/reopen | Parts, hierarchy, pose, scale and anchors preserved |
| 8. World Esc | No partial series or duplicate refunds; original blueprint ghost restored |
| 9. World reload | Owner save/exit/reload preserves approved rotations/scales |
| 10. Camera | B, Shift-forward near earth, editor transitions; no freeze/unintended stop |
| 11. EN/RU | Labels/hints/numbers fit; no raw tokens/clipped commands |
| 12. Multiplayer separately | Matching lab versions; host/remote place/remove/reconnect/restart agree on transforms/resources/ownership |

Return PASS/FAIL/NOT TESTED by number; first failing action and profile log.
Do not publish personal log data. Explain engine restrictions rather than assuming
every restriction is a mod bug. BW-01 awaits relevant baseline acceptance or an
accepted bounded fix. Multiplayer gates new world/network behavior, not isolated
UI proposal preparation.
