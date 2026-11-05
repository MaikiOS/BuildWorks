using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Real Unity lifetime/TRS only; Piece/native collection are existing Workbench host substitutes.
[InitializeOnLoad]
public static class MidpointLifecycleAcceptance
{
    private const string Pending = "BuildWorks.MidpointLifecycle.Pending";
    private static IEnumerator run;
    private static int frame = -1, assertions;
    private static double started;
    private static readonly List<GameObject> roots = new List<GameObject>();
    private static readonly List<string> checks = new List<string>();

    static MidpointLifecycleAcceptance()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            started = EditorApplication.timeSinceStartup;
            run = Exercise();
            EditorApplication.update += Advance;
        };
    }

    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Advance()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 30) throw new TimeoutException("Midpoint lifecycle exceeded30s play time.");
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (run.MoveNext()) return;
            EditorApplication.update -= Advance;
            Cleanup();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            EditorApplication.update -= Advance;
            Cleanup();
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static GameObject Make(string name)
    {
        var root = new GameObject(name); roots.Add(root); root.AddComponent<Piece>();
        foreach (var position in new[] { new Vector3(1,0,0), new Vector3(3,2,0) })
        {
            var node = new GameObject("native") { tag = "snappoint" };
            node.transform.SetParent(root.transform, false); node.transform.localPosition = position;
        }
        return root;
    }

    private static List<Transform> Query(GameObject root)
    {
        var result = new List<Transform>(); var piece = root.GetComponent<Piece>();
        piece.GetSnapPoints(result); VanillaMidpointSnapPoints.Append(piece, result); return result;
    }

    private static void Require(bool value, string label)
    { ++assertions; if (!value) throw new InvalidOperationException(label); checks.Add(label); }

    private static IEnumerator Exercise()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/fb-0006-20261105/supplement-p1/unity"));
        Directory.CreateDirectory(output);
        var active = Make("active");
        var initial = Query(active); var midpoint = initial.Single(VanillaMidpointSnapPoints.IsGenerated);
        Vector3 local = midpoint.localPosition; Quaternion rotation = midpoint.localRotation;
        active.transform.SetPositionAndRotation(new Vector3(5,-2,7), Quaternion.Euler(20,40,5));
        active.transform.localScale = new Vector3(2,3,.5f);
        Vector3 expectedWorld = active.transform.TransformPoint(local);
        midpoint.hasChanged = false;
        Require(Query(active).SequenceEqual(initial) && !midpoint.hasChanged, "warm root TRS keeps identity/order without local writes");
        Require(Vector3.Distance(midpoint.position, expectedWorld) < .00001f &&
            Quaternion.Angle(midpoint.rotation, active.transform.rotation * rotation) < .001f,
            "root translation/rotation/nonuniform scale propagate real child world frame");
        active.transform.GetChild(1).localPosition = new Vector3(5,2,0);
        Require(Query(active).Contains(midpoint) && midpoint.localPosition.Equals(new Vector3(3,1,0)), "local edit invalidates real Unity midpoint");
        var activeClone = Object.Instantiate(active); roots.Add(activeClone);
        var activeCloneMid = Query(activeClone).Single(VanillaMidpointSnapPoints.IsGenerated);
        Require(activeClone.activeInHierarchy && activeCloneMid != midpoint && VanillaMidpointSnapPoints.IsOwned(activeCloneMid) && midpoint,
            "active clone owns its remapped midpoint without destroying the source node");
        Object.Destroy(activeClone); Object.Destroy(active);
        yield return null; yield return null;
        Require(!active && !activeClone && !midpoint && !activeCloneMid, "active source/clone deferred Destroy removes cache hierarchy after frame boundary");

        var template = Make("inactive-template"); template.SetActive(false);
        var sourceMid = Query(template).Single(VanillaMidpointSnapPoints.IsGenerated);
        var clone = Object.Instantiate(template); roots.Add(clone);
        var cloneMid = clone.GetComponentsInChildren<Transform>(true).Single(VanillaMidpointSnapPoints.IsGenerated);
        var cloneCache = clone.GetComponent<VanillaMidpointSnapPoints>();
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var owner = (Transform)typeof(VanillaMidpointSnapPoints).GetField("owner", fields).GetValue(cloneCache);
        var owned = (List<GameObject>)typeof(VanillaMidpointSnapPoints).GetField("owned", fields).GetValue(cloneCache);
        // No Append/Awake is required on this never-active clone before teardown.
        Require(!clone.activeInHierarchy && owner == clone.transform && owned.Contains(cloneMid.gameObject) &&
            !owned.Contains(sourceMid.gameObject) && cloneMid != sourceMid && VanillaMidpointSnapPoints.IsOwned(cloneMid),
            "inactive clone serialized ownership remaps to cloned child before first Append");
        VanillaMidpointSnapPoints.RemoveAll();
        Require(!sourceMid.CompareTag("snappoint") && !cloneMid.CompareTag("snappoint") &&
            !sourceMid.gameObject.activeSelf && !cloneMid.gameObject.activeSelf && !sourceMid.parent && !cloneMid.parent,
            "RemoveAll explicitly retires source and unqueried clone before deferred Destroy");
        Require(!Query(template).Any(VanillaMidpointSnapPoints.IsGenerated) && !Query(clone).Any(VanillaMidpointSnapPoints.IsGenerated),
            "same-frame query cannot revive retiring cache or nodes");
        yield return null; yield return null;
        Require(!sourceMid && !cloneMid && !template.GetComponent<VanillaMidpointSnapPoints>() && !clone.GetComponent<VanillaMidpointSnapPoints>(),
            "RemoveAll removes never-active source and unqueried clone cache after frame boundary");
        var repaired = Query(clone).Single(VanillaMidpointSnapPoints.IsGenerated);
        Require(VanillaMidpointSnapPoints.IsOwned(repaired) && repaired.parent == clone.transform, "post-teardown query creates fresh owned nodes");
        Object.Destroy(clone); Object.Destroy(template);
        yield return null; yield return null;
        Require(!clone && !template && !repaired, "never-active deferred Destroy removes cache and owned nodes without relying on OnDestroy");
        Require(Resources.FindObjectsOfTypeAll<VanillaMidpointSnapPoints>().Length == 0, "no remaining native cache objects after owned fixtures destroyed");
        WorldSnapCollectionAcceptance.Run(output);
        File.WriteAllText(Path.Combine(output, "lifecycle-check.txt"), "PASS assertions=" + assertions + " unity=" + Application.unityVersion +
            "\n" + string.Join("\n", checks) + "\nReal Unity lifetime/TRS; inert Piece host. Not Valheim/FPS/payment/save/network.");
        Debug.Log("BUILDWORKS_MIDPOINT_LIFECYCLE_OK assertions=" + assertions);
    }

    private static void Cleanup()
    {
        VanillaMidpointSnapPoints.RemoveAll();
        foreach (var root in roots) if (root) Object.DestroyImmediate(root);
        roots.Clear();
    }
}
