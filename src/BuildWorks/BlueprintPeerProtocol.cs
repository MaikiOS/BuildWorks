using System;
using System.IO;
using System.Linq;
using System.Text;
using P = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks
{
    // A separate direct-RPC channel; no claimed sender, role or Origin in requests.
    internal static class BlueprintPeerProtocol
    {
        internal const int MaximumPeers = 64, MaximumPendingOffers = 16, MaximumOfferRecords = 256;
        internal enum Command : byte { Hello, Welcome, Roster, RosterResult, OfferStart, Chunk, Commit, Offered,
            Inbox, InboxResult, Read, DataStart, Accept, Decline, Withdraw, Status, StatusResult, Cancel, Error }
        internal enum OfferState : byte { Pending, Accepted, Declined, Withdrawn, Expired }
        internal sealed class Participant { internal Guid Session; internal string Name; }
        internal sealed class Offer
        {
            internal Guid Id, SenderSession, RecipientSession, DocumentId;
            internal string Sender, Recipient, Name, Sha256;
            internal int Bytes, Parts, FormatVersion;
            internal OfferState State;
        }
        internal sealed class Message
        {
            internal Command Kind;
            internal long World;
            internal Guid Request, Session, Target, Id;
            internal string Principal, Name, Sha256;
            internal int Length, Offset;
            internal byte[] Bytes;
            internal Participant[] Participants;
            internal Offer[] Offers;
            internal Offer Offer;
            internal P.Failure Error;
        }
        internal static byte[] Encode(Message message)
        {
            if (message == null) throw Invalid();
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory, new UTF8Encoding(false, true)))
            {
                writer.Write((byte)1); writer.Write((byte)message.Kind); writer.Write(message.World);
                writer.Write(message.Request.ToByteArray()); writer.Write(message.Session.ToByteArray());
                switch (message.Kind)
                {
                    case Command.Hello: case Command.Roster: case Command.Inbox: case Command.Commit: case Command.Cancel: break;
                    case Command.Welcome: P.WriteText(writer, message.Principal); P.WriteText(writer, message.Name); break;
                    case Command.RosterResult:
                        if (message.Participants == null || message.Participants.Length > MaximumPeers) throw Invalid();
                        writer.Write(message.Participants.Length);
                        foreach (var peer in message.Participants) { if (peer == null) throw Invalid(); writer.Write(peer.Session.ToByteArray()); P.WriteText(writer, peer.Name); }
                        break;
                    case Command.OfferStart:
                        writer.Write(message.Target.ToByteArray()); writer.Write(message.Length); P.WriteHash(writer, message.Sha256); break;
                    case Command.Chunk:
                        if (message.Bytes == null || message.Bytes.Length < 1 || message.Bytes.Length > P.ChunkBytes) throw Invalid();
                        writer.Write(message.Offset); writer.Write(message.Bytes.Length); writer.Write(message.Bytes); break;
                    case Command.Offered: case Command.DataStart: case Command.StatusResult: WriteOffer(writer, message.Offer); break;
                    case Command.InboxResult:
                        if (message.Offers == null || message.Offers.Length > MaximumPendingOffers) throw Invalid();
                        writer.Write(message.Offers.Length); foreach (var offer in message.Offers) WriteOffer(writer, offer); break;
                    case Command.Read: case Command.Accept: case Command.Decline: case Command.Withdraw: case Command.Status:
                        writer.Write(message.Id.ToByteArray()); break;
                    case Command.Error: writer.Write((byte)message.Error); break;
                    default: throw Invalid();
                }
                writer.Flush(); if (memory.Length > P.MaximumFrameBytes) throw Invalid();
                byte[] frame = memory.ToArray(); Decode(frame); return frame;
            }
        }
        internal static Message Decode(byte[] frame)
        {
            if (frame == null || frame.Length < 42 || frame.Length > P.MaximumFrameBytes) throw Invalid();
            try
            {
                using (var memory = new MemoryStream(frame, false))
                using (var reader = new BinaryReader(memory, new UTF8Encoding(false, true)))
                {
                    if (reader.ReadByte() != 1) throw Invalid();
                    var message = new Message { Kind = (Command)reader.ReadByte(), World = reader.ReadInt64(),
                        Request = P.ReadGuid(reader), Session = P.ReadGuid(reader) };
                    if (message.World == 0 || message.Request == Guid.Empty ||
                        (message.Kind == Command.Hello ? message.Session != Guid.Empty : message.Session == Guid.Empty)) throw Invalid();
                    switch (message.Kind)
                    {
                        case Command.Hello: case Command.Roster: case Command.Inbox: case Command.Commit: case Command.Cancel: break;
                        case Command.Welcome:
                            message.Principal = P.ReadText(reader); message.Name = P.ReadText(reader);
                            if (!Label(message.Principal, 256) || !Label(message.Name, 64)) throw Invalid(); break;
                        case Command.RosterResult:
                            message.Participants = new Participant[P.ReadRange(reader, 0, MaximumPeers)];
                            for (int i = 0; i < message.Participants.Length; ++i)
                            {
                                var peer = new Participant { Session = P.ReadGuid(reader), Name = P.ReadText(reader) };
                                if (peer.Session == Guid.Empty || !Label(peer.Name, 64) || message.Participants.Take(i).Any(value => value.Session == peer.Session)) throw Invalid();
                                message.Participants[i] = peer;
                            }
                            break;
                        case Command.OfferStart:
                            message.Target = P.ReadGuid(reader); if (message.Target == Guid.Empty || message.Target == message.Session) throw Invalid();
                            message.Length = P.ReadRange(reader, 1, P.MaximumDocumentBytes); message.Sha256 = P.ReadHash(reader); break;
                        case Command.Chunk:
                            message.Offset = P.ReadRange(reader, 0, P.MaximumDocumentBytes - 1);
                            message.Bytes = P.ReadExact(reader, P.ReadRange(reader, 1, Math.Min(P.ChunkBytes, P.MaximumDocumentBytes - message.Offset))); break;
                        case Command.Offered: case Command.DataStart: case Command.StatusResult: message.Offer = ReadOffer(reader); break;
                        case Command.InboxResult:
                            message.Offers = new Offer[P.ReadRange(reader, 0, MaximumPendingOffers)];
                            for (int i = 0; i < message.Offers.Length; ++i)
                            {
                                var offer = ReadOffer(reader);
                                if (offer.State != OfferState.Pending || message.Offers.Take(i).Any(value => value.Id == offer.Id)) throw Invalid();
                                message.Offers[i] = offer;
                            }
                            break;
                        case Command.Read: case Command.Accept: case Command.Decline: case Command.Withdraw: case Command.Status:
                            message.Id = P.ReadGuid(reader); if (message.Id == Guid.Empty) throw Invalid(); break;
                        case Command.Error:
                            message.Error = (P.Failure)reader.ReadByte(); if ((byte)message.Error > (byte)P.Failure.Timeout) throw Invalid(); break;
                        default: throw Invalid();
                    }
                    if (memory.Position != memory.Length) throw Invalid(); return message;
                }
            }
            catch (Exception failure) when (failure is EndOfStreamException || failure is DecoderFallbackException || failure is ArgumentException)
            { throw Invalid(); }
        }
        private static void WriteOffer(BinaryWriter writer, Offer offer)
        {
            if (offer == null) throw Invalid();
            writer.Write(offer.Id.ToByteArray()); writer.Write(offer.SenderSession.ToByteArray()); writer.Write(offer.RecipientSession.ToByteArray());
            writer.Write(offer.DocumentId.ToByteArray()); P.WriteText(writer, offer.Sender); P.WriteText(writer, offer.Recipient); P.WriteText(writer, offer.Name);
            P.WriteHash(writer, offer.Sha256); writer.Write(offer.Bytes); writer.Write(offer.Parts); writer.Write(offer.FormatVersion); writer.Write((byte)offer.State);
        }
        private static Offer ReadOffer(BinaryReader reader)
        {
            var offer = new Offer { Id = P.ReadGuid(reader), SenderSession = P.ReadGuid(reader), RecipientSession = P.ReadGuid(reader),
                DocumentId = P.ReadGuid(reader), Sender = P.ReadText(reader), Recipient = P.ReadText(reader), Name = P.ReadText(reader), Sha256 = P.ReadHash(reader),
                Bytes = P.ReadRange(reader, 1, P.MaximumDocumentBytes), Parts = P.ReadRange(reader, 1, CompositeBlueprintStore.MaximumParts),
                FormatVersion = P.ReadRange(reader, 1, BlueprintExchangeFormat.Version), State = (OfferState)reader.ReadByte() };
            if (offer.Id == Guid.Empty || offer.DocumentId == Guid.Empty || offer.SenderSession == Guid.Empty || offer.RecipientSession == Guid.Empty ||
                offer.SenderSession == offer.RecipientSession || !Label(offer.Sender, 256) || !Label(offer.Recipient, 256) || !Label(offer.Name, 48) ||
                (byte)offer.State > (byte)OfferState.Expired) throw Invalid();
            return offer;
        }
        internal static bool Label(string value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum &&
            value == value.Trim() && !value.Any(c => char.IsControl(c) || c == '<' || c == '>');
        internal static Offer Snapshot(Offer offer) => Decode(Encode(new Message { Kind = Command.StatusResult, World = 1,
            Request = Guid.NewGuid(), Session = Guid.NewGuid(), Offer = offer })).Offer;
        private static InvalidDataException Invalid() => new InvalidDataException("Invalid peer exchange frame.");
    }
}
