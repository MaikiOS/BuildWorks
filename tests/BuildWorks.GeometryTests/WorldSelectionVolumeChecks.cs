using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

internal static class WorldSelectionVolumeChecks
{
    public static void Run()
    {
        var volume = new WorldSelectionVolume(new Point3(10004, 20, -9998), new Point3(10000, -2, -10000));
        Check(volume.Contains(new Point3(10000, -2, -10000)) && volume.Contains(new Point3(10004, 20, -9998)),
            "Both boundaries must be inclusive at real world coordinates.");
        Check(!volume.Contains(new Point3(10004.001, 20, -9998)) && !volume.Contains(new Point3(10002, 20.001, -9999)),
            "Outside origins must not enter the volume.");
        Check((volume.Maximum - volume.Center).LengthSquared < volume.QueryRadius * volume.QueryRadius,
            "Host strict radius query would omit corner pieces.");
        Check(!volume.Contains(new Point3(double.NaN, 0, 0)), "Non-finite origin entered the volume.");
        Reject(() => new WorldSelectionVolume(new Point3(double.NaN, 0, 0), new Point3()));
        Reject(() => new WorldSelectionVolume(new Point3(), new Point3(0, double.PositiveInfinity, 0)));
        Reject(() => new WorldSelectionVolume(new Point3(-double.MaxValue, 0, 0), new Point3(double.MaxValue, 0, 0)));

        volume = new WorldSelectionVolume(new Point3(), new Point3(2, 2, 2));
        Point3 Position(int id) => new Point3(id, 1, 1);
        var included = new HashSet<int> { 4, 2 };
        var excluded = new HashSet<int> { 1, 4 };
        Check(volume.TrySelect(new[] { 0, 1, 2, 2, 3 }, Position, included, excluded, 2, out List<int> selected) &&
            selected.SequenceEqual(new[] { 0, 2 }), "Origin rule, deduplication or exclude priority changed.");
        excluded.Remove(4);
        Check(volume.TrySelect(new[] { 0, 1, 2, 3 }, Position, included, excluded, 3, out selected) &&
            selected.SequenceEqual(new[] { 0, 2, 4 }), "Manual outside inclusion was lost.");
        Check(!volume.TrySelect(new[] { 0, 1, 2 }, Position, included, excluded, 2, out selected) && selected == null,
            "Over-budget proposal must fail without a partial selection.");
        Check(included.SetEquals(new[] { 4, 2 }) && excluded.SetEquals(new[] { 1 }), "Selection mutated manual decisions.");
        volume = new WorldSelectionVolume(new Point3(0, 100, 0), new Point3(2, 200, 2));
        Check(volume.TrySelect(new[] { 0, 1, 2 }, Position, included, excluded, 3, out selected) &&
            selected.SequenceEqual(new[] { 4, 2 }), "Changing height discarded manual decisions.");
        Check(volume.TrySelect(Array.Empty<int>(), Position, Array.Empty<int>(), new HashSet<int>(), 1, out selected) &&
            selected.Count == 0, "Empty selection failed.");
        Reject(() => volume.TrySelect(Array.Empty<int>(), Position, included, excluded, 0, out _));

        const int count = 50000;
        volume = new WorldSelectionVolume(new Point3(), new Point3(count, 1, 1));
        Check(volume.TrySelect(Enumerable.Range(0, count), i => new Point3(i, 0, 0), Array.Empty<int>(),
            new HashSet<int>(), count, out selected) && selected.Count == count && selected[0] == 0 && selected[count - 1] == count - 1,
            "Heavy selection dropped records or changed source order.");
        Check(!volume.TrySelect(Enumerable.Range(0, count + 1), i => new Point3(i, 0, 0), Array.Empty<int>(),
            new HashSet<int>(), count, out selected) && selected == null, "Heavy overflow produced a partial result.");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception("Invalid volume/input accepted.");
    }
}
