# BuildWorks catalog game checks

**English** | [Русский](current-pass_RU.md)

Test **0.19.54**, only in **TerrainRamp-1.0-Test**. Reply only to **1–8 below**.
Ostrix accepted chair checks .53; [archived](archive/pass-0.19.53.md), no repeat.

## Preparation

Use a test blueprint copy. Editor → **Tab**. Start with your normal resolution
and language. UI scale: **Camera → Interface**. Workbench covers 1280×720,
1920×1080, 2560×1440, 3440×1440 at 100/120/140%. In game test your normal and
one available smaller resolution; restore settings afterwards. No other profiles.
The catalog contains available pieces from the current hammer, including mods;
it does not unlock recipes or expose arbitrary non-hammer prefabs.

## Reply checks

1. **Layout/readability.** Try normal/smaller resolution and 100/140% UI.
   **Expected:** close/search/pages fit; cards reflow without overlap.
   Names read clearly; full long names appear on hover. Details hide when
   space is insufficient; category selection never stretches navigation.
   **Report:** good, or screenshot + resolution/UI scale.

2. **One-click placement.** Click a wall/furniture card, not its star.
   **Expected:** catalog closes, placement ghost follows cursor, no second
   confirmation. Esc cancels. Repeat with a stored blueprint.
   **Report:** good, or card and failed/extra action.

3. **Shared view filters.** Select material/category, switch **Atlas ↔ Quick**
   and **Parts ↔ Blueprints**.
   **Expected:** both views share results; tabs retain their own queries/filters.
   **Report:** good, or the lost filter.

4. **Sections/completeness.** Building, Decor, Furniture, Resources, Crafting;
   building families Walls/Floors/Roofs/Frame/Openings/Stairs. Reset filters
   and search a familiar modded piece.
   **Expected:** all available items remain in All parts, including unknown categories.
   **Report:** good, or name/prefab and expected section.

5. **Correct upgrade station.** Crafting → Workstation → Workbench/Forge → Upgrades.
   **Expected:** upgrades linked to that station, visibly labeled, not the station
   needed for construction. Details navigate to its group without placement;
   narrow screens use the Workstation picker.
   **Report:** good, or upgrade and wrong/missing station.

6. **Search/pickers/pages.** Search name/prefab/#index, pick material/source,
   scroll to end. Page with Q/E, PgUp/PgDn, grid wheel and page input; type Q/E
   inside search.
   **Expected:** typing never changes page/tool; picker wheel scrolls its list.
   First Esc closes picker, next catalog. No results is explicit; reset restores items.
   **Report:** good, or exact failing sequence.

7. **Favorites/recent.** Star a card, open Favorites, switch views; take a
   different piece, cancel, reopen Recent.
   **Expected:** star never places; shared favorites; latest taken item first.
   Lists currently last only for this editor session; closing it clears them.
   **Report:** good, or unexpected action.

8. **Details/language/icons.** Hover long names/upgrades; toggle eye details;
   change game language and reopen editor.
   **Expected:** text stays inside panels; full tooltips fit. Own sections/materials
   translate; external mod names stay intact. Recognizable star/eye/close/reset,
   no missing-glyph squares.
   **Report:** good, or screenshot + language.

## Reply format

“1 — good, 1920×1080/140%; 2 — good; 3 — …”.
Failures: number, action, expected/actual, screenshot. No extra lettered lists.
Workbench uses actual product code and fixture items, not Valheim runtime proof.
Owner acceptance remains open until your replies.
