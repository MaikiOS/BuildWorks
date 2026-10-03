using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Executes product View/Controller/Scene copied verbatim by Capture-UiWorkbench.ps1.
// Only the game host types are stubbed; no surrogate editor UI is built here.
[InitializeOnLoad]
public static class RuntimeEditorAcceptance
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const string PendingRun = "BuildWorks.RuntimeAcceptance.Pending";
    private static IEnumerator run;
    private static int lastFrame = -1;
    private static readonly List<string> checks = new List<string>();
    private static readonly List<string> runtimeErrors = new List<string>();
    [Serializable]
    private sealed class Result
    {
        public string status = "passed_runtime_workbench";
        public string unityVersion = Application.unityVersion;
        public string completedAtUtc = DateTime.UtcNow.ToString("o");
        public int screenshots;
        public string[] checks;
        public bool controllerInputPassed;
        public bool exactAssetPassed;
        public bool nativeInteractionPassed;
        public int uiCases;
        public string graphicRaycastResolution = "1920x1080 at 100/120/140%; other matrix resolutions render actual UI with direct native pointer dispatch";
        public bool gameHostTested = false;
    }
    static RuntimeEditorAcceptance()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingRun, false)) return;
            SessionState.SetBool(PendingRun, false);
            run = CaptureRuntime();
            EditorApplication.update += Advance;
        };
    }

    private static void Advance()
    {
        try
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (run.MoveNext()) return;
            EditorApplication.update -= Advance;
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            EditorApplication.update -= Advance;
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, Private).GetValue(instance);
    private static void Set(object instance, string name, object value) =>
        instance.GetType().GetField(name, Private).SetValue(instance, value);
    private static object Call(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, Private).Invoke(instance, args);
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    public static void CaptureAll()
    {
        Debug.Log("BUILDWORKS_RUNTIME_EDITOR_ENTER");
        // Match the embedded PNG decoder: no NPOT resize or compression; minify with mipmaps.
        foreach (string icon in new[] { "native", "corner", "midpoint", "centre", "pin", "active", "helper", "selected", "geometry" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/BuildWorks/Icons/snap-" + icon + ".png");
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        if (!Resources.Load<TMP_Settings>("TMP Settings"))
        {
            string package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly).resolvedPath;
            AssetDatabase.importPackageCompleted += AfterTmpImport;
            AssetDatabase.ImportPackage(Path.Combine(package, "Package Resources/TMP Essential Resources.unitypackage"), false);
            return;
        }
        BeginPlay();
    }

    private static void AfterTmpImport(string packageName)
    {
        AssetDatabase.importPackageCompleted -= AfterTmpImport;
        EditorApplication.delayCall += BeginPlay;
    }

    private static void BeginPlay()
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty values = tags.FindProperty("tags");
        bool hasSnapTag = false;
        for (int index = 0; index < values.arraySize; ++index)
            hasSnapTag |= values.GetArrayElementAtIndex(index).stringValue == "snappoint";
        if (!hasSnapTag)
        {
            values.InsertArrayElementAtIndex(values.arraySize);
            values.GetArrayElementAtIndex(values.arraySize - 1).stringValue = "snappoint";
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(PendingRun, true);
        EditorApplication.EnterPlaymode();
    }

    private static IEnumerator CaptureRuntime()
    {
            Require(Application.isPlaying, "Acceptance must run product code in Play Mode");
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    runtimeErrors.Add(message);
            };
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            foreach (object step in TestSkin()) yield return step;
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string output = Path.Combine(repository, "artifacts", "ui-runtime");
            Directory.CreateDirectory(output);
            Camera template = new GameObject("HostCamera", typeof(Camera)).GetComponent<Camera>();
            template.enabled = false;
            TMP_Text fontTemplate = new GameObject("Font", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            fontTemplate.font = TMP_FontAsset.CreateFontAsset(Font.CreateDynamicFontFromOSFont("Arial", 20));
            fontTemplate.gameObject.SetActive(false);
            checks.Add(F9HudAcceptance.Run(fontTemplate.font));
            checks.Add(BlueprintResourceHudAcceptance.Run(fontTemplate.font, output));
            checks.Add(WorldSelectionHighlightAcceptance.Run());
            checks.Add(PlacementContactAcceptance.Run());
            checks.Add(UICatalogAcceptance.Run(fontTemplate.font));
            foreach (object step in ControllerInputAcceptance.Run(template, fontTemplate, output)) yield return step;
            foreach (object step in ControllerInputAcceptance.RunContourLifecycle(template, fontTemplate, output)) yield return step;
            checks.Add("Actual contour input: hover guide without copies, leave clears, click previews, cancel/tool switch/delete clear guide permanently");
            checks.Add("Actual Controller.Update/LateUpdate frame chords and native EventSystem raycasts: Ctrl+A, Shift rows/viewport, pin, shared gizmo, Alt move preview/commit/cancel/Undo, scale, placement RMB catalog reopen/camera and compound import; single Outliner context subscriptions; Array arrows/wheel/Ctrl zoom/Fit/spacing/shared step presets/live scrub before release/all TMP profile/atomic apply");
            TestEditorGestures(template, fontTemplate);
            TestNativeSocketTargets(template);
            yield return null;
            string storePath = Path.Combine(output, "acceptance-" + Guid.NewGuid().ToString("N") + ".json");
            var store = new CompositeBlueprintStore(storePath);
            var source = new Dictionary<string, GameObject>();
            foreach (string name in new[] { "woodwall", "wood_pole" })
            {
                GameObject prefab = Resources.Load<GameObject>("VanillaPreview/" + name);
                Require(prefab, "Imported vanilla visual missing: " + name);
                source.Add(name, prefab);
            }
            var thumbnails = new Dictionary<string, Sprite>();
            foreach (var item in source) thumbnails.Add(item.Key, CatalogThumbnail(template, item.Value));
            yield return null;
            var catalog = new List<BlueprintEditorCatalogItem>();
            for (int i = 0; i < 96; ++i)
                catalog.Add(new BlueprintEditorCatalogItem(i % 2 == 0 ? "woodwall" : "wood_pole",
                    "Деталь " + i, thumbnails[i % 2 == 0 ? "woodwall" : "wood_pole"], "Строительство",
                    "Материал " + (i % 36).ToString("00"), "Ванильное", i));
            foreach (string category in new[] { "Декор", "Крыши" })
            {
                var blueprint = new CompositeBlueprintStore.Blueprint { id = "matrix-" + category, name = category + " — пример", category = category };
                blueprint.parts.Add(new CompositeBlueprintStore.Part { stableId = "wall", prefabName = "woodwall", displayName = "Стена",
                    rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity) });
                catalog.Add(new BlueprintEditorCatalogItem(blueprint.id, blueprint.name, thumbnails["woodwall"],
                    category, "Чертёж", "Мои чертежи", catalog.Count, blueprint));
            }
            using (var controller = new BlueprintEditorController(store, template, fontTemplate,
                name => source.TryGetValue(name, out GameObject prefab) ? prefab : null,
                name => name, () => catalog, message => { throw new InvalidOperationException(message); }))
            {
                Require(controller.OpenNew(out string error), "OpenNew: " + error);
                Require(controller.AddPart("woodwall", "Стена"), "Add native wall");
                Require(controller.AddPart("wood_pole", "Столб"), "Add native pole");
                var view = Field<BlueprintEditorView>(controller, "view");
                var scene = Field<BlueprintEditorScene>(controller, "scene");
                Require(scene.ModelSpaceGizmo && scene.NativePointsOnly && !scene.ExperimentalGeometryPoints,
                    "Editor defaults are not model sizing/native snap/experiment off");
                scene.ModelSpaceGizmo = false; scene.NativePointsOnly = false;
                var document = controller.Document;
                string selected = document.Parts[0].StableId;
                document.SelectOnly(selected);
                Call(controller, "Bind", "Проверка фактического редактора");
                Call(controller, "SetActiveTool", BlueprintEditorTool.Transform);
                TMP_InputField scale = Field<TMP_InputField>(view, "inspectorScale");
                Require(scale.text == "100", "Scale must display percent, not factor");
                scale.text = "125";
                scale.onEndEdit.Invoke(scale.text);
                Require(Math.Abs(document.Parts[0].Scale.X - 1.25) < 0.0001, "Actual TMP scale event did not reach document");
                // Rejected values must neither commit nor leave the view displaying them.
                scale.text = "0";
                scale.onEndEdit.Invoke(scale.text);
                Require(Math.Abs(document.Parts[0].Scale.X - 1.25) < 0.0001, "Invalid percentage changed document");
                Call(controller, "Bind", "Числовые поля проверены");
                var scrub = scale.GetComponent<BlueprintEditorNumericScrub>();
                Require(Math.Abs(view.ScaleStepPercent - 10f) < 0.0001f,
                    "Scale scrub default step must be 10 percentage points");
                var pointer = new PointerEventData(EventSystem.current) {
                    button = PointerEventData.InputButton.Left, position = Vector2.zero };
                scrub.OnBeginDrag(pointer);
                pointer.position = Vector2.right * 10f;
                scrub.OnDrag(pointer);
                scrub.OnEndDrag(pointer);
                Require(Math.Abs(document.Parts[0].Scale.X - 1.35) < 0.0001,
                    "Scale scrub must apply the configured 10 percentage point step");
                view.SetHoveredNode(document.Parts[1].StableId);
                Require(Field<string>(view, "hoveredNodeId") == document.Parts[1].StableId, "Hover row was not updated");
                view.SetOperationMetrics(1, Vector3.right, Vector3.zero, 1.35f, 0, 2);
                string metrics = Field<string>(view, "operationMetrics");
                view.SetStatus("Проверочное сообщение");
                Require(Field<TMP_Text>(view, "statusText").text.StartsWith(metrics), "Status message erased operation metrics");
                Set(controller, "cameraFocus", new Vector3(1000,1000,1000));
                Field<RectTransform>(view, "viewportControls").Find("ViewportSectionButton0").GetComponent<Button>().onClick.Invoke();
                Require(view.HasViewportSettings, "View button must open viewport settings");
                Field<GameObject>(view, "viewportSettings").transform.Find("ViewportSection0/ViewportFrameSelection").GetComponent<Button>().onClick.Invoke();
                Require(Field<Vector3>(controller, "cameraFocus").sqrMagnitude < 1000, "Viewport frame-selection action is disconnected");
                view.HideViewportSettings();
                checks.Add("Real TMP events: percentage, scrub, invalid value and hover row");
                TestSaveRoundtrip(controller, storePath);
                TestScaleBoundaries(controller, storePath);
                view = Field<BlueprintEditorView>(controller, "view");
                scene = Field<BlueprintEditorScene>(controller, "scene");
                document = controller.Document;
                document.SelectOnly(selected);
                TestModifiers(controller);
                TestControllerHandles(controller);
                yield return null;

                Camera uiCamera = new GameObject("CaptureUiCamera", typeof(Camera)).GetComponent<Camera>();
                uiCamera.enabled = false;
                uiCamera.clearFlags = CameraClearFlags.Depth;
                uiCamera.cullingMask = 1 << 5;
                uiCamera.nearClipPlane = 0.1f;
                uiCamera.farClipPlane = 100f;
                view.RootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                view.RootCanvas.worldCamera = uiCamera;
                view.RootCanvas.planeDistance = 10f;
                foreach (Transform child in view.RootCanvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                int count = 0;
                foreach (int width in new[] { 1920, 2560, 3440 })
                foreach (float uiScale in new[] { 1f, 1.2f, 1.4f })
                {
                    int height = width == 1920 ? 1080 : 1440;
                    var target = new RenderTexture(width, height, 24);
                    uiCamera.targetTexture = target;
                    scene.Camera.targetTexture = target;
                    view.SetUiScale(uiScale);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    view.Tick();
                    Canvas.ForceUpdateCanvases();
                    scene.SetViewport(view.ViewportScreenRect());
                    Call(controller, "FrameAll");
                    foreach (string state in new[] { "select", "transform", "array", "contour", "catalog",
                        "outliner", "settings", "catalog-blueprints", "catalog-materials" })
                    {
                        view.HideCatalog();
                        view.HideOutlinerMenu();
                        view.HideViewportSettings();
                        BlueprintEditorTool tool = state == "array" ? BlueprintEditorTool.Array :
                            state == "contour" ? BlueprintEditorTool.Contour :
                            state == "select" ? BlueprintEditorTool.Select : BlueprintEditorTool.Transform;
                        Call(controller, "SetActiveTool", tool);
                        if (state.StartsWith("catalog"))
                        {
                            Call(controller, "RequestCatalog");
                            yield return null;
                            Canvas.ForceUpdateCanvases();
                            ControllerInputAcceptance.NativeClick(view, uiCamera,
                                state == "catalog-blueprints" ? "CatalogBlueprintsMode" : "CatalogPartsMode",
                                width == 1920);
                            TMP_InputField page = Field<TMP_InputField>(view, "catalogPageInput");
                            page.SetTextWithoutNotify("1");
                            page.onEndEdit.Invoke("1");
                            if (state == "catalog-materials")
                            {
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "MaterialsPageDown", width == 1920);
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "MaterialsPageDown", width == 1920);
                            }
                        }
                        else if (state == "outliner") ControllerInputAcceptance.NativeClick(view, uiCamera, "OutlinerMenuButton", width == 1920);
                        else if (state == "settings") ControllerInputAcceptance.NativeClick(view, uiCamera, "View", width == 1920);
                        else Call(controller, "UpdateGizmo");
                        // Product Destroy() and TMP rebuilds must see a real frame.
                        yield return null;
                        Call(controller, "RefreshContextHints");
                        Canvas.ForceUpdateCanvases();
                        ValidateControls(view, state);
                        scene.Camera.Render();
                        uiCamera.Render();
                        string name = state + "-" + width + "x" + height + "-" + Mathf.RoundToInt(uiScale * 100);
                        SavePixels(target, Path.Combine(output, name + ".png"));
                        ++count;
                    }
                    uiCamera.targetTexture = null;
                    scene.Camera.targetTexture = null;
                    Object.DestroyImmediate(target);
                }
                TestUnreadablePicking(template);
                TestPlacementSurface(template);
                TestContourNativeEdges(template);
                TestNestedSelection(template, source["woodwall"]);
                TestHoverAppearance(template);
                TestOccluderFade(template, output);
                TestGizmoHits(template);
                TestModelSpaceGizmo(template);
                TestExperimentalGeometry(template, fontTemplate);
                TestFurnitureCornersAndCursorIntent(template, output);
                controller.Close(true);
                Require(controller.OpenNew(out error), "Reopen: " + error);
                Require(Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Select,
                    "Reopen must reset active tool");
                controller.Close(true);
                checks.Add("Reopening clears active operation");
                Require(runtimeErrors.Count == 0, "Runtime logged errors: " + string.Join("; ", runtimeErrors));
                yield return null;
                ActualDividerAcceptance.Run(output);
                File.WriteAllLines(Path.Combine(output, "acceptance-checks.txt"), checks);
                string nativeProof = File.ReadAllText(Path.Combine(output, "native-interaction-check.txt"));
                string assetProof = File.ReadAllText(Path.Combine(output, "actual-divider-check.txt"));
                var result = new Result { screenshots = count, uiCases = count, checks = checks.ToArray(),
                    controllerInputPassed = File.ReadAllText(Path.Combine(output, "controller-input-check.txt")).StartsWith("PASS:"),
                    nativeInteractionPassed = nativeProof.Contains("PASS installed prefab hierarchy unchanged") && !nativeProof.Contains("\nFAIL"),
                    exactAssetPassed = assetProof.Contains("PASS exact installed asset;") && !assetProof.Contains("BLOCKED/FAILED") };
                bool passed = result.controllerInputPassed && result.nativeInteractionPassed && result.exactAssetPassed;
                if (!passed) result.status = "failed_runtime_workbench";
                File.WriteAllText(Path.Combine(output, "verification-status.json"),
                    JsonUtility.ToJson(result, true));
                Require(passed, "Exact asset/native/input gate failed; see separate proof files and verification-status.json");
                Debug.Log("BUILDWORKS_RUNTIME_EDITOR_OK " + count + " screenshots");
            }
            foreach (Sprite sprite in thumbnails.Values) { Object.Destroy(sprite.texture); Object.Destroy(sprite); }
            yield return null;
    }

    private static void ValidateControls(BlueprintEditorView view, string state)
    {
        GameObject edit = Field<GameObject>(view, "inspectorEditPanel");
        if (state == "select") Require(edit.activeSelf,
            "Select with an editable selection does not expose transform controls");
        RectTransform clip = Field<RectTransform>(view, "inspector").Find("InspectorViewport") as RectTransform;
        Require(clip, "Actual inspector clipping viewport is missing");
        foreach (TextMeshProUGUI text in view.RootCanvas.GetComponentsInChildren<TextMeshProUGUI>())
        {
            if (string.IsNullOrWhiteSpace(text.text)) continue;
            text.ForceMeshUpdate();
            Require(text.font && text.font.material && text.font.material.shader.isSupported,
                "Text has no supported material: " + text.name);
            Require(text.textInfo.characterCount > 0, "Text generated no characters: " + text.name);
            if (text.GetComponentInParent<Button>())
                Require(!text.isTextOverflowing, "Button text overflows: " + text.text);
            if (text.name == "ContourInfo" || text.name == "ArrayInfo")
                Require(!text.isTextOverflowing, "Tool instructions overflow: " + text.name);
            if (text.name.StartsWith("StatusHints", StringComparison.Ordinal))
                Require(!text.isTextTruncated && !text.isTextOverflowing,
                    "Context hotkeys are clipped: " + text.text);
        }
        foreach (TMP_InputField input in clip.GetComponentsInChildren<TMP_InputField>()) EnsureInside(clip, input.GetComponent<RectTransform>());
        foreach (Button button in clip.GetComponentsInChildren<Button>()) EnsureInside(clip, button.GetComponent<RectTransform>());
        RectTransform top = Field<RectTransform>(view, "top");
        float previousRight = float.NegativeInfinity;
        foreach (RectTransform child in top)
        {
            if (!child.gameObject.activeSelf) continue;
            Vector3[] corners = new Vector3[4]; child.GetWorldCorners(corners);
            Require(corners[0].x >= previousRight - 0.5f, "Top controls overlap: " + child.name);
            previousRight = corners[2].x;
            TMP_Text text = child.GetComponent<TMP_Text>();
            if (text && child.name != "Document")
                Require(!text.isTextOverflowing, "Top title text overflows: " + child.name);
        }
        if (state == "catalog")
        {
            RectTransform grid = Field<RectTransform>(view, "catalogGrid");
            Require(grid.childCount == 48, "Actual catalog first page must have 48 entries");
            for (int i = 0; i < grid.childCount; ++i) EnsureInside(grid, (RectTransform)grid.GetChild(i));
        }
        if (state == "settings")
        {
            RectTransform settings = (RectTransform)Field<GameObject>(view, "viewportSettings").transform;
            EnsureInside(view.Viewport, settings);
            foreach (Button button in settings.GetComponentsInChildren<Button>()) EnsureInside(settings, (RectTransform)button.transform);
            foreach (TMP_InputField input in settings.GetComponentsInChildren<TMP_InputField>()) EnsureInside(settings, (RectTransform)input.transform);
        }
        if (state == "outliner") EnsureInside(Field<RectTransform>(view, "outliner"),
            (RectTransform)Field<GameObject>(view, "outlinerMenu").transform);
        if (state == "catalog-blueprints") Require(Field<RectTransform>(view, "catalogGrid").childCount == 2,
            "Blueprint mode should show both stored category examples");
        if (state == "catalog-materials")
        {
            ScrollRect scroll = Field<ScrollRect>(view, "catalogMaterialScroll");
            Require(scroll.verticalNormalizedPosition <= .001f, "Material paging did not reach last items");
            EnsureInside(scroll.viewport, (RectTransform)Field<RectTransform>(view, "catalogCategories").Find("Material_Материал 35"));
        }
    }

    private static void TestSaveRoundtrip(BlueprintEditorController controller, string path)
    {
        float expected = (float)controller.Document.Parts[0].Scale.X;
        Require((bool)Call(controller, "TrySave", false), "Product controller save failed");
        Require(!controller.Document.IsDirty, "Saved document stays dirty");
        var reloaded = new CompositeBlueprintStore(path);
        Require(reloaded.All().Count == 1, "Actual disk library roundtrip");
        Require(Mathf.Abs(reloaded.All()[0].parts[0].scale.x - expected) < 0.0001f, "Store discarded scale");
        CheckReopenedCopy(controller, reloaded, expected);
        checks.Add("Controller save -> disk format9 -> new store -> OpenExisting retains scale and optional blueprint anchor");
    }

    private static void TestScaleBoundaries(BlueprintEditorController controller, string path)
    {
        string selected = controller.Document.Parts[0].StableId;
        foreach (float percent in new[] { 1f, 400f, 125.5f, 126f })
        {
            controller.Document.SelectOnly(selected);
            Call(controller, "SetActiveTool", BlueprintEditorTool.Transform);
            Call(controller, "Bind", "Проверка границ масштаба");
            var input = Field<TMP_InputField>(Field<BlueprintEditorView>(controller, "view"), "inspectorScale");
            input.text = percent.ToString(System.Globalization.CultureInfo.InvariantCulture);
            input.onEndEdit.Invoke(input.text);
            Require(Math.Abs(controller.Document.Parts[0].Scale.X - percent / 100.0) < 0.00001,
                "Actual TMP scale rejects " + percent + "%");
            Require((bool)Call(controller, "TrySave", false), "Saving scale boundary failed");
            var reloaded = new CompositeBlueprintStore(path);
            CheckReopenedCopy(controller, reloaded, percent / 100.0);
        }
        checks.Add("Actual TMP 1%, 400%, 125.5% and 126% -> save and reopen");
    }

    private static void CheckReopenedCopy(BlueprintEditorController original, CompositeBlueprintStore reloaded, double expected)
    {
        // A fresh store belongs to a fresh controller, as at application startup.
        using (var opened = new BlueprintEditorController(reloaded,
            Field<Camera>(original, "cameraTemplate"), Field<TMP_Text>(original, "textTemplate"),
            Field<Func<string, GameObject>>(original, "resolveVisualSource"),
            Field<Func<string, string>>(original, "resolveDisplayName"),
            Field<Func<IReadOnlyList<BlueprintEditorCatalogItem>>>(original, "catalogItems"),
            message => { throw new InvalidOperationException(message); }))
        {
            Require(opened.OpenExisting(reloaded.All()[0], out string error), "New controller reopen: " + error);
            Require(Math.Abs(opened.Document.Parts[0].Scale.X - expected) < 0.00001,
                "New controller discarded persisted scale " + expected);
            opened.Close(true);
        }
    }

    private static void TestModifiers(BlueprintEditorController controller)
    {
        BlueprintEditorDocument doc = controller.Document;
        string selected = doc.Parts[0].StableId;
        string pole = doc.Parts[1].StableId;
        Require(controller.AddPart("wood_pole", "Вторая опора"), "Add second contour support");
        string secondPole = doc.Parts[2].StableId;
        doc.SelectOnly(secondPole);
        Require(controller.ApplyTransformDelta(new Vector3(4,0,0), Quaternion.identity, Vector3.zero), "Separate contour supports");
        doc.SelectOnly(selected);
        Point3 original = doc.Parts[0].Position;
        foreach (BlueprintEditorTool tool in new[] { BlueprintEditorTool.Array, BlueprintEditorTool.Contour })
        {
            Call(controller, "SetActiveTool", tool);
            if (tool == BlueprintEditorTool.Array) Set(controller, "arrayPrimaryAxis", GizmoAxis.X);
            var view = Field<BlueprintEditorView>(controller, "view");
            TMP_InputField step = Field<TMP_InputField>(view,
                tool == BlueprintEditorTool.Array ? "arrayScaleStepX" : "contourScaleStep");
            step.text = "1"; step.onEndEdit.Invoke(step.text);
            Require(Mathf.Abs(Field<float>(controller,
                tool == BlueprintEditorTool.Array ? "arrayScaleStepX" : "contourScaleStep") - 0.01f) < 0.00001f,
                tool + " percentage step did not convert to factor");
            step.text = "0"; step.onEndEdit.Invoke(step.text);
            if (tool == BlueprintEditorTool.Contour)
            {
                var supports = Field<List<string>>(controller, "contourSupportIds");
                supports.Clear(); supports.Add(pole); supports.Add(secondPole);
            }
            Call(controller, "RefreshModifierPreview");
            var scene = Field<BlueprintEditorScene>(controller, "scene");
            var previews = Field<IList>(scene, "contourPreviews");
            Require(previews.Count > 0, tool + " lacks preview");
            Vector3 before = Field<GameObject>(previews[0], "Root").transform.position;
            Vector3 beforeScale = Field<GameObject>(previews[0], "Root").transform.localScale;
            Set(controller, "dragHandle", GizmoHandleKind.Move);
            Set(controller, "dragTranslation", new Vector3(0,0.4f,0));
            Set(controller, "dragScale", 1.1f);
            Set(controller, "dragPivot", new Vector3((float)original.X,(float)original.Y,(float)original.Z));
            Call(controller, "RefreshModifierPreview");
            Vector3 after = Field<GameObject>(previews[0], "Root").transform.position;
            Vector3 afterScale = Field<GameObject>(previews[0], "Root").transform.localScale;
            Require(Vector3.Distance(after - before, new Vector3(0,0.4f,0)) < 0.001f, tool + " does not follow pending drag");
            Require(Vector3.Distance(afterScale, beforeScale * 1.1f) < 0.001f, tool + " loses pending scale");
            Require((doc.Parts[0].Position - original).LengthSquared < 0.000001, tool + " preview mutated source");
            Call(controller, "CancelGizmoDrag");
            Vector3 restored = Field<GameObject>(previews[0], "Root").transform.position;
            Require(Vector3.Distance(restored, before) < 0.001f, tool + " cancel retained pending transform");
        }
        Call(controller, "SetActiveTool", BlueprintEditorTool.Transform);
        checks.Add("Array and contour follow pending move/scale and cancel without changing source");
        checks.Add("Actual modifier TMP percentage fields convert 1% to 0.01");
    }

    private static void TestControllerHandles(BlueprintEditorController controller)
    {
        var doc = controller.Document;
        var scene = Field<BlueprintEditorScene>(controller, "scene");
        var view = Field<BlueprintEditorView>(controller, "view");
        string selected = doc.Parts[0].StableId;
        doc.SelectOnly(selected);
        Call(controller, "SetActiveTool", BlueprintEditorTool.Transform);
        Call(controller, "UpdateGizmo");
        Vector3 pinned = Field<Vector3[]>(controller, "gizmoAnchors")[0];
        Set(controller, "pinnedAnchorWorld", (Vector3?)pinned);
        Call(controller, "UpdateGizmo");
        Vector3[] original = (Vector3[])Field<Vector3[]>(controller, "gizmoAnchors").Clone();
        int pinIndex = Field<int>(controller, "pinnedAnchorPoint");
        Set(controller, "dragSourceAnchors", original);
        Set(controller, "dragHandle", GizmoHandleKind.Rotate);
        Set(controller, "dragPivot", pinned);
        Quaternion turn = Quaternion.AngleAxis(45, Vector3.up);
        Set(controller, "dragRotation", turn);
        List<string> ids = Field<List<string>>(controller, "dragIds");
        ids.Clear(); ids.Add(selected);
        scene.PreviewTransform(doc, ids, Vector3.zero, turn, pinned);
        Call(controller, "UpdateGizmo"); Call(controller, "UpdateGizmo");
        Vector3[] displayed = Field<Vector3[]>(controller, "gizmoAnchors");
        Require(Vector3.Distance(displayed[pinIndex], pinned) < 0.00001f, "Pinned glyph drifted during rotation");
        Require(Vector3.Distance(displayed[4], pinned + turn * (original[4] - pinned)) < 0.00001f,
            "Repeated gizmo refresh moves source glyphs off their transformed positions");
        Call(controller, "CommitGizmoDrag"); Call(controller, "UpdateGizmo");
        Require(Field<Vector3?>(controller, "pinnedAnchorWorld").HasValue &&
            Vector3.Distance(Field<Vector3[]>(controller, "gizmoAnchors")[Field<int>(controller, "pinnedAnchorPoint")], pinned) < 0.00001f,
            "Committed rotation lost fixed world pin");
        Field<Button>(view, "undoButton").onClick.Invoke();
        Require(!Field<Vector3?>(controller, "pinnedAnchorWorld").HasValue, "Undo retained stale world pin");

        Call(controller, "Bind", "Проверка ручки масштаба");
        TMP_InputField scale = Field<TMP_InputField>(view, "inspectorScale");
        scale.text = "400"; scale.onEndEdit.Invoke(scale.text);
        Set(controller, "dragHandle", GizmoHandleKind.Scale);
        Set(controller, "dragStartMouse", Vector2.zero);
        Set(controller, "dragPivot", Vector3.zero);
        ids.Clear(); ids.Add(selected);
        Call(controller, "PreviewGizmoDrag", new Vector2(0,-10000));
        Require(Mathf.Abs(Field<float>(controller, "dragScale") - 0.0025f) < 0.00001f,
            "Scale handle cannot reach 1% from a 400% source");
        Call(controller, "CommitGizmoDrag");
        Require(Math.Abs(doc.Parts[0].Scale.X - 0.01) < 0.00001, "Scale handle commit changed lower limit");
        Call(controller, "Bind", "Проверка ручек завершена");
        scale.text = "126"; scale.onEndEdit.Invoke(scale.text);
        checks.Add("Controller state tests: fixed pin through rotate/commit, Undo clears pin, scale handle 400% -> 1%");
    }

    private static void TestEditorGestures(Camera template, TMP_Text fontTemplate)
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "GestureFixture";
        source.AddComponent<Piece>();
        var localAnchors = new[] { new Vector3(-0.5f,0,-0.5f), new Vector3(0.5f,0,-0.5f),
            new Vector3(0.5f,0,0.5f), new Vector3(-0.5f,0,0.5f) };
        foreach (Vector3 position in localAnchors)
        {
            var point = new GameObject("NativeCorner");
            point.transform.SetParent(source.transform, false);
            point.transform.localPosition = position;
            point.tag = "snappoint";
        }
        source.SetActive(false);
        try
        {
            using (var controller = new BlueprintEditorController(
                new CompositeBlueprintStore(Path.Combine(Application.temporaryCachePath, "gesture-" + Guid.NewGuid().ToString("N") + ".json")),
                template, fontTemplate, _ => source, name => name, () => Array.Empty<BlueprintEditorCatalogItem>(),
                message => { throw new InvalidOperationException(message); }))
            {
                Require(controller.OpenNew(out string error) && controller.AddPart("fixture"), "Gesture controller: " + error);
                var doc = controller.Document;
                var scene = Field<BlueprintEditorScene>(controller, "scene");
                var view = Field<BlueprintEditorView>(controller, "view");
                // Legacy fixed-pixel gesture fixture; model-relative sizing has its own gates.
                scene.ModelSpaceGizmo = false; scene.NativePointsOnly = false;
                string id = doc.Parts[0].StableId;
                Vector3 original = new Vector3((float)doc.Parts[0].Position.X, (float)doc.Parts[0].Position.Y, (float)doc.Parts[0].Position.Z);
                scene.SetViewport(new Rect(0,0,1920,1080));
                scene.SetCameraPose(original + new Vector3(6,5,-8), Quaternion.LookRotation(new Vector3(-6,-5,8)));
                scene.Camera.orthographic = false;
                Require(Field<List<Vector3>>(Field<IDictionary>(scene, "visuals")[id], "SnapLocal").Count == 8 &&
                    source.transform.childCount == 4, "Tagged native rectangle must yield four native/four mids without mutating source");
                Call(controller, "SetActiveTool", BlueprintEditorTool.Array);
                DragArray(controller, GizmoAxis.X, 3, 1);
                Require(Field<int>(controller, "arrayCountX") == 3 && Field<int>(controller, "arrayCountY") == 1,
                    "First golden arrow did not create a 3x1 row");
                Require(Field<Vector3>(controller, "arrayStepX") == Vector3.right,
                    "Array step must use native span, not mouse distance");
                Call(controller, "CommitGizmoDrag");
                DragArray(controller, GizmoAxis.Z, 2, -1);
                Call(controller, "CommitGizmoDrag");
                Require(Field<int>(controller, "arrayCountY") == 2 && Field<Vector3>(controller, "arrayStepY") == Vector3.back &&
                    Field<int>(controller, "arrayPreviewCount") == 5, "Second golden arrow did not create signed 3x2 preview: " +
                    Field<int>(controller, "arrayCountX") + "x" + Field<int>(controller, "arrayCountY") +
                    "; first=" + Field<GizmoAxis>(controller, "arrayPrimaryAxis") + "; step2=" + Field<Vector3>(controller, "arrayStepY") +
                    "; preview=" + Field<int>(controller, "arrayPreviewCount") + "; info=" + Field<TMP_Text>(view, "arrayInfo").text);
                Require(Field<TMP_InputField[]>(view, "arrayCount")[0].text == "3" &&
                    Field<TMP_InputField[]>(view, "arrayCount")[1].text == "2", "Golden gesture did not update actual TMP counts");
                DragArray(controller, GizmoAxis.Y, 2, 1);
                Call(controller, "CancelGizmoDrag");
                Require(Field<Vector3>(controller, "arrayStepY") == Vector3.back,
                    "Cancel did not restore the second array vector");
                DragArray(controller, GizmoAxis.Y, 2, 1);
                Call(controller, "CommitGizmoDrag");
                Require(Field<int>(controller, "arrayCountX") == 3 && Field<int>(controller, "arrayCountY") == 2 &&
                    Field<Vector3>(controller, "arrayStepY") == Vector3.up, "Third axis must replace second vector, not create a third dimension");
                Require(doc.Parts.Count == 1 && Vector3.Distance(original, new Vector3((float)doc.Parts[0].Position.X,
                    (float)doc.Parts[0].Position.Y, (float)doc.Parts[0].Position.Z)) < 0.00001f,
                    "Golden array gesture moved or committed source geometry");
                Field<Button>(view, "arrayApplyButton").onClick.Invoke();
                Require(doc.Parts.Count == 6, "Array Apply did not commit six instances");
                Field<Button>(view, "undoButton").onClick.Invoke();
                Require(doc.Parts.Count == 1, "Array Apply requires more than one Undo");

                Set(controller, "gizmoFamily", GizmoFamily.Points);
                Set(controller, "showAllAnchors", true);
                Call(controller, "UpdateGizmo");
                Require(scene.TryGetGizmoAnchors(new[] { id }, out Vector3[] anchors, out int nativeStart, legacyLayoutAnchors: true) &&
                    anchors.Length - nativeStart == 4 && nativeStart == AnchorAdjustment.SelectableAnchorCount + 4,
                    "Four native points and four generated midpoints must retain distinct provenance");
                foreach (Vector3 native in localAnchors)
                    Require(Array.Exists(anchors, point => Vector3.Distance(point, original + native) < .0001f),
                        "Native snap coordinate was lost by classification");
                Vector3 pin = original + localAnchors[0], moving = original + localAnchors[1];
                Require((bool)Call(controller, "TryBeginGizmoDrag", (Vector2)scene.Camera.WorldToScreenPoint(pin)),
                    "Native anchor cannot be selected through actual hit test");
                Call(controller, "CommitGizmoDrag");
                Call(controller, "UpdateGizmo");
                Field<List<Button>>(view, "anchorPinButtons")[0].onClick.Invoke();
                Field<List<Button>>(view, "anchorPinButtons")[0].onClick.Invoke();
                Require(Field<Vector3?>(controller, "pinnedAnchorWorld").HasValue &&
                    Vector3.Distance(Field<Vector3?>(controller, "pinnedAnchorWorld").Value, pin) < 0.0001f,
                    "Actual pin button did not pin selected native anchor");
                Call(controller, "SetActiveTool", BlueprintEditorTool.Array);
                DragArray(controller, GizmoAxis.X, 3, 1);
                Require(Mathf.Abs(Field<Vector3>(controller, "arrayStepX").x - 1f) < 0.0001f,
                    "Pinning an existing native anchor truncates array span");
                Call(controller, "CancelGizmoDrag");
                Call(controller, "SetActiveTool", BlueprintEditorTool.Transform);
                Button constraint = Field<List<Button>>(view, "anchorConstraintButtons")[2];
                constraint.onClick.Invoke();
                Require(Field<GizmoAxis>(controller, "anchorConstraintAxis") == GizmoAxis.Y, "Rotation axis UI did not select Y");
                Call(controller, "BeginKeyboardPreview", GizmoHandleKind.Rotate);
                Call(controller, "SetKeyboardAxis", GizmoAxis.Y);
                Set(controller, "keyboardNumber", "45");
                Call(controller, "PreviewKeyboardNumber");
                Require(Quaternion.Angle(Field<Quaternion>(controller, "dragRotation"), Quaternion.AngleAxis(45, Vector3.up)) < 0.05f,
                    "Native anchor gesture did not rotate by constrained Y angle");
                Call(controller, "CommitGizmoDrag");
                for (int i = 0; i < 3; ++i) Call(controller, "UpdateGizmo");
                Require(Vector3.Distance(Field<Vector3?>(controller, "pinnedAnchorWorld").Value, pin) < 0.0001f &&
                    Vector3.Distance(Field<Vector3?>(controller, "selectedAnchorWorld").Value, pin) < 0.001f,
                    "Native selected/pinned point jumped after commit and bounds rebuild");
                Field<Button>(view, "undoButton").onClick.Invoke();
                Require(!Field<Vector3?>(controller, "pinnedAnchorWorld").HasValue &&
                    !Field<Vector3?>(controller, "selectedAnchorWorld").HasValue, "Undo retained stale native point selection");
                TMP_InputField scale = Field<TMP_InputField>(view, "inspectorScale");
                scale.text = "1"; scale.onEndEdit.Invoke(scale.text);
                Call(controller, "SetActiveTool", BlueprintEditorTool.Array);
                DragArray(controller, GizmoAxis.X, 2, 1);
                Require(Mathf.Abs(Field<Vector3>(controller, "arrayStepX").x - 0.01f) < 0.00001f &&
                    Field<int>(controller, "arrayCountX") == 2, "1% source array step falls back to one metre or ignores short drag");
                Call(controller, "CancelGizmoDrag");
            }
            TestGroupSnaps(template, source);
        }
        finally { Object.Destroy(source); }
        checks.Add("Actual controller hit/drag/release: native-span first/second array vectors, third-axis replacement, cancel, TMP sync, source unchanged, atomic Apply/Undo; native pin and Y-constrained rotation persist");
    }

    private static void TestGroupSnaps(Camera template, GameObject source)
    {
        foreach (double scale in new[] { 1.0, 0.01 })
        {
            var parts = new List<BlueprintEditorPart>();
            var ids = new[] { "a", "b", "c" };
            for (int index = 0; index < ids.Length; ++index)
                parts.Add(new BlueprintEditorPart(ids[index], "fixture", ids[index], new Point3(index * scale,0,0),
                    new Rotation3(0,0,0,1), "group", scale: new Point3(scale,scale,scale)));
            var doc = new BlueprintEditorDocument(null, "Group native snaps", "Test", parts,
                new[] { new BlueprintEditorGroup("group", "Group") });
            doc.SelectOnly("group");
            using (var scene = new BlueprintEditorScene(template, _ => source))
            {
                Require(scene.TrySync(doc, out string error), "Group snap sync: " + error);
                Require(scene.TryGetGizmoAnchors(ids, out Vector3[] points, out int start, legacyLayoutAnchors: true) &&
                    points.Length - start == 4 && start == AnchorAdjustment.SelectableAnchorCount + 8,
                    "Three-part group must retain four exterior native and eight generated points; scale=" + scale +
                    "; native=" + (points.Length - start) + "; helpers=" + (start - AnchorAdjustment.SelectableAnchorCount));
                for (int index = AnchorAdjustment.SelectableAnchorCount; index < points.Length; ++index)
                    Require(Math.Abs(points[index].x - 0.5 * scale) > 0.0001 &&
                        Math.Abs(points[index].x - 1.5 * scale) > 0.0001,
                        "Shared internal group snap remains visible");
                Require(scene.TryGetGizmoAnchors(ids, out Vector3[] repeated, out int repeatedStart, legacyLayoutAnchors: true) &&
                    repeatedStart == start && repeated.Length == points.Length, "Group snap count changes across refresh");
                for (int index = 0; index < points.Length; ++index)
                    Require(points[index] == repeated[index], "Group snap identity changes across refresh");
                points[start] += Vector3.one * 50f;
                scene.TryGetGizmoAnchors(ids, out points, out start, legacyLayoutAnchors: true);
                Require(points[start] == repeated[start], "Caller mutation corrupts cached group anchors");
                scene.PreviewTransform(doc, ids, Vector3.up * 2f, Quaternion.identity, Vector3.zero);
                scene.TryGetGizmoAnchors(ids, out points, out start, legacyLayoutAnchors: true);
                Require(Vector3.Distance(points[start], repeated[start] + Vector3.up * 2f) < 0.00001f,
                    "Transform preview retains stale group anchor cache");
                scene.TrySync(doc, out _); scene.TryGetGizmoAnchors(ids, out points, out start, legacyLayoutAnchors: true);
                Require(points[start] == repeated[start], "Scene sync did not invalidate preview group anchors");
                doc.SetLocked("b", true); doc.SetVisibility("c", false); scene.TrySync(doc, out _);
                Require(scene.TryGetGizmoAnchors(ids, out points, out start, legacyLayoutAnchors: true) && points.Length - start == 4 &&
                    start == AnchorAdjustment.SelectableAnchorCount + 4,
                    "Hidden/locked members still contribute group native points");
                Require(source.transform.childCount == 4, "Group native extraction mutated source hierarchy");
            }
        }
        var crowded = new List<BlueprintEditorPart>();
        var crowdedIds = new List<string>();
        for (int index = 0; index < BlueprintEditorDocument.MaximumParts; ++index)
        {
            string id = "crowded" + index;
            crowdedIds.Add(id);
            crowded.Add(new BlueprintEditorPart(id, "fixture", id, new Point3(index * 3,0,0), new Rotation3(0,0,0,1)));
        }
        using (var scene = new BlueprintEditorScene(template, _ => source))
        {
            var doc = new BlueprintEditorDocument(null, "Native budget", "Test", crowded);
            Require(scene.TrySync(doc, out string error), error);
            Require(scene.TryGetGizmoAnchors(crowdedIds, out Vector3[] points, out int start, legacyLayoutAnchors: true) &&
                points.Length == AnchorAdjustment.SelectableAnchorCount + 512 && points.Length - start == 512,
                "Generated midpoint helpers must not exhaust the 512 exterior-point budget before native points");
        }
        checks.Add("Native/midpoint provenance: exterior shared joins excluded, stable cache, 100%/1% scale; 128-part budget reserves 512 native points before generated helpers");
    }

    private static void DragArray(BlueprintEditorController controller, GizmoAxis axis, int count, int sign)
    {
        Call(controller, "UpdateGizmo");
        var basis = new object[] { Vector3.zero, Quaternion.identity, null };
        Require((bool)Call(controller, "TrySelectionPivot", basis), "Array selection pivot missing");
        var scene = Field<BlueprintEditorScene>(controller, "scene");
        Vector3 direction = TransformGizmoView.AxisVector(axis, (Quaternion)basis[1], Field<bool>(controller, "localSpace"));
        Vector2 start = scene.Camera.WorldToScreenPoint((Vector3)basis[0] + direction * scene.GizmoScale * 1.25f);
        Require((bool)Call(controller, "TryBeginGizmoDrag", start) &&
            Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Layout && Field<GizmoAxis>(controller, "dragAxis") == axis,
            "Golden " + axis + " arrow did not win actual hit test over source transform");
        float distance = Field<float>(controller, "dragArrayStepLength") * (count - 1) * sign;
        Vector2 end = start + Field<Vector2>(controller, "dragScreenDirection") * distance / Field<float>(controller, "dragWorldUnitsPerPixel");
        Call(controller, "PreviewGizmoDrag", end);
    }

    private static void TestNestedSelection(Camera template, GameObject source)
    {
        var doc = new BlueprintEditorDocument(null, "Nested selection", "Test", new[] {
            new BlueprintEditorPart("a", "wall", "A", new Point3(-3,0,0), new Rotation3(0,0,0,1), "outer"),
            new BlueprintEditorPart("b", "wall", "B", new Point3(3,0,0), new Rotation3(0,0,0,1), "inner") }, new[] {
            new BlueprintEditorGroup("outer", "Outer"),
            new BlueprintEditorGroup("inner", "Inner", parentGroupId: "outer") });
        doc.SelectOnly("outer");
        using (var scene = new BlueprintEditorScene(template, _ => source))
        {
            Require(scene.TrySync(doc, out string error), "Nested sync: " + error);
            var visuals = Field<IDictionary>(scene, "visuals");
            foreach (string id in new[] { "a", "b" })
            {
                object visual = visuals[id];
                Require(Field<bool>(visual, "Selected") && Field<bool>(visual, "ActiveSelection"), "Nested highlight omits " + id);
                foreach (Renderer renderer in Field<Renderer[]>(visual, "Renderers"))
                    Require(!renderer.HasPropertyBlock(), "Selected descendant must retain original materials: " + id);
            }
            Require(scene.TryGetSelectionBounds(doc, out Bounds selected), "Nested selection bounds missing");
            Require(scene.TryGetBounds(new[] { "a", "b" }, out Bounds all), "Nested parts bounds missing");
            Require(Vector3.Distance(selected.center, all.center) < 0.001f && Vector3.Distance(selected.size, all.size) < 0.001f,
                "Nested selection bounds omit descendants");
            doc.SelectOnly("b"); scene.TrySync(doc, out _);
            Require(!Field<bool>(visuals["a"], "Selected") && Field<bool>(visuals["b"], "Selected"), "Leaf selection expands to parent");
        }
        using (var ghost = new PlacementGhostPreviewView())
        {
            ghost.Show(source, new[] {
                new PrecisionPlacementSession.TransformSnapshot(Vector3.zero, Quaternion.identity),
                new PrecisionPlacementSession.TransformSnapshot(Vector3.right, Quaternion.identity, new Vector3(1.2f,0.5f,2f)) }, null);
            var instances = Field<List<GameObject>>(ghost, "instances");
            Require(Vector3.Distance(instances[0].transform.localScale,
                Vector3.Scale(source.transform.lossyScale, new Vector3(1.2f,0.5f,2f))) < 0.001f, "World preview discarded per-part scale");
        }
        checks.Add("Nested descendants have matching bounds and original selection materials; selecting a leaf stays local");
        checks.Add("World ghost preview consumes non-unit XYZ scale");
    }

    private static void TestModelSpaceGizmo(Camera template)
    {
        Camera camera = Object.Instantiate(template);
        camera.pixelRect = new Rect(0, 0, 1920, 1080);
        camera.fieldOfView = 60f;
        using (var gizmo = new TransformGizmoView(screenSpaceSizing: true))
        {
            gizmo.ModelSpaceSizing = true; gizmo.ModelSize = 1.2f;
            Vector3[] points = { Vector3.zero, Vector3.right * .2f };
            foreach (bool orthographic in new[] { false, true })
            {
                camera.orthographic = orthographic;
                float nearPixels = 0f;
                foreach (float distance in new[] { 4f, 16f })
                {
                    camera.orthographicSize = distance;
                    camera.transform.SetPositionAndRotation(new Vector3(0, 0, -distance), Quaternion.identity);
                    gizmo.EditorMouse = new Vector2(-1000, -1000);
                    gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                        GizmoHandleKind.None, GizmoAxis.None, points, 0, -1, 1, false, 1,
                        false, Vector3.zero, false, GizmoAxis.None, false, default, default, 0, allowExtended: true,
                        pointVisibility: _ => false);
                    Require(Mathf.Abs(gizmo.Scale - 1.2f) < .00001f, "3D gizmo depends on camera distance/projection");
                    var line = Field<List<LineRenderer>>(gizmo, "anchorHandles")[0];
                    Require(line.gameObject.activeSelf && line.startColor.a == 1f,
                        "Selected occluded point disappeared after cursor left");
                    Vector2 centre = camera.WorldToScreenPoint(points[0]);
                    float pixels = Vector2.Distance(centre, camera.WorldToScreenPoint(line.GetPosition(1)));
                    Require(gizmo.HitTestAnchor(camera, points, centre + Vector2.right * (pixels - .5f)) == 0,
                        "3D point drawing and picking disagree");
                    Require(gizmo.HitTestAnchor(camera, points, centre + Vector2.up * (pixels + 3f)) < 0,
                        "Far 3D point still captures its old fixed pixel area");
                    if (distance == 4f) nearPixels = pixels;
                    else Require(Mathf.Abs(nearPixels / pixels - 4f) < .001f, "3D point does not shrink with zoom");
                    camera.fieldOfView = 90f;
                    gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                        GizmoHandleKind.None, GizmoAxis.None, points, 0, -1, 1, false, 1,
                        false, Vector3.zero, false, GizmoAxis.None, false, default, default, 0, allowExtended: true);
                    Require(Mathf.Abs(gizmo.Scale - 1.2f) < .00001f, "3D size changes with FOV");
                    camera.fieldOfView = 60f;
                }
            }
            gizmo.NativePointsOnly = true;
            gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, points, 0, -1, 1, true, 1,
                false, Vector3.zero, false, GizmoAxis.None, false, default, default, 0);
            Require(Field<List<LineRenderer>>(gizmo, "anchorHandles")[0].gameObject.activeSelf,
                "Native-only hides the selected helper pivot");
            gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, points, -1, -1, 1, true, 1,
                false, Vector3.zero, false, GizmoAxis.None, false, default, default, 0);
            Require(!Field<List<LineRenderer>>(gizmo, "anchorHandles")[0].gameObject.activeSelf &&
                Field<List<LineRenderer>>(gizmo, "anchorHandles")[1].gameObject.activeSelf,
                "Native-only shows/captures ordinary helpers");
            gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, points, 1, 1, 1, false, 1,
                false, Vector3.zero, false, GizmoAxis.None, false, default, default, 0, helperAnchorOverride: 1);
            Color appended = Field<List<LineRenderer>>(gizmo, "anchorHandles")[1].startColor;
            Require(appended.r > appended.b && appended.g > appended.b && appended.a == 1f,
                "Appended helper A lost ivory provenance behind native boundary");
        }
        Object.DestroyImmediate(camera);
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.transform.localScale = new Vector3(2, 1, .5f); source.SetActive(false);
        var doc = new BlueprintEditorDocument(null, "Model size", "Test", new[] {
            new BlueprintEditorPart("model", "model", "Model", new Point3(0,0,0), new Rotation3(0,0,0,1)) });
        using (var scene = new BlueprintEditorScene(template, _ => source))
        {
            Require(scene.TrySync(doc, out string error), error);
            scene.SetGizmoModelSize(new[] { "model" });
            var gizmo = Field<TransformGizmoView>(scene, "gizmo");
            float reference = gizmo.ModelSize;
            Require(Mathf.Abs(reference - Mathf.Sqrt(5.25f) * .4f) < .0001f,
                "Model reference does not match its actual 3D diagonal");
            doc = new BlueprintEditorDocument(null, "Model size", "Test", new[] {
                new BlueprintEditorPart("model", "model", "Model", new Point3(0,0,0),
                    new Rotation3(0, Math.Sin(Math.PI/8), 0, Math.Cos(Math.PI/8))) });
            Require(scene.TrySync(doc, out error), error); scene.SetGizmoModelSize(new[] { "model" });
            Require(Mathf.Abs(gizmo.ModelSize - reference) < .0001f, "Model rotation changes reference size");
            doc = new BlueprintEditorDocument(null, "Model size", "Test", new[] {
                new BlueprintEditorPart("model", "model", "Model", new Point3(0,0,0),
                    new Rotation3(0, Math.Sin(Math.PI/8), 0, Math.Cos(Math.PI/8)), scale: new Point3(4,4,4)) });
            Require(scene.TrySync(doc, out error), error); scene.SetGizmoModelSize(new[] { "model" });
            Require(Mathf.Abs(gizmo.ModelSize - reference * 4) < .0001f, "Actual model scale does not resize 3D gizmo");
        }
        Object.DestroyImmediate(source);
        checks.Add("Model-space gizmo: fixed metres, 4:1 apparent size, perspective/orthographic, projected hit area, persistent occluded pivot and native-only filter");
    }

    private static void TestExperimentalGeometry(Camera template, TMP_Text fontTemplate)
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh originalMesh = source.GetComponent<MeshFilter>().sharedMesh;
        Mesh mesh = new Mesh();
        Vector3[] vertices = { new Vector3(-1,-.5f,0), new Vector3(0,-.5f,0),
            new Vector3(1,-.25f,0), new Vector3(1,.5f,0), new Vector3(-1,.5f,0) };
        mesh.vertices = vertices; mesh.triangles = new[] { 0,1,4, 1,2,4, 2,3,4 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        source.GetComponent<MeshFilter>().sharedMesh = mesh;
        source.SetActive(false);
        var doc = new BlueprintEditorDocument(null, "Geometry experiment", "Test", new[] {
            new BlueprintEditorPart("model", "model", "Model", new Point3(0,0,0), new Rotation3(0,0,0,1)) });
        using (var scene = new BlueprintEditorScene(template, _ => source))
        {
            scene.SetViewport(new Rect(0,0,1920,1080)); scene.SetCameraPose(new Vector3(0,1,-4), Quaternion.LookRotation(new Vector3(0,-1,4)));
            Require(scene.TrySync(doc, out string error), error);
            scene.NativePointsOnly = false;
            Require(!scene.ExperimentalGeometryPoints, "Geometry experiment must be off initially");
            scene.TryGetGizmoAnchors(new[] { "model" }, out Vector3[] before, out int beforeNative);
            scene.ExperimentalGeometryPoints = true;
            scene.TryGetGizmoAnchors(new[] { "model" }, out Vector3[] expanded, out int expandedNative);
            Require(beforeNative > 4 && scene.GeometryAnchorCount == 1 && expandedNative - beforeNative == 1 &&
                Array.Exists(before, p => p == Vector3.zero),
                "Ordinary bounds/middle helpers must remain; experiment adds only the sharp off-bounds corner");
            Vector3 point = expanded[scene.GeometryAnchorStart];
            Require(point == vertices[2],
                "Broad mesh bend was not ignored");
            var targets = new List<Vector3>(); var flags = new List<bool>();
            Require(scene.TryCursorSnap(Array.Empty<string>(), new[] { point }, new[] { 0 }, 1, 1,
                targets, flags, out _, out Vector3 snapped, out bool native) && !native && snapped == point &&
                targets.Contains(point), "Geometry source/target must share exact feature coordinates");
            scene.NativePointsOnly = true;
            Require(!scene.TryCursorSnap(Array.Empty<string>(), new[] { point }, new[] { 0 }, 1, 1,
                targets, flags, out _, out _, out _), "Native-only allows experimental targets");
            scene.NativePointsOnly = false;
            scene.ShowGizmo(BlueprintEditorTool.Transform, Vector3.zero, Quaternion.identity, false,
                GizmoHandleKind.None, GizmoAxis.None, expanded, -1, expandedNative, true, false, default, false);
            var gizmo = Field<TransformGizmoView>(scene, "gizmo");
            LineRenderer mark = Field<List<LineRenderer>>(gizmo, "anchorHandles")[scene.GeometryAnchorStart];
            Require(mark.enabled && mark.positionCount == 17 && !mark.transform.Find("Engraving").gameObject.activeSelf &&
                mark.startColor.a < .6f &&
                Field<List<float>>(gizmo, "anchorHitRadii")[scene.GeometryAnchorStart] <
                Field<List<float>>(gizmo, "anchorHitRadii")[0],
                "Experimental corner must be a smaller translucent dot, not an ivory knot");
            gizmo.ModelSpaceSizing = false;
            scene.ShowGizmo(BlueprintEditorTool.Transform, Vector3.zero, Quaternion.identity, false,
                GizmoHandleKind.None, GizmoAxis.None, expanded, -1, expandedNative, true, false, default, false);
            Vector2 dotScreen = scene.Camera.WorldToScreenPoint(point);
            Require(scene.HitTestAnchor(expanded, dotScreen + Vector2.right * 4f) == scene.GeometryAnchorStart &&
                scene.HitTestAnchor(expanded, dotScreen + Vector2.right * 8f) == -1,
                "Tiny dot must use its own actual screen-space hit radius, not the ordinary knot radius");
            scene.PreviewTransform(doc, new[] { "model" }, Vector3.up, Quaternion.identity, Vector3.zero);
            var moved = Array.ConvertAll(expanded, p => p + Vector3.up);
            scene.ShowGizmo(BlueprintEditorTool.Transform, Vector3.up, Quaternion.identity, false,
                GizmoHandleKind.None, GizmoAxis.None, moved, -1, expandedNative, true, false, default, false);
            Require(mark.enabled && mark.positionCount == 17 && !mark.transform.Find("Engraving").gameObject.activeSelf,
                "Transform preview lost frozen experimental dot style");
            Require(scene.TrySync(doc, out error), error);
            scene.ExperimentalGeometryPoints = false;
            scene.TryGetGizmoAnchors(new[] { "model" }, out Vector3[] restored, out int restoredNative);
            Require(restoredNative == beforeNative && restored.Length == before.Length,
                "Turning geometry off did not invalidate its cached anchors");
            for (int i = 0; i < before.Length; ++i) Require(restored[i] == before[i], "Geometry option changed the original coordinates");
            Require(source.GetComponent<MeshFilter>().sharedMesh == mesh &&
                mesh.vertices[0] == vertices[0] && doc.Parts[0].Scale.X == 1, "Experiment mutated prefab or document");
        }
        GameObject targetCube = GameObject.CreatePrimitive(PrimitiveType.Cube); targetCube.SetActive(false);
        var snapDoc = new BlueprintEditorDocument(null, "Target provenance", "Test", new[] {
            new BlueprintEditorPart("source", "model", "Source", new Point3(0,0,0), new Rotation3(0,0,0,1)),
            new BlueprintEditorPart("target", "cube", "Target", new Point3(3,0,0), new Rotation3(0,0,0,1)) });
        snapDoc.SelectOnly("source");
        using (var scene = new BlueprintEditorScene(template, name => name == "cube" ? targetCube : source))
        {
            scene.SetViewport(new Rect(0,0,1920,1080)); scene.SetCameraPose(new Vector3(3,0,-4), Quaternion.identity);
            Require(scene.TrySync(snapDoc, out string error), error); scene.ExperimentalGeometryPoints = true;
            scene.NativePointsOnly = false;
            scene.TryGetGizmoAnchors(new[] { "source" }, out _, out _);
            Vector3 target = new Vector3(3,.5f,-.5f);
            scene.PreviewTransform(snapDoc, new[] { "source" }, target - vertices[2], Quaternion.identity, Vector3.zero);
            Require(!(bool)Call(scene, "IsExperimentalTarget", target),
                "An experimental moving source relabeled an ordinary target");
            scene.ShowSnapCandidates(new[] { target }, new[] { false });
            LineRenderer mark = Field<List<LineRenderer>>(Field<TransformGizmoView>(scene, "gizmo"), "snapCandidateHandles")[0];
            Require(!mark.enabled && mark.transform.Find("Engraving").gameObject.activeSelf,
                "Ordinary target must retain its ivory glyph after experimental source alignment");
        }
        Object.DestroyImmediate(targetCube);
        var socket = new GameObject("GeometrySourceSocket"); socket.tag = "snappoint";
        socket.transform.SetParent(source.transform, false);
        socket.transform.localPosition = new Vector3(.5f,.5f,-.5f);
        using (var controller = new BlueprintEditorController(
            new CompositeBlueprintStore(Path.Combine(Application.temporaryCachePath, "geometry-" + Guid.NewGuid().ToString("N") + ".json")),
            template, fontTemplate, _ => source, name => name, () => Array.Empty<BlueprintEditorCatalogItem>(),
            message => { throw new InvalidOperationException(message); }))
        {
            Require(controller.OpenNew(out string error) && controller.AddPart("model"), "Geometry controller: " + error);
            var scene = Field<BlueprintEditorScene>(controller, "scene");
            var before = (List<CompositeBlueprintStore.VectorData>)Call(controller, "BuildBoundsAnchors");
            scene.ExperimentalGeometryPoints = true;
            var after = (List<CompositeBlueprintStore.VectorData>)Call(controller, "BuildBoundsAnchors");
            Require(after.Count == before.Count,
                "Experimental geometry changed serialized blueprint anchor count");
            for (int i = 0; i < before.Count; ++i)
                Require(after[i].x == before[i].x && after[i].y == before[i].y && after[i].z == before[i].z,
                    "Experimental geometry changed serialized blueprint coordinates");
            Call(controller, "BeginKeyboardPreview", GizmoHandleKind.Move);
            Call(controller, "ChooseCursorSource", Field<List<int>>(controller, "keyboardSourceChoices")[0]);
            Call(controller, "CancelGizmoDrag");
            Require(Field<int>(controller, "preferredCursorSource") >= 0, "Geometry source memory fixture did not choose a source");
            var view = Field<BlueprintEditorView>(controller, "view");
            Field<Button>(view, "geometryPointsButton").onClick.Invoke();
            Require(!scene.ExperimentalGeometryPoints && Field<int>(controller, "preferredCursorSource") == -1 &&
                Field<List<string>>(controller, "preferredCursorIds").Count == 0,
                "Geometry toggle retained an index from a different anchor array");
            Call(controller, "BeginKeyboardPreview", GizmoHandleKind.Move);
            Require(Field<int>(controller, "keyboardSourceIndex") == -1,
                "G after geometry toggle captured the wrong remembered point instead of Auto");
            Call(controller, "CancelGizmoDrag");
        }
        Object.DestroyImmediate(source); Object.DestroyImmediate(mesh);
        checks.Add("Geometry experiment: off by default, bounded feature endpoints, exact helper targets, native-only exclusion, cache reset and no prefab/document mutation");
    }

    private static void TestFurnitureCornersAndCursorIntent(Camera template, string output)
    {
        var chair = new GameObject("CornerChair");
        var actualLocal = new List<Vector3>();
        for (int index = 0; index < 6; ++index)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.SetParent(chair.transform, false);
            if (index < 4)
            {
                block.transform.localPosition = new Vector3(index % 2 == 0 ? -.35f : .35f, -.3f, index < 2 ? -.35f : .35f);
                block.transform.localScale = new Vector3(.12f,.7f,.12f);
            }
            else
            {
                block.transform.localPosition = index == 4 ? Vector3.zero : new Vector3(0,.45f,.35f);
                block.transform.localScale = index == 4 ? new Vector3(.9f,.15f,.9f) : new Vector3(.9f,.9f,.12f);
            }
            foreach (Vector3 vertex in block.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                Vector3 point = chair.transform.InverseTransformPoint(block.transform.TransformPoint(vertex));
                if (!actualLocal.Contains(point)) actualLocal.Add(point);
            }
        }
        GameObject buried = GameObject.CreatePrimitive(PrimitiveType.Cube);
        buried.transform.SetParent(chair.transform, false); buried.transform.localScale = Vector3.one * .05f;
        chair.SetActive(false);
        Quaternion rotation = Quaternion.Euler(0,37,12);
        var doc = new BlueprintEditorDocument(null, "Chair corners", "Test", new[] {
            new BlueprintEditorPart("chair", "chair", "Chair", new Point3(0,0,0), new Rotation3(rotation.x,rotation.y,rotation.z,rotation.w)) });
        Mesh combined = null;
        for (int fixture = 0; fixture < 2; ++fixture)
        {
        if (fixture == 1)
        {
            var instances = new List<CombineInstance>();
            foreach (MeshFilter filter in chair.GetComponentsInChildren<MeshFilter>(true))
            {
                instances.Add(new CombineInstance { mesh = filter.sharedMesh,
                    transform = chair.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                filter.GetComponent<Renderer>().enabled = false;
            }
            combined = new Mesh(); combined.CombineMeshes(instances.ToArray());
            chair.AddComponent<MeshFilter>().sharedMesh = combined; chair.AddComponent<MeshRenderer>();
        }
        using (var scene = new BlueprintEditorScene(template, _ => chair))
        {
            scene.SetViewport(new Rect(0,0,1920,1080)); scene.SetCameraPose(new Vector3(0,0,-4), Quaternion.identity);
            Require(scene.TrySync(doc, out string error), error);
            scene.TryGetGizmoAnchors(new[] { "chair" }, out Vector3[] corners, out int nativeStart);
            scene.TryGetGizmoAnchors(new[] { "chair" }, out Vector3[] legacy, out _, legacyLayoutAnchors: true);
            Require(nativeStart == corners.Length && corners.Length == AnchorAdjustment.SelectableAnchorCount &&
                corners.Length == legacy.Length,
                "Turning experiment off must restore ordinary prefab bounds/middle helpers");
            for (int i = 0; i < corners.Length; ++i) Require(corners[i] == legacy[i], "Ordinary helper positions changed");
            scene.ExperimentalGeometryPoints = true;
            scene.TryGetGizmoAnchors(new[] { "chair" }, out Vector3[] detailed, out int detailedNative);
            Require(scene.GeometryAnchorCount > 0, "Chair has no extra mesh corners");
            Require(Array.Exists(detailed, p => Vector3.Distance(Quaternion.Inverse(rotation) * p,
                new Vector3(-.41f,-.65f,-.41f)) < .0001f), "Exposed chair leg corner was removed");
            for (int i = scene.GeometryAnchorStart; i < detailedNative; ++i)
            {
                Vector3 local = Quaternion.Inverse(rotation) * detailed[i];
                Require(actualLocal.Exists(p => Vector3.Distance(p, local) < .0001f),
                    "Experimental chair dot is not an actual mesh vertex");
                Require(!(Mathf.Abs(local.x) < .4499f && Mathf.Abs(local.z) < .4499f && Mathf.Abs(local.y) < .0749f),
                    "A corner buried in the solid seat survived filtering: " + local + " fixture " + fixture);
                Require(!Array.Exists(corners, p => Vector3.Distance(p, detailed[i]) < .005f),
                    "Experimental chair dot overlaps an ordinary helper");
            }
            scene.NativePointsOnly = false; scene.SetGizmoModelSize(new[] { "chair" });
            scene.ShowGizmo(BlueprintEditorTool.Transform, Vector3.zero, rotation, false,
                GizmoHandleKind.None, GizmoAxis.None, detailed, -1, detailedNative, true, false, default, false);
            if (fixture == 0)
            {
                var targetTexture = new RenderTexture(1920,1080,24);
                var otherScenes = new List<GameObject>();
                foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (candidate.name == "BuildWorks_BlueprintEditorScene" && candidate.activeInHierarchy &&
                        candidate != Field<GameObject>(scene, "root"))
                    { otherScenes.Add(candidate); candidate.SetActive(false); }
                try
                {
                    scene.Camera.targetTexture = targetTexture; scene.Camera.Render();
                    SavePixels(targetTexture, Path.Combine(output, "geometry-dots-chair.png"));
                    scene.SetCameraPose(new Vector3(0,0,-2), Quaternion.identity);
                    scene.ShowGizmo(BlueprintEditorTool.Transform, Vector3.zero, rotation, false,
                        GizmoHandleKind.None, GizmoAxis.None, detailed, -1, detailedNative, true, false, default, false);
                    scene.Camera.Render(); SavePixels(targetTexture, Path.Combine(output, "geometry-dots-chair-close.png"));
                }
                finally
                {
                    scene.SetCameraPose(new Vector3(0,0,-4), Quaternion.identity);
                    scene.Camera.targetTexture = null; targetTexture.Release(); Object.DestroyImmediate(targetTexture);
                    foreach (GameObject other in otherScenes) other.SetActive(true);
                }
            }
            var preview = new List<Vector3>(); var provenance = new List<bool>();
            scene.NativePointsOnly = false;
            Vector3 lateCorner = detailed[detailedNative - 1];
            Vector3[] closeGrip = { lateCorner + Vector3.back * .05f };
            Vector2 aim = scene.Camera.WorldToScreenPoint(lateCorner);
            Require(scene.TryCursorSnap(Array.Empty<string>(), closeGrip, new[] { 0 }, 0,0, preview,provenance,
                out _, out Vector3 captured, out _, cursor: aim) && preview.Count == 24 &&
                preview.Exists(p => Vector3.Distance(p, captured) < .0001f),
                "Captured chair corner disappeared when the 24-target preview budget filled");
            scene.ExperimentalGeometryPoints = false;
            scene.TryGetGizmoAnchors(new[] { "chair" }, out Vector3[] restored, out _);
            Require(restored.Length == corners.Length, "Disabling experiment changed ordinary helper count");
            for (int i = 0; i < corners.Length; ++i) Require(restored[i] == corners[i], "Geometry toggle changed an ordinary helper");
        }
        }
        Object.DestroyImmediate(chair);
        if (combined) Object.DestroyImmediate(combined);
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.SetActive(false);
        var socket = new GameObject("NativeSocket"); socket.tag = "snappoint";
        socket.transform.SetParent(source.transform, false);
        doc = new BlueprintEditorDocument(null, "Cursor intent", "Test", new[] {
            new BlueprintEditorPart("target", "target", "Target", new Point3(0,0,0), new Rotation3(0,0,0,1)) });
        using (var scene = new BlueprintEditorScene(template, _ => source))
        {
            scene.SetViewport(new Rect(0,0,1920,1080)); scene.SetCameraPose(new Vector3(0,0,-4), Quaternion.identity);
            Require(scene.TrySync(doc, out string error), error); scene.NativePointsOnly = false;
            var targets = new List<Vector3>(); var flags = new List<bool>();
            Vector3 left = new Vector3(-.5f,.5f,-.5f), right = new Vector3(.5f,.5f,-.5f);
            Vector3[] grip = { new Vector3(0,.5f,-1.2f) };
            Vector2 mouse = scene.Camera.WorldToScreenPoint(left);
            Require(scene.TryCursorSnap(Array.Empty<string>(), grip, new[] { 0 }, 0,1, targets,flags,
                out _, out Vector3 target, out bool native, cursor: mouse) && target == left && !native,
                "Aimed helper outside 0.55m must capture instead of the nearby native socket");
            mouse = scene.Camera.WorldToScreenPoint(right);
            Require(scene.TryCursorSnap(Array.Empty<string>(), grip, new[] { 0 }, 0,1, targets,flags,
                out _, out target, out native, 0,left, cursor: mouse) && target == right && !native,
                "Previous helper capture blocked the explicitly aimed adjacent corner");
            scene.NativePointsOnly = true;
            Require(!scene.TryCursorSnap(Array.Empty<string>(), grip, new[] { 0 }, 0,1, targets,flags,
                out _, out _, out _, cursor: mouse), "Native-only still captures a helper");
        }
        Object.DestroyImmediate(source);
        checks.Add("Ordinary bounds/middle helpers restored; experimental exact chair corners exclude buried geometry/duplicates/broad bends, use translucent dots; aimed capture and adjacent switching");
    }

    private static void TestNativeSocketTargets(Camera template)
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.SetActive(false);
        var socket = new GameObject("InteriorNativeSocket"); socket.tag = "snappoint";
        socket.transform.SetParent(source.transform, false);
        var doc = new BlueprintEditorDocument(null, "Native socket", "Test", new[] {
            new BlueprintEditorPart("socket", "socket", "Socket", new Point3(0,0,0), new Rotation3(0,0,0,1)) });
        using (var scene = new BlueprintEditorScene(template, _ => source))
        {
            scene.SetViewport(new Rect(0,0,1920,1080));
            scene.SetCameraPose(new Vector3(0,0,-4), Quaternion.identity);
            Require(scene.TrySync(doc, out string error), "Native socket sync: " + error);
            var targets = new List<Vector3>(); var nativeFlags = new List<bool>();
            Vector2 mouse = scene.Camera.WorldToScreenPoint(Vector3.zero);
            Require(!scene.IsEditorPointVisible(Vector3.zero), "Socket fixture must be inside its own mesh");
            Require(scene.TryFindEditorSnapTarget(Array.Empty<string>(), mouse, false, targets, nativeFlags,
                out Vector3 target, out bool native) && native && target == Vector3.zero,
                "Native socket inside its own mesh must still be eligible in Game mode");
            foreach (bool flag in nativeFlags) Require(flag, "Game mode leaked a generated midpoint");
        }
        var secondSocket = new GameObject("OuterNativeSocket"); secondSocket.tag = "snappoint";
        secondSocket.transform.SetParent(source.transform, false);
        secondSocket.transform.localPosition = Vector3.right * 2f;
        using (var scene = new BlueprintEditorScene(template, _ => source))
        {
            scene.SetViewport(new Rect(0,0,1920,1080));
            scene.SetCameraPose(new Vector3(0,0,-4), Quaternion.identity);
            Require(scene.TrySync(doc, out string error), error);
            scene.NativePointsOnly = false;
            var targets = new List<Vector3>(); var nativeFlags = new List<bool>();
            Vector2 mouse = scene.Camera.WorldToScreenPoint(Vector3.right);
            Require(!scene.TryFindEditorSnapTarget(Array.Empty<string>(), mouse, false, targets, nativeFlags,
                out _, out _), "Game mode must not attach to a generated midpoint between native sockets");
            Require(scene.TryFindEditorSnapTarget(Array.Empty<string>(), mouse, true, targets, nativeFlags,
                out Vector3 midpoint, out bool midpointNative) && midpoint == Vector3.right && !midpointNative,
                "Mesh mode must restore ordinary midpoint helpers between native sockets");
            Vector3 corner = new Vector3(.5f,.5f,-.5f);
            mouse = scene.Camera.WorldToScreenPoint(corner);
            Require(scene.TryFindEditorSnapTarget(Array.Empty<string>(), mouse, true, targets, nativeFlags,
                out Vector3 target, out bool native) && !native && target == corner,
                "Mesh mode must offer an actual corner instead");
        }
        Object.Destroy(source);
        checks.Add("Game snapping accepts native sockets inside their own mesh, not generated midpoints");
    }

    private static void TestHoverAppearance(Camera template)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.SetActive(false);
        var doc = new BlueprintEditorDocument(null, "Hover appearance", "Test", new[] {
            new BlueprintEditorPart("sphere", "sphere", "Sphere", new Point3(0,0,0), new Rotation3(0,0,0,1)) });
        using (var scene = new BlueprintEditorScene(template, _ => sphere))
        {
            scene.SetViewport(new Rect(0,0,1920,1080));
            scene.SetCameraPose(new Vector3(0,0,-4), Quaternion.identity);
            Require(scene.TrySync(doc, out string error), "Sphere hover sync: " + error);
            object visual = Field<IDictionary>(scene, "visuals")["sphere"];
            Renderer renderer = Field<Renderer[]>(visual, "Renderers")[0];
            Material material = renderer.sharedMaterial;
            Color original = material.color;
            Require(!renderer.HasPropertyBlock(), "Clean visual starts with a hover override");
            scene.SetHovered("sphere", doc);
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Require(!block.isEmpty && block.GetColor("_Color") == new Color(0.6f,0.8f,1f),
                "Hover must use vanilla cyan material override");
            Require(material.color == original, "Hover mutated base material");
            doc.SelectOnly("sphere"); scene.TrySync(doc, out _);
            renderer.GetPropertyBlock(block);
            Require(block.isEmpty && material.color == original,
                "Selected model must retain its material, even under the pointer");
            doc.ClearSelection(); scene.TrySync(doc, out _);
            Require(renderer.HasPropertyBlock(), "Deselecting under pointer did not restore hover");
            scene.SetHovered(null, doc);
            Require(!renderer.HasPropertyBlock() && renderer.sharedMaterial == material && material.color == original,
                "Leaving hover did not restore original renderer material");
            foreach (Transform child in Field<GameObject>(visual, "Root").GetComponentsInChildren<Transform>(true))
                Require(child.name != "GeometryOutline", "Selection outline renderer remains in visual");
        }
        Object.Destroy(sphere);
        checks.Add("Reversible cyan hover on editor copies; selection stays untinted with unchanged material identity, no outlines");
    }

    private static void TestOccluderFade(Camera template, string output)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.localScale = new Vector3(2,2,.2f); wall.SetActive(false);
        Material sourceMaterial = wall.GetComponent<Renderer>().sharedMaterial;
        using (var scene = new BlueprintEditorScene(template, _ => wall))
        {
            var doc = new BlueprintEditorDocument(null, "See-through", "Other", new[] {
                new BlueprintEditorPart("selected","wall","Selected",new Point3(0,1,0),new Rotation3(0,0,0,1)),
                new BlueprintEditorPart("front","wall","Front",new Point3(0,1,-2),new Rotation3(0,0,0,1)),
                new BlueprintEditorPart("behind","wall","Behind",new Point3(0,1,2),new Rotation3(0,0,0,1)),
                new BlueprintEditorPart("side","wall","Side",new Point3(4,1,-2),new Rotation3(0,0,0,1)),
                new BlueprintEditorPart("hidden","wall","Hidden",new Point3(0,1,-4),new Rotation3(0,0,0,1),visible:false) });
            doc.SelectOnly("selected");
            Require(scene.TrySync(doc, out string error), error);
            scene.SetViewport(new Rect(0,0,512,512));
            scene.SetCameraPose(new Vector3(0,1,-10), Quaternion.identity);
            var visuals = Field<IDictionary>(scene, "visuals");
            Renderer front = Field<Renderer[]>(visuals["front"], "Renderers")[0];
            Renderer selected = Field<Renderer[]>(visuals["selected"], "Renderers")[0];
            Material original = front.sharedMaterial, selectionMaterial = selected.sharedMaterial;
            var target = new RenderTexture(512,512,24);
            scene.Camera.targetTexture = target;
            scene.Camera.Render(); SavePixels(target, Path.Combine(output,"see-through-off.png"));
            scene.SetOccluderFade(true);
            Require(front.forceRenderingOff && front.enabled && front.sharedMaterial == original,
                "Front occluder was not fully hidden without changing its material or mesh");
            foreach (string id in new[] {"selected","behind","side","hidden"})
                Require(!Field<bool>(visuals[id], "SeeThrough"), "Non-occluder or selection faded: " + id);
            Require(!selected.forceRenderingOff && selected.sharedMaterial == selectionMaterial && wall.GetComponent<Renderer>().sharedMaterial == sourceMaterial,
                "See-through mutated selection or source prefab materials");
            Vector2 center = scene.Camera.WorldToScreenPoint(new Vector3(0,1,0));
            Require(scene.TryPickPoint(center, out string picked, out _) && picked == "selected",
                "Invisible occluder intercepts the selected part");
            scene.SetHovered("front",doc); scene.UpdateOccluders();
            Require(!front.HasPropertyBlock(), "Hover overrides occluder alpha");
            scene.Camera.Render(); SavePixels(target, Path.Combine(output,"see-through-on.png"));
            scene.SetCameraPose(new Vector3(10,1,0), Quaternion.LookRotation(Vector3.left)); scene.UpdateOccluders();
            Require(!front.forceRenderingOff && front.sharedMaterial == original, "Camera movement leaves stale hidden occluders");
            scene.SetCameraPose(new Vector3(0,1,-10), Quaternion.identity); scene.UpdateOccluders();
            doc.SelectOnly("front"); scene.TrySync(doc,out _); scene.UpdateOccluders();
            Require(!front.forceRenderingOff && front.sharedMaterial == original, "New selection stays hidden");
            doc.SelectOnly("selected"); scene.TrySync(doc,out _); scene.UpdateOccluders();
            scene.SetOccluderFade(false);
            Require(!front.forceRenderingOff && front.sharedMaterial == original, "Turning off does not restore original drawing state");
            scene.SetOccluderFade(true); doc.ClearSelection(); scene.TrySync(doc,out _); scene.UpdateOccluders();
            Require(!front.forceRenderingOff && front.sharedMaterial == original && !doc.IsEffectivelyVisible("hidden") && doc.Parts.Count == 5,
                "Clearing selection leaves transparency or changes document visibility");
            scene.Camera.targetTexture = null; Object.Destroy(target);
        }
        Object.Destroy(wall);
        checks.Add("Editor see-through: F7/button; mesh occluders fully hidden; selection/source solid; camera, selection and off restore drawing; materials and document visibility unchanged");
    }

    private static IEnumerable TestSkin()
    {
        var skin = new BlueprintEditorSkin();
        var textures = new List<Texture2D>();
        var sprites = new List<Sprite>();
        var distinct = new HashSet<string>();
        foreach (BlueprintEditorSkin.Surface style in Enum.GetValues(typeof(BlueprintEditorSkin.Surface)))
        {
            Sprite sprite = skin.Get(style);
            Require(sprite == skin.Get(style), "Skin duplicates shared sprite " + style);
            Require(sprite.border == new Vector4(8,8,8,8), "Skin border is not nine-slice: " + style);
            textures.Add(sprite.texture); sprites.Add(sprite);
            Color32[] pixels = BlueprintEditorSkin.BuildPixels(style);
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; ++i)
            {
                bytes[4*i] = pixels[i].r; bytes[4*i+1] = pixels[i].g;
                bytes[4*i+2] = pixels[i].b; bytes[4*i+3] = pixels[i].a;
            }
            distinct.Add(Convert.ToBase64String(bytes));
            Require(pixels[0].a == 0 && pixels[16*32+16].a == 255, "Skin lost chamfer or opaque center");
        }
        Require(distinct.Count == 9, "UI kit must contain nine distinct surfaces");
        GameObject control = new GameObject("SkinTest", typeof(RectTransform), typeof(Image), typeof(Button));
        Button button = control.GetComponent<Button>();
        Image background = control.GetComponent<Image>();
        skin.Apply(button);
        var pointer = new PointerEventData(EventSystem.current);
        button.OnPointerEnter(pointer);
        Require(background.overrideSprite == skin.Get(BlueprintEditorSkin.Surface.Hover), "Native hover sprite missing");
        button.OnSelect(pointer);
        Require(background.overrideSprite == skin.Get(BlueprintEditorSkin.Surface.Focus), "Keyboard focus sprite missing");
        button.interactable = false;
        Require(background.overrideSprite == skin.Get(BlueprintEditorSkin.Surface.Disabled), "Disabled sprite missing");
        Object.Destroy(control);
        skin.Dispose(); skin.Dispose();
        yield return null;
        foreach (Sprite sprite in sprites) Require(!sprite, "Skin leaks owned sprite");
        foreach (Texture2D texture in textures) Require(!texture, "Skin leaks owned texture");
        checks.Add("Nine shared nine-slice skins, native hover/focus/disabled and complete owned texture disposal");
    }

    private static void TestGizmoHits(Camera camera)
    {
        camera.pixelRect = new Rect(0,0,1920,1080);
        camera.orthographic = false;
        camera.fieldOfView = 60;
        var anchors = new[] { Vector3.zero };
        using (var gizmo = new TransformGizmoView(screenSpaceSizing: true))
        foreach (float distance in new[] { 2f, 50f })
        foreach (bool native in new[] { false, true })
        {
            gizmo.EditorMouse = new Vector2(-1000,-1000);
            camera.transform.SetPositionAndRotation(new Vector3(0,0,-distance), Quaternion.identity);
            gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, anchors, -1, -1, native ? 0 : 1,
                true, 1, false, Vector3.zero, false, GizmoAxis.None, false,
                Vector3.zero, Vector3.zero, 0, allowExtended: true);
            Vector2 center = camera.WorldToScreenPoint(Vector3.zero);
            float hitRadius = native ? 18f : 10.5f;
            Require(gizmo.HitTestAnchor(camera, anchors, center + new Vector2(hitRadius - 0.5f,0)) == 0,
                "Visible anchor edge cannot be hit at distance " + distance);
            Require(gizmo.HitTestAnchor(camera, anchors, center + new Vector2(hitRadius + 0.5f,0)) < 0,
                "Anchor intercepts clicks outside its native/helper target");
            LineRenderer anchor = Field<List<LineRenderer>>(gizmo, "anchorHandles")[0];
            float radius = 0;
            for (int i = 0; i < anchor.positionCount; ++i)
                radius = Mathf.Max(radius, Vector2.Distance(center, camera.WorldToScreenPoint(anchor.GetPosition(i))));
            Require(Mathf.Abs(radius - (native ? 16f : 9f)) < 0.1f,
                "Native/helper anchor glyph is not constant pixels");
            float widthPixels = Vector2.Distance(center,
                camera.WorldToScreenPoint(camera.transform.right * anchor.startWidth));
            Require(widthPixels >= (native ? 2f : 1.5f) - 0.01f &&
                Mathf.Abs(anchor.startColor.a - .55f) <= 1f / 255f,
                "Editor anchor lost its default thickness or visibility: native=" + native +
                "; distance=" + distance + "; widthPixels=" + widthPixels +
                "; alpha=" + anchor.startColor.a);
            LineRenderer scaleHandle = Field<LineRenderer>(gizmo, "scaleHandle");
            Vector2 scale = camera.WorldToScreenPoint((scaleHandle.GetPosition(0) + scaleHandle.GetPosition(2)) * 0.5f);
            Require(gizmo.HitTestExtra(camera, Vector3.zero, Quaternion.identity, false, scale, out _) == GizmoHandleKind.Scale,
                "Uniform scale glyph is not hittable");
            Require(gizmo.HitTestScale(camera, scale + new Vector2(5,4)), "Filled scale interior is not hittable");
            Require(!gizmo.HitTestScale(camera, scale + new Vector2(25,25)), "Scale steals clicks outside its silhouette");
            var artwork = Field<Dictionary<LineRenderer,Mesh>>(gizmo, "artworkMeshes");
            Require(artwork.ContainsKey(anchor) && artwork.ContainsKey(scaleHandle) &&
                anchor.transform.Find("Engraving").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name.EndsWith(
                    native ? "snap-native" : "snap-helper") &&
                scaleHandle.transform.Find("Engraving").GetComponent<MeshRenderer>().sharedMaterial.mainTexture,
                "Snap marker lost its authored provenance artwork or scale lost its original artwork");
            Require(!anchor.enabled && anchor.positionCount == 5,
                "Invisible hit bounds should not duplicate the authored snap artwork");
            Material pointMaterial = anchor.transform.Find("Engraving").GetComponent<MeshRenderer>().sharedMaterial;
            Require(pointMaterial.color == Color.white && pointMaterial.mainTexture.width == 1254 &&
                pointMaterial.mainTexture.filterMode == FilterMode.Trilinear &&
                ((Texture2D)pointMaterial.mainTexture).mipmapCount > 1,
                "Point capture differs from embedded texture size, colour or minification settings");
            Require(anchor.startColor.a >= .55f - 1f / 255f && anchor.startColor.a <= 1f,
                "Snap marker lost provenance brightness during neighbouring handle hover");
            gizmo.EditorMouse = scale + Vector2.right * 40;
            ShowFeedback();
            float nearAlpha = artwork[scaleHandle].colors[0].a;
            Require(nearAlpha > .64f && nearAlpha < 1f, "Scale proximity does not brighten smoothly");
            gizmo.EditorMouse = scale;
            ShowFeedback();
            Require(artwork[scaleHandle].colors[0].a == 1f && anchor.startColor.a < 1f,
                "Scale hover does not exclusively highlight its winner");
            gizmo.EditorMouse = new Vector2(-1000,-1000);
            var helperPoints = new Vector3[9];
            gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, helperPoints, -1, -1, 9,
                true, 1, false, Vector3.zero, false, GizmoAxis.None, false,
                Vector3.zero, Vector3.zero, 0, allowExtended: true);
            Require(Field<List<LineRenderer>>(gizmo, "anchorHandles")[0].transform.Find("Engraving")
                    .GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name.EndsWith("snap-helper") &&
                Field<List<LineRenderer>>(gizmo, "anchorHandles")[8].transform.Find("Engraving")
                    .GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name.EndsWith("snap-helper"),
                "All generated helpers must use one ivory thematic sprite");
            gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, helperPoints, 0, 8, 8,
                true, 1, false, Vector3.zero, false, GizmoAxis.None, false,
                Vector3.zero, Vector3.zero, 0, allowExtended: true);
            for (int index = 0; index < helperPoints.Length; ++index)
                Require(Field<List<LineRenderer>>(gizmo, "anchorHandles")[index].gameObject.activeInHierarchy,
                    "Selecting or pinning a coincident point must not hide other provenance markers");
            Require(Field<List<LineRenderer>>(gizmo, "anchorHandles")[0].transform.Find("Engraving")
                    .GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name.EndsWith("snap-helper") &&
                Field<List<LineRenderer>>(gizmo, "anchorHandles")[0].transform.Find("ActiveAccent").gameObject.activeSelf &&
                Field<List<LineRenderer>>(gizmo, "anchorHandles")[8].transform.Find("PinAccent").gameObject.activeSelf &&
                Field<List<LineRenderer>>(gizmo, "anchorHandles")[8].transform.Find("Engraving").gameObject.activeSelf,
                "Active helper lost its type or appended pin invented native provenance");
            Require(Field<List<LineRenderer>>(gizmo, "anchorHandles")[0].transform.Find("ActiveAccent")
                    .Find("Engraving").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name.EndsWith("snap-selected"),
                "Selected helper does not use the new locking collar artwork");
            gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, new[] { Vector3.zero, Vector3.zero }, -1, -1, 1,
                true, 1, false, Vector3.zero, false, GizmoAxis.None, false,
                Vector3.zero, Vector3.zero, 0, allowExtended: true);
            Require(gizmo.HitTestAnchor(camera, new[] { Vector3.zero, Vector3.zero }, center) == 0 &&
                gizmo.HitTestAnchor(camera, new[] { Vector3.zero, Vector3.zero }, center + Vector2.right * 14f) == 1,
                "Coincident helper centre and native outer rim are not independently pickable");
            void ShowFeedback() => gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, anchors, -1, -1, native ? 0 : 1,
                true, 1, false, Vector3.zero, false, GizmoAxis.None, false,
                Vector3.zero, Vector3.zero, 0, allowExtended: true);
            Vector2 plane = camera.WorldToScreenPoint(new Vector3(0.26f,0.26f,0) * gizmo.Scale);
            Require(gizmo.HitTestExtra(camera, Vector3.zero, Quaternion.identity, false, plane, out GizmoAxis normal) ==
                GizmoHandleKind.MovePlane && normal == GizmoAxis.Z, "XY plane glyph is not hittable");
            foreach (GizmoFamily family in new[] { GizmoFamily.Move, GizmoFamily.Rotate, GizmoFamily.Points, GizmoFamily.Scale })
            {
                gizmo.Family = family;
                gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                    GizmoHandleKind.None, GizmoAxis.None, anchors, -1, -1, native ? 0 : 1,
                    true, 1, false, Vector3.zero, false, GizmoAxis.None, false,
                    Vector3.zero, Vector3.zero, 0, allowExtended: true);
                Require(scaleHandle.gameObject.activeSelf == (family == GizmoFamily.Scale), "Wrong scale family visibility");
                Require(gizmo.HitTestScale(camera, scale) == (family == GizmoFamily.Scale), "Hidden scale captures input");
                Require((gizmo.HitTestAnchor(camera, anchors, center) == 0) == (family == GizmoFamily.Points), "Hidden point family captures input");
                if (family != GizmoFamily.Move)
                    Require(gizmo.HitTestMove(camera, Vector3.zero, Quaternion.identity, false,
                        camera.WorldToScreenPoint(Vector3.right * gizmo.Scale * .7f)) == GizmoAxis.None, "Hidden move captures input");
                if (family != GizmoFamily.Rotate)
                    Require(gizmo.HitTestRotation(camera, Vector3.zero, Quaternion.identity, false, center) == GizmoAxis.None,
                        "Hidden rotation captures input");
            }
            gizmo.Family = GizmoFamily.Combined;
        }
        using (var gizmo = new TransformGizmoView())
        foreach (float distance in new[] { 2f, 50f })
        {
            camera.transform.SetPositionAndRotation(new Vector3(0,0,-distance), Quaternion.identity);
            gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, anchors, -1, -1, 0, true, 1,
                false, Vector3.zero, false, GizmoAxis.None, false, Vector3.zero, Vector3.zero, 0);
            Require(Mathf.Abs(gizmo.Scale - Mathf.Clamp(distance * 0.18f,0.75f,3f)) < 0.0001f,
                "F9 legacy world gizmo sizing changed");
            Vector2 center = camera.WorldToScreenPoint(Vector3.zero);
            Require(gizmo.HitTestAnchor(camera, anchors, center + new Vector2(15.5f,0)) == 0 &&
                gizmo.HitTestAnchor(camera, anchors, center + new Vector2(16.5f,0)) < 0,
                "F9 legacy anchor 16px hit radius changed");
            Vector2 onAxis = camera.WorldToScreenPoint(Vector3.right * gizmo.Scale * 0.7f);
            // Sample below X; above it a distant narrow projection legitimately hits the Y arrow.
            GizmoAxis inside = gizmo.HitTestMove(camera, Vector3.zero, Quaternion.identity, false, onAxis + Vector2.down * 17.5f);
            GizmoAxis outside = gizmo.HitTestMove(camera, Vector3.zero, Quaternion.identity, false, onAxis + Vector2.down * 18.5f);
            Require(inside == GizmoAxis.X && outside == GizmoAxis.None,
                "F9 legacy 18px move-axis hit zone changed: depth=" + distance + "; inside=" + inside +
                "; outside=" + outside + "; axis offset=" + (onAxis - center) + "; pixelRect=" + camera.pixelRect);
        }
        checks.Add("Gizmo: compact thematic sprites at true coordinates; native/helper radius16/9px, unamplified colour; active preserves helper type, appended pin is state only; helper centre/native rim picking; F9 unchanged");
    }

    private static void EnsureInside(RectTransform parent, RectTransform child)
    {
        Vector3[] corners = new Vector3[4]; child.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 local = parent.InverseTransformPoint(corner);
            Rect outer = parent.rect; outer.xMin -= 0.6f; outer.yMin -= 0.6f; outer.xMax += 0.6f; outer.yMax += 0.6f;
            Require(outer.Contains(local), child.name + " is clipped by " + parent.name);
        }
    }

    internal static void SavePixels(RenderTexture target, string path)
    {
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        RenderTexture prior = RenderTexture.active;
        try { RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
        finally { RenderTexture.active = prior; Object.DestroyImmediate(texture); }
    }

    private static Sprite CatalogThumbnail(Camera template, GameObject source)
    {
        var document = new BlueprintEditorDocument(null, "Thumbnail", "Test", new[] {
            new BlueprintEditorPart("part", source.name, source.name, new Point3(0,0,0), new Rotation3(0,0,0,1)) });
        using (var scene = new BlueprintEditorScene(template, _ => source))
        {
            Require(scene.TrySync(document, out string error), "Catalog thumbnail: " + error);
            Require(scene.TryGetDocumentBounds(out Bounds bounds), "Catalog thumbnail bounds missing");
            Quaternion rotation = Quaternion.Euler(12,210,0);
            scene.SetCameraPose(bounds.center - rotation * Vector3.forward * (bounds.size.magnitude + 2), rotation);
            var target = new RenderTexture(128,128,24);
            scene.Camera.targetTexture = target;
            scene.SetViewport(new Rect(0,0,128,128));
            scene.Camera.orthographic = true;
            scene.Camera.orthographicSize = Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z) * 0.7f;
            scene.Camera.Render();
            var texture = new Texture2D(128,128,TextureFormat.RGBA32,false);
            RenderTexture prior = RenderTexture.active;
            try { RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,128,128),0,0); texture.Apply(); }
            finally { RenderTexture.active = prior; scene.Camera.targetTexture = null; Object.Destroy(target); }
            return Sprite.Create(texture, new Rect(0,0,128,128), new Vector2(0.5f,0.5f));
        }
    }

    private static void TestPlacementSurface(Camera template)
    {
        GameObject support = GameObject.CreatePrimitive(PrimitiveType.Cube);
        support.name = "RaisedLockedSupport";
        support.transform.localScale = new Vector3(4,2,4);
        support.SetActive(false);
        GameObject placed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placed.name = "PlacementPreviewSource";
        placed.transform.localScale = new Vector3(0.8f,1.6f,0.6f);
        placed.SetActive(false);
        var doc = new BlueprintEditorDocument(null, "Placement surface", "Test", new[] {
            new BlueprintEditorPart("support", "support", "Support", new Point3(0,3,0), new Rotation3(0,0,0,1)) });
        Require(doc.SetLocked("support", true), "Cannot lock placement fixture support");
        try
        {
            using (var scene = new BlueprintEditorScene(template, name => name == "support" ? support : placed))
            {
                Require(scene.TrySync(doc, out string error), "Placement scene sync: " + error);
                scene.SetViewport(new Rect(0,0,1920,1080));
                scene.SetCameraPose(new Vector3(0,12,0), Quaternion.Euler(90,0,0));
                foreach (bool orthographic in new[] { false, true })
                {
                    scene.Camera.orthographic = orthographic;
                    scene.Camera.orthographicSize = 8;
                    Vector2 overSupport = scene.Camera.WorldToScreenPoint(new Vector3(0,4,0));
                    Require(!scene.TryPick(overSupport, out _), "Locked support remains selectable");
                    Require(scene.TryPlacementSurface(overSupport, out Vector3 surface, out Vector3 normal) &&
                        Vector3.Distance(surface, new Vector3(0,4,0)) < 0.001f,
                        "Placement ray must hit raised locked support, not ground; orthographic=" + orthographic);
                    Require(Vector3.Dot(normal, Vector3.up) > .999f,
                        "Placement surface did not preserve the actual mesh normal");
                    Require(scene.ShowPlacementPreview("placed", surface, normal, Quaternion.Euler(18,25,10), false,
                        out Vector3 finalPosition, out error), "Placement preview: " + error);
                    object preview = Field<object>(scene, "placementPreview");
                    float minimumY = float.PositiveInfinity;
                    foreach (Renderer renderer in Field<Renderer[]>(preview, "Renderers"))
                        minimumY = Mathf.Min(minimumY, renderer.bounds.min.y);
                    Require(Mathf.Abs(minimumY - surface.y) < 0.001f &&
                        Vector3.Distance(finalPosition, surface) > .01f,
                        "Free rotated preview did not rest its real mesh on the support surface");
                    Vector3 missedSupport = new Vector3(6, scene.GroundHeight, 0);
                    Require(scene.TryPlacementSurface(scene.Camera.WorldToScreenPoint(missedSupport), out Vector3 ground) &&
                        Vector3.Distance(ground, missedSupport) < 0.001f,
                        "Placement ray missing support must fall back to ground; orthographic=" + orthographic);
                }
            }
        }
        finally { Object.Destroy(support); Object.Destroy(placed); }
        checks.Add("Placement ray returns the raised locked support normal; rotated preview rests its mesh on that surface; missed ray falls back to ground in perspective/orthographic");
    }

    private static void TestContourNativeEdges(Camera template)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.localScale = new Vector3(2,2,.2f);
        foreach (Vector3 point in new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
            new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0) })
        {
            var snap = new GameObject("NativeSnap");
            snap.tag = "snappoint";
            snap.transform.SetParent(wall.transform, false);
            snap.transform.localPosition = point;
        }
        wall.SetActive(false);
        var doc = new BlueprintEditorDocument(null, "Contour edges", "Test", new[] {
            new BlueprintEditorPart("source", "source", "Source", new Point3(-3,1,0), new Rotation3(0,0,0,1)),
            new BlueprintEditorPart("wall0", "wall", "Wall", new Point3(0,1,0), new Rotation3(0,0,0,1)),
            new BlueprintEditorPart("wall1", "wall", "Wall", new Point3(2,1,0), new Rotation3(0,0,0,1)),
            new BlueprintEditorPart("wall2", "wall", "Wall", new Point3(4,1,0), new Rotation3(0,0,0,1)) });
        doc.SelectOnly("source");
        try
        {
            using (var scene = new BlueprintEditorScene(template, _ => wall))
            {
                Require(scene.TrySync(doc, out string error), "Contour fixture sync: " + error);
                scene.SetViewport(new Rect(0,0,1920,1080));
                scene.SetCameraPose(new Vector3(2,6,-9), Quaternion.LookRotation(new Vector3(0,-4,9)));
                Vector2 edge = scene.Camera.WorldToScreenPoint(new Vector3(0,1.96f,-.1f));
                Require(scene.TryFindContourSupports(doc, edge, out List<string> supports, out _, out _,
                    out string warning) && supports.Count == 3,
                    "Generated midpoint snaps must not split the connected three-wall contour: " + warning);
            }
        }
        finally { Object.Destroy(wall); }
        checks.Add("Native edges connect three adjacent walls despite generated midpoint snap points");
    }

    private static void TestUnreadablePicking(Camera template)
    {
        var mesh = new Mesh { name = "UnreadableRing" };
        mesh.vertices = new[] { new Vector3(-2,-2,0), new Vector3(2,-2,0), new Vector3(2,2,0),new Vector3(-2,2,0),
            new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0) };
        mesh.triangles = new[] {0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
        mesh.RecalculateBounds(); mesh.RecalculateNormals(); mesh.UploadMeshData(true);
        Require(!mesh.isReadable, "Unreadable fixture retained CPU mesh");
        Require(BlueprintEditorMeshData.TryRead(mesh, out Vector3[] vertices, out int[] indices, out string error) &&
            vertices.Length == 8 && indices.Length == 24, "Actual GPU mesh readback: " + error);
        GameObject source = new GameObject("RingSource", typeof(MeshFilter), typeof(MeshRenderer));
        source.GetComponent<MeshFilter>().sharedMesh = mesh;
        source.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"));
        source.SetActive(false);
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "WallBehindRing";
        wall.transform.localScale = new Vector3(6,6,0.1f);
        wall.SetActive(false);
        var doc = new BlueprintEditorDocument(null, "GPU ring", "Test", new[] {
            new BlueprintEditorPart("ring", "ring", "Ring", new Point3(0,0,0), new Rotation3(0,0,0,1)) });
        using (var scene = new BlueprintEditorScene(template, name => name == "ring" ? source : wall))
        {
            Require(scene.TrySync(doc, out error), "Unreadable scene sync: " + error);
            scene.SetViewport(new Rect(0,0,1920,1080));
            scene.SetCameraPose(new Vector3(0,0,-8), Quaternion.identity);
            Require(!scene.TryPick(scene.Camera.WorldToScreenPoint(Vector3.zero), out _), "Empty mesh hole must not pick bounds");
            Require(scene.TryPick(scene.Camera.WorldToScreenPoint(new Vector3(1.5f,1.5f,0)), out string id) && id == "ring",
                "Unreadable ring surface must be pickable");
            doc.AddPart(new BlueprintEditorPart("wall", "wall", "Wall", new Point3(0,0,1), new Rotation3(0,0,0,1)));
            Require(scene.TrySync(doc, out error), "Overlapping scene: " + error);
            foreach (bool orthographic in new[] { false, true })
            {
                scene.Camera.orthographic = orthographic;
                scene.Camera.orthographicSize = 3f;
                Require(scene.TryPick(scene.Camera.WorldToScreenPoint(Vector3.zero), out id) && id == "wall",
                    "Hole must pass through to wall; orthographic=" + orthographic);
                Require(scene.TryPick(scene.Camera.WorldToScreenPoint(new Vector3(1.5f,1.5f,0)), out id) && id == "ring",
                    "Decorative surface must beat overlapping wall; orthographic=" + orthographic);
                Require(!scene.IsEditorPointVisible(new Vector3(1.5f,1.5f,0.5f)),
                    "Occluded snap candidate admitted; orthographic=" + orthographic);
                Require(scene.IsEditorPointVisible(new Vector3(0,0,0.5f)),
                    "Open hole incorrectly occludes snap target; orthographic=" + orthographic);
            }
        }
        Object.Destroy(wall); Object.Destroy(source); Object.Destroy(mesh);
        checks.Add("Unreadable GPU mesh surface beats wall; hole selects wall; snap occlusion in perspective/orthographic");
    }
}
