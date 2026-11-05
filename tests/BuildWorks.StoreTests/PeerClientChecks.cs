using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;
using P = OstrixMods.BuildWorks.BlueprintPeerProtocol;
using S = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PeerClientChecks
    {
        internal static void Run(string folder)
        {
            var store = new B(Path.Combine(folder, "source.json"));
            Require(store.TrySaveDocument("Дом", "QA", new[] { new B.Part { stableId = "a", prefabName = "woodwall", displayName = "Стена",
                position = new B.VectorData(), rotation = new B.QuaternionData { w = 1 }, scale = new B.VectorData { x = 1, y = 1, z = 1 },
                baseScale = new B.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true } }, Array.Empty<B.Group>(),
                Array.Empty<B.VectorData>(), "a", null, out var document, out string error), error);
            byte[] original = BlueprintExchangeFormat.Encode(document);
            double now = 1;
            var errors = new List<Exception>();
            var host = ZNet.instance = new ZNet { World = 901 };
            Player.m_localPlayer = new Player();
            var a = Peer("sender", "Отправитель", 1); var b = Peer("recipient", "Получатель", 2);
            var c = Peer("recipient", "<Administrator>", 2); // Neither display name nor routed UID is identity.
            host.Peers.AddRange(new[] { a, b, c });
            using (var runtime = new BlueprintServerRuntime(folder, errors.Add))
            using (var sender = new BlueprintPeerClient(host.World, bytes => Deliver(a, bytes)))
            using (var recipient = new BlueprintPeerClient(host.World, bytes => Deliver(b, bytes)))
            using (var other = new BlueprintPeerClient(host.World, bytes => Deliver(c, bytes)))
            {
                a.m_rpc.Sending = (name, bytes) => Reply(name, bytes, sender);
                b.m_rpc.Sending = (name, bytes) => Reply(name, bytes, recipient);
                c.m_rpc.Sending = (name, bytes) => Reply(name, bytes, other);
                runtime.Tick(host, now);
                Require(runtime.PeerClient != null && a.m_rpc.Handlers.ContainsKey(BlueprintServerRuntime.PeerRequestRpc), "Peer runtime was not registered");
                sender.Hello().GetAwaiter().GetResult(); recipient.Hello().GetAwaiter().GetResult(); other.Hello().GetAwaiter().GetResult();
                runtime.PeerClient.Hello().GetAwaiter().GetResult();
                Require(sender.Principal == "sender" && recipient.Principal == other.Principal && recipient.Session != other.Session, "Peer session/identity binding changed");
                now += .3;
                var roster = sender.Roster().Result.Reply.Participants;
                Require(roster.Length == 4 && roster.Single(p => p.Session == recipient.Session).Name == "Получатель" &&
                    roster.Single(p => p.Session == other.Session).Name == "Player", "Roster lost native names or accepted markup");
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                object library = typeof(BlueprintServerRuntime).GetField("endpoint", flags).GetValue(runtime);
                object endpoint = typeof(BlueprintServerRuntime).GetField("peerEndpoint", flags).GetValue(runtime);
                Require(ReferenceEquals(typeof(BlueprintServerEndpoint).GetField("transferBudget", flags).GetValue(library),
                    typeof(BlueprintPeerEndpoint).GetField("budget", flags).GetValue(endpoint)), "Runtime did not share the transfer budget");

                byte[] mutable = (byte[])original.Clone();
                now += .3; var upload = sender.Offer(recipient.Session, mutable); mutable[0] ^= 1;
                Pump(upload);
                var offer = upload.Result.Reply.Offer;
                Require(offer.DocumentId.ToString("N") == document.id && offer.Sender == "sender" && offer.Recipient == "recipient", "Offer changed document or authenticated identities");
                now += .3; Require(other.Inbox().Result.Reply.Offers.Length == 0, "Same-principal other session saw recipient inbox");
                now += .3; Require(recipient.Inbox().Result.Reply.Offers.Single().Id == offer.Id, "Recipient inbox lost offer");
                Refused(() => recipient.Accept(offer), "ACK before completed read accepted");
                Refused(() => other.Read(offer), "Wrong session read accepted by client");
                now += .3; var cancelled = recipient.Read(offer); recipient.Cancel(cancelled);
                Require(Failure(cancelled) == S.Failure.Cancelled, "Read cancellation did not settle");
                now += .3; var expected = P.Snapshot(offer); var read = recipient.Read(expected); expected.Name = "changed after call";
                Pump(read);
                Require(read.Result.Document.SequenceEqual(original) && read.Result.Reply.Offer.Name == offer.Name, "Mutable upload/metadata corrupted read");
                string personal = Path.Combine(folder, "recipient.json");
                Require(!File.Exists(personal), "Transport read wrote a personal copy");
                now += .3; Require(sender.Status(offer).Result.Reply.Offer.State == P.OfferState.Pending, "Read implicitly accepted offer");
                var recipientStore = new B(personal);
                var ui = PeerUiFlowChecks.Prepare(folder, recipientStore, recipient, runtime.Client, offer, read.Result.Document);
                b.m_rpc.Sending = (name, bytes) => { }; // Server accepts, but its response is lost.
                now += .3; ui.AcceptForTest(); var copy = ui.Saved;
                Require(!ui.Ack.IsCompleted && ui.AckStarts == 1, "Lost ACK test completed unexpectedly");
                now += 31; recipient.Tick(now); sender.Tick(now); other.Tick(now); runtime.Tick(host, now);
                Require(Failure(ui.Ack) == S.Failure.Timeout && new B(personal).All().Single().id == copy.id, "Lost ACK erased local copy");
                ui.CheckForeignTerminalForTest();
                b.m_rpc.Sending = (name, bytes) => Reply(name, bytes, recipient);
                ui.HostCurrent = false; // An already saved receipt may be acknowledged without preparing another copy.
                now += .3; ui.AcceptForTest();
                Require(ui.Ack.Result.Reply.Offer.State == P.OfferState.Accepted && ui.Saved.id == copy.id && new B(personal).All().Count == 1,
                    "Explicit saved-copy ACK retry duplicated or lost the copy");
                ui.CheckIncomingTerminalForTest(ui.Ack.Result.Reply.Offer);
                now += .3; Require(recipient.Accept(offer).Result.Reply.Offer.State == P.OfferState.Accepted, "ACK retry changed terminal result");
                Require(new B(personal).All().Single().id == copy.id, "ACK changed durable local copy");

                now += .3; var withdrawUpload = sender.Offer(recipient.Session, original); Pump(withdrawUpload);
                var withdrawn = withdrawUpload.Result.Reply.Offer;
                now += .3; Require(sender.Withdraw(withdrawn).Result.Reply.Offer.State == P.OfferState.Withdrawn, "Withdraw failed");
                now += .3; Require(recipient.Read(withdrawn).Result.Document == null, "Terminal read fabricated bytes");
                var old = runtime.PeerClient;
                host.World = 902; runtime.Tick(host, now);
                PumpUntil(() => runtime.PeerClient != null);
                Require(!ReferenceEquals(old, runtime.PeerClient) && !old.Ready && !runtime.PeerClient.Ready, "World switch retained peer session");
                runtime.Suspend(); Require(runtime.PeerClient == null && a.m_rpc.Handlers.Count == 0, "Suspend retained peer callbacks");

                void Reply(string name, ZPackage package, BlueprintPeerClient client)
                { Require(name == BlueprintServerRuntime.PeerReplyRpc, "Peer reply used library channel"); client.Receive(package.GetArray(), now); }
                void Pump(Task<BlueprintPeerClient.Result> task)
                { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
                void PumpUntil(Func<bool> done)
                {
                    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                    while (!done())
                    { Require(DateTime.UtcNow < deadline, "Peer runtime did not settle"); now += .01; sender.Tick(now); recipient.Tick(now); other.Tick(now); runtime.Tick(host, now); Thread.Sleep(1); }
                }
            }
            Require(errors.Count == 0 && host.Peers.All(p => p.m_rpc.Handlers.Count == 0), "Server peer teardown failed");

            Player.m_localPlayer = null;
            var server = Peer("server", "Server", 8);
            var remote = ZNet.instance = new ZNet { Server = false, World = 903, ServerPeer = server };
            using (var runtime = new BlueprintServerRuntime(folder, errors.Add))
            {
                var sent = new List<P.Message>();
                server.m_rpc.Sending = (name, bytes) => { Require(name == BlueprintServerRuntime.PeerRequestRpc, "Remote peer request used library channel"); sent.Add(P.Decode(bytes.GetArray())); };
                runtime.Tick(remote, now); var client = runtime.PeerClient; var hello = client.Hello();
                var callback = server.m_rpc.Handler(BlueprintServerRuntime.PeerReplyRpc);
                byte[] welcome = P.Encode(new P.Message { Kind = P.Command.Welcome, World = remote.World, Request = sent.Last().Request,
                    Session = Guid.NewGuid(), Principal = "recipient", Name = "Получатель" });
                callback(new ZRpc { Socket = server.m_socket }, new ZPackage(welcome));
                Require(!hello.IsCompleted, "Forged server callback completed handshake");
                callback(server.m_rpc, new ZPackage(welcome)); Require(hello.Result.Reply.Kind == P.Command.Welcome && client.Ready, "Peer handshake failed");
                var roster = client.Roster(); var oversized = new ZPackage(new byte[S.MaximumFrameBytes + 1]); callback(server.m_rpc, oversized);
                Require(oversized.ArrayReads == 0 && Failure(roster) == S.Failure.Invalid && !client.Ready, "Oversized frame was copied/accepted");
                var pending = client.Hello(); server.m_rpc.Connected = false; runtime.Tick(remote, now);
                Require(runtime.PeerClient == null && Failure(pending) == S.Failure.Unavailable && server.m_rpc.Handlers.Count == 0, "Disconnect retained peer task/callbacks");
                remote.ServerPeer = Peer("replacement", "Server2", 9); runtime.Tick(remote, now);
                Require(runtime.PeerClient != null && !ReferenceEquals(client, runtime.PeerClient), "Reconnect reused peer client");
                callback(server.m_rpc, new ZPackage(welcome)); Require(!runtime.PeerClient.Ready, "Old callback changed replacement peer session");
            }
            Require(errors.Count == 0 && remote.ServerPeer.m_rpc.Handlers.Count == 0, "Remote peer teardown failed");
            foreach (string broken in new[] { "metadata", "checksum", "offset", "session", "timeout" })
            {
                var sent = new List<P.Message>();
                using (var client = new BlueprintPeerClient(904, bytes => sent.Add(P.Decode(bytes))))
                {
                    var hello = client.Hello(); Guid session = Guid.NewGuid();
                    client.Receive(P.Encode(new P.Message { Kind = P.Command.Welcome, World = 904, Request = sent.Last().Request,
                        Session = session, Principal = "recipient", Name = "Player" }), 1);
                    hello.GetAwaiter().GetResult();
                    string hash; using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(original)).Replace("-", "");
                    var offer = new P.Offer { Id = Guid.NewGuid(), DocumentId = Guid.ParseExact(document.id, "N"), SenderSession = Guid.NewGuid(),
                        RecipientSession = session, Sender = "sender", Recipient = "recipient", Name = document.name, Bytes = original.Length,
                        Sha256 = hash, Parts = 1, FormatVersion = BlueprintExchangeFormat.Version, State = P.OfferState.Pending };
                    var reading = client.Read(offer); Guid request = sent.Last().Request;
                    var header = new P.Message { Kind = P.Command.DataStart, World = 904, Session = session, Request = request, Offer = P.Snapshot(offer) };
                    if (broken == "metadata") header.Offer.Parts++;
                    if (broken == "session") header.Session = Guid.NewGuid();
                    if (broken == "timeout") header.Request = Guid.NewGuid();
                    client.Receive(P.Encode(header), 1);
                    if (broken == "checksum" || broken == "offset")
                    {
                        byte[] bytes = (byte[])original.Clone(); if (broken == "checksum") bytes[0] ^= 1;
                        client.Receive(P.Encode(new P.Message { Kind = P.Command.Chunk, World = 904, Session = session, Request = request,
                            Offset = broken == "offset" ? 1 : 0, Bytes = bytes }), 1);
                        client.Receive(P.Encode(new P.Message { Kind = P.Command.Commit, World = 904, Session = session, Request = request }), 1);
                    }
                    if (broken == "timeout")
                    { Require(!reading.IsCompleted, "Unrelated request completed current read"); client.Tick(32); }
                    Require(reading.IsFaulted && Failure(reading) == (broken == "timeout" ? S.Failure.Timeout : S.Failure.Invalid), "Malformed reply passed: " + broken);
                    Require(client.Ready == (broken == "timeout"), "Invalid reply retained session or timeout forgot authenticated context");
                }
            }
            foreach (bool throwAfterReplacement in new[] { false, true })
            {
                BlueprintPeerClient client = null;
                Task<BlueprintPeerClient.Result> upload = null, replacement = null;
                var sent = new List<P.Message>(); bool entered = false;
                using (client = new BlueprintPeerClient(905, bytes =>
                {
                    var message = P.Decode(bytes); sent.Add(message);
                    if (message.Kind != P.Command.OfferStart) return;
                    entered = true; client.Cancel(upload); replacement = client.Roster();
                    if (throwAfterReplacement) throw new IOException("Old send failed after replacement");
                }))
                {
                    var hello = client.Hello(); Guid session = Guid.NewGuid();
                    client.Receive(P.Encode(new P.Message { Kind = P.Command.Welcome, World = 905, Request = sent.Last().Request,
                        Session = session, Principal = "sender", Name = "Sender" }), 1); hello.GetAwaiter().GetResult();
                    upload = client.Offer(Guid.NewGuid(), original); client.Tick(1);
                    Require(entered && Failure(upload) == S.Failure.Cancelled && replacement != null && !replacement.IsCompleted && client.Ready,
                        "Old upload callback cancelled its replacement request");
                    client.Receive(P.Encode(new P.Message { Kind = P.Command.RosterResult, World = 905, Session = session,
                        Request = sent.Last(m => m.Kind == P.Command.Roster).Request, Participants = Array.Empty<P.Participant>() }), 2);
                    Require(replacement.Result.Reply.Kind == P.Command.RosterResult && client.Ready, "Replacement roster failed after old callback");
                }
            }
            foreach (string mode in new[] { "begin", "invalid", "dispose" })
            {
                BlueprintPeerClient client = null; Task<BlueprintPeerClient.Result> replacement = null;
                var sent = new List<P.Message>(); bool entered = false, refused = false; Guid nextSession = Guid.NewGuid();
                using (client = new BlueprintPeerClient(906, bytes =>
                {
                    var message = P.Decode(bytes); sent.Add(message);
                    if (mode == "begin" && message.Kind == P.Command.Roster && !entered)
                    {
                        entered = true;
                        client.Receive(P.Encode(new P.Message { Kind = P.Command.RosterResult, World = 906, Session = client.Session,
                            Request = message.Request, Participants = Array.Empty<P.Participant>() }), 1);
                        replacement = client.Inbox(); throw new IOException("Completed Begin send failed");
                    }
                    if (message.Kind != P.Command.Cancel || entered) return;
                    entered = true;
                    if (mode == "dispose")
                    { try { client.Inbox(); } catch (InvalidOperationException) { refused = true; } }
                    else
                    {
                        replacement = client.Hello();
                        client.Receive(P.Encode(new P.Message { Kind = P.Command.Welcome, World = 906, Session = nextSession,
                            Request = sent.Last(m => m.Kind == P.Command.Hello).Request, Principal = "recipient", Name = "Player" }), 2);
                    }
                }))
                {
                    var hello = client.Hello(); Guid session = Guid.NewGuid();
                    client.Receive(P.Encode(new P.Message { Kind = P.Command.Welcome, World = 906, Session = session,
                        Request = sent.Last().Request, Principal = "recipient", Name = "Player" }), 1); hello.GetAwaiter().GetResult();
                    if (mode == "begin")
                    {
                        Require(client.Roster().Result.Reply.Kind == P.Command.RosterResult && !replacement.IsCompleted && client.Ready,
                            "Begin send error cancelled replacement");
                        client.Receive(P.Encode(new P.Message { Kind = P.Command.InboxResult, World = 906, Session = session,
                            Request = sent.Last(m => m.Kind == P.Command.Inbox).Request, Offers = Array.Empty<P.Offer>() }), 2);
                        replacement.GetAwaiter().GetResult();
                    }
                    else if (mode == "dispose")
                    {
                        var pending = client.Roster(); client.Dispose();
                        Require(refused && Failure(pending) == S.Failure.Unavailable && !client.Ready && !client.Busy, "Dispose allowed a new operation in its Cancel callback");
                    }
                    else
                    {
                        var pending = client.Inbox();
                        client.Receive(P.Encode(new P.Message { Kind = P.Command.RosterResult, World = 906, Session = session,
                            Request = sent.Last(m => m.Kind == P.Command.Inbox).Request, Participants = Array.Empty<P.Participant>() }), 1);
                        Require(Failure(pending) == S.Failure.Invalid && replacement.IsCompleted && client.Ready && client.Session == nextSession,
                            "Invalid old Receive forgot replacement Hello session");
                    }
                }
            }
            ZNet.instance = null;
            Console.WriteLine("PASS actual peer client/runtime on inert native boundary: names/sessions/shared budget, frozen upload, recipient-only read/cancel, explicit ACK after local fixture Save, terminal retry/withdraw, world switch, forged/oversized callbacks, reconnect/dispose. No live socket/game/UI proof.");
        }
        private static ZNetPeer Peer(string principal, string name, long uid)
        { var socket = new Socket { Host = principal }; return new ZNetPeer { m_playerName = name, m_uid = uid, m_socket = socket, m_rpc = new ZRpc { Socket = socket } }; }
        private static void Deliver(ZNetPeer peer, byte[] bytes) => peer.m_rpc.Handler(BlueprintServerRuntime.PeerRequestRpc)(peer.m_rpc, new ZPackage(bytes));
        private static S.Failure Failure(Task<BlueprintPeerClient.Result> task)
        { try { task.GetAwaiter().GetResult(); throw new Exception("Expected peer failure"); } catch (BlueprintPeerClient.Failure error) { return error.Code; } }
        private static void Refused(Action action, string reason)
        { try { action(); } catch (BlueprintPeerClient.Failure) { return; } throw new Exception(reason); }
        private static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    }
}
