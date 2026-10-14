using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using P = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ServerProtocolChecks
    {
        internal static void Run()
        {
            byte[] content = Enumerable.Range(0, P.ChunkBytes + 19).Select(index => (byte)index).ToArray();
            string sha = Hash(content);
            var item = new P.Item
            {
                Id = Guid.NewGuid(),
                Revision = 7,
                Access = 1,
                Parts = 2,
                Bytes = content.Length,
                Name = "Дом с надписью 🪵",
                Category = "ДОМА",
                Publisher = "Steam_author",
                Editor = "Steam_editor",
                Sha256 = sha
            };
            foreach (P.Command kind in Enum.GetValues(typeof(P.Command)))
            {
                var message = new P.Message
                {
                    Kind = kind,
                    Request = Guid.NewGuid(),
                    Session = Guid.NewGuid(),
                    World = -123456,
                    Id = item.Id,
                    Revision = 7,
                    Access = 1,
                    Total = 1,
                    Length = content.Length,
                    Sha256 = sha,
                    Items = new[] { item },
                    Item = item,
                    Bytes = new byte[] { 1, 2, 3 },
                    Administrator = true,
                    Error = P.Failure.Busy
                };
                byte[] frame = P.Encode(message);
                var copy = P.Decode(frame);
                Require(copy.Kind == kind && copy.World == message.World && copy.Request == message.Request && frame.Length <= P.MaximumFrameBytes,
                    "Protocol command/header roundtrip failed");
                if (kind == P.Command.ListResult || kind == P.Command.DataStart || kind == P.Command.Published)
                {
                    var data = copy.Item ?? copy.Items.Single();
                    Require(data.Id == item.Id && data.Name == item.Name && data.Access == 1 && data.Revision == 7 && data.Sha256 == sha,
                        "Protocol item metadata/UTF-8 changed");
                }
                Refused(() => P.Decode(frame.Concat(new byte[] { 0 }).ToArray()));
                for (int length = 0; length < frame.Length; ++length)
                    Refused(() => P.Decode(frame.Take(length).ToArray()));
                var future = (byte[])frame.Clone(); future[0] = 2; Refused(() => P.Decode(future));
                var unknown = (byte[])frame.Clone(); unknown[1] = 255; Refused(() => P.Decode(unknown));
                var emptyRequest = (byte[])frame.Clone(); Array.Clear(emptyRequest, 10, 16); Refused(() => P.Decode(emptyRequest));
            }
            var start = new P.Message
            {
                Kind = P.Command.PublishStart,
                Request = Guid.NewGuid(),
                Session = Guid.NewGuid(),
                World = 1,
                Length = content.Length,
                Sha256 = sha,
                Access = 0
            };
            Require(P.Decode(P.Encode(start)).Id == Guid.Empty, "New publication unexpectedly requires caller-chosen entry ID");
            start.Revision = 1; Refused(() => P.Encode(start)); start.Revision = 0;
            start.Access = 2; Refused(() => P.Encode(start)); start.Access = 0;
            start.Length = int.MaxValue; Refused(() => P.Encode(start));
            Refused(() => P.Decode(new byte[P.MaximumFrameBytes + 1]));
            var page = new P.Message
            {
                Kind = P.Command.ListResult,
                Request = Guid.NewGuid(),
                Session = Guid.NewGuid(),
                World = 1,
                Total = P.PageSize,
                Items = Enumerable.Repeat(item, P.PageSize).ToArray()
            };
            item.Name = new string('Ж', 512); item.Category = new string('Ж', 512);
            item.Publisher = new string('Ж', 512); item.Editor = new string('Ж', 512);
            Require(P.Encode(page).Length <= P.MaximumFrameBytes, "Maximum full metadata page exceeds frame bound");
            page.Items = Enumerable.Repeat(item, P.PageSize + 1).ToArray(); Refused(() => P.Encode(page));
            item.Name += "Ж"; Refused(() => P.Encode(new P.Message { Kind = P.Command.Published, World = 1, Request = Guid.NewGuid(), Session = Guid.NewGuid(), Item = item }));
            Require(P.Decode(P.Encode(new P.Message { Kind = P.Command.List, World = 1, Request = Guid.NewGuid() })).Session == Guid.Empty,
                "Initial list cannot bootstrap a connection session");

            using (var transfer = new P.Transfer(content.Length, sha.ToLowerInvariant()))
            {
                byte[] first = content.Take(P.ChunkBytes).ToArray(); transfer.Append(0, first); first[0] ^= 1;
                Refused(() => transfer.Append(0, new byte[] { 1 }));
                Refused(() => transfer.Append(P.ChunkBytes + 1, new byte[] { 1 }));
                Refused(() => transfer.Complete());
                transfer.Append(P.ChunkBytes, content.Skip(P.ChunkBytes).ToArray());
                Require(transfer.Complete().SequenceEqual(content), "Transfer retained caller arrays or changed body");
                Refused(() => transfer.Complete()); Refused(() => transfer.Append(content.Length, new byte[] { 1 }));
            }
            using (var transfer = new P.Transfer(3, Hash(new byte[] { 1, 2, 3 })))
            { transfer.Append(0, new byte[] { 3, 2, 1 }); Refused(() => transfer.Complete()); Refused(() => transfer.Complete()); }
            using (var transfer = new P.Transfer(1, Hash(new byte[] { 1 })))
            { Refused(() => transfer.Append(0, new byte[] { 1, 2 })); transfer.Dispose(); Refused(() => transfer.Append(0, new byte[] { 1 })); }
            Refused(() => new P.Transfer(int.MaxValue, sha)); Refused(() => new P.Transfer(1, new string(' ', 64)));
            Console.WriteLine("PASS server protocol: all commands/UTF-8/page limits, every truncation/trailing/future/unknown/empty request refusal, bounded lengths, ordered copied chunks, SHA and one-use/dispose. No live RPC tested.");
        }

        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Refused(Action action)
        {
            try { action(); } catch (InvalidDataException) { return; }
            throw new Exception("Expected rejected server protocol data");
        }
        private static void Require(bool value, string error) { if (!value) throw new Exception(error); }
    }
}
