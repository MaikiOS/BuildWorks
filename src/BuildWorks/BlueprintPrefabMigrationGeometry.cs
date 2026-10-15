using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Shared with the read-only native-asset acceptance; never creates host objects.
    internal static class BlueprintPrefabMigrationGeometry
    {
        private const string OldNote = "REPLACED BY wood_wall_roof_a";
        private const string NewNote = "Replaces wood_wall_roof because of \"same name and position\" snapping issue";
        internal static bool Matches(GameObject old, GameObject current)
        {
            if (!old || !current || !old.transform.Find(OldNote) || !current.transform.Find(NewNote)) return false;
            var left = Nodes(old.transform, OldNote);
            var right = Nodes(current.transform, NewNote);
            if (left == null || right == null || left.Count != right.Count) return false;
            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out Transform b)) return false;
                Transform a = pair.Value;
                if (!a.localScale.Equals(b.localScale) || pair.Key != "" && (!a.localPosition.Equals(b.localPosition) || !a.localRotation.Equals(b.localRotation)) ||
                    a.gameObject.activeSelf != b.gameObject.activeSelf || a.gameObject.layer != b.gameObject.layer || a.tag != b.tag) return false;
                MeshFilter af = a.GetComponent<MeshFilter>(), bf = b.GetComponent<MeshFilter>();
                if ((bool)af != (bool)bf || af && af.sharedMesh != bf.sharedMesh) return false;
                Renderer ar = a.GetComponent<Renderer>(), br = b.GetComponent<Renderer>();
                if ((bool)ar != (bool)br || ar && (ar.GetType() != br.GetType() || ar is SkinnedMeshRenderer ||
                    ar.enabled != br.enabled || ar.shadowCastingMode != br.shadowCastingMode || ar.receiveShadows != br.receiveShadows ||
                    !ar.sharedMaterials.SequenceEqual(br.sharedMaterials))) return false;
                Collider[] ac = a.GetComponents<Collider>(), bc = b.GetComponents<Collider>();
                if (ac.Length != bc.Length) return false;
                for (int index = 0; index < ac.Length; index++)
                    if (!(ac[index] is BoxCollider ab) || !(bc[index] is BoxCollider bb) || !ab.center.Equals(bb.center) || !ab.size.Equals(bb.size) ||
                        ab.enabled != bb.enabled || ab.isTrigger != bb.isTrigger || ab.sharedMaterial != bb.sharedMaterial ||
                        ab.contactOffset != bb.contactOffset) return false;
                LODGroup al = a.GetComponent<LODGroup>(), bl = b.GetComponent<LODGroup>();
                if ((bool)al != (bool)bl) return false;
                if (al)
                {
                    if (al.size != bl.size || !al.localReferencePoint.Equals(bl.localReferencePoint) || al.fadeMode != bl.fadeMode ||
                        al.animateCrossFading != bl.animateCrossFading) return false;
                    LOD[] av = al.GetLODs(), bv = bl.GetLODs();
                    if (av.Length != bv.Length) return false;
                    for (int index = 0; index < av.Length; index++)
                        if (av[index].screenRelativeTransitionHeight != bv[index].screenRelativeTransitionHeight ||
                            av[index].fadeTransitionWidth != bv[index].fadeTransitionWidth ||
                            !av[index].renderers.Select(r => r ? Path(old.transform, r.transform) : null)
                            .SequenceEqual(bv[index].renderers.Select(r => r ? Path(current.transform, r.transform) : null))) return false;
                }
            }
            return true;
        }

        private static Dictionary<string, Transform> Nodes(Transform root, string note)
        {
            var nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                string path = Path(root, node);
                if (path == note)
                {
                    if (node.childCount != 0 || node.GetComponent<Renderer>() || node.GetComponent<Collider>() || node.GetComponent<MeshFilter>()) return null;
                    continue;
                }
                if (nodes.Count >= 128 || nodes.ContainsKey(path)) return null;
                nodes.Add(path, node);
            }
            return nodes;
        }

        private static string Path(Transform root, Transform node)
        {
            string path = "";
            while (node && node != root)
            { path = path == "" ? node.name : node.name + "/" + path; node = node.parent; }
            return path;
        }
    }
}
