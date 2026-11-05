using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using P = OstrixMods.BuildWorks.BlueprintPeerProtocol;
using S = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PeerProtocolChecks
    {
        internal static void Run()
        {
            var budget = new BlueprintTransferBudget(17);
            Require(!budget.TryReserve(-1) && !budget.TryReserve(long.MaxValue) && budget.Reserved == 0, "Invalid reservation changed budget");
            Require(budget.TryReserve(17) && !budget.TryReserve(1), "Exact budget boundary bypassed");
            InvalidRelease(-1); InvalidRelease(18);
            Require(budget.Reserved == 17, "Invalid release changed reservation");
            budget.Release(17);
            Parallel.For(0, 10000, _ =>
            {
                if (!budget.TryReserve(1)) return;
                Require(budget.Reserved >= 1 && budget.Reserved <= 17, "Concurrent reservation exceeded limit");
                budget.Release(1);
            });
            Require(budget.Reserved == 0 && budget.TryReserve(17), "Concurrent release leaked quota");
            budget.Release(17);

            var offer = new P.Offer { Id = Guid.NewGuid(), DocumentId = Guid.NewGuid(), SenderSession = Guid.NewGuid(),
                RecipientSession = Guid.NewGuid(), Sender = "Steam_sender", Recipient = "Steam_recipient", Name = "Дом 🪵",
                Sha256 = new string('a', 64), Bytes = 25, Parts = 2, FormatVersion = 1, State = P.OfferState.Pending };
            foreach (P.Command kind in Enum.GetValues(typeof(P.Command)))
            {
                var message = Message(kind);
                byte[] frame = P.Encode(message);
                var copy = P.Decode(frame);
                Require(copy.Kind == kind && copy.World == message.World && copy.Session == message.Session && copy.Request == message.Request,
                    "Peer header changed");
                Require(P.Encode(copy).SequenceEqual(frame), "Peer payload changed on roundtrip");
                for (int length = 0; length < frame.Length; ++length) Refused(() => P.Decode(frame.Take(length).ToArray()));
                Refused(() => P.Decode(frame.Concat(new byte[] { 0 }).ToArray()));
                var corrupt = (byte[])frame.Clone(); corrupt[0] = 2; Refused(() => P.Decode(corrupt));
                corrupt[0] = 1; corrupt[1] = 255; Refused(() => P.Decode(corrupt));
                var emptyRequest = (byte[])frame.Clone(); Array.Clear(emptyRequest, 10, 16); Refused(() => P.Decode(emptyRequest));
                var wrongSession = (byte[])frame.Clone();
                if (kind == P.Command.Hello) wrongSession[26] = 1; else Array.Clear(wrongSession, 26, 16);
                Refused(() => P.Decode(wrongSession));
            }
            var start = Message(P.Command.OfferStart);
            start.Length = 0; Refused(() => P.Encode(start));
            start.Length = S.MaximumDocumentBytes + 1; Refused(() => P.Encode(start));
            start.Length = 1; start.Target = start.Session; Refused(() => P.Encode(start));
            start.Target = Guid.NewGuid(); start.Sha256 = "not-a-hash"; Refused(() => P.Encode(start));
            var chunk = Message(P.Command.Chunk);
            chunk.Bytes = new byte[S.ChunkBytes + 1]; Refused(() => P.Encode(chunk));
            chunk.Bytes = new byte[] { 1, 2 }; chunk.Offset = S.MaximumDocumentBytes - 1; Refused(() => P.Encode(chunk));
            var roster = Message(P.Command.RosterResult);
            roster.Participants = new[] { roster.Participants[0], roster.Participants[0] }; Refused(() => P.Encode(roster));
            roster.Participants = new P.Participant[P.MaximumPeers + 1]; Refused(() => P.Encode(roster));
            var inbox = Message(P.Command.InboxResult);
            inbox.Offers = new[] { offer, offer }; Refused(() => P.Encode(inbox));
            inbox.Offers = new P.Offer[P.MaximumPendingOffers + 1]; Refused(() => P.Encode(inbox));
            offer.State = P.OfferState.Accepted; inbox.Offers = new[] { offer }; Refused(() => P.Encode(inbox));
            Require(P.Snapshot(offer).State == P.OfferState.Accepted, "Status lost terminal state");
            var snapshot = P.Snapshot(offer); snapshot.Name = "Changed";
            Require(offer.Name == "Дом 🪵", "Offer snapshot aliases source");
            offer.SenderSession = offer.RecipientSession; Refused(() => P.Snapshot(offer));
            foreach (string label in new[] { "", " x", "x ", "x\n", "<x>", new string('a', 65) })
                Require(!P.Label(label, 64), "Invalid display label accepted");
            Console.WriteLine("PASS peer codec: all commands, every truncated frame, strict headers, bounds, duplicate offers/peers, terminal snapshot; shared budget boundaries/concurrency. No peer transport/UI/network proof.");

            P.Message Message(P.Command kind) => new P.Message { Kind = kind, World = 1001, Request = Guid.NewGuid(),
                Session = kind == P.Command.Hello ? Guid.Empty : offer.SenderSession, Target = offer.RecipientSession,
                Id = offer.Id, Principal = "Steam_sender", Name = "Игрок 🪵", Sha256 = offer.Sha256, Length = offer.Bytes,
                Bytes = new byte[] { 1, 2, 3 }, Participants = new[] { new P.Participant { Session = offer.RecipientSession, Name = "Получатель" } },
                Offers = new[] { offer }, Offer = offer, Error = S.Failure.Capacity };
            void InvalidRelease(long bytes)
            {
                try { budget.Release(bytes); } catch (InvalidOperationException) { return; }
                throw new Exception("Invalid release accepted");
            }
        }
        private static void Require(bool valid, string message) { if (!valid) throw new Exception(message); }
        private static void Refused(Action action)
        {
            try { action(); } catch (InvalidDataException) { return; }
            throw new Exception("Malformed peer frame accepted");
        }
    }
}
