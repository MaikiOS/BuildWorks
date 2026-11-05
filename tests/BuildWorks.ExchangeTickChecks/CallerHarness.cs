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
    private sealed class View
    {
        public bool HasCatalog = true;
        public string Status;
        public BlueprintExchangeView Export;
        public void SetStatus(string status) { Status = status; }
        public void ShowExchange(Func<object, object, Action, BlueprintExchangeView> create)
        { Export = create(this, this, () => { }); }
    }
    private class HostObject
    {
        public object transform = new object();
        public HostObject Parent;
        public T GetComponentInParent<T>() where T : class => Parent as T;
        public static implicit operator bool(HostObject value) => value != null;
    }
    private sealed class Canvas : HostObject { }
    private sealed class TMP_Text : HostObject { }
    private sealed class Hud : HostObject
    {
        public static Hud instance;
        public HostObject m_pieceSelectionWindow;
        public TMP_Text m_pieceDescription, m_buildSelection;
    }
    private static class BuildWorksLocalization { public static string Text(string key) => key; }
    private static class BuildWorksPlugin
    { public static object ServerLibraryClient = new object(), PeerBlueprintClient = new object(); }
    private sealed class BlueprintExchangeView
    {
        public readonly object Parent, Font;
        public readonly Store Store;
        public readonly CompositeBlueprintStore.Blueprint Blueprint;
        public readonly Action Closed;
        public readonly bool FrontCanvas;
        public readonly string Report;
        public bool Disposed;
        public BlueprintExchangeView(object parent, object font, Store store, CompositeBlueprintStore.Blueprint blueprint,
            Action closed, bool frontCanvas = false, string report = null, Action<CompositeBlueprintStore.Blueprint[]> imported = null)
        { Parent = parent; Font = font; Store = store; Blueprint = blueprint; Closed = closed; FrontCanvas = frontCanvas; Report = report; }
        public void Dispose() { Disposed = true; }
        public static BlueprintExchangeView ForServer(object parent, object font, Store store, CompositeBlueprintStore.Blueprint blueprint,
            Func<object> client, Action closed, Action<CompositeBlueprintStore.Blueprint[]> accepted, bool frontCanvas, Func<object> peers)
            => throw new InvalidOperationException("Ordinary export invoked server sharing");
        public static BlueprintExchangeView ForShareFile(object parent, object font, Store store, CompositeBlueprintStore.Blueprint blueprint,
            Action closed, bool frontCanvas) => throw new InvalidOperationException("Ordinary export invoked share-file flow");
    }
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
    private Store store => blueprintStore;
    private BlueprintExchangeView exchangeView;
    private bool placementExchange = true, blueprintImportPending;
    private int exchangeClosedFrame, invalidations;
    private void Invalidate() { ++invalidations; }
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
    private void EditorExport(CompositeBlueprintStore.Blueprint blueprint)
    // EDITOR_EXPORT_CALLBACK
    private void HammerExport(CompositeBlueprintStore.Blueprint blueprint)
    // HAMMER_EXPORT_BODY
    private void ShowExchange(CompositeBlueprintStore.Blueprint blueprint, string report = null, bool share = false, bool server = false)
    // HAMMER_EXCHANGE_BODY
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
        ExportCallers();
    }

    private static void ExportCallers()
    {
        var checks = new ExchangeCallerChecks();
        checks.document.SetMetadata("Draft1", "OTHER"); checks.document.SetMetadata("Draft2", "OTHER"); checks.document.Undo();
        var original = checks.document; int token = original.RevisionToken;
        var selected = new CompositeBlueprintStore.Blueprint { id = "selected" };
        checks.EditorExport(selected);
        var editorExport = checks.view.Export;
        Check(editorExport != null && ReferenceEquals(editorExport.Blueprint, selected) && ReferenceEquals(editorExport.Store, checks.store) &&
            !editorExport.FrontCanvas && editorExport.Report == null, "Editor export lost selected blueprint/store or opened another workflow");
        checks.EditorExport(null);
        Check(ReferenceEquals(checks.view.Export, editorExport) && checks.view.Status == "catalog.choose_blueprint", "Editor null selection opened import/export");
        var canvas = new Canvas();
        var hud = new Hud { m_pieceSelectionWindow = new HostObject { Parent = canvas }, m_pieceDescription = new TMP_Text(), m_buildSelection = new TMP_Text() };
        Hud.instance = hud; checks.HammerExport(selected);
        var hammerExport = checks.exchangeView;
        Check(hammerExport != null && ReferenceEquals(hammerExport.Blueprint, selected) && ReferenceEquals(hammerExport.Store, checks.blueprintStore) &&
            ReferenceEquals(hammerExport.Parent, canvas.transform) && ReferenceEquals(hammerExport.Font, hud.m_pieceDescription) &&
            hammerExport.FrontCanvas && hammerExport.Report == null && !checks.placementExchange, "Hammer export lost selection/store/HUD or opened another workflow");
        var other = new CompositeBlueprintStore.Blueprint { id = "other" };
        hud.m_pieceDescription = null; checks.HammerExport(other);
        Check(hammerExport.Disposed && ReferenceEquals(checks.exchangeView.Blueprint, other) && ReferenceEquals(checks.exchangeView.Font, hud.m_buildSelection),
            "Hammer export retained previous selection/modal or lost fallback font");
        checks.exchangeView.Closed();
        Check(checks.exchangeView == null && checks.exchangeClosedFrame == Time.frameCount && checks.invalidations == 1 && !checks.blueprintImportPending,
            "Closing export retained modal or scheduled import navigation");
        Hud.instance = null; checks.HammerExport(selected);
        Hud.instance = hud; hud.m_pieceSelectionWindow = null; checks.HammerExport(selected);
        hud.m_pieceSelectionWindow = new HostObject(); checks.HammerExport(selected);
        hud.m_pieceSelectionWindow.Parent = canvas; checks.blueprintStore = null; checks.HammerExport(selected);
        Check(checks.exchangeView == null, "Missing HUD/canvas/store opened an export modal");
        checks.HistoryIsIntact(original, token);
        Hud.instance = null;
        Console.WriteLine("PASS actual editor export callback and Hammer export/ShowExchange bodies: selected blueprint/store, guards, modal handoff/close and unchanged draft history; host UI substituted, no rendered/runtime proof.");
    }
}
