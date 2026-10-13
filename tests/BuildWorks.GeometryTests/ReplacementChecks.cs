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
                "#Pieces\nMissing;;4;5;6;0;0;0;1;\"\";1;1;1\nwall;;-3;4;2;0;0;0;1;\"\";1;1;1");
            var metadata = new BlueprintPrefabMetadata(1, "Missing", "Unknown", "Piece", "fixture",
                new Point3(2, 1, 3), new Point3(-1, 0, -1), new Point3(1, 2, 1));
            var wallMetadata = new BlueprintPrefabMetadata(1, "wall", "Recorded wall", "Piece", "fixture", new Point3(1, 1, 1));
            var foreign = new BlueprintForeignPartData("blueprint", "opaque", null, null);
            var document = new BlueprintEditorDocument(null, "Replace", "OTHER", new[] {
                new BlueprintEditorPart("m", "Missing", "Unknown", new Point3(4, 5, 6), identity, "g",
                    scale: new Point3(2, .5, 3), sourceDocumentId: evidence.Id, sourceLine: 2, metadata: metadata),
                new BlueprintEditorPart("b", "beam", "Beam", new Point3(7, 8, 9), new Rotation3(0, 1, 0, 0), "h",
                    scale: new Point3(.5, 2, 1), baseScale: new Point3(3, 1, 2)),
                new BlueprintEditorPart("a", "wall", "Wall", new Point3(-3, 4, 2), identity,
                    signText: "Исходная надпись", sourceDocumentId: evidence.Id, sourceLine: 3,
                    foreignData: foreign, metadata: wallMetadata, materialSeed: 42)
            }, new[] { new BlueprintEditorGroup("g", "Target", pivotPartId: "m"),
                new BlueprintEditorGroup("h", "Own assembly", pivotPartId: "b") }, primaryPartId: "m",
                sourceCustomSnapPoints: new[] { new Point3(1, 2, 3) },
                sourceSupports: new[] { new BlueprintEditorSupport("s", "Placement", new Point3(4, 5, 6), identity) },
                placementSupportId: "s", sourceDocuments: new[] { evidence });
            document.SelectOnly("m"); document.ToggleSelection("h"); document.ToggleSelection("a");
            Check(document.ReplacePartWithSelection("m", "Replacement"), "Replacement refused");
            string root = document.ActiveNodeId;
            var copies = document.Parts.Where(part => part.StableId != "a" && part.StableId != "b").ToArray();
            var beam = copies.Single(part => part.PrefabName == "beam");
            var wall = copies.Single(part => part.PrefabName == "wall");
            Check(document.Parts.Count == 4 && document.Groups.Count == 4 &&
                document.Groups.Single(group => group.StableId == root).ParentGroupId == "g" &&
                document.Groups.Single(group => group.StableId == "h").ParentGroupId == null &&
                document.Groups.Single(group => group.StableId == beam.ParentGroupId).ParentGroupId == root &&
                document.Groups.Single(group => group.StableId == beam.ParentGroupId).PivotPartId == beam.StableId &&
                document.Groups.Single(group => group.StableId == root).PivotPartId == beam.StableId &&
                document.Groups.Single(group => group.StableId == "g").PivotPartId == beam.StableId && document.PrimaryPartId == beam.StableId,
                "Replacement did not retain/reparent hierarchy or repair primary/pivot links");
            Check(beam.Position.X == 4 && beam.Position.Y == 5 && beam.Position.Z == 6 && Math.Abs(beam.Rotation.W) == 1 &&
                beam.Scale.Y == 2 && beam.BaseScale.Value.X == 3,
                "Replacement did not align the chosen origin/rotation while retaining its own scale");
            Check(wall.Position.X == 14 && wall.Position.Y == 1 && wall.Position.Z == 13 && Math.Abs(wall.Rotation.Y) == 1,
                "Replacement changed the assembly's relative pose");
            Check(document.Parts.Single(part => part.StableId == "b").Position.X == 7 &&
                document.Parts.Single(part => part.StableId == "b").Rotation.Y == 1 &&
                document.Parts.Single(part => part.StableId == "a").Position.X == -3 &&
                wall.SignText == "Исходная надпись" && wall.SourceDocumentId == evidence.Id && wall.SourceLine == 3 &&
                wall.ForeignData == foreign && wall.Metadata == wallMetadata && wall.MaterialSeed == 42,
                "Copy replacement changed original poses or lost exact saved data");
            Check(document.PlacementSupportId == "s" && document.Supports[0].Position.X == 4 &&
                document.CustomSnapPoints[0].Z == 3 && document.Sources[0].Text == evidence.Text,
                "Replacement damaged independent support/snap/source data");
            Check(document.Undo() && document.Parts.Count == 3 && document.Groups.Count == 2 && document.PrimaryPartId == "m" &&
                document.Parts[0].Metadata == metadata && document.Parts[0].SourceDocumentId == evidence.Id &&
                document.Groups[0].PivotPartId == "m" && document.Groups[1].ParentGroupId == null && !document.CanUndo,
                "One Undo did not restore the full missing record and hierarchy");
            Check(document.Redo() && document.Parts.Count == 4 && document.Parts.Any(part => part.StableId == beam.StableId), "Redo failed or changed copy IDs");
            Check(document.Undo(), "Restore positioning fixture");
            Check(document.PositionPartWithSelection("m", new Point3(1, 2, 3), identity), "Positioning refused");
            copies = document.Parts.Where(part => part.StableId != "m" && part.StableId != "a" && part.StableId != "b").ToArray();
            Check(document.Parts.Count == 5 && document.Groups.Count == 4 && document.PrimaryPartId == "m" &&
                document.Parts[0].Position.X == 4 && document.Parts[0].Metadata == metadata &&
                document.Groups[0].PivotPartId == "m" && document.Groups[1].ParentGroupId == null &&
                copies.Single(part => part.PrefabName == "beam").Position.X == 8 &&
                copies.Single(part => part.PrefabName == "wall").Position.X == -2 &&
                document.Parts.Single(part => part.StableId == "b").Position.X == 7 &&
                document.Parts.Single(part => part.StableId == "a").Position.X == -3 &&
                document.Selection.SequenceEqual(new[] { document.ActiveNodeId }) &&
                document.Groups.Single(group => group.StableId == document.ActiveNodeId).PivotPartId == copies[0].StableId &&
                document.Supports[0].Position.X == 4 && document.Sources[0].Text == evidence.Text,
                "Positioning deleted/reparented the target or damaged relative pose, selection or evidence");
            Check(document.Undo() && document.Parts[1].Position.X == 7 && document.Selection.Contains("m") &&
                !document.CanUndo, "Positioning Undo was not atomic");
            Check(document.Redo() && document.Parts.Count == 5 && document.Parts[1].Position.X == 7 &&
                document.Parts.Any(part => part.StableId == copies[0].StableId),
                "Positioning Redo lost target or pose");

            // The source primary is deliberately not its first row; use its origin, never a bounds centre.
            var source = new BlueprintEditorDocument(null, "Catalogue", "OTHER", new[] {
                new BlueprintEditorPart("other", "wall", "Wall", new Point3(3, 0, 0), identity, "assembly"),
                new BlueprintEditorPart("anchor", "beam", "Beam", new Point3(1, 0, 0),
                    new Rotation3(0, 0, 1, 0), "assembly", scale: new Point3(.5, 2, 1), baseScale: new Point3(3, 1, 2))
            }, new[] { new BlueprintEditorGroup("assembly", "Nested", pivotPartId: "anchor") }, primaryPartId: "anchor",
                sourceCustomSnapPoints: new[] { new Point3(2, 0, 0) },
                sourceSupports: new[] { new BlueprintEditorSupport("sourceSupport", "Source", new Point3(2, 0, 0), identity) },
                sourceDocuments: new[] { evidence });
            var catalogue = new BlueprintEditorDocument(null, "Destination", "OTHER", new[] {
                new BlueprintEditorPart("m", "Missing", "Missing", new Point3(10, 20, 30), identity, "targetGroup",
                    scale: new Point3(4, 4, 4))
            }, new[] { new BlueprintEditorGroup("targetGroup", "Target", pivotPartId: "m") }, primaryPartId: "m");
            Check(catalogue.ReplacePartWithBlueprint("m", source), "Direct catalogue replacement refused");
            var insertedAnchor = catalogue.Parts.Single(part => part.PrefabName == "beam");
            var insertedOther = catalogue.Parts.Single(part => part.PrefabName == "wall");
            Check(insertedAnchor.Position.X == 10 && insertedAnchor.Position.Y == 20 && insertedAnchor.Position.Z == 30 &&
                Math.Abs(insertedAnchor.Rotation.W) == 1 && insertedAnchor.Scale.Y == 2 && insertedAnchor.BaseScale.Value.X == 3 &&
                insertedOther.Position.X == 8 && insertedOther.Position.Y == 20 &&
                catalogue.PrimaryPartId == insertedAnchor.StableId && catalogue.Groups[0].PivotPartId == insertedAnchor.StableId &&
                catalogue.Groups.Single(group => group.StableId == catalogue.ActiveNodeId).ParentGroupId == "targetGroup",
                "Catalogue replacement lost primary origin, rigid pose, native basis or parent hierarchy");
            Check(catalogue.CustomSnapPoints[0].X == 9 && catalogue.CustomSnapPoints[0].Y == 20 &&
                catalogue.Supports[0].Position.X == 9 && catalogue.Sources[0].Text == evidence.Text &&
                source.Parts.Count == 2 && source.Parts[1].Position.X == 1 && !source.CanUndo,
                "Catalogue replacement lost source anchors/evidence or mutated its source");
            Check(catalogue.Undo() && catalogue.Parts.Count == 1 && catalogue.PrimaryPartId == "m" &&
                catalogue.CustomSnapPoints.Count == 0 && catalogue.Supports.Count == 0 && catalogue.Sources.Count == 0 && !catalogue.CanUndo,
                "Catalogue replacement was not one atomic Undo");
            Check(catalogue.Redo() && catalogue.Parts.Count == 2 && catalogue.PrimaryPartId == insertedAnchor.StableId,
                "Catalogue replacement Redo changed inserted identities");

            var simple = new BlueprintEditorDocument(null, "One", "OTHER", new[] {
                new BlueprintEditorPart("m", "Missing", "Missing", new Point3(), identity),
                new BlueprintEditorPart("a", "wall", "Wall", new Point3(1, 0, 0), identity) });
            simple.SelectOnly("m"); int revision = simple.RevisionToken;
            Check(!simple.ReplacePartWithSelection("m", "Replace") && simple.RevisionToken == revision, "Empty replacement changed history");
            simple.ToggleSelection("a");
            Check(simple.ReplacePartWithSelection("m", "Replace") && simple.Parts.Count == 2 && simple.Undo(), "One-to-one or Undo failed");
            simple.SetLocked("a", true); revision = simple.RevisionToken;
            Check(!simple.ReplacePartWithSelection("m", "Replace") && simple.RevisionToken == revision, "Locked part partially replaced");

            var full = new BlueprintEditorDocument(null, "Full", "OTHER", simple.Parts,
                Enumerable.Range(0, BlueprintEditorDocument.MaximumGroups).Select(i => new BlueprintEditorGroup("g" + i, "Group")));
            full.SetLocked("a", false); full.SelectOnly("m"); full.ToggleSelection("a"); revision = full.RevisionToken;
            bool refused = false;
            try { full.ReplacePartWithSelection("m", "Replace"); } catch (InvalidOperationException) { refused = true; }
            Check(refused && full.Parts.Count == 2 && full.Groups.Count == BlueprintEditorDocument.MaximumGroups &&
                full.RevisionToken == revision, "Group budget failed non-atomically");
            refused = false;
            try { full.PositionPartWithSelection("m", new Point3(2, 0, 0), identity); } catch (InvalidOperationException) { refused = true; }
            Check(refused && full.RevisionToken == revision && full.Parts.Count == 2,
                "Copy positioning exceeded the group budget or partially committed");

            var emptied = new BlueprintEditorDocument(null, "Primary", "OTHER", new[] {
                new BlueprintEditorPart("m", "Missing", "Missing", new Point3(), identity),
                new BlueprintEditorPart("a", "wall", "Wall", new Point3(1, 0, 0), identity, "old") },
                new[] { new BlueprintEditorGroup("old", "Old primary", pivotPartId: "a") }, primaryGroupId: "old");
            emptied.SelectOnly("m"); emptied.ToggleSelection("a");
            Check(emptied.ReplacePartWithSelection("m", "Replace") &&
                emptied.PrimaryGroupId == "old" && emptied.Groups[0].PivotPartId == "a" &&
                emptied.Parts.Single(part => part.StableId == "a").ParentGroupId == "old" &&
                emptied.Parts.Single(part => part.StableId == "a").Position.X == 1,
                "Copy replacement moved the original or damaged its primary group");
            Check(emptied.Undo() && emptied.PrimaryGroupId == "old" && emptied.Groups[0].PivotPartId == "a",
                "Undo did not restore the emptied primary group link");

            var heavyParts = Enumerable.Range(0, 49999).Select(i => new BlueprintEditorPart(
                "p" + i, "wall", "Wall", new Point3(i, 0, 0), identity, "own")).ToList();
            heavyParts.Add(new BlueprintEditorPart("m", "Missing", "Missing", new Point3(), identity));
            var heavy = new BlueprintEditorDocument(null, "Heavy", "OTHER", heavyParts,
                new[] { new BlueprintEditorGroup("own", "Own assembly") });
            heavy.SelectOnly("m"); heavy.ToggleSelection("own");
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            revision = heavy.RevisionToken; refused = false;
            try { heavy.ReplacePartWithSelection("m", "Replace"); } catch (InvalidOperationException) { refused = true; }
            Check(refused && heavy.Parts.Count == 50000 && heavy.RevisionToken == revision && !heavy.CanUndo,
                "Heavy grouped copy overflow did not refuse atomically");
            Console.WriteLine("PASS 50000-record grouped copy overflow: " + elapsed.ElapsedMilliseconds + " ms (geometry only)");
            var one = new BlueprintEditorDocument(null, "One", "OTHER", new[] {
                new BlueprintEditorPart("single", "wall", "Wall", new Point3(2, 0, 0), identity) });
            var singleTarget = new BlueprintEditorDocument(null, "Single target", "OTHER", new[] {
                new BlueprintEditorPart("m", "Missing", "Missing", new Point3(5, 6, 7), new Rotation3(.2, .3, .4, .5)) }, primaryPartId: "m");
            Check(singleTarget.ReplacePartWithBlueprint("m", one) && singleTarget.Parts.Count == 1 &&
                singleTarget.PrimaryPartId == singleTarget.Parts[0].StableId &&
                Math.Abs(singleTarget.Parts[0].Position.X - 5) < 1e-10 &&
                (singleTarget.Parts[0].Rotation.Rotate(new Point3(1, 0, 0)) -
                    new Rotation3(.2, .3, .4, .5).Normalized().Rotate(new Point3(1, 0, 0))).LengthSquared < 1e-20 &&
                singleTarget.Undo() && singleTarget.PrimaryPartId == "m",
                "One-to-one catalogue replacement retained a stale part index or lost full target rotation");
            Check(heavy.ReplacePartWithBlueprint("m", one) && heavy.Parts.Count == 50000 && heavy.Undo(),
                "Replacing one at the exact part budget was incorrectly refused");
            revision = heavy.RevisionToken; refused = false;
            try { heavy.ReplacePartWithBlueprint("m", source); } catch (InvalidOperationException) { refused = true; }
            Check(refused && heavy.RevisionToken == revision && heavy.Parts.Count == 50000,
                "Catalogue over-budget replacement changed history or records");

            var nested = new BlueprintEditorDocument(null, "Nested copy", "OTHER", new[] {
                new BlueprintEditorPart("m", "Missing", "Missing", new Point3(4, 5, 6), new Rotation3(.2, .3, .4, .5)),
                new BlueprintEditorPart("a", "wall", "Wall", new Point3(1, 0, 0), identity, "inner")
            }, new[] { new BlueprintEditorGroup("outer", "Outer", pivotPartId: "a"),
                new BlueprintEditorGroup("inner", "Inner", parentGroupId: "outer", pivotPartId: "a") });
            nested.SelectOnly("m"); nested.ToggleSelection("outer");
            Check(nested.ReplacePartWithSelection("m", "Copies") && nested.Parts.Count == 2 && nested.Groups.Count == 5,
                "Nested selected group did not copy");
            var nestedCopy = nested.Parts.Single(part => part.StableId != "a");
            var innerCopy = nested.Groups.Single(group => group.StableId == nestedCopy.ParentGroupId);
            var outerCopy = nested.Groups.Single(group => group.StableId == innerCopy.ParentGroupId);
            Check(innerCopy.PivotPartId == nestedCopy.StableId && outerCopy.PivotPartId == nestedCopy.StableId &&
                outerCopy.ParentGroupId == nested.ActiveNodeId && nested.Groups[1].ParentGroupId == "outer" &&
                nested.Groups[0].PivotPartId == "a" && nested.Parts.Single(part => part.StableId == "a").Position.X == 1 &&
                nested.Undo() && !nested.CanUndo && nested.Redo() && nested.Parts.Any(part => part.StableId == nestedCopy.StableId),
                "Nested copy remap, originals or one-step history failed");

            var medium = new BlueprintEditorDocument(null, "Within copy budget", "OTHER",
                heavyParts.Take(20000).Concat(new[] { new BlueprintEditorPart("m", "Missing", "Missing", new Point3(), identity) }),
                new[] { new BlueprintEditorGroup("own", "Own assembly") });
            medium.SelectOnly("m"); medium.ToggleSelection("own");
            Check(medium.ReplacePartWithSelection("m", "Copies") && medium.Parts.Count == 40000 &&
                medium.Parts.Any(part => part.StableId == "p19999" && part.Position.X == 19999) &&
                medium.Undo() && medium.Parts.Count == 20001 && !medium.CanUndo,
                "Large in-budget copy did not preserve originals or undo atomically");
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
