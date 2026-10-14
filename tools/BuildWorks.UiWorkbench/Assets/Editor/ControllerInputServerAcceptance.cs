using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static partial class ControllerInputAcceptance
{
    internal static IEnumerable<object> RunServerLibrary(Camera camera, TMP_Text font, GameObject wall, string output)
    {
        string folder = Path.Combine(output, "server-ui-" + Guid.NewGuid().ToString("N"));
        var store = new CompositeBlueprintStore(Path.Combine(folder, "personal.json"));
        var part = new CompositeBlueprintStore.Part { stableId = "a", prefabName = "woodwall", displayName = "Saved wall",
            rotation = new CompositeBlueprintStore.QuaternionData { w = 1 }, scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
            baseScale = new CompositeBlueprintStore.VectorData(wall.transform.localScale) };
        Require(store.TrySaveDocument("Server QA", "QA", new[] { part }, Array.Empty<CompositeBlueprintStore.Group>(),
            Array.Empty<CompositeBlueprintStore.VectorData>(), "a", null, out var source, out string error), error);
        object context = new object(), connection = new object(); bool admin = true;
        store.ConfigureImportScale(name => name == "woodwall" ? new CompositeBlueprintStore.VectorData(wall.transform.localScale) : (CompositeBlueprintStore.VectorData?)null,
            () => context, name => name.GetStableHashCode());
        BlueprintServerEndpoint endpoint = null; BlueprintServerClient client = null; var input = new InputFrames();
        var library = new ServerBlueprintLibrary(Path.Combine(folder, "server"), principal => endpoint.IsAdministrator(principal));
        using (endpoint = new BlueprintServerEndpoint(library, 971, _ => ("test-admin", admin), (_, bytes) => client.Receive(bytes, Time.realtimeSinceStartupAsDouble)))
        using (client = new BlueprintServerClient(971, bytes => endpoint.Receive(connection, bytes, Time.realtimeSinceStartupAsDouble), "test-server"))
        using (var controller = new BlueprintEditorController(store, camera, font, name => name == "woodwall" ? wall : null,
            name => name, () => new[] { new BlueprintEditorCatalogItem("woodwall", "Wall", null, "QA", "WOOD", "Valheim", 0),
                new BlueprintEditorCatalogItem("blueprint_" + source.id, source.name, null, "QA", "Blueprints", "Local", 1, source) },
            message => { throw new InvalidOperationException(message); }, editorInput: input))
        {
            BuildWorksPlugin.ServerLibraryClient = client;
            Require(controller.OpenExisting(source, out error), error); yield return null;
            Require(controller.Document.Rename("a", "Unsaved draft"), "Cannot prepare server UI dirty draft");
            Call(controller, "Bind", (object)null);
            int revision = controller.Document.RevisionToken;
            var view = Field<BlueprintEditorView>(controller, "view");
            for (int i = 0; i < 300 && endpoint.HasPendingWork; ++i) { endpoint.Tick(Time.realtimeSinceStartupAsDouble); yield return null; }
            Require(!endpoint.HasPendingWork, "Server initialization did not finish");

            Call(controller, "RequestCatalog"); yield return null;
            NativeClick(view, null, Field<Button>(view, "catalogBlueprintsModeButton")); yield return null;
            NativeClick(view, null, "CatalogServer"); yield return null;
            var exchange = Field<BlueprintExchangeView>(view, "exchangeView");
            Require(exchange != null && Field<bool>(exchange, "serverMode") && view.HasModal, "Editor server entry did not open the shared modal");
            NativeClick(view, null, "ExchangeClose"); yield return null;
            Require(controller.Document.IsDirty && controller.Document.RevisionToken == revision && controller.Document.SourceBlueprintId == source.id,
                "Opening/closing server list changed draft/history/source");

            CompositeBlueprintStore.Blueprint[] accepted = null;
            view.ShowExchange((parent, textFont, closed) => BlueprintExchangeView.ForServer(parent, textFont, store, source, () => client, closed,
                copies => accepted = copies)); yield return null;
            exchange = Field<BlueprintExchangeView>(view, "exchangeView");
            Require(Field<Button>(exchange, "serverPublish").interactable && Field<int>(exchange, "serverTotal") == 0, "Admin empty list has no publication action");
            NativeClick(view, null, "ServerPublish"); yield return null;
            for (int i = 0; i < 300 && Field<object>(exchange, "serverPublication") != null; ++i) { Pump(); yield return null; }
            Require(Field<byte[]>(exchange, "serverPreparedBytes") != null && !Directory.Exists(Path.Combine(folder, "server")), "Publication preparation wrote server files");
            NativeClick(view, null, "ServerCancel"); yield return null;
            Require(!Directory.Exists(Path.Combine(folder, "server")), "Cancelled review published a server file");
            NativeClick(view, null, "ServerPublish"); yield return null;
            for (int i = 0; i < 300 && Field<object>(exchange, "serverPublication") != null; ++i) { Pump(); yield return null; }
            NativeClick(view, null, "ServerAccept"); yield return null;
            for (int i = 0; i < 300 && (Field<int>(exchange, "serverTotal") != 1 || Field<object>(exchange, "serverTask") != null); ++i) { Pump(); yield return null; }
            Require(Field<int>(exchange, "serverTotal") == 1 && Directory.GetFiles(Path.Combine(folder, "server"), "*.bwserver.zip").Length == 1,
                "Confirmed publication did not return one server entry");
            // Dynamically created Graphics join the native raycast registry after rendering.
            yield return null;

            NativeClick(view, null, "ServerEntry0"); yield return null;
            NativeClick(view, null, "ServerUpdate"); yield return null;
            for (int i = 0; i < 300 && Field<object>(exchange, "serverPublication") != null; ++i) { Pump(); yield return null; }
            admin = false; NativeClick(view, null, "ServerAccept"); yield return null;
            for (int i = 0; i < 100 && Field<object>(exchange, "serverTask") != null; ++i) { Pump(); yield return null; }
            Require(Field<TMP_Text>(exchange, "serverStatus").text == BuildWorksLocalization.Text("server.admin_required") &&
                !Field<Button>(exchange, "serverPublish").gameObject.activeSelf, "Current role revocation did not reject mutation/hide admin controls");
            NativeClick(view, null, "ServerRefresh"); yield return null; Pump(); yield return null;
            NativeClick(view, null, "ServerEntry0"); yield return null;
            NativeClick(view, null, "ServerAccept"); yield return null;
            for (int i = 0; i < 300 && (Field<object>(exchange, "serverTask") != null || Field<object>(exchange, "serverDecode") != null || Field<object>(exchange, "serverHostPreparation") != null); ++i) { Pump(); yield return null; }
            Require(Field<object>(exchange, "serverPrepared") != null && store.All().Count == 1, "Receiving a file saved a card before confirmation");
            NativeClick(view, null, "ServerCancel"); yield return null;
            Require(store.All().Count == 1, "Cancelled personal-copy review saved a card");
            NativeClick(view, null, "ServerAccept"); yield return null;
            for (int i = 0; i < 300 && (Field<object>(exchange, "serverTask") != null || Field<object>(exchange, "serverDecode") != null || Field<object>(exchange, "serverHostPreparation") != null); ++i) { Pump(); yield return null; }
            NativeClick(view, null, "ServerAccept"); yield return null;
            Require(store.All().Count == 2, "Confirmed personal-copy acceptance did not save one card");
            var copy = store.All().Single(document => document.id != source.id);
            Require(copy.libraryOrigins.Single().server == "test-server" && copy.libraryOrigins[0].worldUid == 971 &&
                copy.libraryOrigins[0].documentId != source.id && copy.libraryOrigins[0].revision == 1 &&
                copy.parts[0].displayName == "Saved wall" && controller.Document.Parts[0].DisplayName == "Unsaved draft" &&
                controller.Document.IsDirty && controller.Document.RevisionToken == revision && controller.Document.SourceBlueprintId == source.id,
                "Accepted copy lost verified provenance, published dirty draft, or rebound editor/history");
            NativeClick(view, null, "ServerAccept"); yield return null;
            Require(!view.HasModal && accepted?.Single().id == copy.id, "Return to catalog lost acceptance receipt or input ownership");
            admin = true;
            view.ShowExchange((parent, textFont, closed) => BlueprintExchangeView.ForServer(parent, textFont, store, source, () => client, closed));
            yield return null;
            exchange = Field<BlueprintExchangeView>(view, "exchangeView");
            NativeClick(view, null, "ServerEntry0"); yield return null;
            NativeClick(view, null, "ServerUpdate"); yield return null;
            for (int i = 0; i < 300 && Field<object>(exchange, "serverPublication") != null; ++i) { Pump(); yield return null; }
            NativeClick(view, null, "ServerAccept"); yield return null;
            for (int i = 0; i < 300 && Field<object>(exchange, "serverTask") != null; ++i) { Pump(); yield return null; }
            yield return null;
            Require(Field<BlueprintServerProtocol.Item[]>(exchange, "serverItems").Single().Revision == 2, "Admin update did not advance revision");
            NativeClick(view, null, "ServerEntry0"); yield return null;
            NativeClick(view, null, "ServerDelete"); yield return null;
            NativeClick(view, null, "ServerCancel"); yield return null;
            Require(Directory.GetFiles(Path.Combine(folder, "server"), "*.bwserver.zip").Length == 1, "Cancelled delete removed the server document");
            NativeClick(view, null, "ServerDelete"); yield return null;
            NativeClick(view, null, "ServerAccept"); yield return null;
            for (int i = 0; i < 300 && Field<object>(exchange, "serverTask") != null; ++i) { Pump(); yield return null; }
            Require(Field<int>(exchange, "serverTotal") == 0 && Directory.GetFiles(Path.Combine(folder, "server"), "*.bwserver.zip").Length == 0 && store.All().Count == 2,
                "Confirmed server delete did not preserve personal copies or refresh list");
            NativeClick(view, null, "ExchangeClose"); yield return null;
            controller.Close(true);

            void Pump()
            {
                client.Tick(Time.realtimeSinceStartupAsDouble); endpoint.Tick(Time.realtimeSinceStartupAsDouble);
                Frame(controller, input, Vector2.zero);
            }
        }
        BuildWorksPlugin.ServerLibraryClient = null;
    }

    internal static IEnumerable<object> RunOriginMerge(Camera camera, TMP_Text font, GameObject wall, string output)
    {
        string path = Path.Combine(output, "origin-controller-" + Guid.NewGuid().ToString("N"), "library.json");
        var store = new CompositeBlueprintStore(path); object context = new object();
        store.ConfigureImportScale(name => name == "woodwall" ? new CompositeBlueprintStore.VectorData(wall.transform.localScale) : (CompositeBlueprintStore.VectorData?)null,
            () => context, name => name.GetStableHashCode());
        var part = new CompositeBlueprintStore.Part { stableId = "a", prefabName = "woodwall", displayName = "Wall",
            rotation = new CompositeBlueprintStore.QuaternionData { w = 1 }, scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
            baseScale = new CompositeBlueprintStore.VectorData(wall.transform.localScale) };
        var a = new CompositeBlueprintStore.LibraryOrigin("server", 71, "0123456789abcdef0123456789abcdef", 1, 0, "admin", new string('A', 64), 12);
        var b = new CompositeBlueprintStore.LibraryOrigin("server", 71, a.documentId, 2, 0, "admin", new string('B', 64), 12);
        Require(store.TrySaveDocument("Original", "QA", new[] { part }, Array.Empty<CompositeBlueprintStore.Group>(), Array.Empty<CompositeBlueprintStore.VectorData>(),
            "a", null, null, null, out var original, out string error, libraryOrigins: new[] { a }), error);
        var input = new InputFrames();
        using (var controller = new BlueprintEditorController(store, camera, font, name => name == "woodwall" ? wall : null,
            name => name, () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); }, editorInput: input))
        {
            Require(controller.OpenExisting(original, out error), error); yield return null;
            var view = Field<BlueprintEditorView>(controller, "view");
            var incoming = original.ImportCopy(); incoming.libraryOrigins = new List<CompositeBlueprintStore.LibraryOrigin> { b };
            var inserted = (BlueprintEditorDocument)typeof(BlueprintEditorController).GetMethod("DocumentFromBlueprint", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { incoming });
            incoming.libraryOrigins[0].publisher = "changed caller";
            Require(inserted.LibraryOrigins[0].Publisher == "admin", "Opening insertion aliased mutable DTO");
            Require((bool)Call(controller, "ApplyDocumentEdit", new Func<bool>(() => controller.Document.InsertBlueprint(inserted, new Point3(3, 0, 0), new Rotation3(0, 0, 0, 1))), false),
                "Actual editor insertion failed"); yield return null;
            NativeClick(view, null, "Save"); yield return null;
            Require(view.HasModal, "Save did not request metadata confirmation");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(!view.HasModal && original.parts.Count == 2 && new CompositeBlueprintStore(path).All().Single().libraryOrigins.Count == 2,
                "Actual Save did not persist inserted parts and origins");
            NativeClick(view, null, Field<Button>(view, "undoButton")); yield return null;
            NativeClick(view, null, "Save"); yield return null;
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            Require(original.parts.Count == 1 && original.libraryOrigins.Count == 1 && new CompositeBlueprintStore(path).All().Single().libraryOrigins.Count == 1,
                "Actual Save -> Undo -> Save kept cancelled origins");
            NativeClick(view, null, Field<Button>(view, "redoButton")); yield return null;
            NativeClick(view, null, "SaveAs"); yield return null;
            Field<TMP_InputField>(view, "saveMetadataName").SetTextWithoutNotify("Merged copy");
            NativeClick(view, null, Field<Button>(view, "dialogPrimaryButton")); yield return null;
            var copy = store.All().Single(item => item.id != original.id);
            Require(copy.parts.Count == 2 && copy.libraryOrigins.Count == 2 && original.parts.Count == 1 && original.libraryOrigins.Count == 1 &&
                !ReferenceEquals(copy.libraryOrigins[0], original.libraryOrigins[0]) && controller.Document.SourceBlueprintId == copy.id,
                "Actual Save As copied stale template history or changed original");
            Require(BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(store.ShareDirectory, copy.id + ".buildworks.json"))).libraryOrigins.Count == 2,
                "Actual saved share document lost merged origins");
            controller.Close(true);
            Require(controller.OpenExisting(new CompositeBlueprintStore(path).All().Single(item => item.id == copy.id), out error) && controller.Document.LibraryOrigins.Count == 2,
                "Actual controller reopen lost merged history");
            controller.Close(true);
        }
    }

    internal sealed class ServerLayoutFixture : IDisposable
    {
        internal readonly CompositeBlueprintStore Store;
        internal readonly CompositeBlueprintStore.Blueprint Source;
        internal readonly BlueprintServerClient Client;
        private readonly BlueprintServerEndpoint endpoint;
        internal bool Pending => endpoint.HasPendingWork;
        internal ServerLayoutFixture(GameObject wall, string output)
        {
            string folder = Path.Combine(output, "server-layout-" + Guid.NewGuid().ToString("N"));
            Store = new CompositeBlueprintStore(Path.Combine(folder, "personal.json"));
            var part = new CompositeBlueprintStore.Part { stableId = "a", prefabName = "woodwall", displayName = "Стена",
                rotation = new CompositeBlueprintStore.QuaternionData { w = 1 }, scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                baseScale = new CompositeBlueprintStore.VectorData(wall.transform.localScale) };
            Require(Store.TrySaveDocument("Большой дом — проверка серверной библиотеки", "ТЕСТ", new[] { part }, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), "a", null, out Source, out string error), error);
            Store.ConfigureImportScale(name => name == "woodwall" ? new CompositeBlueprintStore.VectorData(wall.transform.localScale) : (CompositeBlueprintStore.VectorData?)null,
                () => this, name => name.GetStableHashCode());
            var library = new ServerBlueprintLibrary(Path.Combine(folder, "server"), _ => true);
            byte[] bytes = BlueprintExchangeFormat.Encode(Source);
            for (int i = 0; i < 14; ++i) library.Publish("Администратор тестового сервера", bytes, i == 13 ? ServerBlueprintLibrary.Access.Administrators : ServerBlueprintLibrary.Access.Players);
            object connection = new object();
            endpoint = new BlueprintServerEndpoint(library, 972, _ => ("Администратор тестового сервера", true), (_, frame) => Client.Receive(frame, Time.realtimeSinceStartupAsDouble));
            Client = new BlueprintServerClient(972, frame => endpoint.Receive(connection, frame, Time.realtimeSinceStartupAsDouble), "test-layout");
            Client.Tick(Time.realtimeSinceStartupAsDouble);
        }
        internal void Tick() { Client.Tick(Time.realtimeSinceStartupAsDouble); endpoint.Tick(Time.realtimeSinceStartupAsDouble); }
        public void Dispose() { Client.Dispose(); endpoint.Dispose(); }
    }
}
