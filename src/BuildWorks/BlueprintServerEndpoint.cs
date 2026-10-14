using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using P = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks
{
    // Host-thread executor; opaque connections are authenticated outside the wire format.
    internal sealed class BlueprintServerEndpoint : IDisposable
    {
        internal const long MaximumTransferBytes = 128L * 1024 * 1024;
        private readonly ServerBlueprintLibrary library;
        private readonly long world;
        private readonly long maximumTransferBytes;
        private readonly Func<object, (string Principal, bool Administrator)> resolve;
        private readonly Action<object, byte[]> send;
        private readonly Action<Exception> log;
        private readonly Task initialization;
        private readonly Dictionary<object, Peer> peers = new Dictionary<object, Peer>();
        private readonly List<Peer> retired = new List<Peer>();
        private Peer authority;
        private long reserved;
        private bool disposed;
        private sealed class Peer
        {
            internal object Connection;
            internal string Principal;
            internal Guid Session = Guid.NewGuid(), Request;
            internal P.Message Header;
            internal P.Transfer Incoming;
            internal byte[] Outgoing;
            internal int Offset;
            internal CancellationTokenSource Stop;
            internal Task<ServerBlueprintLibrary.PreparedPublish> Publication;
            internal Task<ServerBlueprintLibrary.PreparedRead> Reading;
            internal long Reserved;
            internal double LastActivity, LastRate, ControlTokens = 16, ChunkTokens = 300;
            internal readonly Dictionary<Guid, (byte[] Header, byte[] Reply)> Receipts = new Dictionary<Guid, (byte[], byte[])>();
        }
        internal BlueprintServerEndpoint(ServerBlueprintLibrary library, long world,
            Func<object, (string Principal, bool Administrator)> resolve, Action<object, byte[]> send, Action<Exception> log = null,
            long maximumTransferBytes = MaximumTransferBytes)
        {
            if (library == null || world == 0 || resolve == null || send == null) throw new ArgumentException("Invalid server endpoint.");
            if (maximumTransferBytes < 1 || maximumTransferBytes > MaximumTransferBytes) throw new ArgumentOutOfRangeException(nameof(maximumTransferBytes));
            this.library = library; this.world = world; this.resolve = resolve; this.send = send; this.log = log;
            this.maximumTransferBytes = maximumTransferBytes;
            initialization = library.InitializeAsync();
        }
        internal bool HasPendingWork => !initialization.IsCompleted || retired.Any(peer => !WorkFinished(peer)) ||
            peers.Values.Any(peer => peer.Publication != null || peer.Reading != null);
        internal long ReservedBytes => Interlocked.Read(ref reserved);
        internal bool IsAdministrator(string principal)
        {
            if (disposed || authority == null) return false;
            var current = resolve(authority.Connection);
            return current.Principal == principal && current.Administrator;
        }

        internal void Receive(object connection, byte[] frame, double now)
        {
            if (disposed || connection == null) return;
            var identity = resolve(connection);
            if (string.IsNullOrWhiteSpace(identity.Principal)) { Disconnect(connection); return; }
            if (!peers.TryGetValue(connection, out Peer peer))
            {
                if (peers.Count >= 64) return;
                peer = new Peer { Connection = connection, Principal = identity.Principal, LastActivity = now, LastRate = now };
                peers.Add(connection, peer);
            }
            if (peer.Principal != identity.Principal) { Disconnect(connection); return; }
            double delta = Math.Max(0, now - peer.LastRate); peer.LastRate = now;
            peer.ControlTokens = Math.Min(16, peer.ControlTokens + delta * 4); peer.ChunkTokens = Math.Min(300, peer.ChunkTokens + delta * 300);
            bool bulk = frame != null && frame.Length > 1 && frame[1] == (byte)P.Command.Chunk;
            if (bulk ? peer.ChunkTokens < 1 : peer.ControlTokens < 1) return;
            if (bulk) --peer.ChunkTokens; else --peer.ControlTokens;
            P.Message message;
            try { message = P.Decode(frame); }
            catch (InvalidDataException)
            {
                Guid failed = peer.Request == Guid.Empty ? Guid.NewGuid() : peer.Request;
                Release(peer); Fail(peer, failed, P.Failure.Invalid); return;
            }
            if (message.World != world || message.Kind != P.Command.List && message.Session != peer.Session ||
                message.Kind == P.Command.List && message.Session != Guid.Empty && message.Session != peer.Session)
            {
                Guid failed = peer.Request == Guid.Empty ? message.Request : peer.Request;
                Release(peer); Fail(peer, failed, P.Failure.Invalid); return;
            }
            peer.LastActivity = now;
            Peer before = authority; authority = peer;
            try
            {
                if (!initialization.IsCompleted) { Fail(peer, message.Request, P.Failure.Busy); return; }
                initialization.GetAwaiter().GetResult();
                if (peer.Receipts.TryGetValue(message.Request, out var receipt))
                {
                    if (!identity.Administrator) { Fail(peer, message.Request, P.Failure.Denied); return; }
                    if (!receipt.Header.SequenceEqual(frame) && !(message.Kind == P.Command.Commit && P.Decode(receipt.Header).Kind == P.Command.PublishStart))
                    { Fail(peer, message.Request, P.Failure.Invalid); return; }
                    send(connection, receipt.Reply); return;
                }
                if (message.Kind == P.Command.Cancel)
                { if (peer.Request == message.Request) Release(peer); return; }
                if (message.Kind == P.Command.Chunk)
                {
                    if (peer.Request != message.Request || peer.Incoming == null) throw new InvalidDataException();
                    peer.Incoming.Append(message.Offset, message.Bytes); return;
                }
                if (message.Kind == P.Command.Commit)
                {
                    if (peer.Request == message.Request && peer.Publication != null) return;
                    if (peer.Request != message.Request || peer.Incoming == null || !identity.Administrator) throw new UnauthorizedAccessException();
                    byte[] document = peer.Incoming.Complete(); peer.Incoming.Dispose(); peer.Incoming = null;
                    var header = peer.Header;
                    peer.Publication = library.PreparePublish(peer.Principal, document, (ServerBlueprintLibrary.Access)header.Access,
                        header.Id == Guid.Empty ? null : header.Id.ToString("N"), header.Revision, peer.Stop.Token);
                    return;
                }
                if (peer.Request != Guid.Empty) { Fail(peer, message.Request, P.Failure.Busy); return; }
                switch (message.Kind)
                {
                    case P.Command.List:
                        var list = library.List(peer.Principal);
                        if (message.Offset > list.Count) throw new InvalidDataException();
                        Reply(peer, message.Request, P.Command.ListResult, reply =>
                        {
                            reply.Administrator = identity.Administrator; reply.Total = list.Count; reply.Offset = message.Offset;
                            reply.Items = list.Skip(message.Offset).Take(P.PageSize).Select(Item).ToArray();
                        });
                        break;
                    case P.Command.Read:
                        var info = library.Describe(peer.Principal, message.Id.ToString("N"), message.Revision);
                        Reserve(peer, info.DocumentBytes); Start(peer, message);
                        peer.Reading = library.PrepareRead(peer.Principal, info.Id, info.Revision, peer.Stop.Token); break;
                    case P.Command.PublishStart:
                        if (!identity.Administrator) throw new UnauthorizedAccessException();
                        Reserve(peer, checked((long)message.Length * 2)); Start(peer, message);
                        peer.Incoming = new P.Transfer(message.Length, message.Sha256); break;
                    case P.Command.Delete:
                        if (!identity.Administrator) throw new UnauthorizedAccessException();
                        library.Delete(peer.Principal, message.Id.ToString("N"), message.Revision);
                        byte[] deleted = EncodeReply(peer, message.Request, P.Command.Deleted, reply => reply.Id = message.Id);
                        Remember(peer, frame, deleted); send(peer.Connection, deleted); break;
                    default: throw new InvalidDataException();
                }
            }
            catch (Exception exception)
            {
                if (peer.Request == message.Request) Release(peer);
                if (peer.Receipts.ContainsKey(message.Request)) log?.Invoke(exception); else Report(peer, message.Request, exception);
            }
            finally { authority = before; }
        }

        internal void Tick(double now)
        {
            for (int i = retired.Count - 1; i >= 0; --i)
                if (WorkFinished(retired[i])) { Cleanup(retired[i]); retired.RemoveAt(i); }
            if (disposed) return;
            int chunks = 16;
            foreach (Peer peer in peers.Values.ToArray())
            {
                var identity = resolve(peer.Connection);
                if (identity.Principal != peer.Principal) { Disconnect(peer.Connection); continue; }
                if (peer.Request == Guid.Empty) continue;
                Guid request = peer.Request; Peer before = authority; authority = peer;
                try
                {
                    if (now - peer.LastActivity > 30) { Fail(peer, request, P.Failure.Timeout); Release(peer); continue; }
                    if (peer.Publication?.IsCompleted == true)
                    {
                        if (!identity.Administrator) throw new UnauthorizedAccessException();
                        using (var prepared = peer.Publication.GetAwaiter().GetResult())
                        {
                            var result = library.CommitPublish(peer.Principal, prepared, peer.Stop.Token);
                            byte[] reply = EncodeReply(peer, request, P.Command.Published, message => message.Item = Item(result));
                            Remember(peer, P.Encode(peer.Header), reply);
                            send(peer.Connection, reply);
                        }
                        Release(peer); continue;
                    }
                    if (peer.Reading?.IsCompleted == true)
                    {
                        var prepared = peer.Reading.GetAwaiter().GetResult();
                        peer.Outgoing = library.CompleteRead(peer.Principal, prepared); peer.Reading = null;
                        Reply(peer, request, P.Command.DataStart, message => message.Item = Item(prepared.Snapshot.Info));
                    }
                    if (peer.Outgoing != null)
                    {
                        library.Describe(peer.Principal, peer.Header.Id.ToString("N"), peer.Header.Revision);
                        for (int sent = 0; sent < 4 && chunks > 0 && peer.Offset < peer.Outgoing.Length; ++sent, --chunks)
                        {
                            int length = Math.Min(P.ChunkBytes, peer.Outgoing.Length - peer.Offset);
                            var bytes = new byte[length]; Buffer.BlockCopy(peer.Outgoing, peer.Offset, bytes, 0, length);
                            Reply(peer, request, P.Command.Chunk, message => { message.Offset = peer.Offset; message.Bytes = bytes; });
                            peer.Offset += length; peer.LastActivity = now;
                        }
                        if (peer.Offset == peer.Outgoing.Length) { Reply(peer, request, P.Command.Commit); Release(peer); }
                    }
                }
                catch (Exception exception)
                {
                    Release(peer);
                    if (peer.Receipts.ContainsKey(request)) log?.Invoke(exception); else Report(peer, request, exception);
                }
                finally { authority = before; }
            }
        }
        internal void Disconnect(object connection)
        { if (peers.TryGetValue(connection, out var peer)) { Release(peer); peers.Remove(connection); } }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var peer in peers.Values.ToArray()) Release(peer);
            peers.Clear();
            foreach (var peer in retired)
            {
                var task = (Task)peer.Publication ?? peer.Reading;
                task?.ContinueWith(_ => Cleanup(peer), TaskScheduler.Default);
            }
            initialization.ContinueWith(task => { var observed = task.Exception; }, TaskScheduler.Default);
        }
        private void Reserve(Peer peer, long bytes)
        {
            if (bytes < 0 || bytes > maximumTransferBytes - ReservedBytes) throw new InvalidDataException("$buildworks_server.capacity");
            Interlocked.Add(ref reserved, bytes); peer.Reserved = bytes;
        }
        private static void Start(Peer peer, P.Message message)
        { peer.Request = message.Request; peer.Header = message; peer.Stop = new CancellationTokenSource(); }
        private static bool WorkFinished(Peer peer) => (peer.Publication == null || peer.Publication.IsCompleted) && (peer.Reading == null || peer.Reading.IsCompleted);
        private void Release(Peer peer)
        {
            peer.Stop?.Cancel(); peer.Incoming?.Dispose(); peer.Incoming = null; peer.Outgoing = null; peer.Offset = 0;
            if (!WorkFinished(peer))
            {
                retired.Add(new Peer { Publication = peer.Publication, Reading = peer.Reading, Stop = peer.Stop, Reserved = peer.Reserved });
                peer.Reserved = 0;
            }
            else Cleanup(peer);
            peer.Publication = null; peer.Reading = null; peer.Stop = null; peer.Request = Guid.Empty; peer.Header = null;
        }
        private void Cleanup(Peer peer)
        {
            if (peer.Publication?.Status == TaskStatus.RanToCompletion) peer.Publication.Result.Dispose();
            if (peer.Reading?.Status == TaskStatus.RanToCompletion) peer.Reading.Result.Bytes = null;
            var publicationError = peer.Publication?.Exception; var readError = peer.Reading?.Exception;
            Interlocked.Add(ref reserved, -Interlocked.Exchange(ref peer.Reserved, 0)); peer.Stop?.Dispose();
        }
        private static P.Item Item(ServerBlueprintLibrary.Info info) => new P.Item
        {
            Id = Guid.ParseExact(info.Id, "N"),
            Revision = info.Revision,
            Access = (byte)info.Visibility,
            Parts = info.PartCount,
            Bytes = info.DocumentBytes,
            Name = info.Name,
            Category = info.Category,
            Publisher = info.Publisher,
            Editor = info.Editor,
            Sha256 = info.DocumentSha256
        };
        private byte[] Reply(Peer peer, Guid request, P.Command command, Action<P.Message> configure = null)
        { byte[] frame = EncodeReply(peer, request, command, configure); send(peer.Connection, frame); return frame; }
        private byte[] EncodeReply(Peer peer, Guid request, P.Command command, Action<P.Message> configure = null)
        {
            var message = new P.Message { Kind = command, World = world, Request = request, Session = peer.Session };
            configure?.Invoke(message); return P.Encode(message);
        }
        private void Fail(Peer peer, Guid request, P.Failure error) => Reply(peer, request, P.Command.Error, message => message.Error = error);
        private void Report(Peer peer, Guid request, Exception exception)
        {
            if (!(exception is UnauthorizedAccessException || exception is InvalidDataException || exception is OperationCanceledException)) log?.Invoke(exception);
            string key = exception.Message ?? "";
            var error = exception is UnauthorizedAccessException ? P.Failure.Denied : exception is OperationCanceledException ? P.Failure.Cancelled :
                key.Contains("server.unavailable") ? P.Failure.Unavailable : key.Contains("server.capacity") ? P.Failure.Capacity :
                key.Contains("server.conflict") || key.Contains("server.changed") ? P.Failure.Conflict : exception is InvalidDataException ? P.Failure.Invalid : P.Failure.Failed;
            Fail(peer, request, error);
        }
        private static void Remember(Peer peer, byte[] header, byte[] reply)
        {
            if (peer.Receipts.Count >= 32) peer.Receipts.Remove(peer.Receipts.Keys.First());
            peer.Receipts[P.Decode(reply).Request] = (header, reply);
        }
    }
}
