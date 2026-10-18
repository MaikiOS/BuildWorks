# BuildWorks ↔ EarthWorks: boundary and proposal

[Русский](EARTHWORKS_BOUNDARY_RU.md) · 18 October 2026 · **owner boundary confirmed; integration design pending discussion**

**Current work is independent BuildWorks house construction without site preparation.** Terrain work comes later when EarthWorks is connected; its absence does not block building. The joint workflow below is a subsequent stage.

## Confirmed by Ostrix

- The unfinished building, approved v11 fog and stake/board are the future cooperation point.
- BuildWorks owns blueprints, building parts, fog and construction. EarthWorks alone prepares and changes terrain: levelling, fill, excavation and separately agreed operations.
- Cancelling the building never restores terrain or cancels performed earthworks. Later terrain changes require an explicit separate EarthWorks project.
- Both mods remain independently usable. Owner clarification: terrain preparation can be planned from the start for the selected house/blueprint; the joint project has **one board and one material store**. Appearance, wording, access and exact actions remain undecided.

## Current implementation versus plans

| Mod | Supported by current source/documents | Not ready |
| --- | --- | --- |
| BuildWorks .225 | Local unpaid project: GUID/world/revision, immutable blueprint, part IDs/TRS, restored stake, panel, cancel/replay. Valheim19/19 +14/14. | Paid continuation, paid cancellation/refund, v11 on remaining parts and server/remote use. The new operation journal is only a detached component. |
| EarthWorks 0.6.5 developer | Route, road preview, network ZDO board and six laboratory stages. `RoadProjectRecord` v4 stores geometry; `RoadProjectBoard` uses `ew_road_data`/`ew_road_stage`. | Survival/full two-client lifecycle, platforms/foundations, excavations, local terrain editing and a generic stable project ID/revision/API. |

EarthWorks sources: `AGENTS.md`; `DECISIONS.md` EW-042 (restoration is separate work), EW-062 (initial contribution is not a chest; warehouse later), EW-090 (aging/cancellation leaves terrain); `PROJECT_CONTRACT.md`; `ROADMAP.md` phases4–6; `src/EarthWorks/RoadProjectRecord.cs`; `src/EarthWorks/RoadProjectBoard.cs`. This is source/plan inspection, **not a new EarthWorks game test**. BuildWorks evidence: [checkpoint .225](CHECKPOINT_225.md).

## Architect recommendation — not yet an owner decision

1. Finish independent BuildWorks projects first: payment, continuation, safe cancel/refund, persistence and fog on unfinished parts. Integration must not delay this.
2. Complete the required EarthWorks platform/excavation tool and its server permissions/persistence. Give projects stable IDs, revisions and descriptors available after completion. A board's network identity is not a project ID.
3. Add a small optional compatibility module inside an existing mod; a third package is not needed now. Missing EarthWorks or an unsupported contract version disables only the link, preserving standalone BuildWorks.
4. The joint project starts from the selected house/blueprint and its placement. The same board lets the player define and approve EarthWorks preparation; a completed separate site is not a creation prerequisite. Internal terrain/building jobs have stable IDs, world, revisions and plan hashes; the exact schema remains to be designed.
5. One board presents terrain preparation → construction and a common store. Reservations, consumed materials and refunds belong to the particular job/module. One physical access point must not become two independently debited balances. Storage ownership and crash recovery remain to be designed.
6. Historical terrain completion cannot prove that terrain was not edited later. Report current correspondence separately as matches/changed/unknown, without forced zone loading or automatic repair. BuildWorks retains its own placement validation; the link grants no bypass.

Each mod exclusively authorizes and performs work in its own domain. The store needs one operation owner and durable reservation/consumption accounting for both consumers; independently debiting the same stack twice is forbidden. Mutating commands need expected revisions and operation IDs. Fog owns neither payment nor EarthWorks state. One board does not make building cancellation undo completed earthworks.

Ostrix **rejected** the original completed-site-only, two-board, no-common-storage proposal. It is not an alternative fulfilment of the approved joint workflow. Shared storage complexity must be solved explicitly rather than hidden behind independent chests.

### Internal design recommendation — not an implemented system

BuildWorks should own the shared site and physical board/store because they start with the blueprint. EarthWorks attaches its logical terrain job without creating another board. Standalone EarthWorks keeps its own workflow. The site needs an identity separate from both building and terrain job IDs.

One server operation executor owns the common store and persists exact items and reservations by domain/project/stage/operation. Each mod calculates and confirms its own work; the same item cannot be promised to both jobs. A repeat returns its previous result; uncertain crash state requires reconciliation rather than repeated debit or automatic grants. A successful command response alone does not prove world/inventory persistence. The exact protocol still needs design and verification.

Building cancellation must not end the shared board's lifetime while terrain work, stored items or reconciliation remain. Closing the site differs from cancelling the building or terrain job. These rules are proposals; button wording and access are not approved.

**Ostrix has not approved** blocking all building until terrain is ready, EarthWorks resource priority, accepted-item restrictions or an administrator wait bypass. These remain open. Common storage does not silently replace EarthWorks' existing distinction between non-refundable setup and unused materials.

## Future milestones and acceptance

- Standalone BuildWorks: actual debit → creation → save → reload; retry never pays twice; confirmed cancellation refunds only actual costs. Cancellation performs no terrain writes.
- EarthWorks readiness: required tool, independent costs/permissions, stable IDs/revisions, two clients, server restart, cancellation/aging without terrain rollback.
- Link: correct world/project/type/version; stale link/missing mod; reload/reconnect; terrain changed after completion. No extra debit or terrain write from BuildWorks.
- Before implementing integration, agree one board/store and test concurrent contributions, withdrawals, reservations, consumption, each job's cancellation, saves, failures and retries without loss or duplication.

## Open owner decisions

- How does the player define site shape, floor elevation, excavation depth and slopes? A building bounding box cannot determine them reliably.
- Should preparation block the entire build or only affected parts/areas? Not decided.
- Contribution, withdrawal, reservation, access and cancellation rules for the common store. EarthWorks already distinguishes non-refundable setup and a later warehouse; do not silently equate this with building-part refunds.
- Appearance, wording, combined actions and safe board placement.

## Goal text addition — draft

> The unfinished BuildWorks building, approved v11 fog and project stake/board form the future boundary with EarthWorks. Terrain preparation can be planned from the start for the selected house/blueprint. The joint project uses one board and one material store. BuildWorks owns building parts and construction state; EarthWorks owns terrain preparation and modification. Account for each job separately within safe common storage, without double debit or repeated refund. Building cancellation never restores terrain or undoes performed earthworks. Preserve independent mods. Before implementation, agree identities, server authority, stages, storage access/persistence and board presentation/actions. Do not replace current BuildWorks continuation work with full integration.

The active goal mechanism's objective text has not changed. This proposal approves no open design choices and implements no API. EarthWorks code and roadmap have not been modified.
