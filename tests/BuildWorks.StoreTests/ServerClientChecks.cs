using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using P = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ServerClientChecks
    {
        internal static void Run(string folder, byte[] original)
        {
            var seed = new ServerBlueprintLibrary(folder, _ => true);
            seed.Publish("admin", original, ServerBlueprintLibrary.Access.Players);
            seed.Publish("admin", original, ServerBlueprintLibrary.Access.Administrators);
            object connection = new object(); bool administrator = false, corrupt = false, reentrant = false;
            double now = 1;
            var requests = new Queue<byte[]>(); var replies = new Queue<byte[]>();
            BlueprintServerEndpoint endpoint = null; BlueprintServerClient client = null;
            var library = new ServerBlueprintLibrary(folder, principal => endpoint.IsAdministrator(principal));
            using (endpoint = new BlueprintServerEndpoint(library, 1801, _ => ("peer", administrator), (_, bytes) =>
            {
                if (corrupt && P.Decode(bytes).Kind == P.Command.Chunk)
                { bytes = (byte[])bytes.Clone(); bytes[bytes.Length - 1] ^= 1; corrupt = false; }
                if (reentrant) client.Receive(bytes, now); else replies.Enqueue(bytes);
            }))
            using (client = new BlueprintServerClient(1801, bytes =>
            { if (reentrant) endpoint.Receive(connection, bytes, now); else requests.Enqueue(bytes); }))
            {
                Drain(() => !endpoint.HasPendingWork);
                var listing = Wait(client.List());
                Require(client.Ready && !client.IsAdministrator && listing.Reply.Items.Length == 1, "Client bootstrap exposed admin entries");
                P.Item publicItem = listing.Reply.Items.Single();
                var downloaded = Wait(client.Read(publicItem));
                Require(downloaded.Document.SequenceEqual(original) && downloaded.Reply.Kind == P.Command.Commit && downloaded.Reply.World == 1801 &&
                    downloaded.Reply.Session != Guid.Empty && downloaded.Reply.Item.Id == publicItem.Id, "Client download lost bytes or response context");
                Error(client.Delete(publicItem), P.Failure.Denied);
                Error(client.Publish(original, 0), P.Failure.Denied);

                administrator = true;
                Require(Wait(client.List()).Reply.Items.Length == 2 && client.IsAdministrator, "Admin bootstrap/list failed");
                byte[] large = original.Concat(Encoding.UTF8.GetBytes(new string(' ', P.ChunkBytes * 3))).ToArray();
                byte[] expected = (byte[])large.Clone();
                var publication = client.Publish(large, 0); large[0] ^= 1;
                Require(Directory.GetFiles(folder, "*.bwserver.zip").Length == 2, "Client published before commit");
                var published = Wait(publication).Reply.Item;
                Require(Wait(client.Read(published)).Document.SequenceEqual(expected), "Upload snapshot or ordered multi-chunk download failed");
                var updated = Wait(client.Publish(original, 1, published)).Reply.Item;
                Require(updated.Id == published.Id && updated.Revision == 2 && updated.Access == 1, "Client update lost identity/revision/access");
                Error(client.Publish(original, 0, published), P.Failure.Conflict);
                Error(client.Read(published), P.Failure.Conflict);

                var cancel = client.Publish(expected, 0);
                while (requests.Count == 0) client.Tick(now += .001);
                while (requests.Count != 0) endpoint.Receive(connection, requests.Dequeue(), now);
                Require(endpoint.ReservedBytes > 0, "Upload did not reserve before cancellation");
                client.Cancel(cancel); Error(cancel, P.Failure.Cancelled);
                Drain(() => endpoint.ReservedBytes == 0 && !endpoint.HasPendingWork);
                Require(Directory.GetFiles(folder, "*.bwserver.zip").Length == 3, "Cancelled upload created a file");
                var readCancel = client.Read(updated); client.Cancel(readCancel); Error(readCancel, P.Failure.Cancelled);
                Drain(() => !endpoint.HasPendingWork && endpoint.ReservedBytes == 0 && requests.Count == 0 && replies.Count == 0);
                Require(!client.Busy, "Late cancelled response revived client operation");

                corrupt = true; Error(client.Read(updated), P.Failure.Invalid);
                Require(!client.Ready, "Corrupt download retained session");
                Drain(() => endpoint.ReservedBytes == 0 && !endpoint.HasPendingWork && requests.Count == 0 && replies.Count == 0);
                Wait(client.List());
                var timedOut = client.Read(updated);
                now += 31; client.Tick(now); Error(timedOut, P.Failure.Timeout);
                Drain(() => !endpoint.HasPendingWork && endpoint.ReservedBytes == 0 && requests.Count == 0 && replies.Count == 0);

                // A real localhost adapter can call Receive synchronously; no completion may revive the operation.
                reentrant = true;
                var reentrantPublish = Wait(client.Publish(original, 0)).Reply.Item;
                Require(Wait(client.Read(reentrantPublish)).Document.SequenceEqual(original), "Reentrant adapter download failed");
                Wait(client.Delete(reentrantPublish));
                Require(!client.Busy && endpoint.ReservedBytes == 0, "Reentrant adapter leaked operation state");
                reentrant = false;
                var stale = client.List();
                Require(requests.Count == 1, "List request not submitted");
                var frame = P.Decode(requests.Dequeue());
                client.Receive(P.Encode(new P.Message
                {
                    Kind = P.Command.ListResult,
                    World = frame.World,
                    Request = Guid.NewGuid(),
                    Session = frame.Session,
                    Total = 0,
                    Items = Array.Empty<P.Item>()
                }), now);
                Require(!stale.IsCompleted, "Unrelated late response completed active request");
                client.Receive(P.Encode(new P.Message
                {
                    Kind = P.Command.ListResult,
                    World = frame.World,
                    Request = frame.Request,
                    Session = Guid.NewGuid(),
                    Total = 0,
                    Items = Array.Empty<P.Item>()
                }), now);
                Error(stale, P.Failure.Invalid);
                Require(!client.Ready && !client.IsAdministrator, "Wrong connection nonce retained client authority");
                Drain(() => requests.Count == 0 && replies.Count == 0);
                Wait(client.List());
                Wait(client.Delete(updated));
                Require(Directory.GetFiles(folder, "*.bwserver.zip").Length == 2, "Delete result/file count mismatch");
                var disposed = client.List(); client.Dispose(); Error(disposed, P.Failure.Unavailable);
                Require(!client.Ready && !client.Busy, "Disposed client retained operation");
            }
            Console.WriteLine("PASS server client/endpoint integration: bootstrap ACL, immutable multi-chunk upload/download, SHA, revision conflict, cancellation, timeout, late/stale replies, reentrant localhost, delete and disposal. In-process transport only; no native RPC or game proof.");

            BlueprintServerClient.Result Wait(Task<BlueprintServerClient.Result> task)
            { now += .5; Drain(() => task.IsCompleted); return task.GetAwaiter().GetResult(); }
            void Error(Task<BlueprintServerClient.Result> task, P.Failure expected)
            {
                try { Wait(task); throw new Exception("Expected " + expected); }
                catch (BlueprintServerClient.Failure failure) { Require(failure.Code == expected, "Unexpected client failure " + failure.Code + ", expected " + expected); }
            }
            void Drain(Func<bool> done)
            {
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (!done())
                {
                    Require(DateTime.UtcNow < deadline, "Client/endpoint operation did not settle");
                    now += .001; client?.Tick(now);
                    while (requests.Count != 0) endpoint.Receive(connection, requests.Dequeue(), now);
                    endpoint.Tick(now);
                    while (replies.Count != 0) client.Receive(replies.Dequeue(), now);
                    Thread.Sleep(1);
                }
            }
        }
        private static void Require(bool value, string error) { if (!value) throw new Exception(error); }
    }
}
