using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private static Action repaintGameView;
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
        public string graphicRaycastResolution = "1280x720,1920x1080,2560x1440,3440x1440 at 100/120/140%; exact GameView size, native ScreenSpaceOverlay raycasts as in Valheim; camera mode only for screenshot composition";
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
            repaintGameView?.Invoke();
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

    private static Action UseGameViewSize(int width, int height)
    {
        // Unity 6000.0.61f1 verified editor API. In-memory only: do not save user sizes.
        var asm = typeof(EditorWindow).Assembly;
        Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
        object sizes = asm.GetType("UnityEditor.ScriptableSingleton`1").MakeGenericType(sizesType)
            .GetProperty("instance").GetValue(null);
        object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
        Type groupType = group.GetType();
        int index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        object size = Activator.CreateInstance(asm.GetType("UnityEditor.GameViewSize"),
            Enum.Parse(asm.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, "BuildWorks Acceptance");
        groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
        var window = EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"), false, "BuildWorks Acceptance", false);
        var selection = window.GetType().GetMethod("SizeSelectionCallback");
        int original = (int)window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(window);
        selection.Invoke(window, new object[] { index, null }); window.Focus(); window.Repaint();
        // Editor updates are not GameView repaints in batch mode. Render the real
        // fixed-size view before each fixture step instead of fabricating Screen values.
        MethodInfo repaint = typeof(EditorWindow).GetMethod("RepaintImmediately", Private);
        repaintGameView = () => repaint.Invoke(window, null);
        Type playModeType = asm.GetType("UnityEditor.PlayModeView");
        object main = playModeType.GetMethod("GetMainPlayModeView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Debug.Log("BUILDWORKS_MAINVIEW " + ((EditorWindow)main).GetInstanceID() + " " + main.GetType().Name +
            " target=" + playModeType.GetProperty("targetSize", Private).GetValue(main));
        foreach (EditorWindow other in Resources.FindObjectsOfTypeAll(asm.GetType("UnityEditor.GameView")))
            Debug.Log("BUILDWORKS_GAMEVIEW " + other.GetInstanceID() + " selected=" +
                other.GetType().GetProperty("selectedSizeIndex").GetValue(other) + " target=" +
                other.GetType().GetProperty("targetRenderSize", Private).GetValue(other) + " window=" + other.position);
        return () => { repaintGameView = null; selection.Invoke(window, new object[] { original, null }); groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { index }); };
    }

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
        EnsureSnapTag();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(PendingRun, true);
        EditorApplication.EnterPlaymode();
    }

    internal static void EnsureSnapTag()
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
            TestSignPayload();
            TestPlacementSupportFrame();
            TestImportedAxes(template, fontTemplate, output);
            TestPinnedScaleBasis(template, fontTemplate, output);
            checks.Add(StorageCaptureAcceptance.Run());
            foreach (object step in ItemStandPreviewAcceptance.Run(template, output)) yield return step;
            checks.Add("Supplied world mounts: small/native-holder/passive/heavy renderer paths use own quality/variant and per-stack allocation; live inventory changes refresh preview, confirmation binds exact own item and pause refuses changed/construction-spent items without fallback. Inert API fixtures only; real world/reload/network not tested.");
            checks.Add("Actual ItemStand world adapter on inert API fixture: 28 owner-only synchronous attachment cases; supplied-item properties for both archived snapshots and recipes, stack1/N, count allocation, orientation/late-owner-loss queue cancellation, access/ownership/no-item/wrong-type, false native acknowledgement, mid-use owner/item change, exceptions before/after consumption, exact consumed-package attribution/changed valid payload, oversized recipe/hash collision rejection, unspent clone cleanup/delayed cleanup, missing attachment, abnormal consumption, foreign queue isolation and denied/rebound/foreign-holder cleanup guards. Native world placement, drops, save/reload and multiplayer remain unverified.");
            checks.Add("Actual ItemStand tools -> compatible item catalogue -> one click -> replacement Cancel/confirm -> full-package Undo -> detach -> immediate empty-stand attach -> native save/reopen; cleared contents hide tree/tools/visuals, preserve raw evidence and Undo restores; stale locked stand rejected. No inventory/world creation.");
            checks.Add("Actual read-only ItemStand capture: native view override, stable item hash, exact v109 package and immutable full properties; metadata/hash mismatch, corrupt/oversized payload refused; mid-read revision changes, invalid/rebound views and newly populated empty/changed legacy holders refused; captured contents visible in renderer-only scene. No native ItemData.Load, inventory or world reconstruction executed.");
            checks.Add("Actual ItemStand renderer-only adapter: horizontal/wall offsets, quality/variant, nested/duplicate anchors, holder XYZ scale/rotation, selection/move/Undo, temporary copies and content cache replacement; invalid/missing contents explain failure without data loss; thumbnail/rotatable preview, source/material/script isolation. Native inventory/world/armour remain unverified.");
            checks.Add(UICatalogAcceptance.Run(fontTemplate.font));
            foreach (object step in ControllerInputAcceptance.Run(template, fontTemplate, output)) yield return step;
            foreach (object step in ControllerInputAcceptance.RunSupportLifecycle(template, fontTemplate, output)) yield return step;
            foreach (object step in ControllerInputAcceptance.RunExchangeLifecycle(template, fontTemplate, output)) yield return step;
            foreach (object step in ControllerInputAcceptance.RunContourLifecycle(template, fontTemplate, output)) yield return step;
            checks.Add("Actual contour input: hover guide without copies, leave clears, click previews, cancel/tool switch/delete clear guide permanently");
            checks.Add("Actual Controller.Update/LateUpdate frame chords and native EventSystem raycasts: Ctrl+A, Shift rows/viewport, pin, shared gizmo, Alt move preview/commit/cancel/Undo, scale, placement RMB catalog reopen/camera and compound import; single Outliner context subscriptions; Array arrows/wheel/Ctrl zoom/Fit/spacing/shared step presets/live scrub before release/all TMP profile/atomic apply");
            TestEditorGestures(template, fontTemplate);
            foreach (object step in BlueprintScenePerformanceAcceptance.Run(template, output)) yield return step;
            foreach (object step in StagedEditorAcceptance.Run(template, fontTemplate, output)) yield return step;
            checks.Add("1025-part actual editor: staged load/progress/cancel/reopen/failure, bounded virtual tree, missing-record move/Undo/Redo/save/reopen and stale-revision rejection. Unity Workbench only.");
            checks.Add("Bounded actual woodwall scenes: 128/512 parts, sync/resync/mesh-pick timings, material counts and disposal; not large-scene/FPS proof.");
            TestNativeSocketTargets(template);
            TestRotatedPlacementFrames(template);
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
                    i < 93 ? "Деталь " + i : i == 93 ? "Workbench — layout fixture" : i == 94 ? "Upgrade — layout fixture" : "Device — layout fixture",
                    thumbnails[i % 2 == 0 ? "woodwall" : "wood_pole"], "Строительство",
                    "Материал " + (i % 36).ToString("00"), "Ванильное", i,
                    section: i < 93 ? "building" : "crafting", family: "walls",
                    stationId: i == 93 || i == 94 ? "$fixture_workbench" : null,
                    stationName: i == 93 || i == 94 ? "Workbench — fixture" : null,
                    stationIcon: thumbnails["woodwall"], isStation: i == 93, isUpgrade: i == 94));
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
                TMP_InputField scale = Field<TMP_InputField[]>(view, "inspectorScale")[0];
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
                view.RootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                view.RootCanvas.worldCamera = uiCamera;
                view.RootCanvas.planeDistance = 10f;
                foreach (Transform child in view.RootCanvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                int count = 0;
                foreach (int width in new[] { 1280, 1920, 2560, 3440 })
                foreach (float uiScale in new[] { 1f, 1.2f, 1.4f })
                {
                    int height = width == 1280 ? 720 : width == 1920 ? 1080 : 1440;
                    Action restoreScreen = UseGameViewSize(width, height);
                    yield return null; yield return null;
                    Require(Screen.width == width && Screen.height == height,
                        "GameView size mismatch: " + Screen.width + "x" + Screen.height + " instead of " + width + "x" + height);
                    var target = new RenderTexture(width, height, 24);
                    uiCamera.pixelRect = new Rect(0, 0, width, height);
                    uiCamera.ResetAspect(); uiCamera.ResetProjectionMatrix();
                    view.SetUiScale(uiScale);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    Require(Screen.width == width && Screen.height == height,
                        "Size/layout step changed actual GameView size: " + Screen.width + "x" + Screen.height);
                    try { view.Tick(); }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        throw new InvalidOperationException("Layout fixture not ready: requested=" + width + "x" + height +
                            " scale=" + uiScale + " screen=" + Screen.width + "x" + Screen.height +
                            " safe=" + Screen.safeArea + " display=" + view.RootCanvas.renderingDisplaySize +
                            " root=" + Field<RectTransform>(view, "safeRoot").rect, ex);
                    }
                    Canvas.ForceUpdateCanvases();
                    scene.SetViewport(view.ViewportScreenRect());
                    Call(controller, "FrameAll");
                    foreach (string state in new[] { "select", "transform", "scale-xyz-ru", "array", "contour", "catalog",
                        "outliner", "outliner-filter-ru", "outliner-fields-ru", "outliner-empty-ru", "interior-camera-ru", "settings", "catalog-blueprints", "catalog-materials", "catalog-quick", "catalog-crafting", "catalog-managed-ru", "catalog-hidden-ru", "delete-confirm-ru", "hide-confirm-ru", "save-metadata", "save-as", "save-as-ru", "supports", "exchange-import", "exchange-import-ru", "exchange-import-path-ru", "exchange-export", "exchange-contents", "exchange-contents-ru", "exchange-import-review", "exchange-import-review-ru", "exchange-armor", "exchange-armor-ru", "exchange-missing", "exchange-missing-ru", "replace-tools", "replace-confirm", "replace-confirm-ru", "replace-entry-ru", "replace-help-ru" })
                    {
                        Require(Screen.width == width && Screen.height == height,
                            "GameView changed before " + state + ": " + Screen.width + "x" + Screen.height);
                        view.HideCatalog();
                        view.HideOutlinerMenu();
                        view.HideViewportSettings();
                        view.HideDialog();
                        view.HideToolsMenu();
                        document.SelectSupport(null);
                        scene.ModelSpaceGizmo = state == "supports";
                        if (state == "supports")
                        {
                            Call(controller, "SelectSupport", "saved-support");
                            Call(controller, "FrameSelectionOrAll");
                        }
                        BlueprintEditorTool tool = state == "array" ? BlueprintEditorTool.Array :
                            state == "contour" ? BlueprintEditorTool.Contour :
                            state == "select" ? BlueprintEditorTool.Select : BlueprintEditorTool.Transform;
                        Call(controller, "SetActiveTool", tool);
                        if (state == "outliner-filter-ru" || state == "outliner-fields-ru" || state == "outliner-empty-ru")
                        {
                            BuildWorksLocalization.RussianLanguage = true;
                            Call(view, "RefreshOutlinerFilter");
                            if (state == "outliner-empty-ru") Field<TMP_InputField>(view, "outlinerSearch").text = "Нет такого объекта";
                            else
                            {
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "OutlinerFilter");
                                Canvas.ForceUpdateCanvases();
                                if (state == "outliner-fields-ru") Field<GameObject>(view, "outlinerMenu").GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
                            }
                        }
                        else if (state == "interior-camera-ru")
                        {
                            BuildWorksLocalization.RussianLanguage = true;
                            ControllerInputAcceptance.NativeClick(view, uiCamera, "ViewportSectionButton0");
                            yield return null;
                            ControllerInputAcceptance.NativeClick(view, uiCamera, "InteriorCamera");
                            yield return null;
                            ControllerInputAcceptance.NativeClick(view, uiCamera, "ViewportSectionButton0");
                            yield return null;
                        }
                        else if (state == "scale-xyz-ru")
                        {
                            BuildWorksLocalization.RussianLanguage = true;
                            document.SelectOnly(selected);
                            Call(controller, "Bind", (object)null);
                            Field<Button>(view, "inspectorProportionsButton").onClick.Invoke();
                            Canvas.ForceUpdateCanvases();
                            var fields = Field<TMP_InputField[]>(view, "inspectorScale");
                            var button = Field<Button>(view, "inspectorProportionsButton");
                            Require(fields.Length == 3 && fields.All(field => field.IsInteractable()) &&
                                button.GetComponentInChildren<TMP_Text>().text == BuildWorksLocalization.Text("editor.scale.independent"),
                                "XYZ fields/mode unavailable at target resolution");
                            Vector3[] corners = new Vector3[4];
                            foreach (var field in fields)
                            {
                                ((RectTransform)field.transform).GetWorldCorners(corners);
                                float bottom = corners[0].y;
                                ((RectTransform)button.transform).GetWorldCorners(corners);
                                Require(bottom >= corners[1].y - .1f, "XYZ fields overlap the proportions control");
                            }
                        }
                        else if (state == "catalog-managed-ru" || state == "catalog-hidden-ru" || state == "delete-confirm-ru" || state == "hide-confirm-ru")
                        {
                            BuildWorksLocalization.RussianLanguage = true;
                            var external = ExternalBlueprintLibrary.Parse("#Name:External\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\n", "archive/source.blueprint");
                            external.name = "Внешний чертёж";
                            var owned = new CompositeBlueprintStore.Blueprint {
                                id = Field<CompositeBlueprintStore.Blueprint>(controller, "sourceBlueprint").id,
                                name = new string('W', 48), category = "Декор" };
                            view.ShowCatalog(new[] {
                                new BlueprintEditorCatalogItem("owned-layout", owned.name, thumbnails["woodwall"], owned.category, "Чертёж", "Мои чертежи", 0, owned),
                                new BlueprintEditorCatalogItem("external-layout", external.name, thumbnails["woodwall"], external.category, "Чертёж", "Внешняя библиотека", 1,
                                    external, unavailableReason: "Недостающие детали: qa_missing. Мод не определён.", hidden: state == "catalog-hidden-ru") });
                            yield return null;
                            ControllerInputAcceptance.NativeClick(view, uiCamera, "CatalogBlueprintsMode", true); yield return null;
                            ControllerInputAcceptance.NativeClick(view, uiCamera, "CatalogReset"); yield return null;
                            ControllerInputAcceptance.NativeClick(view, uiCamera, "CatalogAtlas"); yield return null;
                            if (state == "catalog-hidden-ru")
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "Category_hidden");
                            else if (state == "delete-confirm-ru" || state == "hide-confirm-ru")
                                Call(controller, "ManageCatalogBlueprint", state == "delete-confirm-ru" ? owned : external);
                        }
                        else if (state.StartsWith("catalog"))
                        {
                            Call(controller, "RequestCatalog");
                            yield return null;
                            Canvas.ForceUpdateCanvases();
                            ControllerInputAcceptance.NativeClick(view, uiCamera,
                                state == "catalog-blueprints" ? "CatalogBlueprintsMode" : "CatalogPartsMode",
                                true);
                            yield return null;
                            ControllerInputAcceptance.NativeClick(view, uiCamera, "CatalogReset");
                            yield return null;
                            ControllerInputAcceptance.NativeClick(view, uiCamera,
                                state == "catalog-quick" ? "CatalogQuick" : "CatalogAtlas");
                            yield return null;
                            TMP_InputField page = Field<TMP_InputField>(view, "catalogPageInput");
                            page.SetTextWithoutNotify("1");
                            page.onEndEdit.Invoke("1");
                            yield return null;
                            if (state == "catalog-materials")
                            {
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "CatalogMaterials");
                                Field<RectTransform>(view, "catalogPicker").GetComponentInChildren<ScrollRect>().verticalNormalizedPosition = 0;
                            }
                            if (state == "catalog-crafting") ControllerInputAcceptance.NativeClick(view, uiCamera, "Category_crafting");
                        }
                        else if (state == "outliner") ControllerInputAcceptance.NativeClick(view, uiCamera, "OutlinerMenuButton");
                        else if (state == "settings") ControllerInputAcceptance.NativeClick(view, uiCamera, "View");
                        else if (state == "save-metadata") view.ShowSaveMetadata("Новый чертёж", "Дома",
                            new[] { "Дома", "Мебель" }, (_, __) => { });
                        else if (state.StartsWith("save-as", StringComparison.Ordinal))
                        {
                            BuildWorksLocalization.RussianLanguage = state.EndsWith("-ru", StringComparison.Ordinal);
                            view.ShowSaveMetadata("Новая копия", "Дома", new[] { "Дома", "Декор" }, (_, __) => { }, saveCopy: true);
                        }
                        else if (state.StartsWith("exchange"))
                        {
                            string folder = Path.Combine(output, "exchange-layout"); Directory.CreateDirectory(folder);
                            var layoutStore = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
                            var blueprint = store.All()[0];
                            if (state.StartsWith("exchange-missing", StringComparison.Ordinal))
                            {
                                var unit = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
                                layoutStore.ConfigureImportScale(name => name.StartsWith("qa_missing", StringComparison.Ordinal)
                                    ? (CompositeBlueprintStore.VectorData?)null : unit);
                                string missingText = "#Name:Missing policy\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\nArmorStand;;2;0;0;0;0;0;1;\"2:1:HelmetIron:3:\";1;1;1\n";
                                for (int index = 0; index < 9; ++index)
                                    missingText += "qa_missing_" + index + ";;" + (index + 3) + ";0;0;0;0;0;1;\"\";1;1;1\n";
                                blueprint = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(
                                    ExternalBlueprintLibrary.Parse(missingText, "missing-policy.blueprint"),
                                    name => name.StartsWith("qa_missing", StringComparison.Ordinal) ? (CompositeBlueprintStore.VectorData?)null : unit));
                            }
                            if (state.StartsWith("exchange-armor", StringComparison.Ordinal))
                            {
                                blueprint = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.Parse(
                                    "#Name:Armor policy\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\nArmorStand;;2;0;0;0;0;0;1;\"2:1:HelmetIron:3:\";1;1;1",
                                    "armor-policy.blueprint"), _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }));
                                for (int index = 0; index < 8; ++index)
                                {
                                    var extra = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.Parse(
                                        blueprint.sourceDocuments[0].text, "armor-extra-" + index + ".blueprint"),
                                        _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }));
                                    blueprint.parts.Add(extra.parts[1]); blueprint.sourceDocuments.AddRange(extra.sourceDocuments);
                                }
                            }
                            File.WriteAllBytes(Path.Combine(folder, "Дом.buildworks.json"), BlueprintExchangeFormat.Encode(blueprint));
                            BuildWorksLocalization.RussianLanguage = state.EndsWith("-ru", StringComparison.Ordinal);
                            view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, layoutStore,
                                !state.StartsWith("exchange-import", StringComparison.Ordinal) ? blueprint : null, closed));
                            for (int i = 0; i < 300 && Field<System.Threading.Tasks.Task<string[]>>(
                                Field<BlueprintExchangeView>(view, "exchangeView"), "listing") != null; ++i)
                            { view.Tick(); yield return null; }
                            if (state == "exchange-import-path-ru")
                            {
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "ExchangeEditPath"); yield return null;
                                var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                                var pathInput = Field<TMP_InputField>(exchange, "directory");
                                string fullPath = "C:\\Users\\nfspr\\AppData\\Roaming\\Thunderstore Mod Manager\\DataFolder\\Valheim\\profiles\\TerrainRamp-1.0-Test\\BepInEx\\config\\BuildWorks\\test-fixtures";
                                pathInput.SetTextWithoutNotify(fullPath);
                                pathInput.DeactivateInputField(); yield return null;
                                pathInput.textComponent.ForceMeshUpdate();
                                Require(pathInput.textComponent.GetPreferredValues(fullPath, pathInput.textViewport.rect.width, 0).y <= pathInput.textViewport.rect.height,
                                    "Expanded Russian path does not fit at " + width + "x" + height + " scale=" + uiScale);
                            }
                            else if (state.StartsWith("exchange-contents", StringComparison.Ordinal) || state.StartsWith("exchange-armor", StringComparison.Ordinal) ||
                                state.StartsWith("exchange-missing", StringComparison.Ordinal))
                            {
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "ExchangeSubmit");
                                var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                                for (int i = 0; i < 300 && Field<System.Threading.Tasks.Task<CompositeBlueprintStore.ContentsSummary>>(
                                    exchange, "contentsInspection") != null; ++i)
                                { view.Tick(); yield return null; }
                                Require(Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary") != null,
                                    "Contents screenshot missed completed inspection");
                                if (state.StartsWith("exchange-armor", StringComparison.Ordinal))
                                {
                                    ControllerInputAcceptance.NativeClick(view, uiCamera, "ExchangeArmorChoice");
                                    Require(Field<bool>(exchange, "omitArmorState"), "Armor screenshot missed explicit omission");
                                }
                                if (state.StartsWith("exchange-missing", StringComparison.Ordinal))
                                {
                                    ControllerInputAcceptance.NativeClick(view, uiCamera, "ExchangeMissingChoice"); yield return null;
                                    ControllerInputAcceptance.NativeClick(view, uiCamera, "ExchangeArmorChoice"); yield return null;
                                    Require(Field<bool>(exchange, "omitMissingParts") && Field<bool>(exchange, "omitArmorState"),
                                        "Missing screenshot missed independent explicit omission choices");
                                }
                            }
                            else if (state.StartsWith("exchange-import-review", StringComparison.Ordinal))
                            {
                                var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                                int index = Array.IndexOf(Field<string[]>(exchange, "entries"), Path.Combine(folder, "Дом.buildworks.json"));
                                Require(index >= 0 && index < 7, "Import review layout source unavailable");
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "ExchangeEntry" + index); view.Tick();
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "ExchangeSubmit");
                                for (int i = 0; i < 300 && Field<System.Threading.Tasks.Task<CompositeBlueprintStore.ImportReview>>(
                                    exchange, "importInspection") != null; ++i)
                                { view.Tick(); yield return null; }
                                Require(Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary") != null,
                                    "Import screenshot missed completed inspection");
                            }
                        }
                        else if (state == "replace-tools" || state == "replace-entry-ru" || state == "replace-help-ru")
                        {
                            BuildWorksLocalization.RussianLanguage = state.EndsWith("-ru", StringComparison.Ordinal);
                            Require(controller.AddPart("qa_absent", "Отсутствующая деталь"), "Missing layout fixture was not added");
                            Call(controller, "Bind", (object)null);
                            yield return null;
                            if (state == "replace-tools") view.ShowToolsMenu(new Vector2(width * .5f, height * .15f), false);
                            else if (state == "replace-help-ru")
                                ControllerInputAcceptance.NativeClick(view, uiCamera, "InspectorReplaceMissing");
                        }
                        else if (state.StartsWith("replace-confirm", StringComparison.Ordinal))
                        {
                            BuildWorksLocalization.RussianLanguage = state.EndsWith("-ru", StringComparison.Ordinal);
                            view.ShowConfirmation(BuildWorksLocalization.Text("editor.replace_title"),
                                BuildWorksLocalization.Text("editor.replace_confirm", new string('W', 128), 50000, new string('W', 64) + "…"),
                                () => { }, height: 450f, scenePreview: true);
                        }
                        else Call(controller, "UpdateGizmo");
                        // Product Destroy() and TMP rebuilds must see a real frame.
                        yield return null;
                        Call(controller, "RefreshContextHints");
                        if (state == "replace-tools") view.Tick();
                        Canvas.ForceUpdateCanvases();
                        ValidateControls(view, state);
                        if (state == "supports")
                        {
                            GameObject panel = Field<GameObject>(view, "supportEditPanel");
                            Require(panel.activeInHierarchy, "Support inspector absent");
                            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>())
                            {
                                text.ForceMeshUpdate();
                                if (!text.GetComponentInParent<TMP_InputField>())
                                    Require(!text.isTextOverflowing, "Support text overflows: " + text.name);
                            }
                            ScrollRect scroll = panel.GetComponentInParent<ScrollRect>();
                            Require(scroll && scroll.vertical && !scroll.horizontal, "Small-screen support fields are unreachable");
                            scroll.verticalNormalizedPosition = 1;
                            Canvas.ForceUpdateCanvases();
                            ControllerInputAcceptance.NativeClick(view, uiCamera, "ApplySupport");
                        }
                        // Do not leave offscreen targets attached across a real GameView
                        // frame: batch-mode Screen can fall back to the editor window size.
                        uiCamera.targetTexture = target;
                        scene.Camera.targetTexture = target;
                        view.RootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                        Canvas.ForceUpdateCanvases();
                        scene.Camera.Render();
                        uiCamera.Render();
                        string name = state + "-" + width + "x" + height + "-" + Mathf.RoundToInt(uiScale * 100);
                        SavePixels(target, Path.Combine(output, name + ".png"));
                        ++count;
                        uiCamera.targetTexture = null;
                        scene.Camera.targetTexture = null;
                        view.RootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        if (state == "replace-tools") ControllerInputAcceptance.NativeClick(view, uiCamera, "ToolsEntry0");
                        if (state.StartsWith("exchange")) ControllerInputAcceptance.NativeClick(view, uiCamera, "ExchangeClose");
                        if (state == "scale-xyz-ru") Field<Button>(view, "inspectorProportionsButton").onClick.Invoke();
                        if (state == "replace-tools" || state == "replace-entry-ru" || state == "replace-help-ru")
                        {
                            view.HideDialog(); view.HideToolsMenu();
                            Field<Button>(view, "undoButton").onClick.Invoke();
                        }
                        if (state == "outliner-filter-ru" || state == "outliner-fields-ru" || state == "outliner-empty-ru")
                            Call(view, "ResetOutlinerFilter");
                        if (state == "interior-camera-ru")
                            ControllerInputAcceptance.NativeClick(view, uiCamera, "InteriorCamera");
                        BuildWorksLocalization.RussianLanguage = false;
                        yield return null;
                    }
                    Object.DestroyImmediate(target);
                    restoreScreen();
                }
                TestUnreadablePicking(template);
                MaterialVariationAcceptance.Run(template, source["woodwall"]);
                checks.Add("Seeded renderer-only/editor previews: native descriptor API fixture, zero/max seed, nested scope, source isolation and hover restore; native API parity is checked separately. Heavy batching/world restoration remain pending.");
                TestPlacementSurface(template);
                TestContourNativeEdges(template);
                TestNestedSelection(template, source["woodwall"]);
                TestHoverAppearance(template);
                TestOccluderFade(template, output);
                TestGizmoHits(template);
                TestModelSpaceGizmo(template);
                LargeGizmoAcceptance.Run(template, output);
                TestExperimentalGeometry(template, fontTemplate);
                WorldSnapPointAcceptance.Run(output);
                WorldSnapCollectionAcceptance.Run(output);
                LargeSnapPersistenceAcceptance.Run(output);
                HammerSnapSearchAcceptance.Run(output);
                GizmoQueryAcceptance.Run(template, output);
                HeavyBlueprintAcceptance.Run(output);
                MaterialVariationAcceptance.RunPlacement(output);
                NativeMaterialPreviewAcceptance.Run(output);
                checks.Add("Material placement preview: small cache/error isolation, clones/batched/fallback real GPU equality for uniform and instanced shader parameters, growing/multiple flush batches, native tint/scalar collision and 50000 distinct seeds on one template with bounded buffers/preparation/cache pruning. Native ghost/ZDO/conversion/game shaders/FPS remain open.");
                checks.Add("Heavy world preview: 513/5000/39487/50000 parts, bounded shared renderer templates; exact nested/root/XYZ matrices; independently camera-isolated instanced/fallback images; hidden holder retains mounted item and differing inventory packages share only visuals. Native world placement/FPS remain unverified.");
                TestFurnitureCornersAndCursorIntent(template, output);
                controller.Close(true);
                Require(controller.OpenNew(out error), "Reopen: " + error);
                Require(Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Select,
                    "Reopen must reset active tool");
                controller.Close(true);
                checks.Add("Reopening clears active operation");
                TestExternalImportEditor(template, fontTemplate, source, output);
                TestIncompleteExternalEditor(template, fontTemplate, source["woodwall"], output);
                TestPendingMaterialSeedEditor(template, fontTemplate, source["woodwall"], output);
                Action restoreSingleScreen = UseGameViewSize(1920, 1080);
                try
                {
                    yield return null;
                    foreach (object step in ControllerInputAcceptance.RunSinglePart(template, fontTemplate, source["woodwall"], output)) yield return step;
                    foreach (object step in ControllerInputAcceptance.RunSaveAs(template, fontTemplate, source["woodwall"], output)) yield return step;
                    foreach (object step in ControllerInputAcceptance.RunCatalogManagement(template, fontTemplate, source["woodwall"], output)) yield return step;
                    checks.Add("Catalogue management: actual owned delete/hide/cancel/unavailable restore/write refusal; open draft Undo/Redo/close prompt/new ID Save/reload; source immutable. No game proof.");
                    checks.Add("Save As native UI preserves original, supported data and copy identity through cancel, failure, retry and reopen");
                    foreach (object step in ControllerInputAcceptance.RunMissingExchange(template, fontTemplate, source["woodwall"], output)) yield return step;
                    foreach (object step in ControllerInputAcceptance.RunPrefabMetadata(template, fontTemplate, output)) yield return step;
                    foreach (object step in ControllerInputAcceptance.RunMissingReplacement(template, fontTemplate, source["woodwall"], output)) yield return step;
                    foreach (object step in ControllerInputAcceptance.RunMissingPositioning(template, fontTemplate, source["woodwall"], output)) yield return step;
                    foreach (object step in ControllerInputAcceptance.RunPartialThumbnails(template, fontTemplate, source["woodwall"], output)) yield return step;
                    foreach (object step in ControllerInputAcceptance.RunOutlinerFilters(fontTemplate, output)) yield return step;
                    checks.Add("Explicit missing replacement: actual Tools button/Cancel/review/commit, one Undo/Redo, stale prefab refusal, exact source/poses and native Save/reopen. No game construction proof.");
                    checks.Add("Prefab metadata: actual nested root-local capture/fresh save, unknown source owner, missing recorded frame pose/scale, no snap/model surfaces, native Save and disk reopen; no game proof.");
                    checks.Add("Explicit missing-part copy: actual default/ID/count/Back reset/native confirmation/stale host refusal/new confirmation -> one-part editor; exact source retained. No game construction proof.");
                    checks.Add("Single-part editor: actual Save/metadata clicks -> disk reopen preserves one row/name/scale; empty editor save refused. Native single-part construction/debit/reload remain unverified.");
                }
                finally { restoreSingleScreen(); }
                foreach (object step in ControllerInputAcceptance.RunMissingCatalogue(template, fontTemplate, source["woodwall"], output)) yield return step;
                checks.Add("Reviewed missing-prefab import -> production catalogue status -> actual warning card/native click -> editor insertion/Undo/Redo/save/disk reopen preserves both records, pending basis and source; unknown costs/stations not shown as free. Native Session producer wiring verified separately by HostContract; no world construction proof.");
                checks.Add("Incomplete foreign blueprint: known nonunit prefab plus missing absolute-scale part, constant uncertainty marker without surface/snap geometry; edit/copy/Undo/native save/reopen/export/reimport preserve source and basis; late nonunit prefab resolves exactly once using original native identity. Not world placement proof.");
                Require(runtimeErrors.Count == 0, "Runtime logged errors: " + string.Join("; ", runtimeErrors));
                yield return null;
                ActualDividerAcceptance.Run(output);
                string nativeProof = File.ReadAllText(Path.Combine(output, "native-interaction-check.txt"));
                string assetProof = File.ReadAllText(Path.Combine(output, "actual-divider-check.txt"));
                Require(assetProof.Contains("PASS actual external pipeline;"), "Real external/native-asset pipeline failed; see actual-divider-check.txt");
                checks.Add("Real BiomeBlueprints CoffeeTable/Forest Hut bytes: installed native prefab scales/meshes, actual editor edit/free support/save/reopen/export/reimport and renderer-only world preview. Two extra asset images; native construction/world reload/network unverified.");
                File.WriteAllLines(Path.Combine(output, "acceptance-checks.txt"), checks);
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
        if (state == "replace-entry-ru" || state == "replace-help-ru")
        {
            var button = Field<Button>(view, "replaceMissingButton");
            Require(button.gameObject.activeInHierarchy && button.interactable, "Persistent replacement entry is unavailable");
            Require(button.GetComponentInChildren<TMP_Text>().text == BuildWorksLocalization.Text("editor.replace_short"),
                "Replacement entry missed the current Russian language");
            EnsureInside(Field<RectTransform>(view, "inspector"), (RectTransform)button.transform);
            Vector3[] buttonCorners = new Vector3[4], clipCorners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(buttonCorners); clip.GetWorldCorners(clipCorners);
            Require(buttonCorners[0].y >= clipCorners[1].y - .5f, "Replacement entry overlaps inspector content");
            var positioning = Field<Button>(view, "positionMissingButton");
            Require(positioning.gameObject.activeInHierarchy && positioning.interactable &&
                positioning.GetComponentInChildren<TMP_Text>().text == BuildWorksLocalization.Text("editor.position_short"),
                "Persistent positioning entry is unavailable or has wrong language");
            EnsureInside(Field<RectTransform>(view, "inspector"), (RectTransform)positioning.transform);
            Vector3[] positionCorners = new Vector3[4];
            ((RectTransform)positioning.transform).GetWorldCorners(positionCorners);
            Require(positionCorners[0].x >= buttonCorners[3].x && positionCorners[0].y >= clipCorners[1].y - .5f,
                "Positioning entry overlaps replacement or inspector content");
            if (state == "replace-help-ru")
            {
                Require(view.HasModal && !Field<TMP_Text>(view, "modalMessage").isTextOverflowing,
                    "Replacement instructions are absent or overflow");
                EnsureInside(Field<RectTransform>(view, "safeRoot"), (RectTransform)Field<TMP_Text>(view, "modalTitle").transform.parent);
            }
        }
        foreach (TextMeshProUGUI text in view.RootCanvas.GetComponentsInChildren<TextMeshProUGUI>())
        {
            if (string.IsNullOrWhiteSpace(text.text)) continue;
            text.ForceMeshUpdate();
            Require(text.font && text.font.material && text.font.material.shader.isSupported,
                "Text has no supported material: " + text.name);
            Require(text.textInfo.characterCount > 0, "Text generated no characters: " + text.name);
            bool cardCaption = text.name == "Label" && text.transform.parent.name.StartsWith("Catalog_", StringComparison.Ordinal);
            if (cardCaption)
                Require(text.overflowMode == TextOverflowModes.Ellipsis && text.maxVisibleLines <= 3 &&
                    text.transform.parent.GetComponent<BlueprintEditorHoverTarget>(),
                    "Long catalogue name lacks bounded ellipsis and full card hover");
            if (text.GetComponentInParent<Button>() && !cardCaption)
                Require(!text.isTextOverflowing, "Button text overflows: " + text.text);
            if (text.name == "ContourInfo" || text.name == "ArrayInfo")
                Require(!text.isTextOverflowing, "Tool instructions overflow: " + text.name);
            if (text.name == "PreviewName") Require(text.overflowMode == TextOverflowModes.Ellipsis && text.maxVisibleLines == 3,
                "Long preview name lacks bounded ellipsis");
            if (text.name == "PreviewMetadata")
                Require(!text.isTextOverflowing, "Catalog preview text overflows: " + text.name);
            if (text.name == "EmptyPreview")
                Require(!text.isTextOverflowing, "Empty-image explanation overflows");
            if (text.name == "FilterHelp") Require(!text.isTextOverflowing, "Outliner filter instructions overflow");
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
            RectTransform panel = Field<RectTransform>(view, "catalogPanel");
            EnsureInside(Field<RectTransform>(view, "safeRoot"), panel);
            Vector3[] panelCorners = new Vector3[4], statusCorners = new Vector3[4];
            panel.GetWorldCorners(panelCorners); Field<RectTransform>(view, "status").GetWorldCorners(statusCorners);
            Require(panelCorners[0].y >= statusCorners[2].y - .5f, "Catalog overlaps expanded context hints");
            RectTransform grid = Field<RectTransform>(view, "catalogGrid");
            Require(grid.childCount == Math.Min(96, Field<BlueprintCatalogLayout>(view, "catalogLayout").PageSize),
                "Actual catalog does not fill its adaptive page");
            for (int i = 0; i < grid.childCount; ++i) EnsureInside(grid, (RectTransform)grid.GetChild(i));
        }
        if (state == "settings" || state == "interior-camera-ru")
        {
            RectTransform settings = (RectTransform)Field<GameObject>(view, "viewportSettings").transform;
            EnsureInside(view.Viewport, settings);
            foreach (Button button in settings.GetComponentsInChildren<Button>()) EnsureInside(settings, (RectTransform)button.transform);
            foreach (TMP_InputField input in settings.GetComponentsInChildren<TMP_InputField>()) EnsureInside(settings, (RectTransform)input.transform);
            if (state == "interior-camera-ru")
            {
                Require(!Field<TMP_Text>(view, "interiorHelp").isTextOverflowing &&
                    !Field<TMP_Text>(view, "interiorCameraHint").isTextOverflowing,
                    "Russian interior camera instructions overflow");
                Require(Field<TMP_Text>(view, "interiorCameraHint").gameObject.activeSelf,
                    "Interior camera mode has no persistent visible indication");
            }
        }
        if (state == "outliner") EnsureInside(Field<RectTransform>(view, "outliner"),
            (RectTransform)Field<GameObject>(view, "outlinerMenu").transform);
        if (state == "outliner-filter-ru" || state == "outliner-fields-ru")
        {
            var menu = (RectTransform)Field<GameObject>(view, "outlinerMenu").transform;
            EnsureInside(Field<RectTransform>(view, "safeRoot"), menu);
            Require(menu.rect.height >= 200, "Filter choices remain trapped in the short tree viewport");
            var rows = Field<RectTransform>(view, "outlinerMenuRows");
            foreach (Button choice in rows.GetComponentsInChildren<Button>())
            {
                Require(((RectTransform)choice.transform).rect.height <= 28.1f,
                    "Filter choice still uses an oversized row");
                Require(choice.GetComponentInChildren<TMP_Text>().fontSize <= 12.1f,
                    "Filter choice font remains oversized");
            }
            Require(menu.rect.height <= LayoutUtility.GetPreferredHeight(rows) + 12.1f,
                "Filter menu reserves empty space below its content");
        }
        if (state == "catalog-blueprints") Require(Field<RectTransform>(view, "catalogGrid").childCount == 2,
            "Blueprint mode should show both stored category examples");
        if (state == "catalog-managed-ru" || state == "catalog-hidden-ru")
        {
            var grid = Field<RectTransform>(view, "catalogGrid");
            Require(grid.childCount == (state == "catalog-hidden-ru" ? 1 : 2), "Management visibility filter lost entries");
            foreach (Transform card in grid)
                Require(card.Find("CatalogManage"), "Management button missing at target resolution");
            EnsureInside(Field<RectTransform>(view, "safeRoot"), Field<RectTransform>(view, "catalogPanel"));
        }
        if (state == "delete-confirm-ru" || state == "hide-confirm-ru")
        {
            TMP_Text message = Field<TMP_Text>(view, "modalMessage");
            Require(message.GetPreferredValues(message.text, message.rectTransform.rect.width, 0).y <= message.rectTransform.rect.height,
                "Catalogue confirmation truncates source/draft explanation");
            EnsureInside(Field<RectTransform>(view, "safeRoot"), (RectTransform)message.transform.parent);
        }
        if (state == "catalog-crafting") Require(Field<RectTransform>(view, "catalogGrid").childCount == 3,
            "Crafting fixture must expose station, upgrade and device");
        if (state == "catalog-materials")
        {
            ScrollRect scroll = Field<RectTransform>(view, "catalogPicker").GetComponentInChildren<ScrollRect>();
            Require(scroll.verticalNormalizedPosition <= .001f, "Material paging did not reach last items");
            EnsureInside(scroll.viewport, (RectTransform)scroll.content.Find("CatalogOption_Материал 35"));
        }
        if (state == "replace-tools")
        {
            EnsureInside(Field<RectTransform>(view, "viewport"), Field<RectTransform>(view, "toolsPanel"));
            Require(Field<GameObject>(view, "toolsMenu").transform.GetSiblingIndex() >
                Field<RectTransform>(view, "status").GetSiblingIndex(), "Tools menu is behind the context hints after Tick");
        }
        if (state.StartsWith("replace-confirm", StringComparison.Ordinal))
        {
            TMP_Text message = Field<TMP_Text>(view, "modalMessage");
            Require(message.GetPreferredValues(message.text, message.rectTransform.rect.width, 0).y <= message.rectTransform.rect.height,
                "Replacement confirmation truncates long ID/name");
        }
        if (state == "save-metadata" || state.StartsWith("replace-confirm", StringComparison.Ordinal)) EnsureInside(Field<RectTransform>(view, "safeRoot"),
            (RectTransform)Field<TMP_Text>(view, "modalTitle").transform.parent);
        if (state.StartsWith("exchange"))
        {
            var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
            var panel = Field<RectTransform>(exchange, "panel");
            EnsureInside(Field<RectTransform>(view, "safeRoot"), panel);
            foreach (Button button in panel.GetComponentsInChildren<Button>()) EnsureInside(panel, (RectTransform)button.transform);
            foreach (TMP_InputField field in panel.GetComponentsInChildren<TMP_InputField>()) EnsureInside(panel, (RectTransform)field.transform);
            if (state.StartsWith("exchange-armor", StringComparison.Ordinal) || state.StartsWith("exchange-missing", StringComparison.Ordinal))
            {
                var contents = Field<RectTransform>(exchange, "contentsPanel");
                var details = Field<TMP_Text>(exchange, "contentsDetails");
                var viewport = details.rectTransform.parent.GetComponent<RectTransform>();
                EnsureInside(contents, viewport);
                EnsureInside(contents, (RectTransform)Field<Button>(exchange, "armorChoice").transform);
                if (state.StartsWith("exchange-missing", StringComparison.Ordinal))
                    EnsureInside(contents, (RectTransform)Field<Button>(exchange, "missingChoice").transform);
                Require(viewport.GetComponent<RectMask2D>() && viewport.GetComponent<ScrollRect>() &&
                    details.rectTransform.rect.height > viewport.rect.height, "Armor loss list is not scrollable and clipped");
                var warning = Field<TMP_Text>(exchange, "message");
                Require(warning.GetPreferredValues(warning.text, warning.rectTransform.rect.width, 0).y <= warning.rectTransform.rect.height,
                    "Armor loss warning is truncated");
            }
        }
    }

    private static void TestSignPayload()
    {
        GameObject root = new GameObject("Sign payload test", typeof(Piece), typeof(ZNetView), typeof(Sign));
        var provider = Splatform.PlatformManager.DistributionPlatform.PrivilegeProvider;
        try
        {
            Piece piece = root.GetComponent<Piece>();
            Sign sign = root.GetComponent<Sign>();
            ZNetView view = root.GetComponent<ZNetView>();
            Require(BlueprintSignText.CanApply(piece, null), "Absent sign payload rejected");
            Require(!BlueprintSignText.CanApply(piece, new string('x', 51)) &&
                !BlueprintSignText.CanApply(piece, "Bad\u0001text"), "Unsafe/over-limit sign accepted");
            Require(BlueprintSignText.TryCapture(piece, out string original) && original == "Default", "Default sign text lost");
            view.GetZDO().Set(ZDOVars.s_text, "Дом\n<b>Ostrix</b>");
            Require(BlueprintSignText.TryCapture(piece, out original) && original == "Дом\n<b>Ostrix</b>", "Raw sign text changed");
            typeof(Sign).GetField("m_isViewable", Private).SetValue(sign, false);
            Require(!BlueprintSignText.TryCapture(piece, out _), "Hidden sign text exposed");
            typeof(Sign).GetField("m_isViewable", Private).SetValue(sign, true);
            provider.Granted = false;
            Require(!BlueprintSignText.TryApply(root, "New") && sign.Calls == 0, "Denied privilege called native sign setter");
            provider.Granted = true;
            sign.Denied = true;
            Require(!BlueprintSignText.TryApply(root, "New") && view.GetZDO().GetString(ZDOVars.s_text, "") == original,
                "Native denial mutated sign or reported success");
            sign.Denied = false;
            Require(BlueprintSignText.TryApply(root, "") && view.GetZDO().GetString(ZDOVars.s_text, "Missing") == "",
                "Explicit empty sign did not persist");
            view.Valid = false;
            Require(!BlueprintSignText.TryCapture(piece, out _) && !BlueprintSignText.TryApply(root, "Invalid"), "Invalid ZDO accepted");
            view.Valid = true;
            UnityEngine.Object.DestroyImmediate(sign);
            Require(!BlueprintSignText.CanApply(piece, "Unsupported") && !BlueprintSignText.TryApply(root, "Unsupported"),
                "Non-sign piece accepted text");
            checks.Add("Actual sign adapter on Unity objects: raw UTF-8/default capture, hidden/invalid denial, native limits, platform denial, setter result mismatch and explicit blank. Host Sign permissions/reload/network remain game acceptance.");
        }
        finally { provider.Granted = true; UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void TestPlacementSupportFrame()
    {
        var blueprint = new CompositeBlueprintStore.Blueprint();
        Require(!BlueprintPlacementFrame.TryResolve(blueprint, out Vector3 origin, out Quaternion orientation) &&
            origin == Vector3.zero && orientation == Quaternion.identity, "Legacy placement frame changed");
        blueprint.supports = new List<CompositeBlueprintStore.Support>
        {
            new CompositeBlueprintStore.Support
            {
                stableId = "entrance", name = "Вход",
                position = new CompositeBlueprintStore.VectorData(new Vector3(1, 2, 3)),
                rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(23, 61, -17))
            }
        };
        blueprint.placementSupportId = "entrance";
        Require(BlueprintPlacementFrame.TryResolve(blueprint, out origin, out orientation), "Saved support frame unresolved");
        foreach (float factor in new[] { 0.5f, 1f, 2f })
        {
            BlueprintPlacementFrame.ToRootPose(new Vector3(10, 20, 30), Quaternion.Euler(0, 0, 90),
                new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0), factor,
                out Vector3 position, out Quaternion rotation);
            Require(Vector3.Distance(position, new Vector3(10 + 2 * factor, 20 + 3 * factor, 30 - factor)) < 0.0001f &&
                Quaternion.Angle(rotation, new Quaternion(0.5f, -0.5f, 0.5f, 0.5f)) < 0.05f,
                "Support pose inverse/order/uniform-scale compensation is wrong");
        }
        GameObject root = new GameObject("Placement support proof");
        try
        {
            Transform child = new GameObject("Part").transform;
            child.SetParent(root.transform, false);
            child.localPosition = new Vector3(4, -2, 6);
            child.localRotation = Quaternion.Euler(37, -11, 29);
            child.localScale = new Vector3(-2, 0.5f, 1.25f);
            Transform nested = new GameObject("Nested visual").transform;
            nested.SetParent(child, false);
            nested.localPosition = new Vector3(-1, 2, 0.3f);
            nested.localRotation = Quaternion.Euler(12, 43, 9);
            nested.localScale = new Vector3(0.75f, 2, 1);
            Vector3 meshCorner = new Vector3(0.3f, -0.7f, 1.1f);
            Vector3 rootPoint = nested.TransformPoint(meshCorner);
            Vector3 childPosition = child.localPosition;
            Quaternion childRotation = child.localRotation;
            Vector3 childScale = child.localScale;
            Bounds bounds = new Bounds(new Vector3(2, -3, 5), new Vector3(3, 7, 11));
            Bounds rebased = BlueprintPlacementFrame.RebaseVisuals(root.transform, bounds, origin, orientation);
            Require(child.localScale == childScale, "Frame rebase changed source scale");
            Vector3 target = new Vector3(17, 29, -13);
            Quaternion targetRotation = Quaternion.Euler(-19, 47, 32);
            BlueprintPlacementFrame.ToRootPose(target, targetRotation, origin, orientation, 1,
                out Vector3 rootPosition, out Quaternion rootRotation);
            root.transform.SetPositionAndRotation(target, targetRotation);
            Require(Vector3.Distance(child.position, rootPosition + rootRotation * childPosition) < 0.0001f &&
                Quaternion.Angle(child.rotation, rootRotation * childRotation) < 0.05f,
                "Hammer visual and root plan poses disagree");
            Require(Vector3.Distance(nested.TransformPoint(meshCorner), rootPosition + rootRotation * rootPoint) < 0.0001f,
                "Rotated support moved nested/mirrored mesh corner away from the plan");
            Vector3 socketLocal = BlueprintPlacementFrame.ToLocal(rootPoint, origin, orientation);
            Require(Vector3.Distance(root.transform.TransformPoint(socketLocal), nested.TransformPoint(meshCorner)) < 0.0001f,
                "Hammer snap and visual disagree");
            for (int corner = 0; corner < 8; ++corner)
            {
                Vector3 point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                Require(rebased.Contains(BlueprintPlacementFrame.ToLocal(point, origin, orientation)),
                    "Support collider lost a rotated bound corner");
            }
        }
        finally { Object.DestroyImmediate(root); }
        checks.Add("Actual placement-frame adapter on Unity transforms: saved rotated support, Hammer visual/root-plan agreement, nested mirrored XYZ scale, native socket coincidence and all eight collider bounds corners; no native Valheim placement executed");
    }

    private static void TestImportedAxes(Camera camera, TMP_Text font, string output)
    {
        const string tilted = "tilted;;0;0;0;0.6532815;0.2705981;-0.2705981;0.6532815;\"\";1;1;1";
        const string straight = "straight;;2;0;0;0;0;0;1;\"\";1;1;1";
        Quaternion expectedTilt = Quaternion.Euler(90, 45, 0);
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.SetActive(false);
        try
        {
            foreach (bool reverse in new[] { false, true })
            {
                string text = "#Name:Axes\n#Pieces\n" + (reverse ? straight + "\n" + tilted : tilted + "\n" + straight);
                var raw = ExternalBlueprintLibrary.Parse(text, "axes.blueprint");
                Require(raw.importError == null && raw.sourceDocuments.Single().text == text, "Axis source parsing changed bytes");
                using (var editor = new BlueprintEditorController(new CompositeBlueprintStore(
                    Path.Combine(output, "axes-" + Guid.NewGuid().ToString("N") + ".json")), camera, font,
                    _ => source, name => name, () => Array.Empty<BlueprintEditorCatalogItem>(),
                    message => { throw new InvalidOperationException(message); }))
                {
                    Require(editor.OpenExisting(raw, out string error), error);
                    var scene = Field<BlueprintEditorScene>(editor, "scene");
                    var visuals = Field<IDictionary>(scene, "visuals");
                    var tiltedPart = editor.Document.Parts.Single(part => part.PrefabName == "tilted");
                    var straightPart = editor.Document.Parts.Single(part => part.PrefabName == "straight");
                    Transform first = Field<GameObject>(visuals[tiltedPart.StableId], "Root").transform;
                    Transform second = Field<GameObject>(visuals[straightPart.StableId], "Root").transform;
                    Require((second.localPosition - first.localPosition - Vector3.right * 2).sqrMagnitude < .00000001f &&
                        Quaternion.Angle(first.localRotation, expectedTilt) < .001f &&
                        Quaternion.Angle(second.localRotation, Quaternion.identity) < .001f,
                        "First row tilt/order rotated editor geometry or swapped axes");
                    foreach (bool support in new[] { false, true })
                    {
                        var frame = raw.ImportCopy();
                        if (support)
                        {
                            frame.supports = new List<CompositeBlueprintStore.Support> {
                                new CompositeBlueprintStore.Support { stableId = "axis-support", name = "Оси",
                                    position = new CompositeBlueprintStore.VectorData(Vector3.zero),
                                    rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(0, 30, 0)) } };
                            frame.placementSupportId = "axis-support";
                        }
                        Require(BlueprintPlacementFrame.TryResolve(frame, out Vector3 origin, out Quaternion orientation) == support,
                            "Part order invented or lost a placement support");
                        var hammer = new GameObject("AxisFrame");
                        try
                        {
                            Transform a = Object.Instantiate(first.gameObject, hammer.transform).transform;
                            Transform b = Object.Instantiate(second.gameObject, hammer.transform).transform;
                            BlueprintPlacementFrame.RebaseVisuals(hammer.transform,
                                new Bounds(Vector3.right, new Vector3(4, 3, 2)), origin, orientation);
                            BlueprintPlacementFrame.ToRootPose(Vector3.zero, Quaternion.identity, origin, orientation, 1,
                                out Vector3 position, out Quaternion rotation);
                            Vector3 expectedDelta = support ? new Vector3(Mathf.Sqrt(3), 0, 1) : Vector3.right * 2;
                            Quaternion expectedRoot = support ? Quaternion.Euler(0, -30, 0) : Quaternion.identity;
                            Require((b.localPosition - a.localPosition - expectedDelta).sqrMagnitude < .00000001f &&
                                Quaternion.Angle(rotation, expectedRoot) < .001f && position == Vector3.zero &&
                                Quaternion.Angle(a.localRotation, expectedRoot * expectedTilt) < .001f &&
                                Quaternion.Angle(b.localRotation, expectedRoot) < .001f,
                                "Hammer visual/root plan inherited first row rotation or lost explicit support compensation");
                        }
                        finally { Object.DestroyImmediate(hammer); }
                    }
                    editor.Close(true);
                }
            }
            checks.Add("Imported axes: rotated first row and reversed order preserve actual editor poses; no-support frame stays identity, explicit Ry30 support gives independent inverse30 displacement/rotation in shared Hammer rebase/root plan. No native registry/Session execution or world placement.");
        }
        finally { Object.DestroyImmediate(source); }
    }

    private static void TestPinnedScaleBasis(Camera camera, TMP_Text font, string output)
    {
        GameObject canonical = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject hammer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject ghost = new GameObject("PinnedScaleGhost");
        canonical.name = "wall"; canonical.SetActive(false);
        hammer.name = "wall"; hammer.SetActive(false);
        canonical.transform.localScale = new Vector3(2, .5f, 3);
        hammer.transform.localScale = new Vector3(.5f, 2, .25f);
        Piece piece = hammer.AddComponent<Piece>();
        string path = Path.Combine(output, "pinned-scale-" + Guid.NewGuid().ToString("N") + ".json");
        BlueprintEditorController Editor(CompositeBlueprintStore store) => new BlueprintEditorController(store,
            camera, font, _ => canonical, name => name, () => Array.Empty<BlueprintEditorCatalogItem>(),
            message => { throw new InvalidOperationException(message); });
        try
        {
            Require(BlueprintPlacementFrame.HasRigidParent(canonical.transform), "Unparented capture rejected");
            canonical.transform.SetParent(ghost.transform, false);
            ghost.transform.rotation = Quaternion.Euler(23, -41, 17);
            Require(BlueprintPlacementFrame.HasRigidParent(canonical.transform), "Rigid rotated capture rejected");
            ghost.transform.localScale = new Vector3(2, .5f, 1);
            Require(!BlueprintPlacementFrame.HasRigidParent(canonical.transform), "Scaled capture parent accepted");
            ghost.transform.localScale = new Vector3(-1, 1, 1);
            Require(!BlueprintPlacementFrame.HasRigidParent(canonical.transform), "Mirrored capture parent accepted");
            ghost.transform.localScale = new Vector3(-1, -1, 1);
            Require(!BlueprintPlacementFrame.HasRigidParent(canonical.transform), "Double-mirrored parent lost its apparent rotation");
            var intermediate = new GameObject("ShearedCaptureParent");
            try
            {
                ghost.transform.localScale = new Vector3(2, .5f, 1);
                intermediate.transform.SetParent(ghost.transform, false);
                intermediate.transform.localRotation = Quaternion.Euler(0, 0, 45);
                intermediate.transform.localScale = new Vector3(1 / Mathf.Sqrt(2.125f), 1 / Mathf.Sqrt(2.125f), 1);
                canonical.transform.SetParent(intermediate.transform, false);
                Matrix4x4 sheared = intermediate.transform.localToWorldMatrix;
                Require(Mathf.Abs(((Vector3)sheared.GetColumn(0)).sqrMagnitude - 1) < .00001f &&
                    Mathf.Abs(((Vector3)sheared.GetColumn(1)).sqrMagnitude - 1) < .00001f,
                    "Shear fixture axes were not normalized");
                Require(!BlueprintPlacementFrame.HasRigidParent(canonical.transform), "Parent shear with unit-length axes was accepted");
                canonical.transform.SetParent(ghost.transform, false);
            }
            finally { Object.DestroyImmediate(intermediate); }
            ghost.transform.localScale = Vector3.one;
            canonical.transform.SetParent(null, false);
            ghost.transform.rotation = Quaternion.identity;
            var raw = ExternalBlueprintLibrary.Parse("#Name:Pinned\n#Pieces\n" +
                "wall;;0;0;0;0;0;0;1;\"\";1;1;1\nwall;;3;0;0;0;0;0;1;\"\";1;1;1", "pinned.blueprint");
            using (var editor = Editor(new CompositeBlueprintStore(path)))
            {
                Require(editor.OpenExisting(raw, out string error), error);
                var scene = Field<BlueprintEditorScene>(editor, "scene");
                object visual = Field<IDictionary>(scene, "visuals")[editor.Document.Parts[0].StableId];
                Require(editor.Document.Parts[0].BaseScale.Value.Equals(new Point3(1, 1, 1)) &&
                    Field<GameObject>(visual, "Root").transform.localScale == Vector3.one,
                    "Canonical prefab base changed imported editor dimensions");
                Require((bool)Call(editor, "TrySave", false, true), "Pinned controller save failed");
                editor.Close(true);
            }
            var stored = new CompositeBlueprintStore(path).All().Single();
            var tiny = stored.ImportCopy();
            foreach (var part in tiny.parts)
                part.baseScale = new CompositeBlueprintStore.VectorData(Vector3.one * .001f);
            using (var thumbnail = new BlueprintThumbnailRenderer.PreviewSession(tiny, new[] { piece, piece }))
                Require(Field<GameObject>(thumbnail, "root").transform.GetChild(0).localScale == Vector3.one * .001f,
                    "Exact tiny editor/thumbnail geometry was rejected or resized by a world scale limit");
            bool tinyWorldRejected = false;
            try { tiny.parts[0].PlacementScale(Vector3.one); }
            catch (ArgumentException) { tinyWorldRejected = true; }
            Require(tinyWorldRejected, "Tiny editor scale bypassed world admission");
            for (int pass = 0; pass < 2; ++pass)
            {
                Vector3 expected = pass == 0 ? Vector3.one : Vector3.one * .5f;
                Require(stored.parts[0].RootScale(hammer.transform.localScale) == expected,
                    "Persisted root scale depends on Hammer prefab");
                using (var thumbnail = new BlueprintThumbnailRenderer.PreviewSession(stored, new[] { piece, piece }))
                    Require(Field<GameObject>(thumbnail, "root").transform.GetChild(0).localScale == expected,
                        "Thumbnail/preview changed pinned root scale");
                var plan = stored.parts.Select(part => new PrecisionPlacementSession.TransformSnapshot(
                    part.position.ToVector3(), part.rotation.ToQuaternion(), part.PlacementScale(hammer.transform.localScale))).ToList();
                Require(plan[0].Scale == (pass == 0 ? new Vector3(2, .5f, 4) : new Vector3(1, .25f, 2)),
                    "World plan lost pinned scale or multiplied canonical base twice");
                using (var preview = new PlacementGhostPreviewView())
                {
                    preview.Show(ghost, plan, new[] { piece, piece }, -1);
                    Require(Field<List<GameObject>>(preview, "instances")[0].transform.localScale == expected,
                        "World ghost differs from editor/thumbnail size");
                }
                var currentLibrary = new CompositeBlueprintStore(path);
                using (var editor = Editor(currentLibrary))
                {
                    Require(editor.OpenExisting(currentLibrary.All().Single(), out string error), error);
                    var scene = Field<BlueprintEditorScene>(editor, "scene");
                    object visual = Field<IDictionary>(scene, "visuals")[editor.Document.Parts[0].StableId];
                    Require(Field<GameObject>(visual, "Root").transform.localScale == expected,
                        "Native reopen changed editor root scale");
                    if (pass == 0)
                    {
                        editor.Document.SelectOnly(editor.Document.Parts[0].StableId);
                        Require(editor.ApplyTransformDelta(Vector3.zero, Quaternion.identity, Vector3.zero, .5f),
                            "Pinned multiplier edit failed");
                        Require((bool)Call(editor, "TrySave", false, true), "Pinned edit save failed");
                    }
                    editor.Close(true);
                }
                stored = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(
                    BlueprintExchangeFormat.Encode(new CompositeBlueprintStore(path).All().Single())));
            }
            var snapBlueprint = stored.ImportCopy();
            var snapped = snapBlueprint.parts[0];
            snapped.baseScale = new CompositeBlueprintStore.VectorData(new Vector3(1.5f, .75f, .5f));
            snapped.scale = new CompositeBlueprintStore.VectorData(new Vector3(.5f, 1, 2));
            snapped.position = new CompositeBlueprintStore.VectorData(new Vector3(1, 2, 3));
            snapped.rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(23, 41, -17));
            Vector3 localSnap = new Vector3(.2f, .3f, -.4f);
            Vector3 rootSnap = snapped.position.ToVector3() + snapped.rotation.ToQuaternion() *
                Vector3.Scale(snapped.RootScale(hammer.transform.localScale), localSnap);
            snapBlueprint.customSnapPoints = new List<CompositeBlueprintStore.VectorData> { new CompositeBlueprintStore.VectorData(rootSnap) };
            var snapSets = BlueprintPlacementFrame.SnapLocalSets(snapBlueprint, new[] { piece, piece });
            var restored = BlueprintSnapPointCodec.Decode(BlueprintSnapPointCodec.Encode(snapSets[0]));
            Require((restored[0] - new Point3(localSnap.x, localSnap.y, localSnap.z)).LengthSquared < .00000001,
                "Rotated nonuniform pinned scale changed saved local socket");
            var legacyLibrary = new CompositeBlueprintStore(path + ".legacy");
            var legacyParts = CompositeBlueprintStore.CloneParts(stored.parts);
            foreach (var part in legacyParts) { part.baseScale = null; part.scale = new CompositeBlueprintStore.VectorData(Vector3.one); }
            Require(legacyLibrary.TrySaveDocument("Legacy", stored.category, legacyParts, stored.groups, stored.anchors,
                null, null, stored.supports, null, out var legacy, out string legacyError, sourceDocuments: stored.sourceDocuments), legacyError);
            using (var editor = Editor(legacyLibrary))
            {
                Require(editor.OpenExisting(legacy, out string error), error);
                Vector3 observedBase = canonical.transform.localScale;
                canonical.transform.localScale = Vector3.one * 4;
                Require((bool)Call(editor, "TrySave", false, true) && legacy.parts[0].baseScale.Value.ToVector3() == observedBase &&
                    legacy.parts[0].RootScale(hammer.transform.localScale) == observedBase,
                    "Explicit legacy save changed observed size or pinned a newly resolved prefab");
                canonical.transform.localScale = observedBase;
                editor.Close(true);
            }
            checks.Add("Pinned native19/portable9 scale: actual controller import/save/reopen/edit, thumbnail PreviewSession and world ghost agree across canonical(2,.5,3)/Hammer(.5,2,.25); scaled/mirrored capture parents refused. Renderer/format tests, no native construction/reload.");
        }
        finally { Object.DestroyImmediate(canonical); Object.DestroyImmediate(hammer); Object.DestroyImmediate(ghost); }
    }

    private static void TestSaveRoundtrip(BlueprintEditorController controller, string path)
    {
        controller.Document.AddSupport(new BlueprintEditorSupport("saved-support", "Вход",
            new Point3(2, 3, 4), new Rotation3(0, 1, 0, 1)));
        controller.Document.SetPlacementSupport("saved-support");
        controller.Document.SelectSupport(null);
        float expected = (float)controller.Document.Parts[0].Scale.X;
        Require((bool)Call(controller, "TrySave", false, true), "Product controller save failed");
        Require(!controller.Document.IsDirty, "Saved document stays dirty");
        var reloaded = new CompositeBlueprintStore(path);
        Require(reloaded.All().Count == 1, "Actual disk library roundtrip");
        Require(Mathf.Abs(reloaded.All()[0].parts[0].scale.x - expected) < 0.0001f, "Store discarded scale");
        CheckReopenedCopy(controller, reloaded, expected);
        Require(File.ReadAllText(path).Contains("\"version\":22"), "Controller did not write format22");
        checks.Add("Controller save -> disk format21 -> new store -> OpenExisting retains scale, rotated root-owned support and placement reference; transient support focus absent");
    }

    private static void TestPendingMaterialSeedEditor(Camera camera, TMP_Text font, GameObject wall, string output)
    {
        string path = Path.Combine(output, "material-seed-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new CompositeBlueprintStore(path);
        var part = new CompositeBlueprintStore.Part { stableId = "seed", prefabName = "woodwall", displayName = "Wall",
            materialSeed = 0, rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } };
        Require(store.TrySave("Pending material", new[] { part }, Array.Empty<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
        using (var editor = new BlueprintEditorController(store, camera, font, name => name == "woodwall" ? wall : null,
            name => name, () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); }))
        {
            Require(editor.OpenExisting(saved, out error), error);
            Call(editor, "Bind", "");
            var view = Field<BlueprintEditorView>(editor, "view");
            Require(Field<TMP_Text>(view, "statusText").text.Contains(BuildWorksLocalization.Text("import.foreign_pending")),
                "Editor reported an unapplied material recipe ready");
            Require(editor.Document.Parts.Single().MaterialSeed == 0 && (bool)Call(editor, "TrySave", false, true),
                "Actual editor save lost zero appearance seed");
            editor.Close(true);
            var reopened = new CompositeBlueprintStore(path).All().Single();
            Require(reopened.parts.Single().materialSeed == 0 && CompositeBlueprintStore.HasForeignData(reopened) &&
                editor.OpenExisting(reopened, out error) && editor.Document.Parts.Single().MaterialSeed == 0,
                "Editor/native reopen lost the pending appearance recipe: " + error);
            editor.Close(true);
        }
        checks.Add("Material seed native21/portable10 data contract: actual editor save/reopen preserves zero and keeps an explicit pending-state warning; external conversion/native placement remain incomplete.");
    }

    private static void TestIncompleteExternalEditor(Camera camera, TMP_Text font, GameObject wall, string output)
    {
        string path = Path.Combine(output, "missing-copy-" + Guid.NewGuid().ToString("N") + ".json");
        const string text = "#Name:Missing model\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\n" +
            "absent_model;;3;4;5;0;0;0;1;\"unknown-info\";.001;.002;.003;unknown-zdo;.5";
        GameObject known = PlacementGhostPreviewView.CreateVisualClone(wall, "NonunitKnownPrefab", 0);
        GameObject later = null;
        known.transform.localScale = new Vector3(2, .5f, 3);
        var models = new Dictionary<string, GameObject> { ["woodwall"] = known };
        BlueprintEditorController NewEditor(CompositeBlueprintStore library) => new BlueprintEditorController(library,
            camera, font, name => models.TryGetValue(name, out var model) ? model : null, name => name,
            () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); });
        try
        {
            var raw = ExternalBlueprintLibrary.Parse(text, "missing.blueprint");
            var store = new CompositeBlueprintStore(path);
            using (var editor = NewEditor(store))
            {
                Require(editor.OpenExisting(raw, out string error), "Incomplete external open: " + error);
                Require(editor.Document.Parts.Count == 2 && !editor.Document.Parts[0].ScaleIsAbsolute &&
                    editor.Document.Parts[0].Scale.X == 1 && editor.Document.Parts[0].BaseScale.Value.X == 1 &&
                    editor.Document.Parts[1].ScaleIsAbsolute,
                    "Incomplete editor lost records or guessed intrinsic scale");
                string missingId = editor.Document.Parts[1].StableId;
                object visual = Field<IDictionary>(Field<BlueprintEditorScene>(editor, "scene"), "visuals")[missingId];
                Require(Field<bool>(visual, "Placeholder") && Field<GameObject>(visual, "Root").transform.localScale == Vector3.one * .3f &&
                    Field<List<Vector3>>(visual, "PlacementLocal").Count == 0 && Field<List<Vector3>>(visual, "SnapLocal").Count == 0,
                    "Unknown source scale became model size or generated sockets");
                editor.Document.SelectOnly(missingId);
                Call(editor, "Bind", "");
                var scene = Field<BlueprintEditorScene>(editor, "scene");
                foreach (bool experimental in new[] { false, true })
                {
                    scene.ExperimentalGeometryPoints = experimental;
                    Require(scene.TryGetGizmoAnchors(new[] { missingId }, out Vector3[] points, out _) && points.Length == 1 &&
                        points[0] == new Vector3(3, 4, 5) && scene.GeometryAnchorCount == 0 &&
                        !scene.TryGetGizmoAnchors(new[] { missingId }, out _, out _, legacyLayoutAnchors: true),
                        "Marker geometry became corner or packing anchors");
                }
                foreach (bool keyboard in new[] { true, false })
                {
                    Call(editor, "BeginKeyboardPreview", GizmoHandleKind.Scale);
                    Vector2 mouse = Field<Vector2>(editor, "dragStartMouse");
                    Set(editor, "keyboardMoved", true);
                    Call(editor, keyboard ? "PreviewKeyboardTransform" : "PreviewGizmoDrag", mouse);
                    Require(Field<float>(editor, "dragScale") == 1f, "Zero-delta scale gesture damaged pending source scale");
                    if (keyboard)
                    {
                        Set(editor, "keyboardNumber", "2");
                        Call(editor, "PreviewKeyboardNumber");
                        Require(Field<bool>(editor, "keyboardValid") && Field<float>(editor, "dragScale") == 2f,
                            "Numeric gesture rejects valid pending absolute scale");
                    }
                    else Call(editor, "PreviewGizmoDrag", mouse + Vector2.up * 100);
                    Require(Field<float>(editor, "dragScale") == 2f &&
                        Field<GameObject>(visual, "Root").transform.localScale == Vector3.one * .3f,
                        "Scale gesture interpreted pending basis or resized uncertainty marker");
                    Call(editor, "CommitGizmoDrag");
                    Require(Math.Abs(editor.Document.Parts[1].Scale.X - .002f) < .00000001 && editor.Document.Undo(),
                        "Pending scale confirm/Undo failed");
                    Require(scene.TrySync(editor.Document, out error), error);
                    visual = Field<IDictionary>(scene, "visuals")[missingId];
                }
                var view = Field<BlueprintEditorView>(editor, "view");
                Call(editor, "Bind", "");
                var positionInputs = Field<TMP_InputField[]>(view, "inspectorPosition");
                positionInputs[0].text = "3.25";
                var scaleInput = Field<TMP_InputField[]>(view, "inspectorScale")[0];
                scaleInput.text = "Infinity";
                scaleInput.onEndEdit.Invoke("Infinity");
                Require(editor.Document.Parts[1].Position.X == 3 && editor.Document.Parts[1].Scale.X == .001f,
                    "Nonfinite pending scale was accepted by inspector");
                scaleInput.text = Field<string[]>(view, "inspectorOriginalScaleText")[0];
                positionInputs[0].onEndEdit.Invoke("3.25");
                Require(editor.Document.Parts[1].Position.X == 3.25 && editor.Document.Parts[1].Scale.X == .001f &&
                    editor.Document.Undo(), "Inspector position edit rejects or changes pending scale");
                Require(scene.TrySync(editor.Document, out error), error);
                visual = Field<IDictionary>(scene, "visuals")[missingId];
                scene.PreviewTransform(editor.Document, new[] { missingId }, Vector3.right, Quaternion.Euler(0, 37, 0), Vector3.zero);
                Require(Field<GameObject>(visual, "Root").transform.localScale == Vector3.one * .3f,
                    "Move/rotate preview resized unknown model marker");
                Require(scene.TrySync(editor.Document, out error), error);
                scene.ShowBlueprintPlacementPreview(new[] { editor.Document.Parts[0] }, Vector3.up, out Vector3 knownOffset);
                scene.ShowBlueprintPlacementPreview(new[] { editor.Document.Parts[0], new BlueprintEditorPart(
                    "missing-low", "absent_model", "missing", new Point3(3, -100, 5), new Rotation3(0, 0, 0, 1),
                    scale: new Point3(.001, .002, .003), scaleIsAbsolute: true) }, Vector3.up, out Vector3 mixedOffset);
                Require(knownOffset == mixedOffset, "Missing marker changed surface grounding of known geometry");
                scene.ClearBlueprintPlacementPreview();
                Require(editor.ApplyTransformDelta(Vector3.right * .5f, Quaternion.Euler(0, 37, 0), new Vector3(3, 4, 5)), "Missing record cannot move/rotate");
                Require(editor.Document.DuplicateSelection(new Point3(2, 0, 0)) &&
                    editor.Document.Parts.Count == 3 && editor.Document.Parts[2].ScaleIsAbsolute &&
                    editor.Document.Undo() && editor.Document.Parts.Count == 2 && editor.Document.Parts[1].ScaleIsAbsolute,
                    "Copy/Undo dropped pending basis");
                Require((bool)Call(editor, "TrySave", false, true), "Incomplete native save failed");
                editor.Close(true);
            }
            var reloaded = new CompositeBlueprintStore(path);
            var saved = reloaded.All().Single();
            Require(saved.parts[1].scaleIsAbsolute && saved.parts[1].scale.x == .001f && saved.parts[1].position.x == 3.5f &&
                Quaternion.Angle(saved.parts[1].rotation.ToQuaternion(), Quaternion.Euler(0, 37, 0)) < .001f &&
                saved.parts[1].foreignData.zdo == "unknown-zdo" && saved.sourceDocuments[0].text == text && raw.parts[1].position.x == 3,
                "Incomplete disk save lost pose, foreign evidence, source or basis");
            Require(reloaded.TryExportFile(saved, output, "missing-exchange-" + Guid.NewGuid().ToString("N"), false,
                out string exported, out string exportError), exportError);
            var exchanged = ExternalBlueprintLibrary.LoadFile(exported).Single();
            Require(exchanged.importError == null && exchanged.parts[1].scaleIsAbsolute && exchanged.parts[1].scale.x == .001f &&
                exchanged.parts[1].position.x == 3.5f && exchanged.sourceDocuments[0].text == text,
                "Incomplete portable roundtrip lost pending part");
            using (var editor = NewEditor(reloaded))
            {
                Require(editor.OpenExisting(saved, out string error) && editor.Document.Parts[1].ScaleIsAbsolute,
                    "Incomplete native editor reopen: " + error);
                later = PlacementGhostPreviewView.CreateVisualClone(wall, "LateNonunitPrefab", 0);
                later.transform.localScale = new Vector3(.0005f, .001f, .0015f);
                models.Add("absent_model", later);
                var scene = Field<BlueprintEditorScene>(editor, "scene");
                Require(scene.TrySync(editor.Document, out error), error);
                object visual = Field<IDictionary>(scene, "visuals")[editor.Document.Parts[1].StableId];
                Require(Field<bool>(visual, "Placeholder"), "Late prefab rendered before explicit scale normalization");
                editor.Close(true);
                Require(editor.OpenExisting(saved, out error) && !editor.Document.Parts[1].ScaleIsAbsolute &&
                    editor.Document.Parts[1].Scale.X == 1 && editor.Document.Parts[1].BaseScale.Value.X == .001f &&
                    editor.Document.Parts[0].Scale.X == 1 && editor.Document.Parts[0].BaseScale.Value.X == 1 &&
                    ReferenceEquals(Field<CompositeBlueprintStore.Blueprint>(editor, "sourceBlueprint"), saved) &&
                    (bool)Call(editor, "TrySave", false, true), "Late normalization lost native write identity or divided twice: " + error);
                editor.Close(true);
            }
            Require(!new CompositeBlueprintStore(path).All()[0].parts[1].scaleIsAbsolute &&
                new CompositeBlueprintStore(path).All()[0].parts[1].baseScale.Value.x == .001f &&
                new CompositeBlueprintStore(path).All()[0].parts[1].scale.x == 1 && raw.sourceDocuments[0].text == text,
                "Resolved basis did not persist or source was modified");
        }
        finally { Object.DestroyImmediate(known); if (later) Object.DestroyImmediate(later); }
    }

    private static void TestExternalImportEditor(Camera camera, TMP_Text font,
        Dictionary<string, GameObject> sources, string output)
    {
        string path = Path.Combine(output, "external-copy-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new CompositeBlueprintStore(path);
        var raw = ExternalBlueprintLibrary.Parse("#Name:Import fixture\n#Category:OTHER\n#Pieces\n" +
            "woodwall;0;1;2;3;0;0;0;1;\"opaque-object-state\";1;1;1;opaque-zdo;.5\nwood_pole;0;4;5;6;0;0;0;1;\"\";1;1;1\n#FutureData\nignored", "fixture.blueprint");
        var original = ExternalBlueprintLibrary.PrepareForHost(raw, name => sources.TryGetValue(name, out GameObject prefab)
            ? new CompositeBlueprintStore.VectorData(prefab.transform.localScale) : (CompositeBlueprintStore.VectorData?)null);
        using (var editor = new BlueprintEditorController(store, camera, font,
            name => sources.TryGetValue(name, out GameObject prefab) ? prefab : null, name => name,
            () => new[] { new BlueprintEditorCatalogItem("external-fixture", original.name, null,
                "OTHER", "blueprints", "external_blueprints", 0, original) },
            message => { throw new InvalidOperationException(message); }))
        {
            Require(editor.OpenExisting(original, out string error), "External editor open: " + error);
            Require(Field<CompositeBlueprintStore.Blueprint>(editor, "sourceBlueprint") == null && store.All().Count == 0,
                "External source is adopted as writable native blueprint");
            var part = editor.Document.Parts[0];
            Require(part.StableId != original.parts[0].stableId && part.Position.X == 1 && part.Position.Y == 2,
                "External draft lost pose or retained source identity");
            editor.Document.SelectOnly(part.StableId);
            Require(editor.ApplyTransformDelta(Vector3.right, Quaternion.identity, Vector3.zero), "Imported part cannot use native transform editing");
            Require(!(bool)Call(editor, "TrySave", false, false) && !File.Exists(path), "Import save wrote before metadata confirmation");
            Field<BlueprintEditorView>(editor, "view").HideDialog();
            Require(!File.Exists(path), "Cancel import save mutated storage");
            Require((bool)Call(editor, "TrySave", false, true), "Import native-copy save failed");
            var reopened = new CompositeBlueprintStore(path);
            Require(reopened.All().Count == 1 && reopened.All()[0].parts[0].position.x == 2 &&
                original.parts[0].position.x == 1 && !reopened.All()[0].IsExternal, "Save mutated source or lost edit");
            Require(reopened.All()[0].sourceDocuments[0].text == raw.sourceDocuments[0].text &&
                reopened.All()[0].parts[0].sourceDocumentId == raw.sourceDocuments[0].id &&
                reopened.All()[0].parts[0].sourceLine == 4 && reopened.All()[0].parts[0].foreignData.info == "\"opaque-object-state\"" &&
                reopened.All()[0].parts[0].foreignData.zdo == "opaque-zdo" && reopened.All()[0].parts[0].foreignData.chance == ".5" &&
                CompositeBlueprintStore.HasForeignData(reopened.All()[0]), "Actual import/edit/save discarded original source, row binding or opaque object state");
            editor.Close(true);
            Require(editor.OpenExisting(store.All()[0], out error), "Saved import cannot reopen: " + error);
            Require(editor.Document.Sources[0].Text == raw.sourceDocuments[0].text &&
                (bool)Call(editor, "TrySave", false, true), "Actual native reopen/update discarded source");
            Call(editor, "RequestCatalog");
            Call(editor, "BeginPartPlacement", new BlueprintEditorCatalogItem("external-fixture", original.name, null,
                "OTHER", "blueprints", "external_blueprints", 0, original), true);
            Require(Field<BlueprintEditorDocument>(editor, "placementBlueprint")?.Parts.Count == 2,
                "One-click external card did not begin whole-blueprint placement");
            Call(editor, "CancelPartPlacement");
            Require(editor.Document.Parts.Count == 2 && Field<BlueprintEditorDocument>(editor, "placementBlueprint") == null,
                "Cancel insert changed saved document or leaked ghost state");
            editor.Close(true);
        }
        checks.Add("External .blueprint -> detached draft -> metadata Cancel no disk -> transform/save native v15 -> reopen/update -> whole-source insert preview/Cancel; exact source/rows, inert object/ZDO/chance/unknown sections retained; native contents not rendered or executed");
    }

    private static void TestScaleBoundaries(BlueprintEditorController controller, string path)
    {
        string selected = controller.Document.Parts[0].StableId;
        foreach (float percent in new[] { 1f, 400f, 1000f, 125.5f, 126f })
        {
            controller.Document.SelectOnly(selected);
            Call(controller, "SetActiveTool", BlueprintEditorTool.Transform);
            Call(controller, "Bind", "Проверка границ масштаба");
            var input = Field<TMP_InputField[]>(Field<BlueprintEditorView>(controller, "view"), "inspectorScale")[0];
            input.text = percent.ToString(System.Globalization.CultureInfo.InvariantCulture);
            input.onEndEdit.Invoke(input.text);
            Require(Math.Abs(controller.Document.Parts[0].Scale.X - percent / 100.0) < 0.00001,
                "Actual TMP scale rejects " + percent + "%");
            Require((bool)Call(controller, "TrySave", false, true), "Saving scale boundary failed");
            var reloaded = new CompositeBlueprintStore(path);
            CheckReopenedCopy(controller, reloaded, percent / 100.0);
        }
        checks.Add("Actual TMP 1%, 400%, 1000%, 125.5% and 126% -> save and reopen");
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
            Require(opened.Document.Supports.Count == original.Document.Supports.Count &&
                opened.Document.PlacementSupportId == original.Document.PlacementSupportId && opened.Document.ActiveSupportId == null,
                "Controller dropped support data/default or persisted temporary focus");
            for (int i = 0; i < original.Document.Supports.Count; ++i)
            {
                BlueprintEditorSupport source = original.Document.Supports[i], restored = opened.Document.Supports[i];
                Require(source.StableId == restored.StableId && source.Name == restored.Name &&
                    Math.Abs(source.Position.X - restored.Position.X) < .000001 &&
                    Math.Abs(source.Position.Y - restored.Position.Y) < .000001 &&
                    Math.Abs(source.Position.Z - restored.Position.Z) < .000001 &&
                    Math.Abs(source.Rotation.Y - restored.Rotation.Y) < .000001 &&
                    Math.Abs(source.Rotation.W - restored.Rotation.W) < .000001,
                    "Controller changed support pose/name on reopen");
            }
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
        TMP_InputField scale = Field<TMP_InputField[]>(view, "inspectorScale")[0];
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
                TMP_InputField scale = Field<TMP_InputField[]>(view, "inspectorScale")[0];
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
        for (int index = 0; index < CompositeBlueprintStore.InteractivePartBudget; ++index)
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
            crowded.Add(new BlueprintEditorPart("over-budget", "fixture", "over-budget", new Point3(), new Rotation3(0,0,0,1)));
            var heavy = new BlueprintEditorDocument(null, "Over budget", "Test", crowded);
            Require(scene.TrySync(heavy, out string budgetError), "Isolated editor extension refused: " + budgetError);
            Require(scene.TryGetGizmoAnchors(crowdedIds, out Vector3[] retained, out _, legacyLayoutAnchors: true) &&
                retained.Length == points.Length, "Over-budget sync changed the previous scene");
        }
        checks.Add("Native/midpoint provenance: exterior shared joins excluded, stable cache, 100%/1% scale; 512 exterior native points remain bounded when isolated editor part capacity exceeds 512");
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
            Require(instances.Count == 2 && Array.TrueForAll(instances[0].GetComponentsInChildren<Renderer>(true), renderer => !renderer.enabled),
                "World preview duplicates the native holder");
            Require(Vector3.Distance(instances[1].transform.localScale,
                Vector3.Scale(source.transform.localScale, new Vector3(1.2f,0.5f,2f))) < 0.001f, "World preview discarded per-part scale");
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

    private static void TestRotatedPlacementFrames(Camera template)
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube); source.SetActive(false);
        var socket = new GameObject("RotatedSocket"); socket.tag = "snappoint"; socket.transform.SetParent(source.transform, false);
        Quaternion targetFrame = Quaternion.Euler(23, 41, -17), yaw = Quaternion.Euler(0, 37, 0);
        Vector3 target = new Vector3(0, 1, 0);
        var doc = new BlueprintEditorDocument(null, "Rotated target", "Test", new[] {
            new BlueprintEditorPart("target", "fixture", "Target", new Point3(0,1,0),
                new Rotation3(targetFrame.x, targetFrame.y, targetFrame.z, targetFrame.w)) });
        try
        {
            using (var scene = new BlueprintEditorScene(template, _ => source))
            {
                scene.SetViewport(new Rect(0,0,1920,1080));
                scene.SetCameraPose(new Vector3(0,2,-5), Quaternion.Euler(10,0,0));
                Require(scene.TrySync(doc, out string error), error);
                Require(scene.ShowPlacementPreview("fixture", target - Vector3.up * .4f, Vector3.up, yaw, true,
                    out Vector3 final, out error, 0), error);
                Require(scene.PlacementSnapTarget.HasValue && Vector3.Distance(final, target) < .001f &&
                    Quaternion.Angle(scene.PlacementFinalRotation, targetFrame * yaw) < .01f,
                    "Single ghost did not inherit target XYZ and preserve grip");
                var ghost = Field<object>(scene, "placementPreview");
                Require(Quaternion.Angle(Field<GameObject>(ghost, "Root").transform.rotation, scene.PlacementFinalRotation) < .01f,
                    "Single ghost final frame differs from commit frame");
                Require(scene.ShowPlacementPreview("fixture", target - Vector3.up * .4f, Vector3.up, yaw, false,
                    out _, out error, 0) && Quaternion.Angle(scene.PlacementFinalRotation, yaw) < .01f,
                    "Unsnapped ghost retained old target XYZ");
                var second = new BlueprintEditorPart("second", "fixture", "Second", new Point3(0,1,0),
                    new Rotation3(yaw.x,yaw.y,yaw.z,yaw.w));
                var first = new BlueprintEditorPart("first", "fixture", "First", new Point3(10,1,0),
                    new Rotation3(yaw.x,yaw.y,yaw.z,yaw.w));
                scene.ShowBlueprintPlacementPreview(new[] { first, second }, target - Vector3.up * .4f,
                    out Vector3 offset, snap:true, inputRotation:yaw);
                Require(scene.PlacementSnapTarget.HasValue && Quaternion.Angle(scene.PlacementFinalRotation, targetFrame * yaw) < .01f,
                    "Second part native socket in blueprint ghost was skipped");
                var previews = Field<IList>(scene, "duplicatePreviews");
                Transform near = Field<GameObject>(previews[1], "Root").transform;
                Require(Vector3.Distance(near.position, target) < .001f &&
                    Quaternion.Angle(near.rotation, targetFrame * yaw) < .01f, "Blueprint ghost grip/frame mismatch");
                Transform far = Field<GameObject>(previews[0], "Root").transform;
                Require(Vector3.Distance(far.position - near.position, targetFrame * Vector3.right * 10) < .001f,
                    "Blueprint alignment changed relative geometry");
                var targets = new List<Vector3>(); var flags = new List<bool>();
                Require(scene.TryCursorSnap(Array.Empty<string>(), new[] { target + Vector3.right * .1f }, new[] { 0 },
                    0, 1, targets, flags, out int selected, out Vector3 point, out bool native,
                    cursor:scene.Camera.WorldToScreenPoint(target)) && selected == 0 && native &&
                    Quaternion.Angle(scene.SnapTargetRotation, targetFrame) < .01f && Vector3.Distance(point, target) < .001f,
                    "G target provenance did not return owner XYZ");
            }
        }
        finally { Object.Destroy(source); }
        checks.Add("XYZ rotated native target: single/second-part blueprint ghosts preserve grip, relative geometry and final commit frame; no stale free frame");
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
