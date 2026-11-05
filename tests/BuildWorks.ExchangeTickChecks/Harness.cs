using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// Executes the actual Tick method with CPU substitutes for UI, tasks and the store.
public sealed class ExchangeTickChecks
{
    private class Selectable
    {
        public string name;
        public bool interactable = true;
        public GameObject gameObject = new GameObject();
        public T GetComponentInChildren<T>() where T : new() { return new T(); }
        public static implicit operator bool(Selectable value) { return value != null; }
    }
    private sealed class Scrollbar : Selectable { }
    private sealed class Vector3
    {
        public static readonly Vector3 one = new Vector3();
        public static Vector3 operator *(Vector3 value, float scale) { return value; }
    }
    private struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    private sealed class Rect { public float width = 1920, height = 1080; }
    private sealed class RectTransform
    {
        public RectTransform parent;
        public Rect rect = new Rect();
        public Vector3 localScale = Vector3.one;
        public Vector2 sizeDelta;
        public GameObject gameObject = new GameObject();
        public RectTransform Find(string name) => this;
        public T GetComponent<T>() where T : class => this as T;
    }
    private static class Mathf { public static float Min(params float[] values) { return values.Min(); } }
    private sealed class Root
    {
        public RectTransform transform = new RectTransform { parent = new RectTransform() };
        public Selectable[] controls;
        public bool active = true;
        public void SetActive(bool value) { active = value; }
        public T[] GetComponentsInChildren<T>() { return controls.Cast<T>().ToArray(); }
    }
    private sealed class Text { public string text = ""; }
    private sealed class TMP_Text { public string text = ""; }

    private sealed class GameObject
    {
        public bool activeSelf = false;
        public bool activeInHierarchy => activeSelf;
        public void SetActive(bool value) { activeSelf = value; }
        public static implicit operator bool(GameObject value) { return value != null; }
    }
    private sealed class PlanReview { public List<object> Blockers = new List<object>(); public void CancelWrite() { } }
    private sealed class Skin { public void Dispose() { } }
    private static class UnityEngine { public static class Object { public static void Destroy(object value) { } } }
    private sealed class EventSystem
    {
        public static EventSystem current = null;
        public static implicit operator bool(EventSystem value) { return value != null; }
        public void SetSelectedGameObject(GameObject value) { }
    }
    private sealed class Input { public GameObject gameObject = new GameObject(); public string text = ""; }
    private static class CompositeBlueprintStore
    {
        internal sealed class Blueprint { public string importSource = "result.zip", importError = null, name = "Document", category = "OTHER"; public List<int> parts = new List<int> { 1 }; }
        internal sealed class ContentsSummary { public int NativeArmorStates = 0, Mounts = 0; }
        internal sealed class MissingPartsReview { public bool CanOmit = true; public int EmptyDocuments = 0; }
        internal sealed class ImportReview
        {
            public List<Blueprint> Documents = new List<Blueprint>();
            public List<(string Source, string Error)> FileErrors;
            public Func<bool> IsHostCurrent;
            public ContentsSummary Contents = new ContentsSummary();
            public MissingPartsReview Missing = new MissingPartsReview();
            public string Source;
            public byte[] Bytes;
            public object Sources;
        }
        // PRODUCTION_CATEGORY_CONSTANTS
        // PRODUCTION_CATEGORY_NORMALIZATION
    }
    private sealed class FakeStore
    {
        public int Revision = 0, Generation = 0, Calls = 0, PrepareCalls = 0;
        public bool CatalogLoading = true;
        public string ImportError = null, ImportResultPath = "result.zip", ImportOperationPath = "result.zip";
        public int Writes;
        public Exception CatalogException = null;
        public CompositeBlueprintStore.Blueprint[] Catalog = new[] { new CompositeBlueprintStore.Blueprint() };
        public bool TryImportReviewed(CompositeBlueprintStore.ImportReview review, bool storage, bool mounts,
            Func<string, int> hash, out string error, bool armor, CompositeBlueprintStore.MissingPartsReview missing)
        { error = null; ++Writes; return true; }
        public TaskCompletionSource<CompositeBlueprintStore.ImportReview> preparation =
            new TaskCompletionSource<CompositeBlueprintStore.ImportReview>();
        public CompositeBlueprintStore.Blueprint[] CatalogAll()
        {
            if (CatalogException != null) throw CatalogException;
            if (++Calls == 1) Generation++;
            if (Calls >= 2) CatalogLoading = false;
            return Catalog;
        }
        public Task<CompositeBlueprintStore.ImportReview> PrepareImportReview(
            CompositeBlueprintStore.ImportReview review, Func<string, int> hash)
        {
            PrepareCalls++;
            PreparedInput = review;
            if (PreparationException != null) throw PreparationException;
            return preparation.Task;
        }
        public CompositeBlueprintStore.ImportReview PreparedInput;
        public Exception PreparationException;
    }
    private static class BuildWorksLocalization
    {
        public static string ResolveUserText(string text) { return text; }
        public static string BlueprintCategoryLabel(string text) { return text; }
    }
    private readonly FakeStore store = new FakeStore();
    private readonly Root root = new Root();
    private readonly RectTransform panel = new RectTransform();
    private readonly Text message = new Text();
    private readonly Input directory = new Input();
    private readonly Selectable primary = new Selectable { name = "ExchangeSubmit" };
    private readonly Selectable copyPath = new Selectable { name = "ExchangeCopyPath" };
    private readonly Selectable contentsBack = new Selectable { name = "ExchangeContentsBack" };
    private Func<CompositeBlueprintStore.Blueprint, string> place = null;
    private Func<bool> placementCurrent = null;
    private int placementRevision = 0;
    private Task<CompositeBlueprintStore.Blueprint> placementPreparation = null;
    private Task<string[]> listing = null;
    private Task<CompositeBlueprintStore.ContentsSummary> contentsInspection = null;
    private Task<CompositeBlueprintStore.ImportReview> importInspection = null, importPreparation = null;
    private Task<PlanReview> planInspection = null;
    private Task<PlanReview> openKeepInspection = null, kgInspection = null, bbpInspection = null, plannerInspection = null, unboundInspection = null;
    private PlanReview openKeepReview = null, kgReview = null, bbpReview = null, plannerReview = null, unboundReview = null;
    private int? openKeepTarget = null, bbpTarget = null;
    private bool kgFormat = false, plannerFormat = false, unboundFormat = false;
    private Task<string> planExport = null;
    private CompositeBlueprintStore.Blueprint blueprint = null;
    private CompositeBlueprintStore.ImportReview importReview = null;
    private CompositeBlueprintStore.ContentsSummary contentsSummary = null;
    private CompositeBlueprintStore.MissingPartsReview missingReview = null;
    private bool sharing = false, importing = false, completed = false, reviewingContents = false, planBuild = false, omitMissingParts = false;
    private bool catalogRetry = false;

    private string importOperationPath = "result.zip";
    private Action<CompositeBlueprintStore.Blueprint[]> importCompleted;
    private bool IsOpen => root.active && !disposed;
    private bool disposed;
    private CancellationTokenSource batchCancellation;
    private GameObject previousFocus = null;
    private Skin skin = new Skin();
    private Action finished;
    private RectTransform contentsPanel = new RectTransform();
    private string selected = "input.blueprint", resultPath = null, result = null;
    private string[] entries = Array.Empty<string>();
    private PlanReview planReview = null;
    private int renderedRows = 0, renderedContents = 0, shownResults = 0, refreshedMissing = 0;
    private ExchangeTickChecks()
    {
        root.controls = new[] { primary, copyPath, contentsBack, new Selectable { name = "ExchangeClose" } };
    }
    private static string T(string key, params object[] args) { return key + string.Concat(args); }
    private void CloseCategoryPicker() { }
    private void SetFileControls(bool value) { }
    private void DisposeServerOperation() { throw new InvalidOperationException("Server mode is outside this harness"); }
    private void SetError(Exception error) { message.text = error.Message; }
    private void RenderRows() { renderedRows++; }
    private bool batchMode = false;
    private bool serverMode = false;
    private void TickServer() { throw new InvalidOperationException("Server mode is outside this harness"); }
    private CompositeBlueprintStore.Blueprint[] batchExported;
    private CompositeBlueprintStore.Blueprint[] batchImported;
    private CompositeBlueprintStore.ImportReview batchRawReview;
    private CompositeBlueprintStore.Blueprint[] batchAllDocuments;
    private readonly HashSet<string> batchIncluded = new HashSet<string>();
    private readonly Dictionary<string, string> batchCategories = new Dictionary<string, string>();
    private readonly Input categoryInput = new Input();
    private bool categoryOverride;
    private readonly Text contentsDetails = new Text();
    private Selectable batchDocumentsButton;
    private RectTransform batchPicker;
    private Selectable Button(string name, string text, float x, float y, float width, Action action, RectTransform parent)
        => new Selectable { name = name };
    private void OpenBatchDocuments() => throw new InvalidOperationException("Picker rendering is outside this CPU harness");
    private void TickBatch() { }
    private void UpdateBatchControls(bool busy) { }
    private void RenderContents()
    {
        renderedContents++;
        if (batchMode && importReview != null)
        { contentsDetails.text = "mounts:" + contentsSummary?.Mounts; AppendBatchReview(); }
    }
    private void RefreshMissing() { refreshedMissing++; missingReview = new CompositeBlueprintStore.MissingPartsReview(); }
    private void RenderPlanReview() { }
    // Foreign review rendering belongs to engine acceptance, never a successful CPU substitute.
    private void RenderOpenKeepReview() => throw new InvalidOperationException("Foreign review rendering is outside this harness");
    private void RenderKgReview() => throw new InvalidOperationException("Foreign review rendering is outside this harness");
    private void RenderBbpReview() => throw new InvalidOperationException("Foreign review rendering is outside this harness");
    private void RenderPlannerReview() => throw new InvalidOperationException("Foreign review rendering is outside this harness");
    private void RenderUnboundReview() => throw new InvalidOperationException("Foreign review rendering is outside this harness");
    private void ShowResult(string text) { shownResults++; result = text; }
    private void UpdateImportProgress() { }
    // PRODUCTION_CATEGORY_VALIDITY
    // PRODUCTION_PREPARE_DOCUMENTS
    private void UpdateCategoryControls(bool busy) { }
    private void ShowSavedCatalogFailure(string error)
    { completed = catalogRetry = true; resultPath = store.ImportResultPath; ShowResult("saved: " + error); }
    private static void Assert(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    // PRODUCTION_METHOD
    // PRODUCTION_BATCH_SUMMARY
    // PRODUCTION_BATCH_CATEGORIES
    // PRODUCTION_BATCH_PREVIEW
    private void ApplyBatchDocuments()
    // PRODUCTION_BATCH_APPLY
    // PRODUCTION_DISPOSE
    private void Back()
    // PRODUCTION_BACK

    public static void Run()
    {
        foreach (bool preparing in new[] { false, true })
        foreach (bool close in new[] { false, true })
        foreach (bool fail in new[] { false, true })
        {
            var late = new TaskCompletionSource<CompositeBlueprintStore.ImportReview>();
            var operation = new ExchangeTickChecks { reviewingContents = true };
            int completions = 0;
            operation.importCompleted = _ => ++completions;
            if (preparing) operation.importPreparation = late.Task; else operation.importInspection = late.Task;
            if (close) operation.Dispose(); else operation.Back();
            if (fail) { late.SetException(new IOException("late failure")); _ = late.Task.Exception; }
            else late.SetResult(new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => true });
            operation.Tick();
            Assert(operation.importInspection == null && operation.importPreparation == null && operation.store.PrepareCalls == 0 &&
                operation.renderedContents == 0 && completions == 0 && operation.shownResults == 0 && operation.store.Writes == 0,
                "Async inspection/preparation after Back/Dispose affected the closed review");
        }
        var closingImport = new ExchangeTickChecks { importing = true, batchCancellation = new CancellationTokenSource() };
        var token = closingImport.batchCancellation.Token;
        int lateCallbacks = 0, closes = 0;
        closingImport.importCompleted = _ => ++lateCallbacks; closingImport.finished = () => ++closes;
        closingImport.Dispose(); closingImport.Dispose(); closingImport.Tick();
        Assert(token.IsCancellationRequested && closes == 1 && lateCallbacks == 0 && closingImport.store.Calls == 0,
            "Closing an in-flight batch did not cancel exactly once or consumed a late result");
        var receipt = new ExchangeTickChecks { batchImported = new[] { new CompositeBlueprintStore.Blueprint() } };
        int receiptCallbacks = 0;
        receipt.finished = () => Assert(!receipt.IsOpen && receiptCallbacks == 0, "Close order changed");
        receipt.importCompleted = _ => { Assert(!receipt.IsOpen && receipt.batchImported == null, "Receipt sent before cleanup"); ++receiptCallbacks; };
        receipt.Dispose(); receipt.Dispose();
        Assert(receiptCallbacks == 1, "Completed batch receipt was duplicated or lost on Dispose");
        Console.WriteLine("PASS actual Back/Dispose/Tick: late inspect/prepare ignored, cancellation once, no late navigation; completed receipt once after cleanup");
        var batchImport = new ExchangeTickChecks { batchMode = true, importing = true, reviewingContents = true,
            importReview = new CompositeBlueprintStore.ImportReview { Documents = new List<CompositeBlueprintStore.Blueprint> {
                new CompositeBlueprintStore.Blueprint { name = "Same name", importSource = "a.blueprint" },
                new CompositeBlueprintStore.Blueprint { name = "Same name", importSource = "pack.zip/sub/b.blueprint" } } },
            batchRawReview = new CompositeBlueprintStore.ImportReview { FileErrors = new List<(string, string)> { ("private/bad.blueprint", "bad format") } } };
        batchImport.store.Catalog = new[] {
            new CompositeBlueprintStore.Blueprint { name = "Same name", category = "SECOND", importSource = "result.zip/document_1.buildworks.json" },
            new CompositeBlueprintStore.Blueprint { name = "Same name", category = "FIRST", importSource = "result.zip/document_0.buildworks.json" } };
        int importCallbacks = 0;
        batchImport.importCompleted = _ => ++importCallbacks;
        batchImport.Tick(); batchImport.Tick(); batchImport.Tick();
        Assert(batchImport.completed && batchImport.shownResults == 1 && batchImport.result.Contains("a.blueprint") &&
            batchImport.result.Contains("pack.zip/sub/b.blueprint") && batchImport.result.Contains("bad.blueprint") &&
            !batchImport.result.Contains("private/") && batchImport.result.Contains("FIRST") && batchImport.result.Contains("SECOND"),
            "Batch import result lost input-to-document mapping or skipped-file errors");
        Assert(batchImport.result.Contains(T("exchange.batch_imported", "a.blueprint", "Same name", "FIRST")) &&
            batchImport.result.Contains(T("exchange.batch_imported", "pack.zip/sub/b.blueprint", "Same name", "SECOND")),
            "Reordered catalog paired the wrong input file and document category");
        Assert(batchImport.IsOpen && importCallbacks == 0 && batchImport.batchImported.SequenceEqual(batchImport.store.Catalog),
            "Batch result changed the catalog navigation receipt or auto-closed before review");
        batchImport.importReview.Documents[0].importSource = @"C:\private\input.blueprint";
        string windows = batchImport.BatchResultSummary(batchImport.store.Catalog);
        Assert(windows.Contains("input.blueprint") && !windows.Contains(@"C:\private"), "Batch result leaked absolute Windows input path");
        foreach (string source in new[] { "result.zip/document_2.buildworks.json", "other.zip/document_0.buildworks.json",
            "result.zip/document_-1.buildworks.json", "result.zip/document_x.buildworks.json", "result.zip/sub/document_0.buildworks.json", null })
        {
            var unknown = new CompositeBlueprintStore.Blueprint { name = "Unknown", category = "OLD", importSource = source };
            string summary = batchImport.BatchResultSummary(new[] { unknown });
            Assert(summary.StartsWith("\n" + T("exchange.batch_done", "Unknown", "OLD")) && !summary.Contains("exchange.batch_imported"),
                "Unknown/foreign archive entry was attributed to a selected input");
        }
        batchImport.importReview = null;
        Assert(batchImport.BatchResultSummary(batchImport.store.Catalog).Contains(T("exchange.batch_done", "Same name", "FIRST")),
            "Older receipt without a retained review did not keep its document result");
        Console.WriteLine("PASS actual batch import Tick/formatter: exact entry mapping, reverse catalog, same names, nested ZIP, skipped errors, Windows path, unmatched entry and old receipt");
        var batchExport = new ExchangeTickChecks { batchMode = true, planExport = Task.FromResult("group.zip"),
            batchExported = new[] {
                new CompositeBlueprintStore.Blueprint { name = "House", category = "HOMES" },
                new CompositeBlueprintStore.Blueprint { name = "Tower", category = "TOWERS" } } };
        batchExport.Tick(); batchExport.Tick();
        Assert(batchExport.completed && batchExport.shownResults == 1 && batchExport.planExport == null &&
            batchExport.result.Contains("group.zip") && batchExport.result.Contains("document_0.buildworks.jsonHouseHOMES") &&
            batchExport.result.Contains("document_1.buildworks.jsonTowerTOWERS"), "Batch export lost its per-entry result or showed it twice");
        var singleExport = new ExchangeTickChecks { planExport = Task.FromResult("single.blueprint"), batchExported = batchExport.batchExported };
        singleExport.Tick();
        Assert(singleExport.result == "exchange.exportedsingle.blueprint", "Single export gained a stale batch receipt");
        foreach (Exception error in new Exception[] { new IOException("write refused"), new OperationCanceledException("cancelled") })
        {
            var failedExport = new ExchangeTickChecks { batchMode = true, batchExported = batchExport.batchExported,
                planExport = Task.FromException<string>(error) };
            failedExport.Tick();
            Assert(!failedExport.completed && failedExport.shownResults == 0 && failedExport.message.text == error.Message &&
                failedExport.planExport == null, "Failed/cancelled batch export claimed published entries");
        }
        Console.WriteLine("PASS actual exchange Tick and batch summary: ZIP entries/names/categories, single export unchanged, no success on failure/cancel");
        var success = new ExchangeTickChecks { importing = true, reviewingContents = true };
        success.importReview = new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => success.store.Generation == 0 };
        success.Tick();
        Assert(success.importing && success.store.Calls == 1, "Import must wait for its catalogue preparation");
        success.Tick();
        success.Tick();
        Assert(success.completed && !success.importing && success.shownResults == 1 && success.store.Calls == 3 &&
            success.resultPath == "result.zip" && success.result.Contains("exchange.imported"),
            "Own import generation change must reach ShowResult exactly once");

        var importFailure = new ExchangeTickChecks { importing = true, reviewingContents = true,
            contentsSummary = new CompositeBlueprintStore.ContentsSummary(), missingReview = new CompositeBlueprintStore.MissingPartsReview() };
        int failureCallbacks = 0;
        importFailure.importCompleted = _ => ++failureCallbacks;
        importFailure.store.ImportError = "normalization failed";
        importFailure.store.ImportResultPath = null;
        importFailure.importReview = new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => importFailure.store.Generation == 0 };
        importFailure.Tick();
        importFailure.Tick();
        importFailure.Tick();
        Assert(!importFailure.importing && !importFailure.completed && !importFailure.primary.interactable &&
            importFailure.contentsBack.interactable && importFailure.message.text == "normalization failed" &&
            importFailure.shownResults == 0 && failureCallbacks == 0, "Import failure must remain visible and require a fresh review via Back");

        var back = new ExchangeTickChecks { listing = Task.FromResult(new[] { "next.blueprint" }) };
        back.importReview = new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => false };
        back.Tick();
        Assert(back.listing == null && back.renderedRows == 1 && back.entries.Length == 1 && back.primary.interactable &&
            back.message.text != "placement.contents_stale", "Retained review after Back must not lock the browser");

        var stale = new ExchangeTickChecks { reviewingContents = true };
        stale.importReview = new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => false };
        stale.Tick();
        Assert(stale.message.text == "placement.contents_stale" && !stale.primary.interactable &&
            stale.contentsBack.interactable && stale.root.controls.Last().interactable && stale.store.Calls == 0 && stale.shownResults == 0,
            "Stale review must block confirmation and allow Back/Close");

        var pending = new ExchangeTickChecks { reviewingContents = true,
            importInspection = Task.FromResult(new CompositeBlueprintStore.ImportReview()) };
        pending.Tick();
        pending.Tick();
        Assert(pending.store.PrepareCalls == 1 && pending.importPreparation != null && pending.contentsSummary == null &&
            !pending.primary.interactable && pending.contentsBack.interactable, "Both import stages must finish before confirmation");
        var prepared = new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => true };
        pending.store.preparation.SetResult(prepared);
        pending.Tick();
        Assert(pending.importPreparation == null && pending.contentsSummary != null && pending.renderedContents == 1 &&
            pending.primary.interactable && ReferenceEquals(pending.missingReview, prepared.Missing) && pending.refreshedMissing == 0,
            "Prepared missing snapshot must publish once without a host contents refresh");

        var changed = new ExchangeTickChecks { reviewingContents = true,
            importPreparation = Task.FromResult(new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => false }) };
        changed.Tick();
        Assert(changed.importPreparation == null && changed.contentsSummary == null && !changed.primary.interactable &&
            changed.contentsBack.interactable && changed.message.text == "placement.contents_stale",
            "Stale worker result must not publish or confirm");
        var failed = new ExchangeTickChecks { reviewingContents = true,
            importPreparation = Task.FromException<CompositeBlueprintStore.ImportReview>(new IOException("worker failure")) };
        failed.Tick();
        Assert(failed.message.text == "worker failure" && failed.importPreparation == null && !failed.primary.interactable &&
            failed.contentsBack.interactable, "Worker failure must retain navigation and block confirmation");
        var empty = new ExchangeTickChecks { reviewingContents = true,
            contentsSummary = new CompositeBlueprintStore.ContentsSummary(),
            missingReview = new CompositeBlueprintStore.MissingPartsReview { CanOmit = false, EmptyDocuments = 1 } };
        empty.Tick();
        Assert(!empty.primary.interactable && empty.contentsBack.interactable && empty.root.controls.Last().interactable,
            "All-missing import must block confirmation while preserving Back/Close");
        empty.blueprint = new CompositeBlueprintStore.Blueprint();
        empty.Tick();
        Assert(empty.primary.interactable, "Existing all-missing blueprint must remain exportable for backup");
        Console.WriteLine("PASS actual exchange Tick: own import to result/failure, browser after Back, stale confirmation, two stages, worker failure and all-missing import/export boundary");
        int callbacks = 0;
        var automatic = new ExchangeTickChecks { importing = true, reviewingContents = true };
        automatic.importCompleted = items => { Assert(!automatic.IsOpen && items.Length == 1, "Callback ran before close"); ++callbacks; };
        automatic.Tick(); automatic.Tick(); automatic.Tick();
        Assert(callbacks == 1 && automatic.shownResults == 0 && !automatic.IsOpen, "Success did not auto-close exactly once");
        var saved = new ExchangeTickChecks { importing = true, reviewingContents = true };
        saved.store.ImportError = "catalogue failed after save";
        saved.Tick(); saved.Tick();
        Assert(saved.completed && saved.catalogRetry && saved.primary.interactable && !saved.contentsBack.interactable &&
            saved.resultPath == "result.zip" && saved.store.Writes == 0,
            "Post-commit failure must offer catalogue refresh without another import");
        var snapshotFailure = new ExchangeTickChecks { importing = true, reviewingContents = true };
        snapshotFailure.store.CatalogException = new IOException("host snapshot failed");
        snapshotFailure.Tick();
        Assert(snapshotFailure.completed && snapshotFailure.catalogRetry && snapshotFailure.shownResults == 1 &&
            !snapshotFailure.importing && snapshotFailure.store.Writes == 0,
            "Main-thread catalogue snapshot error escaped or offered another write after commit");
        var replaced = new ExchangeTickChecks { importing = true, reviewingContents = true };
        replaced.store.ImportOperationPath = "another.zip";
        replaced.Tick();
        Assert(replaced.completed && replaced.store.Calls == 0 && replaced.message.text == "exchange.operation_changed",
            "Old view consumed another operation's result");
        Console.WriteLine("PASS actual exchange Tick: success callback after Dispose once, committed refresh error and operation replacement.");
        var scrolling = new ExchangeTickChecks { importing = true };
        scrolling.store.CatalogLoading = true;
        var scrollbar = new Scrollbar { name = "ExchangeContentsScrollbar" };
        scrolling.root.controls = scrolling.root.controls.Concat(new Selectable[] { scrollbar }).ToArray();
        scrolling.Tick();
        Assert(scrollbar.interactable && !scrolling.primary.interactable, "Busy import blocked read-only report scrolling or enabled writes");
        FilteredBatchPreview();
        PreparationFailures();
    }

    private static void PreparationFailures()
    {
        ExchangeTickChecks Fresh()
        {
            var document = new CompositeBlueprintStore.Blueprint { name = "A" };
            var raw = new CompositeBlueprintStore.ImportReview { Documents = new List<CompositeBlueprintStore.Blueprint> { document } };
            var check = new ExchangeTickChecks { batchMode = true, reviewingContents = true, batchAllDocuments = new[] { document },
                batchRawReview = raw, batchPicker = new RectTransform(),
                contentsSummary = new CompositeBlueprintStore.ContentsSummary { Mounts = 7 },
                importReview = new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => true },
                missingReview = new CompositeBlueprintStore.MissingPartsReview() };
            check.batchIncluded.Add("0"); check.contentsDetails.text = "obsolete warning";
            return check;
        }
        var sync = Fresh();
        sync.importPreparation = Task.FromResult(new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => true });
        sync.store.PreparationException = new InvalidOperationException("profile snapshot unavailable");
        bool escaped = false;
        try { sync.ApplyBatchDocuments(); } catch (InvalidOperationException) { escaped = true; }
        Assert(!escaped, "Synchronous batch preparation exception escaped instead of a visible recoverable error");
        sync.Tick();
        Assert(sync.message.text == "profile snapshot unavailable" && sync.importPreparation == null && sync.importReview == null &&
            sync.contentsSummary == null && sync.missingReview == null && !sync.primary.interactable && sync.contentsBack.interactable &&
            sync.contentsDetails.text == "" && sync.resultPath == null && sync.store.Writes == 0 && sync.shownResults == 0,
            "Failed preparation retained stale approval/task or lost the actual cause");
        sync.Back();
        Assert(!sync.reviewingContents && sync.message.text == "" && sync.IsOpen, "Back could not recover after synchronous preparation failure");
        var asyncFailure = Fresh(); asyncFailure.ApplyBatchDocuments();
        asyncFailure.store.preparation.SetException(new IOException("worker unavailable")); asyncFailure.Tick(); asyncFailure.Tick();
        Assert(asyncFailure.message.text == "worker unavailable" && asyncFailure.importPreparation == null && !asyncFailure.primary.interactable &&
            asyncFailure.store.Writes == 0 && asyncFailure.shownResults == 0, "Async preparation error lost cause or claimed publication");
        var cancelled = Fresh(); cancelled.ApplyBatchDocuments(); cancelled.store.preparation.SetCanceled(); cancelled.Tick();
        Assert(cancelled.importPreparation == null && !cancelled.primary.interactable && cancelled.store.Writes == 0 &&
            cancelled.shownResults == 0 && cancelled.resultPath == null, "Cancelled preparation left confirmation or success enabled");
        var late = Fresh(); late.ApplyBatchDocuments(); late.Back();
        late.store.preparation.SetResult(new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => true }); late.Tick();
        Assert(!late.reviewingContents && late.importReview == null && late.store.Writes == 0 && late.shownResults == 0,
            "Late preparation after Back restored a discarded review or wrote files");
        Console.WriteLine("PASS preparation failure/recovery: synchronous cause, stale task refusal, async/cancel, Back and late completion; host UI/provider substituted, no writes.");
    }

    private static void FilteredBatchPreview()
    {
        var a = new CompositeBlueprintStore.Blueprint { name = "A", category = "A ORIGINAL" };
        var b = new CompositeBlueprintStore.Blueprint { name = "Excluded B", category = "B ORIGINAL" };
        var c = new CompositeBlueprintStore.Blueprint { name = "C", category = "C ORIGINAL" };
        var raw = new CompositeBlueprintStore.ImportReview { Source = "group.zip", Bytes = new byte[] { 1, 2 }, Sources = new object(),
            Documents = new List<CompositeBlueprintStore.Blueprint> { a, b, c }, FileErrors = new List<(string, string)> { ("private/bad.blueprint", "bad file") } };
        var check = new ExchangeTickChecks { batchMode = true, reviewingContents = true, categoryOverride = true,
            batchAllDocuments = raw.Documents.ToArray(), batchRawReview = raw, importReview = raw,
            contentsSummary = new CompositeBlueprintStore.ContentsSummary { Mounts = 1 }, missingReview = new CompositeBlueprintStore.MissingPartsReview(),
            batchPicker = new RectTransform() };
        check.categoryInput.text = " common "; check.batchIncluded.UnionWith(new[] { "0", "2" });
        check.batchCategories["1"] = "<invalid excluded>"; check.batchCategories["2"] = " custom c ";
        check.contentsDetails.text = "old B mount warning"; check.batchPicker.gameObject.SetActive(true);
        check.ApplyBatchDocuments();
        bool pendingCleared = check.contentsDetails.text == "" && check.message.text == T("exchange.loading");
        Assert(check.store.PrepareCalls == 1 && check.store.PreparedInput.Documents.SequenceEqual(new[] { a, c }) &&
            ReferenceEquals(check.store.PreparedInput.Bytes, raw.Bytes) && ReferenceEquals(check.store.PreparedInput.Sources, raw.Sources) &&
            ReferenceEquals(check.store.PreparedInput.FileErrors, raw.FileErrors) && check.contentsSummary == null &&
            check.importReview == null && check.missingReview == null && !check.primary.interactable,
            "Applying batch selection lost selected order/source evidence or retained stale confirmation");
        check.store.preparation.SetResult(new CompositeBlueprintStore.ImportReview {
            Documents = new List<CompositeBlueprintStore.Blueprint> { a, c }, IsHostCurrent = () => true,
            Contents = new CompositeBlueprintStore.ContentsSummary { Mounts = 0 } });
        check.Tick();
        bool commonNormalized = check.contentsDetails.text.Contains(T("exchange.batch_document", "A", 1, "COMMON"));
        bool overrideNormalized = check.BatchCategoryOverrides().Count == 1 && check.BatchCategoryOverrides()[1] == "CUSTOM C" &&
            check.contentsDetails.text.Contains(T("exchange.batch_document", "C", 1, "CUSTOM C"));
        Assert(pendingCleared && commonNormalized && overrideNormalized,
            "Batch review retained stale details or unnormalized accepted categories: pendingCleared=" + pendingCleared +
            ", commonNormalized=" + commonNormalized + ", overrideNormalized=" + overrideNormalized);
        Assert(check.primary.interactable && check.contentsDetails.text.Contains("mounts:0") &&
            !check.contentsDetails.text.Contains("Excluded B") && !check.contentsDetails.text.Contains("old B mount warning") &&
            check.contentsDetails.text.Contains("bad.blueprint") && !check.contentsDetails.text.Contains("private/"),
            "Filtered batch preview retained an excluded document/warning or lost skipped-file disclosure");
        check.batchCategories["2"] = null; check.RenderContents();
        Assert(check.BatchCategoryOverrides().ContainsKey(1) && check.BatchCategoryOverrides()[1] == null &&
            check.contentsDetails.text.Contains(T("exchange.batch_document", "C", 1, "C ORIGINAL")), "Explicit original category became common category");
        check.batchCategories["2"] = "<invalid selected>";
        Assert(!check.ImportCategoryIsValid() && check.BatchCategoryOverrides()[1] == "<invalid selected>", "Invalid selected category was silently dropped");
        check.batchCategories["2"] = "custom c";
        Assert(check.ImportCategoryIsValid(), "Excluded invalid category blocked the selected group");
        check.batchIncluded.Add("1"); Assert(!check.ImportCategoryIsValid(), "Included invalid category allowed confirmation");
        check.batchIncluded.Remove("1"); check.batchIncluded.Remove("0");
        Assert(check.BatchCategoryOverrides().Single().Key == 0 && check.BatchCategoryOverrides()[0] == "CUSTOM C", "Filtered category indices were not remapped");
        Console.WriteLine("PASS actual batch picker Apply/category mapping/preview: A-C filter, pending clear, canonical/common/original categories, excluded/invalid guards and source evidence; host UI/preparation substituted.");
    }
}
internal static class StableHashStub
{
    internal static int GetStableHashCode(this string text) { return 0; }
}
