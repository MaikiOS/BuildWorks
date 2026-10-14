using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The only substituted boundary is physical input. Every gesture enters actual Update/LateUpdate;
// UI clicks are resolved by the actual GraphicRaycaster before native pointer handlers run.
internal static class ControllerInputAcceptance
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);
    internal static bool ImportPending(object exchange) =>
        Field<Task<CompositeBlueprintStore.ImportReview>>(exchange, "importInspection") != null ||
        Field<Task<CompositeBlueprintStore.ImportReview>>(exchange, "importPreparation") != null;
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException("Input frames: " + reason); }
    private static Vector3 V(Point3 value) => new Vector3((float)value.X, (float)value.Y, (float)value.Z);

    internal static IEnumerable<object> RunSinglePart(Camera template, TMP_Text font, GameObject wall, string output)
    {
        string path = Path.Combine(output, "single-part-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new CompositeBlueprintStore(path);
        var part = new CompositeBlueprintStore.Part { stableId = "single", prefabName = "woodwall", displayName = "Wall",
            rotation = new CompositeBlueprintStore.QuaternionData { w = 1 },
            baseScale = new CompositeBlueprintStore.VectorData(wall.transform.localScale),
            scale = new CompositeBlueprintStore.VectorData { x = 2, y = .5f, z = 1.25f } };
        Require(store.TrySave(new[] { part }, Array.Empty<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
        using (var controller = new BlueprintEditorController(store, template, font, name => name == "woodwall" ? wall : null,
            name => name, () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); }))
        {
            Require(controller.OpenExisting(saved, out error), error);
            Require(controller.Document.Parts.Count == 1 && controller.Document.Rename(controller.Document.Parts[0].StableId, "Changed"),
                "Single editor did not open/edit one part");
            yield return null;
            controller.Document.SelectOnly(controller.Document.Parts[0].StableId);
            Call(controller, "Bind", (object)null);
            var view = Field<BlueprintEditorView>(controller, "view");
            Call(controller, "SetActiveTool", BlueprintEditorTool.Transform);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var fields = Field<TMP_InputField[]>(view, "inspectorScale");
            NativeClick(view, null, Field<Button>(view, "inspectorProportionsButton"));
            Require(!Field<bool>(view, "inspectorKeepProportions"), "XYZ toggle did not disable proportions");
            int history = Field<IList>(controller.Document, "undo").Count;
            fields[1].text = "75"; fields[1].onEndEdit.Invoke(fields[1].text);
            Require(controller.Document.Parts[0].Scale.Equals(new Point3(2, .75, 1.25)) &&
                Field<IList>(controller.Document, "undo").Count == history + 1,
                "Independent Y changed X/Z or did not make one history entry");
            Require(controller.Document.Undo(), "XYZ edit cannot be undone"); Call(controller, "Bind", (object)null);
            Require(controller.Document.Parts[0].Scale.Equals(new Point3(2, .5, 1.25)), "XYZ Undo lost original scale");
            Require(controller.Document.Redo(), "XYZ edit cannot be redone"); Call(controller, "Bind", (object)null);
            NativeClick(view, null, Field<Button>(view, "inspectorProportionsButton"));
            fields[2].text = "150"; fields[2].onEndEdit.Invoke(fields[2].text);
            var resized = controller.Document.Parts[0].Scale;
            Require(Math.Abs(resized.X - 2.4) < .000001 && Math.Abs(resized.Y - .9) < .000001 &&
                Math.Abs(resized.Z - 1.5) < .000001, "Linked Z did not preserve all proportions");
            Require(controller.Document.Undo(), "Linked edit cannot be undone"); Call(controller, "Bind", (object)null);
            foreach (string invalid in new[] { "0", "-1", "NaN", "Infinity", "1001" })
            {
                fields[0].text = invalid; fields[0].onEndEdit.Invoke(invalid);
                Require(controller.Document.Parts[0].Scale.Equals(new Point3(2, .75, 1.25)), "Invalid XYZ altered document");
                Call(controller, "Bind", (object)null);
            }
            Require(Field<Button>(view, "saveButton").interactable, "Single editor Save remains disabled");
            NativeClick(view, null, "Save"); yield return null;
            Require(view.HasModal, "Single editor Save did not request metadata");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(!controller.Document.IsDirty && !view.HasModal, "Single editor native Save click failed");
            controller.Close(true);
            var reopened = new CompositeBlueprintStore(path).All().Single();
            Require(reopened.parts.Count == 1 && reopened.parts[0].displayName == "Changed" &&
                reopened.parts[0].scale.ToVector3() == new Vector3(2, .75f, 1.25f),
                "Single actual editor save lost row/name/scale");
            Require(controller.OpenNew(out error), error); yield return null;
            Require(!Field<Button>(view, "saveButton").interactable && !(bool)Call(controller, "TrySave", false, true) &&
                new CompositeBlueprintStore(path).All().Count == 1, "Empty editor saved over the single blueprint");
        }
    }

    internal static IEnumerable<object> RunSaveAs(Camera template, TMP_Text font, GameObject wall, string output)
    {
        string path = Path.Combine(output, "save-as-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new CompositeBlueprintStore(path);
        var evidence = new BlueprintSourceDocument("blueprint", "save-as.blueprint",
            "#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";2;.5;1.25");
        var part = new CompositeBlueprintStore.Part { stableId = "wall", prefabName = "woodwall",
            displayName = "Original wall", parentGroupId = "g", rotation = new CompositeBlueprintStore.QuaternionData { w = 1 },
            scale = new CompositeBlueprintStore.VectorData { x = 2, y = .5f, z = 1.25f },
            baseScale = new CompositeBlueprintStore.VectorData(wall.transform.localScale), materialSeed = 0,
            sourceDocumentId = evidence.Id, sourceLine = 2 };
        var support = new CompositeBlueprintStore.Support { stableId = "s", name = "Support",
            rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } };
        var preview = new CompositeBlueprintStore.Blueprint { previewYaw = 67, previewPitch = 23, previewZoom = 1.3f,
            placementImportId = "original-import",
            libraryOrigins = new List<CompositeBlueprintStore.LibraryOrigin> { new CompositeBlueprintStore.LibraryOrigin(
                "server.test", 71, Guid.NewGuid().ToString("N"), 3, 0, "Steam_admin", new string('A', 64), 12) } };
        Require(store.TrySaveDocument("Original", "OTHER", new[] { part },
            new[] { new CompositeBlueprintStore.Group { stableId = "g", name = "Group", pivotPartId = "wall" } },
            Array.Empty<CompositeBlueprintStore.VectorData>(), "wall", null, new[] { support }, "s",
            out var original, out string error, new[] { new CompositeBlueprintStore.VectorData { x = 3 } }, preview,
            new[] { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) }), error);
        var input = new InputFrames();
        using (var controller = new BlueprintEditorController(store, template, font, name => name == "woodwall" ? wall : null,
            name => name, () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); },
            editorInput: input))
        {
            Require(controller.OpenExisting(original, out error), error); yield return null;
            var view = Field<BlueprintEditorView>(controller, "view");
            Require(Field<Button>(view, "saveAsButton").interactable && !Field<Button>(view, "saveButton").interactable,
                "Clean document cannot Save As independently of ordinary Save");
            string before = File.ReadAllText(path);
            NativeClick(view, null, "SaveAs"); yield return null;
            Require(view.HasModal && Field<TMP_Text>(view, "modalTitle").text == BuildWorksLocalization.Text("editor.save_as"),
                "Save As did not identify the copy operation");
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Require(!view.HasModal && controller.Document.SourceBlueprintId == original.id && !controller.Document.IsDirty &&
                File.ReadAllText(path) == before, "Cancelled Save As changed identity, dirty state or storage");
            Frame(controller, input, view.ViewportScreenRect().center, 0, KeyCode.LeftControl, KeyCode.LeftShift, KeyCode.S);
            Require(view.HasModal && Field<TMP_Text>(view, "modalTitle").text == BuildWorksLocalization.Text("editor.save_as"),
                "Ctrl+Shift+S dispatched ordinary Save or a transform instead of Save As");
            Frame(controller, input, view.ViewportScreenRect().center);
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Require(File.ReadAllText(path) == before && !controller.Document.IsDirty,
                "Cancelled Save As shortcut changed the document");
            Require(controller.Document.Rename("wall", "Edited wall"), "Cannot prepare edited copy");
            Call(controller, "Bind", (object)null);
            NativeClick(view, null, "SaveAs"); yield return null;
            Field<TMP_InputField>(view, "saveMetadataName").SetTextWithoutNotify(" ");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(view.HasModal && File.ReadAllText(path) == before, "Invalid copy name reached persistence");
            Field<TMP_InputField>(view, "saveMetadataName").SetTextWithoutNotify("Copy");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            var copy = store.All().Single(item => item.id != original.id);
            Require(controller.Document.SourceBlueprintId == copy.id && !controller.Document.IsDirty && copy.name == "Copy" &&
                original.name == "Original" && original.parts[0].displayName == "Original wall" &&
                copy.parts[0].displayName == "Edited wall" && copy.parts[0].scale.y == .5f && copy.parts[0].materialSeed == 0 &&
                copy.groups[0].pivotPartId == "wall" && copy.placementSupportId == "s" && copy.customSnapPoints[0].x == 3 &&
                copy.sourceDocuments[0].text == evidence.Text && copy.previewYaw == 67 && copy.previewPitch == 23 &&
                copy.previewZoom == 1.3f && copy.placementImportId == null && copy.libraryOrigins.Count == 1 &&
                copy.libraryOrigins[0].server == "server.test" && copy.libraryOrigins[0].revision == 3 &&
                !ReferenceEquals(copy.libraryOrigins[0], original.libraryOrigins[0]),
                "Save As lost supported data, reused import identity or overwrote original");
            Require(controller.Document.Rename("wall", "Updated copy"), "Cannot edit saved copy");
            Call(controller, "Bind", (object)null);
            NativeClick(view, null, "Save"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(store.All().Count == 2 && copy.parts[0].displayName == "Updated copy" &&
                original.parts[0].displayName == "Original wall", "Ordinary Save updated original or created another copy");
            before = File.ReadAllText(path);
            Set(store, "writeBlockReason", "QA synthetic write refusal");
            NativeClick(view, null, "SaveAs"); yield return null;
            Field<TMP_InputField>(view, "saveMetadataName").SetTextWithoutNotify("Abandoned copy");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(controller.Document.Name == "Copy" && !controller.Document.IsDirty &&
                File.ReadAllText(path) == before, "Failed copy metadata changed the source document");
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Set(store, "writeBlockReason", null);
            Require(controller.Document.Rename("wall", "After cancelled copy"), "Cannot edit after cancelled failure");
            Frame(controller, input, view.ViewportScreenRect().center, 0, KeyCode.LeftControl, KeyCode.S);
            Frame(controller, input, view.ViewportScreenRect().center);
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(copy.name == "Copy" && store.All().Count == 2 && original.name == "Original" &&
                copy.parts[0].displayName == "After cancelled copy", "Ordinary Save applied abandoned copy metadata");
            before = File.ReadAllText(path);
            Set(store, "writeBlockReason", "QA synthetic write refusal");
            NativeClick(view, null, "SaveAs"); yield return null;
            Field<TMP_InputField>(view, "saveMetadataName").SetTextWithoutNotify("Retry copy");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(view.HasModal && store.All().Count == 2 && controller.Document.SourceBlueprintId == copy.id &&
                ReferenceEquals(Field<CompositeBlueprintStore.Blueprint>(controller, "sourceBlueprint"), copy) &&
                File.ReadAllText(path) == before, "Failed Save As changed source identity or library");
            Set(store, "writeBlockReason", null);
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(view.HasModal && Field<TMP_Text>(view, "modalTitle").text == BuildWorksLocalization.Text("editor.save_as"),
                "Retry changed Save As into ordinary Save");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            var reloaded = new CompositeBlueprintStore(path).All();
            Require(reloaded.Count == 3 && reloaded.Single(item => item.id == original.id).name == "Original" &&
                reloaded.Single(item => item.id == copy.id).name == "Copy" &&
                reloaded.Single(item => item.id == controller.Document.SourceBlueprintId).name == "Retry copy",
                "Retry/reload overwrote an existing blueprint or created duplicate copies");
        }
    }

    internal static IEnumerable<object> RunBatchExchange(Camera template, TMP_Text font, GameObject wall, string output)
    {
        string folder = Path.Combine(output, "batch-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string a = Path.Combine(folder, "a.blueprint"), b = Path.Combine(folder, "b.blueprint");
        File.WriteAllText(a, "#Name:One\n#Category:FIRST\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1");
        File.WriteAllText(b, File.ReadAllText(a).Replace("One", "Two").Replace("FIRST", "SECOND"));
        File.WriteAllText(Path.Combine(folder, "bad.blueprint"), "broken source");
        byte[] beforeA = File.ReadAllBytes(a), beforeB = File.ReadAllBytes(b);
        var store = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
        store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
        var part = new CompositeBlueprintStore.Part { stableId = "wall", prefabName = "woodwall", displayName = "Owned wall",
            rotation = new CompositeBlueprintStore.QuaternionData { w = 1 }, scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 } };
        Require(store.TrySave("Owned", new[] { part }, Array.Empty<CompositeBlueprintStore.VectorData>(), out var owned, out string error), error);
        IReadOnlyList<BlueprintEditorCatalogItem> Items() => store.CatalogAll().Select((item, i) => new BlueprintEditorCatalogItem(
            "blueprint_" + item.id, item.name, null, item.category, "Blueprints", "Mine", i, item)).ToArray();
        var input = new InputFrames();
        using (var controller = new BlueprintEditorController(store, template, font, _ => wall, name => name, Items,
            message => { throw new InvalidOperationException(message); }, editorInput: input))
        {
            Require(controller.OpenExisting(owned, out error), error); yield return null;
            Require(controller.Document.Rename("wall", "Draft wall"), "Batch draft setup failed");
            var view = Field<BlueprintEditorView>(controller, "view"); Call(controller, "Bind", (object)null);
            int revision = controller.Document.RevisionToken, history = Field<IList>(controller.Document, "undo").Count;
            view.ShowCatalog(Items()); yield return null;
            NativeClick(view, null, "CatalogBlueprintsMode"); yield return null;
            NativeClick(view, null, "CatalogImport"); yield return null;
            var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
            for (int i = 0; i < 600 && Field<Task<string[]>>(exchange, "listing") != null; ++i) { Frame(controller, input, Vector2.zero); yield return null; }
            NativeClick(view, null, "ExchangeBatchToggle"); yield return null;
            foreach (string path in new[] { a, b, Path.Combine(folder, "bad.blueprint") })
            {
                int index = Array.IndexOf(Field<string[]>(exchange, "entries"), path);
                Require(index >= 0, "Batch file missing from browser");
                NativeClick(view, null, "ExchangeEntry" + index); yield return null;
            }
            Frame(controller, input, Vector2.zero);
            Require(Field<Button>(exchange, "primary").IsInteractable(), "Batch review button stayed disabled after selection");
            NativeClick(view, null, "ExchangeSubmit"); yield return null;
            for (int i = 0; i < 600 && Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary") == null; ++i)
            { Frame(controller, input, Vector2.zero); yield return null; }
            Require(Field<CompositeBlueprintStore.ImportReview>(exchange, "importReview") != null,
                "Batch review not ready: mode=" + Field<bool>(exchange, "batchMode") + " open=" + exchange.IsOpen +
                " review=" + Field<bool>(exchange, "reviewingContents") + " raw=" + Field<Task<CompositeBlueprintStore.ImportReview>>(exchange, "importInspection")?.Status +
                " preparation=" + Field<Task<CompositeBlueprintStore.ImportReview>>(exchange, "importPreparation")?.Status +
                " message=" + Field<TMP_Text>(exchange, "message").text);
            Require(Field<CompositeBlueprintStore.ImportReview>(exchange, "importReview").Documents.Count == 2 &&
                Field<TMP_Text>(exchange, "contentsDetails").text.Contains("bad.blueprint"), "Batch review did not expose malformed file and ready documents");
            Field<TMP_InputField>(exchange, "categoryInput").text = "GROUP COMMON";
            NativeClick(view, null, "ExchangeBatchDocuments"); yield return null;
            Require(!Field<Button>(exchange, "primary").interactable, "Hidden review could submit under document picker");
            var category = view.RootCanvas.GetComponentsInChildren<TMP_InputField>().Single(input => input.name == "ExchangeBatchCategory1");
            category.SetTextWithoutNotify("GROUP OVERRIDE"); category.onEndEdit.Invoke(category.text);
            NativeClick(view, null, "ExchangeBatchPickerClose"); yield return null;
            for (int i = 0; i < 600 && Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary") == null; ++i)
            { Frame(controller, input, Vector2.zero); yield return null; }
            NativeClick(view, null, "ExchangeSubmit"); yield return null;
            for (int i = 0; i < 1200 && !Field<bool>(exchange, "completed"); ++i) { Frame(controller, input, Vector2.zero); yield return null; }
            Require(Field<bool>(exchange, "completed") && view.HasModal && store.CatalogAll().Count == 3 &&
                store.CatalogAll().Single(item => item.name == "Two").category == "GROUP OVERRIDE" &&
                Field<TMP_Text>(exchange, "message").text.Contains("bad.blueprint"), "Batch commit/result/per-document category failed");
            NativeClick(view, null, "ExchangeSubmit"); yield return null;
            Require(!view.HasModal && controller.Document.IsDirty && controller.Document.RevisionToken == revision &&
                Field<IList>(controller.Document, "undo").Count == history, "Batch result/navigation replaced dirty draft or history");

            view.HideCatalog();
            view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, owned, closed)); yield return null;
            exchange = Field<BlueprintExchangeView>(view, "exchangeView");
            for (int i = 0; i < 600 && Field<Task<string[]>>(exchange, "listing") != null; ++i) { Frame(controller, input, Vector2.zero); yield return null; }
            NativeClick(view, null, "ExchangeBatchToggle"); yield return null;
            var choices = Field<CompositeBlueprintStore.Blueprint[]>(exchange, "batchExportChoices");
            for (int i = 0; i < choices.Length; ++i)
                if (choices[i].id != owned.id) { NativeClick(view, null, "ExchangeBatchExport" + i); yield return null; }
            Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify("full-group");
            Frame(controller, input, Vector2.zero);
            Require(Field<Button>(exchange, "primary").IsInteractable(), "Batch review button stayed disabled after selection");
            NativeClick(view, null, "ExchangeSubmit"); yield return null;
            for (int i = 0; i < 600 && Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary") == null; ++i)
            { Frame(controller, input, Vector2.zero); yield return null; }
            Require(Field<CompositeBlueprintStore.ImportReview>(exchange, "importReview").Documents.Count == 3, "Group export reviewed only initial card");
            NativeClick(view, null, "ExchangeSubmit"); yield return null;
            for (int i = 0; i < 1200 && !Field<bool>(exchange, "completed"); ++i) { Frame(controller, input, Vector2.zero); yield return null; }
            string result = Field<string>(exchange, "resultPath");
            Require(File.Exists(result) && ExternalBlueprintLibrary.LoadFile(result).Count == 3 &&
                store.All().Count == 1 && beforeA.SequenceEqual(File.ReadAllBytes(a)) && beforeB.SequenceEqual(File.ReadAllBytes(b)),
                "Group export failed/no native+external data/source mutation/card leak");
            NativeClick(view, null, "ExchangeClose"); yield return null;
            Require(controller.Document.IsDirty && controller.Document.RevisionToken == revision, "Export changed draft");
        }
    }

    internal static IEnumerable<object> RunShareFile(Camera template, TMP_Text font, GameObject wall, string output)
    {
        string folder = Path.Combine(output, "share-ui-" + Guid.NewGuid().ToString("N"));
        var store = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
        var part = new CompositeBlueprintStore.Part { stableId = "wall", prefabName = "woodwall", displayName = "Saved wall",
            rotation = new CompositeBlueprintStore.QuaternionData { w = 1 }, scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 } };
        Require(store.TrySave("Share", new[] { part }, Array.Empty<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
        IReadOnlyList<BlueprintEditorCatalogItem> Items() => store.All().Select((item, i) => new BlueprintEditorCatalogItem(
            "blueprint_" + item.id, item.name, null, item.category, "Blueprints", "Mine", i, item)).ToArray();
        using (var controller = new BlueprintEditorController(store, template, font, _ => wall, name => name, Items,
            message => { throw new InvalidOperationException(message); }))
        {
            Require(controller.OpenExisting(saved, out error), error); yield return null;
            var view = Field<BlueprintEditorView>(controller, "view");
            Require(controller.Document.Rename("wall", "Unsaved wall"), "Cannot prepare share draft");
            Call(controller, "Bind", (object)null);
            int revision = controller.Document.RevisionToken, history = Field<IList>(controller.Document, "undo").Count;
            byte[] original = File.ReadAllBytes(Path.Combine(folder, "library.json"));
            view.ShowCatalog(Items()); yield return null;
            NativeClick(view, null, "CatalogBlueprintsMode"); yield return null;
            NativeClick(view, null, "CatalogShare"); yield return null;
            var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
            string file = Field<string>(exchange, "resultPath");
            NativeClick(view, null, "ExchangeCopyPath"); yield return null;
            Require(GUIUtility.systemCopyBuffer == file && File.Exists(file) &&
                BlueprintExchangeFormat.Decode(File.ReadAllText(file)).parts[0].displayName == "Saved wall" &&
                controller.Document.IsDirty && controller.Document.RevisionToken == revision &&
                Field<IList>(controller.Document, "undo").Count == history && original.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "library.json"))),
                "Share modified draft/history/library or shared unsaved content");
            NativeClick(view, null, "ExchangeClose"); yield return null;
            File.Delete(file); Directory.CreateDirectory(file);
            NativeClick(view, null, "CatalogShare"); yield return null;
            exchange = Field<BlueprintExchangeView>(view, "exchangeView");
            Require(!Field<Button>(exchange, "copyPath").interactable && Field<Button>(exchange, "primary").interactable,
                "Failed share lacks safe retry or exposes invalid copy path");
            Directory.Delete(file);
            NativeClick(view, null, "ExchangeSubmit"); yield return null;
            Require(File.Exists(file) && store.All().Count == 1 && Field<Button>(exchange, "copyPath").interactable,
                "Share retry saved another card or failed to recover");
            NativeClick(view, null, "ExchangeClose"); yield return null;
            NativeClick(view, null, "CatalogClose"); yield return null;
            string backup = Path.Combine(folder, "share-backup");
            Directory.Move(store.ShareDirectory, backup); File.WriteAllText(store.ShareDirectory, "block generated copies");
            NativeClick(view, null, "SaveAs"); yield return null;
            Field<TMP_InputField>(view, "saveMetadataName").SetTextWithoutNotify("Committed copy");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            string committedId = controller.Document.SourceBlueprintId;
            Require(store.All().Count == 2 && !controller.Document.IsDirty && view.HasModal &&
                Field<TMP_Text>(view, "modalTitle").text == BuildWorksLocalization.Text("share.action"),
                "Derived copy failure rolled back successful Save As or lost warning");
            File.Delete(store.ShareDirectory); Directory.Move(backup, store.ShareDirectory);
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(store.All().Count == 2 && controller.Document.SourceBlueprintId == committedId && !view.HasModal &&
                File.Exists(Path.Combine(store.ShareDirectory, committedId + ".buildworks.json")), "Postcommit retry duplicated Save As");
        }
    }

    internal static IEnumerable<object> RunCatalogManagement(Camera template, TMP_Text font, GameObject wall, string output)
    {
        string directory = Path.Combine(output, "catalog-management-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "imports"));
        string sourcePath = Path.Combine(directory, "imports", "table.blueprint");
        const string raw = "#Name:External table\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\n";
        File.WriteAllText(sourcePath, raw);
        string path = Path.Combine(directory, "library.json");
        var store = new CompositeBlueprintStore(path);
        var source = store.CatalogAll().Single();
        Require(store.TryGetOrCreatePlacementCopy(source, out var owned, out string error), error);
        Func<IReadOnlyList<BlueprintEditorCatalogItem>> catalogue = () => store.CatalogAll(true).Select((item, index) =>
            new BlueprintEditorCatalogItem("blueprint_" + item.id, item.name, null, item.category, "Blueprints", "source", index, item,
                unavailableReason: item.IsExternal ? "QA unavailable entry" : null, hidden: store.IsHidden(item))).ToArray();
        using (var controller = new BlueprintEditorController(store, template, font, name => name == "woodwall" ? wall : null,
            name => name, catalogue, message => { throw new InvalidOperationException(message); }))
        {
            Require(controller.OpenExisting(owned, out error), error); yield return null;
            var view = Field<BlueprintEditorView>(controller, "view");
            var document = controller.Document;
            Require(document.Rename(document.Parts[0].StableId, "Draft edit"), "Cannot edit deletion draft");
            Call(controller, "Bind", (object)null);
            int history = Field<IList>(document, "undo").Count;
            string bytes = File.ReadAllText(path);
            view.ShowCatalog(catalogue()); yield return null;
            NativeClick(view, null, "CatalogBlueprintsMode"); yield return null;
            Button Manage(string id) => view.RootCanvas.GetComponentsInChildren<Button>().Single(button =>
                button.name == "CatalogManage" && button.transform.parent.name == "Catalog_blueprint_" + id);
            NativeClick(view, null, Manage(owned.id)); yield return null;
            Require(view.HasModal && Field<TMP_Text>(view, "modalMessage").text.Contains(BuildWorksLocalization.Text("catalog.open_draft")),
                "Current document deletion lacks draft explanation");
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Require(File.ReadAllText(path) == bytes && store.All().Count == 1 && Field<IList>(document, "undo").Count == history,
                "Cancelled deletion changed storage or history");
            NativeClick(view, null, Manage(source.id)); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Require(!store.IsHidden(source) && File.ReadAllText(path) == bytes, "Cancelled hiding changed storage");
            NativeClick(view, null, Manage(source.id)); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(store.IsHidden(source) && Field<RectTransform>(view, "catalogGrid").childCount == 1 && document.Parts.Count == 1,
                "Hide placed/deleted a part or left source visible");
            NativeClick(view, null, "Category_hidden"); yield return null;
            Require(Field<RectTransform>(view, "catalogGrid").childCount == 1 &&
                view.RootCanvas.GetComponentsInChildren<Button>().Any(button => button.name == "CatalogManageDetails" &&
                    button.GetComponentInChildren<TMP_Text>().text == BuildWorksLocalization.Text("catalog.action.restore")),
                "Unavailable hidden entry lost visible restore action");
            NativeClick(view, null, Manage(source.id)); yield return null;
            Require(!view.HasModal && !store.IsHidden(source) && Field<RectTransform>(view, "catalogGrid").childCount == 0,
                "Restore required destructive confirmation or did not return entry");
            NativeClick(view, null, "Category_all"); yield return null;
            Set(store, "writeBlockReason", "QA synthetic write refusal");
            NativeClick(view, null, Manage(owned.id)); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(view.HasModal && store.All().Count == 1 && ReferenceEquals(Field<CompositeBlueprintStore.Blueprint>(controller, "sourceBlueprint"), owned),
                "Failed deletion detached document or removed record");
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Set(store, "writeBlockReason", null);
            NativeClick(view, null, Manage(owned.id)); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(store.All().Count == 0 && Field<CompositeBlueprintStore.Blueprint>(controller, "sourceBlueprint") == null &&
                document.IsDirty && document.Parts[0].DisplayName == "Draft edit" && Field<IList>(document, "undo").Count == history,
                "Deletion destroyed current draft/history or kept stale save target");
            view.HideCatalog();
            Require(document.Undo() && document.IsDirty && document.Redo() && document.IsDirty, "Deletion lost Undo/Redo or unsaved state");
            Call(controller, "Bind", (object)null);
            controller.Close(false); yield return null;
            Require(view.HasModal, "Deleted open draft closed without unsaved prompt");
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            NativeClick(view, null, "Save"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            var saved = new CompositeBlueprintStore(path).All().Single();
            Require(saved.id != owned.id && saved.parts[0].displayName == "Draft edit" && !document.IsDirty &&
                saved.sourceDocuments[0].text == source.sourceDocuments[0].text && File.ReadAllText(sourcePath) == raw,
                "Saving deleted draft reused old ID or lost provenance/source bytes");
        }
        File.WriteAllText(Path.Combine(output, "catalog-management-check.txt"),
            "PASS actual card/buttons: owned deletion/hide confirmation + cancel, unavailable hidden restore, failed write, open draft Undo/Redo/close prompt/Save/new ID/reopen/source immutable. No game proof.");
    }

    internal static IEnumerable<object> RunMissingReplacement(Camera template, TMP_Text font, GameObject wall, string output)
    {
        string path = Path.Combine(output, "replace-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new CompositeBlueprintStore(path);
        var evidence = new BlueprintSourceDocument("blueprint", "replacement.blueprint",
            "#Pieces\nqa_absent;;0;0;0;0;0;0;1;\"\";1;1;1");
        var parts = new[] {
            new CompositeBlueprintStore.Part { stableId = "missing", prefabName = "qa_absent", displayName = "Missing", parentGroupId = "g",
                rotation = new CompositeBlueprintStore.QuaternionData { w = 1 }, sourceDocumentId = evidence.Id, sourceLine = 2,
                baseScale = new CompositeBlueprintStore.VectorData(new Vector3(2, 1, .5f)),
                metadata = CompositeBlueprintStore.PrefabMetadata.FromGeometry(new BlueprintPrefabMetadata(1, "qa_absent", "Recorded", "piece", "QA",
                    new Point3(1, 1, 1), new Point3(0, 0, -1), new Point3(2, 2, 1))) },
            new CompositeBlueprintStore.Part { stableId = "a", prefabName = "woodwall", displayName = "Own wall",
                position = new CompositeBlueprintStore.VectorData { x = 3, y = 2, z = 1 },
                rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(0, 45, 0)),
                scale = new CompositeBlueprintStore.VectorData { x = 2, y = .5f, z = 1 }, baseScale = new CompositeBlueprintStore.VectorData(wall.transform.localScale) },
            new CompositeBlueprintStore.Part { stableId = "b", prefabName = "woodwall", displayName = "Own second wall",
                position = new CompositeBlueprintStore.VectorData { x = 5, y = 2, z = 1 }, rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } }
        };
        Require(store.TrySaveDocument("Replacement", "OTHER", parts,
            new[] { new CompositeBlueprintStore.Group { stableId = "g", name = "Original group", pivotPartId = "missing" } },
            Array.Empty<CompositeBlueprintStore.VectorData>(), "missing", null,
            new[] { new CompositeBlueprintStore.Support { stableId = "s", name = "Placement",
                rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } } }, "s", out var saved, out string error,
            sourceDocuments: new[] { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) }), error);
        bool ownAvailable = true;
        var compound = new CompositeBlueprintStore.Blueprint { id = "replaceCompound", name = "Replacement pair", category = "OTHER",
            primaryPartId = "anchor" };
        compound.groups.Add(new CompositeBlueprintStore.Group { stableId = "nested", name = "Nested" });
        compound.parts.Add(new CompositeBlueprintStore.Part { stableId = "other", prefabName = "woodwall", displayName = "Pair other",
            parentGroupId = "nested", position = new CompositeBlueprintStore.VectorData(new Vector3(2, 1, 0)),
            rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity) });
        compound.parts.Add(new CompositeBlueprintStore.Part { stableId = "anchor", prefabName = "woodwall", displayName = "Pair anchor",
            parentGroupId = "nested", position = new CompositeBlueprintStore.VectorData(new Vector3(4, 1, 0)),
            rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(35, 20, 15)),
            scale = new CompositeBlueprintStore.VectorData(new Vector3(.5f, 2, 1)),
            baseScale = new CompositeBlueprintStore.VectorData(wall.transform.localScale) });
        compound.parts.Add(new CompositeBlueprintStore.Part { stableId = "hidden", prefabName = "woodwall", displayName = "Hidden source part",
            parentGroupId = "nested", editorVisible = false, rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity) });
        var incomplete = new CompositeBlueprintStore.Blueprint { id = "replaceIncomplete", name = "Incomplete source", category = "OTHER" };
        incomplete.parts.Add(new CompositeBlueprintStore.Part { stableId = "absent", prefabName = "qa_absent", displayName = "Absent",
            rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity) });
        using (var controller = new BlueprintEditorController(store, template, font,
            name => name == "woodwall" && ownAvailable ? wall : null, name => name,
            () => new[] { new BlueprintEditorCatalogItem("woodwall", "Own wall", null, "OTHER", "WOOD", "Valheim", 0),
                new BlueprintEditorCatalogItem("replaceCompound", compound.name, null, "OTHER", "WOOD", "Valheim", 1, compound),
                new BlueprintEditorCatalogItem("replaceIncomplete", incomplete.name, null, "OTHER", "WOOD", "Valheim", 2, incomplete) },
            message => { throw new InvalidOperationException(message); }))
        {
            Require(controller.OpenExisting(saved, out error), error); yield return null;
            var document = controller.Document;
            var view = Field<BlueprintEditorView>(controller, "view");
            document.SelectOnly("missing"); Call(controller, "Bind", (object)null);
            Require(!view.MissingReplacementAvailable, "Replacement enabled without any chosen own part");
            int initialRevision = document.RevisionToken;
            NativeClick(view, null, "InspectorReplaceMissing"); yield return null;
            Require(view.HasModal && Field<TMP_Text>(view, "modalMessage").text == BuildWorksLocalization.Text("editor.replace_steps"),
                "Single missing row must expose replacement instructions without a hidden selection prerequisite");
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Require(document.RevisionToken == initialRevision && document.Parts.Count == 3 && !view.HasModal,
                "Instruction cancellation changed the document");
            view.ShowToolsMenu(new Vector2(Screen.width * .5f, Screen.height * .5f), false); yield return null;
            NativeClick(view, null, "ToolsReplaceMissing"); yield return null;
            Require(view.HasModal, "Tools replacement is inaccessible with only the missing row selected");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(view.HasCatalog && !view.HasModal && document.RevisionToken == initialRevision,
                "Instruction action did not open the real catalogue without mutation");
            var scene = Field<BlueprintEditorScene>(controller, "scene");
            GameObject root = Field<GameObject>(scene, "root");
            NativeClick(view, null, "Catalog_woodwall"); yield return null;
            Require(view.HasModal && !view.HasCatalog && document.RevisionToken == initialRevision &&
                Field<IList>(scene, "duplicatePreviews").Count == 1 && Field<object>(controller, "placementItem") == null,
                "Catalogue replacement entered ordinary placement or committed before confirmation");
            Require(Field<GameObject>(view, "modal").GetComponent<Image>().color.a == 0 &&
                Field<GameObject>(view, "modal").GetComponent<Image>().raycastTarget,
                "Replacement preview is obscured or permits clicks through its modal input shield");
            Transform previewRoot = Field<GameObject>(Field<IList>(scene, "duplicatePreviews")[0], "Root").transform;
            Require(root.transform.InverseTransformPoint(previewRoot.position).magnitude < .001f,
                "Catalogue replacement preview added an unwanted ground-contact offset");
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Require(document.RevisionToken == initialRevision && document.Parts.Count == 3 &&
                Field<IList>(scene, "duplicatePreviews").Count == 0, "Catalogue preview cancellation changed the document or leaked copies");
            Require(Mathf.Abs(Field<GameObject>(view, "modal").GetComponent<Image>().color.a - .72f) < .001f,
                "Closing replacement preview left ordinary dialogs transparent");
            NativeClick(view, null, "InspectorReplaceMissing"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            NativeClick(view, null, "Catalog_woodwall"); yield return null;
            document.SelectOnly("a");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(document.RevisionToken == initialRevision && document.PrimaryPartId == "missing" &&
                Field<IList>(scene, "duplicatePreviews").Count == 0, "Changed selection committed a stale catalogue replacement");
            document.SelectOnly("missing"); Call(controller, "Bind", (object)null);
            NativeClick(view, null, "InspectorReplaceMissing"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            NativeClick(view, null, "Catalog_woodwall"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(document.Parts.Count == 3 && document.PrimaryPartId != "missing" &&
                document.Parts.Single(part => part.StableId == document.PrimaryPartId).Position.X == 0,
                "Single prefab catalogue replacement did not occupy the missing origin");
            NativeClick(view, null, Field<Button>(view, "undoButton")); yield return null;
            Require(document.Parts.Count == 3 && document.PrimaryPartId == "missing" && document.Groups.Count == 1,
                "Catalogue replacement needed more than one Undo");
            document.SelectOnly("missing"); Call(controller, "Bind", (object)null);
            NativeClick(view, null, "InspectorReplaceMissing"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            NativeClick(view, null, Field<Button>(view, "catalogBlueprintsModeButton")); yield return null;
            NativeClick(view, null, "Catalog_replaceIncomplete"); yield return null;
            Require(view.HasCatalog && !view.HasModal && Field<TMP_Text>(view, "statusText").text.Contains(
                BuildWorksLocalization.Text("editor.replace_source_missing")), "Incomplete replacement source was silently accepted");
            NativeClick(view, null, "Catalog_replaceCompound"); yield return null;
            Require(view.HasModal && Field<IList>(scene, "duplicatePreviews").Count == 2, "Whole blueprint replacement preview missing");
            previewRoot = Field<GameObject>(Field<IList>(scene, "duplicatePreviews")[1], "Root").transform;
            Require(root.transform.InverseTransformPoint(previewRoot.position).magnitude < .001f &&
                Quaternion.Angle(Quaternion.Inverse(root.transform.rotation) * previewRoot.rotation, Quaternion.identity) < .01f,
                "Whole blueprint preview ignored its primary record origin/full rotation");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(document.Parts.Count == 5 && document.Groups.Count == 3 &&
                document.Parts.Single(part => part.DisplayName == "Hidden source part").Visible == false &&
                V(document.Parts.Single(part => part.StableId == document.PrimaryPartId).Position).magnitude < .001f &&
                document.Parts.Single(part => part.StableId == document.PrimaryPartId).Scale.Y == 2,
                "Whole blueprint replacement lost its primary, own scale or hierarchy");
            NativeClick(view, null, Field<Button>(view, "undoButton")); yield return null;
            Require(document.Parts.Count == 3 && document.PrimaryPartId == "missing" && document.Groups.Count == 1,
                "Whole blueprint replacement was not one atomic Undo");
            document.SelectOnly("missing"); Call(controller, "Bind", (object)null);
            Call(controller, "RequestCatalog"); yield return null;
            NativeClick(view, null, Field<Button>(view, "catalogPartsModeButton")); yield return null;
            NativeClick(view, null, "Catalog_woodwall"); yield return null;
            Require(!view.HasModal && Field<object>(controller, "placementItem") != null,
                "Replacement catalogue context leaked into ordinary placement");
            Call(controller, "CancelPartPlacement"); yield return null;
            document.SelectOnly("missing");
            document.ToggleSelection("a"); document.ToggleSelection("b"); Call(controller, "Bind", (object)null);
            Require(view.MissingReplacementAvailable, "Explicit one-to-many selection not available");
            int positioningRevision = document.RevisionToken;
            Bounds committedBounds = default;
            Require(scene.TryGetBounds(new[] { "a", "b" }, out Bounds originalBounds), "Original bounds missing");
            NativeClick(view, null, "InspectorPositionMissing"); yield return null;
            Require(view.HasModal && document.RevisionToken == positioningRevision &&
                Field<IList>(scene, "duplicatePreviews").Count == 2 &&
                scene.TryGetPositioningOffset("missing", null, out Vector3 selectedResidual) && selectedResidual.magnitude < .001f &&
                scene.TryGetBounds(new[] { "a", "b" }, out Bounds unchangedBounds) && unchangedBounds == originalBounds &&
                Field<TMP_Text>(view, "modalMessage").text.Contains(BuildWorksLocalization.Text("editor.position_known")),
                "Copy preview failed bounds alignment or moved originals");
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Require(document.RevisionToken == positioningRevision && scene.TryGetBounds(new[] { "a", "b" }, out Bounds restoredBounds) &&
                restoredBounds == originalBounds && Field<IList>(scene, "duplicatePreviews").Count == 0,
                "Positioning cancel changed history or failed to restore visuals");
            NativeClick(view, null, "InspectorPositionMissing"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            string[] copyIds = document.Parts.Where(part => part.StableId != "a" && part.StableId != "b" && part.StableId != "missing")
                .Select(part => part.StableId).ToArray();
            Require(document.Parts.Count == 5 && document.PrimaryPartId == "missing" && document.Groups.Count == 2 &&
                !document.Selection.Contains("missing") && scene.TryGetBounds(copyIds, out committedBounds) &&
                Vector3.Distance(committedBounds.center, new Vector3(2, 1, 0)) < .001f &&
                scene.TryGetBounds(new[] { "a", "b" }, out unchangedBounds) && unchangedBounds == originalBounds &&
                Field<Button>(view, "positionMissingButton").gameObject.activeSelf &&
                document.Parts.Single(part => part.StableId == "a").Scale.X == 2,
                "Positioning commit lost marker, own scale, hierarchy or preview pose");
            NativeClick(view, null, Field<Button>(view, "undoButton")); yield return null;
            Require(document.Parts.Count == 3 && document.Selection.Contains("missing") &&
                document.Parts.Single(part => part.StableId == "a").Position.X == 3, "Positioning one Undo failed");
            NativeClick(view, null, Field<Button>(view, "redoButton")); yield return null;
            Require(scene.TryGetBounds(copyIds, out committedBounds) &&
                Vector3.Distance(committedBounds.center, new Vector3(2, 1, 0)) < .001f, "Positioning Redo failed or changed IDs");
            NativeClick(view, null, Field<Button>(view, "undoButton")); yield return null;
            document.SelectOnly("missing"); Call(controller, "Bind", (object)null);
            NativeClick(view, null, "InspectorPositionMissing"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            NativeClick(view, null, Field<Button>(view, "catalogBlueprintsModeButton")); yield return null;
            NativeClick(view, null, "Catalog_replaceCompound"); yield return null;
            Require(view.HasModal && scene.TryGetPositioningOffset("missing", null, out Vector3 residual) && residual.magnitude < .001f,
                "Catalogue positioning failed to align whole visible assembly centre");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(document.Parts.Count == 6 && document.PrimaryPartId == "missing" &&
                document.Parts.Any(part => part.StableId == "missing"), "Catalogue positioning removed original missing record");
            NativeClick(view, null, Field<Button>(view, "undoButton")); yield return null;
            Require(document.Parts.Count == 3 && document.Groups.Count == 1, "Catalogue positioning was not one Undo");
            document.SelectOnly("missing"); document.ToggleSelection("a"); document.ToggleSelection("b"); Call(controller, "Bind", (object)null);
            int revision = document.RevisionToken;
            NativeClick(view, null, "InspectorReplaceMissing"); yield return null;
            Require(view.HasModal && document.Parts.Count == 3 && document.RevisionToken == revision,
                "Tools action removed the missing row before confirmation");
            var visuals = Field<IDictionary>(scene, "visuals");
            Require(Field<IList>(scene, "duplicatePreviews").Count == 2 &&
                root.transform.InverseTransformPoint(Field<GameObject>(Field<IList>(scene, "duplicatePreviews")[0], "Root").transform.position).magnitude < .001f &&
                Vector3.Distance(root.transform.InverseTransformPoint(Field<GameObject>(visuals["a"], "Root").transform.position), new Vector3(3, 2, 1)) < .001f &&
                document.Parts.Single(part => part.StableId == "a").Position.X == 3,
                "Selected replacement preview failed to align or mutated the document");
            NativeClick(view, null, Field<Button>(view, "dialogCancelButton")); yield return null;
            Require(!view.HasModal && document.Parts.Count == 3 && document.RevisionToken == revision, "Cancel changed document");
            Require(Vector3.Distance(root.transform.InverseTransformPoint(Field<GameObject>(visuals["a"], "Root").transform.position),
                new Vector3(3, 2, 1)) < .001f, "Selected replacement cancellation did not restore its visible pose");
            Call(controller, "RequestMissingReplacement"); yield return null;
            Require(view.HasModal, "Stale-prefab confirmation did not open"); ownAvailable = false;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(document.Parts.Count == 3 && document.RevisionToken == revision &&
                Field<TMP_Text>(view, "statusText").text.Contains(BuildWorksLocalization.Text("editor.replace_changed")),
                "Changed prefab availability silently committed replacement");
            ownAvailable = true; Call(controller, "Bind", (object)null);
            Call(controller, "RequestMissingReplacement"); yield return null;
            Require(view.HasModal, "Fresh replacement confirmation did not open");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            var copiedAnchor = document.Parts.Single(part => part.StableId == document.PrimaryPartId);
            Require(document.Parts.Count == 4 && document.PrimaryPartId != "a" && document.PlacementSupportId == "s" &&
                document.Sources[0].Text == evidence.Text && document.Groups.Single(group => group.StableId == "g").PivotPartId == copiedAnchor.StableId &&
                V(copiedAnchor.Position).magnitude < .001f && copiedAnchor.Scale.X == 2 &&
                document.Parts.Single(part => part.StableId == "a").Position.X == 3, "Confirmed copy replacement lost original/pose/source/support/pivot");
            Require(!Field<Button>(view, "replaceMissingButton").gameObject.activeSelf,
                "Replacement entry remains after the last missing part was replaced");
            NativeClick(view, null, Field<Button>(view, "undoButton")); yield return null;
            Require(document.Parts.Count == 3 && document.PrimaryPartId == "missing" && document.Groups.Count == 1,
                "One native Undo did not restore missing row and hierarchy");
            Require(Field<Button>(view, "replaceMissingButton").gameObject.activeSelf,
                "Undo did not restore visible replacement entry");
            NativeClick(view, null, Field<Button>(view, "redoButton")); yield return null;
            Require(document.Parts.Count == 4 && document.Groups.Count == 2, "Native Redo failed");
            NativeClick(view, null, "Save"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(!document.IsDirty, "Replacement Save failed"); controller.Close(true);
        }
        var reopened = new CompositeBlueprintStore(path).All().Single();
        Require(reopened.parts.Count == 4 && reopened.parts.Single(part => part.stableId == reopened.primaryPartId).position.ToVector3().magnitude < .001f &&
            reopened.parts.Single(part => part.stableId == reopened.primaryPartId).scale.x == 2 && reopened.parts.Single(part => part.stableId == "a").position.x == 3 &&
            reopened.groups.Count == 2 && reopened.placementSupportId == "s" && reopened.sourceDocuments[0].text == evidence.Text,
            "Replacement disk reopen lost structure or original evidence");
        File.WriteAllText(Path.Combine(output, "replacement-check.txt"),
            "PASS actual Inspector/Tools entry; exact origin/full rotation preview for selected parts and direct single/whole blueprint catalogue replacements; Cancel restores visuals without document mutation; one Undo/Redo, Save/reopen, stale prefab and ordinary placement recovery; own scales, relative poses, groups, primary/pivots, independent supports and source retained. No game construction proof.");
    }

    internal static IEnumerable<object> RunMissingPositioning(Camera template, TMP_Text font, GameObject wall, string output)
    {
        foreach (bool known in new[] { true, false })
        {
            string path = Path.Combine(output, "position-" + known + "-" + Guid.NewGuid().ToString("N") + ".json");
            var store = new CompositeBlueprintStore(path);
            Quaternion rotation = Quaternion.Euler(25, 30, 15);
            Vector3 origin = new Vector3(7, 3, -2);
            var missing = new CompositeBlueprintStore.Part { stableId = "m", prefabName = "qa_absent", displayName = "Missing",
                position = new CompositeBlueprintStore.VectorData(origin), rotation = new CompositeBlueprintStore.QuaternionData(rotation),
                baseScale = new CompositeBlueprintStore.VectorData(new Vector3(2, 1, .5f)),
                metadata = known ? CompositeBlueprintStore.PrefabMetadata.FromGeometry(new BlueprintPrefabMetadata(1,
                    "qa_absent", "Recorded", "piece", "QA", new Point3(1, 1, 1), new Point3(0, 0, -1), new Point3(2, 2, 1))) : null };
            Require(store.TrySaveDocument("Position", "OTHER", new[] { missing,
                new CompositeBlueprintStore.Part { stableId = "a", prefabName = "woodwall", displayName = "Own wall",
                    position = new CompositeBlueprintStore.VectorData(new Vector3(-3, 0, 0)), rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity) },
                new CompositeBlueprintStore.Part { stableId = "b", prefabName = "woodwall", displayName = "Own second wall",
                    position = new CompositeBlueprintStore.VectorData(new Vector3(-1, 0, 0)), rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity) }
            }, Array.Empty<CompositeBlueprintStore.Group>(), Array.Empty<CompositeBlueprintStore.VectorData>(), "m", null,
                Array.Empty<CompositeBlueprintStore.Support>(), null, out var saved, out string error), error);
            var input = new InputFrames();
            using (var controller = new BlueprintEditorController(store, template, font,
                name => name == "woodwall" ? wall : null, name => name,
                () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); }, editorInput: input))
            {
                Require(controller.OpenExisting(saved, out error), error); yield return null;
                var document = controller.Document;
                var view = Field<BlueprintEditorView>(controller, "view");
                var scene = Field<BlueprintEditorScene>(controller, "scene");
                document.SelectOnly("m"); document.ToggleSelection("a"); document.ToggleSelection("b"); Call(controller, "Bind", (object)null);
                int revision = document.RevisionToken;
                NativeClick(view, null, "InspectorPositionMissing"); yield return null;
                Require(view.HasModal && Field<TMP_Text>(view, "modalMessage").text.Contains(BuildWorksLocalization.Text(
                    known ? "editor.position_known" : "editor.position_unknown")), "Positioning did not disclose bounds certainty");
                Vector3 expectedCentre = origin + rotation * new Vector3(2, 1, 0);
                if (known)
                    Require(scene.TryGetPositioningOffset("m", null, out Vector3 residual) && residual.magnitude < .001f,
                        "Rotated pinned envelope copy centre did not align");
                document.SelectOnly("a");
                NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
                Require(document.RevisionToken == revision && document.Parts[1].Position.X == -3, "Stale positioning selection committed");
                document.SelectOnly("m"); document.ToggleSelection("a"); document.ToggleSelection("b"); Call(controller, "Bind", (object)null);
                NativeClick(view, null, "InspectorPositionMissing"); yield return null;
                NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
                string[] copiedIds = document.Parts.Where(part => part.StableId != "a" && part.StableId != "b" && part.StableId != "m")
                    .Select(part => part.StableId).ToArray();
                var copiedAnchor = document.Parts.Single(part => part.StableId == document.Groups.Single(group => group.StableId == document.ActiveNodeId).PivotPartId);
                Vector3 ownPosition = V(copiedAnchor.Position);
                Rotation3 ownRotation = copiedAnchor.Rotation;
                Require(document.Parts.Count == 5 && document.PrimaryPartId == "m" && V(document.Parts[0].Position) == origin &&
                    document.Parts[1].Position.X == -3 && document.Parts[2].Position.X == -1 &&
                    Quaternion.Angle(new Quaternion((float)ownRotation.X, (float)ownRotation.Y, (float)ownRotation.Z, (float)ownRotation.W), rotation) < .01f &&
                    (known || Vector3.Distance(ownPosition, origin) < .001f), "Positioning changed target or unknown-origin alignment");
                CheckAlignedCopyAxes(controller, input, rotation, copiedIds);
                Require(document.Undo() && document.Parts.Count == 3, "Copy alignment requires more than one Undo");
                foreach (bool explicitPrimary in new[] { false, true })
                {
                    document.SelectOnly("m"); Call(controller, "Bind", (object)null);
                    var source = new BlueprintEditorDocument(null, "Catalogue axes", "OTHER", new[] {
                        new BlueprintEditorPart("other", "woodwall", "Other", new Point3(2, 0, 0), new Rotation3(.2, .3, .1, .8)),
                        new BlueprintEditorPart("anchor", "woodwall", "Anchor", new Point3(4, 1, 0), new Rotation3(.4, .2, .3, .7))
                    }, primaryPartId: explicitPrimary ? "anchor" : null);
                    Call(controller, "RequestCatalogAlignment", document, document.RevisionToken, "m", source, true); yield return null;
                    Require(view.HasModal, "Catalogue copy positioning preview absent");
                    NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
                    string[] catalogueCopies = document.Parts.Where(part => part.StableId != "m" && part.StableId != "a" && part.StableId != "b")
                        .Select(part => part.StableId).ToArray();
                    Require(catalogueCopies.Length == 2, "Catalogue positioning lost copies");
                    CheckAlignedCopyAxes(controller, input, rotation, catalogueCopies);
                    Require(source.Parts[0].Position.X == 2 && !source.CanUndo && document.Undo() && document.Parts.Count == 3,
                        "Catalogue alignment modified source or needed more than one Undo");
                }
                Require(document.Redo() && document.Undo(), "Catalogue copy history failed");
                document.SelectOnly("m"); document.ToggleSelection("a"); document.ToggleSelection("b"); Call(controller, "Bind", (object)null);
                NativeClick(view, null, "InspectorPositionMissing"); yield return null;
                NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
                copiedIds = document.Parts.Where(part => part.StableId != "m" && part.StableId != "a" && part.StableId != "b").Select(part => part.StableId).ToArray();
                copiedAnchor = document.Parts.Single(part => part.StableId == document.Groups.Single(group => group.StableId == document.ActiveNodeId).PivotPartId);
                ownPosition = V(copiedAnchor.Position);
                NativeClick(view, null, "Save"); yield return null;
                NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
                Require(!document.IsDirty, "Positioning save failed");
                controller.Close(true);
                var reopened = new CompositeBlueprintStore(path).All().Single();
                Require(controller.OpenExisting(reopened, out error), error); yield return null;
                scene = Field<BlueprintEditorScene>(controller, "scene");
                Require(controller.Document.Parts.Count == 5 && controller.Document.PrimaryPartId == "m" &&
                    Vector3.Distance(V(controller.Document.Parts.Single(part => part.StableId == copiedAnchor.StableId).Position), ownPosition) < .001f &&
                    (controller.Document.Parts[0].Metadata?.Minimum != null) == known,
                    "Positioning reopen lost marker, certainty or own pose");
                if (known) Require(scene.TryGetBounds(copiedIds, out Bounds bounds) &&
                    Vector3.Distance(bounds.center, expectedCentre) < .001f, "Reopened positioning bounds changed");
                controller.Close(true);
            }
        }
        File.WriteAllText(Path.Combine(output, "positioning-check.txt"),
            "PASS rotated XYZ, pinned basis and off-origin recorded envelope centre; unknown origin/rotation with honest warning; stale selection refusal; native Save/reopen retains missing and own pose. No Valheim proof.");
    }

    private static void CheckAlignedCopyAxes(BlueprintEditorController controller, InputFrames input, Quaternion expectedRotation, string[] copiedIds)
    {
        var document = controller.Document;
        var view = Field<BlueprintEditorView>(controller, "view");
        var anchor = (BlueprintEditorPart)Call(controller, "SelectedPrimaryPart");
        Require(anchor != null && copiedIds.Contains(anchor.StableId) && Quaternion.Angle(new Quaternion(
            (float)anchor.Rotation.X, (float)anchor.Rotation.Y, (float)anchor.Rotation.Z, (float)anchor.Rotation.W), expectedRotation) < .01f,
            "Aligned group has no copied primary or wrong local orientation");
        var originals = document.Parts.Where(part => !copiedIds.Contains(part.StableId)).ToDictionary(part => part.StableId, part => part.Position);
        var positions = copiedIds.ToDictionary(id => id, id => document.Parts.Single(part => part.StableId == id).Position);
        Set(controller, "transformFrame", Enum.Parse(Field<object>(controller, "transformFrame").GetType(), "Local"));
        Set(controller, "localSpace", true);
        Vector2 mouse = view.ViewportScreenRect().center;
        Frame(controller, input, mouse); Frame(controller, input, mouse, 0, KeyCode.G); Frame(controller, input, mouse);
        Frame(controller, input, mouse, 0, KeyCode.X); TextFrame(controller, input, mouse, "1");
        Vector3 delta = expectedRotation * Vector3.right;
        Require(Field<bool>(controller, "keyboardValid") && Vector3.Distance(Field<Vector3>(controller, "dragTranslation"), delta) < .001f,
            "G X uses an axis different from the aligned anchor");
        Frame(controller, input, mouse, 0, KeyCode.Return); Frame(controller, input, mouse);
        Require(copiedIds.All(id => Vector3.Distance(V(document.Parts.Single(part => part.StableId == id).Position), V(positions[id]) + delta) < .001f) &&
            originals.All(pair => document.Parts.Single(part => part.StableId == pair.Key).Position.Equals(pair.Value)) && document.Undo(),
            "Local G moved originals/marker or did not have one Undo");
        Call(controller, "Bind", (object)null);
    }

    internal static IEnumerable<object> RunOutlinerFilters(TMP_Text font, string output)
    {
        var unit = new Point3(1, 1, 1); var zero = new Point3(0, 0, 0); var rotation = new Rotation3(0, 0, 0, 1);
        var known = new BlueprintPrefabMetadata(1, "woodwall", "Recorded wall", "Structure", "QA", unit,
            sourceId: "qa.author.mod", sourceVersion: "7.3");
        var other = new BlueprintPrefabMetadata(1, "woodwall", "Recorded furniture", "Furniture", "QA", unit, sourceId: "qa.other");
        var doc = new BlueprintEditorDocument(null, "Tree filters", "OTHER", new[] {
            new BlueprintEditorPart("missing", "qa_absent", "Пропавшая", zero, rotation, "inner"),
            new BlueprintEditorPart("known", "woodwall", "Деревянная стена", zero, rotation, "inner", scale: new Point3(2,1,1), signText: "Тест", metadata: known),
            new BlueprintEditorPart("hidden", "woodwall", "Скрытая", zero, rotation, "other", visible: false, locked: true),
            new BlueprintEditorPart("root", "woodwall", "Отдельная", zero, rotation, metadata: other)
        }, new[] { new BlueprintEditorGroup("outer", "Сборка"), new BlueprintEditorGroup("inner", "Вложенная", parentGroupId: "outer"),
            new BlueprintEditorGroup("other", "Другой узел") });
        doc.SelectOnly("known"); int revision = doc.RevisionToken;
        string[] hierarchy = doc.Parts.Select(part => part.StableId + ":" + part.ParentGroupId)
            .Concat(doc.Groups.Select(group => group.StableId + ":" + group.ParentGroupId)).ToArray();
        using (var view = new BlueprintEditorView(font))
        {
            view.Bind(doc, true, new[] { "missing" }, null); yield return null;
            var rows = Field<RectTransform>(view, "outlinerRows");
            var search = Field<TMP_InputField>(view, "outlinerSearch");
            NativeClick(view, null, rows.Find("Row_outer/Disclosure").GetComponent<Button>()); yield return null;
            Require(!rows.Find("Row_missing"), "Tree fixture was not initially collapsed");
            NativeClick(view, null, "OutlinerFilter"); yield return null;
            ClickOutlinerChoice(view, "OutlinerFilter4"); yield return null;
            Require(rows.Find("Row_outer") && rows.Find("Row_inner") && rows.Find("Row_missing") &&
                !rows.Find("Row_known") && !rows.Find("Row_other") && !rows.Find("Row_root") &&
                !rows.Find("Row_outer/Disclosure").GetComponent<Button>().interactable,
                "Missing filter lost a collapsed ancestor path or included unrelated parts");
            string picked = null; view.NodeClicked += (id, ctrl, shift) => picked = id;
            NativeClick(view, null, "Row_missing"); Require(picked == "missing", "Filtered row lost its native selection identity");
            NativeClick(view, null, "OutlinerFilter"); yield return null;
            ClickOutlinerChoice(view, "OutlinerFilterReset"); yield return null;
            Require(!rows.Find("Row_missing") && Field<HashSet<string>>(view, "collapsedGroups").Contains("outer"),
                "Reset changed the original collapsed state");
            foreach (var test in new[] { (scope: 2, query: "qa_absent"), (scope: 1, query: "ДЕРЕВЯННАЯ"),
                (scope: 3, query: "QA.AUTHOR.MOD"), (scope: 3, query: "7.3"), (scope: 4, query: "Structure") })
            {
                NativeClick(view, null, "OutlinerFilter"); yield return null;
                ClickOutlinerChoice(view, "OutlinerScope" + test.scope); yield return null;
                Require(EventSystem.current.currentSelectedGameObject == search.gameObject,
                    "Choosing a search field did not focus its actual input");
                search.text = test.query; yield return null;
                string id = test.scope == 2 ? "missing" : "known";
                Require(rows.Find("Row_outer") && rows.Find("Row_inner") && rows.Find("Row_" + id) &&
                    !rows.Find("Row_root") && !rows.Find("Row_hidden"), "Scoped query lost its ancestors or matched the wrong field");
            }
            Call(view, "ResetOutlinerFilter");
            int[] modes = { 1, 2, 3, 5, 6, 7, 8 };
            string[][] expected = { new[] { "known" }, new[] { "hidden" }, new[] { "hidden" }, new[] { "missing", "hidden" },
                new[] { "missing", "hidden" }, new[] { "known" }, new[] { "known" } };
            for (int i = 0; i < modes.Length; ++i)
            {
                NativeClick(view, null, "OutlinerFilter"); yield return null;
                ClickOutlinerChoice(view, "OutlinerFilter" + modes[i]); yield return null;
                foreach (BlueprintEditorPart part in doc.Parts)
                    Require((bool)rows.Find("Row_" + part.StableId) == expected[i].Contains(part.StableId),
                        "Filter " + modes[i] + " misclassified " + part.StableId);
            }
            search.text = "No such record"; yield return null;
            Require(rows.Find("NoMatches"), "No-match search has no reset action");
            NativeClick(view, null, "NoMatches"); yield return null;
            Require(search.text == "" && Field<int>(view, "outlinerFilter") == 0 && Field<int>(view, "outlinerSearchScope") == 0 &&
                !rows.Find("Row_missing"), "No-match reset lost the previous collapse or left a hidden filter");
            Require(doc.RevisionToken == revision && !doc.IsDirty && !doc.CanUndo && doc.Selection.SequenceEqual(new[] { "known" }) &&
                hierarchy.SequenceEqual(doc.Parts.Select(part => part.StableId + ":" + part.ParentGroupId)
                    .Concat(doc.Groups.Select(group => group.StableId + ":" + group.ParentGroupId))),
                "Search/filter changed document history, selection or hierarchy");
            view.Bind(new BlueprintEditorDocument(null, "Empty tree", "OTHER", Array.Empty<BlueprintEditorPart>()), true, null, null);
            yield return null;
            Require(!rows.Find("NoMatches") && Field<List<Action>>(view, "outlinerRowBuilders").Count == 0,
                "Unfiltered empty document displayed a misleading search reset");
            search.text = "No such record"; yield return null;
            Require(rows.Find("NoMatches"), "Filtered empty document lost its search reset");
            NativeClick(view, null, "NoMatches"); yield return null;
            Require(!rows.Find("NoMatches"), "Reset left a misleading action in the empty document");
            var heavy = new BlueprintEditorDocument(null, "Heavy tree", "OTHER",
                Enumerable.Range(0, 50000).Select(i => new BlueprintEditorPart("heavy" + i, "wall", "Wall", zero, rotation, "g" + i % 64)),
                Enumerable.Range(0, 64).Select(i => new BlueprintEditorGroup("g" + i, "Group")));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            view.Bind(heavy, true, new[] { "heavy49999" }, null); watch.Stop(); yield return null;
            Require(Field<List<Action>>(view, "outlinerRowBuilders").Count == 50064 && rows.childCount < 64,
                "Grouped heavy tree lost bounded row virtualization");
            NativeClick(view, null, "OutlinerFilter"); yield return null;
            ClickOutlinerChoice(view, "OutlinerFilter4"); yield return null;
            Require(rows.Find("Row_g15") && rows.Find("Row_heavy49999") &&
                Field<List<Action>>(view, "outlinerRowBuilders").Count == 2 && !heavy.IsDirty,
                "Missing filter failed near the end of the grouped heavy tree");
            File.WriteAllText(Path.Combine(output, "outliner-filters-check.txt"),
                "PASS actual native filter-menu clicks, missing collapsed ancestors, unchanged collapse/selection/revision/history/hierarchy; " +
                "name/record or prefab ID/source/version/type queries with input focus; unknown metadata/hidden/locked/sign/scale; " +
                "no-match/reset and unfiltered/filtered empty document; 64 groups + 50000 parts, virtualized rows, last missing record. Grouped tree bind " + watch.ElapsedMilliseconds +
                " ms. Workbench only; no Valheim proof.");
        }
    }

    private static void ClickOutlinerChoice(BlueprintEditorView view, string name)
    {
        var menu = Field<GameObject>(view, "outlinerMenu");
        var button = menu.GetComponentsInChildren<Button>().Single(value => value.name == name);
        var scroll = menu.GetComponent<ScrollRect>();
        Canvas.ForceUpdateCanvases();
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, button.transform);
        float y = Mathf.Clamp(-bounds.center.y - scroll.viewport.rect.height * .5f, 0,
            Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height));
        scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, y);
        Canvas.ForceUpdateCanvases();
        NativeClick(view, null, button);
    }

    internal static IEnumerable<object> RunPartialThumbnails(Camera template, TMP_Text font, GameObject wall, string output)
    {
        GameObject model = PlacementGhostPreviewView.CreateVisualClone(wall, "PartialThumbnailSource", BlueprintThumbnailRenderer.ThumbnailLayer);
        model.SetActive(false);
        Piece piece = model.AddComponent<Piece>();
        try
        {
        var blueprint = new CompositeBlueprintStore.Blueprint { id = "partial-" + Guid.NewGuid().ToString("N"), name = "Неполный чертёж" };
        blueprint.parts.Add(new CompositeBlueprintStore.Part { stableId = "available", prefabName = "woodwall",
            position = new CompositeBlueprintStore.VectorData { x = -2 }, rotation = new CompositeBlueprintStore.QuaternionData { w = 1 },
            scale = new CompositeBlueprintStore.VectorData { x = .5f, y = 2, z = 1 }, baseScale = new CompositeBlueprintStore.VectorData { x = 2, y = 1, z = .5f } });
        blueprint.parts.Add(new CompositeBlueprintStore.Part { stableId = "known", prefabName = "qa_absent",
            position = new CompositeBlueprintStore.VectorData { x = 2, y = 1 }, rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(0, 90, 0)),
            scale = new CompositeBlueprintStore.VectorData { x = 2, y = 3, z = .5f },
            metadata = CompositeBlueprintStore.PrefabMetadata.FromGeometry(new BlueprintPrefabMetadata(1, "qa_absent", "Recorded", "piece", "Valheim",
                new Point3(2, .5, 3), new Point3(1, 2, 3), new Point3(3, 4, 5))) });
        blueprint.parts.Add(new CompositeBlueprintStore.Part { stableId = "unknown", prefabName = "woodwall", scaleIsAbsolute = true,
            position = new CompositeBlueprintStore.VectorData { z = 5 }, rotation = new CompositeBlueprintStore.QuaternionData { w = 1 },
            scale = new CompositeBlueprintStore.VectorData { x = 100, y = 200, z = 300 } });
        Piece[] pieces = { piece, null, piece };
        Mesh markerMesh;
        using (var preview = new BlueprintThumbnailRenderer.PreviewSession(blueprint, pieces))
        {
            var root = Field<GameObject>(preview, "root");
            Transform available = root.transform.Find("Part_0"), known = root.transform.Find("Missing_known"), unknown = root.transform.Find("Missing_unknown");
            Require(available && known && unknown, "Partial preview dropped available parts or missing records");
            Require(available.localScale == new Vector3(1, 2, .5f) && available.localPosition.x == -2,
                "Partial preview changed available part basis/pose");
            Require(known.localScale == new Vector3(4, 1.5f, 1.5f) && known.localPosition == new Vector3(2, 1, 0) &&
                Quaternion.Angle(known.localRotation, Quaternion.Euler(0, 90, 0)) < .001f,
                "Recorded marker lost prefab basis, multiplier or rotation");
            markerMesh = known.GetComponent<MeshFilter>().sharedMesh;
            Require(markerMesh.GetTopology(0) == MeshTopology.Lines && markerMesh.bounds.center == new Vector3(2, 3, 4) &&
                markerMesh.bounds.size == new Vector3(2, 2, 2), "Recorded marker invented a centered mesh instead of recorded local bounds");
            Require(unknown.localScale == Vector3.one * .3f && unknown.localPosition == new Vector3(0, 0, 5),
                "Unknown marker inferred size from an unresolved absolute scale");
            Require(!root.GetComponentsInChildren<Collider>(true).Any() && blueprint.parts[2].scaleIsAbsolute &&
                blueprint.parts[2].scale.x == 100, "Partial preview mutated records or acquired collisions");
            preview.Render(20, 15, 1);
            Require(preview.Texture, "Partial rotatable preview has no GPU target");
            RuntimeEditorAcceptance.SavePixels(preview.Texture, Path.Combine(output, "partial-preview.png"));
        }
        yield return null; yield return null;
        Require(!markerMesh, "Missing marker mesh leaked after preview disposal");
        using (var renderer = new BlueprintThumbnailRenderer())
        {
            Sprite partial = renderer.Refresh(blueprint, pieces, out string error);
            Require(partial && error == null, "Partial thumbnail failed: " + error);
            Color32[] pixels = partial.texture.GetPixels32();
            Require(pixels.Any(pixel => pixel.a == 0) && pixels.Any(pixel => pixel.a > 24), "Partial thumbnail lost transparent background or all geometry");
            File.WriteAllBytes(Path.Combine(output, "partial-thumbnail.png"), partial.texture.EncodeToPNG());
            string disk = (string)Call(renderer, "ThumbnailPath", blueprint.id);
            Require(!File.Exists(disk), "Partial placeholder thumbnail persisted across mod changes");
            var item = new BlueprintEditorCatalogItem("blueprint_" + blueprint.id, blueprint.name, null, "OTHER", "WOOD", "external_blueprints", 0,
                blueprint: blueprint, warningReason: BuildWorksLocalization.Text("editor.missing_dimensions", "qa_absent"), resourcesKnown: false);
            var store = new CompositeBlueprintStore(Path.Combine(output, "partial-catalog-" + Guid.NewGuid().ToString("N") + ".json"));
            using (var controller = new BlueprintEditorController(store, template, font, name => name == "woodwall" ? wall : null, name => name,
                () => new[] { item }, message => { throw new InvalidOperationException(message); }))
            {
                Require(controller.OpenNew(out error), error); yield return null;
                var view = Field<BlueprintEditorView>(controller, "view");
                Call(controller, "RequestCatalog"); yield return null;
                NativeClick(view, null, "CatalogBlueprintsMode"); yield return null;
                var grid = Field<RectTransform>(view, "catalogGrid");
                Require(grid.GetComponentsInChildren<TMP_Text>().Any(text => text.name == "EmptyPreview"), "Missing icon still leaves a blank card");
                view.UpdateCatalogThumbnail(blueprint.id, partial); yield return null;
                Require(!grid.GetComponentsInChildren<TMP_Text>().Any(text => text.name == "EmptyPreview") &&
                    !Field<RectTransform>(view, "catalogPreview").GetComponentsInChildren<TMP_Text>().Any(text => text.name == "EmptyPreview"),
                    "Ready preview is covered by stale empty-image explanation");
                Require(grid.GetComponentsInChildren<TMP_Text>().Any(text => text.text.StartsWith("[!]")) &&
                    Field<RectTransform>(view, "catalogPreview").GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains(BuildWorksLocalization.Text("catalog.thumbnail_markers"))),
                    "Partial thumbnail hid the warning or marker uncertainty");
                controller.Close(true);
            }
            var cache = new CompositeBlueprintStore.Blueprint { id = "partial-cache-" + Guid.NewGuid().ToString("N"), name = "Cache" };
            cache.parts.Add(blueprint.parts[0]);
            Sprite complete = renderer.Refresh(cache, new[] { piece }, out error);
            Require(complete, "Complete cache fixture failed: " + error);
            string cachePath = (string)Call(renderer, "ThumbnailPath", cache.id);
            byte[] originalPng = File.ReadAllBytes(cachePath);
            Require(!renderer.GetOrQueue(cache, new Piece[] { null }), "Complete disk preview masked newly missing model");
            Require(renderer.ProcessOne(out string id, out Sprite missing) && id == cache.id && missing,
                "Partial cache transition did not render a marker");
            Require(originalPng.SequenceEqual(File.ReadAllBytes(cachePath)), "Partial thumbnail overwrote the complete disk cache");
            Require(renderer.GetOrQueue(cache, new[] { piece }), "Restored model reused partial preview instead of complete cache");
            renderer.Delete(cache.id); renderer.Delete(blueprint.id);
        }
        File.WriteAllText(Path.Combine(output, "partial-thumbnail-check.txt"),
            "PASS actual partial PNG and rotatable preview; available root basis/pose, recorded offset/rotated bounds, unknown fixed uncertainty marker, inert absolute record, transparency, disposal, complete/partial/complete cache isolation and actual catalogue fallback/update/warning. No Valheim proof.");
        }
        finally { Object.Destroy(model); }
    }

    internal static IEnumerable<object> RunPrefabMetadata(Camera template, TMP_Text font, string output)
    {
        var prefab = new GameObject("metadata_piece");
        GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.transform.SetParent(prefab.transform, false);
        child.transform.localPosition = new Vector3(3, 5, 7);
        child.transform.localRotation = Quaternion.Euler(0, 90, 0);
        child.transform.localScale = new Vector3(2, 4, 6);
        Object.DestroyImmediate(child.GetComponent<Collider>());
        prefab.AddComponent<Piece>().m_name = "Metadata fixture";
        prefab.transform.SetPositionAndRotation(new Vector3(10, 20, 30), Quaternion.Euler(10, 25, 35));
        prefab.transform.localScale = new Vector3(2, .5f, 3);
        prefab.SetActive(false);
        try
        {
            var metadata = BlueprintPrefabMetadataCapture.Capture(prefab);
            Require(metadata != null && metadata.sourceId == null && metadata.kind == "Piece" &&
                (metadata.minimum.Value.ToVector3() - new Vector3(0, 3, 6)).magnitude < .0001f &&
                (metadata.maximum.Value.ToVector3() - new Vector3(6, 7, 8)).magnitude < .0001f,
                "Prefab snapshot did not cancel root TRS or invented ownership");
            Require(!BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(prefab, out _), "Shared Hammer helper changed inactive-root behavior");
            bool available = true;
            string path = Path.Combine(output, "metadata-" + Guid.NewGuid().ToString("N") + ".json");
            var store = new CompositeBlueprintStore(path);
            store.ConfigurePrefabMetadata(_ => available ? BlueprintPrefabMetadataCapture.Capture(prefab) : null);
            var part = new CompositeBlueprintStore.Part { prefabName = prefab.name, stableId = "recorded", displayName = "Recorded",
                position = new CompositeBlueprintStore.VectorData { x = 4 }, rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(0, 30, 0)),
                baseScale = new CompositeBlueprintStore.VectorData(prefab.transform.localScale), scale = new CompositeBlueprintStore.VectorData { x = 1, y = .75f, z = 2 } };
            Require(store.TrySave("Metadata", new[] { part }, Array.Empty<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
            child.transform.localPosition += Vector3.right;
            Require(store.TryUpdateParts(saved, saved.parts, saved.anchors, out error) &&
                Mathf.Abs(saved.parts[0].metadata.minimum.Value.x - 1) < .0001f, "Prefab mutation reused cached bounds");
            available = false;
            using (var controller = new BlueprintEditorController(store, template, font, _ => null, name => name,
                () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); }))
            {
                Require(controller.OpenExisting(saved, out error), error); yield return null;
                var scene = Field<BlueprintEditorScene>(controller, "scene");
                GameObject sceneRoot = Field<GameObject>(scene, "root");
                MeshFilter frame = sceneRoot.GetComponentsInChildren<MeshFilter>(true).Single(value => value.gameObject.name == "Missing_recorded");
                Require(frame.sharedMesh.GetTopology(0) == MeshTopology.Lines && frame.sharedMesh.GetIndexCount(0) == 24,
                    "Saved bounds were shown as a solid fabricated model");
                Vector3 local = saved.parts[0].metadata.minimum.Value.ToVector3();
                Vector3 expected = sceneRoot.transform.TransformPoint(new Vector3(4, 0, 0) + Quaternion.Euler(0, 30, 0) * Vector3.Scale(local, new Vector3(2, .375f, 6)));
                Require((frame.transform.TransformPoint(local) - expected).magnitude < .001f &&
                    !scene.TryGetBounds(new[] { "recorded" }, out _), "Bounds frame lost pose/scale or became model geometry");
                var target = new RenderTexture(960, 540, 24);
                try
                {
                    scene.Camera.targetTexture = target; scene.Camera.Render();
                    RuntimeEditorAcceptance.SavePixels(target, Path.Combine(output, "metadata-bounds-frame.png"));
                }
                finally { scene.Camera.targetTexture = null; Object.DestroyImmediate(target); }
                Require(controller.Document.Rename("recorded", "Retained"), "Could not edit missing record");
                Call(controller, "Bind", (object)null);
                var view = Field<BlueprintEditorView>(controller, "view");
                NativeClick(view, null, "Save"); yield return null;
                NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
                Require(!controller.Document.IsDirty, "Metadata save failed");
                controller.Close(true);
            }
            var reopened = new CompositeBlueprintStore(path).All().Single();
            Require(reopened.parts[0].metadata.minimum.Value.x > .999f && reopened.parts[0].displayName == "Retained",
                "Missing editor save/reopen erased captured metadata");
            File.WriteAllText(Path.Combine(output, "metadata-check.txt"),
                "PASS actual capture: nested rotated/nonuniform child, inactive translated/rotated/scaled prefab; fresh save, unknown owner; missing recorded frame exact pose/scale, no model bounds; native Save and disk reopen. No game construction proof.");
        }
        finally { Object.DestroyImmediate(prefab); }
    }

    internal static IEnumerable<object> RunMissingExchange(Camera template, TMP_Text font, GameObject wall, string output)
    {
        string folder = Path.Combine(output, "missing-exchange-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string sourcePath = Path.Combine(folder, "12-missing-choice.blueprint");
        const string original = "#Name:QA113 Missing Choice\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\nqa_absent;;2;0;0;0;0;0;1;\"unknown-state\";1;1;1";
        File.WriteAllText(sourcePath, original);
        var store = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
        object context = new object();
        store.ConfigureImportScale(name => name == "woodwall" ? new CompositeBlueprintStore.VectorData(wall.transform.localScale)
            : (CompositeBlueprintStore.VectorData?)null, () => context);
        using (var controller = new BlueprintEditorController(store, template, font, name => name == "woodwall" ? wall : null,
            name => name, () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); }))
        {
            Require(controller.OpenNew(out string error), error);
            var view = Field<BlueprintEditorView>(controller, "view");
            view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, null, closed));
            var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
            for (int frame = 0; frame < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++frame)
            { view.Tick(); yield return null; }
            Call(exchange, "Browse", folder);
            for (int frame = 0; frame < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++frame)
            { view.Tick(); yield return null; }
            int index = Array.IndexOf(Field<string[]>(exchange, "entries"), sourcePath);
            Require(index >= 0 && index < 7, "Missing exchange fixture absent");
            NativeClick(view, null, "ExchangeEntry" + index); view.Tick(); yield return null;
            for (int iteration = 0; iteration < 2; ++iteration)
            {
                NativeClick(view, null, "ExchangeSubmit");
                for (int frame = 0; frame < 300 && ImportPending(exchange); ++frame)
                { view.Tick(); yield return null; }
                Require(Field<CompositeBlueprintStore.MissingPartsReview>(exchange, "missingReview").Parts == 1 &&
                    !Field<bool>(exchange, "omitMissingParts") && Field<Button>(exchange, "missingChoice").interactable &&
                    Field<TMP_Text>(exchange, "contentsDetails").text.Contains("qa_absent"), "Missing choice default/count/ID wrong");
                NativeClick(view, null, "ExchangeMissingChoice"); yield return null;
                Require(Field<bool>(exchange, "omitMissingParts"), "Missing choice did not toggle");
                if (iteration == 0) { NativeClick(view, null, "ExchangeContentsBack"); yield return null; }
            }
            context = new object();
            NativeClick(view, null, "ExchangeSubmit"); yield return null;
            view.Tick();
            Require(!Field<bool>(exchange, "importing") && !Directory.Exists(store.ImportDirectory) &&
                Field<TMP_Text>(exchange, "message").text == BuildWorksLocalization.Text("placement.contents_stale"),
                "Stale-context confirmation published a copy or hid its refusal");
            NativeClick(view, null, "ExchangeContentsBack"); view.Tick(); yield return null;
            NativeClick(view, null, "ExchangeSubmit");
            for (int frame = 0; frame < 300 && ImportPending(exchange); ++frame)
            { view.Tick(); yield return null; }
            Require(Field<bool>(exchange, "reviewingContents") && Field<CompositeBlueprintStore.MissingPartsReview>(exchange, "missingReview")?.Parts == 1 &&
                Field<Button>(exchange, "missingChoice").gameObject.activeInHierarchy,
                "Fresh missing review did not recover: review=" + Field<bool>(exchange, "reviewingContents") +
                "; pending=" + ImportPending(exchange) +
                "; importing=" + Field<bool>(exchange, "importing") + "; completed=" + Field<bool>(exchange, "completed") +
                "; primary=" + Field<Button>(exchange, "primary").interactable +
                "; missing=" + Field<CompositeBlueprintStore.MissingPartsReview>(exchange, "missingReview")?.Parts +
                "; message=" + Field<TMP_Text>(exchange, "message").text);
            NativeClick(view, null, "ExchangeMissingChoice"); yield return null;
            NativeClick(view, null, "ExchangeSubmit");
            for (int frame = 0; frame < 300 && Field<bool>(exchange, "importing"); ++frame)
            { view.Tick(); yield return null; }
            Require(Field<bool>(exchange, "completed") && File.ReadAllText(sourcePath) == original, "Fresh confirmation failed or changed source");
            var imported = store.CatalogAll().Single();
            Require(imported.parts.Count == 1 && imported.sourceDocuments.Single().text == original,
                "UI explicit missing copy lost source or retained a removed record");
            NativeClick(view, null, "ExchangeClose"); yield return null;
            controller.Close(true);
            Require(controller.OpenExisting(imported, out error) && controller.Document.Parts.Count == 1, error ?? "One retained part did not open");
            controller.Close(true);
        }
        File.WriteAllText(Path.Combine(output, "missing-exchange-check.txt"),
            "PASS actual missing choice/ID/count/default/Back reset/native confirmation/stale host refusal/new confirmation/editor one-part open; no Valheim construction proof");
    }

    internal static IEnumerable<object> RunMissingCatalogue(Camera template, TMP_Text font, GameObject wall, string output)
    {
        string directory = Path.Combine(output, "c89-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(directory);
        string original = "#Name:Incomplete catalog\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\nmissing_catalog_model;;2;3;4;0;0;0;1;\"\";.001;.002;.003";
        string sourcePath = Path.Combine(directory, "missing.blueprint");
        File.WriteAllText(sourcePath, original);
        string storePath = Path.Combine(directory, "library.json");
        var store = new CompositeBlueprintStore(storePath);
        GameObject Resolve(string name) => name == "woodwall" ? wall : null;
        store.ConfigureImportScale(name => Resolve(name) ? new CompositeBlueprintStore.VectorData(wall.transform.localScale) : (CompositeBlueprintStore.VectorData?)null);
        Require(store.TryImportReviewed(CompositeBlueprintStore.ReviewImportFile(sourcePath, null), false, false, null, out string error), error);
        for (int frame = 0; frame < 300 && store.CatalogLoading; ++frame)
        { store.CatalogAll(); yield return null; }
        Require(!store.CatalogLoading, "Reviewed import catalogue task did not finish");
        var imported = store.CatalogAll().Single();
        BlueprintEditorController.EditorCatalogStatus(imported, name => Resolve(name), out string blocked, out string warning);
        Require(string.IsNullOrEmpty(blocked) && warning.Contains("missing_catalog_model"), "Production catalogue status blocks missing records");
        var repeated = new CompositeBlueprintStore.Blueprint();
        for (int i = 0; i < 50000; ++i)
            repeated.parts.Add(new CompositeBlueprintStore.Part {
                prefabName = i % 5 == 1 ? "missing_catalog_model" : i % 5 == 3 ? "absolute_only" : i % 5 == 4 ? "WoodWall" : "woodwall",
                scaleIsAbsolute = i % 5 == 2 || i % 5 == 3 });
        var resolutions = new Dictionary<string, int>(StringComparer.Ordinal);
        var statusClock = System.Diagnostics.Stopwatch.StartNew();
        BlueprintEditorController.EditorCatalogStatus(repeated, name => {
            resolutions.TryGetValue(name, out int count); resolutions[name] = count + 1;
            return name == "woodwall";
        }, out string repeatedBlocked, out string repeatedWarning);
        File.WriteAllText(Path.Combine(output, "catalog-status-check.txt"),
            "parts=50000; resolutions=" + string.Join(",", resolutions.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + ":" + pair.Value)) +
            "; elapsedMs=" + statusClock.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "; no Valheim timing proof");
        Require(repeatedBlocked == null && repeatedWarning == BuildWorksLocalization.Text("editor.missing_dimensions", "WoodWall, absolute_only, missing_catalog_model, woodwall") &&
            resolutions.Count == 3 && resolutions.Values.All(count => count == 1), "Catalogue status resolves the same prefab more than once or loses absolute/missing/case-sensitive warnings");
        resolutions.Clear();
        BlueprintEditorController.EditorCatalogStatus(repeated, name => {
            resolutions.TryGetValue(name, out int count); resolutions[name] = count + 1; return true;
        }, out repeatedBlocked, out repeatedWarning);
        Require(repeatedBlocked == null && repeatedWarning == BuildWorksLocalization.Text("editor.missing_dimensions", "absolute_only, woodwall") &&
            resolutions.Count == 3 && resolutions.Values.All(count => count == 1), "Catalogue status retained stale availability between passes");
        var overBudget = new CompositeBlueprintStore.Blueprint { importError = string.Empty };
        for (int i = 0; i <= CompositeBlueprintStore.EditorPartBudget; ++i) overBudget.parts.Add(new CompositeBlueprintStore.Part());
        BlueprintEditorController.EditorCatalogStatus(overBudget, name => true, out string limitError, out string limitWarning);
        Require(!string.IsNullOrEmpty(limitError) && limitWarning == null, "Empty import error bypasses catalogue limit status");
        var card = new BlueprintEditorCatalogItem("blueprint_" + imported.id, imported.name, null, imported.category,
            "blueprints", "external_blueprints", 0, imported, unavailableReason: blocked, warningReason: warning, resourcesKnown: false);
        var input = new InputFrames();
        var errors = new List<string>();
        using (var controller = new BlueprintEditorController(store, template, font, Resolve, name => name, () => new[] { card }, errors.Add, editorInput: input))
        {
            Require(controller.OpenNew(out error), error);
            var view = Field<BlueprintEditorView>(controller, "view");
            yield return null;
            Call(controller, "RequestCatalog"); yield return null;
            NativeClick(view, null, "CatalogBlueprintsMode"); yield return null;
            var button = view.RootCanvas.GetComponentsInChildren<Button>().Single(value => value.name == "Catalog_" + card.PrefabName);
            Require(button.GetComponentInChildren<TMP_Text>().text.StartsWith("[!]"), "Missing-model warning is absent on card");
            Call(view, "PreviewCatalogItem", card);
            var preview = Field<RectTransform>(view, "catalogPreview");
            Require(preview.GetComponentsInChildren<TMP_Text>(true).Any(value => value.name == "PreviewMetadata" &&
                value.text.Contains(BuildWorksLocalization.Text("editor.catalog_cost_unknown"))) &&
                !preview.GetComponentsInChildren<TMP_Text>(true).Any(value => value.name == "PreviewCostTitle"),
                "Unresolved construction cost/stations shown as zero/free");
            NativeClick(view, null, button); yield return null;
            Require(Field<BlueprintEditorDocument>(controller, "placementBlueprint")?.Parts.Count == 2 &&
                Field<BlueprintEditorDocument>(controller, "placementBlueprint").Parts[1].ScaleIsAbsolute,
                "Actual catalogue card did not start intact missing-model insertion");
            Vector2 mouse = view.ViewportScreenRect().center;
            Frame(controller, input, mouse); yield return null;
            Frame(controller, input, mouse, 1); yield return null;
            Frame(controller, input, mouse); yield return null;
            Require(controller.Document.Parts.Count == 2 && controller.Document.Parts[1].ScaleIsAbsolute &&
                Math.Abs(controller.Document.Parts[1].Scale.X - .001f) < .00000001 &&
                controller.Document.Sources.Single().Text == original && controller.Document.Undo() &&
                controller.Document.Parts.Count == 0 && controller.Document.Redo() && controller.Document.Parts.Count == 2,
                "Actual insertion/Undo/Redo lost records, basis or provenance");
            Call(controller, "CancelPartPlacement");
            Require((bool)Call(controller, "TrySave", false, true), "Inserted incomplete blueprint save failed");
            controller.Close(true);
            var saved = new CompositeBlueprintStore(storePath).All().Single();
            Require(saved.parts.Count == 2 && saved.parts[1].scaleIsAbsolute && saved.sourceDocuments.Single().text == original &&
                File.ReadAllText(sourcePath) == original && errors.Count == 0, "Inserted incomplete native copy lost data or changed source");
        }
    }

    private sealed class InputFrames : IBlueprintEditorInput
    {
        private HashSet<KeyCode> keys = new HashSet<KeyCode>(), previousKeys = new HashSet<KeyCode>();
        private int buttons, previousButtons;
        public Vector2 MousePosition { get; private set; }
        public Vector2 MouseScrollDelta { get; set; }
        public float UnscaledDeltaTime => 1f / 30f;
        public bool HasFocus { get; set; } = true;
        public string InputString { get; set; } = "";
        public float MouseX, MouseY;
        public bool WarpAvailable;
        public int WarpAttempts;
        public readonly List<Vector2> WarpTargets = new List<Vector2>();
        public bool TryWarpMousePosition(Vector2 position)
        {
            ++WarpAttempts;
            if (!WarpAvailable) return false;
            WarpTargets.Add(position);
            return true;
        }
        public bool GetKey(KeyCode key) => keys.Contains(key);
        public bool GetKeyDown(KeyCode key) => keys.Contains(key) && !previousKeys.Contains(key);
        public bool GetMouseButton(int button) => (buttons & 1 << button) != 0;
        public bool GetMouseButtonDown(int button) => GetMouseButton(button) && (previousButtons & 1 << button) == 0;
        public bool GetMouseButtonUp(int button) => !GetMouseButton(button) && (previousButtons & 1 << button) != 0;
        public float GetAxis(string axis) => axis == "Mouse X" ? MouseX : axis == "Mouse Y" ? MouseY : 0f;
        public void Next(Vector2 mouse, int heldButtons = 0, params KeyCode[] heldKeys)
        {
            previousKeys = keys; keys = new HashSet<KeyCode>(heldKeys);
            previousButtons = buttons; buttons = heldButtons; MousePosition = mouse;
            MouseScrollDelta = Vector2.zero; MouseX = MouseY = 0f;
            InputString = "";
        }
    }

    private static void Frame(BlueprintEditorController controller, InputFrames input, Vector2 mouse,
        int buttons = 0, params KeyCode[] keys)
    {
        input.Next(mouse, buttons, keys);
        controller.Update(); controller.LateUpdate(); Canvas.ForceUpdateCanvases();
    }

    private static void TestExactInspectorScale(BlueprintEditorController controller, BlueprintEditorView view, BlueprintEditorDocument doc)
    {
        var part = doc.Parts.First(x => x.StableId == "left");
        var scale = new Point3(1.234567f, 2f, .01001f);
        doc.SetPartProperties("left", part.DisplayName, part.Position, part.Rotation, scale);
        doc.SelectOnly("left"); Call(controller, "Bind", new object[] { null });
        Call(view, "SubmitInspector");
        Require(doc.Parts.First(x => x.StableId == "left").Scale.Equals(scale),
            "Unchanged rounded scale field changed exact XYZ");
        Field<TMP_InputField[]>(view, "inspectorScale")[0].SetTextWithoutNotify("154.320875");
        Call(view, "SubmitInspector");
        var resized = doc.Parts.First(x => x.StableId == "left").Scale;
        Require(resized.X > scale.X && Math.Abs(resized.Y / resized.X - scale.Y / scale.X) < .00001 &&
            Math.Abs(resized.Z / resized.X - scale.Z / scale.X) < .00001, "Resize lost XYZ proportions");
        Field<Button>(view, "inspectorProportionsButton").onClick.Invoke();
        var fields = Field<TMP_InputField[]>(view, "inspectorScale");
        double originalX = resized.X, originalZ = resized.Z;
        fields[1].text = "150"; fields[1].onEndEdit.Invoke(fields[1].text);
        resized = doc.Parts.First(x => x.StableId == "left").Scale;
        Require(resized.X == originalX && resized.Y == 1.5 && resized.Z == originalZ,
            "Independent edit changed untouched exact components");
        Field<Button>(view, "inspectorProportionsButton").onClick.Invoke();
        doc.SetPartProperties("left", part.DisplayName, part.Position, part.Rotation, new Point3(1,1,1));
        Call(controller, "Bind", new object[] { null });
        doc.ToggleSelection("right"); Call(controller, "Bind", new object[] { null });
        Field<Button>(view, "inspectorProportionsButton").onClick.Invoke();
        fields[0].text = "150"; fields[0].onEndEdit.Invoke(fields[0].text);
        Require(doc.Parts.Where(x => x.StableId == "left" || x.StableId == "right")
            .All(x => x.Scale.Equals(new Point3(1.5, 1, 1))), "Selection XYZ fields did not reach the shared transform");
        Require(doc.Undo(), "Selection XYZ edit cannot be undone");
        doc.SelectOnly("left"); Call(controller, "Bind", new object[] { null });
        Field<Button>(view, "inspectorProportionsButton").onClick.Invoke();
    }

    private static void TestPointHoverHints(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, BlueprintEditorDocument doc)
    {
        Frame(controller, input, Vector2.zero);
        Vector3 pivot = V(doc.Parts[0].Position);
        var gizmo = Field<TransformGizmoView>(scene, "gizmo");
        var scale = Field<LineRenderer>(gizmo, "scaleHandle");
        Vector3 scalePoint = (scale.GetPosition(0) + scale.GetPosition(2)) * .5f;
        Vector3 axisPoint = pivot + Vector3.right * gizmo.Scale * .7f;
        Vector3 freePoint = pivot + (scene.Camera.transform.right + scene.Camera.transform.up) * gizmo.Scale * .6f;
        string pointCaption = BuildWorksLocalization.Text("editor.view.point_hover",
            BuildWorksLocalization.Text("editor.view.point_native"));
        foreach (Vector3 point in new[] { freePoint, scalePoint, axisPoint })
        {
            // Coincident fixture forces the same conflict as a dense real model.
            Set(controller, "gizmoAnchors", new[] { point });
            Set(controller, "gizmoNativeAnchorStart", 0);
            Set(controller, "gizmoNativeAnchorEnd", 1);
            scene.ShowGizmo(BlueprintEditorTool.Transform, pivot, Quaternion.identity, false,
                GizmoHandleKind.None, GizmoAxis.None, new[] { point }, -1, 0, true,
                false, Vector3.zero, false);
            Frame(controller, input, scene.Camera.WorldToScreenPoint(point), 0,
                point == axisPoint ? new[] { KeyCode.LeftAlt } : Array.Empty<KeyCode>());
            Require((Field<TMP_Text>(view, "nextActionText").text == pointCaption) == (point == freePoint),
                "Point caption disagrees with scale/Alt-arrow click priority: point=" + point +
                "; free=" + freePoint + "; scale=" + scalePoint + "; axis=" + axisPoint +
                "; actual=" + Field<TMP_Text>(view, "nextActionText").text);
        }
        Frame(controller, input, Vector2.zero);
        Require(Field<TMP_Text>(view, "nextActionText").text != pointCaption,
            "Leaving the viewport retains a stale point caption");
    }

    private static void TestRotatedG(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorDocument doc)
    {
        BlueprintEditorPart right = doc.Parts.First(x => x.StableId == "right");
        Rotation3 originalRightRotation = right.Rotation;
        Quaternion targetRotation = Quaternion.Euler(23,41,-17);
        doc.SetPartProperties("right", right.DisplayName, right.Position,
            new Rotation3(targetRotation.x,targetRotation.y,targetRotation.z,targetRotation.w), right.Scale);
        doc.SelectOnly("left"); scene.TrySync(doc, out _); Call(controller, "Bind", new object[] { null });
        Frame(controller, input, Vector2.zero);
        int source = Field<int>(controller, "gizmoNativeAnchorStart");
        Set(controller, "selectedAnchorPoint", source);
        Set(controller, "selectedAnchorWorld", (Vector3?)Field<Vector3[]>(controller, "gizmoAnchors")[source]);
        Vector3 point = V(right.Position) + targetRotation * new Vector3(.5f,0,-.5f);
        Vector2 mouse = scene.Camera.WorldToScreenPoint(point);
        Frame(controller, input, mouse, 0, KeyCode.G);
        mouse += Vector2.right * 2;
        Frame(controller, input, mouse); Frame(controller, input, mouse); Frame(controller, input, mouse);
        Require(Field<bool>(controller, "snapTargetVisible"), "Rotated G fixture never captured target");
        Vector3 grip = Field<Vector3>(controller, "snapTargetWorld");
        for (int i = 0; i < 2; ++i)
        {
            input.Next(mouse); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); Canvas.ForceUpdateCanvases();
        }
        Quaternion rotation = Field<Quaternion>(controller, "dragRotation");
        Require(Quaternion.Angle(rotation, targetRotation * Quaternion.Euler(0,45,0)) < .02f,
            "Stationary G wheel lost target XYZ frame");
        int selected = Field<int>(controller, "keyboardAutoSource");
        Vector3 pivot = Field<Vector3>(controller, "dragPivot"), translation = Field<Vector3>(controller, "dragTranslation");
        Vector3[] anchors = Field<Vector3[]>(controller, "dragSourceAnchors");
        Require(selected >= 0 && Vector3.Distance(pivot + rotation * (anchors[selected] - pivot) + translation,
            Field<Vector3>(controller, "snapTargetWorld")) < .001f, "G wheel moved grip away from target");
        Frame(controller, input, mouse, 0, KeyCode.Escape); Frame(controller, input, mouse);
        Require(Quaternion.Angle(new Quaternion((float)doc.Parts[0].Rotation.X, (float)doc.Parts[0].Rotation.Y,
            (float)doc.Parts[0].Rotation.Z, (float)doc.Parts[0].Rotation.W), Quaternion.identity) < .01f,
            "G cancel leaked rotation into document");
        doc.SetPartProperties("right", right.DisplayName, right.Position, originalRightRotation, right.Scale);
        Require(right.Rotation.Equals(originalRightRotation), "Rotated G fixture did not restore target pose");
        Set(controller, "selectedAnchorPoint", -1); Set(controller, "selectedAnchorWorld", null);
        Call(controller, "Bind", new object[] { null }); Frame(controller, input, Vector2.zero);
    }

    internal static IEnumerable Run(Camera template, TMP_Text font, string output)
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "InputFrameSource";
        foreach (Vector3 point in new[] { new Vector3(-.5f,0,-.5f), new Vector3(.5f,0,-.5f),
            new Vector3(.5f,0,.5f), new Vector3(-.5f,0,.5f),
            new Vector3(0,-.5f,0), new Vector3(0,.5f,0) })
        {
            var snap = new GameObject("FrameNative"); snap.tag = "snappoint";
            snap.transform.SetParent(source.transform, false); snap.transform.localPosition = point;
        }
        source.SetActive(false);
        var input = new InputFrames();
        var catalog = new List<BlueprintEditorCatalogItem>
        { new BlueprintEditorCatalogItem("fixture", "Новая деталь", null, "Строительство", "Дерево", "Test", 0) };
        var compound = new CompositeBlueprintStore.Blueprint { id = "compound", name = "Двойная деталь", category = "Декор" };
        compound.groups.Add(new CompositeBlueprintStore.Group { stableId = "nested", name = "Вложенная группа" });
        for (int index = 0; index < 2; ++index)
            compound.parts.Add(new CompositeBlueprintStore.Part { stableId = "child" + index, prefabName = "fixture",
                displayName = "Составная " + index, parentGroupId = "nested",
                position = new CompositeBlueprintStore.VectorData(new Vector3(index == 0 ? -.75f : .75f, 0, 0)),
                rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity),
                scale = new CompositeBlueprintStore.VectorData(Vector3.one * (index == 0 ? .5f : 2f)) });
        catalog.Add(new BlueprintEditorCatalogItem("compound", compound.name, null, compound.category, "Чертёж", "Мои чертежи", 1, compound));
        Camera uiCamera = new GameObject("InputUiCamera", typeof(Camera)).GetComponent<Camera>();
        uiCamera.enabled = false; uiCamera.cullingMask = 1 << 5; uiCamera.clearFlags = CameraClearFlags.Depth;
        uiCamera.nearClipPlane = .1f; uiCamera.farClipPlane = 100f;
        var target = new RenderTexture(1920, 1080, 24);
        uiCamera.targetTexture = target;
        var errors = new List<string>();
        using (var controller = new BlueprintEditorController(new CompositeBlueprintStore(Path.Combine(output, "input-fixture.json")),
            template, font, _ => source, name => name, () => catalog, errors.Add,
            editorInput: input, initialPlacementSnapPointProvider: () => 0))
        try
        {
            Require(controller.OpenNew(out string error), error);
            BlueprintEditorDocument doc = controller.Document;
            doc.AddPart(new BlueprintEditorPart("left", "fixture", "Левая", new Point3(-1.5,.5,0), new Rotation3(0,0,0,1)));
            doc.AddPart(new BlueprintEditorPart("right", "fixture", "Правая", new Point3(1.5,.5,0), new Rotation3(0,0,0,1)));
            doc.AddPart(new BlueprintEditorPart("locked", "fixture", "Locked", new Point3(0,.5,3), new Rotation3(0,0,0,1), locked:true));
            doc.AddPart(new BlueprintEditorPart("hidden", "fixture", "Hidden", new Point3(0,.5,-3), new Rotation3(0,0,0,1), visible:false));
            doc.SelectOnly("left");
            var scene = Field<BlueprintEditorScene>(controller, "scene");
            var view = Field<BlueprintEditorView>(controller, "view");
            Require(scene.ModelSpaceGizmo && scene.NativePointsOnly && !scene.ExperimentalGeometryPoints,
                "New editor defaults must be model-space sizing, native snap and geometry experiment off");
            // Legacy gesture regressions have explicit screen/all settings.
            scene.ModelSpaceGizmo = false; scene.NativePointsOnly = false;
            Call(controller, "UpdateTransformContext");
            Require(Field<Action>(view, "DuplicateSelectionRequested").GetInvocationList().Length == 1 &&
                Field<Action>(view, "DeleteSelectionRequested").GetInvocationList().Length == 1 &&
                Field<Action<string>>(view, "PrimaryPartRequested").GetInvocationList().Length == 1 &&
                Field<Action<string>>(view, "PrimaryGroupRequested").GetInvocationList().Length == 1,
                "Outliner context actions are subscribed more than once");
            Require(scene.TrySync(doc, out error), error); view.Bind(doc, true, null, null);
            view.RootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            // Keep UI geometry away from near-plane precision loss in native rectangle raycasts.
            view.RootCanvas.worldCamera = uiCamera; view.RootCanvas.planeDistance = 10f;
            scene.Camera.targetTexture = target;
            Set(controller, "cameraFocus", new Vector3(0,.5f,0)); Set(controller, "cameraDistance", 8f);
            Set(controller, "cameraYaw", 20f); Set(controller, "cameraPitch", 20f);
            TestExactInspectorScale(controller, view, doc);
            yield return null;
            Frame(controller, input, Vector2.zero); yield return null;
            TestRotatedG(controller, input, scene, doc);
            NativeClick(view, uiCamera, "View");
            Frame(controller, input, Vector2.zero); yield return null;
            Require(Field<TMP_Text>(view, "statusHintText").text ==
                BuildWorksLocalization.Text("editor.hint.viewport_settings"),
                "Viewport settings retain unavailable tool shortcuts in the footer");
            Frame(controller, input, Vector2.zero, 0, KeyCode.Escape); yield return null;
            NativeClick(view, uiCamera, "OutlinerMenuButton");
            Frame(controller, input, Vector2.zero); yield return null;
            Require(Field<TMP_Text>(view, "statusHintText").text ==
                BuildWorksLocalization.Text("editor.hint.outliner_menu"),
                "Outliner drawer retains unavailable tool shortcuts in the footer");
            Frame(controller, input, Vector2.zero, 0, KeyCode.Escape); yield return null;
            int catalogRequests = 0;
            controller.CatalogRequested += () => ++catalogRequests;
            Frame(controller, input, Vector2.zero, 0, KeyCode.Tab); yield return null;
            Require(view.HasCatalog && catalogRequests == 1,
                "Tab did not open the Blueprint Editor catalog exactly once");
            view.HideCatalog();
            Frame(controller, input, Vector2.zero); yield return null;
            TestPrimaryGroupGizmo(controller, input, scene, view, doc);
            TestPointHoverHints(controller, input, scene, view, doc);
            Vector2 left = scene.Camera.WorldToScreenPoint(new Vector3(-1.5f,.5f,0));
            Vector2 right = scene.Camera.WorldToScreenPoint(new Vector3(1.5f,.5f,0));
            Require(view.ViewportScreenRect().Contains(left) && view.ViewportScreenRect().Contains(right), "Fixture outside actual viewport");

            Frame(controller, input, left, 0, KeyCode.LeftShift, KeyCode.H); yield return null;
            Require(doc.Parts[0].Visible && !doc.Parts[1].Visible && !doc.Parts[2].Visible && !doc.Parts[3].Visible,
                "Shift+H did not isolate the selected part");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;

            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.H); yield return null;
            Require(!doc.Parts[0].Visible && doc.Parts[1].Visible && doc.Parts[2].Visible && doc.Parts[3].Visible,
                "Ctrl+H did not show everything except the selected part");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;

            Frame(controller, input, left, 0, KeyCode.H); yield return null;
            Require(!doc.Parts[0].Visible && doc.Parts[1].Visible && doc.Parts[2].Visible && !doc.Parts[3].Visible,
                "H did not hide only the selected part");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftAlt, KeyCode.H); yield return null;
            Require(doc.Parts[0].Visible && doc.Parts[1].Visible && doc.Parts[2].Visible && doc.Parts[3].Visible,
                "Alt+H did not show all parts");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;
            Require(doc.Parts[0].Visible && doc.Parts[1].Visible && doc.Parts[2].Visible && !doc.Parts[3].Visible,
                "Visibility shortcut undo did not restore the fixture");

            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.A); yield return null;
            Require(doc.Selection.Count == 2 && doc.IsPartSelected("left") && doc.IsPartSelected("right") &&
                !doc.IsPartSelected("locked") && !doc.IsPartSelected("hidden"), "Ctrl+A includes hidden/locked or misses editable parts");
            Frame(controller, input, left); yield return null;
            Require(Field<GameObject>(Field<TransformGizmoView>(scene, "gizmo"), "root").activeSelf, "Select-all must immediately show handles");

            // Actual outliner row callbacks use the same Shift/Ctrl source as controller Update.
            Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
            Frame(controller, input, Vector2.zero, 0, KeyCode.LeftShift); NativeClick(view, uiCamera, "Row_right"); yield return null;
            Frame(controller, input, Vector2.zero, 0, KeyCode.LeftShift); yield return null;
            Require(doc.EditablePartSelectionCount == 2, "Shift range row callback did not select both parts");
            foreach (string id in new[] { "left", "right" }) Require(IsPainted(scene, id), "Shift-held selection is not blue: " + id);
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-shift-selection.png"));
            Frame(controller, input, right); yield return null;
            foreach (string id in new[] { "left", "right" }) Require(!IsPainted(scene, id), "Shift release must restore original selection materials: " + id);
            Require(Field<GameObject>(Field<TransformGizmoView>(scene, "gizmo"), "root").activeSelf, "Select must immediately show handles");

            Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
            NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Select]);
            yield return null;
            Frame(controller, input, right, 1, KeyCode.LeftShift); yield return null;
            Frame(controller, input, right, 0, KeyCode.LeftShift); yield return null;
            Require(doc.EditablePartSelectionCount == 2, "Shift viewport hit was swallowed by gizmo");
            Frame(controller, input, right); yield return null;

            foreach (object tick in TestTransformContract(controller, input, scene, view, uiCamera, target, output))
                yield return tick;
            foreach (object tick in TestPointFrames(controller, input, scene, view, uiCamera, target, output))
                yield return tick;
            foreach (object tick in TestViewDepth(controller, input, scene, view, uiCamera, target, output))
                yield return tick;
            foreach (object tick in TestCursorPreview(controller, input, scene, view, uiCamera, target, output))
                yield return tick;
            foreach (object tick in TestInputRevision(controller, input, scene, view, uiCamera, target, output))
                yield return tick;
            foreach (object tick in TestContinuousPointer(controller, input, scene, view, uiCamera, target, output))
                yield return tick;

            Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
            NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
            yield return null;
            Frame(controller, input, left); yield return null;
            Vector3[] anchors = Field<Vector3[]>(controller, "gizmoAnchors");
            int native = Field<int>(controller, "gizmoNativeAnchorStart");
            Set(controller, "gizmoFamily", GizmoFamily.Points);
            Frame(controller, input, left); yield return null;
            Vector2 anchorMouse = scene.Camera.WorldToScreenPoint(anchors[native]);
            Frame(controller, input, anchorMouse, 1, KeyCode.LeftShift); yield return null;
            Frame(controller, input, anchorMouse, 0, KeyCode.LeftShift); yield return null;
            Require(Field<Vector3?>(controller, "pinnedAnchorWorld").HasValue && doc.EditablePartSelectionCount == 1,
                "Shift native point should pin current selection, not toggle object");
            Frame(controller, input, anchorMouse); yield return null;

            // Alt is latched on pointer-down. Preview is separate; releasing Alt before mouse-up keeps the copy operation.
            Vector3 original = V(doc.Parts[0].Position);
            Set(controller, "gizmoFamily", GizmoFamily.Move);
            Frame(controller, input, left); yield return null;
            Vector2 moveStart = MoveHandle(scene, Field<Vector3?>(controller, "selectedAnchorWorld") ?? original, GizmoAxis.X);
            Frame(controller, input, moveStart, 1, KeyCode.LeftAlt); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Move &&
                Field<bool>(controller, "dragDuplicate"), "Alt arrow did not start copy drag: handle=" +
                Field<GizmoHandleKind>(controller, "dragHandle") + "; anchor=" + Field<int>(controller, "dragAnchorPoint") +
                "; selected=" + Field<int>(controller, "selectedAnchorPoint") + "; mouse=" + moveStart);
            Vector2 moveEnd = moveStart + Field<Vector2>(controller, "dragScreenDirection") * 80;
            Frame(controller, input, moveEnd, 1); yield return null;
            Require(doc.Parts.Count == 4 && V(doc.Parts[0].Position) == original, "Alt preview changed source document");
            Require(Vector3.Distance(VisualRoot(scene, "left").transform.position, original) < .0001f &&
                Field<IList>(scene, "duplicatePreviews").Count == 1, "Alt preview moved source visual or omitted copy");
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-alt-preview.png"));
            Frame(controller, input, moveEnd); yield return null;
            Require(doc.Parts.Count == 5 && V(doc.Parts[0].Position) == original &&
                Field<IList>(scene, "duplicatePreviews").Count == 0, "Alt release did not atomically commit only the copy");
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(doc.Parts.Count == 4, "Alt copy needs more than one Undo");
            Frame(controller, input, left); yield return null;
            moveStart = MoveHandle(scene, original, GizmoAxis.X);
            Frame(controller, input, moveStart, 1, KeyCode.LeftAlt); yield return null;
            moveEnd = moveStart + Field<Vector2>(controller, "dragScreenDirection") * 60;
            Frame(controller, input, moveEnd, 1, KeyCode.LeftAlt); yield return null;
            Frame(controller, input, moveEnd, 1, KeyCode.LeftAlt, KeyCode.Escape); yield return null;
            Frame(controller, input, moveEnd); yield return null;
            Require(doc.Parts.Count == 4 && Field<IList>(scene, "duplicatePreviews").Count == 0 &&
                V(doc.Parts[0].Position) == original, "Esc copy cancellation changed source or leaked preview");

            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.A); yield return null;
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.G); yield return null;
            Frame(controller, input, left); yield return null;
            Require(doc.Groups.Count == 1 && doc.Selection.Count == 1, "Ctrl+G did not produce selected group");
            string originalGroup = doc.Groups[0].StableId;
            foreach (object tick in TestOutlinerDragCancel(controller, input, scene, view, uiCamera, originalGroup))
                yield return tick;
            moveStart = MoveHandle(scene, new Vector3(0,.5f,0), GizmoAxis.X);
            Frame(controller, input, moveStart, 1, KeyCode.LeftAlt); yield return null;
            Require(Field<bool>(controller, "dragDuplicate"), "Group Alt move did not start: handle=" +
                Field<GizmoHandleKind>(controller, "dragHandle") + "; anchor=" + Field<int>(controller, "dragAnchorPoint") +
                "; selected=" + Field<int>(controller, "selectedAnchorPoint") + "; mouse=" + moveStart +
                "; axisHit=" + scene.HitTestGizmo(BlueprintEditorTool.Transform, new Vector3(0,.5f,0),
                    Quaternion.identity, true, moveStart, out GizmoAxis diagnosticAxis) + "/" + diagnosticAxis);
            moveEnd = moveStart + Field<Vector2>(controller, "dragScreenDirection") * 70;
            Frame(controller, input, moveEnd, 1, KeyCode.LeftAlt); yield return null;
            Require(doc.Parts.Count == 4 && doc.Groups.Count == 1 && Field<IList>(scene, "duplicatePreviews").Count == 2,
                "Group Alt preview omitted descendants or changed group document");
            Frame(controller, input, moveEnd); yield return null;
            Require(doc.Parts.Count == 6 && doc.Groups.Count == 2 && doc.Parts[0].ParentGroupId == originalGroup &&
                doc.Parts[4].ParentGroupId == doc.Parts[5].ParentGroupId && doc.Parts[4].ParentGroupId != originalGroup,
                "Group Alt commit lost hierarchy or changed original group");
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(doc.Parts.Count == 4 && doc.Groups.Count == 1, "Group copy Undo is not atomic");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;
            NativeClick(view, uiCamera, "Row_left"); yield return null;
            Frame(controller, input, left); yield return null;

            // Separate uniform scale square wins the real controller hit ordering.
            Set(controller, "gizmoFamily", GizmoFamily.Scale);
            Frame(controller, input, left); yield return null;
            var gizmo = Field<TransformGizmoView>(scene, "gizmo");
            var scaleLine = Field<LineRenderer>(gizmo, "scaleHandle");
            Vector2 scaleMouse = scene.Camera.WorldToScreenPoint((scaleLine.GetPosition(0) + scaleLine.GetPosition(2)) * .5f);
            Frame(controller, input, scaleMouse, 1); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Scale &&
                Field<int>(controller, "dragAnchorPoint") < 0, "Scale square mistaken for anchor/plane");
            Require(Math.Abs(view.ScaleStepPercent - 10f) < .0001f, "Default scale step is not 10 percentage points");
            Frame(controller, input, scaleMouse + Vector2.up * 26, 1); yield return null;
            Frame(controller, input, scaleMouse + Vector2.up * 26); yield return null;
            Require(Math.Abs(doc.Parts[0].Scale.X - 1.3) < .00001 && doc.Parts[0].Scale.X == doc.Parts[0].Scale.Y &&
                doc.Parts[0].Scale.Y == doc.Parts[0].Scale.Z, "Scale frame gesture did not quantize 126% to uniform 130%");
            scaleMouse = scene.Camera.WorldToScreenPoint((scaleLine.GetPosition(0) + scaleLine.GetPosition(2)) * .5f);
            Frame(controller, input, scaleMouse, 1); yield return null;
            Frame(controller, input, scaleMouse + Vector2.up * 10, 1); yield return null;
            Frame(controller, input, scaleMouse + Vector2.up * 10); yield return null;
            Require(Math.Abs(doc.Parts[0].Scale.X - 1.4) < .00001,
                "Single 130% + one scale step did not match inspector 140%");
            Frame(controller, input, scaleMouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, scaleMouse); yield return null;
            Require(Math.Abs(doc.Parts[0].Scale.X - 1.3) < .00001, "Second scale step did not undo to 130%");
            // Shift held after pointer-down bypasses the step, as with F9 move/rotation.
            scaleMouse = scene.Camera.WorldToScreenPoint((scaleLine.GetPosition(0) + scaleLine.GetPosition(2)) * .5f);
            Frame(controller, input, scaleMouse, 1); yield return null;
            Frame(controller, input, scaleMouse + Vector2.up * 7, 1, KeyCode.LeftShift); yield return null;
            Require(Math.Abs(Field<float>(controller, "dragScale") - (1f + .07f / 1.3f)) < .0001f,
                "Shift held during scale still quantizes the scale delta");
            Frame(controller, input, scaleMouse + Vector2.up * 7, 1, KeyCode.Escape); yield return null;
            Frame(controller, input, scaleMouse); yield return null;
            Require(Math.Abs(doc.Parts[0].Scale.X - 1.3) < .00001, "Cancelled free scale changed committed 130%");

            foreach (object tick in TestViewportToolbar(controller, input, scene, view, uiCamera))
                yield return tick;
            foreach (object tick in TestInteriorCamera(controller, input, scene, view, uiCamera, output))
                yield return tick;

            NativeClick(view, uiCamera, "View"); yield return null;
            Require(view.HasViewportSettings, "View options failed to open");
            Vector3 beforeBlocked = V(doc.Parts[0].Position);
            Frame(controller, input, left, 1); yield return null;
            Frame(controller, input, left + Vector2.right * 70, 1); yield return null;
            Frame(controller, input, left); yield return null;
            Require(V(doc.Parts[0].Position) == beforeBlocked && Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.None,
                "Viewport options allow click-through transform");
            Require(!view.HasViewportSettings, "Outside click did not dismiss viewport options");
            NativeClick(view, uiCamera, "View"); yield return null;
            NativeClick(view, uiCamera, "ViewportSettingsReset"); yield return null;
            Frame(controller, input, left, 0, KeyCode.Escape); yield return null;
            Require(!view.HasViewportSettings && controller.IsOpen, "Esc closes editor instead of viewport options");
            Frame(controller, input, left); yield return null;

            // Real catalog click enters placement; wheel rotates while camera gestures remain available.
            Frame(controller, input, left, 0, KeyCode.LeftShift, KeyCode.A); yield return null;
            Require(view.HasCatalog, "Shift+A did not open catalog");
            NativeClick(view, uiCamera, "Catalog_fixture"); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null, "Catalog card did not start placement");
            Require(Field<int>(controller, "placementManualSnapPoint") == 0,
                "Part placement did not inherit the active vanilla snap point");
            Frame(controller, input, left, 2); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") == null && view.HasCatalog && doc.Parts.Count == 4,
                "Plain RMB after placing a part did not keep the part and reopen the catalog");
            NativeClick(view, uiCamera, "Catalog_fixture"); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null,
                "Reopened catalog did not start the next part placement");
            Require(scene.ShowPlacementPreview("fixture", new Vector3(4f, -1f, 0f), Quaternion.identity,
                    false, out Vector3 freeOrigin, out string originWarning) &&
                string.IsNullOrEmpty(originWarning) && Math.Abs(freeOrigin.y + .5f) < .0001f,
                "Placement preview did not rest its real mesh on the aimed surface");
            Require(scene.ShowPlacementPreview("fixture", new Vector3(-1.5f, -.45f, 0f), Quaternion.identity,
                    true, out Vector3 belowGrid, out string snapWarning, 5) &&
                string.IsNullOrEmpty(snapWarning) && belowGrid.y < scene.GroundHeight - .1f,
                "Native upper source point cannot snap below the editor grid");
            Require(scene.PlacementSnapPreviewTargets.Count > 0 &&
                scene.PlacementSnapPreviewTargets.Count == scene.PlacementSnapPreviewNative.Count,
                "Part placement does not preview compatible targets inside two metres");
            Vector2 deleteDuringPlacement = scene.Camera.WorldToScreenPoint(V(doc.Parts[0].Position));
            Require(scene.TryPick(deleteDuringPlacement, out _),
                "Placement-delete fixture is not pickable");
            Frame(controller, input, deleteDuringPlacement, 4); yield return null;
            Frame(controller, input, deleteDuringPlacement); yield return null;
            Require(doc.Parts.Count == 3 && Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null,
                "MMB click did not delete a part while catalog placement stayed active");
            Frame(controller, input, deleteDuringPlacement, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, deleteDuringPlacement); yield return null;
            Require(doc.Parts.Count == 4 && Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null,
                "Ctrl+Z did not restore an MMB-deleted part while placement stayed active");
            Frame(controller, input, deleteDuringPlacement, 0, KeyCode.LeftControl, KeyCode.Y); yield return null;
            Frame(controller, input, deleteDuringPlacement); yield return null;
            Require(doc.Parts.Count == 3 && Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null,
                "Ctrl+Y did not repeat deletion while placement stayed active");
            Frame(controller, input, deleteDuringPlacement, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, deleteDuringPlacement); yield return null;
            Require(doc.Parts.Count == 4,
                "Second Ctrl+Z did not restore the placement-delete fixture");
            float yaw = Field<float>(controller, "cameraYaw");
            Frame(controller, input, left, 4); yield return null;
            Frame(controller, input, left + new Vector2(40,20), 4); yield return null;
            Frame(controller, input, left + new Vector2(40,20)); yield return null;
            Require(Math.Abs(Field<float>(controller, "cameraYaw") - yaw) > 1, "Placement swallowed MMB orbit");
            Vector3 focus = Field<Vector3>(controller, "cameraFocus");
            Frame(controller, input, left, 4, KeyCode.LeftShift); yield return null;
            Frame(controller, input, left + Vector2.right * 35, 4, KeyCode.LeftShift); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Vector3.Distance(Field<Vector3>(controller, "cameraFocus"), focus) > .01, "Placement swallowed Shift+MMB pan");
            focus = Field<Vector3>(controller, "cameraFocus");
            Frame(controller, input, left, 2, KeyCode.W); yield return null;
            Frame(controller, input, left, 2, KeyCode.W); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Vector3.Distance(Field<Vector3>(controller, "cameraFocus"), focus) > .01 && !view.HasCatalog &&
                Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null, "RMB+W placement navigation cancelled/opened catalog");
            input.Next(left); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Require(Math.Abs(Field<float>(controller, "placementYaw") - 22.5f) < .001,
                "Wheel did not rotate placement by the world-style step");
            int placementSnapPoints = scene.PlacementSourceSnapPointCount;
            Require(placementSnapPoints > 0, "Placement fixture has no native source snap points");
            Frame(controller, input, left, 0, KeyCode.E); yield return null;
            Require(Math.Abs(Field<float>(controller, "placementYaw") - 22.5f) < .001 &&
                Field<int>(controller, "placementManualSnapPoint") == 1 &&
                Field<TMP_Text>(view, "statusText").text.Contains(
                    BuildWorksLocalization.Text("editor.snap_selected", string.Empty)) &&
                Field<TMP_Text>(view, "statusHintText").text.Contains("2/"),
                "E did not select the next source snap point without rotating placement");
            Frame(controller, input, left); yield return null;
            Vector2 placeMouse = view.ViewportScreenRect().center + Vector2.down * 120;
            Frame(controller, input, placeMouse); yield return null;
            Vector3 placedAt = Field<Vector3>(controller, "placementPosition");
            Require(Field<object>(scene, "placementPreview") != null, "Navigation lost placement preview");
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-placement-navigation.png"));
            Frame(controller, input, placeMouse, 1); yield return null;
            Frame(controller, input, placeMouse); yield return null;
            Require(doc.Parts.Count == 5 && Vector3.Distance(V(doc.Parts[4].Position), placedAt) < .001, "Placed transform differs from preview");
            Frame(controller, input, placeMouse, 0, KeyCode.G); yield return null;
            TMP_InputField[] transformInputs = Field<TMP_InputField[]>(view, "inspectorPosition");
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") == null &&
                Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Transform &&
                transformInputs[0].gameObject.activeInHierarchy,
                "G did not leave repeat placement for numeric Transform controls");
            NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Select]); yield return null;
            Frame(controller, input, placeMouse); yield return null;
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") == null, "Select toolbar failed to leave placement");
            Require(Field<GameObject>(view, "inspectorEditPanel").activeSelf,
                "Select mode hides numeric Transform controls for its active gizmo");
            // Select a different part through the row, then hover/click the newly inserted mesh.
            NativeClick(view, uiCamera, "Row_left"); yield return null;
            Vector2 newPartMouse = scene.Camera.WorldToScreenPoint(placedAt);
            Frame(controller, input, newPartMouse); yield return null;
            string newId = doc.Parts[4].StableId;
            Require(scene.TryPick(newPartMouse, out string hit) && hit == newId && IsPainted(scene, newId),
                "Newly inserted part cannot be picked/hovered after placement");
            // Gizmo handles legitimately own their visible area. Click a rendered surface pixel outside them.
            newPartMouse = ClearSurfacePoint(controller, scene, newId, newPartMouse, V(doc.Parts[0].Position));
            Frame(controller, input, newPartMouse, 1); yield return null;
            string clickDiagnostic = "handle=" + Field<GizmoHandleKind>(controller, "dragHandle") +
                "; anchor=" + Field<int>(controller, "dragAnchorPoint") + "; selected=" + Field<int>(controller, "selectedAnchorPoint") +
                "; activeTool=" + Field<BlueprintEditorTool>(controller, "activeTool");
            Frame(controller, input, newPartMouse); yield return null;
            Require(doc.IsPartSelected(newId), "Newly inserted part click did not select it: " + clickDiagnostic + "; position=" + placedAt);
            Frame(controller, input, newPartMouse, 4); yield return null;
            Frame(controller, input, newPartMouse); yield return null;
            Require(doc.Parts.Count == 4, "MMB click did not delete the selected viewport part");
            Frame(controller, input, newPartMouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, newPartMouse); yield return null;
            Require(doc.Parts.Count == 5, "Undo did not restore the MMB-deleted viewport part");

            Frame(controller, input, newPartMouse, 0, KeyCode.LeftShift, KeyCode.A); yield return null;
            NativeClick(view, uiCamera, "CatalogBlueprintsMode"); yield return null;
            NativeClick(view, uiCamera, "Catalog_compound"); yield return null;
            input.Next(placeMouse); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Frame(controller, input, placeMouse); yield return null;
            IList compoundVisuals = Field<IList>(scene, "duplicatePreviews");
            Require(compoundVisuals.Count == 2 && doc.Parts.Count == 5, "Compound catalog did not preview both scaled parts without committing");
            var previewPositions = new List<Vector3>();
            foreach (object visual in compoundVisuals) previewPositions.Add(Field<GameObject>(visual, "Root").transform.position);
            int previewInstance = Field<GameObject>(compoundVisuals[0], "Root").GetInstanceID();
            Frame(controller, input, placeMouse); yield return null;
            Require(Field<GameObject>(Field<IList>(scene, "duplicatePreviews")[0], "Root").GetInstanceID() == previewInstance,
                "Compound preview recreated visual instances on an unchanged frame");
            Require(scene.TryPlacementSurface(placeMouse, out Vector3 compoundSurface), "Compound target surface unavailable");
            float minimumY = float.PositiveInfinity;
            foreach (object visual in compoundVisuals)
                foreach (Renderer renderer in Field<Renderer[]>(visual, "Renderers")) minimumY = Mathf.Min(minimumY, renderer.bounds.min.y);
            if (!scene.PlacementSnapTarget.HasValue)
                Require(Mathf.Abs(minimumY - compoundSurface.y) < .001, "Compound preview bottom does not touch actual surface");
            else
            {
                float snapDistance = float.PositiveInfinity;
                foreach (object visual in compoundVisuals)
                    foreach (Vector3 local in Field<List<Vector3>>(visual, "SnapLocal"))
                        snapDistance = Mathf.Min(snapDistance, Vector3.Distance(Field<GameObject>(visual, "Root").transform.TransformPoint(local),
                            scene.PlacementSnapTarget.Value));
                Require(snapDistance < .001, "Compound native target does not coincide with any displayed native point");
            }
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-compound-preview.png"));
            Frame(controller, input, placeMouse, 1); yield return null;
            Frame(controller, input, placeMouse); yield return null;
            Require(doc.Parts.Count == 7 && doc.Groups.Count == 2 && doc.Selection.Count == 1 &&
                doc.Parts[5].ParentGroupId == doc.Parts[6].ParentGroupId &&
                Vector3.Distance(V(doc.Parts[5].Position), previewPositions[0]) < .001 &&
                Vector3.Distance(V(doc.Parts[6].Position), previewPositions[1]) < .001 &&
                Math.Abs(doc.Parts[5].Scale.X - .5) < .0001 && Math.Abs(doc.Parts[6].Scale.X - 2) < .0001,
                "Compound commit loses hierarchy, scale or preview transforms");
            NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Select]); yield return null;
            Frame(controller, input, placeMouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(doc.Parts.Count == 5 && doc.Groups.Count == 0 && Field<IList>(scene, "duplicatePreviews").Count == 0,
                "Compound import does not cancel preview/undo atomically");
            Frame(controller, input, placeMouse); yield return null;
            foreach (object tick in TestArrayInput(controller, input, scene, view, uiCamera, target, output))
                yield return tick;
            NativeClick(view, uiCamera, "ViewportSectionButton0"); yield return null;
            NativeClick(view, uiCamera, "ProjectionToggle"); yield return null;
            Require(scene.Camera.orthographic, "Projection precondition before reopen");
            Call(controller, "ToggleInteriorCamera");
            Frame(controller, input, view.ViewportScreenRect().center, 2, KeyCode.W); yield return null;
            controller.Close(true);
            Require(!Field<bool>(controller, "interiorCamera") && !Field<bool>(controller, "cameraFlying") &&
                Cursor.lockState == Field<CursorLockMode>(controller, "previousCursorLock") &&
                Cursor.visible == Field<bool>(controller, "previousCursorVisible"),
                "Closing the editor left interior navigation or cursor ownership active");
            Require(controller.OpenNew(out error), "Reopen after projection: " + error);
            scene = Field<BlueprintEditorScene>(controller, "scene"); view = Field<BlueprintEditorView>(controller, "view");
            Require(scene.Camera.orthographic && Field<bool>(view, "orthographic"), "Reopen projection UI disagrees with camera");
            Require(errors.Count == 0, "Controller logged errors: " + string.Join("; ", errors));
            File.WriteAllText(Path.Combine(output, "controller-input-check.txt"),
                "PASS: actual Update/LateUpdate with deterministic key/button edges; actual EventSystem GraphicRaycaster and pointer handlers. " +
                "Ctrl+A editable; Select Shift rows/viewport/blue-release-gizmo; Transform clicks/visible box update selection without moving parts; " +
                "multi-selection pivot behind unselected mesh, R+X/Y/Z numeric rotation about fixed A; A/B frame, axis repeat/cancel, UI sizing and point filters; " +
                "native pin; Outliner native drag blocks Ctrl+A/Delete, Esc preserves selection and late drop cannot reparent; " +
                "Alt Move preview+commit+Undo+cancel; group copy hierarchy; uniform 10% scale step/Shift free/cancel; " +
                "viewport grid/projection icons via native raycast, no camera wheel/gizmo/selection interception; " +
                "viewport options/click-through guard; catalog single placement; " +
                "MMB orbit/Shift pan/RMB fly/QE; Select toolbar exits placement; new-part hover/select; " +
                "compound catalog preview/ground/scale/hierarchy/commit/Undo; projection reopen; " +
                "Array native arrow press/drag/release, wheel last direction/Ctrl zoom, Pack/Fit/Exact including 0.2m second Fit drag on 2m source, all numeric TMP parameters, symmetry, preview/apply/Undo.\n" +
                "Input provider is substituted; this does not inject physical OS keys or prove Valheim host input interception.");
        }
        finally
        {
            uiCamera.targetTexture = null; Object.Destroy(uiCamera.gameObject); Object.Destroy(target); Object.Destroy(source);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }

    internal static IEnumerable RunExchangeLifecycle(Camera template, TMP_Text font, string output)
    {
        string folder = Path.Combine(output, "exchange-input-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string sourcePath = Path.Combine(folder, "atlas.blueprint");
        const string text = "#Name:Atlas\n#Pieces\nwall;;0;0;0;0;0;0;1;\"\";1;1;1\nwall;;2;0;0;0;0;0;1;\"\";1;1;1";
        File.WriteAllText(sourcePath, text);
        var store = new CompositeBlueprintStore(Path.Combine(folder, "native-" + Guid.NewGuid().ToString("N") + ".json"));
        store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 2, y = .5f, z = 3 });
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube); source.transform.localScale = new Vector3(2, .5f, 3); source.SetActive(false);
        var input = new InputFrames();
        try
        {
            IReadOnlyList<BlueprintEditorCatalogItem> Catalogue()
            {
                var published = store.CatalogAll();
                string category = published.FirstOrDefault()?.category ?? "OTHER";
                var items = new List<BlueprintEditorCatalogItem> {
                    new BlueprintEditorCatalogItem("wall", "Wall", null, "building", "wood", "Test", 0) };
                for (int i = 0; i < 120; ++i)
                {
                    var decoy = new CompositeBlueprintStore.Blueprint { id = "decoy" + i, name = "Atlas", category = category };
                    items.Add(new BlueprintEditorCatalogItem(decoy.id, decoy.name, null, category, "wood", "Test", i + 1, decoy));
                }
                foreach (var result in published)
                    items.Add(new BlueprintEditorCatalogItem(result.id, result.name, null, result.category, "wood", "Test", items.Count, result));
                return items;
            }
            using (var controller = new BlueprintEditorController(store, template, font, _ => source, name => name,
                Catalogue,
                error => { throw new InvalidOperationException(error); }, editorInput: input))
            {
                Require(controller.OpenNew(out string error), error);
                var draft = controller.Document;
                Require(draft.AddPart(new BlueprintEditorPart("draft", "wall", "Draft wall", new Point3(8, 2, 3),
                    new Rotation3(0, 0, 0, 1))), "Dirty import draft setup failed");
                draft.SelectOnly("draft");
                int draftRevision = draft.RevisionToken, draftHistory = Field<IList>(draft, "undo").Count;
                var view = Field<BlueprintEditorView>(controller, "view");
                Call(controller, "RequestCatalog"); yield return null;
                NativeClick(view, null, "CatalogBlueprintsMode"); yield return null;
                Field<TMP_InputField>(view, "catalogSearch").text = "incompatible-search";
                NativeClick(view, null, "CatalogImport"); yield return null;
                Require(view.HasModal, "Catalog import did not open shared file browser");
                for (int i = 0; i < 300 && Field<Task<string[]>>(Field<BlueprintExchangeView>(view, "exchangeView"), "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                var directory = Field<TMP_InputField>(exchange, "directory");
                string originalDirectory = directory.text;
                var folderLabel = Field<TMP_Text>(exchange, "folderLabel");
                Require(!directory.gameObject.activeSelf && folderLabel.gameObject.activeSelf &&
                    folderLabel.text.EndsWith(Path.GetFileName(originalDirectory)), "Default folder display is not compact");
                NativeClick(view, null, "ExchangeCopyPath"); yield return null;
                Require(GUIUtility.systemCopyBuffer == originalDirectory, "Compact folder lost the full copyable path");
                NativeClick(view, null, "ExchangeEditPath"); yield return null;
                string longPath = "C:\\Users\\nfspr\\AppData\\Roaming\\Thunderstore Mod Manager\\DataFolder\\Valheim\\profiles\\TerrainRamp-1.0-Test\\BepInEx\\config\\BuildWorks\\test-fixtures";
                directory.SetTextWithoutNotify(longPath);
                directory.ActivateInputField(); yield return null;
                directory.caretPosition = longPath.Length; directory.ForceLabelUpdate();
                Canvas.ForceUpdateCanvases(); directory.textComponent.ForceMeshUpdate();
                Require(directory.lineType == TMP_InputField.LineType.MultiLineSubmit &&
                    directory.textComponent.textWrappingMode == TextWrappingModes.Normal &&
                    !directory.textComponent.parseCtrlCharacters &&
                    !directory.textComponent.textInfo.characterInfo.Take(directory.textComponent.textInfo.characterCount)
                        .Any(character => character.character == '\n' || character.character == '\r' || character.character == '\t') &&
                    directory.textComponent.textInfo.lineCount > 1 &&
                    directory.textComponent.GetPreferredValues(longPath, directory.textViewport.rect.width, 0).y <= directory.textViewport.rect.height &&
                    directory.textComponent.overflowMode == TextOverflowModes.Overflow &&
                    directory.text == longPath && !Field<Button>(exchange, "primary").interactable,
                    "Expanded exchange path clipped, lost or allowed an unconfirmed destination");
                NativeClick(view, null, "ExchangeCancelPath"); yield return null;
                Require(!directory.gameObject.activeSelf && directory.text == originalDirectory && folderLabel.gameObject.activeSelf,
                    "Cancel path edit lost the browsed folder");
                NativeClick(view, null, "ExchangeEditPath"); yield return null;
                Require(directory.gameObject.activeSelf && directory.text == originalDirectory, "Path edit cannot reopen");
                var close = ((RectTransform)Field<RectTransform>(exchange, "panel").Find("ExchangeClose"));
                Require(close.anchoredPosition.x > 600 && close.anchoredPosition.y > -50,
                    "Exchange Close is not in the upper-right header");
                directory.SetTextWithoutNotify(folder);
                NativeClick(view, null, "ExchangeGo"); yield return null;
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                string[] entries = Field<string[]>(exchange, "entries");
                Require(!directory.gameObject.activeSelf && folderLabel.text.EndsWith(Path.GetFileName(folder)),
                    "Open directory did not restore its compact label");
                int chosen = Array.IndexOf(entries, sourcePath);
                Require(chosen >= 0 && chosen < 7, "Supported source file unavailable in browser");
                NativeClick(view, null, "ExchangeEntry" + chosen); Frame(controller, input, Vector2.zero); yield return null;
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && ImportPending(exchange); ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(Field<bool>(exchange, "reviewingContents") && !Directory.Exists(store.ImportDirectory),
                    "Import wrote before contents confirmation");
                var discardedReview = Field<CompositeBlueprintStore.ImportReview>(exchange, "importReview");
                var delayedReview = new TaskCompletionSource<CompositeBlueprintStore.ImportReview>();
                Set(exchange, "importReview", null); Set(exchange, "contentsSummary", null);
                Set(exchange, "importPreparation", delayedReview.Task);
                Frame(controller, input, Vector2.zero); yield return null;
                Require(!Field<Button>(exchange, "primary").interactable && Field<Button>(exchange, "contentsBack").interactable,
                    "Pending host review allowed confirmation or blocked Back");
                Require(Field<RectTransform>(exchange, "progress").gameObject.activeSelf &&
                    Field<TMP_Text>(exchange, "message").text.Contains(BuildWorksLocalization.Text("exchange.stage_2")) &&
                    !Field<TMP_Text>(exchange, "message").isTextOverflowing,
                    "Actual pending task has no readable phase progress");
                Field<TMP_InputField>(exchange, "categoryInput").text = "discarded category";
                NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                delayedReview.SetResult(discardedReview); Frame(controller, input, Vector2.zero); yield return null;
                Require(Field<TMP_InputField>(exchange, "directory").interactable && !Directory.Exists(store.ImportDirectory),
                    "Import Back changed library or did not restore browser");
                Require(!ImportPending(exchange) && Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary") == null,
                    "Back published the abandoned host review");
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && ImportPending(exchange); ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(!Field<bool>(exchange, "categoryOverride") && Field<TMP_InputField>(exchange, "categoryInput").text == "",
                    "Back/new review retained an abandoned category");
                NativeClick(view, null, "ExchangeCategoryChoose"); yield return null;
                Require(Field<RectTransform>(exchange, "categoryPicker").gameObject.activeSelf &&
                    !Field<RectTransform>(exchange, "contentsPanel").gameObject.activeSelf, "Category picker did not own its review surface");
                Field<TMP_InputField>(exchange, "categorySearch").text = "OTHER";
                Frame(controller, input, Vector2.zero); yield return null;
                NativeClick(view, null, "ExchangeCategoryOption0"); yield return null;
                Require(Field<bool>(exchange, "categoryOverride") && Field<TMP_InputField>(exchange, "categoryInput").text == "OTHER" &&
                    !Field<RectTransform>(exchange, "categoryPicker").gameObject.activeSelf, "Existing category choice failed");
                NativeClick(view, null, "ExchangeCategoryOriginal"); yield return null;
                Require(!Field<bool>(exchange, "categoryOverride"), "Source-category reset failed");
                Field<TMP_InputField>(exchange, "categoryInput").text = "<bad>";
                Require(!Field<Button>(exchange, "primary").interactable &&
                    Field<TMP_Text>(exchange, "message").text == BuildWorksLocalization.Text("exchange.invalid_category"),
                    "Invalid typed category was not explained/refused");
                Field<TMP_InputField>(exchange, "categoryInput").text = "qa import181";
                Require(Field<Button>(exchange, "primary").interactable &&
                    Field<TMP_Text>(exchange, "contentsDetails").text.Contains("QA IMPORT181"), "Common category is not shown before confirmation");
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<bool>(exchange, "importing"); ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                for (int i = 0; i < 3; ++i) { Frame(controller, input, Vector2.zero); yield return null; }
                Require(Field<bool>(exchange, "completed") && !view.HasModal && view.HasCatalog &&
                    ReferenceEquals(controller.Document, draft) && draft.IsDirty && draft.RevisionToken == draftRevision &&
                    Field<IList>(draft, "undo").Count == draftHistory && draft.Selection.SequenceEqual(new[] { "draft" }) &&
                    draft.Parts.Single().Position.X == 8 && File.ReadAllText(sourcePath) == text,
                    "Import failed to auto-close, or modified dirty draft/original: review=" + Field<bool>(exchange, "reviewingContents") +
                    "; importing=" + Field<bool>(exchange, "importing"));
                string importedPath = Field<string>(exchange, "resultPath");
                Require(File.Exists(importedPath), "Persistent result path is not the copied file");
                var prepared = store.CatalogAll().First(item => item.importSource.StartsWith(Path.GetFileName(importedPath) + "/", StringComparison.Ordinal));
                var selectedResult = Field<BlueprintEditorCatalogItem>(view, "catalogPreviewItem");
                Require(prepared.category == "QA IMPORT181", "Typed category did not reach published card");
                Require(selectedResult.Blueprint.id == prepared.id && selectedResult.Blueprint.importSource == prepared.importSource &&
                    Field<int>(view, "catalogPage") > 0 && Field<TMP_InputField>(view, "catalogSearch").text == "" &&
                    Field<Transform>(view, "catalogGrid").Find("Catalog_" + prepared.id) &&
                    EventSystem.current.currentSelectedGameObject == Field<Transform>(view, "catalogGrid").Find("Catalog_" + prepared.id).gameObject,
                    "Imported same-name result was not selected on its actual page after clearing an obstructing search");
                Require(prepared.parts[0].baseScale.HasValue && prepared.parts[0].baseScale.Value.ToVector3() == Vector3.one &&
                    prepared.parts[0].scale.ToVector3() == Vector3.one && !prepared.parts[0].scaleIsAbsolute &&
                    prepared.parts[0].RootScale(source.transform.localScale) == Vector3.one,
                    "Host scale not pinned independently of prefab base");
                view.CancelCatalog();
                controller.Close(discardChanges: true);
                Require(controller.OpenExisting(prepared, out error), error);
                var scene = Field<BlueprintEditorScene>(controller, "scene"); view = Field<BlueprintEditorView>(controller, "view");
                var visuals = Field<IDictionary>(scene, "visuals");
                foreach (BlueprintEditorPart part in controller.Document.Parts)
                    Require(Vector3.Distance(Field<GameObject>(visuals[part.StableId], "Root").transform.localScale, Vector3.one) < .0001f,
                        "Actual editor geometry is oversized by prefab base");
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, prepared, closed)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                string name = "atlas_" + Guid.NewGuid().ToString("N");
                Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify(name);
                NativeClick(view, null, "ExchangeFormat"); yield return null;
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.ContentsSummary>>(exchange, "contentsInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(Field<bool>(exchange, "reviewingContents") && !Field<bool>(exchange, "completed") &&
                    !File.Exists(Path.Combine(store.ExportDirectory, name + ".zip")), "Export wrote before contents confirmation");
                NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                Require(Field<TMP_InputField>(exchange, "fileName").gameObject.activeInHierarchy &&
                    Field<TMP_InputField>(exchange, "directory").interactable, "Back did not restore file settings");
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.ContentsSummary>>(exchange, "contentsInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                Require(view.HasModal && Field<bool>(exchange, "completed") && Field<string>(exchange, "resultPath").EndsWith(name + ".zip"),
                    "Named ZIP export did not retain a usable result");
                string exported = Field<string>(exchange, "resultPath");
                var exportedPart = ExternalBlueprintLibrary.LoadFile(exported)[0].parts[0];
                Require(exportedPart.baseScale.HasValue && exportedPart.baseScale.Value.ToVector3() == Vector3.one &&
                    exportedPart.scale.ToVector3() == Vector3.one && !exportedPart.scaleIsAbsolute &&
                    exportedPart.RootScale(new Vector3(.5f, 2, .25f)) == Vector3.one,
                    "ZIP export changed pinned size or multiplier");
                NativeClick(view, null, "ExchangeClose"); yield return null;
                Require(!view.HasModal, "Close button cannot dismiss result");
                prepared = prepared.ImportCopy();
                prepared.libraryOrigins = new List<CompositeBlueprintStore.LibraryOrigin> { new CompositeBlueprintStore.LibraryOrigin(
                    "server.test", 71, Guid.NewGuid().ToString("N"), 3, 0, "Steam_admin", new string('A', 64), 12) };
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, prepared, closed)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                string planName = "plan_" + Guid.NewGuid().ToString("N");
                Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify(planName);
                NativeClick(view, null, "ExchangeFormat"); NativeClick(view, null, "ExchangeFormat"); yield return null;
                Require(Field<bool>(exchange, "planBuild"), "Third format is not PlanBuild");
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<PlanBuildExport>>(exchange, "planInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                string planPath = Path.Combine(store.ExportDirectory, planName);
                var planDetails = Field<TMP_Text>(exchange, "contentsDetails");
                Require(planDetails.text.Contains(BuildWorksLocalization.Text("exchange.plan_origins", 1)),
                    "PlanBuild review silently omitted library provenance");
                Require(Field<PlanBuildExport>(exchange, "planReview") != null && !Directory.Exists(planPath) &&
                    planDetails.text.Contains(planName + ".buildworks.json") && planDetails.text.Contains(planName + ".blueprint") &&
                    !Field<Button>(exchange, "storageChoice").gameObject.activeSelf &&
                    planDetails.rectTransform.rect.height > ((RectTransform)planDetails.transform.parent).rect.height,
                    "PlanBuild review wrote early, lost backup/loss paths, or cannot scroll");
                NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                Require(Field<PlanBuildExport>(exchange, "planReview") == null && !Directory.Exists(planPath) &&
                    Field<TMP_InputField>(exchange, "directory").interactable, "PlanBuild Back kept stale review or wrote files");
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<PlanBuildExport>>(exchange, "planInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<string>>(exchange, "planExport") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(!Field<bool>(exchange, "completed") && !Directory.Exists(planPath) &&
                    Field<TMP_Text>(exchange, "message").text.Contains("260"), "Long bundle path did not refuse clearly before publishing");
                NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                planName = "plan_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                planPath = Path.Combine(store.ExportDirectory, planName);
                Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify(planName);
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<PlanBuildExport>>(exchange, "planInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<string>>(exchange, "planExport") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(Field<bool>(exchange, "completed") && Field<string>(exchange, "resultPath") == planPath &&
                    File.Exists(Path.Combine(planPath, planName + ".blueprint")),
                    "PlanBuild UI failed to publish complete bundle: " + Field<TMP_Text>(exchange, "message").text);
                byte[] planBackupBytes = File.ReadAllBytes(Path.Combine(planPath, planName + ".buildworks.json"));
                var expectedPortable = prepared.ImportCopy();
                expectedPortable.id = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(planBackupBytes)).id;
                Require(planBackupBytes.SequenceEqual(BlueprintExchangeFormat.Encode(expectedPortable)),
                    "PlanBuild full backup changed original construction data");
                NativeClick(view, null, "ExchangeClose"); yield return null;
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, prepared, closed)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                var closeReviewTask = store.ReviewPlanBuildExport(prepared);
                for (int i = 0; i < 300 && !closeReviewTask.IsCompleted; ++i) yield return null;
                var closeReview = closeReviewTask.GetAwaiter().GetResult();
                var pendingExport = new TaskCompletionSource<string>();
                Set(exchange, "planReview", closeReview); Set(exchange, "planExport", pendingExport.Task); exchange.Tick();
                NativeClick(view, null, "ExchangeClose"); yield return null;
                bool closeCancelled = false;
                try { closeReview.TryWrite(prepared, _ => null, folder, "closed-export", out _, out _); }
                catch (OperationCanceledException) { closeCancelled = true; }
                pendingExport.SetCanceled();
                Require(closeCancelled && !Directory.Exists(Path.Combine(folder, "closed-export")), "Closing a busy export did not cancel publication");
                string report = "Editor: OK\nWorld: unsupported state\nPrefab scale: (0.25, 0.25, 0.25)";
                var reportStore = new CompositeBlueprintStore(Path.Combine(folder, "report-only", "blueprints.json"));
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, reportStore, prepared, closed, report: report)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                Require(Field<bool>(exchange, "completed") && Field<TMP_Text>(exchange, "message").text == report &&
                    !Directory.Exists(reportStore.ExportDirectory), "Read-only report wrote exports or lost text");
                NativeClick(view, null, "ExchangeCopyPath"); yield return null;
                Require(GUIUtility.systemCopyBuffer == report && !Field<Button>(exchange, "primary").interactable,
                    "Diagnostics cannot copy the full report or allow an unintended export");
                NativeClick(view, null, "ExchangeClose"); yield return null;
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, prepared, closed)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify(name);
                NativeClick(view, null, "ExchangeFormat"); yield return null;
                byte[] before = File.ReadAllBytes(exported);
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.ContentsSummary>>(exchange, "contentsInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                Require(!Field<bool>(exchange, "completed") && File.ReadAllBytes(exported).SequenceEqual(before) &&
                    Field<TMP_InputField>(exchange, "fileName").text == name && Field<TMP_Text>(exchange, "message").text.Length > 0,
                    "Duplicate export overwrote file or hid recoverable error");
                NativeClick(view, null, "ExchangeClose"); yield return null;
                var policy = CompositeBlueprintStore.CopyWithContentsPolicy(prepared, false, false, null);
                // New native test objects, not the original foreign wall rows.
                foreach (var part in policy.parts) { part.sourceDocumentId = null; part.sourceLine = 0; }
                policy.parts[0].prefabName = "piece_chest";
                policy.parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData {
                    format = BlueprintForeignPartData.NativeContainerFormat, info = "5:2:0:0", zdo = "bQAAAAAA" };
                policy.parts[1].prefabName = "itemstand";
                policy.parts[1].foreignData = new CompositeBlueprintStore.ForeignPartData {
                    format = BlueprintForeignPartData.NativeItemStandFormat, info = "SwordIron:0:1:0",
                    zdo = Convert.ToBase64String(ItemStandPreviewAcceptance.ItemPackage("SwordIron")) };
                byte[] placementOriginal = BlueprintExchangeFormat.Encode(policy);
                int placementFiles = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length;
                int placements = 0;
                foreach (bool excludeStorage in new[] { false, true })
                foreach (bool excludeMounts in new[] { false, true })
                {
                    CompositeBlueprintStore.Blueprint placementRecipe = null;
                    view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, policy, closed,
                        place: recipe => { placementRecipe = recipe; ++placements; return null; }, placementCurrent: () => true));
                    yield return null;
                    exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                    for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.ContentsSummary>>(exchange, "contentsInspection") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    Require(Field<Button>(exchange, "storageChoice").interactable && Field<Button>(exchange, "mountChoice").interactable &&
                        !Field<TMP_InputField>(exchange, "directory").gameObject.activeSelf &&
                        !Field<Button>(exchange, "copyPath").gameObject.activeSelf,
                        "Placement review missed independent choices or exposed file workflow");
                    if (excludeStorage) { NativeClick(view, null, "ExchangeStorageChoice"); yield return null; }
                    if (excludeMounts) { NativeClick(view, null, "ExchangeMountChoice"); yield return null; }
                    NativeClick(view, null, "ExchangeSubmit");
                    for (int i = 0; i < 300 && exchange.IsOpen; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    Require(placementRecipe != null && !exchange.IsOpen &&
                        placementRecipe.parts[0].foreignData.contentsCleared == excludeStorage &&
                        placementRecipe.parts[1].foreignData.contentsCleared == excludeMounts &&
                        BlueprintExchangeFormat.Encode(policy).SequenceEqual(placementOriginal) &&
                        Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length == placementFiles,
                        "Placement choices coupled, mutated source or wrote files");
                }
                bool placementCurrent = true;
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, policy, closed,
                    place: recipe => { ++placements; return "$buildworks_import.object_data"; }, placementCurrent: () => placementCurrent));
                yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.ContentsSummary>>(exchange, "contentsInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                NativeClick(view, null, "ExchangeSubmit");
                for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.Blueprint>>(exchange, "placementPreparation") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(exchange.IsOpen && placements == 5 && !Field<bool>(exchange, "clearStorage") && !Field<bool>(exchange, "clearMounts") &&
                    Field<Button>(exchange, "primary").interactable && Field<TMP_Text>(exchange, "message").text.Length > 0,
                    "Unsupported placement closed review, silently cleared contents or prevented retry");
                placementCurrent = false; exchange.Tick();
                Require(!Field<Button>(exchange, "primary").interactable && Field<Button>(exchange, "contentsBack").interactable,
                    "Stale placement was still confirmable or could not be cancelled");
                NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                var pendingPlacement = new TaskCompletionSource<CompositeBlueprintStore.Blueprint>();
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, policy, closed,
                    place: recipe => { ++placements; return null; }, placementCurrent: () => true));
                yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                Set(exchange, "placementPreparation", pendingPlacement.Task); exchange.Tick();
                NativeClick(view, null, "ExchangeClose"); yield return null;
                pendingPlacement.SetResult(policy.ImportCopy()); exchange.Tick();
                Require(placements == 5 && !exchange.IsOpen && BlueprintExchangeFormat.Encode(policy).SequenceEqual(placementOriginal) &&
                    Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length == placementFiles,
                    "Closing pending placement later committed a callback or modified files/source");
                foreach (bool emptyStorage in new[] { false, true })
                foreach (bool emptyMounts in new[] { false, true })
                {
                    view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, policy, closed)); yield return null;
                    exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                    for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify("policy_" + Guid.NewGuid().ToString("N"));
                    NativeClick(view, null, "ExchangeSubmit"); yield return null;
                    for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.ContentsSummary>>(exchange, "contentsInspection") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    var summary = Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary");
                    Require(summary != null && summary.CanClearStorage && summary.CanClearMounts && summary.Storages == 1 && summary.Mounts == 1,
                        "UI contents counts missing: " + Field<TMP_Text>(exchange, "message").text);
                    if (emptyStorage) NativeClick(view, null, "ExchangeStorageChoice");
                    if (emptyMounts) NativeClick(view, null, "ExchangeMountChoice");
                    NativeClick(view, null, "ExchangeSubmit"); yield return null;
                    Require(Field<bool>(exchange, "completed"),
                        "Contents export did not complete: " + Field<TMP_Text>(exchange, "message").text);
                    var copy = ExternalBlueprintLibrary.LoadFile(Field<string>(exchange, "resultPath")).Single();
                    Require(copy.parts[0].foreignData.contentsCleared == emptyStorage && copy.parts[1].foreignData.contentsCleared == emptyMounts &&
                        !policy.parts[0].foreignData.contentsCleared && !policy.parts[1].foreignData.contentsCleared,
                        "Actual UI coupled storage/mount choices or mutated source");
                    NativeClick(view, null, "ExchangeClose"); yield return null;
                    string importFolder = Path.Combine(folder, "contents-" + emptyStorage + "-" + emptyMounts);
                    Directory.CreateDirectory(importFolder);
                    File.WriteAllBytes(Path.Combine(importFolder, "contents.buildworks.json"), BlueprintExchangeFormat.Encode(policy));
                    view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, null, closed)); yield return null;
                    exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                    for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    Field<TMP_InputField>(exchange, "directory").SetTextWithoutNotify(importFolder);
                    NativeClick(view, null, "ExchangeGo");
                    for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    NativeClick(view, null, "ExchangeEntry0"); Frame(controller, input, Vector2.zero);
                    NativeClick(view, null, "ExchangeSubmit");
                    for (int i = 0; i < 300 && ImportPending(exchange); ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    Require(Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary").Storages == 1 &&
                        Field<Button>(exchange, "storageChoice").interactable && Field<Button>(exchange, "mountChoice").interactable,
                        "Import review missed native contents");
                    if (emptyStorage) NativeClick(view, null, "ExchangeStorageChoice");
                    if (emptyMounts) NativeClick(view, null, "ExchangeMountChoice");
                    NativeClick(view, null, "ExchangeSubmit");
                    for (int i = 0; i < 300 && Field<bool>(exchange, "importing"); ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    Require(Field<bool>(exchange, "completed"), "Confirmed contents import failed: " + Field<TMP_Text>(exchange, "message").text);
                    copy = ExternalBlueprintLibrary.LoadFile(Field<string>(exchange, "resultPath")).Single();
                    Require(copy.parts[0].foreignData.contentsCleared == emptyStorage && copy.parts[1].foreignData.contentsCleared == emptyMounts,
                        "Actual import UI coupled choices");
                    NativeClick(view, null, "ExchangeClose"); yield return null;
                }
                string goodMountPackage = policy.parts[1].foreignData.zdo;
                policy.parts[1].foreignData.zdo = "bQIA";
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, policy, closed)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify("storage_only_" + Guid.NewGuid().ToString("N"));
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.ContentsSummary>>(exchange, "contentsInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(Field<Button>(exchange, "storageChoice").interactable && !Field<Button>(exchange, "mountChoice").interactable &&
                    Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary").UnreadableMounts == 1,
                    "Unreadable mount blocked an independent valid storage choice");
                NativeClick(view, null, "ExchangeStorageChoice"); NativeClick(view, null, "ExchangeSubmit"); yield return null;
                Require(Field<bool>(exchange, "completed"), "Independent storage-only export failed");
                var storageOnly = ExternalBlueprintLibrary.LoadFile(Field<string>(exchange, "resultPath")).Single();
                Require(storageOnly.parts[0].foreignData.contentsCleared && !storageOnly.parts[1].foreignData.contentsCleared &&
                    storageOnly.parts[1].foreignData.zdo == "bQIA" && !policy.parts[0].foreignData.contentsCleared,
                    "Storage-only export changed unreadable mount evidence or source");
                NativeClick(view, null, "ExchangeClose"); yield return null;
                policy.parts[1].foreignData.zdo = goodMountPackage;
                string lockedPath = Path.Combine(folder, "locked.blueprint"); File.WriteAllText(lockedPath, text);
                policy.parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData { format = "vbuild", zdo = "opaque" };
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, policy, closed)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                string cancelledName = "cancel_" + Guid.NewGuid().ToString("N");
                Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify(cancelledName);
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.ContentsSummary>>(exchange, "contentsInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary").Unknown == 1 &&
                    !Field<Button>(exchange, "storageChoice").interactable && !Field<Button>(exchange, "mountChoice").interactable &&
                    Field<Button>(exchange, "primary").interactable, "Unknown state did not block only destructive choices");
                NativeClick(view, null, "ExchangeClose"); yield return null;
                Require(!File.Exists(Path.Combine(store.ExportDirectory, cancelledName + ".buildworks.json")) &&
                    policy.parts[0].foreignData.zdo == "opaque", "Cancel changed source or wrote an export");
                string unreadablePackage = Convert.ToBase64String(new byte[] { 109, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 64, 1, 0, 0, 0, 0 });
                policy.parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData {
                    format = BlueprintForeignPartData.NativeContainerFormat, info = "5:2:1:1|Wood", zdo = unreadablePackage };
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, policy, closed)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Field<TMP_InputField>(exchange, "fileName").SetTextWithoutNotify("mount_only_" + Guid.NewGuid().ToString("N"));
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                for (int i = 0; i < 300 && Field<Task<CompositeBlueprintStore.ContentsSummary>>(exchange, "contentsInspection") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(!Field<Button>(exchange, "storageChoice").interactable && Field<Button>(exchange, "mountChoice").interactable,
                    "Unreadable storage blocked an independent valid mount choice");
                NativeClick(view, null, "ExchangeMountChoice");
                NativeClick(view, null, "ExchangeSubmit"); yield return null;
                Require(Field<bool>(exchange, "completed"), "Independent mount-only export failed");
                var mountOnly = ExternalBlueprintLibrary.LoadFile(Field<string>(exchange, "resultPath")).Single();
                Require(!mountOnly.parts[0].foreignData.contentsCleared && mountOnly.parts[0].foreignData.zdo == unreadablePackage &&
                    mountOnly.parts[1].foreignData.contentsCleared && !policy.parts[1].foreignData.contentsCleared,
                    "Mount-only export changed unreadable storage or source");
                NativeClick(view, null, "ExchangeClose"); yield return null;
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, null, closed)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Field<TMP_InputField>(exchange, "directory").SetTextWithoutNotify(folder);
                NativeClick(view, null, "ExchangeGo");
                Require(Field<string[]>(exchange, "entries").Length == 0 && Field<string>(exchange, "selected") == null &&
                    !Field<RectTransform>(exchange, "rows").GetComponentsInChildren<Button>().Any(),
                    "Loading folder leaves stale selectable files");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                chosen = Array.IndexOf(Field<string[]>(exchange, "entries"), lockedPath);
                Require(chosen >= 0, "Locked-file fixture missing");
                for (int i = 0; i < chosen / 7; ++i) { NativeClick(view, null, "ExchangeNext"); yield return null; }
                NativeClick(view, null, "ExchangeEntry" + chosen); Frame(controller, input, Vector2.zero); yield return null;
                using (var fileLock = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    NativeClick(view, null, "ExchangeSubmit");
                    for (int i = 0; i < 300 && ImportPending(exchange); ++i)
                    { Frame(controller, input, Vector2.zero); yield return null; }
                    Require(Field<TMP_Text>(exchange, "message").text.Length > 0 && !Field<bool>(exchange, "completed") &&
                        !Field<Button>(exchange, "primary").interactable && Field<Button>(exchange, "contentsBack").interactable,
                        "Inspection error enabled confirmation or blocked Back");
                }
                NativeClick(view, null, "ExchangeContentsBack"); yield return null;
                NativeClick(view, null, "ExchangeEntry" + chosen); Frame(controller, input, Vector2.zero); yield return null;
                NativeClick(view, null, "ExchangeSubmit");
                for (int i = 0; i < 300 && ImportPending(exchange); ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                NativeClick(view, null, "ExchangeSubmit");
                for (int i = 0; i < 300 && Field<bool>(exchange, "importing"); ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(Field<bool>(exchange, "completed"), "Recovered browser cannot retry import");
                NativeClick(view, null, "ExchangeClose"); yield return null;
                string armorPath = Path.Combine(folder, "armor-policy.blueprint");
                const string armorText = "#Name:Armor policy\n#Pieces\nwall;;0;0;0;0;0;0;1;\"\";1;1;1\nArmorStand;;2;0;0;0;0;0;1;\"2:1:HelmetIron:3:\";1;1;1";
                File.WriteAllText(armorPath, armorText);
                int importsBeforeArmor = Directory.GetFiles(store.ImportDirectory).Length;
                view.ShowExchange((parent, textFont, closed) => new BlueprintExchangeView(parent, textFont, store, null, closed)); yield return null;
                exchange = Field<BlueprintExchangeView>(view, "exchangeView");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Field<TMP_InputField>(exchange, "directory").SetTextWithoutNotify(folder);
                NativeClick(view, null, "ExchangeGo");
                for (int i = 0; i < 300 && Field<Task<string[]>>(exchange, "listing") != null; ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                chosen = Array.IndexOf(Field<string[]>(exchange, "entries"), armorPath);
                Require(chosen >= 0, "Armor policy fixture missing");
                for (int i = 0; i < chosen / 7; ++i) { NativeClick(view, null, "ExchangeNext"); yield return null; }
                NativeClick(view, null, "ExchangeEntry" + chosen); Frame(controller, input, Vector2.zero); yield return null;
                NativeClick(view, null, "ExchangeSubmit");
                for (int i = 0; i < 300 && ImportPending(exchange); ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(Field<CompositeBlueprintStore.ContentsSummary>(exchange, "contentsSummary").ArmorStates == 1 &&
                    Field<Button>(exchange, "armorChoice").gameObject.activeSelf && !Field<bool>(exchange, "omitArmorState") &&
                    Field<TMP_Text>(exchange, "contentsDetails").text.Contains("armor-policy.blueprint") &&
                    Field<TMP_Text>(exchange, "contentsDetails").text.Contains("ArmorStand"), "Armor default or loss list missing");
                NativeClick(view, null, "ExchangeArmorChoice");
                Require(Field<bool>(exchange, "omitArmorState") && Directory.GetFiles(store.ImportDirectory).Length == importsBeforeArmor,
                    "Armor review wrote before confirmation");
                NativeClick(view, null, "ExchangeContentsBack"); Frame(controller, input, Vector2.zero); yield return null;
                NativeClick(view, null, "ExchangeSubmit");
                for (int i = 0; i < 300 && ImportPending(exchange); ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(!Field<bool>(exchange, "omitArmorState"), "Back retained a stale destructive choice");
                NativeClick(view, null, "ExchangeArmorChoice"); NativeClick(view, null, "ExchangeSubmit");
                for (int i = 0; i < 300 && Field<bool>(exchange, "importing"); ++i)
                { Frame(controller, input, Vector2.zero); yield return null; }
                Require(Field<bool>(exchange, "completed") && File.ReadAllText(armorPath) == armorText, "Armor reviewed import failed or changed source");
                var armorCopy = ExternalBlueprintLibrary.LoadFile(Field<string>(exchange, "resultPath")).Single();
                Require(armorCopy.parts[1].foreignData == null && armorCopy.sourceDocuments[0].text == armorText,
                    "Armor UI choice did not reach the copy or lost exact evidence");
                File.WriteAllText(Path.Combine(output, "exchange-input-check.txt"),
                    "PASS actual catalogue/browser clicks, original-safe reviewed import, persistent path and Escape, nonunit prefab visual scale, named native ZIP, duplicate export rejection and retained inputs, import/export four independent contents choices, back/confirmation/unknown-state guard/cancel, no stale files while loading, inspection error recovery and retry; ArmorStand loss/source/count review defaults to keep, no pre-confirm writes, Back resets omission, confirmed copy omits only state and retains source. No Valheim host proof.");
            }
        }
        finally { Object.Destroy(source); }
    }

    internal static IEnumerable RunSupportLifecycle(Camera template, TMP_Text font, string output)
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.SetActive(false);
        var input = new InputFrames();
        var errors = new List<string>();
        using (var controller = new BlueprintEditorController(new CompositeBlueprintStore(
            Path.Combine(output, "support-input-fixture.json")), template, font, _ => source,
            name => name, () => Array.Empty<BlueprintEditorCatalogItem>(), errors.Add, editorInput: input))
        try
        {
            Require(controller.OpenNew(out string error), error);
            BlueprintEditorDocument doc = controller.Document;
            doc.AddPart(new BlueprintEditorPart("left", "fixture", "Left", new Point3(-1, 0, 0), new Rotation3(0, 0, 0, 1)));
            doc.AddPart(new BlueprintEditorPart("right", "fixture", "Right", new Point3(1, 0, 0), new Rotation3(0, 0, 0, 1)));
            doc.SelectOnly("left"); doc.ToggleSelection("right");
            Call(controller, "RefreshSelection");
            var view = Field<BlueprintEditorView>(controller, "view");
            var scene = Field<BlueprintEditorScene>(controller, "scene");
            yield return null;
            Set(controller, "selectedAnchorWorld", new Vector3(0, 1, 0));
            Require(!view.RootCanvas.GetComponentsInChildren<Transform>(true).Any(t =>
                t.name == "CreateSupportToolbar" || t.name == "CreateSupport" || t.name == "ClearSupportCentre" || t.name == "DeleteSupport"),
                "Support actions must live in Tools, not the object tree/inspector");
            view.ShowToolsMenu(view.ViewportScreenRect().center, false);
            yield return null;
            NativeClick(view, null, "ToolsEntry4"); yield return null;
            string id = doc.ActiveSupportId;
            Require(id != null && doc.Selection.Count == 2 && doc.PlacementSupportId == null,
                "Creating/selecting support must retain parts without assigning placement");
            var gizmo = Field<TransformGizmoView>(scene, "gizmo");
            Require(!gizmo.ScaleEnabled, "Support exposes scale handles");
            Vector3 original = V(doc.FindSupport(id).Position);
            Require(Vector3.Distance(original, new Vector3(0, 1, 0)) < .001f,
                "Create lost explicit A while ApplyDocumentEdit cleared transient anchors");
            Require(scene.ModelSpaceGizmo && gizmo.ModelSize >= .4f,
                "Default model-space support gizmo collapses without part bounds");
            Frame(controller, input, Vector2.zero, 0, KeyCode.F); Frame(controller, input, Vector2.zero);
            Require(Vector3.Distance(Field<Vector3>(controller, "cameraFocus"), original) < .001f,
                "F frames retained parts instead of selected support");
            Vector3[] parts = doc.Parts.Select(p => V(p.Position)).ToArray();
            var visuals = Field<IDictionary>(scene, "visuals");
            Vector3[] visualPositions = doc.Parts.Select(p => Field<GameObject>(visuals[p.StableId], "Root").transform.position).ToArray();
            Vector2 mouse = scene.Camera.WorldToScreenPoint(original);
            Vector2 arrow = scene.Camera.WorldToScreenPoint(original + Vector3.right * gizmo.Scale * .7f);
            Frame(controller, input, arrow, 1);
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Move,
                "Support move arrow does not start native pointer drag");
            Frame(controller, input, arrow + Vector2.right * 60, 1);
            Vector3 pointerDelta = Field<Vector3>(controller, "dragTranslation");
            Frame(controller, input, arrow + Vector2.right * 60); yield return null;
            Require(pointerDelta.magnitude > .01f &&
                Vector3.Distance(V(doc.FindSupport(id).Position), original + pointerDelta) < .001f,
                "Support pointer release did not commit displayed pose");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); Frame(controller, input, mouse);
            Frame(controller, input, mouse, 0, KeyCode.G); Frame(controller, input, mouse);
            Frame(controller, input, mouse, 0, KeyCode.X); Frame(controller, input, mouse + Vector2.right * 50);
            Frame(controller, input, mouse, 0, KeyCode.Escape); Frame(controller, input, mouse);
            Require(!Field<bool>(controller, "keyboardPreview") &&
                Vector3.Distance(V(doc.FindSupport(id).Position), original) < .001f, "Support Esc committed the preview");
            Frame(controller, input, mouse, 0, KeyCode.G); Frame(controller, input, mouse);
            Frame(controller, input, mouse, 0, KeyCode.X); Frame(controller, input, mouse + Vector2.right * 60);
            input.Next(mouse); input.InputString = "3"; controller.Update(); controller.LateUpdate(); yield return null;
            Require(Vector3.Distance(Field<Vector3>(controller, "dragTranslation"), Vector3.right * 3) < .001f,
                "Support G number must replace mouse displacement from operation start");
            for (int i = 0; i < parts.Length; ++i)
                Require(Vector3.Distance(parts[i], V(doc.Parts[i].Position)) < .001f &&
                    Vector3.Distance(visualPositions[i], Field<GameObject>(visuals[doc.Parts[i].StableId], "Root").transform.position) < .001f,
                    "Support preview moved retained part geometry");
            Frame(controller, input, mouse, 0, KeyCode.Return); Frame(controller, input, mouse); yield return null;
            Require(Vector3.Distance(V(doc.FindSupport(id).Position), original + Vector3.right * 3) < .001f,
                "Support G commit did not move only the support");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); Frame(controller, input, mouse);
            Require(Vector3.Distance(V(doc.FindSupport(id).Position), original) < .001f, "Support move needs more than one Undo");
            Frame(controller, input, mouse, 0, KeyCode.R); Frame(controller, input, mouse);
            Frame(controller, input, mouse, 0, KeyCode.Y); Frame(controller, input, mouse + Vector2.up * 40);
            input.Next(mouse); input.InputString = "90"; controller.Update(); controller.LateUpdate();
            Quaternion preview = Field<Quaternion>(controller, "dragRotation");
            Frame(controller, input, mouse, 0, KeyCode.S); Frame(controller, input, mouse); yield return null;
            Require(Field<bool>(controller, "keyboardPreview") && Quaternion.Angle(preview,
                Field<Quaternion>(controller, "dragRotation")) < .01f, "S interrupted support R preview");
            Frame(controller, input, mouse, 0, KeyCode.Return); Frame(controller, input, mouse);
            Require(Quaternion.Angle(new Quaternion((float)doc.FindSupport(id).Rotation.X,
                (float)doc.FindSupport(id).Rotation.Y, (float)doc.FindSupport(id).Rotation.Z,
                (float)doc.FindSupport(id).Rotation.W), Quaternion.Euler(0, 90, 0)) < .01f,
                "Support R numeric angle is wrong");
            NativeClick(view, null, "SupportPlacement"); yield return null;
            Require(doc.PlacementSupportId == id && doc.ActiveSupportId == id,
                "Placement assignment changed edit scope");
            Field<TMP_InputField[]>(view, "supportPosition")[0].SetTextWithoutNotify("513");
            NativeClick(view, null, "ApplySupport"); yield return null;
            Require(Vector3.Distance(V(doc.FindSupport(id).Position), original) < .001f,
                "Out-of-range inspector support position committed");
            Field<TMP_InputField[]>(view, "supportPosition")[0].SetTextWithoutNotify(original.x.ToString());
            Field<TMP_InputField>(view, "supportName").SetTextWithoutNotify("Вход · свободная опора");
            Field<TMP_InputField[]>(view, "supportPosition")[1].SetTextWithoutNotify("2");
            NativeClick(view, null, "ApplySupport"); yield return null;
            Require(doc.FindSupport(id).Name == "Вход · свободная опора" && Mathf.Abs(V(doc.FindSupport(id).Position).y - 2) < .001f,
                "Support inspector name/pose apply disconnected");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); Frame(controller, input, mouse);
            Require(doc.FindSupport(id).Name != "Вход · свободная опора" &&
                Vector3.Distance(V(doc.FindSupport(id).Position), original) < .001f,
                "Support name and pose require two Undos");
            NativeClick(view, null, "SupportCentre"); Frame(controller, input, mouse); yield return null;
            Require(doc.ActiveSupportId == null && doc.PlacementSupportId == id && gizmo.ScaleEnabled &&
                Field<string>(controller, "transformSupportPivotId") == id, "Centre assignment did not switch back to part scope");
            Vector3 before = V(doc.FindSupport(id).Position);
            Require(controller.ApplyTransformDelta(Vector3.zero, Quaternion.Euler(0, 90, 0), before),
                "Part transform from support centre failed");
            Require(Vector3.Distance(V(doc.FindSupport(id).Position), before) < .001f &&
                Vector3.Distance(V(doc.Parts[0].Position), parts[0]) > .1f, "Centre transform moved support or failed to move parts");
            yield return null;
            view.ShowToolsMenu(view.ViewportScreenRect().center, false);
            yield return null;
            NativeClick(view, null, "ToolsEntry6"); yield return null;
            Require(Field<string>(controller, "transformSupportPivotId") == null, "Normal centre reset is disconnected");
            NativeClick(view, null, "Support_" + id); Frame(controller, input, mouse); yield return null;
            view.ShowToolsMenu(view.ViewportScreenRect().center, false);
            yield return null;
            NativeClick(view, null, "ToolsEntry5"); yield return null;
            Require(doc.Supports.Count == 0 && doc.Parts.Count == 2 && doc.PlacementSupportId == null,
                "Support Delete removed retained parts or left placement ID");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); Frame(controller, input, mouse);
            Require(doc.FindSupport(id) != null && doc.PlacementSupportId == id, "Undo support deletion loses placement role");
            controller.Close(true); Require(controller.OpenNew(out error), error);
            Require(controller.Document.ActiveSupportId == null && Field<string>(controller, "transformSupportPivotId") == null,
                "Reopen leaked support focus/centre");
            Require(errors.Count == 0, "Support controller errors: " + string.Join("; ", errors));
            File.WriteAllText(Path.Combine(output, "support-input-check.txt"),
                "PASS: native support tree/inspector clicks, retained parts, G/R numeric replacement, S non-interruption, atomic Apply/Undo, placement vs centre, Delete/Undo and reopen isolation. No Valheim host test.");
        }
        finally { Object.Destroy(source); }
    }

    internal static IEnumerable RunContourLifecycle(Camera template, TMP_Text font, string output)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.localScale = new Vector3(2,2,.2f);
        foreach (Vector3 point in new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
            new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0) })
        {
            var snap = new GameObject("NativeSnap"); snap.tag = "snappoint";
            snap.transform.SetParent(wall.transform, false); snap.transform.localPosition = point;
        }
        wall.SetActive(false);
        var input = new InputFrames();
        using (var controller = new BlueprintEditorController(
            new CompositeBlueprintStore(Path.Combine(output, "contour-input-fixture.json")),
            template, font, _ => wall, name => name, null, error => { throw new Exception(error); },
            editorInput: input))
        try
        {
            Require(controller.OpenNew(out string error), error);
            BlueprintEditorDocument doc = controller.Document;
            doc.AddPart(new BlueprintEditorPart("source", "pole", "Source", new Point3(-3,1,0), new Rotation3(0,0,0,1)));
            for (int index = 0; index < 3; ++index)
                doc.AddPart(new BlueprintEditorPart("wall" + index, "wall", "Wall",
                    new Point3(index * 2,1,0), new Rotation3(0,0,0,1)));
            doc.SelectOnly("source");
            var scene = Field<BlueprintEditorScene>(controller, "scene");
            var view = Field<BlueprintEditorView>(controller, "view");
            Require(scene.TrySync(doc, out error), error); view.Bind(doc, true, null, null);
            Set(controller, "cameraFocus", new Vector3(2,1,0)); Set(controller, "cameraDistance", 10f);
            Set(controller, "cameraYaw", 0f); Set(controller, "cameraPitch", 20f);
            yield return null;
            Frame(controller, input, Vector2.zero); yield return null;
            LineRenderer guide = Field<LineRenderer>(Field<TransformGizmoView>(scene, "gizmo"), "contactGuide");
            Require(!scene.OccluderFadeEnabled, "See-through must start disabled");
            Frame(controller, input, Vector2.zero, 0, KeyCode.F7); yield return null;
            Require(scene.OccluderFadeEnabled, "F7 did not enable editor see-through");
            NativeClick(view, null, "ViewportSectionButton1"); yield return null;
            NativeClick(view, null, "OccluderFadeToggle"); yield return null;
            view.HideViewportSettings();
            Require(!scene.OccluderFadeEnabled, "Viewport button did not disable see-through");
            Vector2 edge = scene.Camera.WorldToScreenPoint(new Vector3(0,1.96f,-.1f));
            foreach (KeyCode finish in new[] { KeyCode.None, KeyCode.Escape, KeyCode.Alpha2, KeyCode.Alpha1, KeyCode.Delete })
            {
                Frame(controller, input, Vector2.zero, 0, KeyCode.C); yield return null;
                Frame(controller, input, edge); yield return null;
                Require(guide.gameObject.activeInHierarchy && Field<int>(controller, "contourPreviewCount") == 0 &&
                    Field<List<string>>(controller, "contourSupportIds").Count == 0 && doc.Parts.Count == 4,
                    "Contour hover must show only the guide without locking supports, copying or editing");
                var guideBefore = new Vector3[guide.positionCount]; guide.GetPositions(guideBefore);
                Vector2 sameEdge = scene.Camera.WorldToScreenPoint(new Vector3(.4f,1.96f,-.1f));
                Frame(controller, input, sameEdge); yield return null;
                Require(guide.positionCount == guideBefore.Length, "Moving along an edge changed guide size");
                for (int point = 0; point < guideBefore.Length; ++point)
                    Require((guide.GetPosition(point) - guideBefore[point]).sqrMagnitude < .000001f,
                        "Contour guide follows cursor instead of actual edge endpoints");
                Frame(controller, input, Vector2.zero); yield return null;
                Require(!guide.gameObject.activeSelf, "Contour guide remains after leaving the edge");
                Frame(controller, input, edge, 1); yield return null;
                Frame(controller, input, edge); yield return null;
                Require(Field<int>(controller, "contourPreviewCount") > 0 && guide.gameObject.activeInHierarchy,
                    "Click did not commit hovered chain to the copy preview");
                if (finish == KeyCode.None) NativeClick(view, null, "CancelContour");
                else if (finish == KeyCode.Alpha1 || finish == KeyCode.Alpha2)
                    Frame(controller, input, Vector2.zero, 0, KeyCode.LeftControl, finish);
                else Frame(controller, input, Vector2.zero, 0, finish);
                yield return null;
                Frame(controller, input, Vector2.zero); yield return null;
                Require(!guide.gameObject.activeSelf && Field<IList>(scene, "contourPreviews").Count == 0,
                    "Contour guide or ghosts survive cancel/tool change/delete: " + finish);
                if (finish == KeyCode.Delete) break;
                NativeClick(view, null, "Row_wall1"); yield return null;
                Frame(controller, input, Vector2.zero); yield return null;
                Require(!guide.gameObject.activeSelf, "Selecting another part resurrects the old contour guide");
                NativeClick(view, null, "Row_source"); yield return null;
            }
            File.WriteAllText(Path.Combine(output, "contour-input-check.txt"),
                "PASS actual Update/LateUpdate: hover guide only; leave clears; click previews; Cancel/Esc/G/Q/Delete clear guides and ghosts; new selection cannot resurrect guide; F7/button toggle see-through.");
        }
        finally { controller.Close(true); Object.Destroy(wall); }
    }

    private static IEnumerable TestOutlinerDragCancel(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, string originalGroup)
    {
        BlueprintEditorDocument doc = controller.Document;
        NativeClick(view, uiCamera, "Row_left"); yield return null;
        Require(doc.Selection.Count == 1 && doc.IsPartSelected("left") && doc.Parts[0].ParentGroupId == originalGroup,
            "Tree drag cancellation precondition: selected nested child missing");
        RectTransform rows = Field<RectTransform>(view, "outlinerRows");
        Button row = rows.Find("Row_left").GetComponent<Button>();
        var rowRect = (RectTransform)row.transform;
        Vector2 mouse = RectTransformUtility.WorldToScreenPoint(uiCamera, rowRect.TransformPoint(rowRect.rect.center));
        Frame(controller, input, mouse, 1); yield return null;
        GameObject dragHost = ExecuteEvents.GetEventHandler<IDragHandler>(row.gameObject);
        Require(dragHost == rows.gameObject, "Tree drag host is not the persistent Outliner rows object");
        var pointer = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left, eligibleForClick = true,
            pointerPress = row.gameObject, pointerClick = row.gameObject, pointerDrag = dragHost,
            pointerPressRaycast = new RaycastResult { gameObject = row.gameObject },
            pressPosition = mouse, position = mouse + Vector2.right * 20f, delta = Vector2.right * 20f
        };
        var module = EventSystem.current.GetComponent<StandaloneInputModule>();
        typeof(PointerInputModule).GetMethod("ProcessDrag", Private).Invoke(module, new object[] { pointer });
        Require(pointer.dragging && !pointer.eligibleForClick && view.IsOutlinerDragging,
            "Native drag did not enter actual Outliner transfer state");
        Frame(controller, input, mouse, 1, KeyCode.LeftControl, KeyCode.A, KeyCode.Delete); yield return null;
        Require(view.IsOutlinerDragging && doc.Parts.Count == 4 && doc.Groups.Count == 1 &&
            doc.Selection.Count == 1 && doc.IsPartSelected("left") && doc.Parts[0].ParentGroupId == originalGroup,
            "Ctrl+A/Delete changed selection or geometry during an Outliner drag");
        Frame(controller, input, mouse, 1, KeyCode.Escape); yield return null;
        Require(!view.IsOutlinerDragging && doc.Selection.Count == 1 && doc.IsPartSelected("left") &&
            doc.Parts[0].ParentGroupId == originalGroup && controller.IsOpen,
            "Escape cancelled more than the Outliner transfer or left drag state active");
        GameObject rootDrop = Field<TMP_Text>(view, "outlinerTitle").transform.parent.gameObject;
        ExecuteEvents.ExecuteHierarchy(rootDrop, pointer, ExecuteEvents.pointerEnterHandler);
        typeof(StandaloneInputModule).GetMethod("ReleaseMouse", Private).Invoke(module, new object[] { pointer, rootDrop });
        Frame(controller, input, mouse); yield return null;
        Require(!view.IsOutlinerDragging && doc.Parts.Count == 4 && doc.Groups.Count == 1 &&
            doc.Parts[0].ParentGroupId == originalGroup && doc.Selection.Count == 1 && doc.IsPartSelected("left"),
            "Delayed native drop after Escape moved the selected child or changed selection");
        NativeClick(view, uiCamera, "Row_" + originalGroup); yield return null;
        Frame(controller, input, scene.Camera.WorldToScreenPoint(new Vector3(0,.5f,0))); yield return null;
        Require(doc.Selection.Count == 1 && doc.Selection[0] == originalGroup,
            "Tree cancellation fixture did not restore the selected group");
    }

    private static IEnumerable TestInteriorCamera(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, string output)
    {
        var doc = controller.Document;
        int revision = doc.RevisionToken;
        bool originalProjection = Field<bool>(controller, "orthographic");
        bool originalSnap = Field<bool>(controller, "placementSnap");
        Set(controller, "orthographic", true); view.SetProjection(true); Call(controller, "ApplyCamera");
        Vector3 overviewPosition = scene.Camera.transform.position, focus = Field<Vector3>(controller, "cameraFocus");
        Quaternion overviewRotation = scene.Camera.transform.rotation;
        float distance = Field<float>(controller, "cameraDistance"), yaw = Field<float>(controller, "cameraYaw"),
            pitch = Field<float>(controller, "cameraPitch"), clip = scene.Camera.nearClipPlane;
        NativeClick(view, uiCamera, "ViewportSectionButton0"); yield return null;
        var speed = Field<TMP_InputField>(view, "interiorSpeedInput");
        speed.text = "0.5"; speed.onEndEdit.Invoke(speed.text); yield return null;
        Require(Mathf.Abs(Field<float>(controller, "interiorSpeed") - .5f) < .0001f, "Interior speed input is not connected");
        Call(controller, "SetInteriorSpeed", float.NaN);
        Require(Field<float>(controller, "interiorSpeed") == .5f, "Invalid speed reached camera movement");
        NativeClick(view, uiCamera, "InteriorCamera"); yield return null;
        Require(Field<bool>(controller, "interiorCamera") && !view.HasViewportSettings && !scene.Camera.orthographic &&
            scene.Camera.nearClipPlane == .02f && !Field<Button>(view, "projectionButton").interactable &&
            Field<TMP_Text>(view, "interiorCameraHint").gameObject.activeSelf && scene.Camera.transform.position == overviewPosition,
            "Inside entry lost pose, perspective, near clip, visible mode or UI exit");
        Vector2 mouse = view.ViewportScreenRect().center;
        Vector3 start = scene.Camera.transform.position;
        Frame(controller, input, mouse, 2); yield return null;
        Require(Field<bool>(controller, "cameraFlying") && scene.Camera.transform.position == start,
            "Interior RMB did not immediately acquire stationary center aiming");
        Frame(controller, input, mouse, 2, KeyCode.W); yield return null;
        Require(Mathf.Abs(Vector3.Distance(start, scene.Camera.transform.position) - .5f / 30f) < .0001f &&
            Field<bool>(controller, "cameraFlying") && Field<TMP_Text>(view, "cameraReticle").gameObject.activeSelf,
            "Interior speed depends on overview distance or flight lacks its center marker");
        input.Next(mouse, 2); input.MouseX = .2f; input.MouseY = .1f;
        controller.Update(); controller.LateUpdate(); yield return null;
        Require(Quaternion.Angle(scene.Camera.transform.rotation, overviewRotation) > .1f,
            "Interior look input did not turn its own camera");
        start = scene.Camera.transform.position;
        input.HasFocus = false; Frame(controller, input, mouse, 2, KeyCode.W); yield return null;
        Require(!Field<bool>(controller, "cameraFlying") && !Field<TMP_Text>(view, "cameraReticle").gameObject.activeSelf &&
            Cursor.visible && scene.Camera.transform.position == start && Field<bool>(controller, "interiorCamera"),
            "Focus loss did not release flight without moving or losing the interior pose");
        input.HasFocus = true; Frame(controller, input, mouse); yield return null;
        Frame(controller, input, mouse, 2, KeyCode.W); yield return null;
        start = scene.Camera.transform.position;
        view.ShowError("QA", "Camera blocker");
        Frame(controller, input, mouse, 2, KeyCode.W); yield return null;
        Require(!Field<bool>(controller, "cameraFlying") && Cursor.visible && scene.Camera.transform.position == start,
            "Modal kept flight/cursor capture alive");
        Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        Require(!view.HasModal && Field<bool>(controller, "interiorCamera"), "Modal Escape exited the interior mode");
        Frame(controller, input, mouse); yield return null;
        input.Next(mouse, 2, KeyCode.W); input.MouseScrollDelta = Vector2.up;
        controller.Update(); controller.LateUpdate(); yield return null;
        Require(Mathf.Abs(Field<float>(controller, "interiorSpeed") - .6f) < .0001f,
            "Interior wheel did not change speed");
        Frame(controller, input, mouse); yield return null;
        Require(Field<Vector3>(controller, "cameraFocus") == focus && Field<float>(controller, "cameraDistance") == distance &&
            Field<float>(controller, "cameraYaw") == yaw && Field<float>(controller, "cameraPitch") == pitch && doc.RevisionToken == revision,
            "Interior navigation changed the saved overview or document");
        Call(controller, "RequestCatalog"); yield return null;
        NativeClick(view, uiCamera, "Catalog_fixture"); yield return null;
        Set(controller, "placementSnap", false);
        int count = doc.Parts.Count;
        Frame(controller, input, mouse, 2, KeyCode.W); yield return null;
        Frame(controller, input, mouse + Vector2.right * 100, 2); yield return null;
        Require(scene.TryPlacementSurface(mouse, out Vector3 surface), "Interior test has no center-ray target");
        Vector3 preview = Field<Vector3>(controller, "placementPosition");
        Require(Mathf.Abs(preview.x - surface.x) < .001f && Mathf.Abs(preview.z - surface.z) < .001f,
            "Interior preview follows hidden pointer instead of viewport center");
        Frame(controller, input, mouse + Vector2.right * 100, 3); yield return null;
        Require(doc.Parts.Count == count + 1 && Vector3.Distance(V(doc.Parts.Last().Position), preview) < .001f,
            "Interior placement differs from the center-ray preview");
        Frame(controller, input, mouse); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") == null && Field<bool>(controller, "interiorCamera"),
            "Placement Escape lost its existing cancellation priority");
        Frame(controller, input, mouse); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        Require(doc.Parts.Count == count, "Interior placement cannot be undone");
        Frame(controller, input, mouse); yield return null;
        NativeClick(view, uiCamera, "ViewportSectionButton0"); yield return null;
        NativeClick(view, uiCamera, "InteriorCamera"); yield return null;
        Require(!Field<bool>(controller, "interiorCamera") && scene.Camera.orthographic && scene.Camera.nearClipPlane == clip &&
            Vector3.Distance(scene.Camera.transform.position, overviewPosition) < .0001f &&
            Quaternion.Angle(scene.Camera.transform.rotation, overviewRotation) < .001f && Cursor.visible &&
            !Field<TMP_Text>(view, "cameraReticle").gameObject.activeSelf,
            "Return to overview did not restore the exact pose/projection/clip or release input");
        Set(controller, "orthographic", originalProjection); view.SetProjection(originalProjection); Call(controller, "ApplyCamera");
        Set(controller, "placementSnap", originalSnap);
        File.WriteAllText(Path.Combine(output, "interior-camera-check.txt"),
            "PASS actual menu entry/speed and independent position/look; perspective/.02 near clip; RMB release/focus loss/modal pause; " +
            "wheel speed; saved overview/document unchanged by navigation; center-ray preview and placement/Undo, Escape priorities; exact overview pose/projection/clip return. Workbench only; native game cursor/input unverified.");
    }

    private static IEnumerable TestViewportToolbar(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera)
    {
        BlueprintEditorDocument doc = controller.Document;
        string selection = string.Join("|", doc.Selection);
        Vector3 position = V(doc.Parts[0].Position);
        double scale = doc.Parts[0].Scale.X;
        float distance = Field<float>(controller, "cameraDistance");
        float yaw = Field<float>(controller, "cameraYaw"), pitch = Field<float>(controller, "cameraPitch");
        foreach (string field in new[] { "gridButton", "projectionButton", "gridButton", "projectionButton" })
        {
            view.HideViewportSettings();
            NativeClick(view, uiCamera, field == "gridButton" ? "ViewportSectionButton1" : "ViewportSectionButton0");
            Button button = Field<Button>(view, field);
            var rect = (RectTransform)button.transform;
            Vector2 mouse = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
            Require(view.ViewportScreenRect().Contains(mouse) && view.IsViewportControlHit(mouse),
                "Grid/projection icon is not a protected in-viewport control");
            bool gridBefore = Field<Transform>(scene, "majorGrid").gameObject.activeSelf;
            bool projectionBefore = scene.Camera.orthographic;
            input.Next(mouse); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Require(Mathf.Abs(Field<float>(controller, "cameraDistance") - distance) < .0001f,
                "Wheel over viewport icon zoomed the editor camera");
            Frame(controller, input, mouse, 1); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.None &&
                Field<int>(controller, "dragAnchorPoint") < 0 && !Field<bool>(controller, "selectionPending"),
                "Viewport icon pointer-down began a gizmo or selection gesture");
            NativeClick(view, uiCamera, button);
            Frame(controller, input, mouse); yield return null;
            Require(field == "gridButton"
                ? Field<Transform>(scene, "majorGrid").gameObject.activeSelf != gridBefore && scene.Camera.orthographic == projectionBefore
                : scene.Camera.orthographic != projectionBefore && Field<Transform>(scene, "majorGrid").gameObject.activeSelf == gridBefore,
                "Native viewport icon did not toggle only its intended state");
            Require(string.Join("|", doc.Selection) == selection && V(doc.Parts[0].Position) == position &&
                doc.Parts[0].Scale.X == scale && Mathf.Abs(Field<float>(controller, "cameraYaw") - yaw) < .0001f &&
                Mathf.Abs(Field<float>(controller, "cameraPitch") - pitch) < .0001f,
                "Viewport icon changed selection, geometry or camera orientation");
        }
        Require(Field<Transform>(scene, "majorGrid").gameObject.activeSelf &&
            Field<Transform>(scene, "minorGrid").gameObject.activeSelf && !scene.Camera.orthographic,
            "Viewport toolbar fixture did not restore grid/perspective");
        Camera template = Field<Camera>(controller, "cameraTemplate");
        float worldFov = template.fieldOfView;
        view.HideViewportSettings();
        NativeClick(view, uiCamera, "ViewportSectionButton0"); yield return null;
        Slider fov = Field<Slider>(view, "fieldOfViewSlider");
        TMP_InputField fovInput = Field<TMP_InputField>(view, "fieldOfViewInput");
        fov.value = 85f; yield return null;
        Require(Mathf.Abs(scene.Camera.fieldOfView - 85f) < .001f && template.fieldOfView == worldFov,
            "FOV slider did not change only the editor camera");
        fovInput.SetTextWithoutNotify("55"); fovInput.onEndEdit.Invoke("55"); yield return null;
        Require(Mathf.Abs(scene.Camera.fieldOfView - 55f) < .001f && Mathf.Abs(fov.value - 55f) < .001f,
            "Numeric FOV did not synchronize camera and slider");
        NativeClick(view, uiCamera, "FieldOfViewReset"); yield return null;
        Require(Mathf.Abs(scene.Camera.fieldOfView - worldFov) < .001f, "FOV reset differs from game camera");
        view.HideViewportSettings();
        NativeClick(view, uiCamera, "ViewportSectionButton0"); yield return null;
        NativeClick(view, uiCamera, "ProjectionToggle"); yield return null;
        Require(!fov.interactable && !fovInput.interactable && !Field<Button>(view, "fieldOfViewReset").interactable,
            "Orthographic view leaves perspective FOV controls active");
        NativeClick(view, uiCamera, "ProjectionToggle"); yield return null;
        Require(fov.interactable && fovInput.interactable && template.fieldOfView == worldFov,
            "Perspective did not restore editor FOV controls or changed world camera");
        view.HideViewportSettings();
    }

    private static IEnumerable TestCursorPreview(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string output)
    {
        BlueprintEditorDocument doc = controller.Document;
        scene.NativePointsOnly = true; Call(controller, "UpdateTransformContext");
        Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
        Vector3 original = V(doc.Parts[0].Position);
        Vector2 start = scene.Camera.WorldToScreenPoint(original);
        int history = Field<IList>(doc, "undo").Count;
        Frame(controller, input, start, 0, KeyCode.G); yield return null;
        Require(Field<bool>(controller, "keyboardPreview") && V(doc.Parts[0].Position) == original,
            "G did not start a non-destructive cursor preview");
        Vector2 empty = new Vector2(scene.Camera.pixelRect.xMin + 30, scene.Camera.pixelRect.yMin + 30);
        Frame(controller, input, empty); yield return null;
        Require(Field<bool>(controller, "keyboardValid") && V(doc.Parts[0].Position) == original,
            "Auto surface move cannot use the workspace ground without modifying the document");
        Vector3 cameraFocus = Field<Vector3>(controller, "cameraFocus");
        float cameraPitch = Field<float>(controller, "cameraPitch");
        Set(controller, "cameraFocus", Vector3.up * 100);
        Set(controller, "cameraPitch", -89f);
        Frame(controller, input, empty); yield return null;
        Require(!Field<bool>(controller, "keyboardValid") && V(doc.Parts[0].Position) == original,
            "Surface move accepts a ray with neither a piece nor ground contact");
        Frame(controller, input, empty, 0, KeyCode.Return); yield return null;
        Require(Field<bool>(controller, "keyboardPreview"), "Missing surface confirmed a move");
        Set(controller, "cameraFocus", cameraFocus); Set(controller, "cameraPitch", cameraPitch);
        Frame(controller, input, empty); yield return null;
        Vector2 surface = scene.Camera.WorldToScreenPoint(V(doc.Parts[1].Position));
        Frame(controller, input, surface, 0, KeyCode.LeftShift); yield return null;
        Require(Field<bool>(controller, "keyboardValid"), "Surface fixture did not hit an unmoved part");
        Frame(controller, input, surface, 0, KeyCode.LeftShift, KeyCode.Q); yield return null;
        int sourceIndex = Field<int>(controller, "keyboardSourceIndex");
        int nativeStart = Field<int>(controller, "gizmoNativeAnchorStart");
        int nativeEnd = Field<int>(controller, "dragNativeAnchorEnd");
        foreach (int choice in Field<List<int>>(controller, "keyboardSourceChoices"))
            Require(choice >= nativeStart && choice < nativeEnd, "Default Q/E cycle contains helpers before native points");
        Vector3 selectedSource = Field<Vector3[]>(controller, "dragSourceAnchors")[sourceIndex];
        Vector3 sourcePivot = Field<Vector3>(controller, "dragPivot");
        Vector3 sourceWorld = sourcePivot + Field<Quaternion>(controller, "dragRotation") *
            (selectedSource - sourcePivot) * Field<float>(controller, "dragScale") + Field<Vector3>(controller, "dragTranslation");
        Require(scene.TryMoveSurface(surface, new[] { "left" }, out Vector3 sourceTarget) &&
            Vector3.Distance(sourceWorld, sourceTarget) < .001f,
            "Q did not attach the new source at the unchanged mouse position in the same frame");
        Frame(controller, input, surface, 0, KeyCode.LeftShift); yield return null;
        Frame(controller, input, surface, 0, KeyCode.LeftShift, KeyCode.E); yield return null;
        Require(Field<int>(controller, "keyboardSourceIndex") != sourceIndex && Field<bool>(controller, "keyboardValid"),
            "E at the same cursor did not select and evaluate the next point");
        Require(Field<int>(controller, "keyboardSourceIndex") == -1, "Q then E does not return to Auto");
        NativeClick(view, uiCamera, "ViewportSectionButton3"); yield return null;
        Require(!Field<Button>(view, "geometryPointsButton").interactable &&
            !Field<Button>(view, "edgeButton").interactable,
            "Pending G allows geometry/frame settings to change frozen source indices");
        NativeClick(view, uiCamera, "GeometryPoints"); yield return null;
        Require(!scene.ExperimentalGeometryPoints, "Disabled geometry toggle changed anchors during G");
        int firstNative = Field<List<int>>(controller, "keyboardSourceChoices")[0];
        NativeClick(view, uiCamera, "CursorSource_" + firstNative); yield return null;
        Require(Field<bool>(controller, "keyboardPreview") && V(doc.Parts[0].Position) == original &&
            Field<IList>(doc, "undo").Count == history && Field<int>(controller, "keyboardSourceIndex") == firstNative,
            "Source chooser confirms/mutates document instead of only selecting a point");
        NativeClick(view, uiCamera, "PointTypes"); yield return null;
        Require(Field<List<int>>(controller, "keyboardSourceChoices").Exists(index => index < nativeStart),
            "Additional points do not extend the explicitly enabled source set");
        NativeClick(view, uiCamera, "PointTypes"); yield return null;
        Require(Field<List<int>>(controller, "keyboardSourceChoices").TrueForAll(index => index >= nativeStart && index < nativeEnd),
            "Native-only header leaves hidden helpers in Q/E choices");
        NativeClick(view, uiCamera, "PointTypes"); yield return null;
        Require(Field<List<int>>(controller, "keyboardSourceChoices").Exists(index => index < nativeStart),
            "Restoring both point types did not restore opted-in helper choices");
        NativeClick(view, uiCamera, "PointTypes"); yield return null;
        Require(Field<List<int>>(controller, "keyboardSourceChoices").TrueForAll(index => index >= nativeStart && index < nativeEnd),
            "Disabling helpers still lengthens the native-only Q/E cycle");
        view.HideViewportSettings();
        Frame(controller, input, surface); yield return null;
        Frame(controller, input, surface, 0, KeyCode.R); yield return null;
        Frame(controller, input, surface); yield return null;
        Frame(controller, input, surface, 0, KeyCode.G); yield return null;
        Require(Field<int>(controller, "keyboardSourceIndex") == firstNative &&
            Field<bool>(controller, "keyboardSurface") && V(doc.Parts[0].Position) == original,
            "G to R to G loses the explicitly chosen attachment source or mutates the document");
        Set(controller, "cameraFocus", Vector3.up * 100); Set(controller, "cameraPitch", -89f);
        Frame(controller, input, empty, 0, KeyCode.Return); yield return null;
        Require(Field<bool>(controller, "keyboardPreview") && !Field<bool>(controller, "keyboardValid") &&
            Field<IList>(doc, "undo").Count == history, "Same-frame miss+Enter committed a stale surface hit");
        Set(controller, "cameraFocus", cameraFocus); Set(controller, "cameraPitch", cameraPitch);
        Frame(controller, input, empty); yield return null;
        Frame(controller, input, surface, 0, KeyCode.LeftShift); yield return null;
        Frame(controller, input, new Vector2(20, 1050), 0, KeyCode.Return); yield return null;
        Require(Field<bool>(controller, "keyboardPreview"), "Enter over UI confirmed a cursor move");
        Frame(controller, input, start, 0, KeyCode.Escape); yield return null;
        Frame(controller, input, start); yield return null;
        Frame(controller, input, start, 0, KeyCode.G); yield return null;
        Frame(controller, input, empty, 0, KeyCode.Delete); yield return null;
        Require(doc.Parts.Count == 4 && Field<IList>(doc, "undo").Count == history, "Pending move deleted/undid document state");
        Frame(controller, input, start, 0, KeyCode.P); yield return null;
        Frame(controller, input, start, 0, KeyCode.X); yield return null;
        Vector2 end = start + Field<Vector2>(controller, "dragScreenDirection") * 60;
        Frame(controller, input, end); yield return null;
        Frame(controller, input, start); yield return null;
        Require(Vector3.Distance(VisualRoot(scene, "left").transform.position, original) < .0001f,
            "Returning to the axis start retained a stale nonzero delta");
        Frame(controller, input, end); yield return null;
        Vector3 pose = VisualRoot(scene, "left").transform.position;
        Require(Vector3.Distance(pose, original) > .05f && V(doc.Parts[0].Position) == original,
            "Axis cursor preview did not move only the temporary visual");
        Save(scene, view, uiCamera, target, Path.Combine(output, "input-cursor-move.png"));
        Frame(controller, input, end, 4); yield return null;
        Frame(controller, input, end + new Vector2(25,10), 4); yield return null;
        Frame(controller, input, end + new Vector2(25,10)); yield return null;
        Frame(controller, input, end + new Vector2(25,10)); yield return null;
        Require(Vector3.Distance(VisualRoot(scene, "left").transform.position, pose) < .0001f &&
            Field<bool>(controller, "keyboardPreview"), "Camera navigation confirmed or jumped the cursor preview");
        Frame(controller, input, end + new Vector2(25,10), 0, KeyCode.Return); yield return null;
        Require(!Field<bool>(controller, "keyboardPreview") && Vector3.Distance(V(doc.Parts[0].Position), pose) < .001f &&
            Field<IList>(doc, "undo").Count == history + 1, "Cursor move did not commit exactly one Undo");
        Frame(controller, input, start, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        Require(V(doc.Parts[0].Position) == original && Field<IList>(doc, "undo").Count == history,
            "One Undo did not restore the cursor move");
        Frame(controller, input, start); yield return null;
        Frame(controller, input, start, 0, KeyCode.S); yield return null;
        Frame(controller, input, start, 0, KeyCode.Return); yield return null;
        Require(Field<IList>(doc, "undo").Count == history, "Zero-delta preview added Undo");
        Frame(controller, input, start); yield return null;
        Frame(controller, input, start, 0, KeyCode.G); yield return null;
        Frame(controller, input, start, 0, KeyCode.P); yield return null;
        Frame(controller, input, start + Vector2.right * 40); yield return null;
        input.HasFocus = false;
        Frame(controller, input, start + Vector2.right * 40); yield return null;
        input.HasFocus = true;
        Require(!Field<bool>(controller, "keyboardPreview") && V(doc.Parts[0].Position) == original &&
            Vector3.Distance(VisualRoot(scene, "left").transform.position, original) < .0001f,
            "Focus loss did not restore the document and visual");
        foreach (KeyCode action in new[] { KeyCode.R, KeyCode.S })
        {
            Vector2 origin = scene.Camera.WorldToScreenPoint(original);
            Vector2 begin = origin + Vector2.right * 100;
            Frame(controller, input, begin); yield return null;
            Frame(controller, input, begin, 0, action); yield return null;
            Frame(controller, input, begin + Vector2.up * 45); yield return null;
            Require(Field<bool>(controller, "keyboardPreview") && V(doc.Parts[0].Position) == original,
                action + " changed the document before confirmation");
            Frame(controller, input, begin + Vector2.up * 45, 0, KeyCode.Return); yield return null;
            Require(Field<IList>(doc, "undo").Count == history + 1, action + " was not one committed Undo");
            Frame(controller, input, begin, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(Field<IList>(doc, "undo").Count == history && Math.Abs(doc.Parts[0].Scale.X - 1) < .0001 &&
                Quaternion.Angle(VisualRoot(scene, "left").transform.rotation, Quaternion.identity) < .001f,
                action + " did not undo exactly");
        }
        Frame(controller, input, start); yield return null;
        Rect before = view.ViewportScreenRect();
        NativeClick(view, uiCamera, "HintsToggle"); Frame(controller, input, start); yield return null;
        Require(view.ViewportScreenRect().height > before.height + 100 &&
            Field<TMP_Text>(view, "nextActionText").gameObject.activeInHierarchy, "Hints collapse did not recover viewport space");
        NativeClick(view, uiCamera, "HintsToggle"); Frame(controller, input, start); yield return null;
        Require(Mathf.Abs(view.ViewportScreenRect().height - before.height) < .01f, "Hints expand did not restore layout");
        NativeClick(view, uiCamera, "ViewportFocus"); Frame(controller, input, start); yield return null;
        Require(!Field<RectTransform>(view, "outliner").gameObject.activeSelf && view.ViewportScreenRect().width > before.width,
            "Focus did not reclaim the side panel");
        NativeClick(view, uiCamera, "ViewportFocus"); Frame(controller, input, start); yield return null;
        Frame(controller, input, start, 0, KeyCode.Space); yield return null;
        Require(view.HasToolsMenu, "Space did not open the single tool menu");
        Frame(controller, input, start, 0, KeyCode.Alpha2); yield return null;
        Require(!view.HasToolsMenu && Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Transform,
            "Tool menu numeric choice did not select the persistent gizmo");
        Frame(controller, input, start); yield return null;
        NativeClick(view, uiCamera, "ViewportSectionButton2"); yield return null;
        NativeClick(view, uiCamera, "GizmoFamilyPoints"); yield return null;
        Require(!view.HasViewportSettings, "Choosing a handle family must close settings before the next part click");
        Frame(controller, input, start); yield return null;
        Require(Field<GizmoFamily>(controller, "gizmoFamily") == GizmoFamily.Points, "Manipulator family did not update");
        foreach (bool modelSpace in new[] { true, false })
        {
            scene.ModelSpaceGizmo = modelSpace;
            doc.ClearSelection(); view.Bind(doc, true, null, Vector3.one);
            Frame(controller, input, Vector2.zero); yield return null;
            NativeClick(view, uiCamera, "ViewportSectionButton2"); yield return null;
            NativeClick(view, uiCamera, "GizmoFamilyScale"); yield return null;
            Require(!view.HasViewportSettings, "Scale settings swallow the next click");
            NativeClick(view, uiCamera, "Row_left");
            Frame(controller, input, start); yield return null;
            Require(Field<GizmoFamily>(controller, "gizmoFamily") == GizmoFamily.Scale &&
                Field<LineRenderer>(Field<TransformGizmoView>(scene, "gizmo"), "scaleHandle").gameObject.activeInHierarchy &&
                !Field<LineRenderer[]>(Field<TransformGizmoView>(scene, "gizmo"), "moveLines")[0].gameObject.activeInHierarchy,
                "First part click lost Scale handles in sizing mode " + modelSpace);
            NativeClick(view, uiCamera, "ViewportSectionButton2"); yield return null;
            NativeClick(view, uiCamera, "GizmoFamilyScale");
            Frame(controller, input, start); yield return null;
            Require(Field<GizmoFamily>(controller, "gizmoFamily") == GizmoFamily.Combined,
                "Repeated handle button did not restore all handles");
        }
        scene.ModelSpaceGizmo = true;
        // Tooltip measurements use the actual font and the safe canvas, not character counts.
        RectTransform anchor = Field<RectTransform>(view, "top");
        foreach (float scale in new[] { 1f, 1.2f, 1.4f })
        foreach (string help in new[] { BuildWorksLocalization.Text("editor.view.tool_transform"),
            "Перемещение, вращение и масштаб выбранной детали. X/Y/Z — ось; повтор — локальные координаты; Esc — отмена без изменения чертежа." })
        {
            view.SetUiScale(scale);
            view.GetType().GetMethod("BeginTooltip", Private).Invoke(view, new object[] { anchor, help });
            view.GetType().GetMethod("ShowTooltipNow", Private).Invoke(view, null);
            Canvas.ForceUpdateCanvases();
            TMP_Text tooltip = Field<TMP_Text>(view, "tooltipText"); tooltip.ForceMeshUpdate();
            Require(!tooltip.isTextOverflowing && !tooltip.raycastTarget, "Measured EN/RU tooltip is clipped or captures input at " + scale);
        }
        view.SetUiScale(1f);
        Set(controller, "gizmoFamily", GizmoFamily.Move);
        Frame(controller, input, start); yield return null;
    }

    private static void TextFrame(BlueprintEditorController controller, InputFrames input, Vector2 mouse,
        string text, params KeyCode[] keys)
    {
        input.Next(mouse, 0, keys); input.InputString = text;
        controller.Update(); controller.LateUpdate(); Canvas.ForceUpdateCanvases();
    }

    private static IEnumerable TestContinuousPointer(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture renderTarget, string output)
    {
        Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
        Rect viewport = view.ViewportScreenRect();
        Vector2 start = viewport.center;
        Vector2 edge = new Vector2(viewport.xMin + viewport.width * .3f, viewport.yMax - 2f);
        Require(!view.IsViewportControlHit(edge), "Continuous pointer test edge is covered by UI");
        var doc = controller.Document;
        BlueprintEditorPart Part() => doc.Parts.First(x => x.StableId == "left");
        Point3 originalScale = Part().Scale, originalPosition = Part().Position;
        int history = Field<IList>(doc, "undo").Count;
        object savedFrame = Field<object>(controller, "transformFrame");
        bool savedLocal = Field<bool>(controller, "localSpace");
        input.WarpAvailable = true;
        foreach (Vector2 border in new[] { edge,
            new Vector2(edge.x, viewport.yMin + 2),
            new Vector2(viewport.xMin + 2, viewport.center.y),
            new Vector2(viewport.xMax - 2, viewport.center.y),
            new Vector2(edge.x, viewport.yMin - 15) })
        {
            input.WarpTargets.Clear();
            Frame(controller, input, start, 0, KeyCode.S); yield return null;
            Frame(controller, input, border); yield return null;
            Require(input.WarpTargets.Count == 1 && viewport.Contains(input.WarpTargets[0]),
                "Scale pointer stops at viewport edge instead of wrapping: " + border +
                " viewport=" + viewport + " targets=" + input.WarpTargets.Count +
                " unavailable=" + Field<bool>(controller, "pointerWrapUnavailable") +
                " paused=" + Field<bool>(controller, "keyboardPaused") +
                " handle=" + Field<GizmoHandleKind>(controller, "dragHandle") +
                " cursor=" + Cursor.lockState + " screen=" + Screen.width + "x" + Screen.height);
            float preview = Field<float>(controller, "dragScale");
            Vector2 target = input.WarpTargets[0];
            Frame(controller, input, border); yield return null;
            Frame(controller, input, border); yield return null;
            Require(Field<bool>(controller, "pointerWarpPending") &&
                Field<float>(controller, "dragScale") == preview && input.WarpTargets.Count == 1,
                "Delayed warp moved preview or retried an outstanding native command");
            // Hand motion toward the landing edge is not acknowledgement of a delayed native warp.
            Frame(controller, input, Vector2.Lerp(border, target, .55f)); yield return null;
            Require(Field<bool>(controller, "pointerWarpPending") &&
                Field<float>(controller, "dragScale") == preview && input.WarpTargets.Count == 1,
                "Manual halfway motion falsely acknowledged a pending warp and jumped preview");
            Frame(controller, input, target); yield return null;
            Require(!Field<bool>(controller, "pointerWarpPending") &&
                Mathf.Abs(Field<float>(controller, "dragScale") - preview) < .00001f,
                "Warp-generated pointer jump changed scale");
            if (border == edge)
                Save(scene, view, uiCamera, renderTarget, Path.Combine(output, "input-pointer-wrap.png"));
            Vector2 continued = target + start - border + Vector2.up * 20f;
            Frame(controller, input, continued); yield return null;
            Require(Field<float>(controller, "dragScale") != preview,
                "Hand motion after acknowledged warp did not continue scaling");
            Frame(controller, input, continued, 0, KeyCode.Escape); yield return null;
            Require(Part().Scale.Equals(originalScale) && Part().Position.Equals(originalPosition) &&
                Field<IList>(doc, "undo").Count == history &&
                Field<Vector2>(controller, "transformPointerOffset") == Vector2.zero,
                "Wrapping cancellation changed document/history or left pointer offset");
        }

        foreach (string buttonName in new[] { "ViewportFocus", "ViewportSectionButton1" })
        foreach (bool control in new[] { false, true })
        foreach (KeyCode mode in new[] { KeyCode.G, KeyCode.R, KeyCode.S })
        {
            Frame(controller, input, start, 0, mode); yield return null;
            Frame(controller, input, start, 0, KeyCode.X); yield return null;
            var button = view.RootCanvas.GetComponentsInChildren<Button>(true).Single(value => value.name == buttonName);
            var rect = (RectTransform)button.transform;
            Vector2 buttonMouse = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
            Require(view.IsViewportControlHit(buttonMouse), "Toolbar fixture is outside its control hit region");
            Frame(controller, input, buttonMouse, 1, control ? new[] { KeyCode.LeftControl } : Array.Empty<KeyCode>()); yield return null;
            Require(Field<bool>(controller, "keyboardPreview") && Field<IList>(doc, "undo").Count == history &&
                Part().Scale.Equals(originalScale) && Part().Position.Equals(originalPosition),
                "Toolbar mouse-down implicitly committed a transform");
            NativeClick(view, uiCamera, buttonName); yield return null;
            Require(Field<IList>(doc, "undo").Count == history && Part().Scale.Equals(originalScale) && Part().Position.Equals(originalPosition),
                "Toolbar click applied model changes");
            NativeClick(view, uiCamera, buttonName); yield return null; // Restore focus/settings before cancelling preview.
            Frame(controller, input, start, 0, KeyCode.Escape); yield return null;
        }

        input.WarpTargets.Clear();
        Frame(controller, input, start, 0, KeyCode.S); yield return null;
        Frame(controller, input, edge, 0, KeyCode.LeftControl); yield return null;
        Require(input.WarpTargets.Count == 0, "Ctrl cannot release pointer to panels");
        NativeClick(view, uiCamera, "PreviewAxisX"); yield return null;
        Require(Field<GizmoAxis>(controller, "dragAxis") == GizmoAxis.X,
            "Ctrl panel access lost native axis button input");
        Frame(controller, input, edge, 0, KeyCode.Escape); yield return null;
        Frame(controller, input, start, 0, KeyCode.G); yield return null;
        Frame(controller, input, edge); yield return null;
        Require(input.WarpTargets.Count == 0, "Surface placement was warped to another target");
        Frame(controller, input, edge, 0, KeyCode.Escape); yield return null;

        foreach (bool focusLoss in new[] { false, true })
        {
            Frame(controller, input, start, 0, KeyCode.S); yield return null;
            Frame(controller, input, edge); yield return null;
            float retainedPreview = Field<float>(controller, "dragScale");
            input.HasFocus = !focusLoss;
            for (int i = 0; i < (focusLoss ? 1 : 9); ++i)
            { Frame(controller, input, edge); yield return null; }
            input.HasFocus = true;
            if (!focusLoss)
            {
                Require(Field<bool>(controller, "keyboardPreview") && !Field<bool>(controller, "pointerWarpPending") &&
                    Field<bool>(controller, "pointerWrapUnavailable") && Field<float>(controller, "dragScale") == retainedPreview,
                    "Unacknowledged warp erased the current keyboard preview");
                Frame(controller, input, edge, 0, KeyCode.Escape); yield return null;
            }
            Require(!Field<bool>(controller, "keyboardPreview") && !Field<bool>(controller, "pointerWarpPending") &&
                Part().Scale.Equals(originalScale) && Field<IList>(doc, "undo").Count == history &&
                Cursor.lockState == CursorLockMode.None && Cursor.visible,
                "Focus loss or explicit cancellation kept transform/cursor or changed data");
        }

        Frame(controller, input, start, 0, KeyCode.S); yield return null;
        Frame(controller, input, edge); yield return null;
        float confirmedScale = Field<float>(controller, "dragScale");
        Frame(controller, input, edge, 0, KeyCode.Return); yield return null;
        Require(!Field<bool>(controller, "keyboardPreview") && !Field<bool>(controller, "pointerWarpPending") &&
            Mathf.Abs((float)Part().Scale.X - (float)originalScale.X * confirmedScale) < .0001f &&
            Field<IList>(doc, "undo").Count == history + 1,
            "Enter during pending warp changed accepted scale or failed one-step commit");
        Require(doc.Undo(), "Pending-warp commit cannot Undo"); Call(controller, "Bind", (object)null);

        input.WarpAvailable = false; input.WarpAttempts = 0;
        Frame(controller, input, start, 0, KeyCode.S); yield return null;
        Frame(controller, input, edge); yield return null;
        Frame(controller, input, edge); yield return null;
        Require(input.WarpAttempts == 1 && Field<bool>(controller, "pointerWrapUnavailable") &&
            !Field<bool>(controller, "pointerWarpPending") && Cursor.lockState == CursorLockMode.None && Cursor.visible,
            "Unavailable backend was retried or captured cursor");
        input.Next(start); input.InputString = "1.25"; controller.Update(); yield return null;
        Frame(controller, input, start, 0, KeyCode.Return); yield return null;
        Require(Mathf.Abs((float)Part().Scale.X - (float)originalScale.X * 1.25f) < .0001f,
            "Unsupported warp broke existing numeric confirmation");
        Require(doc.Undo(), "Unsupported-warp numeric commit cannot Undo"); Call(controller, "Bind", (object)null);

        input.WarpAvailable = true; input.WarpTargets.Clear();
        Set(controller, "transformFrame", Enum.Parse(savedFrame.GetType(), "View"));
        Set(controller, "localSpace", true); Set(controller, "gizmoFamily", GizmoFamily.Combined);
        Call(controller, "SetActiveTool", BlueprintEditorTool.Transform); Call(controller, "UpdateTransformContext");
        foreach (bool local in new[] { false, true })
        foreach (var pair in new[] { new[] { KeyCode.X, KeyCode.Y }, new[] { KeyCode.X, KeyCode.Z }, new[] { KeyCode.Y, KeyCode.Z } })
        {
            Set(controller, "localSpace", local);
            Frame(controller, input, start, 0, KeyCode.G); yield return null;
            Frame(controller, input, start, 0, pair[0]); yield return null;
            Frame(controller, input, start, 0, pair[1]); yield return null;
            var planes = Field<LineRenderer[]>(Field<TransformGizmoView>(scene, "gizmo"), "movePlanes");
            int expectedNormal = !pair.Contains(KeyCode.X) ? 0 : !pair.Contains(KeyCode.Y) ? 1 : 2;
            for (int axis = 0; axis < 3; axis++)
                Require((Mathf.Abs(planes[axis].startColor.r - 1f) < .004f &&
                    Mathf.Abs(planes[axis].startColor.g - .78f) < .004f && Mathf.Abs(planes[axis].startColor.b - .32f) < .004f) == (axis == expectedNormal),
                    "Keyboard pair highlights a different plane: " + pair[0] + pair[1] + " local=" + local +
                    " plane=" + axis + " color=" + planes[axis].startColor + " handle=" + Field<GizmoHandleKind>(controller, "dragHandle") +
                    " first=" + Field<GizmoAxis>(controller, "dragAxis") + " second=" + Field<GizmoAxis>(controller, "keyboardSecondAxis"));
            Frame(controller, input, start, 0, KeyCode.Escape); yield return null;
        }
        Set(controller, "localSpace", true);
        foreach (KeyCode mode in new[] { KeyCode.G, KeyCode.R })
        foreach (Vector2 border in new[] { edge, new Vector2(edge.x, viewport.yMin - 8),
            new Vector2(viewport.xMin - 8, viewport.center.y), new Vector2(viewport.xMax + 8, viewport.center.y),
            new Vector2(viewport.xMin + 2, viewport.yMin + 2), new Vector2(viewport.xMax - 2, viewport.yMax - 2) })
        {
            input.WarpTargets.Clear();
            Frame(controller, input, start, 0, mode); yield return null;
            Frame(controller, input, start, 0, KeyCode.X); yield return null;
            Frame(controller, input, start, 0, KeyCode.Z); yield return null;
            Frame(controller, input, border); yield return null;
            Require(input.WarpTargets.Count == 1, "G/R mouse preview did not wrap");
            Vector3 beforeTranslation = Field<Vector3>(controller, "dragTranslation");
            Quaternion beforeRotation = Field<Quaternion>(controller, "dragRotation");
            Frame(controller, input, input.WarpTargets[0]); yield return null;
            Require(Vector3.Distance(Field<Vector3>(controller, "dragTranslation"), beforeTranslation) < .0001f &&
                Quaternion.Angle(Field<Quaternion>(controller, "dragRotation"), beforeRotation) < .01f,
                "Warp jump changed G-plane/R rotation preview");
            Frame(controller, input, start, 0, KeyCode.Escape); yield return null;
        }
        // Physical LMB drag must commit its last preview even before the warp event arrives.
        input.WarpTargets.Clear();
        Frame(controller, input, start); yield return null;
        var gizmo = Field<TransformGizmoView>(scene, "gizmo");
        var handle = Field<LineRenderer>(gizmo, "depthHandle");
        Vector2 handleMouse = scene.Camera.WorldToScreenPoint((handle.GetPosition(0) + handle.GetPosition(2)) * .5f);
        Frame(controller, input, handleMouse, 1); yield return null;
        Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Move,
            "Physical continuous pointer fixture missed depth handle");
        Frame(controller, input, edge, 1); yield return null;
        Require(input.WarpTargets.Count == 1, "Physical gizmo did not wrap");
        Vector3 translation = Field<Vector3>(controller, "dragTranslation");
        Frame(controller, input, edge); yield return null;
        Require(!Field<bool>(controller, "pointerWarpPending") &&
            Vector3.Distance(V(Part().Position), V(originalPosition) + translation) < .0001f,
            "Physical release during pending warp lost preview or moved twice");
        Require(doc.Undo(), "Physical wrapped move cannot Undo"); Call(controller, "Bind", (object)null);
        Set(controller, "transformFrame", savedFrame); Set(controller, "localSpace", savedLocal);
        Call(controller, "UpdateTransformContext");
        input.WarpAvailable = false;
        Frame(controller, input, start); yield return null;
    }

    private static IEnumerable TestInputRevision(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string output)
    {
        BlueprintEditorDocument doc = controller.Document;
        Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Select]);
        Vector3 original = V(doc.Parts[0].Position);
        Vector2 mouse = scene.Camera.WorldToScreenPoint(original) + Vector3.right * 100;
        int history = Field<IList>(doc, "undo").Count;
        var gizmo = Field<TransformGizmoView>(scene, "gizmo");
        Frame(controller, input, mouse); yield return null;
        Require(Field<GameObject>(gizmo, "root").activeSelf && !IsPainted(scene, "left") &&
            Field<GizmoFamily>(controller, "gizmoFamily") == GizmoFamily.Combined && Field<IList>(doc, "undo").Count == history,
            "Selection must show all handles immediately without material overrides or editing");
        Frame(controller, input, mouse, 0, KeyCode.R); yield return null;
        Require(Field<bool>(controller, "keyboardPreview"), "R must be available directly from selection");
        Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.G); yield return null;
        Require(Field<bool>(controller, "keyboardPreview") && Field<GameObject>(gizmo, "root").activeSelf &&
            Field<GizmoFamily>(controller, "gizmoFamily") == GizmoFamily.Combined && Field<IList>(doc, "undo").Count == history,
            "G must start surface preview without a document edit");
        Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        Require(doc.IsPartSelected("left") && Field<GameObject>(gizmo, "root").activeSelf, "Esc lost selection or its handles after preview cancellation");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Alpha2); yield return null;
        Vector2 rotationCenter = scene.Camera.WorldToScreenPoint(Field<Vector3>(controller, "dragPivot"));
        Vector2 rotationStart = rotationCenter + Vector2.right * 100;
        Vector2 rotationMoved = rotationCenter + Vector2.up * 100;
        Frame(controller, input, rotationStart, 0, KeyCode.R); yield return null;
        Frame(controller, input, rotationMoved); yield return null;
        Require(Quaternion.Angle(Field<Quaternion>(controller, "dragRotation"), Quaternion.identity) > 5,
            "Free rotation fixture did not change before axis selection");
        Frame(controller, input, rotationMoved, 0, KeyCode.X); yield return null;
        Require(Quaternion.Angle(Field<Quaternion>(controller, "dragRotation"), Quaternion.identity) < .001f &&
            Vector3.Distance(VisualRoot(scene, "left").transform.position, original) < .001f,
            "Axis choice must reset all prior mouse rotation and position to operation start");
        Frame(controller, input, rotationMoved + Vector2.right * 50); yield return null;
        TextFrame(controller, input, rotationMoved + Vector2.right * 50, "30"); yield return null;
        Require(Quaternion.Angle(Field<Quaternion>(controller, "dragRotation"), Quaternion.AngleAxis(30, Vector3.right)) < .001f,
            "Axis mouse motion must be replaced, not accumulated, by numeric rotation");
        Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.R); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Frame(controller, input, mouse + Vector2.up * 80); yield return null;
        TextFrame(controller, input, mouse + Vector2.up * 80, "30", KeyCode.Alpha3, KeyCode.Alpha0); yield return null;
        Require(Quaternion.Angle(Field<Quaternion>(controller, "dragRotation"), Quaternion.AngleAxis(30, Vector3.right)) < .001f &&
            Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Transform,
            "Typed rotation adds to mouse rotation or digits change tools");
        Vector3 numericPose = VisualRoot(scene, "left").transform.position;
        Frame(controller, input, mouse, 4); yield return null;
        Frame(controller, input, mouse + new Vector2(40,20), 4); yield return null;
        Frame(controller, input, mouse + new Vector2(40,20)); yield return null;
        Frame(controller, input, mouse + new Vector2(40,20)); yield return null;
        Require(Quaternion.Angle(Field<Quaternion>(controller, "dragRotation"), Quaternion.AngleAxis(30, Vector3.right)) < .001f &&
            Vector3.Distance(numericPose, VisualRoot(scene, "left").transform.position) < .0001f && doc.Parts.Count == 4,
            "Camera rebasing changes numeric baseline or deletes a preview target");
        Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
        Require(Field<IList>(doc, "undo").Count == history + 1, "Numeric rotation is not one transaction");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        Require(Quaternion.Angle(VisualRoot(scene, "left").transform.rotation, Quaternion.identity) < .001f &&
            Field<IList>(doc, "undo").Count == history, "Numeric rotation Undo failed");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Y); yield return null;
        Require(Field<IList>(doc, "undo").Count == history + 1, "Numeric rotation Redo failed");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Frame(controller, input, mouse + Vector2.right * 40); yield return null;
        TextFrame(controller, input, mouse, "-2,5", KeyCode.Keypad2); yield return null;
        Require(Vector3.Distance(Field<Vector3>(controller, "dragTranslation"), Vector3.left * 2.5f) < .0001f,
            "Signed comma/keypad Move is not relative to operation start");
        Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
        Require(Vector3.Distance(V(doc.Parts[0].Position), original + Vector3.left * 2.5f) < .001f, "Numeric axis Move did not commit");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
        Frame(controller, input, mouse + Vector2.up * 50); yield return null;
        TextFrame(controller, input, mouse, "1.5"); yield return null;
        Require(Math.Abs(Field<float>(controller, "dragScale") - 1.5f) < .00001f, "Numeric scale multiplied the mouse result");
        Save(scene, view, uiCamera, target, Path.Combine(output, "input-numeric-scale.png"));
        Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
        Require(Math.Abs(doc.Parts[0].Scale.X - 1.5) < .00001f && Field<IList>(doc, "undo").Count == history + 1,
            "Uniform numeric scale commit/history failed");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        foreach (var axisCase in new[] { (KeyCode.X, GizmoAxis.X, new Vector3(1.5f, 1, 1)),
            (KeyCode.Y, GizmoAxis.Y, new Vector3(1, 1.5f, 1)), (KeyCode.Z, GizmoAxis.Z, new Vector3(1, 1, 1.5f)) })
        {
            Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
            Frame(controller, input, mouse); yield return null;
            if (axisCase.Item2 == GizmoAxis.Y) NativeClick(view, uiCamera, "PreviewAxisY");
            else Frame(controller, input, mouse, 0, axisCase.Item1);
            yield return null;
            Frame(controller, input, mouse); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Scale &&
                Field<GizmoAxis>(controller, "dragAxis") == axisCase.Item2, "Scale ignores the selected axis: " + axisCase.Item2);
            Frame(controller, input, mouse + Vector2.up * 50); yield return null;
            Require(Vector3.Distance(VisualRoot(scene, "left").transform.localScale, axisCase.Item3) < .0001f,
                "Mouse axis scale affects other axes: " + axisCase.Item2);
            TextFrame(controller, input, mouse, "1.5"); yield return null;
            Vector3 previewScale = VisualRoot(scene, "left").transform.localScale;
            Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Scale), axisCase.Item3) < .0001f &&
                Vector3.Distance(VisualRoot(scene, "left").transform.localScale, previewScale) < .0001f &&
                Field<IList>(doc, "undo").Count == history + 1, "Axis scale commit differs from preview: " + axisCase.Item2);
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Scale), Vector3.one) < .0001f, "Axis scale Undo failed");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Y); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Scale), axisCase.Item3) < .0001f, "Axis scale Redo failed");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        }
        foreach (var pair in new[] { (KeyCode.X, KeyCode.Y, new Vector3(1, 1, 0)),
            (KeyCode.X, KeyCode.Z, new Vector3(1, 0, 1)), (KeyCode.Y, KeyCode.Z, new Vector3(0, 1, 1)),
            (KeyCode.Z, KeyCode.X, new Vector3(1, 0, 1)) })
        {
            Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
            Frame(controller, input, mouse, 0, pair.Item1); yield return null;
            if (pair.Item1 == KeyCode.X && pair.Item2 == KeyCode.Z) NativeClick(view, uiCamera, "PreviewAxisZ");
            else Frame(controller, input, mouse, 0, pair.Item2);
            yield return null;
            Require(Field<GizmoAxis>(controller, "keyboardSecondAxis") != GizmoAxis.None,
                "Second scale axis replaced the first");
            Frame(controller, input, mouse + Vector2.up * 50); yield return null;
            Vector3 expectedScale = Vector3.one + pair.Item3 * .5f;
            Require(Vector3.Distance(VisualRoot(scene, "left").transform.localScale, expectedScale) < .0001f,
                "Pair mouse scale changed the excluded axis");
            TextFrame(controller, input, mouse, "1.5"); yield return null;
            Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Scale), expectedScale) < .0001f &&
                Field<IList>(doc, "undo").Count == history + 1, "Pair numeric scale/commit was not atomic");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Y); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Scale), expectedScale) < .0001f, "Pair scale Redo failed");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, mouse, 0, KeyCode.G); yield return null;
            Frame(controller, input, mouse, 0, pair.Item1); yield return null;
            Frame(controller, input, mouse, 0, pair.Item2); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.MovePlane,
                "Pair Move did not select its plane");
            Vector3 destination = Field<Vector3>(controller, "dragPlaneStart") +
                Field<Vector3>(controller, "keyboardPlaneFirst") * .4f + Field<Vector3>(controller, "keyboardPlaneSecond") * .6f;
            Vector2 pairMouse = scene.Camera.WorldToScreenPoint(destination);
            Frame(controller, input, pairMouse); yield return null;
            Require(Field<bool>(controller, "keyboardValid"), "Pair plane preview became invalid");
            Vector3 offset = Field<Vector3>(controller, "dragTranslation");
            Require(offset.magnitude > .1f && Mathf.Abs(Vector3.Dot(offset, Vector3.one - pair.Item3)) < .0001f,
                "Pair mouse Move left its plane");
            TextFrame(controller, input, mouse, "-1.25"); yield return null;
            Require(Field<bool>(controller, "keyboardValid") &&
                Vector3.Distance(Field<Vector3>(controller, "dragTranslation"), pair.Item3 * -1.25f) < .0001f,
                "Pair Move number is not per selected axis");
            Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Position), original + pair.Item3 * -1.25f) < .001f &&
                Field<IList>(doc, "undo").Count == history + 1, "Pair Move commit differs from preview");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(Field<GizmoAxis>(controller, "keyboardSecondAxis") == GizmoAxis.None,
                "Pair constraint leaked after commit");
        }
        Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.Z); yield return null;
        bool pairLocal = Field<bool>(controller, "localSpace");
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Require(Field<bool>(controller, "localSpace") != pairLocal &&
            Field<GizmoAxis>(controller, "keyboardSecondAxis") == GizmoAxis.Z, "Pair repeat lost axes or coordinate toggle");
        Frame(controller, input, mouse, 0, KeyCode.Y); yield return null;
        Require(Field<GizmoAxis>(controller, "dragAxis") == GizmoAxis.Y &&
            Field<GizmoAxis>(controller, "keyboardSecondAxis") == GizmoAxis.None, "Third axis did not reset the pair");
        Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        Require(Field<IList>(doc, "undo").Count == history && V(doc.Parts[0].Scale) == Vector3.one,
            "Pair cancellation changed the document");
        Frame(controller, input, mouse, 0, KeyCode.G); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.Z); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.P); yield return null;
        Require(Field<bool>(controller, "keyboardSurface") && Field<GizmoAxis>(controller, "keyboardSecondAxis") == GizmoAxis.None,
            "Surface mode retained a pair constraint");
        Frame(controller, input, mouse, 0, KeyCode.R); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.Z); yield return null;
        Require(Field<GizmoAxis>(controller, "dragAxis") == GizmoAxis.Z &&
            Field<GizmoAxis>(controller, "keyboardSecondAxis") == GizmoAxis.None, "Rotation acquired ambiguous dual axes");
        Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        foreach (var boundary in new[] { (GizmoAxis.X, true, false), (GizmoAxis.None, true, false),
            (GizmoAxis.X, false, false), (GizmoAxis.X, true, true) })
        {
            double startScale = boundary.Item2 ? BlueprintEditorDocument.MaximumScale - .1 : .0101;
            var initial = boundary.Item1 == GizmoAxis.None ? new Point3(startScale, startScale, startScale) : new Point3(startScale, 1, 1);
            Require(doc.SetPartProperties("left", doc.Parts[0].DisplayName, doc.Parts[0].Position,
                doc.Parts[0].Rotation, initial), "Scale boundary fixture failed");
            Call(controller, "Bind", (object)null);
            Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
            if (boundary.Item1 == GizmoAxis.X) { Frame(controller, input, mouse, 0, KeyCode.X); yield return null; }
            if (boundary.Item3) { Frame(controller, input, mouse, 0, KeyCode.Z); yield return null; }
            Vector2 beyond = mouse + Vector2.up * (boundary.Item2 ? 100 : -100);
            Frame(controller, input, beyond); yield return null;
            Require(Field<bool>(controller, "keyboardValid"), "Mouse overshoot invalidates a scale boundary");
            double expected = boundary.Item2 ? BlueprintEditorDocument.MaximumScale : BlueprintEditorDocument.MinimumScale;
            Require(Math.Abs(VisualRoot(scene, "left").transform.localScale.x - expected) < .00001,
                "Mouse did not retain the reachable scale boundary");
            Frame(controller, input, beyond, 0, KeyCode.Return); yield return null;
            Require(!Field<bool>(controller, "keyboardPreview") && Math.Abs(doc.Parts[0].Scale.X - expected) < .00001,
                "Mouse overshoot cannot commit the visible scale boundary");
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, mouse); yield return null;
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Scale), Vector3.one) < .0001f && Field<IList>(doc, "undo").Count == history,
                "Scale boundary Undo did not restore source and history");
        }
        bool boundaryLocal = Field<bool>(controller, "localSpace");
        Set(controller, "localSpace", false);
        foreach (float angle in new[] { 89.99f, 90f })
        {
            Quaternion turn = Quaternion.Euler(0, angle, 0);
            Require(doc.SetPartProperties("left", doc.Parts[0].DisplayName, doc.Parts[0].Position,
                new Rotation3(turn.x, turn.y, turn.z, turn.w), new Point3(10, 1, .01)), "Rotated boundary fixture failed");
            Call(controller, "Bind", (object)null);
            Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
            Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
            // Keep the physical cursor in the viewport; emulate accumulated modal movement.
            Set(controller, "dragStartMouse", mouse - Vector2.up * 2100);
            Frame(controller, input, mouse); yield return null;
            Require(Field<bool>(controller, "keyboardValid") && (angle < 90
                ? Field<float>(controller, "dragScale") == 1 : Field<float>(controller, "dragScale") > 20),
                "Tiny projection missed the bound or orthogonal roundoff froze scale");
            Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
            Require(!Field<bool>(controller, "keyboardPreview"), "Rotated boundary cannot confirm");
            if (angle == 90) { Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null; }
            Frame(controller, input, mouse); yield return null;
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(Field<IList>(doc, "undo").Count == history && Vector3.Distance(V(doc.Parts[0].Scale), Vector3.one) < .0001f,
                "Rotated boundary changed history or source after Undo");
        }
        Set(controller, "localSpace", boundaryLocal);
        var originalIds = Field<List<string>>(controller, "dragIds").ToArray();
        try
        {
            foreach (bool absolute in new[] { false, true })
            {
                var large = new BlueprintEditorPart("bound", "woodwall", "Bound", new Point3(), new Rotation3(0,0,0,1),
                    scale: absolute ? new Point3(float.MaxValue / 2.0, 1, 1) : new Point3(1,1,1),
                    scaleIsAbsolute: absolute, baseScale: absolute ? (Point3?)null : new Point3(float.MaxValue / 2.0, 1, 1));
                Set(controller, "document", new BlueprintEditorDocument(null, "Bound", "OTHER", new[] { large }));
                var ids = Field<List<string>>(controller, "dragIds"); ids.Clear(); ids.Add("bound");
                Require(Math.Abs((float)Call(controller, "ClampMouseScale", 20f) - 2) < .00001,
                    "Mouse scale ignored pinned-product or absolute float bounds");
            }
        }
        finally
        {
            Set(controller, "document", doc);
            var ids = Field<List<string>>(controller, "dragIds"); ids.Clear(); ids.AddRange(originalIds);
        }
        bool savedLocal = Field<bool>(controller, "localSpace");
        object savedFrame = Field<object>(controller, "transformFrame");
        Quaternion diagonal = Quaternion.Euler(0, 45, 0);
        Require(doc.SetPartProperties("left", doc.Parts[0].DisplayName, doc.Parts[0].Position,
            new Rotation3(diagonal.x, diagonal.y, diagonal.z, diagonal.w)), "Cannot arrange rotated axis fixture");
        doc.AcceptLastEdit(); Call(controller, "Bind", (object)null);
        Set(controller, "localSpace", true);
        Set(controller, "transformFrame", Enum.Parse(savedFrame.GetType(), "Local"));
        Call(controller, "UpdateTransformContext");
        Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        TextFrame(controller, input, mouse, "1.5"); yield return null;
        Require(Field<bool>(controller, "keyboardValid") &&
            Vector3.Distance(VisualRoot(scene, "left").transform.localScale, new Vector3(1.5f, 1, 1)) < .0001f,
            "Rotated local X scale was rejected or affected the wrong axis");
        Vector3 localPreview = VisualRoot(scene, "left").transform.localScale;
        Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
        Require(Vector3.Distance(V(doc.Parts[0].Scale), localPreview) < .0001f, "Rotated local scale differs after commit");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Frame(controller, input, mouse); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Require(!Field<bool>(controller, "localSpace"), "Repeated scale axis did not switch to world coordinates");
        TextFrame(controller, input, mouse, "1.5"); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
        Require(!Field<bool>(controller, "keyboardValid") && Field<bool>(controller, "keyboardPreview") &&
            Vector3.Distance(VisualRoot(scene, "left").transform.localScale, Vector3.one) < .0001f &&
            Vector3.Distance(V(doc.Parts[0].Scale), Vector3.one) < .0001f && Field<IList>(doc, "undo").Count == history + 1,
            "Sheared world scale moved the preview, committed, or changed history");
        Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        Require(doc.Undo(), "Cannot restore rotated fixture"); Call(controller, "Bind", (object)null);
        Set(controller, "localSpace", savedLocal); Set(controller, "transformFrame", savedFrame);
        Call(controller, "UpdateTransformContext");
        foreach (string invalid in new[] { "-", "1,", "0", "-1", "10.01", "99999999999999999999999999999999" })
        {
            Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
            TextFrame(controller, input, mouse, invalid); yield return null;
            Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
            Require(Field<bool>(controller, "keyboardPreview") && !Field<bool>(controller, "keyboardValid") &&
                Field<IList>(doc, "undo").Count == history, "Invalid number committed: " + invalid);
            Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        }
        Frame(controller, input, mouse, 0, KeyCode.G); yield return null;
        Require(Field<bool>(controller, "keyboardPreview"), "Repeated G did not start surface Move");
        TextFrame(controller, input, mouse, "2"); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.Return); yield return null;
        Require(!Field<bool>(controller, "keyboardValid") && Field<bool>(controller, "keyboardPreview"), "Free Move accepted an ambiguous scalar");
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Require(Field<bool>(controller, "keyboardValid"), "Axis selection did not validate the pending scalar");
        Frame(controller, input, mouse); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Require(Field<bool>(controller, "keyboardSavedLocalSpace") != Field<bool>(controller, "localSpace"), "Repeated axis did not toggle coordinates");
        TextFrame(controller, input, mouse, "\b"); yield return null;
        Require(Field<string>(controller, "keyboardNumber") == "", "Backspace did not clear numeric ownership");
        Frame(controller, input, mouse, 0, KeyCode.R); yield return null;
        Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Rotate &&
            Field<Vector3>(controller, "dragTranslation") == Vector3.zero, "Action replacement retained previous Move");
        TextFrame(controller, input, mouse, "45"); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.Delete); yield return null;
        Require(doc.Parts.Count == 4 && Field<string>(controller, "keyboardNumber") == "", "Delete did not clear only the numeric buffer");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        Require(!Field<bool>(controller, "keyboardPreview") && Field<IList>(doc, "undo").Count == history &&
            V(doc.Parts[0].Position) == original, "Preview Undo also undid a document transaction");
        foreach (KeyCode tool in new[] { KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha1, KeyCode.Alpha2 })
        {
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Alpha2); yield return null;
            Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
            TextFrame(controller, input, mouse, "1.5"); yield return null;
            Frame(controller, input, mouse, 0, KeyCode.LeftControl, tool); yield return null;
            Require(!Field<bool>(controller, "keyboardPreview") && Field<string>(controller, "keyboardNumber") == "" &&
                Math.Abs(doc.Parts[0].Scale.X - 1) < .00001f, "Ctrl+tool leaked or committed preview: " + tool);
        }
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Alpha1); yield return null;
        mouse = scene.Camera.WorldToScreenPoint(original);
        Frame(controller, input, mouse, 4); yield return null;
        Frame(controller, input, mouse + Vector2.right * 30, 4); yield return null;
        Frame(controller, input, mouse, 4); yield return null;
        Frame(controller, input, mouse); yield return null;
        Require(doc.Parts.Count == 4, "MMB drag returning to origin became deletion");
        mouse = scene.Camera.WorldToScreenPoint(original);
        Frame(controller, input, mouse, 4, KeyCode.LeftShift); yield return null;
        Frame(controller, input, mouse); yield return null;
        Require(doc.Parts.Count == 4, "Modified MMB click deleted a part");
        Frame(controller, input, mouse, 4); yield return null;
        Frame(controller, input, mouse, 4, KeyCode.LeftControl); yield return null;
        Frame(controller, input, mouse, 4); yield return null;
        Frame(controller, input, mouse); yield return null;
        Require(doc.Parts.Count == 4, "Modifier during MMB gesture became a deletion after release");
        Frame(controller, input, mouse, 4); yield return null;
        view.ShowToolsMenu(mouse, false);
        Frame(controller, input, mouse); yield return null;
        Require(!Field<bool>(controller, "cameraDragging") && Field<string>(controller, "middleDeleteTarget") == null,
            "Tools popup retained an MMB deletion gesture");
        Frame(controller, input, mouse, 0, KeyCode.Escape); yield return null;
        Require(doc.Parts.Count == 4, "Popup MMB release deleted a part");
        Frame(controller, input, mouse, 4); yield return null;
        Frame(controller, input, mouse); yield return null;
        Require(doc.Parts.Count == 3 && Field<IList>(doc, "undo").Count == history + 1, "Select MMB click did not delete once");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
        Require(doc.Parts.Count == 4 && doc.IsPartSelected("left"), "Select MMB deletion Undo failed");
        Frame(controller, input, mouse, 0, KeyCode.G); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.S); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.X); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.Z); yield return null;
        TextFrame(controller, input, mouse, "1.5"); yield return null;
        input.HasFocus = false; Frame(controller, input, mouse); yield return null; input.HasFocus = true;
        Require(!Field<bool>(controller, "keyboardPreview") && Field<GizmoAxis>(controller, "keyboardSecondAxis") == GizmoAxis.None &&
            Field<string>(controller, "keyboardNumber") == "" &&
            Math.Abs(doc.Parts[0].Scale.X - 1) < .00001f, "Focus loss leaked numeric preview");
        Frame(controller, input, mouse); yield return null;
        File.WriteAllText(Path.Combine(output, "input-revision-check.txt"),
            "PASS actual Update/LateUpdate: immediate Combined selection gizmo/original materials; G surface preview; R/X mouse then 30; signed Move/comma/keypad; S1.5; invalid input; edit/clear; axis repeat; one Undo/Redo; camera baseline; Ctrl1-4/action replacement; MMB click/drag/modifier; focus cleanup.");
    }

    private static IEnumerable TestTransformContract(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string output)
    {
        BlueprintEditorDocument doc = controller.Document;
        Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        Frame(controller, input, Vector2.zero); yield return null;
        Vector3 originalLeft = V(doc.Parts[0].Position), originalRight = V(doc.Parts[1].Position);
        Vector2 right = scene.Camera.WorldToScreenPoint(originalRight);
        Vector2 otherSurface = ClearSurfacePoint(controller, scene, "right", right, originalLeft);
        Vector2 empty = EmptyTransformPoint(controller, scene, view, originalLeft);
        foreach (KeyCode[] keys in new[] { Array.Empty<KeyCode>(), new[] { KeyCode.LeftShift }, new[] { KeyCode.LeftControl } })
        {
            Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left");
            Frame(controller, input, otherSurface, 1, keys); yield return null;
            Require(Field<bool>(controller, "selectionPending"), "Transform did not start selection on another piece");
            Frame(controller, input, otherSurface, 0, keys); yield return null;
            Require(doc.IsPartSelected("right") && doc.Selection.Count == (keys.Length == 0 ? 1 : 2),
                "Transform click did not replace/add selection as requested");
            empty = EmptyTransformPoint(controller, scene, view, originalRight);
            Frame(controller, input, empty, 1, keys); yield return null;
            Frame(controller, input, empty, 0, keys); yield return null;
            Require(doc.Selection.Count == (keys.Length == 0 ? 0 : 2) &&
                V(doc.Parts[0].Position) == originalLeft && V(doc.Parts[1].Position) == originalRight,
                "Empty-space selection modified geometry or lost modifier selection");
            Frame(controller, input, empty); yield return null;
        }
        Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left");
        empty = EmptyTransformPoint(controller, scene, view, originalLeft);
        Frame(controller, input, empty, 1); yield return null;
        Frame(controller, input, otherSurface, 1); yield return null;
        RectTransform box = Field<RectTransform>(view, "selectionBox");
        Require(box.gameObject.activeSelf && box.sizeDelta.x > 0f && box.sizeDelta.y > 0f &&
            !box.GetComponent<Image>().raycastTarget && !box.GetComponent<Image>().fillCenter,
            "Selection rectangle is invisible, filled or intercepts pointer input");
        Frame(controller, input, otherSurface); yield return null;
        Require(doc.IsPartSelected("right") && !box.gameObject.activeSelf &&
            V(doc.Parts[0].Position) == originalLeft && V(doc.Parts[1].Position) == originalRight,
            "Transform box did not select the target, hide on release, or preserved geometry incorrectly");

        // Select is still additive. Both selected parts must rotate around the same native pin.
        Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
        Frame(controller, input, Vector2.zero, 0, KeyCode.LeftShift);
        NativeClick(view, uiCamera, "Row_right"); yield return null;
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        Frame(controller, input, empty); yield return null;
        Require(doc.EditablePartSelectionCount == 2, "Pin fixture lacks multi-selection");
        Set(controller, "gizmoFamily", GizmoFamily.Points);
        Frame(controller, input, empty); yield return null;
        Vector3[] anchors = Field<Vector3[]>(controller, "gizmoAnchors");
        Vector3 pin = anchors[Field<int>(controller, "gizmoNativeAnchorStart")];
        Vector3 occluder = pin + (scene.Camera.transform.position - pin).normalized * 0.7f;
        doc.AddPart(new BlueprintEditorPart("pin-occluder", "fixture", "Pin occluder",
            new Point3(occluder.x, occluder.y, occluder.z), new Rotation3(0,0,0,1), scale:new Point3(.45,.45,.45)));
        doc.ClearSelection();
        Require(scene.TrySync(doc, out string error), error); view.Bind(doc, true, null, null);
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Select]);
        Frame(controller, input, empty); yield return null;
        Vector3[] selectablePoints = { originalLeft, originalRight, occluder };
        for (int index = 0; index < selectablePoints.Length; ++index)
        {
            Vector2 point = scene.Camera.WorldToScreenPoint(selectablePoints[index]);
            Frame(controller, input, point, 1, KeyCode.LeftShift); yield return null;
            Frame(controller, input, point, 0, KeyCode.LeftShift); yield return null;
            Require(Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Select &&
                doc.EditablePartSelectionCount == index + 1,
                "Select did not remain additive across three Shift clicks without re-entering Select");
        }
        Frame(controller, input, empty); yield return null;
        doc.SelectOnly("left"); doc.ToggleSelection("right");
        Require(scene.TrySync(doc, out error), error); view.Bind(doc, true, null, null);
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        Set(controller, "gizmoFamily", GizmoFamily.Points);
        Frame(controller, input, empty); yield return null;
        foreach (GizmoAxis axis in new[] { GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z })
        {
            Set(controller, "gizmoFamily", GizmoFamily.Points);
            Vector2 pinMouse = scene.Camera.WorldToScreenPoint(pin);
            Require(scene.TryPick(pinMouse, out string picked) && picked == "pin-occluder",
                "Pin regression precondition: unselected mesh does not occlude selected native point");
            Frame(controller, input, pinMouse, 1, KeyCode.LeftShift); yield return null;
            Frame(controller, input, pinMouse, 0, KeyCode.LeftShift); yield return null;
            Require(Field<Vector3?>(controller, "pinnedAnchorWorld").HasValue &&
                Vector3.Distance(Field<Vector3?>(controller, "pinnedAnchorWorld").Value, pin) < .0001f &&
                doc.EditablePartSelectionCount == 2 && !doc.IsPartSelected("pin-occluder"),
                "Transform Shift pin was intercepted by an unselected occluding mesh");
            Frame(controller, input, pinMouse); yield return null;
            KeyCode key = axis == GizmoAxis.X ? KeyCode.X : axis == GizmoAxis.Y ? KeyCode.Y : KeyCode.Z;
            Frame(controller, input, pinMouse, 0, key); yield return null;
            Frame(controller, input, pinMouse); yield return null;
            Require(Field<GizmoAxis>(controller, "anchorConstraintAxis") == axis, "Pinned X/Y/Z shortcut lost axis " + axis);
            Require(Field<LineRenderer>(Field<TransformGizmoView>(scene, "gizmo"), "constraintPath").gameObject.activeSelf,
                "Pinned X/Y/Z has no constraint path before dragging " + axis);
            Vector3 axisWorld = axis == GizmoAxis.X ? Vector3.right : axis == GizmoAxis.Y ? Vector3.up : Vector3.forward;
            Frame(controller, input, pinMouse, 0, KeyCode.R); yield return null;
            Frame(controller, input, pinMouse); yield return null;
            Frame(controller, input, pinMouse, 0, key); yield return null;
            Require(Field<bool>(controller, "keyboardPreview") &&
                Vector3.Distance(Field<Vector3>(controller, "dragPivot"), pin) < .0001f &&
                Vector3.Dot(Field<Vector3>(controller, "dragAxisWorld"), axisWorld) > .999f,
                "R + axis did not rotate around the selected native pivot " + axis +
                "; preview=" + Field<bool>(controller, "keyboardPreview") +
                "; pivot=" + Field<Vector3>(controller, "dragPivot") + "; pin=" + pin +
                "; axis=" + Field<Vector3>(controller, "dragAxisWorld") +
                "; frame=" + Field<object>(controller, "transformFrame") +
                "; local=" + Field<bool>(controller, "localSpace"));
            Quaternion expected = Quaternion.AngleAxis(30f, axisWorld);
            input.Next(pinMouse); input.InputString = "30";
            controller.Update(); controller.LateUpdate(); yield return null;
            Require(Quaternion.Angle(Field<Quaternion>(controller, "dragRotation"), expected) < .1f &&
                Vector3.Distance(Field<Vector3?>(controller, "pinnedAnchorWorld").Value, pin) < .0001f,
                "Numeric rotation uses a different axis or moves pivot A " + axis);
            if (axis == GizmoAxis.X)
                Save(scene, view, uiCamera, target, Path.Combine(output, "input-transform-occluded-pin-x.png"));
            Frame(controller, input, pinMouse, 0, KeyCode.Return); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Position), pin + expected * (originalLeft - pin)) < .002f &&
                Vector3.Distance(V(doc.Parts[1].Position), pin + expected * (originalRight - pin)) < .002f &&
                doc.EditablePartSelectionCount == 2 && !doc.IsPartSelected("pin-occluder"),
                "Constrained rotation commit differs from the fixed-pin multi-selection preview " + axis);
            Frame(controller, input, empty, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, empty); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Position), originalLeft) < .0001f &&
                Vector3.Distance(V(doc.Parts[1].Position), originalRight) < .0001f,
                "Constrained multi-selection rotation did not undo atomically");
        }
        // Undo restored the group poses; remove only the explicit occlusion fixture.
        doc.SelectOnly("pin-occluder"); Require(doc.DeleteSelection(true), "Pin fixture cleanup failed");
        doc.SelectOnly("left"); Require(scene.TrySync(doc, out error), error); view.Bind(doc, true, null, null);
        Set(controller, "gizmoFamily", GizmoFamily.Points);
        Frame(controller, input, empty, 0, KeyCode.Z); yield return null;
        Frame(controller, input, empty); yield return null;
        Require(doc.Parts.Count == 4 && Field<GizmoAxis>(controller, "anchorConstraintAxis") == GizmoAxis.None,
            "Constrained pin fixture left a part or active axis behind");
    }

    private static IEnumerable TestPointFrames(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string output)
    {
        Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
        Set(controller, "gizmoFamily", GizmoFamily.Points);
        Frame(controller, input, Vector2.zero); yield return null;
        var gizmo = Field<TransformGizmoView>(scene, "gizmo");
        scene.ModelSpaceGizmo = true; scene.NativePointsOnly = true;
        Call(controller, "UpdateTransformContext");
        Require(!Field<bool>(controller, "showAllAnchors"), "Default point policy is not near-cursor");
        NativeClick(view, uiCamera, "ViewportSectionButton2"); yield return null;
        NativeClick(view, uiCamera, "GizmoSizing"); yield return null;
        Require(!scene.ModelSpaceGizmo && !gizmo.ModelSpaceSizing, "Size UI did not switch drawing and hit tests together");
        NativeClick(view, uiCamera, "GizmoSizing"); yield return null;
        Require(scene.ModelSpaceGizmo, "Size UI cannot restore default model size");
        scene.ModelSpaceGizmo = false; Call(controller, "UpdateTransformContext");
        view.HideViewportSettings();
        NativeClick(view, uiCamera, "PointTypes"); Frame(controller, input, Vector2.zero); yield return null;
        Require(!scene.NativePointsOnly, "Shared filter did not enable helpers");
        NativeClick(view, uiCamera, "PointTypes");
        NativeClick(view, uiCamera, "PointReveal"); Frame(controller, input, Vector2.zero); yield return null;
        Require(Field<bool>(controller, "showAllAnchors"), "Show-all header toggle failed");
        NativeClick(view, uiCamera, "PointReveal"); Frame(controller, input, Vector2.zero); yield return null;
        NativeClick(view, uiCamera, "PointTypes"); Frame(controller, input, Vector2.zero); yield return null;
        Vector3[] anchors = Field<Vector3[]>(controller, "gizmoAnchors");
        int first = Field<int>(controller, "gizmoNativeAnchorStart");
        Vector3 a = anchors[first], b = anchors[first + 1];
        Vector2 aMouse = scene.Camera.WorldToScreenPoint(a), bMouse = scene.Camera.WorldToScreenPoint(b);
        Frame(controller, input, aMouse); yield return null;
        Frame(controller, input, aMouse, 1); yield return null;
        Frame(controller, input, aMouse); yield return null;
        Require(Field<Vector3?>(controller, "selectedAnchorWorld") == a && !Field<bool>(controller, "keyboardPreview"),
            "Choosing A started a transform instead of selecting its pivot");
        Frame(controller, input, Vector2.zero); yield return null;
        int selected = Field<int>(controller, "selectedAnchorPoint");
        Require(Field<List<LineRenderer>>(gizmo, "anchorHandles")[selected].gameObject.activeSelf,
            "Selected A is not persistent away from cursor");
        NativeClick(view, uiCamera, "ViewportSectionButton3"); yield return null;
        Require(Field<Button>(view, "edgeButton").transform.IsChildOf(Field<GameObject[]>(view, "viewportSections")[3].transform),
            "Optional A/B still occupies the primary viewport toolbar");
        NativeClick(view, uiCamera, "GeometryPoints"); yield return null;
        Require(scene.ExperimentalGeometryPoints, "Geometry opt-in button is disconnected");
        NativeClick(view, uiCamera, "GeometryPoints"); yield return null;
        Require(!scene.ExperimentalGeometryPoints, "Geometry opt-out button is disconnected");
        NativeClick(view, uiCamera, "EdgeFrame"); view.HideViewportSettings(); yield return null;
        Frame(controller, input, aMouse, 1); Frame(controller, input, aMouse); yield return null;
        Require(!Field<Vector3?>(controller, "framePointWorld").HasValue && Field<bool>(controller, "pickingFramePoint"),
            "Coincident A/B created an invalid working axis");
        Frame(controller, input, bMouse); Frame(controller, input, bMouse, 1); yield return null;
        Frame(controller, input, bMouse); yield return null;
        Require(Field<Vector3?>(controller, "selectedAnchorWorld") == a &&
            Field<Vector3?>(controller, "framePointWorld") == b &&
            Field<object>(controller, "transformFrame").ToString() == "Edge", "Choosing B lost A or working frame");
        Require(Field<LineRenderer>(gizmo, "frameGuide").gameObject.activeSelf, "A/B guide was not drawn");
        Vector3 direction = (b-a).normalized;
        Frame(controller, input, aMouse, 0, KeyCode.R); Frame(controller, input, aMouse); yield return null;
        Frame(controller, input, aMouse, 0, KeyCode.X); Frame(controller, input, aMouse); yield return null;
        Require(Vector3.Dot(Field<Vector3>(controller, "dragAxisWorld"), direction) > .999f,
            "Edge X is not aligned to A→B");
        AssertFrameText("editor.view.frame_edge");
        Frame(controller, input, aMouse, 0, KeyCode.X); Frame(controller, input, aMouse); yield return null;
        Require(Field<object>(controller, "transformFrame").ToString() == "World" && !Field<bool>(controller, "localSpace"),
            "Repeated X switched maths but left the Edge header active");
        AssertFrameText("editor.view.frame_world");
        Frame(controller, input, aMouse, 0, KeyCode.X); Frame(controller, input, aMouse); yield return null;
        Require(Field<object>(controller, "transformFrame").ToString() == "Edge" &&
            Vector3.Dot(Field<Vector3>(controller, "dragAxisWorld"), direction) > .999f,
            "Repeated X cannot restore the frozen Edge frame");
        AssertFrameText("editor.view.frame_edge");
        Frame(controller, input, aMouse, 0, KeyCode.Escape); Frame(controller, input, aMouse); yield return null;
        Require(!Field<bool>(controller, "keyboardPreview") &&
            Field<Vector3?>(controller, "selectedAnchorWorld") == a && Field<Vector3?>(controller, "framePointWorld") == b,
            "Esc leaked the modal preview or lost its original A/B frame");
        Save(scene, view, uiCamera, target, Path.Combine(output, "input-point-frame.png"));
        NativeClick(view, uiCamera, "ViewportSectionButton3"); yield return null;
        NativeClick(view, uiCamera, "EdgeFrame"); view.HideViewportSettings(); Frame(controller, input, Vector2.zero); yield return null;
        Require(!Field<Vector3?>(controller, "framePointWorld").HasValue &&
            Field<object>(controller, "transformFrame").ToString() == "Local" &&
            !Field<LineRenderer>(gizmo, "frameGuide").gameObject.activeSelf, "Clearing B left an active edge frame");
        NativeClick(view, uiCamera, "Row_right"); yield return null;
        NativeClick(view, uiCamera, "Row_left");
        Frame(controller, input, Vector2.zero); yield return null;
        void AssertFrameText(string key)
        {
            string caption = BuildWorksLocalization.Text(key);
            Require(Field<Button>(view, "frameButton").GetComponentInChildren<TMP_Text>().text == caption &&
                Field<TMP_Text>(view, "nextActionText").text.Contains(caption),
                "Header and modal hint disagree with actual working frame: " + caption);
        }
    }

    private static IEnumerable TestViewDepth(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string output)
    {
        Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
        Set(controller, "gizmoFamily", GizmoFamily.Combined);
        Set(controller, "transformFrame", Enum.Parse(Field<object>(controller, "transformFrame").GetType(), "View"));
        Set(controller, "localSpace", true);
        Call(controller, "SetActiveTool", BlueprintEditorTool.Transform);
        Call(controller, "UpdateTransformContext");
        var gizmo = Field<TransformGizmoView>(scene, "gizmo");
        var doc = Field<BlueprintEditorDocument>(controller, "document");
        BlueprintEditorPart part = doc.Parts.First(p => p.StableId == "left");
        Vector3 original = V(part.Position);
        Frame(controller, input, Vector2.zero); yield return null;
        var handle = Field<LineRenderer>(gizmo, "depthHandle");
        Require(handle.gameObject.activeInHierarchy && gizmo.ViewDepthHandle, "View frame has no reachable depth handle");
        Vector3 center = (handle.GetPosition(0) + handle.GetPosition(2)) * .5f;
        Vector2 mouse = scene.Camera.WorldToScreenPoint(center);
        Require(gizmo.HitTestMove(scene.Camera, original, scene.Camera.transform.rotation, true, mouse) == GizmoAxis.Z,
            "Depth badge does not hit the actual camera Z axis");
        Frame(controller, input, mouse); yield return null;
        Save(scene, view, uiCamera, target, Path.Combine(output, "input-view-depth.png"));
        Frame(controller, input, mouse, 1); yield return null;
        Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Move &&
            Field<GizmoAxis>(controller, "dragAxis") == GizmoAxis.Z, "Clicking depth badge did not start camera Z movement");
        Frame(controller, input, mouse + Vector2.up * 50, 1); yield return null;
        Vector3 delta = Field<Vector3>(controller, "dragTranslation");
        Require(delta.magnitude > .01f && Vector3.Dot(delta.normalized, scene.Camera.transform.forward) > .999f &&
            Vector3.Distance(V(part.Position), original) < .0001f, "Depth drag moved sideways or changed document before confirmation");
        Frame(controller, input, mouse + Vector2.up * 50); yield return null;
        Require(Vector3.Distance(V(doc.Parts.First(p => p.StableId == "left").Position), original + delta) < .0001f,
            "Depth release did not commit the shown displacement");
        Frame(controller, input, mouse, 0, KeyCode.LeftControl, KeyCode.Z); Frame(controller, input, mouse); yield return null;
        Require(Vector3.Distance(V(doc.Parts.First(p => p.StableId == "left").Position), original) < .0001f,
            "Depth drag needs more than one Undo");
        Frame(controller, input, mouse, 0, KeyCode.G); Frame(controller, input, mouse); yield return null;
        Frame(controller, input, mouse, 0, KeyCode.Z); Frame(controller, input, mouse); yield return null;
        Frame(controller, input, mouse + Vector2.up * 50); yield return null;
        Require(Field<bool>(controller, "keyboardValid") && Field<Vector3>(controller, "dragTranslation").magnitude > .01f,
            "G Z remains immobile when the axis projects onto one pixel");
        input.Next(mouse + Vector2.up * 50); input.InputString = "1"; controller.Update(); controller.LateUpdate(); yield return null;
        Require(Vector3.Distance(Field<Vector3>(controller, "dragTranslation"), scene.Camera.transform.forward) < .0001f,
            "View-depth numeric input adds mouse preview instead of replacing it from the operation start");
        Frame(controller, input, mouse, 0, KeyCode.Escape); Frame(controller, input, mouse); yield return null;
        Require(!Field<bool>(controller, "keyboardPreview") &&
            Vector3.Distance(V(doc.Parts.First(p => p.StableId == "left").Position), original) < .0001f,
            "Depth Esc leaks a modal transform or changes the document");
        Set(controller, "transformFrame", Enum.Parse(Field<object>(controller, "transformFrame").GetType(), "Local"));
        Call(controller, "UpdateTransformContext"); Frame(controller, input, Vector2.zero); yield return null;
        Require(!handle.gameObject.activeSelf, "Depth badge leaked into a non-view frame");
    }

    private static Vector2 EmptyTransformPoint(BlueprintEditorController controller, BlueprintEditorScene scene,
        BlueprintEditorView view, Vector3 pivot)
    {
        Rect viewport = view.ViewportScreenRect();
        for (int y = 1; y < 5; ++y)
        for (int x = 1; x < 5; ++x)
        {
            Vector2 point = new Vector2(Mathf.Lerp(viewport.xMin, viewport.xMax, x / 5f),
                Mathf.Lerp(viewport.yMin, viewport.yMax, y / 5f));
            if (!scene.TryPick(point, out _) && !scene.HitTestScale(point) &&
                scene.HitTestAnchor(Field<Vector3[]>(controller, "gizmoAnchors"), point) < 0 &&
                scene.HitTestGizmo(BlueprintEditorTool.Transform, pivot, Quaternion.identity, true, point, out _) == GizmoHandleKind.None)
                return point;
        }
        throw new InvalidOperationException("Input frames: no empty viewport pixel outside transform handles");
    }

    private static IEnumerable TestArrayInput(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string output)
    {
        BlueprintEditorDocument doc = controller.Document;
        NativeClick(view, uiCamera, "Row_left"); yield return null;
        Vector2 mouse = scene.Camera.WorldToScreenPoint(V(doc.Parts[0].Position));
        Frame(controller, input, mouse); yield return null;
        NativeClick(view, uiCamera,
            Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        yield return null;
        Require(Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Transform &&
            Field<GameObject>(view, "inspectorEditPanel").activeSelf,
            "Array fixture did not open the Transform inspector");
        TMP_InputField sourceScale = Field<TMP_InputField[]>(view, "inspectorScale")[0];
        sourceScale.text = "200"; sourceScale.onEndEdit.Invoke(sourceScale.text);
        Frame(controller, input, mouse); yield return null;
        Require(Math.Abs(doc.Parts[0].Scale.X - 2) < .00001, "Array fixture did not establish 2m native span");
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Array]);
        Frame(controller, input, mouse); yield return null;
        Require(Field<RepeatDistributionMode>(controller, "arrayDistribution") == RepeatDistributionMode.Pack,
            "Fresh Array session did not start in predictable Pack mode");
        NativeClick(view, uiCamera, "ArrayDistribution"); yield return null;
        Require(Field<RepeatDistributionMode>(controller, "arrayDistribution") == RepeatDistributionMode.Fit,
            "Array reset fixture did not enter Fit mode");
        NativeClick(view, uiCamera,
            Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        NativeClick(view, uiCamera,
            Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Array]);
        Frame(controller, input, mouse); yield return null;
        Require(Field<RepeatDistributionMode>(controller, "arrayDistribution") == RepeatDistributionMode.Pack &&
            Field<int>(controller, "arrayCountX") == 2 && Field<int>(controller, "arrayCountY") == 1 &&
            !Field<bool>(controller, "arraySymmetric") &&
            Mathf.Abs(Field<float>(controller, "arrayRise")) < .0001f,
            "Fresh Array session inherited the previous distribution or profile");
        Require(Field<int>(controller, "arrayPreviewCount") == 0 &&
            Field<TMP_Text>(view, "arrayInfo").text ==
                BuildWorksLocalization.Text("editor.view.array_direction"),
            "Array has no explicit pending-direction state");
        foreach (GizmoAxis axis in new[] { GizmoAxis.X, GizmoAxis.Z })
        {
            Vector2 start = LayoutHandle(controller, scene, axis);
            Frame(controller, input, start, 1); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Layout &&
                Field<GizmoAxis>(controller, "dragAxis") == axis, "Golden arrow pointer did not begin layout " + axis);
            int count = axis == GizmoAxis.X ? 3 : 2;
            float pixels = (count - 1) * Field<float>(controller, "dragArrayStepLength") /
                Field<float>(controller, "dragWorldUnitsPerPixel");
            Vector2 end = start + Field<Vector2>(controller, "dragScreenDirection") * pixels;
            Frame(controller, input, end, 1); yield return null;
            Frame(controller, input, end); yield return null;
            mouse = scene.Camera.WorldToScreenPoint(V(doc.Parts[0].Position));
            float distance = Field<float>(controller, "cameraDistance");
            float packStep = Field<Vector3>(controller,
                axis == GizmoAxis.X ? "arrayStepX" : "arrayStepY").magnitude;
            input.Next(mouse); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Require(Field<int>(controller, axis == GizmoAxis.X ? "arrayCountX" : "arrayCountY") == count + 1 &&
                Mathf.Abs(distance - Field<float>(controller, "cameraDistance")) < .00001f &&
                Mathf.Abs(packStep - Field<Vector3>(controller,
                    axis == GizmoAxis.X ? "arrayStepX" : "arrayStepY").magnitude) < .0001f,
                "Pack wheel did not extend the row at a fixed natural step " + axis);
            input.Next(mouse, 0, KeyCode.LeftControl); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Require(Field<int>(controller, axis == GizmoAxis.X ? "arrayCountX" : "arrayCountY") == count + 1 &&
                Field<float>(controller, "cameraDistance") < distance, "Ctrl+wheel did not zoom exclusively");
            input.Next(mouse); input.MouseScrollDelta = Vector2.down;
            controller.Update(); controller.LateUpdate(); yield return null;
        }
        Frame(controller, input, mouse); yield return null;
        Require(Field<int>(controller, "arrayCountX") == 3 && Field<int>(controller, "arrayCountY") == 2,
            "Second-direction wheel changed first count");
        NativeClick(view, uiCamera, "ArrayDistribution"); yield return null;
        Vector2 shortStart = LayoutHandle(controller, scene, GizmoAxis.Z);
        Frame(controller, input, shortStart, 1); yield return null;
        Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Layout &&
            Field<float>(controller, "dragArrayStepLength") > 1.99f, "Short Fit precondition: second arrow / 2m span");
        Vector2 shortEnd = shortStart + Field<Vector2>(controller, "dragScreenDirection") *
            (.2f / Field<float>(controller, "dragWorldUnitsPerPixel"));
        Frame(controller, input, shortEnd, 1); yield return null;
        Frame(controller, input, shortEnd); yield return null;
        Require(Mathf.Abs(Field<float>(controller, "arrayDistanceY") - .2f) < .0001f &&
            Mathf.Abs(Field<Vector3>(controller, "arrayStepY").magnitude - .2f) < .0001f &&
            Field<int>(controller, "arrayCountY") == 2,
            "Fit second arrow rejected 0.2m drag because of the 2m source span");
        float fitSpan = Field<Vector3>(controller, "arrayStepY").magnitude * (Field<int>(controller, "arrayCountY") - 1);
        input.Next(mouse); input.MouseScrollDelta = Vector2.up;
        controller.Update(); controller.LateUpdate(); yield return null;
        Require(Mathf.Abs(Field<Vector3>(controller, "arrayStepY").magnitude *
            (Field<int>(controller, "arrayCountY") - 1) - fitSpan) < .0001f, "Fit wheel changed dragged span");
        NativeClick(view, uiCamera, "ArrayDistribution"); yield return null;
        Require(Mathf.Abs(Field<Vector3>(controller, "arrayStepX").magnitude - .25f) < .0001,
            "Exact spacing preset unavailable");
        NativeClick(view, uiCamera, "ArrayStep"); yield return null;
        Require(Mathf.Abs(Field<Vector3>(controller, "arrayStepX").magnitude - .5f) < .0001,
            "Exact spacing button failed");
        NativeClick(view, uiCamera, "ArrayDistribution"); yield return null;
        NativeClick(view, uiCamera, "ArrayStep"); yield return null;
        string stepPrefix = BuildWorksLocalization.Text(
            "editor.array_step_info", string.Empty, string.Empty).Split(':')[0] + ":";
        Require(Field<TMP_Text>(view, "arrayInfo").text.StartsWith(stepPrefix) &&
            (VisibleButtonText(view, "ArrayStep").Contains("0,01") ||
            VisibleButtonText(view, "ArrayStep").Contains("0.01")),
            "Current spacing is hidden on or below the spacing button");
        NativeClick(view, uiCamera, "ArrayMoveStep"); yield return null;
        NativeClick(view, uiCamera, "ArrayAngleStep"); yield return null;
        string moveStepLabel = VisibleButtonText(view, "ArrayMoveStep");
        Require(Mathf.Abs(Field<float>(controller, "translationStep") - .1f) < .0001f &&
            Mathf.Abs(Field<float>(controller, "rotationStep") - 5f) < .0001f &&
            (moveStepLabel.Contains("0,1") || moveStepLabel.Contains("0.1")) &&
            VisibleButtonText(view, "ArrayAngleStep").Contains("5"),
            "Array rise/angle scrub presets do not match the shared F9 values");
        TMP_InputField liveRiseField = Field<TMP_InputField>(view, "arrayRise");
        liveRiseField.SetTextWithoutNotify("0");
        IList liveBefore = Field<IList>(scene, "contourPreviews");
        var liveBeforePositions = new List<Vector3>();
        foreach (object preview in liveBefore)
            liveBeforePositions.Add(Field<GameObject>(preview, "Root").transform.position);
        int partsBeforeLiveScrub = doc.Parts.Count;
        var liveScrubPointer = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left, eligibleForClick = true, position = new Vector2(100,100) };
        BlueprintEditorNumericScrub liveScrub = liveRiseField.GetComponent<BlueprintEditorNumericScrub>();
        liveScrub.OnBeginDrag(liveScrubPointer);
        liveScrubPointer.position += Vector2.right;
        liveScrub.OnDrag(liveScrubPointer);
        controller.GetType().GetMethod("RefreshContextHints", Private).Invoke(controller, null);
        Require(view.IsNumericScrubbing &&
            Field<TMP_Text>(view, "statusHintText").text ==
                BuildWorksLocalization.Text("editor.hint.numeric_scrub"),
            "Numeric scrub does not replace unavailable tool shortcuts in the footer");
        IList liveAfter = Field<IList>(scene, "contourPreviews");
        bool movedBeforeRelease = liveAfter.Count > 0 && liveAfter.Count == liveBeforePositions.Count;
        for (int index = 0; movedBeforeRelease && index < liveAfter.Count; ++index)
            if (Vector3.Distance(Field<GameObject>(liveAfter[index], "Root").transform.position,
                liveBeforePositions[index]) > .001f) { movedBeforeRelease = true; break; }
            else if (index == liveAfter.Count - 1) movedBeforeRelease = false;
        Require(Mathf.Abs(Field<float>(controller, "arrayRise") - .1f) < .0001f && movedBeforeRelease &&
            doc.Parts.Count == partsBeforeLiveScrub,
            "Array numeric scrub did not rebuild the visible ghost live before release or mutated the document");
        liveScrub.OnEndDrag(liveScrubPointer);
        Require(!view.IsNumericScrubbing, "Numeric scrub remains active after drag release");
        TMP_InputField fractionalScale = Field<TMP_InputField>(view, "arrayScaleStepX");
        fractionalScale.text = "0.1"; fractionalScale.onEndEdit.Invoke(fractionalScale.text);
        Require(Mathf.Abs(Field<float>(controller, "arrayScaleStepX") - .001f) < .000001f &&
            Field<int>(controller, "arrayPreviewCount") > 0, "Fractional 0.1% Array step is rejected by actual TMP callback");
        string[] names = { "arrayRise", "arrayRotation", "arrayPitch", "arrayRoll", "arrayScaleStepX" };
        string[] values = { "0.05", "20", "15", "-10", "10" };
        string[] controllerNames = { "arrayRise", "arrayRotation", "arrayPitch", "arrayRoll", "arrayScaleStepX" };
        float[] expected = { .05f,20f,15f,-10f,.1f };
        for (int index = 0; index < names.Length; ++index)
        {
            TMP_InputField field = Field<TMP_InputField>(view, names[index]);
            field.text = values[index]; field.onEndEdit.Invoke(field.text);
            Require(Mathf.Abs(Field<float>(controller, controllerNames[index]) - expected[index]) < .00001f,
                "Actual TMP callback lost " + names[index]);
            Require(Field<int>(controller, "arrayPreviewCount") > 0, "Parameter broke preview: " + names[index]);
        }
        NativeClick(view, uiCamera, "ArraySymmetry"); yield return null;
        Require(Field<bool>(controller, "arraySymmetric"), "Symmetry button did not apply");
        EventSystem.current.SetSelectedGameObject(null);
        IList previews = Field<IList>(scene, "contourPreviews");
        var poses = new List<Vector3>(); var rotations = new List<Quaternion>(); var scales = new List<Vector3>();
        foreach (object preview in previews)
        {
            Transform transform = Field<GameObject>(preview, "Root").transform;
            poses.Add(transform.position); rotations.Add(transform.rotation); scales.Add(transform.localScale);
        }
        Require(poses.Count == 8 && scales[0].x > scales[1].x, "Symmetric uniform scale profile did not reach preview");
        Require(Vector3.Distance(poses[3] - poses[0], Field<Vector3>(controller,"arrayStepY")) < .001f &&
            Quaternion.Angle(rotations[3], rotations[0]) < .001f && Vector3.Distance(scales[3],scales[0]) < .001f,
            "Second row changes shape profile instead of translating it");
        Save(scene, view, uiCamera, target, Path.Combine(output, "input-array-profile.png"));
        int before = doc.Parts.Count;
        NativeClick(view, uiCamera, "ApplyArray"); yield return null;
        Require(doc.Parts.Count == before + poses.Count, "Apply lost planned Array copies");
        for (int index = 0; index < poses.Count; ++index)
        {
            BlueprintEditorPart part = doc.Parts[before + index];
            Require(Vector3.Distance(V(part.Position), poses[index]) < .001f &&
                Vector3.Distance(V(part.Scale), scales[index]) < .001f &&
                Quaternion.Angle(new Quaternion((float)part.Rotation.X,(float)part.Rotation.Y,
                    (float)part.Rotation.Z,(float)part.Rotation.W), rotations[index]) < .01f,
                "Array commit differs from visual preview " + index);
        }
        Frame(controller,input,mouse,0,KeyCode.LeftControl,KeyCode.Z); yield return null;
        Require(doc.Parts.Count == before, "Array profile is not one Undo");
        Frame(controller,input,mouse); yield return null;
        Frame(controller,input,mouse,0,KeyCode.LeftControl,KeyCode.Z); yield return null;
        Require(Math.Abs(doc.Parts[0].Scale.X - 1.3) < .00001, "Array fixture source scale did not restore");
        Frame(controller,input,mouse); yield return null;
    }

    private static Vector2 LayoutHandle(BlueprintEditorController controller, BlueprintEditorScene scene, GizmoAxis axis)
    {
        LineRenderer line = Field<LineRenderer[]>(Field<TransformGizmoView>(scene, "gizmo"),
            "layoutAxisLines")[(int)axis - 1];
        Vector3 pivot = V(controller.Document.Parts[0].Position);
        int freeSamples = 0, matchingSamples = 0;
        for (int index = 40; index >= 0; --index)
        {
            Vector2 point = scene.Camera.WorldToScreenPoint(Vector3.Lerp(line.GetPosition(0), line.GetPosition(1), index / 40f));
            bool free = scene.HitTestAnchor(Field<Vector3[]>(controller, "gizmoAnchors"), point) < 0 &&
                !scene.HitTestScale(point);
            if (free) ++freeSamples;
            bool matching = scene.HitTestGizmo(BlueprintEditorTool.Array, pivot, Quaternion.identity,
                Field<bool>(controller,"localSpace"), point, out GizmoAxis hit) == GizmoHandleKind.Layout && hit == axis;
            if (matching) ++matchingSamples;
            if (free && matching) return point;
        }
        throw new InvalidOperationException("Input frames: no golden arrow pixel outside native anchor glyphs: " +
            axis + " (free " + freeSamples + ", matching " + matchingSamples + ")");
    }

    private static void TestPrimaryGroupGizmo(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, BlueprintEditorDocument doc)
    {
        Vector3 left = V(doc.Parts[0].Position), right = V(doc.Parts[1].Position);
        const string outside = "primary-frame-outside";
        doc.AddPart(new BlueprintEditorPart(outside, "fixture", "Outside",
            new Point3(0, .5, 4), new Rotation3(0, 0, 0, 1), scale: new Point3(3, 1, 1)));
        doc.SelectOnly("left"); doc.ToggleSelection(outside);
        Require(scene.TrySync(doc, out string error), error);
        var boundsIds = new List<string> { "left", outside };
        Require(scene.TryGetBounds(boundsIds, out Bounds selectionBounds),
            "Bounds-pivot fixture has no visual bounds");
        object[] boundsArgs = { null, null, new List<string>() };
        Require((bool)controller.GetType().GetMethod("TrySelectionBasis", Private)
            .Invoke(controller, boundsArgs) &&
            Vector3.Distance((Vector3)boundsArgs[0], selectionBounds.center) < .0001f,
            "Fallback selection pivot is not the center of visible bounds");
        doc.SelectOnly("left"); doc.ToggleSelection("right");
        Require(doc.CreateGroup("primary-group", "Primary group") &&
            doc.SetPrimaryGroup("primary-group"), "Primary-group fixture setup failed");
        doc.ToggleSelection(outside);
        Require(scene.TrySync(doc, out error), error);
        view.Bind(doc, true, null, null);
        controller.GetType().GetMethod("SetActiveTool", Private)
            .Invoke(controller, new object[] { BlueprintEditorTool.Transform });
        var ids = new List<string>();
        object[] pivotArgs = { null, null, ids };
        Require((bool)controller.GetType().GetMethod("TrySelectionPivot", Private)
            .Invoke(controller, pivotArgs), "Primary-group pivot was unavailable");
        Require(ids.Contains("left") && ids.Contains("right") && ids.Contains(outside),
            "Primary-group selection did not retain its group and outside transform members");
        controller.GetType().GetMethod("UsePrimaryGizmoFrame", Private)
            .Invoke(controller, new object[] { ids });
        Require(ids.Count == 2 && !ids.Contains(outside),
            "Primary-group gizmo included a selected part outside its declared frame");
        doc.SelectOnly("primary-group");
        Require(doc.SetPrimaryPart(outside) && doc.SetGroupPivot("primary-group", "left"),
            "Independent world/group anchor fixture setup failed");
        ids.Clear();
        pivotArgs = new object[] { null, null, ids };
        Require((bool)controller.GetType().GetMethod("TrySelectionPivot", Private)
            .Invoke(controller, pivotArgs) &&
            Vector3.Distance((Vector3)pivotArgs[0], left) < .0001f && ids.Count == 2,
            "Selected group did not anchor its pivot to its own part or lost transform members");
        controller.GetType().GetMethod("UsePrimaryGizmoFrame", Private)
            .Invoke(controller, new object[] { ids });
        Require(ids.Count == 1 && ids[0] == "left",
            "Group pivot did not isolate its own gizmo frame");
        Require(controller.ApplyTransformDelta(Vector3.up, Quaternion.identity, left),
            "Primary-group transform was rejected");
        Require(Vector3.Distance(V(doc.Parts[0].Position), left + Vector3.up) < .0001f &&
            Vector3.Distance(V(doc.Parts[1].Position), right + Vector3.up) < .0001f,
            "Group-pivot gizmo did not transform the whole selected group");
        Require(doc.PrimaryPartId == outside,
            "Group transform changed the independent world anchor");
        Require(doc.CreateEmptyGroup("secondary-group", "Secondary group"),
            "Secondary group fixture setup failed");
        doc.SelectOnly(outside);
        Require(doc.SetSelectionGroup("secondary-group") &&
            doc.SetGroupPivot("secondary-group", outside) && doc.SetPrimaryPart("left"),
            "Multi-group world-anchor fixture setup failed");
        doc.SelectOnly("primary-group");
        doc.ToggleSelection("secondary-group");
        ids.Clear();
        pivotArgs = new object[] { null, null, ids };
        Require((bool)controller.GetType().GetMethod("TrySelectionPivot", Private)
            .Invoke(controller, pivotArgs) &&
            Vector3.Distance((Vector3)pivotArgs[0], left + Vector3.up) < .0001f && ids.Count == 3,
            "Last selected group pivot overrode the selected world anchor");
        controller.GetType().GetMethod("UsePrimaryGizmoFrame", Private)
            .Invoke(controller, new object[] { ids });
        Require(ids.Count == 1 && ids[0] == "left",
            "Multi-group gizmo frame did not isolate the world anchor");
        input.Next(Vector2.zero, 0, KeyCode.N);
        ids.Clear();
        pivotArgs = new object[] { null, null, ids };
        Require((bool)controller.GetType().GetMethod("TrySelectionPivot", Private)
            .Invoke(controller, pivotArgs) &&
            Vector3.Distance((Vector3)pivotArgs[0], left + Vector3.up) > .1f && ids.Count == 3,
            "N did not temporarily ignore the world anchor for the selected frame");
        controller.GetType().GetMethod("UsePrimaryGizmoFrame", Private)
            .Invoke(controller, new object[] { ids });
        Require(ids.Count == 3, "N still reduced anchors to the declared world frame");
        input.Next(Vector2.zero);
        Require(doc.Undo() && doc.Undo() && doc.Undo() && doc.Undo(),
            "Multi-group world-anchor fixture could not restore document history");
        Require(doc.Undo() && doc.Undo() && doc.Undo() && doc.Undo() && doc.Undo() && doc.Undo(),
            "Primary-group fixture could not restore document history");
        doc.SelectOnly("left");
        Require(scene.TrySync(doc, out error), error);
        view.Bind(doc, true, null, null);
    }

    private static object Call(object target, string name, params object[] arguments) =>
        target.GetType().GetMethod(name, Private).Invoke(target, arguments);

    private static Vector2 MoveHandle(BlueprintEditorScene scene, Vector3 pivot, GizmoAxis axis) =>
        scene.Camera.WorldToScreenPoint(Field<LineRenderer[]>(Field<TransformGizmoView>(scene, "gizmo"), "moveLines")[(int)axis - 1].GetPosition(1));

    private static GameObject VisualRoot(BlueprintEditorScene scene, string id) =>
        Field<GameObject>(Field<IDictionary>(scene, "visuals")[id], "Root");

    private static bool IsPainted(BlueprintEditorScene scene, string id)
    {
        foreach (Renderer renderer in Field<Renderer[]>(Field<IDictionary>(scene, "visuals")[id], "Renderers"))
            if (renderer.HasPropertyBlock()) return true;
        return false;
    }

    private static Vector2 ClearSurfacePoint(BlueprintEditorController controller, BlueprintEditorScene scene,
        string id, Vector2 center, Vector3 selectedPivot)
    {
        for (int y = -40; y <= 40; y += 10)
        for (int x = -40; x <= 40; x += 10)
        {
            Vector2 point = center + new Vector2(x,y);
            if (scene.TryPick(point, out string hit) && hit == id &&
                scene.HitTestAnchor(Field<Vector3[]>(controller, "gizmoAnchors"), point) < 0 &&
                scene.HitTestGizmo(BlueprintEditorTool.Transform, selectedPivot, Quaternion.identity, true,
                    point, out _) == GizmoHandleKind.None) return point;
        }
        throw new InvalidOperationException("Input frames: inserted mesh has no selectable surface pixel outside the visible gizmo");
    }

    internal static void NativeClick(BlueprintEditorView view, Camera uiCamera, string name, bool verifyRaycast = true)
    {
        Button expected = null;
        foreach (Button button in view.RootCanvas.GetComponentsInChildren<Button>())
            if (button.name == name) { expected = button; break; }
        Require(expected, "Visible button missing: " + name);
        NativeClick(view, uiCamera, expected, verifyRaycast);
    }

    private static string VisibleButtonText(BlueprintEditorView view, string name)
    {
        foreach (Button button in view.RootCanvas.GetComponentsInChildren<Button>())
            if (button.name == name) return button.GetComponentInChildren<TMP_Text>().text;
        throw new InvalidOperationException("Visible button missing: " + name);
    }

    private static void NativeClick(BlueprintEditorView view, Camera uiCamera, Button expected, bool verifyRaycast = true)
    {
        Canvas.ForceUpdateCanvases();
        if (view.RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) uiCamera = null;
        string name = expected.name;
        var rect = (RectTransform)expected.transform;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, position, uiCamera, out Vector2 diagnosticLocal);
        var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
        GameObject target = expected.gameObject;
        if (verifyRaycast)
        {
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "No actual UI raycast at " + name + "; position=" + position +
                "; screen=" + Screen.width + "x" + Screen.height + "; active=" + expected.gameObject.activeInHierarchy +
                "; graphicDepth=" + expected.GetComponent<Graphic>()?.depth + "; culled=" + expected.GetComponent<Graphic>()?.canvasRenderer.cull);
            target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if (target != expected.gameObject)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Debug.Log("Raycast diagnostic " + name + "; Screen=" + Screen.width + "x" + Screen.height +
                    "; camera=" + (uiCamera ? uiCamera.pixelRect + "/" + uiCamera.aspect + "/" + uiCamera.targetTexture : "overlay") +
                    "; corners=" + string.Join(";", Array.ConvertAll(corners, point => point.ToString("F7"))) +
                    "; screens=" + string.Join(";", Array.ConvertAll(corners, point => RectTransformUtility.WorldToScreenPoint(uiCamera, point).ToString("F4"))));
            }
            Require(target == expected.gameObject, "UI raycast intercepted " + name + " by " + (target ? target.name : "null") +
                "; first=" + hits[0].gameObject.name + "; position=" + position +
                "; active=" + expected.gameObject.activeInHierarchy + "; interactable=" + expected.IsInteractable() +
                "; rect=" + rect.rect + "; graphic=" + expected.targetGraphic.raycastTarget + "/" + expected.targetGraphic.depth +
                "/" + expected.targetGraphic.canvasRenderer.cull + "; layer=" + expected.gameObject.layer +
                "; contains=" + RectTransformUtility.RectangleContainsScreenPoint(rect, position, uiCamera) +
                "; accepts=" + expected.targetGraphic.Raycast(position, uiCamera) + "; canvas=" + expected.targetGraphic.canvas.name +
                "; pose=" + rect.position.ToString("F7") + "/" + rect.lossyScale.ToString("F7") + "/" + rect.rotation +
                "; local=" + rect.localScale.ToString("F7") + "; parent=" + rect.parent.localScale.ToString("F7") +
                "; localHit=" + diagnosticLocal.ToString("F4") + "; mode=" + view.RootCanvas.renderMode +
                "; hits=" + string.Join(",", hits.ConvertAll(hit => hit.gameObject.name)));
        }
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        EventSystem.current.SetSelectedGameObject(null);
    }

    private static void Save(BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string path)
    {
        foreach (Transform child in view.RootCanvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases(); scene.Camera.Render(); uiCamera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height), 0,0); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.Destroy(texture); RenderTexture.active = previous;
    }
}
