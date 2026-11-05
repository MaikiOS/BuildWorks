using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using P = OstrixMods.BuildWorks.BlueprintPeerProtocol;
using S = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks
{
    // Host-thread transport only. Reading never accepts an offer or writes a personal copy.
    internal sealed class BlueprintPeerClient : IDisposable
    {
        internal sealed class Result { internal P.Message Reply; internal byte[] Document; }
        internal sealed class Failure : IOException
        {
            internal readonly S.Failure Code;
            internal Failure(S.Failure code) : base("Peer exchange: " + code) { Code = code; }
        }
        private sealed class Operation : IDisposable
        {
            internal readonly TaskCompletionSource<Result> Completion = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal P.Message Header;
            internal P.Offer Expected, Received;
            internal S.Transfer Incoming;
            internal byte[] Upload;
            internal SHA256 Hash;
            internal int Offset;
            internal bool Sent, Committed;
            internal double Activity;
            public void Dispose() { Incoming?.Dispose(); Hash?.Dispose(); Incoming = null; Hash = null; Upload = null; }
        }
        private readonly long world;
        private readonly Action<byte[]> send;
        private Operation active;
        private P.Offer delivered;
        private double now;
        private bool disposed;
        internal Guid Session { get; private set; }
        internal string Principal { get; private set; }
        internal string DisplayName { get; private set; }
        internal bool Ready => !disposed && Session != Guid.Empty;
        internal bool Busy => active != null;
        internal float Progress => active?.Upload != null ? (float)active.Offset / active.Upload.Length :
            active?.Incoming != null ? (float)active.Incoming.Received / active.Received.Bytes : 0;
        internal BlueprintPeerClient(long world, Action<byte[]> send)
        { if (world == 0 || send == null) throw new ArgumentException(); this.world = world; this.send = send; }
        internal Task<Result> Hello() => Begin(new P.Message { Kind = P.Command.Hello });
        internal Task<Result> Roster() => Begin(new P.Message { Kind = P.Command.Roster });
        internal Task<Result> Inbox() => Begin(new P.Message { Kind = P.Command.Inbox });
        internal Task<Result> Offer(Guid recipient, byte[] document)
        {
            if (recipient == Guid.Empty || recipient == Session || document == null || document.Length < 1 || document.Length > S.MaximumDocumentBytes)
                throw Invalid();
            RequireAvailable(false);
            return Begin(new P.Message { Kind = P.Command.OfferStart, Target = recipient, Length = document.Length }, upload: (byte[])document.Clone());
        }
        internal Task<Result> Read(P.Offer offer)
        {
            var expected = Recipient(offer);
            if (expected.State != P.OfferState.Pending) throw Invalid();
            return Begin(new P.Message { Kind = P.Command.Read, Id = expected.Id }, expected);
        }
        internal Task<Result> Accept(P.Offer offer)
        {
            var expected = Recipient(offer);
            if (expected.State != P.OfferState.Pending || !Same(expected, delivered)) throw Invalid();
            return Begin(new P.Message { Kind = P.Command.Accept, Id = expected.Id }, expected);
        }
        internal Task<Result> Decline(P.Offer offer)
        { var expected = Recipient(offer); return Begin(new P.Message { Kind = P.Command.Decline, Id = expected.Id }, expected); }
        internal Task<Result> Withdraw(P.Offer offer)
        {
            var expected = P.Snapshot(offer);
            if (!Ready || expected.SenderSession != Session || expected.Sender != Principal) throw Invalid();
            return Begin(new P.Message { Kind = P.Command.Withdraw, Id = expected.Id }, expected);
        }
        internal Task<Result> Status(P.Offer offer)
        {
            var expected = P.Snapshot(offer);
            if (!Ready || !(expected.SenderSession == Session && expected.Sender == Principal ||
                expected.RecipientSession == Session && expected.Recipient == Principal)) throw Invalid();
            return Begin(new P.Message { Kind = P.Command.Status, Id = expected.Id }, expected);
        }
        private P.Offer Recipient(P.Offer offer)
        {
            var expected = P.Snapshot(offer);
            if (!Ready || expected.RecipientSession != Session || expected.Recipient != Principal) throw Invalid();
            return expected;
        }
        private void RequireAvailable(bool hello)
        {
            if (disposed || active != null || (hello ? Ready : !Ready)) throw new InvalidOperationException("Peer exchange is unavailable or busy.");
        }
        private Task<Result> Begin(P.Message header, P.Offer expected = null, byte[] upload = null)
        {
            RequireAvailable(header.Kind == P.Command.Hello);
            header.World = world; header.Request = Guid.NewGuid(); header.Session = Session;
            var operation = new Operation { Header = header, Expected = expected, Upload = upload, Activity = now };
            active = operation;
            try { if (upload == null) Send(operation, header); else operation.Hash = SHA256.Create(); }
            catch (Exception error) { CancelActive(operation, error); }
            return operation.Completion.Task;
        }
        internal void Tick(double time)
        {
            now = time; var operation = active;
            if (operation == null) return;
            if (time - operation.Activity > 30) { CancelActive(operation, new Failure(S.Failure.Timeout)); return; }
            try
            {
                if (operation.Upload == null || operation.Committed) return;
                for (int count = 0; count < 4 && active == operation && operation.Offset < operation.Upload.Length; ++count)
                {
                    int length = Math.Min(S.ChunkBytes, operation.Upload.Length - operation.Offset);
                    if (operation.Hash != null) operation.Hash.TransformBlock(operation.Upload, operation.Offset, length, null, 0);
                    else
                    {
                        byte[] bytes = new byte[length]; Buffer.BlockCopy(operation.Upload, operation.Offset, bytes, 0, length);
                        Send(operation, new P.Message { Kind = P.Command.Chunk, Offset = operation.Offset, Bytes = bytes });
                    }
                    operation.Offset += length; operation.Activity = time;
                }
                if (active != operation || operation.Offset != operation.Upload.Length) return;
                if (operation.Hash != null)
                {
                    operation.Hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    operation.Header.Sha256 = BitConverter.ToString(operation.Hash.Hash).Replace("-", "");
                    operation.Hash.Dispose(); operation.Hash = null; operation.Offset = 0; Send(operation, operation.Header);
                }
                else { operation.Committed = true; Send(operation, new P.Message { Kind = P.Command.Commit }); }
            }
            catch (Exception error) { CancelActive(operation, error); }
        }
        internal void Receive(byte[] frame, double time)
        {
            var operation = active;
            if (operation == null || disposed) return;
            try
            {
                var message = P.Decode(frame);
                if (message.Request != operation.Header.Request) return;
                if (message.World != world || Ready && message.Session != Session) throw Invalid();
                operation.Activity = time;
                if (message.Kind == P.Command.Error)
                {
                    if (message.Error == S.Failure.Invalid) Forget();
                    Finish(operation, new Failure(message.Error));
                    return;
                }
                switch (operation.Header.Kind)
                {
                    case P.Command.Hello:
                        if (message.Kind != P.Command.Welcome) throw Invalid();
                        Session = message.Session; Principal = message.Principal; DisplayName = message.Name;
                        Finish(operation, result: new Result { Reply = message }); break;
                    case P.Command.Roster:
                        if (message.Kind != P.Command.RosterResult) throw Invalid();
                        Finish(operation, result: new Result { Reply = message }); break;
                    case P.Command.Inbox:
                        if (message.Kind != P.Command.InboxResult) throw Invalid();
                        foreach (var offer in message.Offers) if (offer.RecipientSession != Session || offer.Recipient != Principal) throw Invalid();
                        Finish(operation, result: new Result { Reply = message }); break;
                    case P.Command.OfferStart:
                        var offered = message.Offer;
                        if (message.Kind != P.Command.Offered || !operation.Committed || offered.SenderSession != Session || offered.Sender != Principal ||
                            offered.RecipientSession != operation.Header.Target || offered.Bytes != operation.Header.Length ||
                            offered.Sha256 != operation.Header.Sha256 || offered.State != P.OfferState.Pending) throw Invalid();
                        Finish(operation, result: new Result { Reply = message }); break;
                    case P.Command.Read:
                        if (message.Kind == P.Command.StatusResult)
                        {
                            if (!Same(message.Offer, operation.Expected) || message.Offer.State == P.OfferState.Pending) throw Invalid();
                            Finish(operation, result: new Result { Reply = message });
                        }
                        else if (message.Kind == P.Command.DataStart)
                        {
                            if (operation.Incoming != null || !Same(message.Offer, operation.Expected) || message.Offer.State != P.OfferState.Pending) throw Invalid();
                            operation.Received = message.Offer; operation.Incoming = new S.Transfer(message.Offer.Bytes, message.Offer.Sha256);
                        }
                        else if (message.Kind == P.Command.Chunk && operation.Incoming != null) operation.Incoming.Append(message.Offset, message.Bytes);
                        else if (message.Kind == P.Command.Commit && operation.Incoming != null)
                        {
                            byte[] bytes = operation.Incoming.Complete(); delivered = P.Snapshot(operation.Received); message.Offer = P.Snapshot(delivered);
                            Finish(operation, result: new Result { Reply = message, Document = bytes });
                        }
                        else throw Invalid();
                        break;
                    case P.Command.Accept: case P.Command.Decline: case P.Command.Withdraw: case P.Command.Status:
                        if (message.Kind != P.Command.StatusResult || !Same(message.Offer, operation.Expected) ||
                            operation.Header.Kind != P.Command.Status && message.Offer.State == P.OfferState.Pending) throw Invalid();
                        Finish(operation, result: new Result { Reply = message }); break;
                    default: throw Invalid();
                }
            }
            catch (Exception error)
            {
                if (error is InvalidDataException || error is OverflowException) error = Invalid();
                CancelActive(operation, error, forget: true);
            }
        }
        internal void Cancel(Task<Result> task)
        { if (active != null && ReferenceEquals(active.Completion.Task, task)) CancelActive(active, new Failure(S.Failure.Cancelled)); }
        private void CancelActive(Operation operation, Exception error, bool forget = false)
        {
            if (operation == null || !ReferenceEquals(active, operation)) return;
            Guid session = Session;
            active = null;
            if (forget) Forget();
            // A synchronous Cancel callback may start another operation; finish only this captured one.
            if (operation.Sent && session != Guid.Empty)
                try { send(P.Encode(new P.Message { Kind = P.Command.Cancel, World = world, Request = operation.Header.Request, Session = session })); }
                catch { /* Disconnected transport. */ }
            Finish(operation, error);
        }
        private void Send(Operation operation, P.Message message)
        {
            message.World = world; message.Request = operation.Header.Request; message.Session = Session;
            operation.Sent = true; send(P.Encode(message));
        }
        private void Finish(Operation operation, Exception error = null, Result result = null)
        {
            if (ReferenceEquals(active, operation)) active = null;
            operation.Dispose();
            if (error == null) operation.Completion.TrySetResult(result); else operation.Completion.TrySetException(error);
        }
        private void Forget() { Session = Guid.Empty; Principal = DisplayName = null; delivered = null; }
        public void Dispose() { if (disposed) return; disposed = true; CancelActive(active, new Failure(S.Failure.Unavailable), forget: true); Forget(); }
        private static bool Same(P.Offer a, P.Offer b) => a != null && b != null && a.Id == b.Id && a.DocumentId == b.DocumentId &&
            a.SenderSession == b.SenderSession && a.RecipientSession == b.RecipientSession && a.Sender == b.Sender && a.Recipient == b.Recipient &&
            a.Name == b.Name && a.Bytes == b.Bytes && a.Sha256 == b.Sha256 && a.Parts == b.Parts && a.FormatVersion == b.FormatVersion;
        private static Failure Invalid() => new Failure(S.Failure.Invalid);
    }
}
