using System;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class LibraryOriginMergeChecks
    {
        internal static void Run()
        {
            var a = Origin(1); var b = Origin(2);
            var destination = Document(a); var source = Document(b);
            var view = destination.LibraryOrigins;
            Check(destination.InsertBlueprint(source, new Point3(4, 0, 0), new Rotation3(0, 0, 0, 1)), "Insert failed");
            Check(view.Count == 2 && view[0].SameValue(a) && view[1].SameValue(b) && source.Parts.Count == 1 && source.LibraryOrigins.Count == 1,
                "Stable union lost or mutated a source");
            Check(destination.Undo() && view.Count == 1 && destination.Parts.Count == 1, "Undo separated parts and origins");
            Check(destination.Redo() && view.Count == 2 && destination.Parts.Count == 2, "Redo lost origin");
            Check(destination.InsertBlueprint(source, new Point3(8, 0, 0), new Rotation3(0, 0, 0, 1)) && view.Count == 2, "Repeated insertion duplicated origin");
            var observed = Document(Origin(2, sha: new string('B', 64)));
            Check(destination.InsertBlueprint(observed, new Point3(), new Rotation3(0, 0, 0, 1)) && view.Count == 3, "Distinct SHA was overwritten");
            var conflict = Document(Origin(2, publisher: "different"));
            RefuseAtomic(destination, conflict, BlueprintLibraryOrigin.ConflictError);
            var full = new BlueprintEditorDocument(null, "Full", "QA", source.Parts,
                sourceLibraryOrigins: Enumerable.Range(1, 16).Select(index => Origin(index)));
            RefuseAtomic(full, Document(Origin(17)), BlueprintLibraryOrigin.LimitError);
            Check(full.InsertBlueprint(source, new Point3(), new Rotation3(0, 0, 0, 1)) && full.LibraryOrigins.Count == 16,
                "Dedup at capacity was rejected");
            var replacement = Document(a);
            Check(replacement.ReplacePartWithBlueprint("a", source) && replacement.LibraryOrigins.Count == 2 && replacement.Undo() && replacement.LibraryOrigins.Count == 1,
                "Catalogue replacement lost atomic origin history");
            bool duplicate = false;
            try { new BlueprintEditorDocument(null, "Duplicate", "QA", sourceLibraryOrigins: new[] { a, a }); }
            catch (ArgumentException) { duplicate = true; }
            Check(duplicate && !typeof(BlueprintLibraryOrigin).GetProperties().Any(property => property.CanWrite && property.SetMethod.IsPublic),
                "Mutable or duplicate origins accepted");
        }
        private static void RefuseAtomic(BlueprintEditorDocument destination, BlueprintEditorDocument source, string message)
        {
            int revision = destination.RevisionToken, parts = destination.Parts.Count, origins = destination.LibraryOrigins.Count;
            bool undo = destination.CanUndo, redo = destination.CanRedo, refused = false;
            try { destination.InsertBlueprint(source, new Point3(), new Rotation3(0, 0, 0, 1)); }
            catch (InvalidOperationException error) { refused = error.Message == message; }
            Check(refused && destination.RevisionToken == revision && destination.Parts.Count == parts && destination.LibraryOrigins.Count == origins &&
                destination.CanUndo == undo && destination.CanRedo == redo, "Failed merge changed document/history");
        }
        private static BlueprintEditorDocument Document(BlueprintLibraryOrigin origin) => new BlueprintEditorDocument(null, "House", "QA",
            new[] { new BlueprintEditorPart("a", "woodwall", "Wall", new Point3(), new Rotation3(0, 0, 0, 1)) }, sourceLibraryOrigins: new[] { origin });
        private static BlueprintLibraryOrigin Origin(long revision, string publisher = "admin", string sha = null) =>
            new BlueprintLibraryOrigin("server", 71, "0123456789abcdef0123456789abcdef", revision, 0, publisher, sha ?? new string('A', 64), 12);
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
