using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using P = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintExchangeView
    {
        private enum ServerReview { None, Copy, Publish, Update, Delete, Saved }
        // A closed panel cannot accumulate abandoned large decode/preparation workers.
        private static Task serverBackground;
        private bool serverMode, serverRoleKnown;
        private Func<BlueprintServerClient> serverProvider;
        private BlueprintServerClient serverClient;
        private CompositeBlueprintStore.Blueprint serverLocalSource;
        private P.Item[] serverItems = Array.Empty<P.Item>();
        private P.Item serverSelected, serverReceived;
        private int serverOffset, serverTotal, serverStoreRevision;
        private byte serverAccess;
        private ServerReview serverReview;
        private P.Command serverOperation;
        private Task<BlueprintServerClient.Result> serverTask;
        private Task<(CompositeBlueprintStore.Blueprint Document, int Version)> serverDecode;
        private Task<CompositeBlueprintStore.ImportReview> serverHostPreparation;
        private Task<byte[]> serverPublication;
        private CompositeBlueprintStore.ImportReview serverPrepared;
        private CompositeBlueprintStore.LibraryOrigin serverOrigin;
        private byte[] serverPreparedBytes;
        private RectTransform serverList;
        private TMP_Text serverDetails, serverStatus, serverPage, serverSource;
        private string serverNotice;
        private Button serverAccept, serverPublish, serverUpdate, serverDelete, serverVisibility, serverCancel, serverRefresh, serverPrevious, serverNext;
        private bool ServerBusy => serverTask != null || serverDecode != null || serverHostPreparation != null || serverPublication != null;
        private static bool ServerWorkerBusy => Volatile.Read(ref serverBackground)?.IsCompleted == false;

        internal static BlueprintExchangeView ForServer(Transform parent, TMP_Text font, CompositeBlueprintStore store,
            CompositeBlueprintStore.Blueprint selectedLocal, Func<BlueprintServerClient> provider, Action closed,
            Action<CompositeBlueprintStore.Blueprint[]> accepted = null, bool frontCanvas = false)
        {
            var view = new BlueprintExchangeView(parent, font, store, null, closed, frontCanvas, report: "", imported: accepted);
            view.serverMode = true; view.serverProvider = provider; view.serverLocalSource = selectedLocal;
            view.BuildServerPanel(); view.TickServer(); return view;
        }
        private void BuildServerPanel()
        {
            foreach (Transform child in panel)
                child.gameObject.SetActive(child.name == "ExchangeTitle" || child.name == "ExchangeClose" || child.name == "ExchangeHelp");
            panel.Find("ExchangeTitle").GetComponent<TMP_Text>().text = T("server.library");
            panel.Find("ExchangeHelp").GetComponent<TMP_Text>().text = T("server.help");
            serverList = ServerScroll("ServerEntries", 20, 100, 336, 260);
            RectTransform details = ServerScroll("ServerDetails", 370, 100, 390, 260);
            serverDetails = Label("ServerDetailsText", "", 8, 8, 360, 244, 14, details);
            serverDetails.alignment = TextAlignmentOptions.TopLeft; serverDetails.overflowMode = TextOverflowModes.Overflow;
            details.GetComponentInParent<ScrollRect>().content = serverDetails.rectTransform;
            serverPrevious = Button("ServerPrevious", "‹", 20, 376, 38, () => LoadServerPage(Math.Max(0, serverOffset - P.PageSize)));
            serverPage = Label("ServerPage", "", 64, 376, 120, 30, 14);
            serverNext = Button("ServerNext", "›", 192, 376, 38, () => LoadServerPage(serverOffset + P.PageSize));
            serverRefresh = Button("ServerRefresh", T("server.refresh"), 242, 376, 114, () => LoadServerPage(0));
            serverVisibility = Button("ServerVisibility", "", 370, 376, 190, () => { serverAccess = (byte)(1 - serverAccess); RenderServer(); });
            serverDelete = Button("ServerDelete", T("server.delete"), 568, 376, 192, () =>
            { serverReview = ServerReview.Delete; SetServerDetails(T("server.delete_review", serverSelected.Name, serverSelected.Revision)); RenderServer(); });
            serverSource = Label("ServerLocalSource", "", 20, 414, 740, 36, 14);
            serverPublish = Button("ServerPublish", T("server.publish"), 20, 460, 236, () => PrepareServerPublication(false));
            serverUpdate = Button("ServerUpdate", T("server.update"), 264, 460, 236, () => PrepareServerPublication(true));
            serverCancel = Button("ServerCancel", T("server.cancel"), 520, 460, 240, () => { if (serverReview == ServerReview.Saved) Dispose(); else CancelServerOperation(); });
            serverStatus = Label("ServerStatus", "", 20, 500, 740, 30, 14);
            serverAccept = Button("ServerAccept", T("server.receive"), 20, 536, 740, AcceptServerAction);
        }
        private RectTransform ServerScroll(string name, float x, float y, float width, float height)
        {
            var viewport = Rect(name, panel, x, y, width, height);
            viewport.gameObject.AddComponent<Image>().color = new Color(.05f, .055f, .055f, 1);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.viewport = viewport; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 132;
            var content = Rect(name + "Content", viewport, 0, 0, width - 14, height); scroll.content = content;
            var bar = Rect(name + "Scrollbar", viewport, width - 10, 0, 10, height);
            bar.gameObject.AddComponent<Image>().color = new Color(.08f, .08f, .07f, 1);
            var handle = Rect(name + "Handle", bar, 0, 0, 10, 24);
            var image = handle.gameObject.AddComponent<Image>(); image.color = new Color(.7f, .58f, .32f, 1);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = image;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }
        private void TickServer()
        {
            var parent = (RectTransform)root.transform.parent;
            panel.localScale = Vector3.one * Mathf.Min(1, (parent.rect.width - 24) / 780, (parent.rect.height - 24) / 580);
            var current = serverProvider?.Invoke();
            if (!ReferenceEquals(current, serverClient))
            {
                CancelServerOperation(); serverClient = current; serverRoleKnown = false;
                serverItems = Array.Empty<P.Item>(); serverSelected = null; serverOffset = serverTotal = 0;
                SetServerDetails(T("server.choose")); RebuildServerRows();
                if (current == null) serverStatus.text = T("server.no_connection"); else LoadServerPage(0);
            }
            if (serverClient == null) { serverStatus.text = T("server.no_connection"); RenderServer(); return; }
            try
            {
                if (serverTask?.IsCompleted == true)
                {
                    var task = serverTask; serverTask = null;
                    var result = task.GetAwaiter().GetResult();
                    if (serverOperation == P.Command.List)
                    {
                        serverItems = result.Reply.Items; serverOffset = result.Reply.Offset; serverTotal = result.Reply.Total;
                        serverRoleKnown = true; serverSelected = null; serverReview = ServerReview.None;
                        serverStatus.text = serverNotice ?? T(serverTotal == 0 ? "server.empty" : "server.ready"); serverNotice = null;
                        RebuildServerRows(); SetServerDetails(T("server.choose"));
                    }
                    else if (serverOperation == P.Command.Read)
                    {
                        serverReceived = result.Reply.Item;
                        byte[] bytes = result.Document; string expected = serverReceived.Sha256;
                        serverDecode = TrackServerWorker(Task.Run(() =>
                        {
                            using (var hash = SHA256.Create())
                                if (BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "") != expected)
                                    throw new System.IO.InvalidDataException("$buildworks_server.invalid");
                            var document = BlueprintExchangeFormat.DecodeWithVersion(new UTF8Encoding(false, true).GetString(bytes), out int version);
                            return (document, version);
                        }));
                        serverStatus.text = T("server.preparing");
                    }
                    else { serverReview = ServerReview.None; serverNotice = T("server.operation_done"); LoadServerPage(0); }
                }
                if (serverDecode?.IsCompleted == true)
                {
                    var task = serverDecode; serverDecode = null; var decoded = task.GetAwaiter().GetResult();
                    serverOrigin = new CompositeBlueprintStore.LibraryOrigin(serverClient.ServerIdentity, serverClient.WorldUid,
                        serverReceived.Id.ToString("N"), serverReceived.Revision, serverReceived.Access, serverReceived.Publisher, serverReceived.Sha256, decoded.Version);
                    if (!CompositeBlueprintStore.ValidLibraryOrigins(new[] { serverOrigin })) throw new System.IO.InvalidDataException("$buildworks_server.invalid");
                    serverHostPreparation = TrackServerWorker(store.PrepareImportReview(new CompositeBlueprintStore.ImportReview
                    { Documents = new System.Collections.Generic.List<CompositeBlueprintStore.Blueprint> { decoded.Document } }, name => name.GetStableHashCode()));
                }
                if (serverHostPreparation?.IsCompleted == true)
                {
                    var task = serverHostPreparation; serverHostPreparation = null; serverPrepared = task.GetAwaiter().GetResult();
                    serverReview = ServerReview.Copy; serverStatus.text = T("server.copy_review");
                    var summary = serverPrepared.Contents; var missing = serverPrepared.Missing;
                    SetServerDetails(T("server.copy_details", serverReceived.Name, serverReceived.Parts, missing.Parts,
                        summary.Storages, summary.StoredItems, summary.Mounts, summary.Unknown));
                    if (!missing.Known) SetServerDetails(T("server.host_not_ready"));
                    else if (missing.EmptyDocuments != 0) SetServerDetails(serverDetails.text + "\n" + T("server.no_available_parts"));
                }
                if (serverPublication?.IsCompleted == true)
                {
                    var task = serverPublication; serverPublication = null; serverPreparedBytes = task.GetAwaiter().GetResult();
                    serverStatus.text = T("server.publication_review");
                    SetServerDetails(T(serverReview == ServerReview.Update ? "server.update_review" : "server.publish_review",
                        serverLocalSource.name, serverSelected?.Name ?? "", serverSelected?.Revision ?? 0));
                }
                if (serverTask != null) serverStatus.text = T("server.waiting", Mathf.RoundToInt(serverClient.Progress * 100));
            }
            catch (Exception error) { FailServer(error); }
            RenderServer();
        }
        private static Task<T> TrackServerWorker<T>(Task<T> task)
        {
            Interlocked.Exchange(ref serverBackground, task);
            task.ContinueWith(done => { var observed = done.Exception; Interlocked.CompareExchange(ref serverBackground, null, done); }, TaskScheduler.Default);
            return task;
        }
        private void LoadServerPage(int offset)
        {
            if (ServerBusy || serverClient == null) return;
            serverReview = ServerReview.None; serverPrepared = null; serverPreparedBytes = null;
            BeginServerRequest(P.Command.List, () => serverClient.List(offset));
        }
        private void BeginServerRequest(P.Command command, Func<Task<BlueprintServerClient.Result>> start)
        {
            try { serverOperation = command; serverTask = start(); serverStatus.text = T("server.waiting", 0); }
            catch (Exception error) { FailServer(error); }
        }
        private void PrepareServerPublication(bool update)
        {
            if (ServerBusy || ServerWorkerBusy || serverLocalSource == null) return;
            try
            {
                serverReview = update ? ServerReview.Update : ServerReview.Publish; serverStoreRevision = store.Revision;
                serverPublication = TrackServerWorker(store.PrepareServerPublication(serverLocalSource)); serverStatus.text = T("server.preparing");
            }
            catch (Exception error) { FailServer(error); }
            RenderServer();
        }
        private void AcceptServerAction()
        {
            if (ServerBusy || serverClient == null) return;
            try
            {
                switch (serverReview)
                {
                    case ServerReview.None:
                        if (serverSelected != null && !ServerWorkerBusy) BeginServerRequest(P.Command.Read, () => serverClient.Read(serverSelected)); break;
                    case ServerReview.Copy:
                        if (serverPrepared?.IsHostCurrent?.Invoke() != true || !serverPrepared.Missing.Known || serverPrepared.Missing.EmptyDocuments != 0)
                            throw new System.IO.InvalidDataException("$buildworks_placement.contents_stale");
                        if (!store.TrySaveAcceptedCopy(serverPrepared.Documents.Single(), serverOrigin, out var copy, out string error))
                            throw new System.IO.IOException(error);
                        batchImported = new[] { copy }; serverReview = ServerReview.Saved;
                        serverStatus.text = T("server.saved", copy.name); SetServerDetails(T("server.saved_details") + "\n" + ShareWarning(store)); break;
                    case ServerReview.Publish:
                    case ServerReview.Update:
                        if (serverPreparedBytes == null || store.Revision != serverStoreRevision) throw new System.IO.InvalidDataException("$buildworks_server.local_changed");
                        byte[] bytes = serverPreparedBytes; serverPreparedBytes = null;
                        BeginServerRequest(P.Command.PublishStart, () => serverClient.Publish(bytes, serverAccess, serverReview == ServerReview.Update ? serverSelected : null)); break;
                    case ServerReview.Delete: BeginServerRequest(P.Command.Delete, () => serverClient.Delete(serverSelected)); break;
                    case ServerReview.Saved: Dispose(); return;
                }
            }
            catch (Exception error) { FailServer(error); }
            RenderServer();
        }
        private void CancelServerOperation()
        {
            bool uncertain = serverTask != null && (serverOperation == P.Command.PublishStart || serverOperation == P.Command.Delete);
            bool saved = serverReview == ServerReview.Saved;
            DisposeServerOperation(); serverReview = ServerReview.None;
            if (serverStatus) serverStatus.text = saved ? T("server.saved", batchImported[0].name) : T(uncertain ? "server.check_result" : "server.cancelled");
            if (serverDetails) SetServerDetails(T("server.choose"));
            if (serverAccept) RenderServer();
        }
        private void DisposeServerOperation()
        {
            if (serverTask != null) { serverClient?.Cancel(serverTask); serverTask.ContinueWith(task => { var observed = task.Exception; }, TaskScheduler.Default); }
            serverTask = null; serverDecode = null; serverHostPreparation = null; serverPublication = null;
            serverPrepared = null; serverPreparedBytes = null; serverOrigin = null; serverReceived = null;
        }
        private void FailServer(Exception error)
        {
            DisposeServerOperation(); serverReview = ServerReview.None;
            if (error is BlueprintServerClient.Failure failure)
            {
                string[] keys = { "server.invalid", "server.unavailable", "server.admin_required", "server.conflict", "server.capacity", "server.busy", "server.cancelled", "server.failed", "server.timeout" };
                serverStatus.text = T(keys[(int)failure.Code]);
                if (failure.Code == P.Failure.Denied || failure.Code == P.Failure.Unavailable) serverRoleKnown = false;
            }
            else serverStatus.text = error is InvalidOperationException ? T("server.busy") : BuildWorksLocalization.ResolveUserText(error.Message);
        }
        private void RebuildServerRows()
        {
            foreach (Transform child in serverList) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            serverList.sizeDelta = new Vector2(322, Mathf.Max(260, serverItems.Length * 48));
            for (int i = 0; i < serverItems.Length; ++i)
            {
                var item = serverItems[i];
                var button = Button("ServerEntry" + i, item.Name, 4, i * 48, 314, () =>
                { if (ServerBusy) return; serverSelected = item; serverAccess = item.Access; serverReview = ServerReview.None; SetServerDetails(T("server.details", item.Name, item.Category, item.Publisher, item.Revision, item.Parts, item.Bytes, T(item.Access == 0 ? "server.players" : "server.administrators"))); RenderServer(); }, serverList);
                ((RectTransform)button.transform).sizeDelta = new Vector2(314, 44);
                var caption = button.GetComponentInChildren<TMP_Text>();
                caption.rectTransform.sizeDelta = new Vector2(298, 22);
                caption.textWrappingMode = TextWrappingModes.NoWrap; caption.maxVisibleLines = 1;
                Label("ServerEntryInfo" + i, T("server.row", item.Parts, item.Revision), 8, 22, 298, 22, 12, button.transform);
            }
            serverList.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1;
        }
        private void SetServerDetails(string text)
        {
            serverDetails.text = text;
            serverDetails.rectTransform.sizeDelta = new Vector2(360, Mathf.Max(244, serverDetails.GetPreferredValues(text, 360, 0).y + 16));
            serverDetails.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1;
        }
        private void RenderServer()
        {
            bool busy = ServerBusy, idle = !busy && serverReview == ServerReview.None;
            bool admin = serverRoleKnown && serverClient?.IsAdministrator == true;
            serverPublish.gameObject.SetActive(admin); serverUpdate.gameObject.SetActive(admin);
            serverDelete.gameObject.SetActive(admin); serverVisibility.gameObject.SetActive(admin);
            serverSource.text = T(serverLocalSource == null ? "server.local_choose" : "server.local_selected", serverLocalSource?.name ?? "");
            serverPage.text = T("server.page", serverOffset / P.PageSize + 1, Mathf.Max(1, (serverTotal + P.PageSize - 1) / P.PageSize));
            serverPrevious.interactable = idle && serverOffset > 0; serverNext.interactable = idle && serverOffset + serverItems.Length < serverTotal;
            serverRefresh.interactable = !busy && serverClient != null && serverReview != ServerReview.Saved;
            serverPublish.interactable = admin && idle && serverLocalSource != null && !ServerWorkerBusy;
            serverUpdate.interactable = serverPublish.interactable && serverSelected != null;
            serverDelete.interactable = admin && idle && serverSelected != null;
            serverVisibility.interactable = admin && !busy && serverReview != ServerReview.Copy && serverReview != ServerReview.Saved;
            serverVisibility.GetComponentInChildren<TMP_Text>().text = T(serverAccess == 0 ? "server.players" : "server.administrators");
            serverCancel.interactable = busy || serverReview != ServerReview.None;
            serverCancel.GetComponentInChildren<TMP_Text>().text = T(serverReview == ServerReview.Saved ? "exchange.batch_catalog" :
                busy && (serverOperation == P.Command.PublishStart || serverOperation == P.Command.Delete) ? "server.stop_waiting" : "server.cancel");
            serverAccept.interactable = !busy && serverClient != null && (serverReview == ServerReview.Saved ||
                serverReview == ServerReview.Copy && serverPrepared?.Missing.Known == true && serverPrepared.Missing.EmptyDocuments == 0 ||
                (serverReview == ServerReview.Publish || serverReview == ServerReview.Update) && serverPreparedBytes != null && admin ||
                serverReview == ServerReview.Delete && admin || idle && serverSelected != null && serverClient.Ready && !ServerWorkerBusy);
            serverAccept.GetComponentInChildren<TMP_Text>().text = T(serverReview == ServerReview.None ? "server.receive" : serverReview == ServerReview.Copy ? "server.save_copy" :
                serverReview == ServerReview.Saved ? "exchange.batch_catalog" : "server.confirm");
            int selectedIndex = Array.IndexOf(serverItems, serverSelected);
            foreach (var button in serverList.GetComponentsInChildren<Button>())
            {
                button.interactable = idle;
                skin.Apply(button, button.name == "ServerEntry" + selectedIndex);
            }
        }
    }
}
