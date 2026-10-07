# BuildWorks 0.19.80 player acceptance

**English** | [Русский](itemstand-placement-0.19.80_RU.md)

Installed on 2026-10-07 only in TerrainRamp-1.0-Test; main DLL 0.19.80.0 and both hashes verified.
Ostrix launches Valheim in a disposable test world/character only.
Unity228/26 fixture cases are not game proof. [Combined checklist](full-acceptance-0.19.80.md).

1. In the editor, author a small blueprint with a native horizontal or wall mount
   and one compatible ordinary item from Tools. Put the matching item and normal
   construction materials in inventory. Place through Hammer: exactly one item
   is consumed; orientation, quality, variant and actual item properties match.
   Repeat with one-item and multi-item inventory stacks and both mount types.
2. Remove the matching item before starting: placement must explain the missing
   item and must not start construction. Add it and retry. A captured full
   snapshot additionally requires exact native properties, including inventory
   cell and flags; a merely similar item must be refused, not rewritten.
3. Place two mounts from a multi-item stack. Count remaining inventory items,
   mounted items and construction resources. No free contents or double spending.
4. Force a native pause after a filled mount by omitting materials for a later
   structural part; press Esc. Native removal should return construction resources
   and drop the mounted item once. Count world drops as well as inventory.
   If cancellation reports pending/uncertain state, do not manually refund it.
5. Save/reload a successful placement: mounted properties and orientation persist.
   A remote client sees the same placement; repeat place/remove with the client
   initiating it and verify access-denied/ward behavior without bypasses.
6. Cleared native mounts place empty. Unsupported foreign state, populated
   containers and armour state must still explain refusal, not silently vanish.
   Import/export/reopen preserves their archival bytes independently.

Report game/plugin versions and steps 1–6 separately. A failed resource/item
count, wrong orientation, lost contents or delayed attachment stops acceptance.
