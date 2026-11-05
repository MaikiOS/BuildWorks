using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static partial class ControllerInputAcceptance
{
    internal static IEnumerable<object> RunOpenKeepExports(Camera template, TMP_Text font, string output)
    {
        string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../tests/fixtures/openkeep/hut_2x2.json"));
        var blueprint = ExternalBlueprintLibrary.LoadFile(fixture).Single();
        byte[] original = File.ReadAllBytes(fixture);
        string working = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/ok315", Guid.NewGuid().ToString("N").Substring(0, 8)));
        var store = new CompositeBlueprintStore(Path.Combine(working, "native.json"));
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.SetActive(false);
        var input = new InputFrames();
        try
        {
            using (var controller = new BlueprintEditorController(store, template, font, _ => prefab, id => id,
                () => Array.Empty<BlueprintEditorCatalogItem>(), error => { throw new InvalidOperationException(error); }, editorInput: input))
            {
                Require(controller.OpenNew(out string error), error);
                var view = Field<BlueprintEditorView>(controller, "view");
                foreach (bool russian in new[] { false, true })
                foreach (OpenKeepExport.Consumer target in Enum.GetValues(typeof(OpenKeepExport.Consumer)))
                {
                    BuildWorksLocalization.RussianLanguage = russian;
                    view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, blueprint, closed));
                    yield return null;
                    var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                    for (int i = 0; i < 600 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    string name = target + "-" + (russian ? "ru" : "en");
                    string path = Path.Combine(store.ExportDirectory, name);
                    Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify(name);
                    for (int i = 0; i < (target == OpenKeepExport.Consumer.OpenKeep ? 3 : 4); ++i) NativeClick(view, null, "ExchangeFormat");
                    Require(Field<OpenKeepExport.Consumer?>(exchange, "openKeepTarget") == target && !Field<bool>(exchange, "archive") &&
                        !Field<bool>(exchange, "planBuild"), "Foreign format cycle has incompatible flags");
                    NativeClick(view, null, "ExchangeSubmit"); yield return null;
                    for (int i = 0; i < 600 && Field<Task<OpenKeepExport>>(exchange, "openKeepInspection") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    var review = Field<OpenKeepExport>(exchange, "openKeepReview");
                    var details = Field<TMP_Text>(exchange, "contentsDetails");
                    Require(review != null && review.Blockers.Count == 0 && !Directory.Exists(path) &&
                        details.text.Contains(name + ".json") && details.text.Contains(name + ".buildworks.json") &&
                        details.text.Contains(BuildWorksLocalization.Text("exchange.openkeep_original_site")) &&
                        details.rectTransform.rect.height > ((RectTransform)details.transform.parent).rect.height &&
                        !Field<Button>(exchange, "storageChoice").gameObject.activeSelf && Field<Button>(exchange, "primary").interactable,
                        "Foreign review omitted backup/site limits, wrote early, clipped or cannot confirm");
                    if (russian && target == OpenKeepExport.Consumer.OpenKeep)
                    {
                        string screenshot = Path.Combine(output, "openkeep-review-ru-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png");
                        ScreenCapture.CaptureScreenshot(screenshot);
                        for (int i = 0; i < 120 && !File.Exists(screenshot); ++i) yield return null;
                        Require(File.Exists(screenshot) && new FileInfo(screenshot).Length > 1000, "Actual review screenshot missing");
                        yield return null; yield return null;
                        Require(Field<bool>(exchange, "reviewingContents") && Field<OpenKeepExport>(exchange, "openKeepReview") == review,
                            "Review changed before deferred screenshot completed");
                        File.Copy(screenshot, Path.Combine(output, "openkeep-review-ru.png"), true);
                        var scroll = details.transform.parent.GetComponent<ScrollRect>();
                        scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -20) });
                        yield return null; yield return null;
                        Require(scroll.verticalNormalizedPosition < .1f, "Native ScrollRect event did not expose lower source-frame warning");
                        string lower = Path.Combine(output, "openkeep-site-warning-ru-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png");
                        ScreenCapture.CaptureScreenshot(lower);
                        for (int i = 0; i < 120 && !File.Exists(lower); ++i) yield return null;
                        Require(File.Exists(lower) && new FileInfo(lower).Length > 1000 && Field<bool>(exchange, "reviewingContents"),
                            "Lower source-frame screenshot missing or review closed early");
                        yield return null; yield return null;
                        File.Copy(lower, Path.Combine(output, "openkeep-site-warning-ru.png"), true);
                        scroll.verticalNormalizedPosition = 1;
                    }
                    NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                    Require(Field<OpenKeepExport>(exchange, "openKeepReview") == null && !Directory.Exists(path) &&
                        Field<TMP_InputField>(exchange, "directory").interactable, "Back kept stale foreign review or wrote files");
                    NativeClick(view, null, "ExchangeSubmit"); yield return null;
                    for (int i = 0; i < 600 && Field<Task<OpenKeepExport>>(exchange, "openKeepInspection") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    blueprint.parts[0].position.x += 1;
                    NativeClick(view, null, "ExchangeSubmit"); yield return null;
                    for (int i = 0; i < 600 && Field<Task<string>>(exchange, "planExport") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    Require(!Directory.Exists(path) && !Field<bool>(exchange, "completed") &&
                        Field<TMP_Text>(exchange, "message").text.Contains(BuildWorksLocalization.Text("exchange.plan_changed")), "Stale foreign review published");
                    blueprint.parts[0].position.x -= 1;
                    NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                    NativeClick(view, null, "ExchangeSubmit"); yield return null;
                    for (int i = 0; i < 600 && Field<Task<OpenKeepExport>>(exchange, "openKeepInspection") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    NativeClick(view, null, "ExchangeSubmit"); yield return null;
                    for (int i = 0; i < 600 && Field<Task<string>>(exchange, "planExport") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    Require(Field<bool>(exchange, "completed") && Directory.GetFiles(path).Length == 2 &&
                        File.Exists(Path.Combine(path, name + ".json")) && File.Exists(Path.Combine(path, name + ".buildworks.json")),
                        "UI did not publish both foreign/native files: " + Field<TMP_Text>(exchange, "message").text);
                    NativeClick(view, null, "ExchangeClose"); yield return null;
                }
                var blocked = ExternalBlueprintLibrary.DetachedCopy(blueprint);
                blocked.parts[0].rotation.x = .1f;
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, blocked, closed)); yield return null;
                var denied = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 600 && Field<Task<string[]>>(denied, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                for (int i = 0; i < 3; ++i) NativeClick(view, null, "ExchangeFormat");
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 600 && Field<Task<OpenKeepExport>>(denied, "openKeepInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(!Field<Button>(denied, "primary").interactable && Field<Button>(denied, "contentsBack").interactable &&
                    Field<TMP_Text>(denied, "contentsDetails").text.Contains(BuildWorksLocalization.Text("exchange.openkeep_block_rotation", 1)),
                    "Unrepresentable rotation not explained or confirm still enabled");
                var closeReview = Field<OpenKeepExport>(denied, "openKeepReview");
                // A reviewed valid export must be cancelled when the owning dialog closes during its file task.
                var valid = store.ReviewOpenKeepExport(blueprint, OpenKeepExport.Consumer.OpenKeep);
                for (int i = 0; i < 600 && !valid.IsCompleted; ++i) yield return null;
                closeReview = valid.GetAwaiter().GetResult();
                var pending = new TaskCompletionSource<string>();
                Set(denied, "openKeepReview", closeReview); Set(denied, "planExport", pending.Task); denied.Tick();
                NativeClick(view, null, "ExchangeClose"); yield return null;
                bool cancelled = false;
                try { closeReview.TryWrite(blueprint, _ => null, output, "closed-export", out _, out _); }
                catch (OperationCanceledException) { cancelled = true; }
                pending.SetCanceled();
                Require(cancelled && !Directory.Exists(Path.Combine(output, "closed-export")), "Close did not cancel foreign publication");
                Require(original.SequenceEqual(File.ReadAllBytes(fixture)), "UI changed original source bytes");
            }
        }
        finally { BuildWorksLocalization.RussianLanguage = false; Object.Destroy(prefab); }
    }
}
