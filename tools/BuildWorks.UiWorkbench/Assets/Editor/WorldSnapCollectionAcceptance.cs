using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class WorldSnapCollectionAcceptance
{
    private static readonly FieldInfo Current = AccessTools.Field(typeof(BlueprintWorldSnapPoints), "current");
    private static readonly FieldInfo Spare = AccessTools.Field(typeof(BlueprintWorldSnapPoints), "spare");
    private static int foreignCalls;
    private static bool Active => Current.GetValue(null) != null;

    internal static void Run(string output)
    {
        var root = new GameObject("WorldCollectionAcceptance");
        var harmony = new Harmony(BuildWorksPlugin.PluginGuid);
        var foreign = new Harmony("workbench.foreign.collection");
        try
        {
            Piece first = Make(root, "First"), second = Make(root, "Second");
            var initial = new List<Transform>(); BlueprintWorldSnapPoints.Append(first, initial);
            initial.Add(initial[0]); initial.Add(null);
            Piece.NearbyPieces.AddRange(new[] { first, second, first });
            PatchPostfix(harmony);
            var expected = new List<Transform>(initial); var expectedPieces = new List<Piece>();
            Piece.GetSnapPoints(Vector3.zero, 10, expected, expectedPieces);
            Require(expected.Any(VanillaMidpointSnapPoints.IsGenerated), "Native midpoint fixture not exercised");
            harmony.CreateClassProcessor(typeof(BlueprintWorldSnapPoints.CollectionPatch)).Patch();
            Compare(initial, expected, expectedPieces);
            object pooled = Spare.GetValue(null);
            Compare(initial, expected, expectedPieces);
            Require(ReferenceEquals(pooled, Spare.GetValue(null)), "Top-level collection does not reuse its cleared set");
            Require(AccessTools.Field(pooled.GetType(), "Result").GetValue(pooled) == null &&
                ((HashSet<Transform>)AccessTools.Field(pooled.GetType(), "Seen").GetValue(pooled)).Count == 0,
                "Pooled context retains scene references");

            var alternative = new List<Transform>(initial);
            Piece.BeforeCollection = piece => {
                BlueprintWorldSnapPoints.Append(piece, alternative); BlueprintWorldSnapPoints.Append(piece, alternative);
            };
            Compare(initial, expected, expectedPieces);
            Require(alternative.Count == initial.Count + 2, "Different result list used scoped membership");
            Piece.BeforeCollection = null;

            foreach (bool sameList in new[] { false, true })
            {
                var actual = new List<Transform>(initial);
                var visited = new List<Piece>(); int calls = 0;
                Piece.BeforeCollection = _ => {
                    if (++calls != 2) return;
                    object parent = Current.GetValue(null);
                    var callback = Piece.BeforeCollection; Piece.BeforeCollection = null;
                    Piece.GetSnapPoints(Vector3.zero, 10, sameList ? actual : new List<Transform>(), new List<Piece>());
                    Piece.BeforeCollection = callback;
                    Require(ReferenceEquals(parent, Current.GetValue(null)), "Nested query replaced parent context");
                };
                Piece.GetSnapPoints(Vector3.zero, 10, actual, visited);
                Piece.BeforeCollection = null;
                Require(!Active && actual.Count(t => t && t.name == "BuildWorks_CustomSnap") ==
                    expected.Count(t => t && t.name == "BuildWorks_CustomSnap"), "Nested query duplicated custom points");
                if (!sameList) Require(actual.SequenceEqual(expected), "Nested separate list changed output order");
            }
            int iteration = 0;
            Piece.BeforeCollection = _ => { if (++iteration == 2) throw new InvalidOperationException("Injected world query fault"); };
            bool threw = false;
            try { Piece.GetSnapPoints(Vector3.zero, 10, new List<Transform>(), new List<Piece>()); }
            catch (InvalidOperationException) { threw = true; }
            Require(threw && !Active, "Exception leaked collection context");
            Piece.BeforeCollection = null; Compare(initial, expected, expectedPieces);

            foreach (Type[] signature in new[] { new[] { typeof(List<Transform>) },
                new[] { typeof(Vector3), typeof(float), typeof(List<Transform>), typeof(List<Piece>) } })
            {
                foreignCalls = 0;
                foreign.Patch(AccessTools.Method(typeof(Piece), nameof(Piece.GetSnapPoints), signature),
                    prefix: new HarmonyMethod(typeof(WorldSnapCollectionAcceptance), nameof(ForeignPrefix)));
                Compare(initial, expected, expectedPieces);
                Require(foreignCalls > 0, "Foreign patch bypassed"); foreign.UnpatchSelf();
            }
            Piece.NearbyPieces.Clear();
            var empty = new List<Transform>(initial);
            Piece.GetSnapPoints(Vector3.zero, 10, empty, new List<Piece>());
            Require(empty.SequenceEqual(initial) && !Active, "Empty query altered seed/leaked context");
            File.WriteAllText(Path.Combine(output, "world-snap-collection-check.txt"),
                "PASS: actual production Harmony static collection + composed production native/midpoint/custom helpers on inert native-signature Piece; exact ordered reference list/piece visits, repeated hits, preseeded duplicates/null, other list, nested same/different lists, fault cleanup, cleared pool reuse and foreign patches on either overload. Plugin postfix call order pinned by HostContract. Not Valheim physics/save/network/FPS.");
        }
        finally
        {
            Piece.BeforeCollection = null; Piece.NearbyPieces.Clear();
            foreign.UnpatchSelf(); harmony.UnpatchSelf();
            Object.DestroyImmediate(root); BlueprintWorldSnapPoints.RemoveAll(); VanillaMidpointSnapPoints.RemoveAll();
        }
    }

    internal static string Measure(IReadOnlyList<Piece> pieces)
    {
        var harmony = new Harmony(BuildWorksPlugin.PluginGuid);
        try
        {
            Piece.NearbyPieces.AddRange(pieces);
            PatchPostfix(harmony);
            var expected = new List<Transform>(); var expectedPieces = new List<Piece>();
            var watch = Stopwatch.StartNew();
            Piece.GetSnapPoints(Vector3.zero, 10, expected, expectedPieces);
            double baseline = watch.Elapsed.TotalMilliseconds;
            harmony.CreateClassProcessor(typeof(BlueprintWorldSnapPoints.CollectionPatch)).Patch();
            var actual = new List<Transform>(); var actualPieces = new List<Piece>(); watch.Restart();
            Piece.GetSnapPoints(Vector3.zero, 10, actual, actualPieces);
            double indexed = watch.Elapsed.TotalMilliseconds;
            Require(actual.SequenceEqual(expected) && actualPieces.SequenceEqual(expectedPieces) && !Active,
                "Large native/postfix collection changed reference order/count");
            return $"fullPostfixReference={baseline:F4}ms, scopedCollection={indexed:F4}ms";
        }
        finally { Piece.NearbyPieces.Clear(); harmony.UnpatchSelf(); }
    }

    private static Piece Make(GameObject root, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform, false);
        Piece piece = go.AddComponent<Piece>(); go.AddComponent<ZNetView>();
        BlueprintWorldSnapPoints.Apply(go, new[] { new Point3(1, 0, 0), new Point3(3, 0, 0) });
        foreach (float x in new[] { 0f, 2f })
        {
            var node = new GameObject("Native") { tag = "snappoint" }; node.transform.SetParent(go.transform, false);
            node.transform.localPosition = Vector3.right * x;
        }
        return piece;
    }
    private static void Compare(List<Transform> initial, List<Transform> expected, List<Piece> expectedPieces)
    {
        var actual = new List<Transform>(initial); var visited = new List<Piece>();
        Piece.GetSnapPoints(Vector3.zero, 10, actual, visited);
        Require(actual.SequenceEqual(expected) && visited.SequenceEqual(expectedPieces) && !Active,
            "Scoped query changed existing/native/midpoint/custom order or leaked context");
    }
    private static void ForeignPrefix() { ++foreignCalls; Require(!Active, "Foreign patch ran inside optimized context"); }
    private static void PatchPostfix(Harmony harmony) => harmony.Patch(
        AccessTools.Method(typeof(Piece), nameof(Piece.GetSnapPoints), new[] { typeof(List<Transform>) }),
        postfix: new HarmonyMethod(typeof(WorldSnapCollectionAcceptance), nameof(AppendPostfix)));
    private static void AppendPostfix(Piece __instance, List<Transform> points)
    {
        VanillaMidpointSnapPoints.Append(__instance, points);
        BlueprintWorldSnapPoints.Append(__instance, points);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
