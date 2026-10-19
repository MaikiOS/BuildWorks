using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Recreated from the world-bound server file; contains no native Piece, Sign, inventory or resource grants.
    internal sealed class BlueprintConstructionProjectBoard : MonoBehaviour, Hoverable, Interactable
    {
        private BlueprintConstructionProjectRuntime owner;
        private BlueprintConstructionProjectStore.Record record;
        private Material material;
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
            var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab("woodwall") : null;
            var hostMaterial = prefab ? prefab.GetComponentsInChildren<MeshRenderer>(true)
                .SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m && m.shader) : null;
            if (!hostMaterial) throw new InvalidOperationException("Native project board material is missing.");
            material = new Material(hostMaterial) { color = new Color(.65f, .5f, .3f) };
            Box("Stake", new Vector3(0, .6f, 0), new Vector3(.08f, 1.2f, .08f));
            Box("Board", new Vector3(0, 1.13f, 0), new Vector3(.72f, .38f, .06f));
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .7f, 0); collider.size = new Vector3(.76f, 1.4f, .12f);
            TMP_Text template = Hud.instance ? Hud.instance.m_pieceDescription : null;
            if (template && template.font)
            {
                var labelObject = new GameObject("ProjectLabel"); labelObject.SetActive(false);
                var label = labelObject.AddComponent<TextMeshPro>();
                label.transform.SetParent(transform, false); label.transform.localPosition = new Vector3(0, 1.13f, -.038f);
                label.transform.localRotation = Quaternion.Euler(0, 180, 0);
                label.font = template.font; label.fontSize = 1.1f; label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(.66f, .32f); label.text = "BuildWorks";
                label.color = new Color(.95f, .89f, .72f); label.raycastTarget = false;
                labelObject.SetActive(true);
            }
        }

        private void Box(string name, Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(transform, false); box.transform.localPosition = position; box.transform.localScale = scale;
            box.layer = gameObject.layer;
            var collider = box.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            box.GetComponent<Renderer>().sharedMaterial = material;
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
        private void OnDestroy() { if (material) Destroy(material); }
    }
}
