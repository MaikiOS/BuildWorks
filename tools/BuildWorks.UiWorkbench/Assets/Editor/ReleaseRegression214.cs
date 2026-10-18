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
public static class ReleaseRegression214
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Pending = "BuildWorks.Release214.Pending";
    static ReleaseRegression214()
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
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../..")), output = Path.Combine(root, "artifacts/release-214");
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
            Require(rectangles == 48 && catalogCases == 3, "Release regression coverage incomplete.");
            File.WriteAllText(Path.Combine(output, "ui-verification.json"), JsonUtility.ToJson(new Result { passed = true, rectangles = rectangles, catalogCases = catalogCases }, true));
            Debug.Log("BUILDWORKS_RELEASE214_OK");
        }
        finally { if (ownedFont && !EditorUtility.IsPersistent(ownedFont)) Object.DestroyImmediate(ownedFont); Object.DestroyImmediate(fontHost); Object.DestroyImmediate(eventHost); }
    }
    private static BlueprintEditorCatalogItem Item(CompositeBlueprintStore.Blueprint blueprint, int index)
        => new BlueprintEditorCatalogItem("blueprint_" + blueprint.id, blueprint.name, null, blueprint.category, "Blueprints", "source", index, blueprint);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Result { public bool passed, gameHostTested = false; public int rectangles, catalogCases; }
}
