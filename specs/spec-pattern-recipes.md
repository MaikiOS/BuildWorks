# BuildWorks persistent operation contract

**English** | [Русский](spec-pattern-recipes_RU.md)

This is the English summary of the accepted safety contract. It is future scope,
not implemented Store v10. The current [roadmap](../docs/ROADMAP.md) determines
the delivery order. Begin with one persistent Array, not all future operations.

## Data and evaluation

- Groups remain structural hierarchy, not transform nodes.
- An optional recipe owns a canonical source subtree plus ordered operations.
- Evaluate from that source each time; do not repeatedly transform the previous
  result and accumulate drift. Saved parts/groups are the checked materialized result.
- Each operation has a stable ID, type, enabled state, target granularity, axes,
  pivot and typed parameters. Targets are individual parts or immediate child
  assemblies as a whole.
- Direct transforms retain identity. Generated copies use deterministic logical
  IDs derived from recipe/operation/source/cell identity.
- A world placement anchor follows its identified result; it does not silently
  move to an arbitrary generated copy or compete with local group pivots.

The pipeline is source → ordered operations → validation → preview → atomic
commit → normal native pieces. Preview and commit use the same evaluated result;
reject stale preview revisions. Cancel leaves the document unchanged.

## Editing and safety

- Bake explicitly converts the result to independently editable regular pieces
  in one Undo. Do not silently sever a generator link when editing a copy.
- Store migration, clone, duplication, Undo and save/reopen preserve the recipe,
  source and anchor identity. The planned version is v10; current version is v9.
- Preserve or block unsupported versions/types without overwriting valid data.
- Initially reject nested/intersecting recipes explicitly.
- Keep existing 128-part/64-group editor and 512-part world limits until measured.
- Reuse existing Array math. Direct group layout operations do not require v10.

## Acceptance

Repeated evaluation is deterministic; disabled operations recover the source;
preview=commit=Bake; cancel changes nothing; save/reopen retains editability;
history restores parameters/order; copied hierarchies and pivots stay independent.
Position-only layout changes do not resize pieces; child assemblies retain internal
distances. Existing v9 documents load without loss.

## Guides require a separate owner discussion

Before Line A/B, polyline, arc or Bezier implementation, agree on manual magnetic
guides versus generated repetition, point editing, spacing/count, source frame,
orientation/roll, open/closed paths and persistence. Existing curve mathematics
does not approve that tool. Ordinary Valheim pieces stay rigid.

Excluded near-term: vanilla mesh deformation, non-uniform shear, node graphs,
expressions, full Fields/dynamics and speculative influence/random systems.
