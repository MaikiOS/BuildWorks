using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class StagedEditorAcceptance
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Staged editor: " + message); }
    private static int Count(BlueprintEditorScene scene) => Field<IDictionary>(scene, "visuals").Count;

    internal static IEnumerable Run(Camera template, TMP_Text font, string output)
    {
        GameObject wall = Resources.Load<GameObject>("VanillaPreview/woodwall");
        Require(wall, "woodwall fixture missing");
        string directory = Path.Combine(output, "staged-editor");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "library.json");
        if (File.Exists(path)) File.Delete(path);
        const int total = 1025;
        string fixture = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../tests/fixtures/player-exchange/07-heavy-editor.blueprint"));
        string original = File.ReadAllText(fixture);
        var external = ExternalBlueprintLibrary.Parse(original, "07-heavy-editor.blueprint");
        Require(external.importError == null && external.parts.Count == total, external.importError);
        var store = new CompositeBlueprintStore(path);
        var errors = new List<string>();
        GameObject Resolve(string name)
        {
            return name == "woodwall" ? wall : null;
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int ticks = 0;
        using (var controller = new BlueprintEditorController(store, template, font, Resolve, name => name,
            () => Array.Empty<BlueprintEditorCatalogItem>(), errors.Add))
        {
            Require(controller.OpenExisting(external, out string error) && controller.IsLoading && controller.BlocksWorldInput, error);
            Require(!controller.AddPart("woodwall"), "editing was permitted during load");
            var scene = Field<BlueprintEditorScene>(controller, "scene");
            GameObject cancelledRoot = Field<GameObject>(scene, "root");
            controller.Update();
            Require(Count(scene) > 0 && Count(scene) <= 16, "first update created unbounded visuals");
            controller.Close(true);
            Require(!controller.IsOpen && !File.Exists(path), "cancel wrote the library or kept the controller open");
            yield return null;
            Require(!cancelledRoot, "cancel leaked partial scene");

            Require(controller.OpenExisting(external, out error), error);
            scene = Field<BlueprintEditorScene>(controller, "scene");
            while (controller.IsLoading && ticks < 4096)
            {
                int before = Count(scene);
                controller.Update();
                Require(Count(scene) - before <= 16, "a load update exceeded its part budget");
                ++ticks;
                yield return null;
            }
            Require(!controller.IsLoading && controller.IsOpen && Count(scene) == total && controller.Document.Parts.Count == total,
                "load did not finish or discarded records");
            var view = Field<BlueprintEditorView>(controller, "view");
            RectTransform rows = Field<RectTransform>(view, "outlinerRows");
            Require(rows.childCount < 64 && Field<List<Action>>(view, "outlinerRowBuilders").Count == total,
                "outliner instantiated the full tree or lost descriptors");
            Canvas.ForceUpdateCanvases();
            Field<RectTransform>(view, "outliner").GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
            yield return null;
            view.Tick();
            yield return null;
            string last = controller.Document.Parts[total - 1].StableId;
            Require(rows.Find("Row_" + last), "virtual scrolling did not reach the last missing record");
            controller.Document.SelectOnly(last);
            Require(controller.ApplyTransformDelta(Vector3.up, Quaternion.identity, Vector3.zero), "missing record could not move");
            Require(controller.Document.Parts[total - 1].Position.Y == 1, "missing transform changed incorrectly");
            Call(controller, "ApplyHistory", (Func<bool>)controller.Document.Undo, (Func<bool>)controller.Document.Redo);
            Require(controller.Document.Parts[total - 1].Position.Y == 0, "Undo lost the missing record transform");
            Call(controller, "ApplyHistory", (Func<bool>)controller.Document.Redo, (Func<bool>)controller.Document.Undo);
            Require((bool)Call(controller, "TrySave", false, true), "heavy document save failed");
            controller.Close(true);
            yield return null;
            var saved = new CompositeBlueprintStore(path).All().Single();
            Require(saved.parts.Count == total && saved.parts[total - 1].position.y == 1 &&
                saved.sourceDocuments.Single().text == original && errors.Count == 0, "save lost records/source or logged an error");
            Require(controller.OpenExisting(saved, out error), error);
            while (controller.IsLoading) { controller.Update(); yield return null; }
            Require(controller.Document.Parts.Count == total && controller.Document.Parts[total - 1].Position.Y == 1,
                "heavy reopen changed saved data");
            controller.Close(true);

            Require(controller.OpenExisting(external, out error), error);
            controller.Update();
            controller.Document.SetMetadata("Injected load revision", "OTHER");
            controller.Update();
            Require(controller.IsLoading && errors.Count == 1 && Field<BlueprintEditorView>(controller, "view").HasModal,
                "staged failure was hidden or allowed editing");
            GameObject failedRoot = Field<GameObject>(Field<BlueprintEditorScene>(controller, "scene"), "root");
            controller.Close(true);
            yield return null;
            Require(!failedRoot, "failed staged scene leaked");
        }
        var document = new BlueprintEditorDocument(null, "Revision fixture", "OTHER", new[] {
            new BlueprintEditorPart("a", "woodwall", "Wall", new Point3(0,0,0), new Rotation3(0,0,0,1)),
            new BlueprintEditorPart("b", "woodwall", "Wall", new Point3(2,0,0), new Rotation3(0,0,0,1)) });
        using (var scene = new BlueprintEditorScene(template, _ => wall))
        {
            scene.BeginInitialLoad(document);
            Require(!scene.StepInitialLoad(out _, 1, 100), "load completed before its part budget");
            document.SetMetadata("Changed while loading", "OTHER");
            bool rejected = false;
            try { scene.StepInitialLoad(out _); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "stale document revision was projected");
        }
        watch.Stop();
        File.WriteAllText(Path.Combine(directory, "verification.txt"),
            "PASS actual editor 1025 parts: bounded staged load (" + ticks + " ticks), cancel/reopen, failure cleanup, " +
            "virtual tree last row, missing transform, Undo/Redo, native-format save/reopen, unchanged source, stale-revision rejection; " +
            watch.ElapsedMilliseconds + " ms total including yielded frames. Unity Workbench; no Valheim/runtime/server proof.");
    }
}
