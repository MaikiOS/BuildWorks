# Current blueprint format and exchange contract

[Русский](blueprint-format_RU.md) · [Current acceptance](../../docs/CURRENT_STATUS.md)

Native library schema **24** (`CompositeBlueprintStore`); portable schema **13** (`BlueprintExchangeFormat`). Schema versions are not package versions.

| Input/output | Supported boundary |
| --- | --- |
| `.blueprint` import | Declared PlanBuild/Infinity-style text dialects; finite positions/quaternions/component scale, recognized headers/sections/state only |
| `.vbuild` import | Recognized BuildShare dialects; extension alone does not establish support |
| `.buildworks.json` / bounded ZIP import | Validated internal documents and archive entries, part/depth/byte guards; no arbitrary model files |
| Single export | Internal JSON, full BuildWorks ZIP, or constrained PlanBuild `.blueprint` with reviewed losses/native companion |
| Batch export | Full BuildWorks ZIP; selection/category/name/folder review |
| BuildShare export | Not implemented |

Store/edit/share retain prefab IDs, stable record IDs, transforms including XYZ scale, groups, text, free anchors/user snaps, supported material and item state, dependencies, versioned provenance and opaque source records. Raw source files stay unchanged. Archived unknown fields are not understood/restored behavior; unsupported required structure/transforms fail explicitly. Export writes no unrecognized BuildWorks extensions to foreign formats.

Prefab scale and nested TRS are separate from relative part scale. Original world Coordinates/Rotation are provenance, not an extra transform for already-local rows. Missing prefab entries keep transforms and known metadata, with uncertain markers if size/source are unknown. All-missing conversion is refused; reviewed no-missing copies do not alter the source. Only verified exact `wood_wall_roof → wood_wall_roof_a` migration is automatic; no fuzzy substitutions.

Content categories have independent import/export/build choices. Survival/admin authority is not granted by understood data. New objects receive fresh network identity. Native reload and foreign-mod compatibility must be verified per declared behavior.

Limits: document50000 parts; normal interactive/editor512, isolated QA/Unity editor50000; owned store32 cards/12 categories/100000 aggregate parts. Byte/depth/archive and memory guards remain. See source constants for exact validation; these maxima are not performance guarantees.

[Corpus provenance/permissions](../../docs/EXTERNAL_CORPUS_REGISTER.md) · [Historical format evolution](blueprint-format-history-206.md). No claim that every external file or unknown dialect is supported.
