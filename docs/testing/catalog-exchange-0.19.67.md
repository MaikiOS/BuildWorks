# BuildWorks 0.19.67 player acceptance

TerrainRamp-1.0-Test only. Ostrix launches Valheim.
180 isolated Unity states and native UI clicks are not Valheim host proof.

1. Hammer → Blueprints: faster category scrolling. Import file opens a folder
   browser with Downloads/Documents/Desktop/Up, without requiring a memorized path.
   Old rows disappear while changing folders. Import blueprint/vbuild/native JSON/ZIP
   preserves the source and reports the separate copied file path.
2. Dialog field input/click/wheel cannot move the camera or place parts. Escape
   closes only the dialog; the close-frame click also cannot place a piece.
3. Export allows folder, extension-free filename and BuildWorks JSON or ZIP.
   Result stays open; Copy path returns the full path. Duplicate names cannot
   overwrite existing files; edit the name and retry. Repeat in the editor catalogue.
4. Reimport the export; compare XYZ size, signs, groups and support poses. Compare
   a foreign construction using non-unit prefab roots with its source mod: no double
   scale or arbitrary shrinking. Missing/unsupported dependencies are explicit, not partial.
5. Editor Space/F3 Tools creates a support. Tree rows contain only objects/supports.
   Select support, G/R do not move parts; assign placement origin, save/reopen and
   compare. Delete/reset centre are Tools actions, not tree buttons.
6. With hints open, test dialogs at 1280×720 and the owner's wide resolution,
   UI scales 100/120/140%. Footer controls remain accessible above hints.

Full foreign-data preservation, interactive scenes above 512 parts and ordinary
item/holder contents are unfinished. World-support reload/remote-client durability
is unverified. This accepts one checkpoint, not the complete active goal.
