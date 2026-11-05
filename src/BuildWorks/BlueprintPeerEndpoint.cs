using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using P = OstrixMods.BuildWorks.BlueprintPeerProtocol;
using S = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks
{
    // Session-scoped server offers. Accept is the recipient's ACK, not proof of its disk.
    internal sealed class BlueprintPeerEndpoint : IDisposable
    {
        private readonly long world;
        private readonly BlueprintTransferBudget budget;
        private readonly Func<object, (string Principal, string Name)> resolve;
        private readonly Action<object, byte[]> send;
        private readonly Func<byte[], Task<Description>> validate;
        private readonly Dictionary<object, Peer> peers = new Dictionary<object, Peer>();
        private readonly List<Operation> operations = new List<Operation>();
        private readonly List<Work> retired = new List<Work>();
        private bool disposed;
        private long reserved;
        internal long ReservedBytes => Interlocked.Read(ref reserved);
        internal bool HasPendingWork => retired.Any(w => !w.Task.IsCompleted) || operations.Any(o => o.Work?.Task != null);
        internal int RecordCount => operations.Count;
        private sealed class Peer
        {
            internal object Connection;
            internal string Principal, Name;
            internal Guid Session = Guid.NewGuid(), ReadRequest;
            internal Operation Upload, Reading;
            internal int Offset;
            internal double RateTime, Controls = 16, Chunks = 300;
        }
        private sealed class Work
        {
            internal byte[] Bytes;
            internal Task<Description> Task;
            internal long Reserved;
        }
        private sealed class Operation
        {
            internal Peer Sender, Recipient;
            internal P.Message Header;
            internal byte[] Frame;
            internal S.Transfer Transfer;
            internal Work Work;
            internal P.Offer Offer;
            internal S.Failure? Failure;
            internal bool Pending = true, Delivered;
            internal double Activity, Expires;
        }
        internal sealed class Description
        {
            internal readonly Guid Id;
            internal readonly string Name;
            internal readonly int Parts, Version;
            internal Description(Guid id, string name, int parts, int version)
            { Id = id; Name = name; Parts = parts; Version = version; }
        }
        internal BlueprintPeerEndpoint(long world, BlueprintTransferBudget budget,
            Func<object, (string Principal, string Name)> resolve, Action<object, byte[]> send,
            Func<byte[], Task<Description>> validate = null)
        {
            if (world == 0 || budget == null || resolve == null || send == null) throw new ArgumentException("Invalid peer endpoint.");
            this.world = world; this.budget = budget; this.resolve = resolve; this.send = send;
            this.validate = validate ?? (bytes => Task.Run(() => Validate(bytes)));
        }
        internal static Description Validate(byte[] bytes)
        {
            var document = BlueprintExchangeFormat.DecodeWithVersion(new UTF8Encoding(false, true).GetString(bytes), out int version);
            if (!Guid.TryParseExact(document.id, "N", out Guid id) || id == Guid.Empty || document.id != id.ToString("N") || document.parts.Count < 1)
                throw new InvalidDataException("Invalid peer document.");
            return new Description(id, P.Label(document.name, 48) ? document.name : id.ToString("N"), document.parts.Count, version);
        }
        private bool Current(Peer peer) => peers.TryGetValue(peer.Connection, out var current) && ReferenceEquals(current, peer) &&
            resolve(peer.Connection).Principal == peer.Principal;
        private void RefreshPeers()
        {
            foreach (var peer in peers.Values.ToArray())
                if (!Current(peer)) Disconnect(peer.Connection);
        }
        internal void Receive(object connection, byte[] frame, double now)
        {
            if (disposed || connection == null) return;
            RefreshPeers();
            Expire(now);
            var identity = resolve(connection);
            if (!P.Label(identity.Principal, 256)) { Disconnect(connection); return; }
            peers.TryGetValue(connection, out Peer peer);
            if (peer != null && !Admit(peer, frame, now)) return;
            if (peer == null && (frame == null || frame.Length != 42 || frame[0] != 1 || frame[1] != (byte)P.Command.Hello)) return;
            P.Message message;
            try { message = P.Decode(frame); }
            catch (InvalidDataException)
            { if (peer != null) { AbortUpload(peer, S.Failure.Invalid); Fail(peer, Guid.NewGuid(), S.Failure.Invalid); } return; }
            if (message.World != world) { if (peer != null) Fail(peer, message.Request, S.Failure.Invalid); return; }
            if (peer == null)
            {
                if (message.Kind != P.Command.Hello || peers.Count >= P.MaximumPeers) return;
                peer = new Peer { Connection = connection, Principal = identity.Principal,
                    Name = P.Label(identity.Name, 64) ? identity.Name : "Player", RateTime = now };
                peers.Add(connection, peer);
                if (!Admit(peer, frame, now)) return;
            }
            if (message.Kind != P.Command.Hello && message.Session != peer.Session)
            { Fail(peer, message.Request, S.Failure.Invalid); return; }
            try
            {
                switch (message.Kind)
                {
                    case P.Command.Hello:
                        Reply(peer, message.Request, P.Command.Welcome, m => { m.Principal = peer.Principal; m.Name = peer.Name; }); break;
                    case P.Command.Roster:
                        Reply(peer, message.Request, P.Command.RosterResult, m => m.Participants = peers.Values
                            .Select(p => new P.Participant { Session = p.Session, Name = p.Name }).ToArray()); break;
                    case P.Command.Inbox:
                        Reply(peer, message.Request, P.Command.InboxResult, m => m.Offers = operations.Where(o => o.Pending &&
                            o.Offer != null && ReferenceEquals(o.Recipient, peer)).Select(o => P.Snapshot(o.Offer)).ToArray()); break;
                    case P.Command.OfferStart: Start(peer, message, frame, now); break;
                    case P.Command.Chunk:
                        if (peer.Upload == null || peer.Upload.Header.Request != message.Request || peer.Upload.Transfer == null)
                        { Fail(peer, message.Request, S.Failure.Invalid); break; }
                        peer.Upload.Transfer.Append(message.Offset, message.Bytes); peer.Upload.Activity = now; break;
                    case P.Command.Commit: Commit(peer, message.Request, now); break;
                    case P.Command.Cancel:
                        if (peer.Upload?.Header.Request == message.Request) AbortUpload(peer, S.Failure.Cancelled);
                        if (peer.ReadRequest == message.Request) ClearRead(peer);
                        break;
                    case P.Command.Read: Read(peer, message); break;
                    case P.Command.Status: case P.Command.Accept: case P.Command.Decline: case P.Command.Withdraw:
                        Act(peer, message); break;
                    default: Fail(peer, message.Request, S.Failure.Invalid); break;
                }
            }
            catch (Exception error)
            {
                if (peer.Upload?.Header.Request == message.Request) AbortUpload(peer, S.Failure.Invalid);
                if (peer.ReadRequest == message.Request) ClearRead(peer);
                Fail(peer, message.Request, error is InvalidDataException || error is DecoderFallbackException ? S.Failure.Invalid : S.Failure.Failed);
            }
        }
        private static bool Admit(Peer peer, byte[] frame, double now)
        {
            double delta = Math.Max(0, now - peer.RateTime); peer.RateTime = now;
            peer.Controls = Math.Min(16, peer.Controls + delta * 4); peer.Chunks = Math.Min(300, peer.Chunks + delta * 300);
            if (frame != null && frame.Length > 1 && frame[1] == (byte)P.Command.Chunk)
            { if (peer.Chunks < 1) return false; --peer.Chunks; }
            else { if (peer.Controls < 1) return false; --peer.Controls; }
            return true;
        }
        private void Start(Peer peer, P.Message message, byte[] frame, double now)
        {
            var prior = operations.FirstOrDefault(o => ReferenceEquals(o.Sender, peer) && o.Header.Request == message.Request);
            if (prior != null)
            {
                if (!prior.Frame.SequenceEqual(frame)) Fail(peer, message.Request, S.Failure.Conflict);
                else Replay(peer, prior);
                return;
            }
            if (peer.Upload != null || peer.Reading != null) { Fail(peer, message.Request, S.Failure.Busy); return; }
            var recipient = peers.Values.SingleOrDefault(p => p.Session == message.Target);
            if (recipient == null || ReferenceEquals(recipient, peer)) { Fail(peer, message.Request, S.Failure.Unavailable); return; }
            if (operations.Count >= P.MaximumOfferRecords || operations.Count(o => o.Pending) >= P.MaximumPendingOffers)
            { Fail(peer, message.Request, S.Failure.Capacity); return; }
            long bytes = checked((long)message.Length * 2);
            if (!budget.TryReserve(bytes)) { Fail(peer, message.Request, S.Failure.Capacity); return; }
            var work = new Work { Reserved = bytes }; Interlocked.Add(ref reserved, bytes);
            try
            {
                var operation = new Operation { Sender = peer, Recipient = recipient, Header = message,
                    Frame = (byte[])frame.Clone(), Transfer = new S.Transfer(message.Length, message.Sha256), Work = work, Activity = now };
                operations.Add(operation); peer.Upload = operation;
            }
            catch { Cleanup(work); throw; }
        }
        private void Commit(Peer peer, Guid request, double now)
        {
            var operation = operations.FirstOrDefault(o => ReferenceEquals(o.Sender, peer) && o.Header.Request == request);
            if (operation == null) { Fail(peer, request, S.Failure.Invalid); return; }
            if (!ReferenceEquals(peer.Upload, operation) || operation.Work?.Task != null) { Replay(peer, operation); return; }
            operation.Work.Bytes = operation.Transfer.Complete(); operation.Transfer.Dispose(); operation.Transfer = null;
            operation.Activity = now; operation.Work.Task = validate(operation.Work.Bytes) ?? throw new InvalidOperationException("Missing validation task.");
        }
        private void Replay(Peer peer, Operation operation)
        {
            if (operation.Offer != null) Reply(peer, operation.Header.Request, P.Command.Offered, m => m.Offer = P.Snapshot(operation.Offer));
            else if (operation.Failure.HasValue) Fail(peer, operation.Header.Request, operation.Failure.Value);
        }
        private Operation Find(Peer peer, P.Message message)
        {
            var operation = operations.FirstOrDefault(o => o.Offer?.Id == message.Id);
            return operation != null && (ReferenceEquals(operation.Sender, peer) || ReferenceEquals(operation.Recipient, peer)) ? operation : null;
        }
        private void Read(Peer peer, P.Message message)
        {
            var operation = Find(peer, message);
            if (operation == null || !ReferenceEquals(operation.Recipient, peer)) { Fail(peer, message.Request, S.Failure.Unavailable); return; }
            if (!operation.Pending) { Status(peer, message.Request, operation); return; }
            if (peer.Upload != null || peer.Reading != null) { Fail(peer, message.Request, S.Failure.Busy); return; }
            peer.Reading = operation; peer.ReadRequest = message.Request; peer.Offset = 0;
            Reply(peer, message.Request, P.Command.DataStart, m => m.Offer = P.Snapshot(operation.Offer));
        }
        private void Act(Peer peer, P.Message message)
        {
            var operation = Find(peer, message);
            if (operation == null) { Fail(peer, message.Request, S.Failure.Unavailable); return; }
            if (message.Kind == P.Command.Status) { Status(peer, message.Request, operation); return; }
            bool sender = ReferenceEquals(operation.Sender, peer);
            if (message.Kind == P.Command.Withdraw ? !sender : sender) { Fail(peer, message.Request, S.Failure.Denied); return; }
            if (operation.Pending)
            {
                if (message.Kind == P.Command.Accept && !operation.Delivered) { Fail(peer, message.Request, S.Failure.Invalid); return; }
                Finish(operation, message.Kind == P.Command.Accept ? P.OfferState.Accepted : message.Kind == P.Command.Decline ? P.OfferState.Declined : P.OfferState.Withdrawn);
            }
            Status(peer, message.Request, operation);
        }
        private void Status(Peer peer, Guid request, Operation operation) =>
            Reply(peer, request, P.Command.StatusResult, m => m.Offer = P.Snapshot(operation.Offer));
        private void Expire(double now)
        {
            foreach (var operation in operations.Where(o => o.Pending && o.Offer != null && now >= o.Expires).ToArray())
                Finish(operation, P.OfferState.Expired);
        }
        internal void Tick(double now)
        {
            retired.RemoveAll(w => w.Task.IsCompleted && Interlocked.Read(ref w.Reserved) == 0);
            if (disposed) return;
            RefreshPeers();
            Expire(now);
            foreach (var operation in operations.Where(o => o.Pending).ToArray())
            {
                if (operation.Offer != null)
                { if (now >= operation.Expires) Finish(operation, P.OfferState.Expired); continue; }
                if (now - operation.Activity > 30) { AbortUpload(operation.Sender, S.Failure.Timeout); continue; }
                var task = operation.Work?.Task;
                if (task?.IsCompleted != true) continue;
                try
                {
                    var value = task.GetAwaiter().GetResult(); operation.Work.Task = null;
                    operation.Offer = new P.Offer { Id = Guid.NewGuid(), SenderSession = operation.Sender.Session,
                        RecipientSession = operation.Recipient.Session, Sender = operation.Sender.Principal, Recipient = operation.Recipient.Principal,
                        DocumentId = value.Id, Name = value.Name, Parts = value.Parts, FormatVersion = value.Version,
                        Bytes = operation.Header.Length, Sha256 = operation.Header.Sha256, State = P.OfferState.Pending };
                    operation.Expires = now + 300; operation.Sender.Upload = null;
                    Reply(operation.Sender, operation.Header.Request, P.Command.Offered, m => m.Offer = P.Snapshot(operation.Offer));
                }
                catch
                {
                    if (operation.Offer == null) AbortUpload(operation.Sender, S.Failure.Invalid);
                    // A lost Offered reply does not revoke a committed offer; sender can replay it.
                    else continue;
                    Fail(operation.Sender, operation.Header.Request, S.Failure.Invalid);
                }
            }
            int chunks = 16;
            foreach (var peer in peers.Values.ToArray())
            {
                var operation = peer.Reading;
                if (operation == null) continue;
                Guid request = peer.ReadRequest;
                try
                {
                    for (int sent = 0; sent < 4 && chunks > 0 && peer.ReadRequest == request && ReferenceEquals(peer.Reading, operation) && operation.Pending && peer.Offset < operation.Work.Bytes.Length; ++sent, --chunks)
                    {
                        byte[] bytes = new byte[Math.Min(S.ChunkBytes, operation.Work.Bytes.Length - peer.Offset)];
                        Buffer.BlockCopy(operation.Work.Bytes, peer.Offset, bytes, 0, bytes.Length);
                        int offset = peer.Offset; peer.Offset += bytes.Length;
                        Reply(peer, request, P.Command.Chunk, m => { m.Offset = offset; m.Bytes = bytes; });
                    }
                    if (peer.ReadRequest == request && ReferenceEquals(peer.Reading, operation) && operation.Pending && peer.Offset == operation.Work.Bytes.Length)
                    { operation.Delivered = true; ClearRead(peer); Reply(peer, request, P.Command.Commit); }
                }
                catch { if (peer.ReadRequest == request) ClearRead(peer); }
            }
        }
        private static void ClearRead(Peer peer) { peer.Reading = null; peer.ReadRequest = Guid.Empty; peer.Offset = 0; }
        private void AbortUpload(Peer peer, S.Failure failure)
        {
            var operation = peer.Upload;
            if (operation == null) return;
            peer.Upload = null; operation.Failure = failure; operation.Pending = false;
            Release(operation);
        }
        private void Finish(Operation operation, P.OfferState state)
        {
            operation.Pending = false; operation.Offer.State = state;
            foreach (var peer in peers.Values.Where(p => ReferenceEquals(p.Reading, operation))) ClearRead(peer);
            Release(operation);
        }
        private void Release(Operation operation)
        {
            operation.Transfer?.Dispose(); operation.Transfer = null;
            Work work = operation.Work; operation.Work = null;
            if (work == null) return;
            if (work.Task != null && !work.Task.IsCompleted)
            { retired.Add(work); work.Task.ContinueWith(_ => Cleanup(work), TaskScheduler.Default); }
            else Cleanup(work);
        }
        private void Cleanup(Work work)
        {
            var observed = work.Task?.Exception; work.Bytes = null;
            long bytes = Interlocked.Exchange(ref work.Reserved, 0);
            Interlocked.Add(ref reserved, -bytes); budget.Release(bytes);
        }
        internal void Disconnect(object connection)
        {
            if (!peers.TryGetValue(connection, out var peer)) return;
            foreach (var operation in operations.Where(o => o.Pending && (ReferenceEquals(o.Sender, peer) || ReferenceEquals(o.Recipient, peer))).ToArray())
                if (operation.Offer == null) AbortUpload(operation.Sender, S.Failure.Cancelled); else Finish(operation, P.OfferState.Expired);
            ClearRead(peer); peers.Remove(connection);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var peer in peers.Keys.ToArray()) Disconnect(peer);
        }
        private void Reply(Peer peer, Guid request, P.Command command, Action<P.Message> configure = null)
        {
            var message = new P.Message { Kind = command, World = world, Session = peer.Session, Request = request };
            configure?.Invoke(message); send(peer.Connection, P.Encode(message));
        }
        private void Fail(Peer peer, Guid request, S.Failure failure) => Reply(peer, request, P.Command.Error, m => m.Error = failure);
    }
}
