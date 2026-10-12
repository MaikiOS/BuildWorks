# .161 — collider contact source candidate; runtime pending

**English** | [Русский](COLLIDER_CONTACT_161_2026-10-12_RU.md)

The .160 native attempt found a real floor under the chest, rejected by our
renderer-based tolerance. .161 prepares the shared contact correction. It is
**not installed**; QA remains .160. Ostrix is working in Nuke, so neither Unity
nor Valheim was launched for this step. The full blueprint goal remains active.

## Change and limits

- Box contact uses collider centre, size, current rotation and absolute lossy
  scale. Mesh contact recomputes world bounds from its local mesh bounds.
  Inactive retry ghosts are supported without activating or moving them.
- Eligible shapes are tested separately; their union must not invent a support
  surface between distant colliders. Ray/shape intersection excludes empty
  corners of a rotated box. Distance is measured to the boundary along that ray,
  so a thin side cannot hide deep penetration through another face.
- WearNTear shapes use its confirmed .15m-per-side support envelope to find
  contact context. This is a deliberate BuildWorks policy, not a claim that
  PieceRayTest itself uses .15m or that support/stability has been reproduced.
  Subsequent native flags, range, rigidbody, clipping, rights and cost checks remain.
- Any eligible unhandled collider (including mixed prefabs) keeps the entire
  prefab on the previous strict visual path. A malformed known shape cannot be
  hidden by that fallback. Sphere/Capsule adapters are not claimed.
- Water still uses the existing visible bounds. No fixture, item transfer,
  resource ledger, permission, network identity or world save was changed.

## Evidence

Artifacts: `artifacts/checkpoint-0.19.161`.

| Check | Result |
| --- | --- |
| Old production algorithm, 7cm support regression | Failed as expected: `cpu-red.log` |
| First candidate, thin penetration regression | Failed as expected: `cpu-thin-red.log` |
| Second candidate, mixed collider regression | Failed as expected: `cpu-mixed-red.log` |
| Final production method with substituted host/physics | 62 assertions passed: `cpu-green.log` |
| Normal / QA builds | Both 0 errors / 0 warnings |
| Current host contract / translations / compiled QA isolation | Passed; 989 translation keys |
| Authored real Physics fixture, offline compilation | 0 errors / 0 warnings; **not executed** |
| Independent correctness / Ponytail source review | PASSED / source checkpoint accepted |
| Unity Physics / Valheim / reload / network | **Not run** |

The CPU harness compiles the complete production method with type aliases.
Unity Vector3/Bounds/Ray math is real; Transform, Quaternion and Physics are
substitutes. These assertions do not prove actual collider behaviour or native
placement. `artifacts/placement-contact-cpu/verification-status.json` records
the source hash and explicitly sets both runtime evidence flags to false.
The Workbench WearNTear type is an inert marker, not a native support simulation.
No full UI regression, heavy performance test or unrelated geometry/store suite
was repeated for this host-contact-only change. `verify` is unavailable; direct
diff review, focused checks and independent review were used instead.

## Next gate, after Nuke work

1. Run `scripts/Capture-UiWorkbench.ps1 -ContactOnly` against current source.
   Keep its receipt/log separate from the historical full UI run. Resolve any
   failure before guarded QA deployment. A compile result is not this pass.
2. Only then use the guarded deployment script and verify installed hashes.
   Launch through Thunderstore → TerrainRamp-1.0-Test → Modded when authorized.
3. In the existing isolated QA world, obtain fresh channel state/capture and
   verify version, nonce and inventory before spending anything. Last visible
   .160 balance was Wood42+50 and two swords. Do not reprovision or replay commands.
4. Use the already imported **TEST 24 Empty Storage Mount**. Source for finding
   it: `BepInEx/config/BuildWorks/test-fixtures/24-contents-empty.buildworks.json`.
   Leave storage contents and mounted items off. Confirm the world build once;
   expect all four parts, matching ghost pose and native support. A pause at the
   chest, collapse, wrong pose or unexpected debit is a failure to investigate.
5. Record exact total costs, inspect empty chest/mount, save/exit/reload and
   compare all parts. Test cancellation separately with a recorded balance;
   return only actual inputs. Filled contents, supplied-item attributes,
   administrator restore and host+remote-client remain separate open gates.

No repeat of Ostrix's already accepted editor/table cases is requested.
