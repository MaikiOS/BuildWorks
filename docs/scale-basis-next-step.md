<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Persisted scale basis — implementation contract

Implemented in canonical .110; automated checks and independent review passed.
QA .110 installed, hashes match,48 QA files/library unchanged. No native game proof.
.109 BakeMesh(baked,true) regression is retained; this step addresses the distinct
canonical ZNetScene versus Hammer PieceTable prefab root-scale mismatch.

Optional Part.baseScale in native17/portable7:
effectiveAbsolute=(baseScale??resolvedPrefab.localScale)×scale component-wise.
scale remains the editor multiplier0.01..4. baseScale is finite/positive, as is
the resulting float-scale product. baseScale plus scaleIsAbsolute is invalid.
Pending external absolute rows keep their raw scale and flag until prefab resolution;
resolution pins baseScale=rawAbsolute, scale=one, flag=false, once. Source bytes remain.
Do not divide imported size by canonical prefab scale or clamp the model to a range.

Legacy16/6 reads preserve null base without guessing. An explicit editor save may pin
the successfully resolved visual's actual prefab base while retaining its multiplier;
missing records remain null. Pin from the existing scene visual, not a newly resolved
potentially different prefab. Partial TryUpdateParts retains an old explicit base
when incoming base is null; incoming non-null base wins and cannot regain a stale
absolute flag. Full document update remains authoritative.

Every world path must calculate relative=effectiveAbsolute/HammerPrefab.localScale,
validate finite/positive0.01..4 and native sync-scale support before plan mutation.
For group uniform scale preflight all parts before adding any. Passive/batched preview
poses remain Hammer-relative because templates already contain prefab scale. Editor,
thumbnail and direct Hammer visual roots use effectiveAbsolute. Custom snap inverse
uses effectiveAbsolute; native snap and bounds generation use the same relative.
Registry signature must include baseScale presence and components.

Capture uses placed root localScale, validates its ratio to the Hammer prefab and
native persistence, then saves baseScale=placedLocalScale, scale=one. Capture rejects
a parent matrix whose basis differs from its Unity rotation basis: scale/shear or
reflections not represented by that rotation would otherwise change restoration.
Real Unity tests cover rigid rotation, scale, odd/double mirrors and shear with unit
length basis columns. This is stronger than checking lossyScale or column lengths.
Foreign row scale and ZDO scale must not apply twice.

Affected manual maps: CompositeBlueprintStore Part/clone/partial update/validation,
ExternalBlueprintLibrary preparation/detached copy, strict BlueprintExchangeFormat
version gates and nullable-vector validation, Geometry BlueprintEditorPart all clones,
arrays/contours/equality/atomic validation, controller DTO/new part/save pin,
BlueprintEditorScene temporary/main transforms, thumbnail roots, Hammer registry
visuals/snaps/bounds/signature, BlueprintPlacementFrame snap inverse, Session passive
preview/plans/world admission/capture/legacy workspace save/bounds/snaps. RuntimeSources
are generated only after production edits, never edited as the source of truth.

Minimum regression: sourceAbsolute1, canonical(2,.5,3), Hammer(.5,2,.25) must show root1
everywhere and plan relative(2,.5,4). Absolute.001/Hammer.001 permits relative1;
absolute.001/Hammer1 remains exact in editor but world rejects without a partial plan.
Also verify old16/6 null roundtrip, explicit save pin, partial updates, unknown→late
resolution, arrays/Undo, strict rejection of new field in old versions, overflowing
product, rotated nonuniform custom snaps and native capture parent rejection.
Keep the actual Lava Lantern bounds regression. Native place/reload/network still
require game acceptance; an inert reconstruction is not proof of a world reload.
