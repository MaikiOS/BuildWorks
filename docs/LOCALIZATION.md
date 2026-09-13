# Localization

**English** | [Русский](LOCALIZATION_RU.md)

English is the required fallback language. Russian is a complete overlay. BuildWorks has no Jotunn dependency, so `BuildWorksLocalization` registers embedded TSV catalogs directly with Valheim `Localization` and repeats registration after `SetupLanguage`.

## Adding or changing text

1. Add the same stable key to `src/BuildWorks/Translations/English.tsv` and `Russian.tsv`.
2. Keep the same numbered placeholders in both values.
3. Use `BuildWorksLocalization.Text("area.action", arguments)` for immediate UI text.
4. Use `BuildWorksLocalization.Token("area.action")` only when a Valheim component performs localization later.
5. Build the plugin, then run `scripts/Test-Localization.ps1 -RequireBuiltAssembly`.

Source catalog keys stay dotted for readability. `BuildWorksLocalization` converts dots to underscores only at the Valheim boundary: `editor.view.title` becomes `$buildworks_editor_view_title`. Valheim token strings must not contain dots because its parser stops at that separator. The localization gate verifies that every converted runtime key is valid and collision-free; `BuildWorks.LocalizationTests` exercises real registration, English/Russian selection, store errors, and missing-key fallback without starting Unity.

Never compare translated strings in logic. Categories, materials, sources and actions use stable IDs such as `wood`, `other`, `vanilla`, and `blueprints`; translate only their displayed labels.

TSV values support `\n`, `\t`, and `\\`. Keys are case-sensitive. Duplicate keys, empty values, missing Russian/English pairs, placeholder mismatches, missing statically referenced keys, and embedded Cyrillic runtime strings fail the localization check.

`CompositeBlueprintStore` stays independent from Unity localization. It returns `$buildworks_...` error keys, and UI boundaries resolve them with `BuildWorksLocalization.ResolveUserText`. The legacy persisted category `ПРОЧЕЕ` is migrated to stable ID `OTHER` when loaded.

The check verifies matching catalogs, stable key syntax, placeholder parity, statically referenced keys, absence of embedded Cyrillic UI literals, and both embedded TSV resources in the built DLL. The isolated Unity Workbench copies `English.tsv` and uses an English-only test adapter; production language registration remains exclusively in `BuildWorksLocalization.cs`.
