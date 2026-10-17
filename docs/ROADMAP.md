# BuildWorks roadmap

**2026-10-17 · [Русский](ROADMAP_RU.md) · [Current evidence](CURRENT_STATUS.md)**

This is the forward plan. Completed changes belong in the [development journal](DEVELOPMENT_JOURNAL.md); the former append-only roadmap is preserved as [history through .206](ROADMAP_HISTORY_206.md). No dates or completion percentages are promised.

| Order | Deliverable | Depends on | Done when |
| --- | --- | --- | --- |
| **1 — current** | Approved v11 on an actual unfinished house | .206 multi-source renderer; bounded contact ownership | Existing Forest Hut has recognizable textured transparent pieces, body passage/standing/return, independent matter and measured memory/FPS in Valheim; owner accepts look |
| **2** | Persistent project and stake/board | 1; durable per-part identity, payment settlement and server authority | Missing resources leave pass-through unfinished parts; hammer opens materials/continue/cancel; reload/reconnect reconstructs state; confirmed cancellation returns exactly paid resources once |
| **3** | Supported import/edit/build/reload across formats | Existing .195 house path; explicit source contracts | Declared .blueprint/.vbuild/internal cases retain transforms/scale/signs/anchors/materials; missing-data choices are clear; full batch paths and foreign-format interoperability verified |
| **4** | Items, storage, mounts, armor and admin restore | 2's durable accounting; supplied-item identity | Independent settings, survival supplied attributes, no free repair/duplication; server-approved admin state; cancel/crash/replay/reload tests pass |
| **5** | Usable large-world capture and heavy construction | Loaded-area/boundary rules; 1–4 performance measurements | Area + adjustable top/bottom + manual include/exclude works; no stale duplicates; catalog/editor/ghost/build/save remain bounded; small/direct vs large/planned threshold established |
| **6** | Server library and sharing between players | Server role checks and durable state | Admin publishes players/admin entries; permitted players see and accept personal copies; direct player sharing; host+remote/reconnect/server restart and denial tests pass |
| **7** | Remaining editor acceptance and release | Targeted regressions for reported UX; 1–6 | Axis/wrap/replacement/camera/anchors and supported resolutions pass; scoped manual and native checks documented; owner explicitly authorizes a binary release |

Newly reproduced regressions in import→editor→native construction take priority over expanding features. Current owner order puts v11/board first; other requirements remain in the [project contract](../PROJECT_CONTRACT.md). Completed foundation: native precision/editor, selected import and Forest Hut reload, file/batch exchange, server UI foundation and bounded v11 renderer. Foundation is not full milestone completion.

Later, after this module: EarthWorks terrain/material/planned-building integration and separately agreed original adaptive pieces/guides. No OBJ/FBX/glTF importer, terrain transfer or EarthWorks runtime dependency is added here. Curves/Line A/B need renewed tool-semantics approval.
