# Publish checklist — BuildWorks 0.19.37 alpha source

**English** | [Русский](PUBLISH_CHECKLIST_RU.md)

## Ready locally

- [x] English-first README, feature guide, controls, architecture, localization guide, and public roadmap;
- [x] complete Russian counterparts;
- [x] proprietary/source-available license and contribution rules;
- [x] clean source, tests, scripts, and Workbench copy without game DLLs or user data;
- [x] Release build: 0 warnings, 0 errors;
- [x] localization: 596/596 keys and embedded-resource verification;
- [x] Geometry 104, Store, EditorBridge 3, WorldLayout 10, HostContract, Unity Workbench 81/81;
- [x] Git remote points to `https://github.com/MaikiOS/BuildWorks.git`;
- [x] release changes are grouped into one verified checkpoint commit.

## Before publishing 0.19.37

- [x] install only in `TerrainRamp-1.0-Test` after explicit owner approval and while Valheim is closed;
- [x] complete the English in-game UI smoke;
- [ ] complete the Russian in-game UI smoke;
- [x] accept the reported slanted-piece Q/E positioning in-game;
- [ ] verify the persistent footer, 2 m target preview, unchanged 0.55 m snap threshold, then the pending roof/furniture, Q/E-below-grid, and fresh-Array smoke tests;
- [x] generate a 0.19.37 binary ZIP from the accepted DLL pair;
- [ ] update screenshots if the in-game language layout differs from the Workbench;
- [x] receive explicit owner authorization to commit, push, and create the prerelease.

The 0.19.33 ZIP and manifests are retained as historical alpha artifacts. They are not a 0.19.37 release. The 0.19.34 source report is retained as the rejected dotted-token baseline; 0.19.35 records the accepted English-localization fix.
