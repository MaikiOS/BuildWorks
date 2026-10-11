using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Executes the actual Tick method with CPU substitutes for UI, tasks and the store.
public sealed class ExchangeTickChecks
{
    private sealed class Selectable
    {
        public string name;
        public bool interactable = true;
        public static implicit operator bool(Selectable value) { return value != null; }
    }
    private sealed class Vector3
    {
        public static readonly Vector3 one = new Vector3();
        public static Vector3 operator *(Vector3 value, float scale) { return value; }
    }
    private sealed class Rect { public float width = 1920, height = 1080; }
    private sealed class RectTransform
    {
        public RectTransform parent;
        public Rect rect = new Rect();
        public Vector3 localScale = Vector3.one;
    }
    private static class Mathf { public static float Min(params float[] values) { return values.Min(); } }
    private sealed class Root
    {
        public RectTransform transform = new RectTransform { parent = new RectTransform() };
        public Selectable[] controls;
        public T[] GetComponentsInChildren<T>() { return controls.Cast<T>().ToArray(); }
    }
    private sealed class Text { public string text = ""; }
    private sealed class GameObject { public bool activeSelf = false; }
    private sealed class Input { public GameObject gameObject = new GameObject(); }
    private static class CompositeBlueprintStore
    {
        internal sealed class Blueprint { public string importSource = "result.zip", importError = null; }
        internal sealed class ContentsSummary { }
        internal sealed class MissingPartsReview { public bool CanOmit = true; }
        internal sealed class ImportReview
        {
            public Func<bool> IsHostCurrent;
            public ContentsSummary Contents = new ContentsSummary();
        }
    }
    private sealed class FakeStore
    {
        public int Revision = 0, Generation = 0, Calls = 0, PrepareCalls = 0;
        public bool CatalogLoading = true;
        public string ImportError = null, ImportResultPath = "result.zip";
        public TaskCompletionSource<CompositeBlueprintStore.ImportReview> preparation =
            new TaskCompletionSource<CompositeBlueprintStore.ImportReview>();
        public CompositeBlueprintStore.Blueprint[] CatalogAll()
        {
            if (++Calls == 1) Generation++;
            if (Calls >= 2) CatalogLoading = false;
            return new[] { new CompositeBlueprintStore.Blueprint() };
        }
        public Task<CompositeBlueprintStore.ImportReview> PrepareImportReview(
            CompositeBlueprintStore.ImportReview review, Func<string, int> hash)
        {
            PrepareCalls++;
            return preparation.Task;
        }
    }
    private static class BuildWorksLocalization { public static string ResolveUserText(string text) { return text; } }
    private readonly FakeStore store = new FakeStore();
    private readonly Root root = new Root();
    private readonly RectTransform panel = new RectTransform();
    private readonly Text message = new Text();
    private readonly Input directory = new Input();
    private readonly Selectable primary = new Selectable { name = "ExchangePrimary" };
    private readonly Selectable copyPath = new Selectable { name = "ExchangeCopyPath" };
    private readonly Selectable contentsBack = new Selectable { name = "ExchangeContentsBack" };
    private Func<CompositeBlueprintStore.Blueprint, string> place = null;
    private Func<bool> placementCurrent = null;
    private int placementRevision = 0;
    private Task<CompositeBlueprintStore.Blueprint> placementPreparation = null;
    private Task<string[]> listing = null;
    private Task<CompositeBlueprintStore.ContentsSummary> contentsInspection = null;
    private Task<CompositeBlueprintStore.ImportReview> importInspection = null, importPreparation = null;
    private Task<object> planInspection = null;
    private Task<string> planExport = null;
    private CompositeBlueprintStore.Blueprint blueprint = null;
    private CompositeBlueprintStore.ImportReview importReview = null;
    private CompositeBlueprintStore.ContentsSummary contentsSummary = null;
    private CompositeBlueprintStore.MissingPartsReview missingReview = null;
    private bool importing = false, completed = false, reviewingContents = false, planBuild = false, omitMissingParts = false;
    private bool IsOpen = true;
    private string selected = "input.blueprint", resultPath = null, result = null;
    private string[] entries = Array.Empty<string>();
    private object planReview = null;
    private int renderedRows = 0, renderedContents = 0, shownResults = 0;
    private ExchangeTickChecks()
    {
        root.controls = new[] { primary, copyPath, contentsBack, new Selectable { name = "ExchangeClose" } };
    }
    private static string T(string key, params object[] args) { return key + string.Concat(args); }
    private void Dispose() { IsOpen = false; }
    private void SetError(Exception error) { message.text = error.Message; }
    private void RenderRows() { renderedRows++; }
    private void RenderContents() { renderedContents++; }
    private void RefreshMissing() { missingReview = new CompositeBlueprintStore.MissingPartsReview(); }
    private void RenderPlanReview() { }
    private void ShowResult(string text) { shownResults++; result = text; }
    private static void Assert(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    // PRODUCTION_METHOD

    public static void Run()
    {
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
        importFailure.store.ImportError = "normalization failed";
        importFailure.importReview = new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => importFailure.store.Generation == 0 };
        importFailure.Tick();
        importFailure.Tick();
        importFailure.Tick();
        Assert(!importFailure.importing && !importFailure.completed && !importFailure.primary.interactable &&
            importFailure.contentsBack.interactable && importFailure.message.text == "normalization failed" &&
            importFailure.shownResults == 0, "Import failure must remain visible and require a fresh review via Back");

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
        pending.store.preparation.SetResult(new CompositeBlueprintStore.ImportReview { IsHostCurrent = () => true });
        pending.Tick();
        Assert(pending.importPreparation == null && pending.contentsSummary != null && pending.renderedContents == 1 &&
            pending.primary.interactable, "Current prepared review must publish once and enable confirmation");

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
        Console.WriteLine("PASS actual exchange Tick: own import to result/failure, browser after Back, stale confirmation, two stages and worker failure");
    }
}
internal static class StableHashStub
{
    internal static int GetStableHashCode(this string text) { return 0; }
}
