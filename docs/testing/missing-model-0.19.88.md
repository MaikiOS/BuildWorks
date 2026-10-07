# Incomplete blueprints: checkpoint 0.19.88

**English** | [Русский](missing-model-0.19.88_RU.md)

Source .88; installed test build remains .82. This checkpoint does not establish native construction
or world reload. Evidence: `artifacts/checkpoint-0.19.88/verification.json`.

## Change

Foreign `.blueprint`/`.vbuild` records contain absolute localScale. Previously a missing prefab made
PrepareForHost reject the entire blueprint. Editor scene placeholders already existed; the earlier
audit claim that every missing-prefab editor entry was blocked was too broad.

Store15/portable JSON6 add optional `Part.scaleIsAbsolute`. Missing records retain exact XYZ/rotation/scale,
source row and opaque data. Known records divide by actual intrinsic prefab scale once; unknown bases
are never invented as unit scale. An absent flag keeps historical relative semantics. Versions14/5 reject
the new field. Editor normalization uses a working copy and keeps the original native library identity
for writes. Installing a prefab does not silently render pending records: reopen normalizes, explicit save
persists the resolved basis. Registered editor visuals no longer require unlocked survival recipes.
World construction availability remains strict; pending basis rejects before plan mutation.

The constant .3 selection/camera marker is not a model bounding box. It supplies no surfaces, geometry
corners, native sockets or automatic packing dimensions. Authored positions/user points remain meaningful.
Array packing refuses unknown dimensions; explicit steps/distances do not infer marker geometry. Insertion
surface contact uses known geometry. Move/rotate/scale previews preserve constant marker size.

## Evidence

Actual Controller/Scene acceptance uses a known intrinsic `(2,.5,3)` model and missing absolute
`(.001,.002,.003)` record. It checks zero-delta numeric/mouse scaling, commit/Undo, inspector position,
Infinity rejection, move/rotate preview, ordinary/experimental anchors, refused packing bounds and a low
marker that must not move known geometry's ground contact. Edit/copy/Undo/native save/disk reopen/JSON
export/reimport preserve source and basis. A late prefab `(.0005,.001,.0015)` resolves to `(2,2,2)` exactly
once; a fresh library verifies explicit persistence. Actual WorldLayout refuses pending scale1 atomically.
Store covers version gates, provenance and the real external corpus. Workbench is inert-host proof only.

Independent reviews identified and closed relative-only gesture/inspector limits, changing marker preview
size, fabricated corners/contact, and an obsolete missing-exception assertion.

## Remaining scope

Missing-mod names on cards, source/type/local-bounds metadata, one-to-many replacement and all incomplete
import entry points still need separate acceptance. A prefab name cannot reliably identify its mod or size.
Archived unknown fields do not establish restored behavior. Heavy editor, admin restore, containers/armour/
free items and independent construction contents choices remain open.

No prepared safe survival stand exists. Read-only host analysis found SaveSystem caches `s_saveCollections`/
`s_cachedWorlds` and an independent cloud provider; late SetSaveDataPath does not prove isolation. No cache
clearing, root/cloud redirection or bootstrap was implemented. Valheim/worlds/characters were untouched.
Full import/build/reload remains the priority; older accepted tools do not require wholesale retesting.
