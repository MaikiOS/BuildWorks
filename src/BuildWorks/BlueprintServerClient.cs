using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using P = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks
{
    // One host-thread operation at a time. The native adapter verifies the sending server connection.
    internal sealed class BlueprintServerClient : IDisposable
    {
        internal sealed class Result
        {
            internal P.Message Reply;
            internal byte[] Document;
        }
        internal sealed class Failure : IOException
        {
            internal readonly P.Failure Code;
            internal Failure(P.Failure code) : base("Server library: " + code) { Code = code; }
        }
        private sealed class Operation : IDisposable
        {
            internal readonly TaskCompletionSource<Result> Completion = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal P.Message Header;
            internal P.Item Expected, Received;
            internal P.Transfer Incoming;
            internal SHA256 Hash;
            internal byte[] Upload;
            internal int Offset;
            internal bool Sent, Committed;
            internal double LastActivity;
            public void Dispose() { Incoming?.Dispose(); Hash?.Dispose(); Incoming = null; Hash = null; Upload = null; }
        }
        private readonly long world;
        private readonly Action<byte[]> send;
        private Guid session;
        private Operation active;
        private double now;
        private bool disposed;
        internal bool Ready => !disposed && session != Guid.Empty;
        internal bool IsAdministrator { get; private set; }
        internal bool Busy => active != null;
        internal float Progress => active == null ? 0 : active.Upload != null ? (float)active.Offset / active.Upload.Length :
            active.Incoming != null ? (float)active.Incoming.Received / active.Received.Bytes : 0;

        internal BlueprintServerClient(long world, Action<byte[]> send)
        { if (world == 0 || send == null) throw new ArgumentException(); this.world = world; this.send = send; }
        internal Task<Result> List(int offset = 0) => Begin(new P.Message { Kind = P.Command.List, Offset = offset });
        internal Task<Result> Read(P.Item item)
        {
            var snapshot = Snapshot(item);
            return Begin(new P.Message { Kind = P.Command.Read, Id = snapshot.Id, Revision = snapshot.Revision }, snapshot);
        }
        internal Task<Result> Delete(P.Item item)
        {
            var snapshot = Snapshot(item);
            return Begin(new P.Message { Kind = P.Command.Delete, Id = snapshot.Id, Revision = snapshot.Revision }, snapshot);
        }
        internal Task<Result> Publish(byte[] document, byte access, P.Item previous = null)
        {
            if (document == null || document.Length < 1 || document.Length > P.MaximumDocumentBytes || access > 1) throw Invalid();
            var snapshot = previous == null ? null : Snapshot(previous);
            var header = new P.Message
            {
                Kind = P.Command.PublishStart,
                Id = snapshot?.Id ?? Guid.Empty,
                Revision = snapshot?.Revision ?? 0,
                Access = access,
                Length = document.Length
            };
            if (disposed || active != null || !Ready) throw new InvalidOperationException("Server library is unavailable or busy.");
            // Own the snapshot before returning to a caller that can edit its document.
            byte[] frozen = (byte[])document.Clone();
            return Begin(header, snapshot, frozen);
        }
        private Task<Result> Begin(P.Message header, P.Item expected = null, byte[] upload = null)
        {
            if (disposed || active != null || header.Kind != P.Command.List && !Ready)
                throw new InvalidOperationException("Server library is unavailable or busy.");
            header.World = world; header.Session = session; header.Request = Guid.NewGuid();
            var operation = new Operation { Header = header, Expected = expected, Upload = upload, LastActivity = now };
            active = operation;
            try
            {
                if (upload == null) Send(operation, header);
                else operation.Hash = SHA256.Create();
            }
            catch (Exception exception) { CancelActive(exception); }
            return operation.Completion.Task;
        }
        internal void Tick(double time)
        {
            now = time;
            var operation = active;
            if (operation == null) return;
            if (time - operation.LastActivity > 30) { CancelActive(new Failure(P.Failure.Timeout)); return; }
            try
            {
                if (operation.Upload == null || operation.Committed) return;
                // Bound both hashing and native sends per frame; no native API runs on a worker.
                for (int count = 0; count < 4 && active == operation && operation.Offset < operation.Upload.Length; ++count)
                {
                    int length = Math.Min(P.ChunkBytes, operation.Upload.Length - operation.Offset);
                    if (operation.Hash != null) operation.Hash.TransformBlock(operation.Upload, operation.Offset, length, null, 0);
                    else
                    {
                        byte[] bytes = new byte[length]; Buffer.BlockCopy(operation.Upload, operation.Offset, bytes, 0, length);
                        Send(operation, new P.Message { Kind = P.Command.Chunk, Offset = operation.Offset, Bytes = bytes });
                    }
                    operation.Offset += length; operation.LastActivity = time;
                }
                if (active != operation || operation.Offset != operation.Upload.Length) return;
                if (operation.Hash != null)
                {
                    operation.Hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    operation.Header.Sha256 = BitConverter.ToString(operation.Hash.Hash).Replace("-", "");
                    operation.Hash.Dispose(); operation.Hash = null; operation.Offset = 0;
                    Send(operation, operation.Header);
                }
                else { operation.Committed = true; Send(operation, new P.Message { Kind = P.Command.Commit }); }
            }
            catch (Exception exception) { CancelActive(exception); }
        }
        internal void Receive(byte[] frame, double time)
        {
            var operation = active;
            if (operation == null || disposed) return;
            try
            {
                var message = P.Decode(frame);
                if (message.Request != operation.Header.Request) return; // A cancelled operation's late reply.
                if (message.World != world || session != Guid.Empty && message.Session != session) throw Invalid();
                if (session == Guid.Empty)
                {
                    if (operation.Header.Kind != P.Command.List || message.Kind != P.Command.ListResult && message.Kind != P.Command.Error) throw Invalid();
                    session = message.Session;
                }
                operation.LastActivity = time;
                if (message.Kind == P.Command.Error)
                {
                    Finish(operation, new Failure(message.Error));
                    if (message.Error == P.Failure.Invalid) { session = Guid.Empty; IsAdministrator = false; }
                    return;
                }
                switch (operation.Header.Kind)
                {
                    case P.Command.List:
                        if (message.Kind != P.Command.ListResult || message.Offset != operation.Header.Offset) throw Invalid();
                        IsAdministrator = message.Administrator;
                        Finish(operation, result: new Result { Reply = message }); break;
                    case P.Command.Read:
                        if (message.Kind == P.Command.DataStart)
                        {
                            if (operation.Incoming != null || !SameDocument(message.Item, operation.Expected)) throw Invalid();
                            operation.Received = message.Item; operation.Incoming = new P.Transfer(message.Item.Bytes, message.Item.Sha256);
                        }
                        else if (message.Kind == P.Command.Chunk && operation.Incoming != null) operation.Incoming.Append(message.Offset, message.Bytes);
                        else if (message.Kind == P.Command.Commit && operation.Incoming != null)
                        {
                            message.Item = operation.Received;
                            Finish(operation, result: new Result { Reply = message, Document = operation.Incoming.Complete() });
                        }
                        else throw Invalid();
                        break;
                    case P.Command.PublishStart:
                        if (message.Kind != P.Command.Published || !operation.Committed || message.Item.Access != operation.Header.Access ||
                            message.Item.Bytes != operation.Header.Length || message.Item.Sha256 != operation.Header.Sha256 ||
                            operation.Expected == null && message.Item.Revision != 1 || operation.Expected != null &&
                            (message.Item.Id != operation.Expected.Id || message.Item.Revision != checked(operation.Expected.Revision + 1))) throw Invalid();
                        Finish(operation, result: new Result { Reply = message }); break;
                    case P.Command.Delete:
                        if (message.Kind != P.Command.Deleted || message.Id != operation.Header.Id) throw Invalid();
                        Finish(operation, result: new Result { Reply = message }); break;
                    default: throw Invalid();
                }
            }
            catch (Exception exception)
            {
                if (exception is InvalidDataException || exception is OverflowException) exception = Invalid();
                CancelActive(exception);
                if (!(exception is Failure failure) || failure.Code == P.Failure.Invalid)
                { session = Guid.Empty; IsAdministrator = false; }
            }
        }
        internal void Cancel(Task<Result> task)
        { if (active != null && ReferenceEquals(active.Completion.Task, task)) CancelActive(new Failure(P.Failure.Cancelled)); }
        private void CancelActive(Exception exception)
        {
            var operation = active;
            if (operation == null) return;
            bool notify = operation.Sent && session != Guid.Empty;
            active = null; // A synchronous adapter must not deliver the old response into this operation.
            if (notify) try { Send(operation, new P.Message { Kind = P.Command.Cancel }); } catch { /* Connection may already be gone. */ }
            Finish(operation, exception);
        }
        private void Send(Operation operation, P.Message message)
        {
            message.World = world; message.Request = operation.Header.Request; message.Session = session;
            operation.Sent = true; send(P.Encode(message));
        }
        private void Finish(Operation operation, Exception exception = null, Result result = null)
        {
            if (ReferenceEquals(active, operation)) active = null;
            operation.Dispose();
            if (exception == null) operation.Completion.TrySetResult(result);
            else operation.Completion.TrySetException(exception);
        }
        public void Dispose()
        { if (disposed) return; CancelActive(new Failure(P.Failure.Unavailable)); disposed = true; session = Guid.Empty; IsAdministrator = false; }
        private static bool SameDocument(P.Item a, P.Item b) => a.Id == b.Id && a.Revision == b.Revision && a.Bytes == b.Bytes && a.Sha256 == b.Sha256 && a.Access == b.Access;
        private static P.Item Snapshot(P.Item item)
        {
            // Reuse the wire validation and detach the caller's mutable metadata.
            return P.Decode(P.Encode(new P.Message { Kind = P.Command.Published, World = 1, Request = Guid.NewGuid(), Session = Guid.NewGuid(), Item = item })).Item;
        }
        private static Failure Invalid() => new Failure(P.Failure.Invalid);
    }
}
