using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using P = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ServerEndpointChecks
    {
        private sealed class Connection
        {
            internal string Principal;
            internal bool Admin, Connected = true;
            internal Guid Session;
            internal readonly List<P.Message> Replies = new List<P.Message>();
        }
        internal static void Run(string folder, byte[] document)
        {
            var seed = new ServerBlueprintLibrary(folder, _ => true);
            var publicInfo = seed.Publish("admin", document, ServerBlueprintLibrary.Access.Players);
            var privateInfo = seed.Publish("admin", document, ServerBlueprintLibrary.Access.Administrators);
            int owner = Environment.CurrentManagedThreadId;
            var admin = new Connection { Principal = "admin", Admin = true };
            var player = new Connection { Principal = "player" };
            var claimed = new Connection { Principal = "admin" };
            double now = 0; bool loseReply = false, revokeRead = false;
            BlueprintServerEndpoint endpoint = null;
            var library = new ServerBlueprintLibrary(folder, principal => endpoint.IsAdministrator(principal));
            using (endpoint = new BlueprintServerEndpoint(library, 1001,
                connection =>
                {
                    Require(Environment.CurrentManagedThreadId == owner, "Endpoint resolves native identity on worker");
                    var value = (Connection)connection;
                    return value.Connected ? (value.Principal, value.Admin) : (null, false);
                }, (connection, bytes) =>
                {
                    Require(Environment.CurrentManagedThreadId == owner, "Endpoint sends native RPC on worker");
                    var reply = P.Decode(bytes); var value = (Connection)connection;
                    if (loseReply && reply.Kind == P.Command.Published) { loseReply = false; throw new IOException("Reply lost after commit"); }
                    value.Replies.Add(reply);
                    if (revokeRead && reply.Kind == P.Command.DataStart) { value.Admin = false; revokeRead = false; }
                }, maximumTransferBytes: 1024 * 1024))
            {
                Pump(() => !endpoint.HasPendingWork);
                Bootstrap(admin); Bootstrap(player); Bootstrap(claimed);
                var publicList = player.Replies.Last();
                Require(publicList.Kind == P.Command.ListResult && !publicList.Administrator && publicList.Items.Single().Id.ToString("N") == publicInfo.Id,
                    "Player list exposed restricted metadata");
                Require(claimed.Replies.Last().Items.Length == 1, "Other connection reused administrator authority for the same principal");
                var denied = Send(player, new P.Message { Kind = P.Command.Read, Id = Guid.Parse(privateInfo.Id), Revision = 1 });
                Require(Last(player, denied).Error == P.Failure.Unavailable && endpoint.ReservedBytes == 0, "Restricted read allocated or returned bytes");
                denied = Send(player, Start());
                Require(Last(player, denied).Error == P.Failure.Denied && endpoint.ReservedBytes == 0, "Player upload allocated memory before permission check");

                var reading = Send(player, new P.Message { Kind = P.Command.Read, Id = Guid.Parse(publicInfo.Id), Revision = 1 });
                Pump(() => player.Replies.Any(reply => reply.Request == reading && (reply.Kind == P.Command.Commit || reply.Kind == P.Command.Error)));
                var received = player.Replies.Where(reply => reply.Request == reading).ToArray();
                using (var transfer = new P.Transfer(received.First(reply => reply.Kind == P.Command.DataStart).Item.Bytes, Hash(document)))
                {
                    foreach (var chunk in received.Where(reply => reply.Kind == P.Command.Chunk)) transfer.Append(chunk.Offset, chunk.Bytes);
                    Require(transfer.Complete().SequenceEqual(document), "Read stream changed document bytes");
                }
                Require(endpoint.ReservedBytes == 0, "Completed download leaked reservation");
                revokeRead = true;
                var revokedRead = Send(admin, new P.Message { Kind = P.Command.Read, Id = Guid.Parse(privateInfo.Id), Revision = 1 });
                Pump(() => admin.Replies.Any(reply => reply.Request == revokedRead && reply.Kind == P.Command.Error));
                Require(!admin.Replies.Any(reply => reply.Request == revokedRead && reply.Kind == P.Command.Chunk) && endpoint.ReservedBytes == 0,
                    "Revoked administrator received a data chunk"); admin.Admin = true;

                var header = Start(); var publication = Send(admin, header);
                Send(admin, new P.Message { Kind = P.Command.Chunk, Request = publication, Bytes = document });
                loseReply = true;
                Send(admin, new P.Message { Kind = P.Command.Commit, Request = publication });
                Send(admin, new P.Message { Kind = P.Command.Commit, Request = publication }); // In-flight duplicate is harmless.
                Pump(() => !endpoint.HasPendingWork && endpoint.ReservedBytes == 0);
                int publishedFiles = Directory.GetFiles(folder, "*.bwserver.zip").Length;
                Send(admin, header); // Lost receipt: same connection/request must return one result.
                var published = Last(admin, publication);
                Require(published.Kind == P.Command.Published && publishedFiles == 3 && Directory.GetFiles(folder, "*.bwserver.zip").Length == 3,
                    "Lost publication reply caused duplication or lost receipt");
                Send(admin, new P.Message { Kind = P.Command.Commit, Request = publication });
                Require(Last(admin, publication).Kind == P.Command.Published, "Committed retry was not idempotent");

                var bad = Start(); var malformed = Send(admin, bad);
                Send(admin, new P.Message { Kind = P.Command.Chunk, Request = malformed, Offset = 1, Bytes = new byte[] { 1 } });
                Require(Last(admin, malformed).Kind == P.Command.Error && endpoint.ReservedBytes == 0 && Directory.GetFiles(folder, "*.bwserver.zip").Length == 3,
                    "Out-of-order upload left state or a publication");
                var abandoned = Send(admin, Start());
                Send(admin, new P.Message { Kind = P.Command.Cancel, Request = abandoned });
                Require(endpoint.ReservedBytes == 0, "Cancelled upload retained memory");
                foreach (bool staleSession in new[] { false, true })
                {
                    Guid active = Send(admin, Start());
                    byte[] invalid = staleSession ? P.Encode(new P.Message
                    {
                        Kind = P.Command.List,
                        World = 1001,
                        Request = Guid.NewGuid(),
                        Session = Guid.NewGuid()
                    }) : new byte[P.MaximumFrameBytes + 1];
                    endpoint.Receive(admin, invalid, now);
                    Require(endpoint.ReservedBytes == 0 && Last(admin, active).Error == P.Failure.Invalid &&
                        Directory.GetFiles(folder, "*.bwserver.zip").Length == 3, "Malformed/session frame pinned active upload quota");
                }

                var peer2 = new Connection { Principal = "admin2", Admin = true };
                var peer3 = new Connection { Principal = "admin3", Admin = true };
                Bootstrap(peer2); Bootstrap(peer3);
                var large1 = Start(); large1.Length = 256 * 1024; var large2 = Start(); large2.Length = 256 * 1024;
                var slot1 = Send(admin, large1); var slot2 = Send(peer2, large2);
                var full = Start(); full.Length = 256 * 1024; var fullId = Send(peer3, full);
                Require(endpoint.ReservedBytes == 1024 * 1024 && Last(peer3, fullId).Error == P.Failure.Capacity,
                    "Aggregate incoming memory quota was bypassed");
                Send(admin, new P.Message { Kind = P.Command.Cancel, Request = slot1 });
                Send(peer2, new P.Message { Kind = P.Command.Cancel, Request = slot2 });
                Require(endpoint.ReservedBytes == 0, "Released quota was not reusable");

                var interrupted = Send(admin, Start());
                Guid oldSession = admin.Session; endpoint.Disconnect(admin);
                Require(endpoint.ReservedBytes == 0, "Disconnect retained upload"); Bootstrap(admin);
                var replay = Start(); replay.Request = interrupted; replay.Session = oldSession;
                var replayId = Send(admin, replay, preserveSession: true);
                Require(admin.Session != oldSession && Last(admin, replayId).Error == P.Failure.Invalid && endpoint.ReservedBytes == 0,
                    "Old connection session replay was accepted");
                var timedOut = Send(admin, Start()); now += 31; endpoint.Tick(now);
                Require(Last(admin, timedOut).Error == P.Failure.Timeout && endpoint.ReservedBytes == 0, "Inactive upload did not time out");

                var pending = Send(admin, Start());
                Send(admin, new P.Message { Kind = P.Command.Chunk, Request = pending, Bytes = document });
                Send(admin, new P.Message { Kind = P.Command.Commit, Request = pending }); admin.Admin = false;
                Pump(() => !endpoint.HasPendingWork && endpoint.ReservedBytes == 0);
                Require(Last(admin, pending).Kind == P.Command.Error && Directory.GetFiles(folder, "*.bwserver.zip").Length == 3 &&
                    Directory.GetFiles(folder, "*.tmp.zip").Length == 0, "Commit-time revocation published or leaked staging"); admin.Admin = true;

                var removal = Send(admin, new P.Message { Kind = P.Command.Delete, Id = published.Item.Id, Revision = published.Item.Revision });
                Require(Last(admin, removal).Kind == P.Command.Deleted, "Admin delete failed");
                Send(admin, new P.Message { Kind = P.Command.Delete, Request = removal, Id = published.Item.Id, Revision = published.Item.Revision });
                Require(Last(admin, removal).Kind == P.Command.Deleted && Directory.GetFiles(folder, "*.bwserver.zip").Length == 2,
                    "Delete retry repeated effects or lost receipt");
                now += 10; int count = player.Replies.Count;
                for (int i = 0; i < 40; ++i)
                    endpoint.Receive(player, P.Encode(new P.Message { Kind = P.Command.List, World = 1001, Request = Guid.NewGuid(), Session = player.Session }), now);
                Require(player.Replies.Count - count <= 16, "Request flood bypassed rate limit");
            }
            Console.WriteLine("PASS server endpoint: authenticated connection-scoped ACL, nonce/reconnect denial, read stream, revoked read/commit, ordered/cancel/timeout/quota/rate limits and lost-reply/idempotent publish/delete. In-process transport only; no native RPC registration/UI/game proof.");

            P.Message Start() => new P.Message { Kind = P.Command.PublishStart, Request = Guid.NewGuid(), Length = document.Length, Sha256 = Hash(document) };
            Guid Send(Connection connection, P.Message message, bool preserveSession = false)
            {
                if (message.Request == Guid.Empty) message.Request = Guid.NewGuid(); message.World = 1001;
                if (!preserveSession) message.Session = connection.Session;
                now += .3; endpoint.Receive(connection, P.Encode(message), now); return message.Request;
            }
            void Bootstrap(Connection connection)
            {
                var request = Send(connection, new P.Message { Kind = P.Command.List }, preserveSession: true);
                Require(Last(connection, request).Kind == P.Command.ListResult, "Endpoint bootstrap failed"); connection.Session = Last(connection, request).Session;
            }
            void Pump(Func<bool> complete)
            {
                for (int i = 0; i < 5000; ++i) { now += .001; endpoint.Tick(now); if (complete()) return; Thread.Sleep(1); }
                throw new Exception("Server endpoint timed out in fixture");
            }
        }
        private static P.Message Last(Connection connection, Guid request) => connection.Replies.Last(reply => reply.Request == request);
        private static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
        private static void Require(bool value, string error) { if (!value) throw new Exception(error); }
    }
}
