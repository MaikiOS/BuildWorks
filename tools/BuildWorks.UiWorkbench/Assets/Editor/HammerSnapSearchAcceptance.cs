using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using OstrixMods.BuildWorks;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class HammerSnapSearchAcceptance
{
    public static void CaptureOnly()
    {
        RuntimeEditorAcceptance.EnsureSnapTag();
        try { Run(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/ui-runtime"))); UnityEditor.EditorApplication.Exit(0); }
        catch (Exception error) { UnityEngine.Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
    }
    private static int foreignCalls;
    internal static void Run(string output)
    {
        var owned = new List<GameObject>();
        var harmony = new Harmony(BuildWorksPlugin.PluginGuid);
        var foreign = new Harmony("workbench.foreign.snap");
        Player previous = Player.m_localPlayer;
        try
        {
            Player player = Make("SearchPlayer", owned).AddComponent<Player>();
            Player other = Make("OtherPlayer", owned).AddComponent<Player>();
            Player.m_localPlayer = player;
            var ghost = Make(BlueprintHammerSnapSearch.PrefabPrefix + "Acceptance", owned);
            ghost.AddComponent<Piece>();
            ghost.transform.SetPositionAndRotation(new Vector3(30, 4, -18), Quaternion.Euler(13, 71, -8));
            ghost.transform.localScale = new Vector3(0.5f, 2, 1.25f);
            for (int i = 0; i < 17125; ++i)
            {
                var node = new GameObject("snappoint" + i) { tag = "snappoint" };
                node.transform.SetParent(ghost.transform, false);
                node.transform.localPosition = new Vector3(i % 137, i / 137, 0) * 0.125f;
            }
            for (int i = 0; i < 128; ++i)
            {
                var target = Make("Target" + i, owned);
                target.transform.position = ghost.transform.GetChild(i * 113).position + Vector3.right * 0.25f;
                player.SnapTargets.Add(target.transform);
            }
            var watch = Stopwatch.StartNew();
            bool expected = player.TestSnap(ghost.transform, 0.5f, out Transform expectedA, out Transform expectedB);
            double nativeMs = watch.Elapsed.TotalMilliseconds;
            Require(player.NativeSnapCalls == 17125, "Baseline did not execute full native-shaped scan");
            harmony.CreateClassProcessor(typeof(BlueprintHammerSnapSearch)).Patch();
            player.NativeSnapCalls = 0; watch.Restart();
            bool actual = player.TestSnap(ghost.transform, 0.5f, out Transform a, out Transform b);
            double indexedMs = watch.Elapsed.TotalMilliseconds;
            Require(actual == expected && a == expectedA && b == expectedB && player.NativeSnapCalls == 0,
                $"Patched large source search changed pair or did not use index: found={actual}/{expected}, source={a?.name}/{expectedA?.name}, target={b?.name}/{expectedB?.name}, nativeCalls={player.NativeSnapCalls}");
            Require(ClearContext(), "Context survived completed outer call");

            // Repeated outer calls take fresh positions; no cross-frame/list cache.
            player.SnapTargets[0].position = ghost.transform.GetChild(17124).position;
            CompareReference(player, ghost.transform, 0.5f, -1, true);
            CompareReference(player, ghost.transform, 0, -1, true);
            CompareReference(player, ghost.transform, 0.5f, 17124, false);
            Require(player.NativeSnapCalls > 0, "Manual path was accelerated instead of retained native");
            CompareReference(player, ghost.transform, -1, -1, false);
            CompareReference(player, ghost.transform, float.NaN, -1, false);
            CompareReference(player, ghost.transform, float.PositiveInfinity, -1, false);

            var small = Make(BlueprintHammerSnapSearch.PrefabPrefix + "Small", owned);
            small.AddComponent<Piece>();
            var smallPoint = new GameObject("snappoint") { tag = "snappoint" };
            smallPoint.transform.SetParent(small.transform, false);
            CompareReference(player, small.transform, 0.5f, -1, false);
            string originalName = ghost.name; ghost.name = "OrdinaryPrefab";
            CompareReference(player, ghost.transform, 0.5f, -1, false); ghost.name = originalName;
            Player.m_localPlayer = other;
            CompareReference(player, ghost.transform, 0.5f, -1, false); Player.m_localPlayer = player;

            var saved = player.SnapTargets.ToArray();
            player.SnapTargets.Clear(); CompareReference(player, ghost.transform, 0.5f, -1, true);
            // Native rounded distances stay primary; geometric ordering resolves ties.
            ghost.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); ghost.transform.localScale = Vector3.one;
            for (int i = 0; i < ghost.transform.childCount; ++i) ghost.transform.GetChild(i).localPosition = Vector3.zero;
            saved[0].position = new Vector3(1, 0.0003f, 0); saved[1].position = Vector3.right;
            player.SnapTargets.Add(saved[0]); player.SnapTargets.Add(saved[1]);
            CompareReference(player, ghost.transform, 1, -1, true);
            for (int i = 0; i < ghost.transform.childCount; ++i)
                ghost.transform.GetChild(i).localPosition = new Vector3(i % 137, i / 137, 0) * 0.125f;
            player.SnapTargets.Clear(); player.SnapTargets.AddRange(saved);

            int iteration = 0;
            other.SnapTargets.Add(saved[0]);
            player.BeforeSnapIteration = () => {
                if (++iteration == 2) other.TestSnap(small.transform, 1, out _, out _);
            };
            CompareReference(player, ghost.transform, 0.5f, -1, true);
            player.BeforeSnapIteration = () => { if (++iteration == 17128) throw new InvalidOperationException("Injected snap fault"); };
            bool threw = false;
            try { player.TestSnap(ghost.transform, 0.5f, out _, out _); }
            catch (InvalidOperationException) { threw = true; }
            Require(threw && ClearContext(), "Exception leaked scoped snapshot");
            player.BeforeSnapIteration = null;

            MethodInfo inner = AccessTools.Method(typeof(Player), "FindClosestSnappoint");
            foreign.Patch(inner, prefix: new HarmonyMethod(typeof(HammerSnapSearchAcceptance), nameof(ForeignPrefix)));
            foreignCalls = 0; player.NativeSnapCalls = 0;
            player.TestSnap(ghost.transform, 0.5f, out _, out _);
            Require(foreignCalls == 17125 && player.NativeSnapCalls == 17125,
                "Native delegate fallback bypassed later foreign prefix");
            foreign.UnpatchSelf();
            CompareReference(player, ghost.transform, 0.5f, -1, true);

            var random = new System.Random(123);
            for (int pass = 0; pass < 12; ++pass)
            {
                for (int i = 0; i < saved.Length; ++i)
                    saved[i].position = new Vector3((float)random.NextDouble() * 20,
                        (float)random.NextDouble() * 20, (float)random.NextDouble() * 2 - 1);
                CompareReference(player, ghost.transform, pass % 2 == 0 ? 0.5f : 1f, -1, true);
            }
            File.WriteAllText(Path.Combine(output, "hammer-snap-search-check.txt"),
                $"PASS: actual Harmony patches of inert native-signature Player fixture;17125 rotated/XYZ source transforms x128 targets; original ordered pair matches; manual/small/ordinary/nonlocal/invalid radius fallback; fresh targets, sqrt ties, empty sets, nested calls/exception cleanup and later foreign prefix preserved. One Unity fixture sample: reference {nativeMs:F4}ms, indexed {indexedMs:F4}ms. Not actual Valheim Player/world/FPS/network proof.");
        }
        finally
        {
            foreign.UnpatchSelf(); harmony.UnpatchSelf(); Player.m_localPlayer = previous;
            foreach (GameObject item in owned) Object.DestroyImmediate(item);
        }
    }

    private static void ForeignPrefix() { ++foreignCalls; }
    private static bool ClearContext() => AccessTools.Field(typeof(BlueprintHammerSnapSearch), "current").GetValue(null) == null;
    private static GameObject Make(string name, List<GameObject> owned)
    { var go = new GameObject(name); owned.Add(go); return go; }

    private static void CompareReference(Player player, Transform ghost, float radius, int manual, bool indexed)
    {
        var sources = new List<Transform>(); ghost.GetComponent<Piece>().GetSnapPoints(sources);
        var targets = new List<Transform>(player.SnapTargets);
        if (manual < 0 && player == Player.m_localPlayer && !float.IsNaN(radius) && !float.IsInfinity(radius) && radius >= 0)
        {
            sources = sources.OrderBy(p => p.position.x).ThenBy(p => p.position.y).ThenBy(p => p.position.z).ToList();
            targets = targets.OrderBy(p => p.position.x).ThenBy(p => p.position.y).ThenBy(p => p.position.z).ToList();
        }
        Transform expectedA = null, expectedB = null; float best = 9999999f;
        for (int i = 0; i < sources.Count; ++i)
        {
            if (manual >= 0 && i != manual) continue;
            Transform target = null; float distance = 999999f;
            foreach (Transform candidate in targets)
            {
                float d = Vector3.Distance(candidate.position, sources[i].position);
                if (!(d > radius) && d < distance) { target = candidate; distance = d; }
            }
            if (target && distance < best) { expectedA = sources[i]; expectedB = target; best = distance; }
        }
        player.ManualSnap = manual; player.NativeSnapCalls = 0;
        bool found = player.TestSnap(ghost, radius, out Transform a, out Transform b);
        Require(found == (expectedA != null) && (!found ||
            a.position.Equals(expectedA.position) && b.position.Equals(expectedB.position)), "Search differs from geometric Unity Distance reference");
        Require(manual < 0 || !found || a == expectedA, "Manual source identity changed");
        Require(indexed ? player.NativeSnapCalls == 0 : player.NativeSnapCalls > 0, "Wrong accelerated/fallback branch");
        Require(ClearContext(), "Context leaked between calls"); player.ManualSnap = -1;
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
