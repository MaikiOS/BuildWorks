using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Read-only native attachment rules on renderer-only copies. This is not
    // inventory reconstruction, foreign ZDO execution, or permission to place.
    internal static class BlueprintItemStandPreview
    {
        private static readonly Type EquipmentVisualType = typeof(ItemStand).Assembly.GetType("IEquipmentVisual", false);
        internal static bool Supports(ItemStand stand, ItemDrop.ItemData item) =>
            stand && item?.m_shared != null && stand.m_supportedTypes != null &&
            stand.m_supportedItems != null && stand.m_unsupportedItems != null && !stand.IsUnsupported(item) &&
            (stand.IsSupported(item) || stand.m_supportedTypes.Contains(item.m_shared.m_itemType));
        internal static bool TryAdd(GameObject source, GameObject visual,
            BlueprintForeignPartData data, out string warning)
        {
            warning = null;
            if (data == null || data.ContentsCleared || !source || !visual) return false;
            ItemStand stand = source.GetComponent<ItemStand>();
            if (!stand) return false;
            string reason = "editor.item_preview_invalid";
            GameObject content = null;
            try
            {
                // Only known PlanBuild/native visual metadata is decoded. Raw
                // item packages, foreign ZDO and chance remain inert.
                if (data.Format != "blueprint" && data.Format != BlueprintForeignPartData.NativeItemStandFormat ||
                    data.Info == null) throw new InvalidDataException();
                string info = data.Format == "blueprint" ? BlueprintExchangeFormat.DecodeSignText(data.Info) : data.Info;
                string[] fields = info?.Split(':');
                if (fields == null || fields.Length != 4 || string.IsNullOrWhiteSpace(fields[0]) ||
                    fields[0].Length > 128 || Path.GetFileName(fields[0]) != fields[0] ||
                    !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int variant) ||
                    !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int quality) ||
                    !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out int orientation))
                    throw new InvalidDataException();
                GameObject item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(fields[0]) : null;
                reason = "editor.item_preview_missing";
                if (!item) throw new InvalidDataException();
                reason = "editor.item_preview_unsupported";
                ItemDrop drop = item.GetComponent<ItemDrop>();
                var itemData = drop ? drop.m_itemData : null;
                GameObject attach = ItemStand.GetAttachPrefab(item);
                if (itemData?.m_shared == null || !attach || quality < 1 || quality > itemData.m_shared.m_maxQuality ||
                    variant >= Math.Max(1, itemData.m_shared.m_variants) || itemData.m_shared.m_itemStandOffsets == null ||
                    !Supports(stand, itemData))
                    throw new InvalidDataException();
                Transform target = MapTransform(source.transform, visual.transform, stand.GetAttach(itemData));
                GameObject attachObject = ItemStand.GetAttachGameObject(attach);
                if (!target || !attachObject) throw new InvalidDataException();
                var settings = new List<ItemStand.OrientationSettings>();
                stand.GetOrientationSettings(itemData, ref settings);
                if (orientation >= settings.Count || settings[orientation] == null) throw new InvalidDataException();
                ItemStand.OrientationSettings offset = settings[orientation];
                Vector3 position = attach.transform.localPosition + offset.m_position;
                Quaternion rotation = attach.transform.localRotation * Quaternion.Euler(offset.m_rotation);
                Vector3 scale = Vector3.Scale(Vector3.Scale(attach.transform.localScale,
                    itemData.GetScale(quality)), offset.m_scale);
                if (!Finite(position) || !Finite(offset.m_rotation) || !Finite(scale) ||
                    scale.x == 0f || scale.y == 0f || scale.z == 0f) throw new InvalidDataException();
                // Native ItemStand executes the first IEquipmentVisual. Only the
                // native ItemStyle material operation is understood here.
                if (attachObject.GetComponentInChildren<ParticleIntensityScaler>(true) ||
                    attachObject.GetComponentInChildren<ParticleSystem>(true)) throw new InvalidDataException();
                Renderer[] renderers = attachObject.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidDataException();
                foreach (Renderer renderer in renderers)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (!(renderer is MeshRenderer && filter && filter.sharedMesh) &&
                        !(renderer is SkinnedMeshRenderer skin && skin.sharedMesh)) throw new InvalidDataException();
                }
                if (EquipmentVisualType == null) throw new InvalidDataException();
                Component equipment = attachObject.GetComponentInChildren(EquipmentVisualType);
                if (equipment != null && !(equipment is ItemStyle)) throw new InvalidDataException();
                content = PlacementGhostPreviewView.CreateVisualClone(attachObject,
                    "BuildWorks_ItemStandContent", visual.layer);
                content.transform.SetParent(target, false);
                content.transform.SetLocalPositionAndRotation(position, rotation);
                content.transform.localScale = scale;
                if (equipment is ItemStyle style)
                {
                    Transform styled = MapTransform(attachObject.transform, content.transform, style.transform);
                    if (!styled) throw new InvalidDataException();
                    VisualCloneOwnedAssets owned = content.GetComponent<VisualCloneOwnedAssets>();
                    foreach (Renderer renderer in styled.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] materials = renderer.sharedMaterials;
                        for (int index = 0; index < materials.Length; ++index)
                        {
                            if (!materials[index]) continue;
                            Material material = new Material(materials[index]) { hideFlags = HideFlags.HideAndDontSave };
                            if (material.HasProperty("_Style")) material.SetInt("_Style", variant);
                            owned.Track(material);
                            materials[index] = material;
                        }
                        renderer.sharedMaterials = materials;
                    }
                }
                content.SetActive(true);
                return true;
            }
            catch (Exception error) when (error is InvalidDataException || error is ArgumentException || error is UnityException)
            {
                if (content) { content.SetActive(false); UnityEngine.Object.Destroy(content); }
                warning = BuildWorksLocalization.Text("editor.item_preview_warning", source.name,
                    BuildWorksLocalization.Text(reason));
                return false;
            }
        }

        private static Transform MapTransform(Transform sourceRoot, Transform targetRoot, Transform source)
        {
            if (!source) return null;
            if (source == sourceRoot) return targetRoot;
            Transform parent = MapTransform(sourceRoot, targetRoot, source.parent);
            int index = source.GetSiblingIndex();
            return parent && index < parent.childCount ? parent.GetChild(index) : null;
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
