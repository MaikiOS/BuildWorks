using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class WorldStorageReadbackChecks
    {
        private static readonly Guid Operation = new Guid("01234567-89ab-cdef-0123-456789abcdef");
        private static readonly byte[] Inventory = { 109, 0, 0, 0, 0, 0 };
        private static int assertions;
        internal static void Run()
        {
            var fixture = new Fixture();
            byte[] receipt = Encoding.UTF8.GetBytes("project/part/operation/exact-receipt");
            var receiptExpected = NativeWorldStorageReadback.Expectation.ForExactBlob(71, Operation, 43, 100, 1, 2, 3, receipt);
            var receiptFixture = new Fixture { Chunk = Chunk(inventory: receipt) };
            receipt[0] ^= 1;
            Check(receiptFixture.Verify(out var receiptEvidence, receiptExpected) && receiptEvidence.PayloadSha256.Length == 64,
                "Exact receipt blob was rejected or aliased caller data");
            Check(!new Fixture().Verify(out _, receiptExpected), "Native inventory silently substituted for the exact receipt");
            bool refusedInventory = false;
            try { Expect(Encoding.UTF8.GetBytes("not-an-inventory")); } catch (ArgumentException) { refusedInventory = true; }
            Check(refusedInventory, "Receipt support weakened the native inventory constructor");
            foreach (byte[] invalid in new[] { Array.Empty<byte>(), new byte[65537] })
            {
                bool refused = false;
                try { NativeWorldStorageReadback.Expectation.ForExactBlob(71, Operation, 43, 100, 1, 2, 3, invalid); }
                catch (ArgumentException) { refused = true; }
                Check(refused, "Unbounded/empty construction receipt accepted");
            }
            Check(fixture.Verify(out var evidence) && evidence.World == 71 && evidence.Operation == Operation &&
                evidence.FormatVersion == 41 &&
                evidence.Chunks == 1 && evidence.Objects == 1 && evidence.Chunk == "01_02__3_1.chunk" && evidence.ObjectOffset == 6 &&
                evidence.InventorySha256.Length == 64, "Exact world/chunk identity evidence missing");
            byte[] currentInventory = (byte[])Inventory.Clone(); currentInventory[0] = 110;
            var current = new Fixture { Chunk = Chunk(inventory: currentInventory), Ok = BitConverter.GetBytes(42) };
            current.Meta[4] = 42; current.Map[0] = 42; current.Chunk[0] = 42;
            Check(current.Verify(out var currentEvidence, Expect(currentInventory)) && currentEvidence.FormatVersion == 42,
                "World42/inventory110 structural evidence rejected");
            current.Chunk[0] = 41;
            Check(current.Verify(out _, Expect(currentInventory)), "Retained immutable world41 chunk rejected by world42 generation");
            current.Meta[4] = 41;
            Check(!current.Verify(out _, Expect(currentInventory)), "Mixed committed metadata/completion versions accepted");
            current.Meta[4] = 42; current.Map[0] = 41;
            Check(!current.Verify(out _, Expect(currentInventory)), "Mixed committed mapping/completion versions accepted");
            var expectedBytes = (byte[])Inventory.Clone();
            var frozen = Expect(expectedBytes); expectedBytes[4] = 1;
            Check(fixture.Verify(out _, frozen), "Expected native bytes aliased caller");
            foreach (var flags in new[] { 256 | 32 | 64 | 128, 0x3fff, 0x3fff & ~8192 })
            {
                fixture.Chunk = Chunk(flags: flags);
                Check(fixture.Verify(out _), "Supported flags/position/rotation/maps rejected");
            }
            fixture = new Fixture { Chunk = Chunk(largeMap: true) };
            Check(fixture.Verify(out _), "Native two-byte map count rejected");
            fixture = new Fixture(); fixture.Map = Mapping(2);
            Check(fixture.Verify(out _), "Native size-changed overcount rejected");
            fixture.Map = Mapping(0); Check(!fixture.Verify(out _), "Mapping declared undercount accepted");
            fixture = new Fixture { Chunk = Chunk(marker: Guid.Empty.ToString("N")) };
            Check(!fixture.Verify(out _), "Missing operation marker accepted");
            foreach (var chunk in new[] { Chunk(creator: 101), Chunk(prefab: 44), Chunk(inventory: new byte[] {109,0,0,0,1,0}),
                Chunk(flags: 32 | 64 | 128), Chunk(duplicateKey: true), Chunk(flags: 0x4000), Chunk(nonfinite: true) })
            { fixture = new Fixture { Chunk = chunk }; Check(!fixture.Verify(out _), "Wrong or corrupt marked object accepted"); }
            fixture = new Fixture { Chunk = Chunk(count: 2), Map = Mapping(2, objects: 2) };
            Check(!fixture.Verify(out _), "Duplicate marker in same chunk accepted");
            fixture = new Fixture { Map = Mapping(2, chunks: 2) };
            Check(!fixture.Verify(out _), "Duplicate marker in another mapped chunk accepted");
            fixture = new Fixture { Meta = Metadata(72) };
            Check(!fixture.Verify(out _), "Different world UID accepted");
            fixture = new Fixture();
            foreach (string field in new[] { "Meta", "Map", "Chunk", "Ok" })
            {
                var member = typeof(Fixture).GetField(field);
                var valid = (byte[])member.GetValue(fixture);
                for (int n = 0; n < valid.Length; ++n)
                {
                    member.SetValue(fixture, valid.Take(n).ToArray());
                    Check(!fixture.Verify(out _), "Truncated " + field + " accepted at " + n);
                }
                member.SetValue(fixture, valid.Concat(new byte[] { 0 }).ToArray());
                Check(!fixture.Verify(out _), "Trailing " + field + " accepted");
                member.SetValue(fixture, valid);
            }
            fixture.Ok = BitConverter.GetBytes(43); Check(!fixture.Verify(out _), "Future completion accepted");
            fixture = new Fixture(); fixture.Meta[4] = 43; Check(!fixture.Verify(out _), "Future world metadata accepted");
            fixture = new Fixture(); fixture.Map[0] = 43; Check(!fixture.Verify(out _), "Future mapping accepted");
            fixture = new Fixture(); fixture.Chunk[0] = 43; Check(!fixture.Verify(out _), "Future chunk accepted");
            fixture = new Fixture();
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Check(!fixture.Verify(out _, cancellation: cancel.Token), "Cancelled readback accepted");
            }
            fixture.Missing = true; Check(!fixture.Verify(out _), "Missing committed chunk accepted");
            fixture = new Fixture { Map = Mapping(2, chunks: 2) };
            fixture.Map[21] = fixture.Map[10]; fixture.Map[22] = fixture.Map[11];
            Check(!fixture.Verify(out _), "Duplicate chunk mapping accepted");
            fixture = new Fixture(); fixture.Chunk[24] = 128;
            Check(!fixture.Verify(out _), "Noncanonical extended typed-map count accepted");
            fixture = new Fixture();
            int markerOffset = Find(fixture.Chunk, Encoding.UTF8.GetBytes(Operation.ToString("N")));
            fixture.Chunk[markerOffset] = 255;
            Check(!fixture.Verify(out _), "Invalid native UTF8 accepted");
            fixture = new Fixture();
            using (var meta = new MemoryStream(fixture.Meta))
            using (var map = new MemoryStream(fixture.Map))
            using (var ok = new MemoryStream(fixture.Ok))
                Check(!NativeWorldStorageReadback.TryVerify(meta, map, ok, _ => new Oversized(), Expect(Inventory), out _, out _),
                    "Oversized chunk read before budget check");
            fixture = new Fixture();
            using (var cancel = new CancellationTokenSource())
            using (var meta = new MemoryStream(fixture.Meta))
            using (var map = new MemoryStream(fixture.Map))
            using (var ok = new MemoryStream(fixture.Ok))
                Check(!NativeWorldStorageReadback.TryVerify(meta, map, ok, _ => new CancelOnDispose(fixture.Chunk, cancel), Expect(Inventory), out _, out _, cancel.Token),
                    "Cancellation at final chunk close returned an accepted receipt");
            fixture = new Fixture { Chunk = AppendTarget(Chunk(marker: "other", count: 10000), 10001), Map = Mapping(10001, objects: 10001) };
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool bulkPassed = fixture.Verify(out var bulkEvidence);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bulkPassed && bulkEvidence.Objects == 10001 && allocated < 4 * 1024 * 1024, "Per-object/map allocation regression: " + allocated);
            Console.WriteLine("World reader .NET10 CPU allocation:10001 records, " + allocated + " bytes; not a Valheim/Mono latency benchmark");
            fixture = new Fixture { Chunk = AppendTarget(Chunk(marker: "other", inventory: new byte[2 * 1024 * 1024]), 2), Map = Mapping(2, objects: 2) };
            before = GC.GetAllocatedBytesForCurrentThread();
            bool opaquePassed = fixture.Verify(out _);
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(opaquePassed && allocated < 512 * 1024, "Unrelated native byte-array value was decoded/allocated or blocked: " + allocated);
            fixture = new Fixture { Chunk = Chunk(opaqueText: new string('x', 2 * 1024 * 1024)) };
            before = GC.GetAllocatedBytesForCurrentThread();
            opaquePassed = fixture.Verify(out _); allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(opaquePassed && allocated < 512 * 1024, "Unrelated native string value was decoded/allocated or blocked: " + allocated);
            AbsenceChecks();
            CompositeAbsenceChecks();
            Console.WriteLine("PASS saved-world41/42 presence and batch absence: " + assertions + " bounded structural/identity/contents/duplicate/truncation/cancel assertions; no native world save, settlement or network proof");
        }
        private static void AbsenceChecks()
        {
            Guid other = new Guid("99999999-9999-9999-9999-999999999999");
            var ids = new[] { Operation, Guid.NewGuid() };
            var query = new NativeWorldStorageReadback.AbsenceExpectation(71, 2, ids);
            ids[0] = other;
            var fixture = new Fixture { Chunk = Chunk(marker: other.ToString("N")), Map = Mapping(3, chunks: 2) };
            Check(fixture.Absent(query, out var proof) && proof.World == 71 && proof.MarkerKey == 2 && proof.CompleteMappedScan &&
                proof.Chunks == 2 && proof.Objects == 2 && proof.Operations[0] == Operation && proof.Generation == 0 && fixture.Opens == 2,
                "Batch absence failed, aliased caller identities, or did not finish all mapped chunks exactly once");
            foreach (string marker in new[] { Operation.ToString("N"), Operation.ToString("N").ToUpperInvariant(), query.Operations[1].ToString("N"), "malformed" })
            {
                fixture = new Fixture { Map = Mapping(2, chunks: 2) };
                fixture.AbsenceChunk = name => new MemoryStream(Chunk(marker: name.StartsWith("01_02", StringComparison.Ordinal)
                    ? other.ToString("N") : marker, creator: 17, prefab: 99), false);
                Check(!fixture.Absent(query, out proof) && proof == null && fixture.Opens == 2,
                    "Late marker, changed-case marker or wrong prefab/creator counted as absent");
            }
            foreach (byte[] bad in new[] { Chunk(marker: other.ToString("N"), duplicateKey: true), Chunk(marker: other.ToString("N"), nonfinite: true),
                Chunk(marker: other.ToString("N"), flags: 0x4000), Chunk(marker: other.ToString("N"), count: 2),
                Chunk(marker: other.ToString("N"), markerWrongType: true) })
            {
                fixture = new Fixture { Chunk = bad };
                Check(!fixture.Absent(query, out proof) && proof == null, "Corrupt or mismatched chunk accepted as absence");
            }
            fixture = new Fixture { Chunk = Chunk(marker: "") };
            Check(fixture.Absent(query, out _), "Unmarked ordinary object prevented absence proof");
            fixture = new Fixture { Chunk = Chunk(marker: other.ToString("N")), Meta = Metadata(72) };
            Check(!fixture.Absent(query, out proof) && proof == null, "Other world accepted for batch absence");
            fixture = new Fixture { Chunk = Chunk(marker: other.ToString("N")), Missing = true };
            Check(!fixture.Absent(query, out proof) && proof == null, "Missing mapped chunk counted as absence");
            fixture = new Fixture { Chunk = Chunk(marker: other.ToString("N")) };
            foreach (string field in new[] { "Meta", "Map", "Chunk", "Ok" })
            {
                var member = typeof(Fixture).GetField(field); byte[] valid = (byte[])member.GetValue(fixture);
                for (int n = 0; n < valid.Length; ++n)
                {
                    member.SetValue(fixture, valid.Take(n).ToArray());
                    Check(!fixture.Absent(query, out proof) && proof == null, "Truncated absence " + field + " accepted at " + n);
                }
                member.SetValue(fixture, valid.Concat(new byte[] { 0 }).ToArray());
                Check(!fixture.Absent(query, out proof) && proof == null, "Trailing absence " + field + " accepted");
                member.SetValue(fixture, valid);
            }
            foreach (Guid[] invalid in new[] { Array.Empty<Guid>(), new[] { Guid.Empty }, new[] { Operation, Operation },
                Enumerable.Repeat(Operation, BlueprintEditorDocument.MaximumParts + 1).ToArray() })
            {
                bool rejected = false;
                try { new NativeWorldStorageReadback.AbsenceExpectation(71, 2, invalid); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Invalid/empty/duplicate/unbounded batch absence identity accepted");
            }
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); fixture = new Fixture { Chunk = Chunk(marker: other.ToString("N")) };
                Check(!fixture.Absent(query, out proof, cancel.Token) && proof == null && fixture.Opens == 0, "Pre-cancelled absence scanned files");
            }
            using (var cancel = new CancellationTokenSource())
            {
                fixture = new Fixture { AbsenceChunk = _ => new CancelOnDispose(Chunk(marker: other.ToString("N")), cancel) };
                Check(!fixture.Absent(query, out proof, cancel.Token) && proof == null, "Final chunk-close cancellation accepted as absence");
            }
            fixture = new Fixture { Map = Mapping(10001, objects: 10001), Chunk = Chunk(marker: other.ToString("N"), count: 10001) };
            long before = GC.GetAllocatedBytesForCurrentThread();
            Check(fixture.Absent(query, out proof) && proof.Objects == 10001 && fixture.Opens == 1 &&
                GC.GetAllocatedBytesForCurrentThread() - before < 4 * 1024 * 1024, "Batch absence rescanned or allocated per requested operation");
        }
        private static void CompositeAbsenceChecks()
        {
            Guid refund = Guid.NewGuid(), unrelated = Guid.NewGuid();
            var source = new[] { Operation };
            var first = new NativeWorldStorageReadback.AbsenceMarkerSet(2, source);
            var second = new NativeWorldStorageReadback.AbsenceMarkerSet(4, new[] { refund });
            var query = new NativeWorldStorageReadback.AbsenceExpectation(71, first, second);
            source[0] = unrelated;
            var fixture = new Fixture { Map = Mapping(2, chunks: 2), Chunk = Chunk(marker: unrelated.ToString("N"), opaqueText: unrelated.ToString("N")) };
            Check(fixture.Absent(query, out var proof) && proof.MarkerSets.Count == 2 && proof.MarkerSets[0].Operations[0] == Operation &&
                proof.MarkerSets[1].Operations[0] == refund && proof.Objects == 2 && fixture.Opens == 2 && proof.Generation == 0,
                "Composite absence did not scan both immutable sets and all chunks once");
            foreach (Action legacy in new Action[] { () => { _ = query.MarkerKey; }, () => { _ = query.Operations; },
                () => { _ = proof.MarkerKey; }, () => { _ = proof.Operations; } })
            {
                bool rejected = false; try { legacy(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Composite proof silently exposed only one legacy marker set");
            }
            foreach (var pair in new[] { (Operation.ToString("N"), unrelated.ToString("N")),
                (unrelated.ToString("N"), refund.ToString("N").ToUpperInvariant()), (Operation.ToString("N"), refund.ToString("N")),
                (unrelated.ToString("N"), "malformed"), ("malformed", unrelated.ToString("N")) })
            {
                fixture = new Fixture { Map = Mapping(2, chunks: 2) };
                fixture.AbsenceChunk = name => new MemoryStream(name.StartsWith("01_02", StringComparison.Ordinal)
                    ? Chunk(marker: unrelated.ToString("N"), opaqueText: unrelated.ToString("N"))
                    : Chunk(marker: pair.Item1, opaqueText: pair.Item2), false);
                Check(!fixture.Absent(query, out var missing) && missing == null && fixture.Opens == 2,
                    "A late target/malformed marker from either namespace was overlooked");
            }
            foreach (int key in new[] { 2, 4 })
            {
                byte[] bad = Chunk(marker: unrelated.ToString("N"), opaqueText: unrelated.ToString("N"), markerWrongType: true);
                // First int-map entry after the fixed header has the tracked key.
                Array.Copy(BitConverter.GetBytes(key), 0, bad, 25, 4);
                fixture = new Fixture { Chunk = bad };
                Check(!fixture.Absent(query, out var missing) && missing == null, "Wrong-typed composite marker accepted");
            }
            foreach (Action invalid in new Action[] {
                () => new NativeWorldStorageReadback.AbsenceMarkerSet(0, new[] { Operation }),
                () => new NativeWorldStorageReadback.AbsenceMarkerSet(2, Array.Empty<Guid>()),
                () => new NativeWorldStorageReadback.AbsenceMarkerSet(2, new[] { Guid.Empty }),
                () => new NativeWorldStorageReadback.AbsenceMarkerSet(2, new[] { Operation, Operation }),
                () => new NativeWorldStorageReadback.AbsenceMarkerSet(2, Enumerable.Repeat(Operation, 65537)),
                () => new NativeWorldStorageReadback.AbsenceExpectation(71, first, first),
                () => new NativeWorldStorageReadback.AbsenceExpectation(71, first, new NativeWorldStorageReadback.AbsenceMarkerSet(4, new[] { Operation })),
                () => new NativeWorldStorageReadback.AbsenceExpectation(0, first, second),
                () => new NativeWorldStorageReadback.AbsenceExpectation(71, null, second) })
            {
                bool rejected = false; try { invalid(); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Invalid composite marker context accepted");
            }
            var payout = Enumerable.Range(1, 65536).Select(i => new Guid(i, 1, 1, new byte[8])).ToArray();
            var full = new NativeWorldStorageReadback.AbsenceMarkerSet(4, payout);
            query = new NativeWorldStorageReadback.AbsenceExpectation(71, first, full);
            payout[0] = Operation;
            Check(query.MarkerSets[1].Operations.Count == 65536 && query.MarkerSets[1].Operations[0] != Operation,
                "Legal payout budget rejected or aliased caller");
            bool overflow = false;
            try { new NativeWorldStorageReadback.AbsenceExpectation(71, full, first); } catch (ArgumentException) { overflow = true; }
            Check(overflow, "Construction set exceeded its preserved part budget");
        }
        private sealed class Fixture
        {
            public byte[] Meta = Metadata(71), Map = Mapping(1), Ok = BitConverter.GetBytes(41), Chunk = WorldStorageReadbackChecks.Chunk();
            internal bool Missing;
            internal int Opens;
            internal Func<string, Stream> AbsenceChunk;
            internal bool Absent(NativeWorldStorageReadback.AbsenceExpectation query, out NativeWorldStorageReadback.AbsenceEvidence evidence,
                CancellationToken cancellation = default)
            {
                Opens = 0;
                using (var meta = new MemoryStream(Meta, false))
                using (var map = new MemoryStream(Map, false))
                using (var ok = new MemoryStream(Ok, false))
                    return NativeWorldStorageReadback.TryVerifyAbsence(meta, map, ok, name => {
                        ++Opens; if (Missing) throw new FileNotFoundException();
                        return AbsenceChunk?.Invoke(name) ?? new MemoryStream(Chunk, false);
                    }, query, out evidence, out _, cancellation);
            }
            internal bool Verify(out NativeWorldStorageReadback.Evidence evidence, NativeWorldStorageReadback.Expectation expectation = null,
                CancellationToken cancellation = default)
            {
                using (var meta = new MemoryStream(Meta, false))
                using (var map = new MemoryStream(Map, false))
                using (var ok = new MemoryStream(Ok, false))
                    return NativeWorldStorageReadback.TryVerify(meta, map, ok, name =>
                    {
                        Check(!name.Contains("..") && !Path.IsPathRooted(name), "Untrusted chunk filename escaped root");
                        if (Missing) throw new FileNotFoundException();
                        return new MemoryStream(Chunk, false);
                    }, expectation ?? Expect(Inventory), out evidence, out _, cancellation);
            }
        }
        private static NativeWorldStorageReadback.Expectation Expect(byte[] inventory) => new NativeWorldStorageReadback.Expectation(71, Operation, 43, 100, 1, 2, 3, inventory);
        private static byte[] Metadata(long world)
        {
            byte[] payload = Bytes(w =>
            {
                w.Write(41); w.Write("QA"); w.Write("seed"); w.Write(10); w.Write(world); w.Write(1); w.Write(true);
                w.Write(1); w.Write("key"); w.Write(1); for (int n = 0; n < 4; ++n) w.Write("player" + n);
            });
            return Bytes(w => { w.Write(payload.Length); w.Write(payload); });
        }
        private static byte[] Mapping(int total, int chunks = 1, int objects = 1) => Bytes(w =>
        {
            w.Write((short)41); w.Write(total); w.Write(chunks);
            for (int n = 0; n < chunks; ++n) { w.Write((ushort)(258 + n)); w.Write((byte)3); w.Write(1u); w.Write(objects); }
        });
        private static byte[] Chunk(string marker = null, long creator = 100, int prefab = 43, byte[] inventory = null,
            int flags = 256 | 32 | 64 | 128, int count = 1, bool duplicateKey = false, bool nonfinite = false, bool largeMap = false, string opaqueText = null, bool markerWrongType = false) => Bytes(w =>
        {
            w.Write((short)41); w.Write(count);
            for (int item = 0; item < count; ++item)
            {
                int bits = flags | (nonfinite || largeMap ? 2 : 0) | (markerWrongType ? 16 : 0);
                w.Write((ushort)bits);
                if ((bits & 8192) != 0) { w.Write((short)0); w.Write((short)0); }
                else { w.Write(0f); w.Write(1f); w.Write(0f); }
                w.Write(prefab); if ((bits & 4096) != 0) { w.Write((ushort)0x0001); w.Write((ushort)0x0002); }
                if ((bits & 1) != 0) { w.Write((byte)1); w.Write(10); }
                for (int map = 0; map < 7; ++map)
                {
                    if ((bits & (2 << map)) == 0) continue;
                    int entries = map == 0 && largeMap ? 128 : 1;
                    if (map == 4 && duplicateKey) entries = 2;
                    if (map == 5 && opaqueText != null) entries = 2;
                    if (entries < 128) w.Write((byte)entries); else { w.Write((byte)128); w.Write((byte)128); }
                    for (int n = 0; n < entries; ++n)
                    {
                        w.Write(map == 4 ? 1 : map == 5 ? (n == 0 ? 2 : 4) : map == 6 ? 3 : map == 3 && markerWrongType ? 2 : 1000 + n);
                        switch (map)
                        {
                            case 0: w.Write(nonfinite ? float.NaN : 1f); break;
                            case 1: for (int k = 0; k < 3; ++k) w.Write(1f); break;
                            case 2: for (int k = 0; k < 4; ++k) w.Write(1f); break;
                            case 3: w.Write(10); break;
                            case 4: w.Write(creator); break;
                            case 5: w.Write(n == 0 ? marker ?? Operation.ToString("N") : opaqueText); break;
                            case 6: byte[] data = inventory ?? Inventory; w.Write(data.Length); w.Write(data); break;
                        }
                    }
                }
            }
        });
        private static byte[] Bytes(Action<BinaryWriter> write)
        {
            using (var memory = new MemoryStream())
            { using (var writer = new BinaryWriter(memory, Encoding.UTF8, true)) write(writer); return memory.ToArray(); }
        }
        private static byte[] AppendTarget(byte[] prefix, int count)
        {
            byte[] combined = prefix.Concat(Chunk().Skip(6)).ToArray();
            Array.Copy(BitConverter.GetBytes(count), 0, combined, 2, 4); return combined;
        }
        private sealed class Oversized : MemoryStream
        {
            public override long Length => NativeWorldStorageReadback.MaximumChunkBytes + 1L;
            public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Budget was not checked before reading");
        }
        private sealed class CancelOnDispose : MemoryStream
        {
            private readonly CancellationTokenSource cancellation;
            internal CancelOnDispose(byte[] bytes, CancellationTokenSource cancellation) : base(bytes, false) { this.cancellation = cancellation; }
            protected override void Dispose(bool disposing) { if (disposing) cancellation.Cancel(); base.Dispose(disposing); }
        }
        private static int Find(byte[] bytes, byte[] sequence)
        {
            for (int start = 0; start <= bytes.Length - sequence.Length; ++start)
                if (sequence.SequenceEqual(bytes.Skip(start).Take(sequence.Length))) return start;
            throw new InvalidOperationException("Marker fixture missing");
        }
        private static void Check(bool value, string error)
        { ++assertions; if (!value) throw new InvalidOperationException(error); }
    }
}
