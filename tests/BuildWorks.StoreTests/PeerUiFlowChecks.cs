using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;
using P = OstrixMods.BuildWorks.BlueprintPeerProtocol;

namespace OstrixMods.BuildWorks
{
    // Only the surrounding view/labels are inert. Extracted production methods use the real Store and peer client.
    internal sealed partial class BlueprintExchangeView
    {
        private enum PeerReview { None, Saved }
        private sealed class Text { internal string text; }
        private bool disposed, peerMode = true;
        private readonly B store;
        private readonly BlueprintPeerClient peerClient;
        private readonly BlueprintServerClient peerLibrary;
        private readonly Func<BlueprintPeerClient> peerProvider;
        private readonly Func<BlueprintServerClient> serverProvider;
        private readonly Guid peerSession;
        private readonly string peerPrincipal, peerServer;
        private readonly long peerWorld;
        private byte[] peerBytes;
        private P.Offer peerOffer, peerSent;
        private P.Participant[] peerPlayers = Array.Empty<P.Participant>();
        private Guid peerRecipient;
        private B.ImportReview peerPrepared;
        private B.Blueprint peerSaved;
        private B.Blueprint[] batchImported;
        private PeerReview peerReview;
        private readonly Text peerStatus = new Text();
        internal Action BeforeAck;
        internal bool HostCurrent = true, Connected = true;
        internal int AckStarts;
        internal Task<BlueprintPeerClient.Result> Ack;
        internal B.Blueprint Saved => peerSaved;
        internal BlueprintExchangeView(B store, BlueprintPeerClient peer, BlueprintServerClient library, P.Offer offer, byte[] bytes)
        {
            this.store = store; peerClient = peer; peerLibrary = library;
            peerProvider = () => Connected ? peerClient : null; serverProvider = () => Connected ? peerLibrary : null;
            peerSession = peer.Session; peerPrincipal = peer.Principal; peerWorld = library.WorldUid; peerServer = library.ServerIdentity;
            peerOffer = P.Snapshot(offer); peerBytes = (byte[])bytes.Clone();
            peerPrepared = new B.ImportReview { IsHostCurrent = () => HostCurrent };
        }
        internal void AcceptForTest() => SaveAndAcceptPeer();
        internal void CloseForTest() => disposed = true;
        internal void CheckRecipientNamesForTest()
        {
            peerPlayers = new[] {
                new P.Participant { Session = Guid.ParseExact("12345600000000000000000000000001", "N"), Name = "Игрок" },
                new P.Participant { Session = Guid.ParseExact("65432100000000000000000000000002", "N"), Name = "Игрок" } };
            foreach (var player in peerPlayers)
            {
                peerRecipient = player.Session;
                if (PeerRecipientName() != player.Name + " · " + player.Session.ToString("N").Substring(0, 6))
                    throw new Exception("Recipient confirmation lost the roster session distinction for equal names.");
            }
            peerRecipient = Guid.Empty;
            if (PeerRecipientName() != "") throw new Exception("Missing recipient fabricated a display name.");
            peerPlayers = Array.Empty<P.Participant>();
        }
        internal void CheckForeignTerminalForTest()
        {
            var saved = peerSaved; var bytes = peerBytes; var prepared = peerPrepared; var incoming = peerOffer;
            peerSent = P.Snapshot(incoming); peerSent.Id = Guid.NewGuid();
            foreach (var state in new[] { P.OfferState.Accepted, P.OfferState.Withdrawn })
            {
                var outgoing = P.Snapshot(peerSent); outgoing.State = state; ShowPeerTerminal(outgoing);
                if (peerSent.State != state || !ReferenceEquals(peerOffer, incoming) || peerSaved != saved ||
                    !ReferenceEquals(peerBytes, bytes) || !ReferenceEquals(peerPrepared, prepared) || peerReview != PeerReview.Saved ||
                    !peerStatus.text.Contains("peer.saved_unknown") || peerStatus.text.Contains("peer.saved_confirmed"))
                    throw new Exception("Unrelated outgoing terminal reply confirmed or cleared the saved incoming copy retry.");
            }
        }
        internal void CheckIncomingTerminalForTest(P.Offer accepted)
        {
            ShowPeerTerminal(accepted);
            if (peerOffer.State != P.OfferState.Accepted || peerStatus.text != "peer.saved_confirmed" ||
                peerBytes != null || peerPrepared != null || peerReview != PeerReview.None || peerSaved == null)
                throw new Exception("Matching incoming confirmation did not finish its own saved-copy review.");
        }
        internal void CheckSavedForTest()
        {
            if (peerSaved == null || batchImported?.Single() != peerSaved || peerReview != PeerReview.Saved || peerStatus.text == null)
                throw new Exception("Production save did not publish local saved state.");
        }
        internal static void DecodeForTest(byte[] bytes, P.Offer offer) => DecodePeerDocument(bytes, offer);
        private void StartPeer(P.Command command, Func<Task<BlueprintPeerClient.Result>> start)
        {
            if (command != P.Command.Accept) throw new Exception("Unexpected test dispatch.");
            ++AckStarts; BeforeAck?.Invoke(); Ack = start();
        }
        private static string T(string key, params object[] values) => key;
        private static string ShareWarning(B value) => value.ShareError ?? "";
        private void SetPeerDetails(string text) { }
    }
}

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PeerUiFlowChecks
    {
        internal static BlueprintExchangeView Prepare(string folder, B store, BlueprintPeerClient recipient,
            BlueprintServerClient library, P.Offer offer, byte[] bytes)
        {
            BlueprintExchangeView.DecodeForTest(bytes, offer);
            foreach (var change in new Action<P.Offer>[] { o => o.Name += "x", o => o.DocumentId = Guid.NewGuid(), o => o.Parts++, o => o.FormatVersion--, o => o.Bytes++, o => o.Sha256 = new string('0',64) })
            {
                var invalid = P.Snapshot(offer); change(invalid);
                Refuse(() => BlueprintExchangeView.DecodeForTest(bytes, invalid));
            }
            var view = new BlueprintExchangeView(store, recipient, library, offer, bytes);
            view.CheckRecipientNamesForTest();
            Require(store.All().Count == 0 && view.AckStarts == 0, "Preview implicitly saved or acknowledged.");
            view.HostCurrent = false; Refuse(view.AcceptForTest); view.HostCurrent = true;
            view.Connected = false; Refuse(view.AcceptForTest); view.Connected = true;
            var foreign = P.Snapshot(offer); foreign.Recipient = "another payer";
            Refuse(new BlueprintExchangeView(store, recipient, library, foreign, bytes).AcceptForTest);
            var closed = new BlueprintExchangeView(store, recipient, library, offer, bytes); closed.CloseForTest(); Refuse(closed.AcceptForTest);
            Require(store.All().Count == 0 && view.AckStarts == 0, "Refused consent wrote or acknowledged.");
            string path = Path.Combine(folder, "peer-ui-rollback.json");
            var rollback = new B(path); Directory.CreateDirectory(path + ".tmp");
            var failed = new BlueprintExchangeView(rollback, recipient, library, offer, bytes); Refuse(failed.AcceptForTest);
            Require(failed.AckStarts == 0 && rollback.All().Count == 0 && !File.Exists(path), "Failed primary write acknowledged or retained a card.");
            Directory.Delete(path + ".tmp");
            view.BeforeAck = () => { view.CheckSavedForTest(); Require(new B(Path.Combine(folder, "recipient.json")).All().Single().id == view.Saved.id, "ACK preceded durable Store."); };
            Console.WriteLine("PASS extracted production peer decode/context/Save: metadata refusals, preview has no effects, stale host/connection/recipient/closed refused, actual disk failure has no ACK.");
            return view;
        }
        private static void Refuse(Action action)
        { try { action(); } catch (Exception error) when (error is InvalidDataException || error is InvalidOperationException || error is IOException) { return; } throw new Exception("Expected refusal."); }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
