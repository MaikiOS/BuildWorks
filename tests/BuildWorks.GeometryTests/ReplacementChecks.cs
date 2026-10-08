using System;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class ReplacementChecks
    {
        internal static void Run()
        {
            var identity = new Rotation3(0, 0, 0, 1);
            var evidence = new BlueprintSourceDocument("blueprint", "missing.blueprint",
                "#Pieces\nMissing;;4;5;6;0;0;0;1;\"\";1;1;1");
            var metadata = new BlueprintPrefabMetadata(1, "Missing", "Unknown", "Piece", "fixture",
                new Point3(2, 1, 3), new Point3(-1, 0, -1), new Point3(1, 2, 1));
            var document = new BlueprintEditorDocument(null, "Replace", "OTHER", new[] {
                new BlueprintEditorPart("m", "Missing", "Unknown", new Point3(4, 5, 6), identity, "g",
                    scale: new Point3(2, .5, 3), sourceDocumentId: evidence.Id, sourceLine: 2, metadata: metadata),
                new BlueprintEditorPart("b", "beam", "Beam", new Point3(7, 8, 9), new Rotation3(0, 1, 0, 0), "h",
                    scale: new Point3(.5, 2, 1), baseScale: new Point3(3, 1, 2)),
                new BlueprintEditorPart("a", "wall", "Wall", new Point3(-3, 4, 2), identity)
            }, new[] { new BlueprintEditorGroup("g", "Target", pivotPartId: "m"),
                new BlueprintEditorGroup("h", "Own assembly", pivotPartId: "b") }, primaryPartId: "m",
                sourceCustomSnapPoints: new[] { new Point3(1, 2, 3) },
                sourceSupports: new[] { new BlueprintEditorSupport("s", "Placement", new Point3(4, 5, 6), identity) },
                placementSupportId: "s", sourceDocuments: new[] { evidence });
            document.SelectOnly("m"); document.ToggleSelection("h"); document.ToggleSelection("a");
            Check(document.ReplacePartWithSelection("m", "Replacement"), "Replacement refused");
            string root = document.ActiveNodeId;
            Check(document.Parts.Count == 2 && document.Groups.Count == 3 &&
                document.Groups.Single(group => group.StableId == root).ParentGroupId == "g" &&
                document.Groups.Single(group => group.StableId == "h").ParentGroupId == root &&
                document.Groups.Single(group => group.StableId == "g").PivotPartId == "b" && document.PrimaryPartId == "b",
                "Replacement did not retain/reparent hierarchy or repair primary/pivot links");
            var beam = document.Parts.Single(part => part.StableId == "b");
            Check(beam.Position.X == 7 && beam.Position.Y == 8 && beam.Rotation.Y == 1 && beam.Scale.Y == 2 && beam.BaseScale.Value.X == 3,
                "Replacement guessed a pose or scale");
            Check(document.PlacementSupportId == "s" && document.Supports[0].Position.X == 4 &&
                document.CustomSnapPoints[0].Z == 3 && document.Sources[0].Text == evidence.Text,
                "Replacement damaged independent support/snap/source data");
            Check(document.Undo() && document.Parts.Count == 3 && document.Groups.Count == 2 && document.PrimaryPartId == "m" &&
                document.Parts[0].Metadata == metadata && document.Parts[0].SourceDocumentId == evidence.Id &&
                document.Groups[0].PivotPartId == "m" && document.Groups[1].ParentGroupId == null && !document.CanUndo,
                "One Undo did not restore the full missing record and hierarchy");
            Check(document.Redo() && document.Parts.Count == 2, "Redo failed");

            var simple = new BlueprintEditorDocument(null, "One", "OTHER", new[] {
                new BlueprintEditorPart("m", "Missing", "Missing", new Point3(), identity),
                new BlueprintEditorPart("a", "wall", "Wall", new Point3(1, 0, 0), identity) });
            simple.SelectOnly("m"); int revision = simple.RevisionToken;
            Check(!simple.ReplacePartWithSelection("m", "Replace") && simple.RevisionToken == revision, "Empty replacement changed history");
            simple.ToggleSelection("a");
            Check(simple.ReplacePartWithSelection("m", "Replace") && simple.Parts.Count == 1 && simple.Undo(), "One-to-one or Undo failed");
            simple.SetLocked("a", true); revision = simple.RevisionToken;
            Check(!simple.ReplacePartWithSelection("m", "Replace") && simple.RevisionToken == revision, "Locked part partially replaced");

            var full = new BlueprintEditorDocument(null, "Full", "OTHER", simple.Parts,
                Enumerable.Range(0, BlueprintEditorDocument.MaximumGroups).Select(i => new BlueprintEditorGroup("g" + i, "Group")));
            full.SetLocked("a", false); full.SelectOnly("m"); full.ToggleSelection("a"); revision = full.RevisionToken;
            bool refused = false;
            try { full.ReplacePartWithSelection("m", "Replace"); } catch (InvalidOperationException) { refused = true; }
            Check(refused && full.Parts.Count == 2 && full.Groups.Count == BlueprintEditorDocument.MaximumGroups &&
                full.RevisionToken == revision, "Group budget failed non-atomically");

            var emptied = new BlueprintEditorDocument(null, "Primary", "OTHER", new[] {
                new BlueprintEditorPart("m", "Missing", "Missing", new Point3(), identity),
                new BlueprintEditorPart("a", "wall", "Wall", new Point3(1, 0, 0), identity, "old") },
                new[] { new BlueprintEditorGroup("old", "Old primary", pivotPartId: "a") }, primaryGroupId: "old");
            emptied.SelectOnly("m"); emptied.ToggleSelection("a");
            Check(emptied.ReplacePartWithSelection("m", "Replace") &&
                emptied.PrimaryGroupId == emptied.ActiveNodeId && emptied.Groups[0].PivotPartId == null,
                "Empty primary group was left as a save-blocking reference");
            Check(emptied.Undo() && emptied.PrimaryGroupId == "old" && emptied.Groups[0].PivotPartId == "a",
                "Undo did not restore the emptied primary group link");

            var heavyParts = Enumerable.Range(0, 49999).Select(i => new BlueprintEditorPart(
                "p" + i, "wall", "Wall", new Point3(i, 0, 0), identity, "own")).ToList();
            heavyParts.Add(new BlueprintEditorPart("m", "Missing", "Missing", new Point3(), identity));
            var heavy = new BlueprintEditorDocument(null, "Heavy", "OTHER", heavyParts,
                new[] { new BlueprintEditorGroup("own", "Own assembly") });
            heavy.SelectOnly("m"); heavy.ToggleSelection("own");
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            Check(heavy.ReplacePartWithSelection("m", "Replace") && heavy.Parts.Count == 49999 && heavy.Undo(),
                "Heavy grouped replacement or full Undo failed");
            Console.WriteLine("PASS 50000-record grouped replacement + Undo: " + elapsed.ElapsedMilliseconds + " ms (geometry only)");
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
