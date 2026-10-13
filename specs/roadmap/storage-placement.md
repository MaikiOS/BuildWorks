# Survival storage placement — implementation contract

Status: shared reservation .171, local native transfer .172, write-ahead evidence .173 same-session inverse return .174 and marker-bound explicit recovery .175 and canonical Local inventory readback .176 implemented in sources, 2026-10-13. Installed QA .176 still
blocks populated storage unless its contents are explicitly omitted from the
temporary placement recipe. This document does not enable restoration or replace
the full BuildWorks goal. Keep archived source state unchanged.

.145 implements the mounted-item subset: aggregate exact bindings, ordered
next-piece native payment preflight, and postpayment availability checks.
`docs/MOUNT_RESERVATION_CHECKPOINT_2026-10-10_RU.md` records its tests and limits.
In .171, container demands use decoded prefab/quantity, exact own-item bindings,
immutable quantity/property snapshots, recorded-cell/max-stack limits and shared
next-piece payment checks. Container supplies carry no mounted appearance.
See `docs/CONTENT_RESERVATION_171_RU.md`. This source checkpoint is not deployed.
In .172 the local primitive checks actual prefab grid/quest/equip/ownership and observes both inventories after a single native move, including false/exception. Both rollback paths retain filled/unverified storage. See docs/CONTENT_TRANSFER_172.md. The primitive is not connected to world placement; durable custody/return/compensation, player/target persistence, server ack and integration remain open.

.173 adds a versioned/checksummed/ordered write-ahead journal, complete preflight byte budget, actual before/after state evidence and unresolved-target cancellation hold. It never replays/refunds/rebinds: all files require reconciliation, even local terminal records. Durable recovery/compensation, native saves/server ack and world placement integration remain open. See docs/CONTENT_JOURNAL_173.md.

.175 uses WAL3 plus a persistent target operation marker, not saved native UID/revision: ZDO.Load remaps/resets those fields. Marker Set follows flushed preparation and waits boundedly for native cache/capture before any item attempt. Explicit recovery binds exact current manifests/raw/cells/fresh ItemData; old/uncertain/unmarked evidence stays held. No game reload, disk/server receipt, construction refund or populated placement is claimed. See docs/CONTENT_RECOVERY_175.md.

.176 binds completed journal custody to canonical Local profile46/Player33/Inventory109 readback with independent SHA-512/framing/identity/exact byte checks. No save is requested, no world/server/cloud/fresh-save/full-player proof is inferred, and no holds/markers/journals are retired. See docs/CONTENT_PROFILE_176.md. Separate Container pending-state must begin only after recorded native construction debit; ItemStand state must not be reused. CompleteNativePlacement/supply release/refund still require durable two-sided settlement.

## Required behavior

- A survival recipe requests prefab IDs and quantities from readable archived
  contents. The player provides those items; archived quality, durability,
  crafter, custom data and administrative state are not requirements or rewards.
- Transfer the provided items with their own attributes. Different provided
  stacks must not be merged if that changes attributes. If preserving those
  properties needs more cells than the original layout, preflight actual native
  capacity and show the difference before starting; never silently discard items.
- Storage and mounted-item options remain independent. A disabled category
  consumes nothing from that category and leaves the source archive intact.
- Construction costs are native and separate from contents. A stack of Wood
  cannot simultaneously pay a construction requirement and satisfy a reservation
  for three Wood inside a chest. Cancellation/refund cannot count transferred
  contents as construction payment.
- Networked authority and administrator restoration need their own validated
  server protocol. A local owner flag, queued ZDO send or client report alone is
  not a server acknowledgement, disk save or authorization.

## Existing integration points

`PrecisionPlacementSession` builds a part-aligned plan and calls
`BlueprintItemStandPlacement.SelectSupplies` before preview. A part-aligned
`Supply` binds either a mounted item or immutable N-item container reservations. `PlacementGhostPreviewView` uses its `Contents`
to display that provided item's native appearance. Retain this behavior while
using N-item container reservations; do not turn a container into a fake stand.

`BuildWorksPlugin` wraps native `Player.UpdatePlacement` with construction-payment
capture/completion. `OnNativePlacementFinished` defers mounted-item application
because `TryPlacePiece` finishes before native resource consumption. Transfer
container contents only after this payment has completed. Otherwise
`BlueprintConstructionRefund` measures a contents debit as construction cost.

The adapter must bind each reservation to the exact source item, quantity and
provided properties, while allowing separately recorded native material debits.
Before each native placement, require enough inventory for that placement's
cost plus all still-held contents reservations. Preserve the current ability to
pause for later construction materials; do not require the whole building's cost
up front. After payment completion, revalidate the bound contents against actual
debits before moving them. Rebinding to another player's item or changed properties
requires a new explicit preparation; it must not silently restore archived state.

## Verified native API and limits

Current inspected Steam manifest25730771; native transfer fingerprints unchanged. The older printed25185596 was a fixed label, not manifest evidence (see docs/CONTENT_TRANSFER_172.md).

`bool Inventory.MoveItemToThis(Inventory fromInventory, ItemDrop.ItemData item, int amount, int x, int y)`

The positional private `AddItem` clones into an empty cell, sets quantity/position,
adds to the destination, reduces the source stack, then notifies destination
changes. The wrapper subsequently removes an exhausted source or notifies its
changes. `ItemData.Clone` preserves fields and separately copies `m_customData`.

This is not a transaction. The amount is clamped to the source stack. A compatible
occupied cell may receive a partial quantity before the method returns false.
Callbacks may throw after mutation. An exact-transfer adapter therefore requires
positive bounded quantity, stable source binding, a valid empty target cell and
verification of actual before/after quantities and properties. A bool alone is
insufficient evidence of success or no change.

Destination notification is gated by `Player.m_localPlayerExists`. Container
`Awake` subscribes `OnContainerChanged`; that callback saves only when not loading
and owner. Private `Container.Save` itself has no owner gate. It serializes the
inventory into `ZDOVars.s_items` and updates the local revision. These calls are
not a ready dedicated-server transfer path or crash-safe two-inventory commit.

Use the Container's cached override-aware `m_nview`, exact fresh ZDOID and
valid/owner checks. Require no open container interaction, native creator/access
and ward permission, supported prefab shape and no generated default loot.
Native inventory UI also checks quest items and unequips/removes queued equip
actions. The adapter must preserve these restrictions and handling; raw inventory
transfer does not perform them. Equipment must not be duplicated or left bound
to the player after moving.

.174 adds a same-session inverse return of exact delivered references into pre-reserved empty player cells, using current provided attributes and a v2 parent/direction journal. A deterministic CreateNew child claim freezes forward continuation; both operations remain held even after local success. V1 remains readable/held and cannot authorize return. No restart rebind, player/world persistence or server acknowledgement is claimed. See docs/CONTENT_RETURN_174.md.

## Implementation dependencies and rollback

1. One plan-wide reservation covers N container items and mounted items, plus
   native material debits for the next placement. Bind IDs/revisions/properties,
   bound quantities, actual cell capacity and operation order. Limits from the
   file decoder remain; all totals use checked arithmetic.
2. One pending payload stage runs after construction payment completion. It
   journals actual custody changes, applies exact empty-cell transfers, verifies
   source/destination/ZDO state and handles callback/ownership failures.
3. Cancellation uses the observed custody records, never an assumed bool or the
   archived item properties. Returning an item and destroying a filled chest
   must not both return that item. Direct fallback destruction is forbidden
   while content custody or native drops are unresolved. An uncertain transfer
   must stay visible/recoverable and cannot be replayed automatically.
4. Persisted operation IDs/custody and the server acknowledgement protocol must
   settle interruption/reload/reconnect before claiming crash/network safety.
   `ForceSendZDO` queues an ID; it does not satisfy this requirement.

Required tests: shared Wood cost/contents; two containers plus a mount competing
for one stack; provided items with differing attributes; split quantities and
cell exhaustion; changed bindings; equipped/quest items; occupied cells; native
false after partial change; exceptions during notification/save; owner/context
loss; cancel before/after each debit; save/reload/reconnect and duplicate replay.
Use authored files24/25 for host QA after the input-channel smoke and geometry
preflight; do not mark their current populated-storage refusal as successful
restoration. Tests must distinguish source archival bytes, converter understanding
and actual restored native behavior.

`scripts/Test-HostContract.ps1` pins six inspected transfer/callback/clone method
bodies, checks signatures/order/owner-save wiring and rejects two deliberately
broken copies in memory. These are read-only static checks; no inventory transfer,
game launch, native save or server acknowledgement is executed by them.
