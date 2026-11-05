using System;
using System.Collections.Generic;
using System.IO;
using OstrixMods.BuildWorks;
using UnityEngine;

internal static class Program
{
    private static int checks, failures;

    private static Piece Native(string name)
    {
        var root = new GameObject(name);
        var piece = root.AddComponent<Piece>();
        for (int index = 0; index < 2; ++index)
        {
            var point = new GameObject("native") { tag = "snappoint" };
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = Vector3.right * (index * 2);
        }
        return piece;
    }

    private static void Append(Piece piece)
    {
        var points = new List<Transform>();
        piece.GetSnapPoints(points);
        VanillaMidpointSnapPoints.Append(piece, points);
        if (piece.GetComponent<VanillaMidpointSnapPoints>() == null || points.Count != 3)
            throw new InvalidOperationException("Actual production Append did not create the cache/midpoint.");
    }

    private static void Expect(GameObject prefab, bool expected, string label)
    {
        bool allowed = true;
        try { StationPolicy.Check(prefab); }
        catch (InvalidDataException) { allowed = false; }
        ++checks;
        if (allowed != expected)
        {
            ++failures;
            Console.WriteLine("FAIL " + label + ": expected allowed=" + expected + ", actual=" + allowed);
        }
    }

    private static int Main()
    {
        if (typeof(Piece).Assembly == typeof(VanillaMidpointSnapPoints).Assembly)
            throw new InvalidOperationException("Fixture would give false native assembly identity.");
        Console.WriteLine("Piece assembly=" + typeof(Piece).Assembly.GetName().Name +
            "; cache assembly=" + typeof(VanillaMidpointSnapPoints).Assembly.GetName().Name);

        var station = Native("station");
        Expect(station.gameObject, true, "native before Append");
        Append(station);
        Expect(station.gameObject, true, "native after actual Append/exact root cache");

        var unknownRoot = Native("unknown-root");
        unknownRoot.gameObject.AddComponent<BuildWorks_ForeignBehaviour>();
        Expect(unknownRoot.gameObject, false, "unknown root behaviour");

        var unknownChild = Native("unknown-child");
        var child = new GameObject("child");
        child.transform.SetParent(unknownChild.transform, false);
        child.AddComponent<BuildWorks_ForeignBehaviour>();
        Expect(unknownChild.gameObject, false, "unknown child behaviour");

        var childCache = Native("child-cache");
        var nested = Native("nested-piece");
        nested.transform.SetParent(childCache.transform, false);
        Append(nested);
        Expect(childCache.gameObject, false, "child cache");

        var duplicate = Native("duplicate-cache");
        Append(duplicate);
        duplicate.gameObject.AddComponent<VanillaMidpointSnapPoints>();
        Expect(duplicate.gameObject, false, "second root cache");

        var mixed = Native("own-cache-and-foreign");
        Append(mixed);
        mixed.gameObject.AddComponent<BuildWorks_ForeignBehaviour>();
        Expect(mixed.gameObject, false, "own cache does not admit foreign sibling");

        var invalid = Native("invalid-native-behaviour");
        invalid.Alive = false;
        Expect(invalid.gameObject, false, "invalid Unity behaviour");

        VanillaMidpointSnapPoints.RemoveAll();
        Console.WriteLine((failures == 0 ? "PASS " : "FAIL ") + checks +
            " station component-policy checks; failures=" + failures +
            "; separate assembly identities; actual Append; no station/geometry/placement runtime.");
        return failures == 0 ? 0 : 1;
    }
}

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksPlugin { internal const string PluginGuid = "ostrmod.buildworks"; }
    internal static class HammerBlueprintPieceRegistry { internal const string PrefabPrefix = "BuildWorks_Blueprint_"; }
    internal sealed class BuildWorks_ForeignBehaviour : MonoBehaviour { }
}
