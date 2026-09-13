# BuildWorks 0.19.34 alpha source status

**English** | [Русский](ALPHA_0.19.34_RU.md)

0.19.34 is an automatically verified source baseline. It has not been installed or accepted in Valheim; the installed test-profile baseline remains 0.19.33.

## Changes

- added direct English-fallback and Russian-overlay registration through Valheim `Localization`, without adding Jotunn;
- moved current player-facing Hammer, Blueprint Editor, Outliner, Array, Contour, F9, validation, and store messages to 596 stable keys;
- separated translated labels from category, material, source, action, and persistence IDs;
- localized generated copy, Array, and Contour names without coupling `BuildWorks.Geometry` to Valheim;
- added an executable localization gate that scans both assemblies and verifies the embedded catalogs in the built DLL;
- added English-first architecture, localization, and contribution guides with Russian counterparts;
- added formatting rules and documented the main runtime ownership boundaries.

## Automated evidence

- Release build: 0 warnings, 0 errors;
- localization: 596 matching English/Russian keys, 576 statically referenced runtime keys, both TSV resources embedded;
- Geometry: 104 checks;
- Store, EditorBridge 3, WorldLayout 10, HostContract: PASS;
- Unity Workbench: 81/81 states PASS;
- Assembly and file version: 0.19.34.0.

## Manual gates

English and Russian must both be checked in Valheim across the indexed Hammer, blueprint library, editor, Outliner menu, Array, Contour, F9, and validation errors. No raw `$buildworks_...` key may be visible. Save/reload and multiplayer remain separate gates.

Persistent operation recipes, curves, and the future F9 redesign were not started.
