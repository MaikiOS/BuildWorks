# BuildWorks 0.19.35 alpha source status

**English** | [Русский](ALPHA_0.19.35_RU.md)

0.19.34 was rejected during its first English in-game smoke because Valheim stopped dotted localization tokens at the first dot. 0.19.35 fixes that shared boundary and is installed only in the isolated `TerrainRamp-1.0-Test` profile. Repeated English/Russian acceptance remains pending.

## Fix

- readable source key `editor.view.title` now becomes Valheim token `$buildworks_editor_view_title`;
- token creation and `Localization.AddWord` use the same conversion;
- all 596 converted keys are underscore-only and collision-free;
- Valheim's missing `[key]` form now returns the English fallback;
- a host-independent test exercises English, Russian, store errors, and fallback behavior.

## Automated evidence

- Release build: 0 warnings, 0 errors;
- localization catalogs 596/596, runtime tokens 596, static references 576, exact embedded resources: PASS;
- localization runtime test: PASS;
- Geometry 104, Store, EditorBridge 3, WorldLayout 10, HostContract: PASS;
- Unity Workbench: 81/81 PASS;
- installed pair matches the verified local SHA-256 values.

No commit, push, pull request, binary package, or public release was created. Persistent operations, curves, and the F9 redesign were not started.
