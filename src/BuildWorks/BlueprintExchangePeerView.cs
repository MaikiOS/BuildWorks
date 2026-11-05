using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using P = OstrixMods.BuildWorks.BlueprintPeerProtocol;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintExchangeView
    {
        private enum PeerReview { None, Send, Copy, Saved }
        private Func<BlueprintPeerClient> peerProvider;
        private BlueprintPeerClient peerClient;
        private BlueprintServerClient peerLibrary;
        private Guid peerSession, peerRecipient;
        private string peerPrincipal, peerServer;
        private long peerWorld;
        private bool peerMode, peerInbox, peerCommit;
        private GameObject[] libraryWidgets, peerWidgets;
        private bool[] libraryVisibility;
        private Button libraryTab, peerTab, peerAction, peerReject, peerWithdraw, peerRefresh, peerCancel;
        private RectTransform peerRows;
        private TMP_Text peerDetails, peerStatus, peerSource;
        private P.Participant[] peerPlayers = Array.Empty<P.Participant>();
        private P.Offer[] peerOffers = Array.Empty<P.Offer>();
        private P.Offer peerOffer, peerSent;
        private PeerReview peerReview;
        private P.Command peerCommand;
        private Task<BlueprintPeerClient.Result> peerTask;
        private Task<(CompositeBlueprintStore.Blueprint Document, int Version)> peerDecode;
        private Task<CompositeBlueprintStore.ImportReview> peerPreparation;
        private Task<byte[]> peerPublication;
        private CompositeBlueprintStore.ImportReview peerPrepared;
        private CompositeBlueprintStore.Blueprint peerSaved;
        private byte[] peerBytes;
        private int peerSourceRevision;
        private bool PeerBusy => peerTask != null || peerDecode != null || peerPreparation != null || peerPublication != null;

        private void InitializePeerPanel(Func<BlueprintPeerClient> provider)
        {
            peerProvider = provider;
            if (provider == null) return;
            panel.Find("ExchangeHelp").gameObject.SetActive(false);
            libraryWidgets = panel.Cast<Transform>().Where(t => t.name != "ExchangeTitle" && t.name != "ExchangeClose" && t.name != "ExchangeHelp")
                .Select(t => t.gameObject).ToArray();
            libraryTab = Button("ExchangeLibraryTab", T("server.library"), 20, 56, 360, () => SwitchPeer(false));
            peerTab = Button("ExchangePeerTab", T("peer.tab"), 400, 56, 360, () => SwitchPeer(true));
            libraryTab.interactable = false;
            SetServerDetails(T("server.help") + "\n" + T("server.choose"));
        }
        private void SwitchPeer(bool value)
        {
            if (value == peerMode || ServerBusy || PeerBusy || ServerWorkerBusy) return;
            if (value)
            {
                libraryVisibility = libraryWidgets.Select(w => w.activeSelf).ToArray();
                foreach (var widget in libraryWidgets) widget.SetActive(false);
                if (peerWidgets == null) BuildPeerPanel();
                foreach (var widget in peerWidgets) widget.SetActive(true);
            }
            else
            {
                DisposePeerOperation();
                foreach (var widget in peerWidgets) widget.SetActive(false);
                for (int i = 0; i < libraryWidgets.Length; ++i) libraryWidgets[i].SetActive(libraryVisibility[i]);
            }
            peerMode = value;
            panel.Find("ExchangeTitle").GetComponent<TMP_Text>().text = T(value ? "peer.tab" : "server.library");
            libraryTab.interactable = value; peerTab.interactable = !value;
            if (value) RefreshPeer(); else RenderServer();
        }
        private void BuildPeerPanel()
        {
            var old = new HashSet<GameObject>(panel.Cast<Transform>().Select(t => t.gameObject));
            peerRows = ServerScroll("PeerEntries", 20, 100, 336, 260);
            var detail = ServerScroll("PeerDetails", 370, 100, 390, 260);
            peerDetails = Label("PeerDetailsText", T("peer.help"), 8, 8, 360, 244, 14, detail);
            peerDetails.alignment = TextAlignmentOptions.TopLeft;
            peerDetails.overflowMode = TextOverflowModes.Overflow;
            detail.GetComponentInParent<ScrollRect>().content = peerDetails.rectTransform;
            Button("PeerPlayers", T("peer.players"), 20, 376, 108, () => SelectPeerList(false));
            Button("PeerInbox", T("peer.inbox"), 136, 376, 108, () => SelectPeerList(true));
            peerRefresh = Button("PeerRefresh", T("server.refresh"), 252, 376, 104, RefreshPeer);
            peerSource = Label("PeerSource", "", 20, 414, 740, 36, 14);
            peerReject = Button("PeerDecline", T("peer.decline"), 20, 460, 236, () => StartPeer(P.Command.Decline, () => peerClient.Decline(peerOffer)));
            peerWithdraw = Button("PeerWithdraw", T("peer.withdraw"), 264, 460, 236, () => StartPeer(P.Command.Withdraw, () => peerClient.Withdraw(peerSent)));
            peerCancel = Button("PeerCancel", T("server.cancel"), 520, 460, 240, CancelPeer);
            peerStatus = Label("PeerStatus", "", 20, 500, 740, 30, 14);
            peerAction = Button("PeerAccept", "", 20, 536, 740, AcceptPeerAction);
            peerWidgets = panel.Cast<Transform>().Select(t => t.gameObject).Where(w => !old.Contains(w)).ToArray();
        }
        private void SelectPeerList(bool inbox)
        {
            if (PeerBusy || ServerWorkerBusy) return;
            peerInbox = inbox; peerOffer = null; peerRecipient = Guid.Empty;
            peerReview = PeerReview.None; peerBytes = null; peerPrepared = null; peerSaved = null;
            RefreshPeer();
        }
        private bool PeerContext(bool hello = false) => !disposed && peerMode && peerClient != null && peerLibrary != null &&
            ReferenceEquals(peerProvider?.Invoke(), peerClient) && ReferenceEquals(serverProvider?.Invoke(), peerLibrary) &&
            peerLibrary.WorldUid == peerWorld && peerLibrary.ServerIdentity == peerServer &&
            (hello || peerClient.Ready && peerClient.Session == peerSession && peerClient.Principal == peerPrincipal);
        private void RequirePeerContext(bool hello = false)
        { if (!PeerContext(hello)) throw new InvalidOperationException(T("peer.context_changed")); }
        private void RefreshPeer()
        {
            if (PeerBusy || ServerWorkerBusy) return;
            var current = peerProvider?.Invoke(); var library = serverProvider?.Invoke();
            if (!ReferenceEquals(current, peerClient) || !ReferenceEquals(library, peerLibrary))
            {
                DisposePeerOperation(); peerClient = current; peerLibrary = library;
                peerOffer = peerSent = null; peerPlayers = Array.Empty<P.Participant>(); peerOffers = Array.Empty<P.Offer>();
                peerRecipient = Guid.Empty; peerSaved = null; peerReview = PeerReview.None;
            }
            if (current == null || library == null) { peerStatus.text = T("server.no_connection"); RenderPeer(); return; }
            peerWorld = library.WorldUid; peerServer = library.ServerIdentity;
            peerSession = current.Session; peerPrincipal = current.Principal;
            if (!current.Ready) StartPeer(P.Command.Hello, current.Hello);
            else StartPeer(peerInbox ? P.Command.Inbox : P.Command.Roster, () => peerInbox ? current.Inbox() : current.Roster());
        }
        private void StartPeer(P.Command command, Func<Task<BlueprintPeerClient.Result>> start)
        {
            if (PeerBusy) return;
            try
            {
                RequirePeerContext(command == P.Command.Hello); peerCommand = command; peerTask = start();
                peerTask.ContinueWith(done => { var observed = done.Exception; }, TaskScheduler.Default);
                peerStatus.text = T("server.waiting", 0);
            }
            catch (Exception error) { FailPeer(error); }
        }
        private void TickPeer()
        {
            if (!ReferenceEquals(peerProvider?.Invoke(), peerClient) || !ReferenceEquals(serverProvider?.Invoke(), peerLibrary))
            {
                bool saved = peerSaved != null; DisposePeerOperation();
                peerStatus.text = T(saved ? "peer.saved_unknown" : "peer.context_changed"); RenderPeer(); return;
            }
            try
            {
                if (peerTask?.IsCompleted == true)
                {
                    var task = peerTask; peerTask = null; RequirePeerContext(peerCommand == P.Command.Hello);
                    var result = task.GetAwaiter().GetResult();
                    if (result.Reply.World != peerWorld) throw new InvalidDataException(T("server.invalid"));
                    switch (peerCommand)
                    {
                        case P.Command.Hello:
                            peerSession = peerClient.Session; peerPrincipal = peerClient.Principal; RefreshPeer(); break;
                        case P.Command.Roster:
                            peerPlayers = result.Reply.Participants.Where(p => p.Session != peerSession).ToArray();
                            RebuildPeerRows();
                            if (peerSaved == null) { SetPeerDetails(T("peer.help")); peerStatus.text = T("peer.ready"); }
                            else peerStatus.text = T(peerOffer?.State == P.OfferState.Accepted ? "peer.saved_confirmed" : "peer.saved_unknown");
                            break;
                        case P.Command.Inbox:
                            peerOffers = result.Reply.Offers; RebuildPeerRows();
                            peerStatus.text = T(peerSaved == null ? "peer.inbox_help" : peerOffer?.State == P.OfferState.Accepted ? "peer.saved_confirmed" : "peer.saved_unknown"); break;
                        case P.Command.OfferStart:
                            peerSent = P.Snapshot(result.Reply.Offer); peerReview = PeerReview.None; peerBytes = null;
                            peerStatus.text = T("peer.offered"); break;
                        case P.Command.Read:
                            if (result.Document == null) { ShowPeerTerminal(result.Reply.Offer); break; }
                            if (ServerWorkerBusy) throw new InvalidOperationException(T("peer.failure.Busy"));
                            peerOffer = P.Snapshot(result.Reply.Offer); peerBytes = (byte[])result.Document.Clone();
                            var bytes = peerBytes; var offer = P.Snapshot(peerOffer);
                            peerDecode = TrackServerWorker(Task.Run(() => DecodePeerDocument(bytes, offer))); break;
                        case P.Command.Status:
                            if (peerCommit && result.Reply.Offer.State == P.OfferState.Pending)
                            { peerCommit = false; SaveAndAcceptPeer(); }
                            else { peerCommit = false; ShowPeerTerminal(result.Reply.Offer); }
                            break;
                        default: ShowPeerTerminal(result.Reply.Offer); break;
                    }
                }
                if (peerDecode?.IsCompleted == true)
                {
                    var task = peerDecode; peerDecode = null; RequirePeerContext(); var decoded = task.GetAwaiter().GetResult();
                    if (ServerWorkerBusy) throw new InvalidOperationException(T("peer.failure.Busy"));
                    peerPreparation = TrackServerWorker(store.PrepareImportReview(new CompositeBlueprintStore.ImportReview
                    { Documents = new List<CompositeBlueprintStore.Blueprint> { decoded.Document } }, name => name.GetStableHashCode()));
                }
                if (peerPreparation?.IsCompleted == true)
                {
                    var task = peerPreparation; peerPreparation = null; RequirePeerContext(); peerPrepared = task.GetAwaiter().GetResult();
                    peerReview = PeerReview.Copy;
                    SetPeerDetails(T("peer.copy_review", PeerSender(peerOffer), peerOffer.Name, peerOffer.Parts, peerOffer.FormatVersion,
                        peerPrepared.Missing.Parts, peerPrepared.Contents.Storages, peerPrepared.Contents.StoredItems,
                        peerPrepared.Contents.Mounts, peerPrepared.Contents.Unknown));
                    peerStatus.text = T("peer.consent");
                }
                if (peerPublication?.IsCompleted == true)
                {
                    var task = peerPublication; peerPublication = null; RequirePeerContext(); peerBytes = task.GetAwaiter().GetResult();
                    if (store.Revision != peerSourceRevision) throw new InvalidOperationException(T("server.local_changed"));
                    peerReview = PeerReview.Send; SetPeerDetails(T("peer.send_review", serverLocalSource.name, PeerRecipientName()));
                    peerStatus.text = T("peer.send_consent");
                }
            }
            catch (Exception error) { FailPeer(error); }
            if (peerTask != null && peerSaved == null) peerStatus.text = T("server.waiting", Mathf.RoundToInt(peerClient.Progress * 100));
            RenderPeer();
        }
        private static (CompositeBlueprintStore.Blueprint Document, int Version) DecodePeerDocument(byte[] bytes, P.Offer offer)
        {
            using (var hash = SHA256.Create())
                if (bytes == null || bytes.Length != offer.Bytes || BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "") != offer.Sha256)
                    throw new InvalidDataException("$buildworks_server.invalid");
            var document = BlueprintExchangeFormat.DecodeWithVersion(new UTF8Encoding(false, true).GetString(bytes), out int version);
            if (document.id != offer.DocumentId.ToString("N") || document.name != offer.Name || document.parts.Count != offer.Parts || version != offer.FormatVersion)
                throw new InvalidDataException("$buildworks_server.invalid");
            return (document, version);
        }
        private void AcceptPeerAction()
        {
            if (PeerBusy || ServerWorkerBusy) return;
            try
            {
                RequirePeerContext();
                if (peerReview == PeerReview.Copy || peerReview == PeerReview.Saved)
                { peerCommit = true; StartPeer(P.Command.Status, () => peerClient.Status(peerOffer)); }
                else if (peerReview == PeerReview.Send)
                {
                    if (store.Revision != peerSourceRevision) throw new InvalidOperationException(T("server.local_changed"));
                    StartPeer(P.Command.OfferStart, () => peerClient.Offer(peerRecipient, peerBytes));
                }
                else if (peerInbox && peerOffer != null) StartPeer(P.Command.Read, () => peerClient.Read(peerOffer));
                else if (!peerInbox && peerRecipient != Guid.Empty && serverLocalSource != null)
                {
                    peerSourceRevision = store.Revision; peerPublication = TrackServerWorker(store.PrepareServerPublication(serverLocalSource));
                    peerStatus.text = T("server.preparing");
                }
            }
            catch (Exception error) { FailPeer(error); }
            RenderPeer();
        }
        private void SaveAndAcceptPeer()
        {
            RequirePeerContext();
            if (peerBytes == null || peerOffer == null || peerOffer.RecipientSession != peerSession || peerOffer.Recipient != peerPrincipal ||
                peerSaved == null && peerPrepared?.IsHostCurrent?.Invoke() != true) throw new InvalidOperationException(T("peer.context_changed"));
            var origin = new CompositeBlueprintStore.PeerOrigin { server = peerServer, worldUid = peerWorld,
                sender = peerOffer.Sender, recipient = peerOffer.Recipient, offerId = peerOffer.Id.ToString("N"),
                documentId = peerOffer.DocumentId.ToString("N"), sha256 = peerOffer.Sha256, formatVersion = peerOffer.FormatVersion };
            if (!store.TrySaveAcceptedPeerCopy(peerBytes, origin, out var copy, out string error) || copy == null)
                throw new IOException(error);
            // Primary card+receipt are durable before ACK; share-file warnings cannot undo that save.
            peerSaved = copy; batchImported = new[] { copy }; peerReview = PeerReview.Saved;
            peerStatus.text = T("peer.saved_unknown"); SetPeerDetails(T("peer.saved_details", copy.name) + "\n" + ShareWarning(store));
            StartPeer(P.Command.Accept, () => peerClient.Accept(peerOffer));
        }
        private void ShowPeerTerminal(P.Offer offer)
        {
            if (offer == null) throw new InvalidDataException(T("server.invalid"));
            bool incoming = peerOffer?.Id == offer.Id;
            if (!incoming && peerSent?.Id != offer.Id) throw new InvalidDataException(T("server.invalid"));
            if (peerSent?.Id == offer.Id) peerSent = P.Snapshot(offer);
            string state = T(new[] { "peer.state.Pending", "peer.state.Accepted", "peer.state.Declined", "peer.state.Withdrawn", "peer.state.Expired" }[(int)offer.State]);
            if (!incoming)
            {
                peerStatus.text = (peerSaved != null ? T(peerOffer?.State == P.OfferState.Accepted ? "peer.saved_confirmed" : "peer.saved_unknown") + " " : "") +
                    T("peer.outgoing_terminal", offer.Name, state);
                return;
            }
            peerOffer = P.Snapshot(offer);
            peerStatus.text = peerSaved != null ? T(offer.State == P.OfferState.Accepted ? "peer.saved_confirmed" : "peer.saved_terminal", state) : T("peer.terminal", state);
            if (offer.State != P.OfferState.Pending) { peerBytes = null; peerPrepared = null; peerReview = PeerReview.None; }
        }
        private void CancelPeer()
        {
            if (peerSaved != null && !PeerBusy) { Dispose(); return; }
            bool saved = peerSaved != null, uncertain = peerTask != null && peerCommand == P.Command.OfferStart;
            DisposePeerOperation(); peerReview = PeerReview.None;
            peerStatus.text = T(saved ? "peer.saved_unknown" : uncertain ? "server.check_result" : "server.cancelled"); RenderPeer();
        }
        private void DisposePeerOperation()
        {
            var task = peerTask; peerTask = null;
            if (task != null) peerClient?.Cancel(task);
            peerDecode = null; peerPreparation = null; peerPublication = null; peerCommit = false;
        }
        private void FailPeer(Exception error)
        {
            DisposePeerOperation();
            peerReview = peerSaved == null ? PeerReview.None : PeerReview.Saved;
            string[] keys = { "peer.failure.Invalid", "peer.failure.Unavailable", "peer.failure.Denied", "peer.failure.Conflict", "peer.failure.Capacity", "peer.failure.Busy", "peer.failure.Cancelled", "peer.failure.Failed", "peer.failure.Timeout" };
            string reason = error is BlueprintPeerClient.Failure failure ? T(keys[(int)failure.Code]) : BuildWorksLocalization.ResolveUserText(error.Message);
            peerStatus.text = (peerSaved != null ? T("peer.saved_unknown") + " " : peerCommand == P.Command.OfferStart ? T("server.check_result") + " " : "") + reason;
        }
        private string PeerSender(P.Offer offer) => peerPlayers.FirstOrDefault(p => p.Session == offer.SenderSession)?.Name ?? offer.Sender;
        private string PeerRecipientName()
        {
            var player = peerPlayers.FirstOrDefault(p => p.Session == peerRecipient);
            return player == null ? "" : player.Name + " · " + player.Session.ToString("N").Substring(0, 6);
        }
        private void SetPeerDetails(string text)
        {
            peerDetails.text = text; peerDetails.ForceMeshUpdate();
            peerDetails.rectTransform.sizeDelta = new Vector2(360, Mathf.Max(244, peerDetails.preferredHeight + 16));
        }
        private void RebuildPeerRows()
        {
            foreach (Transform child in peerRows) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            int count = peerInbox ? peerOffers.Length : peerPlayers.Length;
            peerRows.sizeDelta = new Vector2(322, Mathf.Max(260, count * 48));
            for (int index = 0; index < count; ++index)
            {
                int row = index;
                string label = peerInbox ? PeerSender(peerOffers[row]) + "\n" + peerOffers[row].Name : peerPlayers[row].Name + " · " + peerPlayers[row].Session.ToString("N").Substring(0, 6);
                Button("PeerEntry_" + index, label, 0, index * 48, 318, () =>
                {
                    if (PeerBusy || ServerWorkerBusy) return;
                    peerReview = PeerReview.None; peerBytes = null; peerPrepared = null; peerSaved = null;
                    if (peerInbox) { peerOffer = P.Snapshot(peerOffers[row]); SetPeerDetails(T("peer.offer_details", PeerSender(peerOffer), peerOffer.Name, peerOffer.Parts, peerOffer.FormatVersion)); }
                    else { peerRecipient = peerPlayers[row].Session; SetPeerDetails(T("peer.recipient", PeerRecipientName())); }
                    RenderPeer();
                }, peerRows);
            }
        }
        private void RenderPeer()
        {
            if (!peerAction) return;
            bool idle = !PeerBusy && !ServerWorkerBusy, ready = PeerContext();
            libraryTab.interactable = idle; peerRefresh.interactable = idle;
            peerCancel.GetComponentInChildren<TMP_Text>().text = T(peerSaved != null && !PeerBusy ? "editor.view.close" : "server.cancel");
            peerReject.interactable = idle && ready && peerOffer?.State == P.OfferState.Pending && peerSaved == null;
            peerWithdraw.interactable = idle && ready && peerSent != null && peerSent.State == P.OfferState.Pending;
            peerAction.interactable = idle && ready && (peerReview != PeerReview.None || peerInbox && peerOffer?.State == P.OfferState.Pending || !peerInbox && peerRecipient != Guid.Empty && serverLocalSource != null);
            peerAction.GetComponentInChildren<TMP_Text>().text = T(peerReview == PeerReview.Copy ? "peer.accept_copy" : peerReview == PeerReview.Saved ? "peer.check_ack" : peerReview == PeerReview.Send ? "peer.offer" : peerInbox ? "peer.preview" : "peer.prepare_send");
            peerSource.text = peerInbox ? T("peer.inbox_source") : T(serverLocalSource == null ? "server.local_choose" : "server.local_selected", serverLocalSource?.name ?? "");
        }
    }
}
