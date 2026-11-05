using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Actual HUD/catalog methods in Unity; external Valheim host types remain Workbench substitutes.
[InitializeOnLoad]
public static class ReleaseRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Pending = "BuildWorks.ReleaseRegression.Pending";
    static ReleaseRegression()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            EditorApplication.delayCall += () => {
                try { Check(); EditorApplication.Exit(0); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            };
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    private static void Check()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../..")), output = Path.Combine(root, "artifacts/release-regression");
        Directory.CreateDirectory(output);
        var eventHost = new GameObject("Release event system", typeof(EventSystem));
        var fontHost = new GameObject("Release font", typeof(RectTransform), typeof(TextMeshProUGUI));
        var font = fontHost.GetComponent<TMP_Text>(); var ownedFont = TMP_FontAsset.CreateFontAsset(Font.CreateDynamicFontFromOSFont("Arial", 20)); font.font = ownedFont;
        int rectangles = 0, catalogCases = 0;
        try
        {
            foreach (var mode in new[] { RenderMode.ScreenSpaceOverlay, RenderMode.ScreenSpaceCamera })
            foreach (float scale in new[] { .75f, 1f, 1.5f })
            foreach (bool offsetParent in new[] { false, true })
            {
                var canvasHost = new GameObject("Selection canvas", typeof(RectTransform), typeof(Canvas), typeof(Hud));
                var cameraHost = new GameObject("Selection camera", typeof(Camera));
                var target = new RenderTexture(640, 360, 0);
                Hud previous = Hud.instance;
                try
                {
                    var camera = cameraHost.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10); camera.targetTexture = target;
                    var canvas = canvasHost.GetComponent<Canvas>(); canvas.renderMode = mode; canvas.scaleFactor = scale; canvas.worldCamera = camera; canvas.planeDistance = 1;
                    var parent = new GameObject("HUD parent", typeof(RectTransform)).GetComponent<RectTransform>(); parent.SetParent(canvasHost.transform, false);
                    parent.anchorMin = parent.anchorMax = new Vector2(.5f, .5f); parent.sizeDelta = new Vector2(900, 600);
                    if (offsetParent) { parent.pivot = new Vector2(.2f, .7f); parent.anchoredPosition = new Vector2(71, -39); parent.localScale = new Vector3(1.2f, .8f, 1); }
                    Hud.instance = canvasHost.GetComponent<Hud>(); Hud.instance.m_rootObject = parent.gameObject;
                    var hudRoot = new GameObject("Existing HUD", typeof(RectTransform)); hudRoot.transform.SetParent(parent, false);
                    var box = new GameObject("Selection box", typeof(RectTransform), typeof(Image)); box.transform.SetParent(parent, false);
                    var rect = box.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                    var constructor = typeof(PrecisionPlacementHudView).GetConstructors().Single();
                    using (var view = (PrecisionPlacementHudView)constructor.Invoke(new object[constructor.GetParameters().Length]))
                    {
                        Set(view, "root", hudRoot); Set(view, "selectionBox", box); Set(view, "selectionBoxRect", rect);
                        Canvas.ForceUpdateCanvases();
                        foreach (var pair in new[] {
                            new[] { new Vector2(80, 60), new Vector2(440, 270) }, new[] { new Vector2(440, 270), new Vector2(80, 60) },
                            new[] { new Vector2(440, 60), new Vector2(80, 270) }, new[] { new Vector2(80, 270), new Vector2(440, 60) } })
                        {
                            view.ShowSelectionBox(pair[0], pair[1]); Canvas.ForceUpdateCanvases();
                            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                            Camera uiCamera = mode == RenderMode.ScreenSpaceOverlay ? null : camera;
                            var low = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
                            var high = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
                            Require(box.activeSelf && Vector2.Distance(low, Vector2.Min(pair[0], pair[1])) < .05f &&
                                Vector2.Distance(high, Vector2.Max(pair[0], pair[1])) < .05f, "Selection rectangle lost screen coordinates: " + mode + "/" + scale + "/" + offsetParent);
                            view.HideSelectionBox(); Require(!box.activeSelf, "Selection rectangle was not hidden."); ++rectangles;
                        }
                    }
                }
                finally { Hud.instance = previous; cameraHost.GetComponent<Camera>().targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(cameraHost); Object.DestroyImmediate(canvasHost); }
            }
            using (var view = new BlueprintEditorView(font))
            {
                var source = new CompositeBlueprintStore.Blueprint { id = "shared-id", name = "Original same ID", category = "QA", importSource = "original.blueprint" };
                var imported = new CompositeBlueprintStore.Blueprint { id = "shared-id", name = "Imported target", category = "QA", importSource = "imported.blueprint" };
                var items = new List<BlueprintEditorCatalogItem> { Item(source, 0), Item(imported, 1) };
                void Check()
                {
                    Require(view.ShowImportedCatalog(items, imported), "Imported target not found."); Canvas.ForceUpdateCanvases();
                    var selected = EventSystem.current.currentSelectedGameObject;
                    Require(selected && selected.transform.parent.name == "CatalogGrid" && selected.GetComponentInChildren<TMP_Text>().text.Contains(imported.name), "Import did not focus its exact source card.");
                    var preview = typeof(BlueprintEditorView).GetField("catalogPreviewItem", Private).GetValue(view) as BlueprintEditorCatalogItem;
                    Require(ReferenceEquals(preview.Blueprint, imported), "Imported preview selected another source with the same ID.");
                    ++catalogCases;
                }
                Check();
                items.InsertRange(0, Enumerable.Range(0, 80).Select(i => Item(new CompositeBlueprintStore.Blueprint {
                    id = "filler-" + i, name = "Filler " + i, category = "QA", importSource = "batch.blueprint" }, i + 2)));
                Check();
                var before = EventSystem.current.currentSelectedGameObject;
                Require(!view.ShowImportedCatalog(items, new CompositeBlueprintStore.Blueprint { id = imported.id, importSource = "absent.blueprint" }) &&
                    EventSystem.current.currentSelectedGameObject == before, "Absent import source changed selection."); ++catalogCases;
            }
            int heightControls = CheckHeightControl(font);
            CheckEvidenceLevels(font, output);
            CheckOptionalStatePolicy(font, output);
            Require(rectangles == 48 && catalogCases == 3 && heightControls == 4, "Release regression coverage incomplete.");
            File.WriteAllText(Path.Combine(output, "ui-verification.json"), JsonUtility.ToJson(new Result { passed = true, rectangles = rectangles, catalogCases = catalogCases, heightControls = heightControls }, true));
            Debug.Log("BUILDWORKS_RELEASE_REGRESSION_OK");
        }
        finally { if (ownedFont && !EditorUtility.IsPersistent(ownedFont)) Object.DestroyImmediate(ownedFont); Object.DestroyImmediate(fontHost); Object.DestroyImmediate(eventHost); }
    }
    private static void CheckEvidenceLevels(TMP_Text font, string output)
    {
        var host = new GameObject("Evidence canvas", typeof(RectTransform), typeof(Canvas));
        var cameraHost = new GameObject("Evidence camera", typeof(Camera));
        var target = new RenderTexture(1280, 720, 24);
        var camera = cameraHost.GetComponent<Camera>();
        var previousTarget = RenderTexture.active;
        bool previousLanguage = BuildWorksLocalization.RussianLanguage;
        try
        {
            camera.targetTexture = target; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var source = ExternalBlueprintLibrary.Parse("#Name:Evidence\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\n#Future\nopaque", "Original.blueprint");
            Require(source.importError == null && source.sourceDocuments[0].ToGeometry().HasOpaqueSections,
                "Evidence fixture lost its recognized row or opaque section.");
            var store = new CompositeBlueprintStore(Path.Combine(output, "evidence-fixture/blueprints.json"));
            foreach (bool russian in new[] { false, true })
            {
                BuildWorksLocalization.RussianLanguage = russian;
                using (var exchange = new BlueprintExchangeView(host.transform, font, store, source, () => { }))
                {
                    typeof(BlueprintExchangeView).GetMethod("ReviewContents", Private).Invoke(exchange, null);
                    var inspection = (System.Threading.Tasks.Task<CompositeBlueprintStore.ContentsSummary>)
                        typeof(BlueprintExchangeView).GetField("contentsInspection", Private).GetValue(exchange);
                    Require(inspection.GetAwaiter().GetResult().Unknown > 0, "Evidence UI lost opaque data accounting.");
                    exchange.Tick(); Canvas.ForceUpdateCanvases();
                    var details = (TMP_Text)typeof(BlueprintExchangeView).GetField("contentsDetails", Private).GetValue(exchange);
                    string[] expected = russian ? new[] { "Сохранено:", "Разобрано:", "Поведение:", "будет сохранён", "каталог 28", "документ 16", "это окно его не проверяет" }
                        : new[] { "Archived:", "Parsed:", "Restored behavior:", "will retain", "catalogue 28", "document 16", "this window does not verify" };
                    Require(expected.All(details.text.Contains), "Evidence UI conflated archived, parsed, source/output versions or verified behavior: " + details.text);
                    var scroll = details.GetComponentInParent<ScrollRect>();
                    Require(scroll && scroll.content == details.rectTransform && details.rectTransform.rect.height > scroll.viewport.rect.height,
                        "Evidence text is not accessible through the existing scroll viewport.");
                    File.WriteAllText(Path.Combine(output, "evidence-" + (russian ? "ru" : "en") + ".txt"), details.text);
                    foreach (bool bottom in new[] { false, true })
                    {
                        scroll.verticalNormalizedPosition = bottom ? 0 : 1; Canvas.ForceUpdateCanvases();
                        camera.Render(); RenderTexture.active = target;
                        var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
                        try { image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                            File.WriteAllBytes(Path.Combine(output, "evidence-" + (russian ? "ru" : "en") + (bottom ? "-bottom.png" : "-top.png")), image.EncodeToPNG()); }
                        finally { Object.DestroyImmediate(image); }
                    }
                }
            }
            Debug.Log("BUILDWORKS_EVIDENCE_LEVELS_OK EN/RU; actual RenderContents; opaque+parsed; scroll; 4 images; game untested");
        }
        finally
        {
            BuildWorksLocalization.RussianLanguage = previousLanguage; RenderTexture.active = previousTarget;
            camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(cameraHost); Object.DestroyImmediate(host);
        }
    }
    private static void CheckOptionalStatePolicy(TMP_Text font, string output)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string folder = Path.Combine(root, "artifacts/point-2.3-20261026/ui-case-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string raw = File.ReadAllText(Path.Combine(root, "tests/fixtures/armor-state/10-armor-state.blueprint")) +
            "\nwoodwall;;3;0;0;0;0;0;1;opaque;1;1;1\nmissing_prefab;;4;0;0;0;0;0;1;\"\";1;1;1\n#Terrain\nsquare;ignored\n";
        string input = Path.Combine(folder, "policy.blueprint"); File.WriteAllText(input, raw);
        var unit = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
        var source = ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.Parse(raw, input), _ => unit);
        Require(source.importError == null && source.parts.Count == 4, "Optional-state fixture failed to parse");
        string ordinaryPacket;
        using (var bytes = new MemoryStream())
        using (var writer = new BinaryWriter(bytes))
        {
            writer.Write(1); writer.Write((byte)1); writer.Write("health".GetStableHashCode()); writer.Write(-1f); writer.Flush();
            ordinaryPacket = Convert.ToBase64String(bytes.ToArray());
        }
        string ordinaryRaw = "#Name:Ordinary\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1;" + ordinaryPacket +
            "\nwoodwall;;1;0;0;0;0;0;1;\"\"\n#Terrain\nsquare;ignored\n";
        var ordinaryInput = ExternalBlueprintLibrary.Parse(ordinaryRaw, "ordinary.blueprint");
        var ordinary = ExternalBlueprintLibrary.PrepareForHost(ordinaryInput, _ => unit, name => name.GetStableHashCode(), 0);
        Require(ordinaryInput.parts[0].foreignData != null && ordinary.parts.All(part => part.foreignData == null) &&
            ordinary.sourceDocuments.Single().text == ordinaryRaw && CompositeBlueprintStore.InspectContents(ordinary, _ => 1).Unknown == 0,
            "Known-state direct-route fixture was not converted or lost its exact original");
        var host = new GameObject("Optional state canvas", typeof(RectTransform), typeof(Canvas));
        host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        bool previousLanguage = BuildWorksLocalization.RussianLanguage;
        T Field<T>(BlueprintExchangeView view, string name) => (T)typeof(BlueprintExchangeView).GetField(name, Private).GetValue(view);
        void Review(BlueprintExchangeView view)
        {
            typeof(BlueprintExchangeView).GetMethod("ReviewContents", Private).Invoke(view, null);
            for (int i = 0; i < 8; ++i)
            {
                foreach (string name in new[] { "listing", "contentsInspection", "importInspection", "importPreparation" })
                    Field<System.Threading.Tasks.Task>(view, name)?.GetAwaiter().GetResult();
                view.Tick();
                if (Field<CompositeBlueprintStore.ContentsSummary>(view, "contentsSummary") != null &&
                    Field<System.Threading.Tasks.Task>(view, "listing") == null) return;
            }
            throw new InvalidOperationException("Optional-state actual review did not complete");
        }
        try
        {
            BuildWorksLocalization.RussianLanguage = false;
            foreach (var preview in new[] { source, ordinary })
            using (var catalogue = new BlueprintEditorView(font))
            {
                Require(catalogue.ShowImportedCatalog(new[] { Item(preview, 0) }, preview), "Source preview unavailable before insertion");
                Canvas.ForceUpdateCanvases();
                Require(catalogue.RootCanvas.GetComponentsInChildren<TMP_Text>(true).Any(text =>
                    text.gameObject.activeInHierarchy && text.text.Contains("Terrain") && text.text.Contains("ownership")),
                    "Direct editor catalogue has no construction/terrain policy before use");
                Require(File.ReadAllText(input) == raw, "Source preview changed the original before any action");
            }
            foreach (bool importing in new[] { false, true })
            {
                BuildWorksLocalization.RussianLanguage = false;
                var store = new CompositeBlueprintStore(Path.Combine(folder, importing ? "import/library.json" : "export/library.json"));
                var context = new object();
                store.ConfigureImportScale(name => name == "missing_prefab" ? (CompositeBlueprintStore.VectorData?)null : unit, () => context);
                using (var view = new BlueprintExchangeView(host.transform, font, store, importing ? null : source, () => { }))
                {
                    if (importing) Set(view, "selected", input);
                    else Field<TMP_InputField>(view, "fileName").SetTextWithoutNotify("omitted");
                    Review(view);
                    Require(!Field<bool>(view, "omitArmorState") && !Field<bool>(view, "omitMissingParts") &&
                        Field<CompositeBlueprintStore.ContentsSummary>(view, "contentsSummary").ArmorStates == 1,
                        "Optional state was not retained by default");
                    Field<Button>(view, "armorChoice").onClick.Invoke();
                    Require(Field<bool>(view, "omitArmorState") && Field<TMP_Text>(view, "message").text ==
                        BuildWorksLocalization.Text("exchange.armor_warning", 1, 1), "Actual armor callback did not show its loss warning");
                    Field<Button>(view, "missingChoice").onClick.Invoke();
                    Require(Field<bool>(view, "omitMissingParts") && Field<TMP_Text>(view, "message").text ==
                        BuildWorksLocalization.Text("exchange.missing_warning", 1, 1, 1), "Combined missing/armor warning lost one exclusion");
                    // The policy must remain visible even when a more specific warning owns the footer.
                    string details = Field<TMP_Text>(view, "contentsDetails").text;
                    Require(details.Contains("Terrain") && details.Contains("ownership"),
                        "Optional state review has no named terrain/ordinary-state policy before commit");
                    Field<Button>(view, "contentsBack").onClick.Invoke();
                    Require(!Field<bool>(view, "reviewingContents") && Field<string>(view, "resultPath") == null &&
                        !Directory.Exists(store.ImportDirectory) && source.parts[1].foreignData != null && File.ReadAllText(input) == raw,
                        "Cancel published an optional-loss copy or changed the input");
                    Review(view);
                    Require(!Field<bool>(view, "omitArmorState") && !Field<bool>(view, "omitMissingParts"), "New review retained cancelled loss choices");
                    Field<Button>(view, "armorChoice").onClick.Invoke(); Field<Button>(view, "missingChoice").onClick.Invoke();
                    Require(Field<Button>(view, "primary").interactable, "Actual confirmation stayed disabled");
                    Field<Button>(view, "primary").onClick.Invoke();
                    if (importing)
                        for (int i = 0; i < 20 && !Field<bool>(view, "completed"); ++i)
                        {
                            ((System.Threading.Tasks.Task)typeof(CompositeBlueprintStore).GetField("importTask", Private).GetValue(store))?.GetAwaiter().GetResult();
                            store.CatalogAll(); view.Tick();
                        }
                    Require(Field<bool>(view, "completed"), "Actual confirmation did not publish its bounded copy: " + Field<TMP_Text>(view, "message").text);
                    var written = importing ? store.CatalogAll().Single() : BlueprintExchangeFormat.Decode(File.ReadAllText(Field<string>(view, "resultPath")));
                    Require(written.parts.Count == 3 && written.parts.Single(part => part.prefabName == "ArmorStand").foreignData == null &&
                        written.parts.Single(part => part.position.x == 3).foreignData != null && written.sourceDocuments.Single().text == raw &&
                        File.ReadAllText(input) == raw && source.parts[1].foreignData != null && source.parts.Count == 4,
                        "Actual callback removed an unrelated unknown field, lost raw source/TRS or mutated the input");
                    var replay = ExternalBlueprintLibrary.PrepareForHost(BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(
                        written.IsExternal ? ExternalBlueprintLibrary.DetachedCopy(written) : written))), _ => unit);
                    Require(replay.parts.Single(part => part.prefabName == "ArmorStand").foreignData == null,
                        "Excluded state resurrected from the archived row");
                }
            }
            string bad = raw.Replace("woodwall;;3;0;0", "woodwall;;NaN;0;0"), badPath = Path.Combine(folder, "bad-mandatory.blueprint");
            File.WriteAllText(badPath, bad);
            var refusedStore = new CompositeBlueprintStore(Path.Combine(folder, "refused/library.json"));
            using (var refused = new BlueprintExchangeView(host.transform, font, refusedStore, null, () => { }))
            {
                Set(refused, "selected", badPath);
                typeof(BlueprintExchangeView).GetMethod("ReviewContents", Private).Invoke(refused, null);
                Field<System.Threading.Tasks.Task>(refused, "importInspection").ContinueWith(_ => { }).GetAwaiter().GetResult();
                refused.Tick();
                Require(Field<CompositeBlueprintStore.ContentsSummary>(refused, "contentsSummary") == null &&
                    !Field<Button>(refused, "primary").interactable && !Directory.Exists(refusedStore.ImportDirectory) &&
                    refusedStore.CatalogAll().Count == 0 && File.ReadAllText(badPath) == bad,
                    "Bad mandatory transform allowed partial UI confirmation/publication or changed source");
            }
            File.WriteAllText(Path.Combine(output, "optional-state-check.txt"), "PASS actual import/export review callbacks: visible pre-use catalogue policy, keep default, armor+missing warnings, explicit flags, cancel/reset, exact raw source, unrelated unknown retained, no resurrection, mandatory failure without partial publication; no game proof\n" + folder);
            Debug.Log("BUILDWORKS_OPTIONAL_STATE_POLICY_OK actual import/export callbacks; no game or restoration authority proof");
        }
        finally { BuildWorksLocalization.RussianLanguage = previousLanguage; Object.DestroyImmediate(host); }
    }
    private static int CheckHeightControl(TMP_Text label)
    {
        var host = new GameObject("Height control", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(AngleDragControl));
        try
        {
            var input = host.GetComponent<TMP_InputField>(); input.textComponent = label; input.enabled = false;
            var control = host.GetComponent<AngleDragControl>(); int previews = 0, finished = 0;
            control.Initialize(delta => { ++previews; control.SetValue(control.Value + delta, "height"); }, input, label, false, .05f, -512, 512);
            control.EditFinished = () => { Require(!input.enabled, "Height completion retained text input."); ++finished; };
            var drag = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            control.OnBeginDrag(drag);
            for (int i = 1; i <= 64; ++i) { drag.position = new Vector2(i, 0); control.OnDrag(drag); }
            Require(previews == 64 && finished == 0, "Height preview ran completion before release.");
            control.OnEndDrag(drag); control.OnEndDrag(drag); Require(finished == 1, "Height drag completion duplicated or missing.");
            control.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = Vector2.up });
            Require(finished == 2, "Height wheel did not complete.");
            control.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right });
            Require(finished == 3 && control.Value == 0, "Height reset did not complete.");
            control.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            input.onValueChanged.Invoke("12.5"); Require(input.enabled && finished == 3, "Text preview ended the gesture.");
            input.onEndEdit.Invoke("12.5"); Require(finished == 4 && control.Value == 12.5f, "Height text completion failed.");
            return finished;
        }
        finally { Object.DestroyImmediate(host); }
    }
    private static BlueprintEditorCatalogItem Item(CompositeBlueprintStore.Blueprint blueprint, int index)
        => new BlueprintEditorCatalogItem("blueprint_" + blueprint.id, blueprint.name, null, blueprint.category, "Blueprints", "source", index, blueprint);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Result { public bool passed, gameHostTested = false; public int rectangles, catalogCases, heightControls; }
}
