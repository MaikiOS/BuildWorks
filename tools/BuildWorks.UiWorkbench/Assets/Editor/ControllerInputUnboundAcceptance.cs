using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static partial class ControllerInputAcceptance
{
    internal static IEnumerable<object> RunUnboundExports(Camera template, TMP_Text font, string output)
    {
        string working = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/unbound-ui", Guid.NewGuid().ToString("N")));
        var store = new CompositeBlueprintStore(Path.Combine(working, "library.json"));
        var source = new CompositeBlueprintStore.Blueprint { id = Guid.NewGuid().ToString("N"), name = "Unbound UI", prefabName = "sign" };
        source.parts.Add(new CompositeBlueprintStore.Part { stableId = "ui-sign", prefabName = "sign", displayName = "Sign",
            position = new CompositeBlueprintStore.VectorData { x = 1.25f }, rotation = new CompositeBlueprintStore.QuaternionData { w = 1 },
            scale = new CompositeBlueprintStore.VectorData { x = 1.5f, y = 1, z = 1 }, signText = "UI Привет", materialSeed = 17 });
        byte[] original = BlueprintExchangeFormat.Encode(source);
        var profile = new UnboundImportContext(new BlueprintPrefabMetadata(1, "sign", "Sign", "Piece", "synthetic-workbench", new Point3(1, 1, 1)),
            true, true, true, true, nativeHeader: ("sign".GetStableHashCode(), true, false, (byte)0));
        bool missingProfile = false;
        store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, unboundContext: _ => missingProfile ? null : profile);
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.SetActive(false); var input = new InputFrames();
        try
        {
            using (var controller = new BlueprintEditorController(store, template, font, _ => prefab, id => id,
                () => Array.Empty<BlueprintEditorCatalogItem>(), text => { throw new InvalidOperationException(text); }, editorInput: input))
            {
                Require(controller.OpenNew(out string error), error); var view = Field<BlueprintEditorView>(controller, "view");
                for (int mode = 0; mode < 4; ++mode)
                {
                    bool russian = mode % 2 == 1, blocked = mode >= 2; BuildWorksLocalization.RussianLanguage = russian; missingProfile = blocked;
                    view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, source, closed)); yield return null;
                    var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                    for (int frame = 0; frame < 600 && Field<Task<string[]>>(exchange, "listing") != null; ++frame)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    string name = "unbound-" + (blocked ? "blocked-" : "") + (russian ? "ru" : "en"), destination = Path.Combine(store.ExportDirectory, name);
                    Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify(name);
                    for (int i = 0; i < 8; ++i) NativeClick(view, null, "ExchangeFormat");
                    Require(Field<bool>(exchange, "plannerFormat") && !Field<bool>(exchange, "unboundFormat"), "Old Planner cycle index changed");
                    NativeClick(view, null, "ExchangeFormat");
                    Require(Field<bool>(exchange, "unboundFormat") && !Field<bool>(exchange, "plannerFormat") && !Field<bool>(exchange, "archive") &&
                        !Field<bool>(exchange, "planBuild") && !Field<bool>(exchange, "kgFormat") && !Field<BetterBuildingExport.Target?>(exchange, "bbpTarget").HasValue &&
                        !Field<OpenKeepExport.Consumer?>(exchange, "openKeepTarget").HasValue, "Unbound target flags conflict");
                    Require(Field<Button>(exchange, "format").GetComponentInChildren<TMP_Text>().text == BuildWorksLocalization.Text("exchange.format_unbound"), "Unbound format label missing");
                    NativeClick(view, null, "ExchangeFormat"); Require(!Field<bool>(exchange, "unboundFormat") && !Field<bool>(exchange, "archive"), "Unbound did not return to native JSON");
                    for (int i = 0; i < 9; ++i) NativeClick(view, null, "ExchangeFormat");
                    NativeClick(view, null, "ExchangeSubmit"); yield return null;
                    for (int frame = 0; frame < 600 && Field<Task<UnboundExport>>(exchange, "unboundInspection") != null; ++frame)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    var review = Field<UnboundExport>(exchange, "unboundReview"); var details = Field<TMP_Text>(exchange, "contentsDetails");
                    Require(review != null && details.text.Contains(name + ".vtbp") && details.text.Contains(name + ".buildworks.json") &&
                        !Directory.Exists(destination), "Unbound review wrote early or omitted pair names");
                    Require(Field<Button>(exchange, "primary").interactable == !blocked && Field<Button>(exchange, "contentsBack").interactable, "Unbound confirmation/blocker controls wrong");
                    Require(Field<RectTransform>(exchange, "contentsPanel").Find("ExchangeContentsTitle").GetComponent<TMP_Text>().text == BuildWorksLocalization.Text("exchange.unbound_title"), "Unbound title not localized");
                    Require(Field<Button>(exchange, "primary").GetComponentInChildren<TMP_Text>().text == BuildWorksLocalization.Text("exchange.export"), "Unbound confirmation falsely promises lossy export");
                    Require(Field<TMP_Text>(exchange, "message").text == BuildWorksLocalization.Text(blocked ? "exchange.unbound_blocked" : "exchange.unbound_warning"), "Unbound status uses another format's wording");
                    if (blocked) Require(details.text.Contains(BuildWorksLocalization.Text("exchange.unbound_block_hostprofile", 1)), "Missing native profile not explained");
                    string screenshot = Path.Combine(output, name + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png");
                    UnityEngine.ScreenCapture.CaptureScreenshot(screenshot);
                    for (int frame = 0; frame < 600 && !File.Exists(screenshot); ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                    Require(File.Exists(screenshot) && new FileInfo(screenshot).Length > 10000, "Unbound frame missing"); File.Copy(screenshot, Path.Combine(output, name + ".png"), true);
                    if (!blocked)
                    {
                        foreach (UnboundExport.BlockReason reason in Enum.GetValues(typeof(UnboundExport.BlockReason)))
                        {
                            var denied = UnboundExport.Review(source, _ => profile); denied.Sources = 2; denied.Blockers.Add((reason, "fixture"));
                            Set(exchange, "unboundInspection", Task.FromResult(denied)); exchange.Tick();
                            string key = "exchange.unbound_block_" + reason.ToString().ToLowerInvariant();
                            Require(!Field<Button>(exchange, "primary").interactable && details.text.Contains(BuildWorksLocalization.Text(key, reason == UnboundExport.BlockReason.MultipleSources ? 2 : 1)), "Unbound blocker mapping failed: " + reason);
                        }
                        NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                        Require(!Directory.Exists(destination) && Field<Task<UnboundExport>>(exchange, "unboundInspection") == null && Field<UnboundExport>(exchange, "unboundReview") == null, "Unbound Back retained review/output");
                        Frame(controller, input, Vector2.zero); yield return null;
                        Require(!Field<bool>(exchange, "reviewingContents") && Field<Button>(exchange, "primary").interactable, "Back did not restore the file-submit control");
                        NativeClick(view, null, "ExchangeSubmit"); yield return null;
                        for (int frame = 0; frame < 600 && Field<Task<UnboundExport>>(exchange, "unboundInspection") != null; ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                        Require(Field<UnboundExport>(exchange, "unboundReview") != null && Field<Button>(exchange, "primary").interactable, "Fresh Unbound review did not become ready");
                        source.parts[0].position.z += 1; NativeClick(view, null, "ExchangeSubmit"); yield return null;
                        for (int frame = 0; frame < 600 && Field<Task<string>>(exchange, "planExport") != null; ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                        Require(!Directory.Exists(destination), "Stale Unbound review wrote a destination");
                        Require(!Field<bool>(exchange, "completed"), "Stale Unbound review reported success");
                        Require(Field<TMP_Text>(exchange, "message").text.Contains(BuildWorksLocalization.Text("exchange.plan_changed")), "Stale Unbound refusal message missing: " + Field<TMP_Text>(exchange, "message").text);
                        source.parts[0].position.z -= 1; NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                        Frame(controller, input, Vector2.zero); yield return null;
                        Require(!Field<bool>(exchange, "reviewingContents") && Field<Button>(exchange, "primary").interactable, "Stale-result Back did not restore file submission");
                        NativeClick(view, null, "ExchangeSubmit"); yield return null;
                        for (int frame = 0; frame < 600 && Field<Task<UnboundExport>>(exchange, "unboundInspection") != null; ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                        NativeClick(view, null, "ExchangeSubmit"); yield return null;
                        for (int frame = 0; frame < 600 && Field<Task<string>>(exchange, "planExport") != null; ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                        Require(Field<bool>(exchange, "completed") && Directory.GetFiles(destination).Length == 2, "Unbound UI did not write pair");
                        var written = UnboundBlueprint.Parse(File.ReadAllText(Path.Combine(destination, name + ".vtbp")));
                        Require(written.Pieces.Single().Position.X == 1.25 && written.Pieces.Single().State.TryGet<string>("text".GetStableHashCode(), out var text) && text == "UI Привет", "UI output differs from working document");
                    }
                    NativeClick(view, null, "ExchangeClose"); yield return null;
                }
                missingProfile = false; BuildWorksLocalization.RussianLanguage = false;
                foreach (int targetIndex in new[] { 6, 7, 8, 9 })
                {
                    view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, source, closed)); yield return null;
                    var batch = Field<BlueprintExchangeView>(view, "exchangeView");
                    for (int frame = 0; frame < 600 && Field<Task<string[]>>(batch, "listing") != null; ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                    for (int i = 0; i < targetIndex; ++i) NativeClick(view, null, "ExchangeFormat");
                    NativeClick(view, null, "ExchangeBatchToggle"); yield return null;
                    Require(Field<bool>(batch, "batchMode") && Field<bool>(batch, "archive") && !Field<bool>(batch, "unboundFormat") &&
                        !Field<bool>(batch, "plannerFormat") && !Field<BetterBuildingExport.Target?>(batch, "bbpTarget").HasValue &&
                        !Field<bool>(batch, "planBuild") && !Field<bool>(batch, "kgFormat") && !Field<OpenKeepExport.Consumer?>(batch, "openKeepTarget").HasValue &&
                        !Field<Button>(batch, "format").interactable && Field<Button>(batch, "format").GetComponentInChildren<TMP_Text>().text == BuildWorksLocalization.Text("exchange.format_zip"), "Batch retained a foreign target: " + targetIndex);
                    Require(Field<RectTransform>(batch, "panel").Find("ExchangeHelp").GetComponent<TMP_Text>().text == BuildWorksLocalization.Text("exchange.export_help"), "Batch retained foreign help text");
                    NativeClick(view, null, "ExchangeBatchToggle"); yield return null; NativeClick(view, null, "ExchangeFormat");
                    Require(!Field<bool>(batch, "batchMode") && Field<bool>(batch, "planBuild") && !Field<bool>(batch, "unboundFormat") &&
                        !Field<bool>(batch, "plannerFormat") && !Field<BetterBuildingExport.Target?>(batch, "bbpTarget").HasValue && !Field<bool>(batch, "archive"), "Single target after batch is not PlanBuild");
                    if (targetIndex == 9)
                    {
                        Field<TMP_InputField>(batch, "fileName").SetTextWithoutNotify("after-unbound-batch");
                        NativeClick(view, null, "ExchangeSubmit"); yield return null;
                        for (int frame = 0; frame < 600 && Field<Task<PlanBuildExport>>(batch, "planInspection") != null; ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                        Require(Field<PlanBuildExport>(batch, "planReview") != null && Field<UnboundExport>(batch, "unboundReview") == null, "Post-batch review used Unbound route");
                        NativeClick(view, null, "ExchangeSubmit"); yield return null;
                        for (int frame = 0; frame < 600 && Field<Task<string>>(batch, "planExport") != null; ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                        string path = Path.Combine(store.ExportDirectory, "after-unbound-batch");
                        Require(Field<bool>(batch, "completed") && File.Exists(Path.Combine(path, "after-unbound-batch.blueprint")) &&
                            !File.Exists(Path.Combine(path, "after-unbound-batch.vtbp")), "Post-batch confirmation wrote the wrong foreign format");
                    }
                    NativeClick(view, null, "ExchangeClose"); yield return null;
                }
                int closes = 0;
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, source, () => { ++closes; closed(); })); yield return null;
                var deferred = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int frame = 0; frame < 600 && Field<Task<string[]>>(deferred, "listing") != null; ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                for (int i = 0; i < 9; ++i) NativeClick(view, null, "ExchangeFormat"); NativeClick(view, null, "ExchangeSubmit");
                Require(Field<bool>(deferred, "reviewingContents"), "Initial deferred review was not entered");
                var oldTask = new TaskCompletionSource<UnboundExport>(); Set(deferred, "unboundInspection", oldTask.Task); Set(deferred, "unboundReview", null); deferred.Tick();
                Frame(controller, input, Vector2.zero); yield return null;
                NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                Frame(controller, input, Vector2.zero); yield return null;
                Require(!Field<bool>(deferred, "reviewingContents") && Field<Button>(deferred, "primary").interactable, "Deferred Back did not restore file submission");
                NativeClick(view, null, "ExchangeFormat"); for (int i = 0; i < 9; ++i) NativeClick(view, null, "ExchangeFormat"); NativeClick(view, null, "ExchangeSubmit");
                Require(Field<bool>(deferred, "reviewingContents"), "Replacement deferred review was not entered");
                var currentTask = new TaskCompletionSource<UnboundExport>(); Set(deferred, "unboundInspection", currentTask.Task); Set(deferred, "unboundReview", null); deferred.Tick();
                oldTask.SetResult(UnboundExport.Review(source, _ => profile)); deferred.Tick();
                Require(Field<UnboundExport>(deferred, "unboundReview") == null && ReferenceEquals(Field<Task<UnboundExport>>(deferred, "unboundInspection"), currentTask.Task) &&
                    !Field<Button>(deferred, "primary").interactable, "Old Unbound task replaced current review");
                var closeReview = UnboundExport.Review(source, _ => profile); currentTask.SetResult(closeReview); deferred.Tick();
                Require(ReferenceEquals(Field<UnboundExport>(deferred, "unboundReview"), closeReview) && Field<Button>(deferred, "primary").interactable, "Current Unbound task did not enable confirm");
                var pendingWrite = new TaskCompletionSource<string>(); var lateReview = new TaskCompletionSource<UnboundExport>();
                Set(deferred, "planExport", pendingWrite.Task); Set(deferred, "unboundInspection", lateReview.Task); deferred.Tick();
                Frame(controller, input, Vector2.zero); yield return null;
                NativeClick(view, null, "ExchangeClose"); yield return null;
                lateReview.SetResult(UnboundExport.Review(source, _ => profile)); pendingWrite.SetResult("late-result"); deferred.Tick();
                bool cancelled = false; try { closeReview.TryWrite(source, _ => profile, working, "closed", out _, out _); } catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled && !Directory.Exists(Path.Combine(working, "closed")) && closes == 1 && !Field<bool>(deferred, "completed"), "Close failed to cancel/ignore deferred work");
                Require(original.SequenceEqual(BlueprintExchangeFormat.Encode(source)), "Unbound UI fixture mutated source");
                File.WriteAllText(Path.Combine(output, "unbound-ui-acceptance.json"), "{\"passed\":true,\"languages\":2,\"screenshots\":4,\"cycle\":true,\"batchReset\":true,\"pairedFiles\":true,\"blockers\":7,\"backNoWrite\":true,\"staleSource\":true,\"deferredReview\":true,\"closeCancels\":true,\"lateCompletionIgnored\":true,\"sourceImmutable\":true,\"gameHostTested\":false}");
            }
        }
        finally { BuildWorksLocalization.RussianLanguage = false; Object.Destroy(prefab); }
    }
}
