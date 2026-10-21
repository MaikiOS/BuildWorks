using System;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Recreated from the world-bound file; visual clones have no native Piece, Sign, inventory or ZNetView.
    internal sealed class BlueprintConstructionProjectBoard : MonoBehaviour, Hoverable, Interactable
    {
        private BlueprintConstructionProjectRuntime owner;
        private BlueprintConstructionProjectStore.Record record;
        private GameObject visual;
        private BoxCollider hitBox;
        private Vector3 groundPoint;
        private float nextGroundCheck;
        private bool retired;
        internal Guid Id => record.Id;
        internal long Revision => record.Revision;
        internal string BlueprintName { get; private set; }
        internal int PartCount => record.Parts.Count;
        internal BlueprintConstructionProjectStore.Record Record => record;

        internal void Initialize(BlueprintConstructionProjectRuntime runtime, BlueprintConstructionProjectStore.Record project)
        {
            owner = runtime; record = project; BlueprintName = project.Blueprint.name;
            int layer = LayerMask.NameToLayer("piece_nonsolid");
            if (layer < 0) throw new InvalidOperationException("Native project interaction layer is missing.");
            gameObject.layer = layer;
            groundPoint = GroundPoint(project);
            // The board is in front of min-Z; turn the native sign's written side away from the building.
            transform.rotation = Quaternion.Euler(0, 180, 0);
            visual = new GameObject("ProjectVisual") { layer = layer };
            visual.transform.SetParent(transform, false); visual.SetActive(false);
            GameObject sign = ZNetScene.instance.GetPrefab("sign"), pole = ZNetScene.instance.GetPrefab("wood_pole");
            if (!sign || !pole) throw new InvalidOperationException("Native project sign/pole prefab is missing.");
            var stake = Copy(pole, "Stake", new Vector3(0, .62f, 0));
            if (!BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(stake, out Bounds stakeBounds, true) || stakeBounds.size.y <= 0)
                throw new InvalidOperationException("Native project stake bounds are missing.");
            // Bury the bottom slightly; normalize only the stake height, never the blueprint.
            stake.transform.localScale = new Vector3(pole.transform.localScale.x * .25f, 1.4f / stakeBounds.size.y, pole.transform.localScale.z * .25f);
            stake.transform.localPosition = new Vector3(-stakeBounds.center.x * stake.transform.localScale.x,
                -.08f - stakeBounds.min.y * stake.transform.localScale.y, -stakeBounds.center.z * stake.transform.localScale.z);
            var plaque = Copy(sign, "Board", new Vector3(0, 1.15f, 0));
            var label = BlueprintSignPreview.Add(sign, plaque, "BuildWorks");
            if (label) foreach (var filter in label.GetComponentsInChildren<MeshFilter>(true))
            {
                // These meshes/materials belong to the baked label, not to the native prefab.
                var colours = new Color32[filter.sharedMesh.vertexCount];
                for (int i = 0; i < colours.Length; ++i) colours[i] = new Color32(255, 255, 255, 255);
                filter.sharedMesh.colors32 = colours;
                var material = filter.GetComponent<Renderer>().sharedMaterial;
                if (material.HasProperty("_FaceColor")) material.SetColor("_FaceColor", new Color(.95f, .89f, .72f));
            }
            if (!BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(visual, out Bounds interaction, true))
                throw new InvalidOperationException("Project interaction bounds are missing.");
            hitBox = gameObject.AddComponent<BoxCollider>();
            hitBox.center = interaction.center; hitBox.size = interaction.size + Vector3.one * .05f;
            hitBox.enabled = false;
            UpdateGround();
        }

        private GameObject Copy(GameObject prefab, string name, Vector3 centre)
        {
            var clone = PlacementGhostPreviewView.CreateVisualClone(prefab, name, gameObject.layer);
            clone.transform.SetParent(visual.transform, false);
            clone.transform.localScale = prefab.transform.localScale;
            if (!BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(clone, out Bounds bounds, true))
                throw new InvalidOperationException("Native project visual bounds are missing.");
            clone.transform.localPosition = centre - Vector3.Scale(bounds.center, clone.transform.localScale);
            return clone;
        }

        internal static Vector3 GroundPoint(BlueprintConstructionProjectStore.Record project)
        {
            var blueprint = project.Blueprint;
            bool first = true; Bounds envelope = default;
            foreach (var part in project.Parts)
            {
                var source = blueprint.parts[part.SourceIndex];
                GameObject prefab = ZNetScene.instance.GetPrefab(source.prefabName);
                Bounds bounds; Vector3 prefabScale;
                if (prefab && BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(prefab, out bounds, true))
                    prefabScale = prefab.transform.localScale;
                else if (source.metadata?.minimum != null && source.metadata.maximum != null)
                {
                    var metadata = source.metadata;
                    bounds = new Bounds(); bounds.SetMinMax(metadata.minimum.Value.ToVector3(), metadata.maximum.Value.ToVector3());
                    prefabScale = metadata.prefabScale.ToVector3();
                }
                else { bounds = new Bounds(Vector3.zero, Vector3.zero); prefabScale = Vector3.one; }
                float[] p = part.WorldPose.Values;
                Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(p[0], p[1], p[2]),
                    new Quaternion(p[3], p[4], p[5], p[6]), Vector3.Scale(prefabScale, new Vector3(p[7], p[8], p[9])));
                for (int corner = 0; corner < 8; ++corner)
                {
                    var point = matrix.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z));
                    if (first) { envelope = new Bounds(point, Vector3.zero); first = false; } else envelope.Encapsulate(point);
                }
            }
            if (first) throw new InvalidOperationException("Project board requires a construction footprint.");
            // shortcut: use the world footprint's front edge; owner placement of the board is a later UI decision.
            return new Vector3(envelope.center.x, envelope.min.y, envelope.min.z - 1);
        }

        private void Update()
        {
            if (retired || Time.unscaledTime < nextGroundCheck) return;
            nextGroundCheck = Time.unscaledTime + .5f; UpdateGround();
        }
        private void UpdateGround()
        {
            bool loaded = false;
            if (ZoneSystem.instance && ZoneSystem.instance.IsZoneLoaded(groundPoint) &&
                ZoneSystem.instance.GetGroundHeight(groundPoint, out float height))
            {
                transform.position = new Vector3(groundPoint.x, height, groundPoint.z);
                loaded = true;
            }
            visual.SetActive(loaded); hitBox.enabled = loaded;
        }
        public string GetHoverName() => BuildWorksLocalization.Text("project.name");
        public float GetHoverOffset() => 1.1f;
        public string GetHoverText() => retired ? string.Empty : GetHoverName() + ": " + BlueprintName + "\n" +
            BuildWorksLocalization.Text(BlueprintConstructionProjectRuntime.HasHammer(Player.m_localPlayer) ? "project.interact" : "project.hammer");
        public bool Interact(Humanoid user, bool hold, bool alt)
        { if (retired || hold || alt) return false; return owner.Open(this, user); }
        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
        internal void Retire()
        { if (retired) return; retired = true; gameObject.SetActive(false); Destroy(gameObject); }
    }
}
