using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEngine;

// Exercise the actual midpoint producer before the actual migration predicate.
public static class PrefabMigrationRuntimeAcceptance
{
    private static int assertions;
    public static void Run()
    {
        var roots = new List<GameObject>();
        Mesh mesh = null;
        try
        {
            mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            GameObject Make(string name, string note)
            {
                var root = new GameObject(name); roots.Add(root);
                root.AddComponent<Piece>();
                var model = new GameObject("Model", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
                model.transform.SetParent(root.transform, false);
                model.GetComponent<MeshFilter>().sharedMesh = mesh;
                foreach (var point in new[] { ("left", Vector3.left), ("right", Vector3.right), ("top", Vector3.up) })
                {
                    var child = new GameObject(point.Item1) { tag = "snappoint" };
                    child.transform.SetParent(root.transform, false); child.transform.localPosition = point.Item2;
                }
                new GameObject(note).transform.SetParent(root.transform, false);
                return root;
            }
            var old = Make("wood_wall_roof", "REPLACED BY wood_wall_roof_a");
            var current = Make("wood_wall_roof_a", "Replaces wood_wall_roof because of \"same name and position\" snapping issue");
            void Match(bool expected, string label) => Require(BlueprintPrefabMigrationGeometry.Matches(old, current) == expected, label);
            Match(true, "clean pair");
            var generated = new List<Transform>();
            VanillaMidpointSnapPoints.Append(current.GetComponent<Piece>(), generated);
            Require(generated.Count > 0, "production producer created helpers");
            Match(true, "owned helpers only on current prefab");
            VanillaMidpointSnapPoints.Append(old.GetComponent<Piece>(), new List<Transform>());
            Match(true, "owned helpers on both prefabs");
            var spoof = new GameObject("BuildWorks_MidSnap_99") { tag = "snappoint" };
            spoof.transform.SetParent(current.transform, false);
            Match(false, "unowned same-prefix leaf must not be ignored");
            UnityEngine.Object.DestroyImmediate(spoof);
            Transform owned = generated[0];
            var childOfHelper = new GameObject("UnexpectedChild"); childOfHelper.transform.SetParent(owned, false);
            Match(false, "owned helper with child must fail closed");
            UnityEngine.Object.DestroyImmediate(childOfHelper);
            var addedMesh = owned.gameObject.AddComponent<MeshFilter>(); addedMesh.sharedMesh = mesh;
            Match(false, "owned helper with mesh must fail closed"); UnityEngine.Object.DestroyImmediate(addedMesh);
            var addedRenderer = owned.gameObject.AddComponent<MeshRenderer>();
            Match(false, "owned helper with renderer must fail closed"); UnityEngine.Object.DestroyImmediate(addedRenderer);
            var addedCollider = owned.gameObject.AddComponent<BoxCollider>();
            Match(false, "owned helper with collider must fail closed"); UnityEngine.Object.DestroyImmediate(addedCollider);
            var addedBehaviour = owned.gameObject.AddComponent<Piece>();
            Match(false, "owned helper with behaviour must fail closed"); UnityEngine.Object.DestroyImmediate(addedBehaviour);
            owned.SetParent(current.transform.Find("Model"), false);
            Match(false, "relocated owned helper must fail closed"); owned.SetParent(current.transform, false);
            current.transform.Find("left").localPosition += Vector3.up;
            Match(false, "real native snap mutation remains rejected");
            current.transform.Find("left").localPosition -= Vector3.up;
            current.transform.Find("Model").GetComponent<BoxCollider>().size = Vector3.one * 2;
            Match(false, "real collider mutation remains rejected");
            current.transform.Find("Model").GetComponent<BoxCollider>().size = Vector3.one;
            Match(true, "restored actual pair with owned helpers");
            var oldOnly = Make("wood_wall_roof", "REPLACED BY wood_wall_roof_a");
            var cleanCurrent = Make("wood_wall_roof_a", "Replaces wood_wall_roof because of \"same name and position\" snapping issue");
            VanillaMidpointSnapPoints.Append(oldOnly.GetComponent<Piece>(), new List<Transform>());
            Require(BlueprintPrefabMigrationGeometry.Matches(oldOnly, cleanCurrent), "owned helpers only on old prefab");
            Debug.Log("PASS PrefabMigrationRuntimeAcceptance assertions=" + assertions);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally
        {
            foreach (var root in roots) if (root) UnityEngine.Object.DestroyImmediate(root);
            if (mesh) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
    private static void Require(bool value, string label)
    { assertions++; if (!value) throw new InvalidOperationException("Migration runtime regression: " + label); }
}
