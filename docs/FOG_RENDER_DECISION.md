# Fog rendering decision

Current follow-up: [288](CHECKPOINT_288.md) implements half-resolution S/T; scoped composite/four-side/native56/56 controls pass, but active half12.52FPS versus147.76 surface-only fails performance. Rain unpinned. Astra identifies a flaw in the prior proposal: work is roughly N/4+f*N, with unmeasured full-resolution fallback fraction f. Small image differences may themselves reflect repeated full marches. Next single A/B: mask of the same predicate without fallback integration, only after the owner releases the game. Diagnostic QA .265 retains the ordinary bundle; no artistic acceptance.

2026-10-21. Owner-requested independent read-only Astra review (`gpt-6-astra`, effort `ultra`). This selects the next experiment, **not a completed fix or accepted visual change**. Installed research QA .264 remains unchanged. [Русский](FOG_RENDER_DECISION_RU.md).

The approved v11 laboratory demonstrated one wall. The current whole-house renderer marches a combined bounds at fine steps, then performs nested light sampling. Canonical `BlueprintFogAtlas.cs:189–203,261` builds the aggregate bounds and full-size Blit; `FogVolumeAtlas.cginc:217–243,265–283` allows4096steps and24secondary light samples over sources. The laboratory used one source/512steps. Its three demonstration light beams also became player-relative game rendering (`FogGhostVolume.shader:72–86`, `BlueprintConstructionProjectFog.cs:209–211`). These are substantiated scaling problems, not proof of the sole cause of the owner's20second delay. Canonical local shaders differ from public Git.

`BlueprintFogVisual.cs:133–139` always uses the dense surface mesh, whereas the laboratory activated it near contact. That possible second cost remains unmeasured; do not change it in the first candidate. [284](CHECKPOINT_284.md) finds no separate accumulated effect-clock lag relative to game time in the observed window; do not blindly change clocks.

## Recommendation

Implement an isolated **wake-only volume renderer**, full resolution first. Preserve current v11 fields, transport, owner-specific return, publication guards and `TexturedGhost`. The surface already provides original textures/normal maps, transparency, drift/deformation and state-driven return. Static volume also contributes independent absorption, brownish scattering, beams and haze: removing it changes secondary lighting, so visual preservation is unproven.

- A separate `FogVolumeAtlas.cginc` branch reads only valid published nonempty smoke fields, reusing `FogPackedSample`, ownership/generation checks, pigment, capsule exclusion, edge fades and wake/extinction coefficients.
- Exclude static building density, nested `AtlasLight`, artificial beams and global `.002` haze. Bound marching by intersected active field bounds and native scene depth. No active contribution returns the original frame. Do not use a late CPU flag to suppress new GPU contact.
- Sum intersecting owners before one composition; per-wall sequential Blits change overlap semantics.
- Connect through the existing shader/atlas path. Do not change compute, queues, paging, surface, cost, project persistence or networking. Retain unused static allocation for this experiment rather than refactoring it.

Defer half-resolution rendering: wrapping the old nested renderer adds RT/composite/edge handling while retaining the structural cost. A new solver or particles would reopen shape/return/ownership problems. Preserve the original renderer as a frozen control; remove unused production branches only after candidate acceptance.

## Two decision gates

1. **Isolated visual gate:** four-section wall, rest/enter/hold inside wall material/exit/full return/recontact, comparable camera/light/speed. Show texture readability, gentle deformation, light varied swirls, stable opening and continuous return, including the changed haze/beams. Qualitative v11 comparison, not exact pixel equality. Reject artistic regression before native testing.
2. **Actual Valheim camera3440×1440:** one bounded .264/candidate/surface-only comparison with identical continuing simulation, rest and a path through wall material. Use `CadenceProbe.Start()` without extra Camera.Render; confirm production render invocation/size. Record visible latency separately from cadence measurement.

Proposed engineering readiness threshold, not an owner-approved new requirement: at least30fps when the surface-only ceiling permits, no queue growth, additional median frame time at most10%rest/25%contact versus surface-only. No seconds-long visible delay; continuous return. Owner acceptance remains separate.

If candidate approaches surface-only but the game remains slow, investigate surface/solver separately rather than reducing volume resolution again. If surface-only is fast but candidate is not, the experiment fails: retain evidence/control and reconsider the remaining cost. No open-ended shader micro-optimization chain.

At the time of the original review the candidate was not implemented; follow-up state is linked above. Large structures,32-part limit, full memory, physical board interaction, paid completion/cancellation and multiplayer remain open. This review does not close9.2–9.7.
