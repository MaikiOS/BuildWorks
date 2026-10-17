<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .114 — recorded prefab metadata and missing-part frames

Native Store19 / portable JSON9 add optional Part.metadata v1: prefab ID, observed
name/type, host assembly identity, intrinsic root scale and optional paired local
minimum/maximum. Source ID/version may be retained as declared provenance; current
automatic capture cannot verify prefab-to-mod ownership and leaves it unknown.
Host identity is not the source mod. No model assets or executable state are stored.

Save/update, reviewed import and native export refresh available prefab snapshots
once per distinct prefab per operation on the Unity thread. Unavailable prefabs
retain their recorded data. Inputs and source evidence stay unchanged. Metadata
survives document copies, Undo/Redo, native/portable/ZIP and editor disk reopen.
Legacy18/8 without metadata still load; older schemas cannot contain the new field.
Single-part admission and empty-document refusal are unchanged.

Static MeshFilter bounds include active visible child geometry and nested transforms
in prefab-root coordinates, excluding root position/rotation/scale. Inactive prefab
roots are allowed only for metadata capture; the existing Hammer helper keeps its
active-root policy. Skinned geometry has unknown bounds until a validated adapter.
This is a recorded visual envelope, not exact shape, collider or support geometry.

An absent prefab with recorded bounds appears as a wire frame at the recorded local
offset, rotation and resolved scale. With unknown bounds the existing small marker
remains. Frames are selectable through a separate pick proxy, but cannot supply
model bounds, packing, collision/contact or snap geometry. Real models continue to
use their live prefab scale; metadata never overrides them or grants build rights.

Checks passed: normal/QA builds0/0; GeometryTests; StoreTests with353 source files
and ready metadata fixture; localization869/LocalizationTests; QA isolation;
EditorBridge; HostContract; actual Session17; full format/whitespace;
DeploymentChecks; Workbench300 plus metadata frame image and capture/save/reopen.
The metadata scene uses an inactive translated/rotated/scaled prefab with a rotated
nonuniform child and independently checks its bounds, scale, fresh save and missing
frame. Correctness and Ponytail complexity reviews passed, including null-name
refusal. The verify helper is absent; direct checks were used.

See QA_0.19.114_RU.md. Mod ownership discovery, skinned envelopes, 1-to-N replacement,
native game build/reload and multiplayer remain open. Fog v11 is approved and now
in the active goal, but is not connected to gameplay. No Valheim launch occurred.

QA .114 installed only in TerrainRamp-1.0-Test through the guarded script;
SHA `CE951EA3E848592BF779CD78A87B2BB88F9FAC7CBAEBFB962B94B94100CD62A7`. Both DLL hashes match; all48 QA files
and library unchanged. No Valheim launch/native game/reload/network acceptance.
