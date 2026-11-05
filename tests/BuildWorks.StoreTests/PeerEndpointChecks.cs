using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;
using P = OstrixMods.BuildWorks.BlueprintPeerProtocol;
using S = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PeerEndpointChecks
    {
        private sealed class Connection
        {
            internal string Principal = "Steam_same", Name = "Игрок";
            internal bool Connected = true;
            internal Guid Session;
            internal readonly List<P.Message> Replies = new List<P.Message>();
        }
        internal static void Run(string folder)
        {
            var source = new B(Path.Combine(folder, "source.json"));
            Require(source.TrySaveDocument("Дом", "QA", new[] { new B.Part { stableId = "a", prefabName = "woodwall", displayName = "Стена",
                position = new B.VectorData(), rotation = new B.QuaternionData { w = 1 }, scale = new B.VectorData { x = 1, y = 1, z = 1 },
                baseScale = new B.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true } }, Array.Empty<B.Group>(),
                Array.Empty<B.VectorData>(), "a", null, out var document, out string error), error);
            document.peerOrigins = new List<B.PeerOrigin> { new B.PeerOrigin { server = "claimed-server", worldUid = 1,
                sender = "Forged_sender", recipient = "Forged_recipient", offerId = Guid.NewGuid().ToString("N"),
                documentId = document.id, sha256 = new string('A', 64), formatVersion = BlueprintExchangeFormat.Version } };
            byte[] bytes = BlueprintExchangeFormat.Encode(document);
            var budget = new BlueprintTransferBudget(bytes.Length * 4);
            var a = new Connection { Principal = "Steam_sender" }; var b = new Connection(); var c = new Connection();
            double now = 0; bool loseAcceptedReply = false;
            using (var endpoint = new BlueprintPeerEndpoint(71, budget,
                o => { var p = (Connection)o; return p.Connected ? (p.Principal, p.Name) : (null, null); },
                (o, frame) => { var reply = P.Decode(frame); ((Connection)o).Replies.Add(reply);
                    if (loseAcceptedReply && reply.Offer?.State == P.OfferState.Accepted) { loseAcceptedReply = false; throw new IOException("Lost ACK reply"); } }))
            {
                Hello(a); Hello(b); Hello(c); Guid session = a.Session; Hello(a);
                Require(a.Session == session, "Hello reset existing session");
                var upload = Upload(a, b, bytes); Pump(() => a.Replies.Any(r => r.Request == upload && r.Kind == P.Command.Offered));
                var offer = a.Replies.Last(r => r.Kind == P.Command.Offered).Offer;
                Require(offer.Sender == a.Principal && offer.Recipient == b.Principal && offer.SenderSession == a.Session && offer.RecipientSession == b.Session,
                    "Offer identity not derived from authenticated sessions");
                Require(budget.Reserved == bytes.Length * 2, "Retained offer unaccounted");
                var retry = Send(a, new P.Message { Kind = P.Command.Commit, Request = upload });
                Require(Last(a, retry).Offer.Id == offer.Id && endpoint.RecordCount == 1, "Commit replay duplicated offer");
                var denied = Send(c, new P.Message { Kind = P.Command.Read, Id = offer.Id });
                Require(Last(c, denied).Error == S.Failure.Unavailable, "Same principal different connection read recipient offer");
                denied = Send(a, new P.Message { Kind = P.Command.Accept, Id = offer.Id });
                Require(Last(a, denied).Error == S.Failure.Denied, "Sender accepted own offer");
                denied = Send(b, new P.Message { Kind = P.Command.Accept, Id = offer.Id });
                Require(Last(b, denied).Error == S.Failure.Invalid && budget.Reserved > 0, "Accept before completed delivery changed offer");
                Guid inbox = Send(c, new P.Message { Kind = P.Command.Inbox }); Require(Last(c, inbox).Offers.Length == 0, "Inbox leaked recipient metadata");
                var read = Send(b, new P.Message { Kind = P.Command.Read, Id = offer.Id });
                Pump(() => b.Replies.Any(r => r.Request == read && r.Kind == P.Command.Commit));
                byte[] delivered;
                using (var transfer = new S.Transfer(offer.Bytes, offer.Sha256))
                {
                    foreach (var chunk in b.Replies.Where(r => r.Request == read && r.Kind == P.Command.Chunk)) transfer.Append(chunk.Offset, chunk.Bytes);
                    delivered = transfer.Complete();
                }
                Require(delivered.SequenceEqual(bytes), "Peer delivery altered portable bytes");
                string blockedPath = Path.Combine(folder, "blocked.json"); Directory.CreateDirectory(blockedPath);
                var blocked = new B(blockedPath);
                var store = new B(Path.Combine(folder, "recipient.json"));
                var origin = new B.PeerOrigin { server = "fixture-server", worldUid = 71, sender = offer.Sender, recipient = offer.Recipient,
                    offerId = offer.Id.ToString("N"), documentId = offer.DocumentId.ToString("N"), sha256 = offer.Sha256, formatVersion = offer.FormatVersion };
                Require(!blocked.TrySaveAcceptedPeerCopy(delivered, origin, out _, out _), "Blocked local Save unexpectedly succeeded");
                var notAccepted = Send(b, new P.Message { Kind = P.Command.Status, Id = offer.Id });
                Require(Last(b, notAccepted).Offer.State == P.OfferState.Pending && budget.Reserved > 0, "Failed local Save implicitly accepted offer");
                Require(store.TrySaveAcceptedPeerCopy(delivered, origin, out var copy, out error), error);
                string saved = File.ReadAllText(Path.Combine(folder, "recipient.json")); int revision = store.Revision;
                loseAcceptedReply = true; Send(b, new P.Message { Kind = P.Command.Accept, Id = offer.Id });
                var accepted = Send(b, new P.Message { Kind = P.Command.Accept, Id = offer.Id });
                Require(Last(b, accepted).Offer.State == P.OfferState.Accepted && budget.Reserved == 0, "Lost ACK retry did not settle once");
                Require(new B(Path.Combine(folder, "recipient.json")).TrySaveAcceptedPeerCopy(delivered, origin, out var replay, out error) &&
                    copy.id == replay.id && store.Revision == revision && saved == File.ReadAllText(Path.Combine(folder, "recipient.json")), "ACK retry rewrote durable copy/receipt");
                var withdraw = Send(a, new P.Message { Kind = P.Command.Withdraw, Id = offer.Id });
                Require(Last(a, withdraw).Offer.State == P.OfferState.Accepted && File.ReadAllText(Path.Combine(folder, "recipient.json")) == saved,
                    "Late withdraw reversed committed acceptance");
                var malformed = Upload(a, b, new byte[] { 1, 2, 3 });
                Pump(() => a.Replies.Any(r => r.Request == malformed && r.Kind == P.Command.Error));
                Require(budget.Reserved == 0, "Portable rejection leaked quota");
                var noncanonical = document.ImportCopy(); noncanonical.id = "ABCDEF0123456789ABCDEF0123456789";
                byte[] noncanonicalBytes = BlueprintExchangeFormat.Encode(noncanonical);
                var incompatibleOrigin = new B.PeerOrigin { server = "fixture-server", worldUid = 71, sender = a.Principal, recipient = b.Principal,
                    offerId = Guid.NewGuid().ToString("N"), documentId = noncanonical.id.ToLowerInvariant(), sha256 = Hash(noncanonicalBytes), formatVersion = BlueprintExchangeFormat.Version };
                Require(!new B(Path.Combine(folder, "noncanonical.json")).TrySaveAcceptedPeerCopy(noncanonicalBytes, incompatibleOrigin, out _, out _),
                    "Noncanonical ID fixture no longer demonstrates exact Store mismatch");
                var incompatible = Upload(a, b, noncanonicalBytes);
                Pump(() => a.Replies.Any(r => r.Request == incompatible && (r.Kind == P.Command.Error || r.Kind == P.Command.Offered)));
                Require(Last(a, incompatible).Kind == P.Command.Error && budget.Reserved == 0, "Endpoint published noncanonical ID that recipient Store cannot accept");
                var pending = Upload(a, b, bytes); Pump(() => a.Replies.Any(r => r.Request == pending && r.Kind == P.Command.Offered));
                var next = Last(a, pending).Offer;
                Send(b, new P.Message { Kind = P.Command.Read, Id = next.Id });
                Send(a, new P.Message { Kind = P.Command.Withdraw, Id = next.Id }); endpoint.Tick(now);
                var status = Send(b, new P.Message { Kind = P.Command.Status, Id = next.Id });
                Require(Last(b, status).Offer.State == P.OfferState.Withdrawn && budget.Reserved == 0, "Withdraw retained delivery/payload");
                var declinedUpload = Upload(a, b, bytes); Pump(() => a.Replies.Any(r => r.Request == declinedUpload && r.Kind == P.Command.Offered));
                var declinedOffer = Last(a, declinedUpload).Offer;
                var declined = Send(b, new P.Message { Kind = P.Command.Decline, Id = declinedOffer.Id });
                Require(Last(b, declined).Offer.State == P.OfferState.Declined && budget.Reserved == 0, "Decline retained payload");
                var lateAccept = Send(b, new P.Message { Kind = P.Command.Accept, Id = declinedOffer.Id });
                Require(Last(b, lateAccept).Offer.State == P.OfferState.Declined, "Late ACK resurrected declined offer");
                Guid stale = b.Session; endpoint.Disconnect(b); Hello(b);
                var staleRequest = Guid.NewGuid(); endpoint.Receive(b, P.Encode(new P.Message { Kind = P.Command.Inbox, World = 71, Session = stale, Request = staleRequest }), now += 1);
                Require(b.Session != stale && Last(b, staleRequest).Error == S.Failure.Invalid, "Reconnected session reused old handle");
                var capacity = new P.Message { Kind = P.Command.OfferStart, Target = b.Session, Length = bytes.Length, Sha256 = Hash(bytes) };
                Guid active = Send(a, capacity); Require(budget.Reserved > 0, "Valid start did not reserve");
                Send(a, new P.Message { Kind = P.Command.Chunk, Request = active, Offset = 1, Bytes = new byte[] { 1 } });
                Require(budget.Reserved == 0, "Out-of-order chunk pinned reservation");
                for (int i = endpoint.RecordCount; i < P.MaximumOfferRecords; ++i)
                { var request = Send(a, new P.Message { Kind = P.Command.OfferStart, Target = b.Session, Length = 1, Sha256 = Hash(new byte[] { 1 }) }); Send(a, new P.Message { Kind = P.Command.Cancel, Request = request }); }
                var full = Send(a, new P.Message { Kind = P.Command.OfferStart, Target = b.Session, Length = 1, Sha256 = Hash(new byte[] { 1 }) });
                Require(Last(a, full).Error == S.Failure.Capacity && endpoint.RecordCount == P.MaximumOfferRecords && budget.Reserved == 0, "Replay tombstones silently evicted or exceeded bound");

                Guid Upload(Connection sender, Connection recipient, byte[] payload)
                {
                    var id = Send(sender, new P.Message { Kind = P.Command.OfferStart, Target = recipient.Session, Length = payload.Length, Sha256 = Hash(payload) });
                    for (int offset = 0; offset < payload.Length; offset += S.ChunkBytes)
                        Send(sender, new P.Message { Kind = P.Command.Chunk, Request = id, Offset = offset, Bytes = payload.Skip(offset).Take(S.ChunkBytes).ToArray() });
                    Send(sender, new P.Message { Kind = P.Command.Commit, Request = id }); return id;
                }
                Guid Send(Connection connection, P.Message message)
                {
                    if (message.Request == Guid.Empty) message.Request = Guid.NewGuid(); message.World = 71;
                    message.Session = message.Kind == P.Command.Hello ? Guid.Empty : connection.Session;
                    endpoint.Receive(connection, P.Encode(message), now += .3); return message.Request;
                }
                void Hello(Connection connection)
                { var request = Send(connection, new P.Message { Kind = P.Command.Hello }); connection.Session = Last(connection, request).Session; }
                void Pump(Func<bool> done)
                {
                    for (int i = 0; i < 5000; ++i) { endpoint.Tick(now += .001); if (done()) return; Thread.Sleep(1); }
                    throw new Exception("Peer endpoint fixture timed out");
                }
            }
            Retirement(bytes);
            Boundaries(bytes);
            Streaming(document);
            Console.WriteLine("PASS peer endpoint: exact sessions, SHA/portable validation, immutable delivery, real Store receipt then ACK/lost reply, withdraw, reconnect denial, ordering and bounded tombstones; blocked worker retirement. CPU only, no native transport/UI/network proof.");
        }
        private static void Retirement(byte[] bytes)
        {
            foreach (bool dispose in new[] { false, true })
            {
                var gate = new TaskCompletionSource<bool>(); var budget = new BlueprintTransferBudget(bytes.Length * 2);
                var a = new Connection(); var b = new Connection();
                using (var endpoint = new BlueprintPeerEndpoint(71, budget, _ => ("Steam", "Игрок"),
                    (c, f) => ((Connection)c).Replies.Add(P.Decode(f)), data => gate.Task.ContinueWith(_ => BlueprintPeerEndpoint.Validate(data))))
                {
                    double now = 0;
                    Guid Send(Connection c, P.Message m)
                    { m.World = 71; m.Request = m.Request == Guid.Empty ? Guid.NewGuid() : m.Request; m.Session = m.Kind == P.Command.Hello ? Guid.Empty : c.Session;
                        endpoint.Receive(c, P.Encode(m), now += .3); return m.Request; }
                    foreach (var c in new[] { a, b }) { var hello = Send(c, new P.Message { Kind = P.Command.Hello }); c.Session = Last(c, hello).Session; }
                    var id = Send(a, new P.Message { Kind = P.Command.OfferStart, Target = b.Session, Length = bytes.Length, Sha256 = Hash(bytes) });
                    Send(a, new P.Message { Kind = P.Command.Chunk, Request = id, Bytes = bytes });
                    Send(a, new P.Message { Kind = P.Command.Commit, Request = id });
                    if (dispose) endpoint.Dispose(); else endpoint.Disconnect(a);
                    Require(endpoint.HasPendingWork && budget.Reserved == bytes.Length * 2 && !budget.TryReserve(1), "Blocked retired validator prematurely freed shared quota");
                    gate.SetResult(true);
                    Require(SpinWait.SpinUntil(() => budget.Reserved == 0, 5000) && endpoint.ReservedBytes == 0, "Finished retired worker leaked quota");
                    endpoint.Tick(now); endpoint.Dispose(); Require(budget.Reserved == 0, "Retired cleanup released quota twice");
                }
            }
        }
        private static void Boundaries(byte[] bytes)
        {
            var budget = new BlueprintTransferBudget(bytes.Length * 40L);
            var a = new Connection(); var b = new Connection(); double now = 0;
            using (var endpoint = new BlueprintPeerEndpoint(71, budget,
                o => ((Connection)o).Connected ? (((Connection)o).Principal, ((Connection)o).Name) : (null, null),
                (c, frame) => ((Connection)c).Replies.Add(P.Decode(frame))))
            {
                var unknown = new Connection();
                byte[] preHello = P.Encode(new P.Message { Kind = P.Command.Chunk, World = 71, Request = Guid.NewGuid(), Session = Guid.NewGuid(), Bytes = new byte[S.ChunkBytes] });
                for (int i = 0; i < 100; ++i) endpoint.Receive(unknown, preHello, now);
                Require(unknown.Replies.Count == 0 && endpoint.RecordCount == 0 && endpoint.ReservedBytes == 0, "Pre-Hello bulk admitted endpoint state");
                foreach (var c in new[] { a, b }) Hello(c);
                Guid Send(Connection c, P.Message m)
                { m.World = 71; m.Request = m.Request == Guid.Empty ? Guid.NewGuid() : m.Request; m.Session = m.Kind == P.Command.Hello ? Guid.Empty : c.Session;
                    endpoint.Receive(c, P.Encode(m), now += .3); return m.Request; }
                void Hello(Connection c) { var id = Send(c, new P.Message { Kind = P.Command.Hello }); c.Session = Last(c, id).Session; }
                P.Message Start(Connection recipient) => new P.Message { Kind = P.Command.OfferStart, Target = recipient.Session, Length = bytes.Length, Sha256 = Hash(bytes) };
                Guid wrongWorld = Guid.NewGuid();
                endpoint.Receive(a, P.Encode(new P.Message { Kind = P.Command.Roster, Request = wrongWorld, World = 72, Session = a.Session }), now += 1);
                Require(Last(a, wrongWorld).Error == S.Failure.Invalid && endpoint.RecordCount == 0, "Wrong world accessed endpoint state");
                var missing = Start(b); missing.Target = Guid.NewGuid();
                var refused = Send(a, missing); Require(Last(a, refused).Error == S.Failure.Unavailable && budget.Reserved == 0, "Unknown target allocated upload");
                Require(budget.TryReserve(bytes.Length * 40L), "External shared reservation failed");
                refused = Send(a, Start(b)); Require(Last(a, refused).Error == S.Failure.Capacity && endpoint.ReservedBytes == 0, "Peer channel bypassed external reservation");
                budget.Release(bytes.Length * 40L);
                var header = Start(b); var id = Send(a, header); long held = budget.Reserved;
                Send(a, header); Require(budget.Reserved == held && endpoint.RecordCount == 1, "Repeated identical header allocated twice");
                var conflict = Start(b); conflict.Request = id; conflict.Sha256 = new string('0', 64);
                refused = Send(a, conflict); Require(Last(a, refused).Error == S.Failure.Conflict && budget.Reserved == held, "Changed header replaced request");
                Send(a, new P.Message { Kind = P.Command.Cancel, Request = id }); Require(budget.Reserved == 0, "Cancel leaked upload");
                id = Send(a, Start(b)); now += 31; endpoint.Tick(now);
                Send(a, new P.Message { Kind = P.Command.Commit, Request = id });
                Require(Last(a, id).Error == S.Failure.Timeout && budget.Reserved == 0, "Upload timeout did not retain failed replay");
                header = Start(b); header.Sha256 = new string('0', 64); id = Send(a, header);
                Send(a, new P.Message { Kind = P.Command.Chunk, Request = id, Bytes = bytes });
                Send(a, new P.Message { Kind = P.Command.Commit, Request = id });
                Require(Last(a, id).Error == S.Failure.Invalid && budget.Reserved == 0, "Wrong SHA created offer or leaked quota");
                id = Send(a, Start(b)); Send(a, new P.Message { Kind = P.Command.Chunk, Request = id, Bytes = bytes });
                Send(a, new P.Message { Kind = P.Command.Commit, Request = id });
                for (int i = 0; !a.Replies.Any(r => r.Request == id && r.Kind == P.Command.Offered); ++i)
                { Require(i < 5000, "Offer validation timeout"); endpoint.Tick(now += .001); Thread.Sleep(1); }
                var offer = Last(a, id).Offer; now += 301;
                var expired = Send(b, new P.Message { Kind = P.Command.Accept, Id = offer.Id });
                Require(Last(b, expired).Offer.State == P.OfferState.Expired && budget.Reserved == 0, "Accept extended expired offer before Tick");
                var senders = Enumerable.Range(0, P.MaximumPendingOffers + 1).Select(_ => new Connection()).ToArray();
                foreach (var c in senders) Hello(c);
                foreach (var c in senders.Take(P.MaximumPendingOffers)) Send(c, Start(b));
                refused = Send(senders.Last(), Start(b));
                Require(Last(senders.Last(), refused).Error == S.Failure.Capacity && budget.Reserved == P.MaximumPendingOffers * bytes.Length * 2L,
                    "Incomplete uploads bypassed global pending quota");
                foreach (var c in senders) endpoint.Disconnect(c);
                Require(budget.Reserved == 0, "Disconnect leaked pending uploads");
                a.Connected = false; Send(b, new P.Message { Kind = P.Command.Roster });
                Require(!b.Replies.Last().Participants.Any(p => p.Session == a.Session), "Roster retained unauthenticated participant");
                var spam = new Connection(); Hello(spam); int before = spam.Replies.Count;
                for (int i = 0; i < 100; ++i) endpoint.Receive(spam, new byte[] { 0 }, now);
                Require(spam.Replies.Count - before <= 16 && budget.Reserved == 0, "Malformed flood bypassed control rate limit");
            }
            Require(budget.Reserved == 0, "Dispose leaked boundaries fixture quota");
        }
        private static void Streaming(B.Blueprint original)
        {
            var document = original.ImportCopy();
            document.primaryPartId = "p0";
            byte[] bytes = null;
            for (int count = 1000; count <= B.MaximumParts; count += 1000)
            {
                document.parts = Enumerable.Range(0, count).Select(i => { var part = B.CloneParts(original.parts)[0]; part.stableId = "p" + i; return part; }).ToList();
                bytes = BlueprintExchangeFormat.Encode(document);
                Console.WriteLine("Peer streaming fixture bytes=" + bytes.Length + ", parts=" + count);
                if (bytes.Length > S.ChunkBytes * 4) break;
            }
            Require(bytes.Length > S.ChunkBytes * 4, "Streaming fixture does not cross per-Tick quota");
            var budget = new BlueprintTransferBudget(bytes.Length * 2L); var a = new Connection(); var b = new Connection();
            Action<Connection, P.Message> onReply = null;
            using (var endpoint = new BlueprintPeerEndpoint(71, budget, _ => ("Steam", "Игрок"),
                (c, f) => { var message = P.Decode(f); ((Connection)c).Replies.Add(message); onReply?.Invoke((Connection)c, message); }))
            {
                double now = 0;
                Guid Send(Connection c, P.Message m)
                { m.World = 71; m.Request = m.Request == Guid.Empty ? Guid.NewGuid() : m.Request; m.Session = m.Kind == P.Command.Hello ? Guid.Empty : c.Session;
                    endpoint.Receive(c, P.Encode(m), now += .3); return m.Request; }
                foreach (var c in new[] { a, b }) { var hello = Send(c, new P.Message { Kind = P.Command.Hello }); c.Session = Last(c, hello).Session; }
                var upload = Send(a, new P.Message { Kind = P.Command.OfferStart, Target = b.Session, Length = bytes.Length, Sha256 = Hash(bytes) });
                for (int offset = 0; offset < bytes.Length; offset += S.ChunkBytes)
                    Send(a, new P.Message { Kind = P.Command.Chunk, Request = upload, Offset = offset, Bytes = bytes.Skip(offset).Take(S.ChunkBytes).ToArray() });
                Send(a, new P.Message { Kind = P.Command.Commit, Request = upload });
                for (int i = 0; !a.Replies.Any(r => r.Request == upload && r.Kind == P.Command.Offered); ++i)
                { Require(i < 5000, "Streaming validator timeout"); endpoint.Tick(now += .001); Thread.Sleep(1); }
                var offer = Last(a, upload).Offer;
                var read = Send(b, new P.Message { Kind = P.Command.Read, Id = offer.Id }); endpoint.Tick(now += .001);
                Require(b.Replies.Count(r => r.Request == read && r.Kind == P.Command.Chunk) == 4 && !b.Replies.Any(r => r.Request == read && r.Kind == P.Command.Commit),
                    "Read exceeded bounded per-Tick chunk quota");
                Send(b, new P.Message { Kind = P.Command.Cancel, Request = read });
                Require(budget.Reserved == bytes.Length * 2L, "Cancelling read freed pending offer payload");
                read = Send(b, new P.Message { Kind = P.Command.Read, Id = offer.Id });
                for (int i = 0; !b.Replies.Any(r => r.Request == read && r.Kind == P.Command.Commit); ++i)
                { Require(i < 100, "Streaming read timeout"); endpoint.Tick(now += .001); }
                using (var transfer = new S.Transfer(bytes.Length, offer.Sha256))
                {
                    foreach (var chunk in b.Replies.Where(r => r.Request == read && r.Kind == P.Command.Chunk)) transfer.Append(chunk.Offset, chunk.Bytes);
                    Require(transfer.Complete().SequenceEqual(bytes), "Restarted multi-Tick delivery changed bytes/order");
                }
                foreach (bool throwAfterRestart in new[] { false, true })
                {
                    Guid oldRead = Send(b, new P.Message { Kind = P.Command.Read, Id = offer.Id }), replacement = Guid.Empty;
                    onReply = (connection, message) =>
                    {
                        if (connection != b || message.Kind != P.Command.Chunk || message.Request != oldRead || replacement != Guid.Empty) return;
                        Send(b, new P.Message { Kind = P.Command.Cancel, Request = oldRead });
                        replacement = Send(b, new P.Message { Kind = P.Command.Read, Id = offer.Id });
                        if (throwAfterRestart) throw new IOException("Reply queued then read replaced");
                    };
                    endpoint.Tick(now += .001); onReply = null;
                    Require(replacement != Guid.Empty && b.Replies.Count(r => r.Request == oldRead && r.Kind == P.Command.Chunk) == 1 &&
                        !b.Replies.Any(r => r.Request == oldRead && r.Kind == P.Command.Commit), "Reentrant read reused old generation or cleared replacement");
                    for (int i = 0; !b.Replies.Any(r => r.Request == replacement && r.Kind == P.Command.Commit); ++i)
                    { Require(i < 100, "Reentrant replacement read did not complete"); endpoint.Tick(now += .001); }
                    using (var transfer = new S.Transfer(bytes.Length, offer.Sha256))
                    {
                        foreach (var chunk in b.Replies.Where(r => r.Request == replacement && r.Kind == P.Command.Chunk)) transfer.Append(chunk.Offset, chunk.Bytes);
                        Require(transfer.Complete().SequenceEqual(bytes), "Reentrant replacement chunks duplicated/lost offsets");
                    }
                }
                var ack = Send(b, new P.Message { Kind = P.Command.Accept, Id = offer.Id });
                Require(Last(b, ack).Offer.State == P.OfferState.Accepted && budget.Reserved == 0, "Streaming ACK did not release payload");
            }
        }
        private static P.Message Last(Connection c, Guid id) => c.Replies.Last(r => r.Request == id);
        private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
