# Missing-part alignment and replacement

[Русский](MISSING_PART_REPLACEMENT_RU.md) · [Current status](CURRENT_STATUS.md)

A missing prefab remains a separate editable record at its saved transform. A known local bounding box is displayed with that transform. Without dimensions, the marker says its size is unknown; an origin is not necessarily the mesh center.

1. Open the incomplete blueprint. Filter the tree for missing records if needed; groups remain intact.
2. Select one missing record and the own pieces to align. The tool offers alignment or alignment with replacement; catalog insertion can also supply available replacement pieces.
3. Review the preview. Known geometry bounds align by their centers and saved orientation; unknown geometry can only use the recorded origin/orientation. No exact surface is invented. Adjust manually if the source lacked dimensions.
4. Confirm **Align** to create copies beside the missing record, keeping source pieces and the missing record. **Replace** also removes that one missing record. Cancel changes nothing; confirmation is one Undo operation. Save/Save As persists the result.

The red box is a positioning reference, not a recovered model. A copy without missing pieces is an explicit import choice; if no available parts remain, conversion is refused. No random name-match substitutions. Only the verified legacy `wood_wall_roof → wood_wall_roof_a` migration is automatic and retains the original ID/provenance.

Evidence: [copy alignment .178](MISSING_COPY_178_RU.md), [catalog replacement .188](EDITOR_CATALOG_REPLACEMENT_188.md). Whole-corpus/native acceptance remains limited.
