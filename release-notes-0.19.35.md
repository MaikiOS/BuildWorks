# BuildWorks 0.19.35 alpha source baseline

**English** | [Русский](release-notes-0.19.35_RU.md)

This source-only alpha fixes the Valheim localization token regression found in 0.19.34. Dotted catalog IDs are preserved for contributors but converted to underscore-only keys at the Valheim boundary.

- 596 English and 596 Russian entries;
- 596 unique Valheim-safe runtime tokens;
- explicit English fallback for missing `[key]` output;
- isolated English/Russian registration regression test;
- Release 0/0 and Unity Workbench 81/81 PASS.

The build is installed only in the isolated owner test profile and still requires an in-game English/Russian smoke. No binary release asset is staged.
