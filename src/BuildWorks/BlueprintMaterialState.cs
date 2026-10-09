using System;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Only native constructive appearance; never load a foreign ZDO or transfer identity.
    internal static class BlueprintMaterialState
    {
        private static readonly int Key = "RandMatSeed".GetStableHashCode();

        internal static bool CanRestore(GameObject source, int? seed) =>
            !seed.HasValue || seed.Value >= 0 && Supports(source);

        internal static bool Supports(GameObject root)
        {
            if (!BlueprintMaterialVariation.IsSupported(root)) return false;
            ZNetView view = root.GetComponent<ZNetView>();
            if (!view) return false;
            foreach (var component in root.GetComponentsInChildren<RandomMaterialValues>(true))
            {
                if (!component.enabled || component.GetComponentInParent<ZNetView>(true) != view) return false;
                for (Transform node = component.transform; node != root.transform; node = node.parent)
                    if (!node.gameObject.activeSelf) return false;
            }
            return true;
        }

        internal static void RequireFreshCreation()
        {
            if (ZNetView.m_useInitZDO || ZNetView.m_initZDO != null)
                throw Unsupported();
        }

        internal static void ApplyCreated(GameObject placed, GameObject source, GameObject ghost, int? seed)
        {
            if (!seed.HasValue) return;
            if (seed.Value < 0 || !placed || !source || placed == source || placed == ghost ||
                !Supports(source) || !Supports(placed)) throw Unsupported();
            ZNetView view = placed.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || !view.IsOwner()) throw Unsupported();
            ZDO data = view.GetZDO();
            if (data == null || data.m_uid.IsNone() || SharesData(data, source) || SharesData(data, ghost))
                throw Unsupported();
            data.Set(Key, seed.Value);
            if (!view || !view.IsValid() || !view.IsOwner() || !ReferenceEquals(view.GetZDO(), data) ||
                data.GetInt(Key, -1) != seed.Value) throw Unsupported();
        }

        internal static bool TryCapture(Piece piece, out int? seed)
        {
            seed = null;
            if (!piece) return false;
            if (piece.GetComponentsInChildren<RandomMaterialValues>(true).Length == 0) return true;
            if (!Supports(piece.gameObject)) return false;
            ZNetView view = piece.GetComponent<ZNetView>();
            if (!view || !view.IsValid()) return false;
            ZDO data = view.GetZDO();
            if (data == null) return false;
            uint revision = data.DataRevision;
            int value = data.GetInt(Key, -1);
            if (value < 0 || !piece || !view || !view.IsValid() ||
                !ReferenceEquals(view.GetZDO(), data) || data.DataRevision != revision) return false;
            seed = value;
            return true;
        }

        internal static bool SharesData(ZDO data, GameObject root)
        {
            ZNetView view = root ? root.GetComponent<ZNetView>() : null;
            ZDO other = view ? view.GetZDO() : null;
            return other != null && (ReferenceEquals(data, other) || data.m_uid == other.m_uid);
        }

        private static InvalidOperationException Unsupported() => new InvalidOperationException(
            BuildWorksLocalization.Text("placement.material_persistence_unsupported"));
    }
}
