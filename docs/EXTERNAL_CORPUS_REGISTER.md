# External test corpus register

Verified 2026-10-12 against the local archives. Both original provenance SHA-256
values match and both archives remained unchanged after inspection. Packages
were not extracted or executed; installed mods and saves were not changed.

| Source | Actual corpus | In-file attribution | Distribution status |
| --- | --- | --- | --- |
| [OverDrive/BiomeBlueprints 1.0.3](https://thunderstore.io/c/valheim/p/OverDrive/BiomeBlueprints/v/1.0.3/) | 353 `.blueprint`; 31,547,180-byte ZIP | All 353 have a nonempty `#Creator:`. These are declarations, not independently verified authorship. CoffeeTable declares Madzobuild | README found; no separate LICENSE found in this ZIP. Its submission permission concerns inclusion in that package, not confirmed inclusion in BuildWorks. Local corpus only; no Git/release bundling |
| [DarkTrooper/2v2t 5.0.0](https://thunderstore.io/c/valheim/p/DarkTrooper/2v2t/v/5.0.0/) | 140 `.blueprint` + 69 `.vbuild`; 12,847,060-byte ZIP | 140 declare `#Creator:`; 69 `.vbuild` files have no such header. Do not invent attribution to the pack uploader | README gives general credit; no separate LICENSE or explicit BuildWorks redistribution grant found. Local corpus only; no Git/release bundling |
| `DvergrLibrary-ConSeannery` | Empty local directory | Unestablished | A directory is not evidence of a downloaded, permitted or tested third corpus |

Live Thunderstore retrieval failed (`is not accessible via this tool` / `Cache miss`).
Current website terms were not verified. No LICENSE in a particular ZIP does not
prove no permission exists elsewhere. No new download or author contact occurred.

Biome ZIP SHA: `36C5A04DF80294C6B49B21C916D808FCE60698B17479BFAB7C95F850BE12C707`.
2v2t ZIP SHA: `4A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88`.
Original provenance remains in `artifacts/external-blueprints/<package>/provenance.json`.
Fresh evidence: `artifacts/corpus-register-2026-10-12/Inspect.ps1`, `summary.json`
and one JSON manifest per package. Each of 562 files records its exact ZIP member
name, size, SHA-256, extension and literal name/creator headers. Unknown authorship
and unconfirmed redistribution are explicit. No geometry/full file content is copied.
The one-off inventory bounds reads to 32 MiB/member and 512 MiB/corpus; duplicate
member names, changed archive identity and incomplete reads fail.

| Format | Current boundary |
| --- | --- |
| `.blueprint` | Known PlanBuild/Infinity dialects and supported data; original text preserved. Inventory does not prove conversion, prefab availability, unknown ZDO behavior or build/reload/network of all 493 files |
| `.vbuild` | Distinct supported BuildShare dialects; an extension alone does not prove compatibility of all 69 files or export support for every dialect |
| `.buildworks.json` / native store | BuildWorks formats and versions governed by the existing contract; no claim of third-party readers |
| ZIP | Bounded container of supported members; not arbitrary nested archive or file support |

See the [current format contract](../specs/roadmap/blueprint-format.md) and
`ExternalBlueprintLibrary.Supported`. OBJ/FBX/glTF and terrain transfer remain
outside this module. F05/F06 remain partial: local corpus/file provenance is now
registered, but release permissions and additional permitted sets remain open.
File inventory and converter acceptance are separate from actual game behavior.
