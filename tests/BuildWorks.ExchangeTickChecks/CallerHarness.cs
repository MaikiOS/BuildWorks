using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

// Real callbacks and catalog method; host UI is replaced, document/history are real.
public sealed class ExchangeCallerChecks
{
    internal static class CompositeBlueprintStore
    {
        internal sealed class Blueprint { public string id = "result", importSource = "result.zip/document_0.buildworks.json", name = "Result", category = "HOMES"; }
    }
    private sealed class Store { }
    private sealed class View { public bool HasCatalog = true; }
    private static class Time { public static int frameCount = 7; }
    internal sealed class BlueprintEditorCatalogItem { public CompositeBlueprintStore.Blueprint Blueprint; }
    private sealed class Filter
    {
        public string Query = "old", Section = "old", Family = "old", Material = "old", Source = "old", Station = "old", Kind = "old";
        public int Page = 3;
    }
    private bool IsEditing = true;
    private View view = new View();
    private Store blueprintStore = new Store();
    private CompositeBlueprintStore.Blueprint pendingImportedBlueprint;
    private int pendingImportedCount, pendingImportedFrame;
    private bool catalogAttachmentMode = true, catalogBlueprintMode = false;
    private Filter catalogBlueprintFilters = new Filter();
    private string catalogNavigationId, catalogNavigationSource;
    private int catalogOpens;
    private BlueprintEditorDocument document = new BlueprintEditorDocument(null, "Draft0", "OTHER");
    private void OpenCatalog(IReadOnlyList<BlueprintEditorCatalogItem> items) { ++catalogOpens; }
    private void EditorImported(CompositeBlueprintStore.Blueprint[] imported)
    // EDITOR_CALLBACK
    private void HammerImported(CompositeBlueprintStore.Blueprint[] imported, Store expectedStore)
    // HAMMER_CALLBACK
    // CATALOG_METHOD

    private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private void HistoryIsIntact(BlueprintEditorDocument original, int token)
    {
        Check(ReferenceEquals(document, original) && document.Name == "Draft1" && document.IsDirty &&
            document.CanUndo && document.CanRedo && document.RevisionToken == token, "Import/catalog changed dirty draft or history");
        Check(document.Redo() && document.Name == "Draft2" && document.Undo() && document.Name == "Draft1" &&
            document.Undo() && document.Name == "Draft0" && document.Redo() && document.Name == "Draft1", "Draft undo/redo values changed");
    }
    public static void Run()
    {
        var checks = new ExchangeCallerChecks();
        checks.document.SetMetadata("Draft1", "OTHER"); checks.document.SetMetadata("Draft2", "OTHER"); checks.document.Undo();
        var original = checks.document; int token = original.RevisionToken;
        var target = new CompositeBlueprintStore.Blueprint();
        checks.EditorImported(new[] { target });
        Check(ReferenceEquals(checks.pendingImportedBlueprint, target) && checks.pendingImportedCount == 1 && checks.pendingImportedFrame == 7,
            "Editor callback lost its navigation target");
        checks.HistoryIsIntact(original, token);
        checks.pendingImportedBlueprint = null;
        checks.IsEditing = false; checks.EditorImported(new[] { target });
        checks.IsEditing = true; checks.view.HasCatalog = false; checks.EditorImported(new[] { target });
        checks.view.HasCatalog = true; checks.EditorImported(Array.Empty<CompositeBlueprintStore.Blueprint>());
        Check(checks.pendingImportedBlueprint == null, "Inactive editor/closed catalog/empty receipt accepted a late target");
        checks.HammerImported(new[] { target }, new Store());
        checks.HammerImported(Array.Empty<CompositeBlueprintStore.Blueprint>(), checks.blueprintStore);
        Check(checks.pendingImportedBlueprint == null, "Hammer accepted another store or empty receipt");
        checks.HammerImported(new[] { target }, checks.blueprintStore);
        Check(ReferenceEquals(checks.pendingImportedBlueprint, target) && checks.pendingImportedCount == 1, "Hammer callback lost target");
        checks.HistoryIsIntact(original, token);
        Check(!checks.ShowImportedCatalog(new[] { new BlueprintEditorCatalogItem { Blueprint = new CompositeBlueprintStore.Blueprint { importSource = "foreign.zip" } } }, target) &&
            checks.catalogOpens == 0, "Catalog selected same ID from a different source");
        Check(checks.ShowImportedCatalog(new[] { new BlueprintEditorCatalogItem { Blueprint = target } }, target) && checks.catalogOpens == 1 &&
            checks.catalogNavigationId == target.id && checks.catalogNavigationSource == target.importSource &&
            checks.catalogBlueprintFilters.Query == "" && checks.catalogBlueprintFilters.Page == 0 &&
            checks.catalogBlueprintFilters.Section == "category:HOMES" && !checks.catalogAttachmentMode && checks.catalogBlueprintMode,
            "Imported catalog did not choose exact identity/category or reset old filters");
        checks.HistoryIsIntact(original, token);
        Console.WriteLine("PASS actual editor/Hammer import callbacks and catalog selection: exact target/guards, same dirty document, unchanged Undo/Redo branch");
    }
}
