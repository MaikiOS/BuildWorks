using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static partial class ControllerInputAcceptance
{
    internal static IEnumerable<object> RunPlannerExports(Camera template, TMP_Text font, string output)
    {
        string working = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/planner324", Guid.NewGuid().ToString("N")));
        var store = new CompositeBlueprintStore(Path.Combine(working, "native.json"));
        string file = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/point-2.17-20261029/producer-run-v1/planner-three-pieces.json"));
        byte[] bytes = File.ReadAllBytes(file);
        var source = ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.LoadFile(file).Single(), _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
        Require(source.importError == null, "Planner UI fixture rejected");
        Require(store.TryGetOrCreatePlacementCopy(source, out var saved, out string error), error);
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.SetActive(false); var input = new InputFrames();
        try
        {
            using (var controller = new BlueprintEditorController(store, template, font, _ => prefab, id => id,
                () => Array.Empty<BlueprintEditorCatalogItem>(), text => { throw new InvalidOperationException(text); }, editorInput: input))
            {
                Require(controller.OpenExisting(saved, out error), error);
                var doc = Field<BlueprintEditorDocument>(controller, "document");
                Require(doc.Groups.Count == 2 && doc.Sources.Count == 1 && doc.Groups.All(g => g.SourceDocumentId != null && g.SourceLayerId != null), "Real editor lost imported layers");
                var view = Field<BlueprintEditorView>(controller, "view");
                for (int mode = 0; mode < 4; ++mode)
                {
                    bool russian = mode % 2 == 1, blocked = mode >= 2; BuildWorksLocalization.RussianLanguage = russian;
                    Require(BlueprintSourcePresentation.Metadata(source) == BuildWorksLocalization.Text("catalog.planner_metadata", 3, 2, 0), "Planner source metadata/language failed");
                    var document = source.ImportCopy(); if (blocked) document.parts[0].rotation.x = .1f;
                    string name = "planner-" + (blocked ? "blocked-" : "") + (russian ? "ru" : "en");
                    view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, document, closed)); yield return null;
                    var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                    for (int i = 0; i < 600 && Field<Task<string[]>>(exchange, "listing") != null; ++i) { Frame(controller, input, Vector2.zero); yield return null; }
                    Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify(name);
                    for (int i = 0; i < 8; ++i) NativeClick(view, null, "ExchangeFormat");
                    Require(Field<bool>(exchange, "plannerFormat") && !Field<BetterBuildingExport.Target?>(exchange, "bbpTarget").HasValue &&
                        !Field<bool>(exchange, "archive") && !Field<bool>(exchange, "planBuild") && !Field<bool>(exchange, "kgFormat") &&
                        !Field<OpenKeepExport.Consumer?>(exchange, "openKeepTarget").HasValue, "Planner format flags conflict");
                    NativeClick(view, null, "ExchangeSubmit"); yield return null;
                    for (int i = 0; i < 600 && Field<Task<PlannerExport>>(exchange, "plannerInspection") != null; ++i) { Frame(controller, input, Vector2.zero); yield return null; }
                    var review = Field<PlannerExport>(exchange, "plannerReview");
                    string title = Field<RectTransform>(exchange, "contentsPanel").Find("ExchangeContentsTitle").GetComponent<TMP_Text>().text;
                    var details = Field<TMP_Text>(exchange, "contentsDetails");
                    Require(review != null && title == BuildWorksLocalization.Text("exchange.planner_title") && details.text.Contains(name + ".json") &&
                        details.text.Contains(name + ".buildworks.json"), "Planner title/paired filenames missing");
                    string destination = Path.Combine(store.ExportDirectory, name); Require(!Directory.Exists(destination), "Planner review wrote before confirm");
                    Require(Field<Button>(exchange, "primary").interactable == !blocked && Field<Button>(exchange, "contentsBack").interactable, "Planner blocker buttons wrong");
                    if (blocked) Require(review.Blockers.Any(b => b.Reason == PlannerExport.BlockReason.Rotation), "Pitch warning absent");
                    var scroll = details.transform.parent.GetComponent<ScrollRect>(); scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -100) });
                    yield return null; yield return null; Require(scroll.verticalNormalizedPosition < .2f, "Planner review cannot scroll to lower warnings");
                    string screenshot = Path.Combine(output, name + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png");
                    ScreenCapture.CaptureScreenshot(screenshot);
                    for (int frame = 0; frame < 600 && !File.Exists(screenshot); ++frame) { Frame(controller, input, Vector2.zero); yield return null; }
                    Require(File.Exists(screenshot) && new FileInfo(screenshot).Length > 10000, "Planner screenshot not written");
                    File.Copy(screenshot, Path.Combine(output, name + ".png"), true);
                    if (!blocked)
                    {
                        NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                        Require(!Directory.Exists(destination) && !Field<bool>(exchange, "reviewingContents"), "Planner Back leaked output");
                        Frame(controller, input, Vector2.zero); yield return null;
                        NativeClick(view, null, "ExchangeSubmit"); yield return null;
                        for (int i = 0; i < 600 && Field<Task<PlannerExport>>(exchange, "plannerInspection") != null; ++i) { Frame(controller, input, Vector2.zero); yield return null; }
                        NativeClick(view, null, "ExchangeSubmit"); yield return null;
                        for (int i = 0; i < 600 && Field<Task<string>>(exchange, "planExport") != null; ++i) { Frame(controller, input, Vector2.zero); yield return null; }
                        Require(Field<bool>(exchange, "completed") && Directory.GetFiles(destination).Length == 2, "Planner UI pair failed");
                        Require(File.ReadAllBytes(Path.Combine(destination, name + ".json")).SequenceEqual(bytes), "UI no-edit planner tokens changed");
                        var native = BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(destination, name + ".buildworks.json")));
                        Require(native.groups.Count == 2 && native.sourceDocuments.Single().text == source.sourceDocuments.Single().text, "UI full copy lost source/layers");
                    }
                    NativeClick(view, null, "ExchangeClose"); yield return null;
                }
                Require(File.ReadAllBytes(file).SequenceEqual(bytes), "UI test changed original producer file");
                File.WriteAllText(Path.Combine(output, "planner-ui-acceptance.json"), "{\"passed\":true,\"languages\":2,\"screenshots\":4,\"editorLayers\":true,\"pairedFiles\":true,\"backNoWrite\":true,\"pitchBlocked\":true,\"nativeScrollEvent\":true,\"sourceImmutable\":true,\"gameHostTested\":false}");
            }
        }
        finally { BuildWorksLocalization.RussianLanguage = false; Object.Destroy(prefab); }
    }
}
