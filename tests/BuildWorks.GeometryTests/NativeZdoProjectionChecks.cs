using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class NativeZdoProjectionChecks
    {
        private const int Prefab = 0x10203040;
        internal static void Run()
        {
            // Literal native prefix: ushort Persistent, int prefab. No self-generated writer roundtrip.
            byte[] minimal = { 0, 1, 0x40, 0x30, 0x20, 0x10 };
            var empty = Read(minimal);
            Require(empty.NativeFlags == 256 && empty.NativePrefabHash == Prefab && empty.Count == 0 &&
                empty.Persistent == true && empty.Distant == false && empty.NativeObjectType == 0 &&
                empty.NativeRotationEuler == null && empty.ConnectionType == null, "Native prefix/defaults");
            byte[] all = Packet(0x1bff, w =>
            {
                w.Write(10f); w.Write(20f); w.Write(30f);
                w.Write((byte)19); w.Write(0x1020304050607080L); w.Write(0xeeddccbbU);
                One(w, 101); w.Write(1.25f);
                One(w, 102); w.Write(1f); w.Write(2f); w.Write(3f);
                One(w, 103); w.Write(.1f); w.Write(.2f); w.Write(.3f); w.Write(.4f);
                One(w, 104); w.Write(-123);
                One(w, 105); w.Write(8589934594L);
                One(w, 106); w.Write("сундук ♞");
                One(w, 107); w.Write(3); w.Write(new byte[] { 1, 2, 3 });
            });
            var data = Read(all);
            Require(data.Count == 7 && data.Persistent == true && data.Distant == true && data.NativeObjectType == 2 &&
                data.NativeRotationEuler.Value.Y == 20 && data.ConnectionType == 19 &&
                data.NativeConnectionUserId == 0x1020304050607080L && data.NativeConnectionId == 0xeeddccbbU, "Header/rotation/connection");
            Require(data.TryGet<float>(101, out var f) && f == 1.25f &&
                data.TryGet<Point3>(102, out var v) && v.Z == 3 &&
                data.TryGet<Rotation3>(103, out var q) && q.W == (double).4f &&
                data.TryGet<int>(104, out var n) && n == -123 &&
                data.TryGet<long>(105, out var l) && l == 8589934594L &&
                data.TryGet<string>(106, out var s) && s == "сундук ♞" &&
                data.TryGet<byte[]>(107, out var b) && b.SequenceEqual(new byte[] { 1, 2, 3 }), "All typed values");
            data.TryGet<byte[]>(107, out var copy); copy[0] = 99;
            Require(data.TryGet<byte[]>(107, out var unchanged) && unchanged[0] == 1 &&
                !data.TryGet<object>(107, out _), "Byte ownership/type boundary");
            foreach (int count in new[] { 1, 127, 128, 255, 256, 293, 32767 })
            {
                var many = Read(Packet(2, w =>
                {
                    if (count < 128) w.Write((byte)count);
                    else { w.Write((byte)(128 | (count >> 8))); w.Write((byte)count); }
                    for (int i = 0; i < count; ++i) { w.Write(i); w.Write((float)i); }
                }));
                Require(many.Count == count && many.TryGet<float>(count - 1, out var last) && last == count - 1, "Count boundary " + count);
            }
            int refused = 0;
            void Reject(byte[] bytes)
            { Require(!BlueprintForeignZdo.TryReadNative(Convert.ToBase64String(bytes), Prefab, out var bad) && bad == null, "Malformed projection accepted"); ++refused; }
            for (int length = 0; length < all.Length; ++length) Reject(all.Take(length).ToArray());
            Reject(minimal.Concat(new byte[] { 0 }).ToArray());
            foreach (ushort flag in new ushort[] { 8192, 16384, 32768 }) Reject(Packet(flag));
            Reject(Packet(2, w => w.Write((byte)0)));
            Reject(Packet(2, w => { w.Write((byte)128); w.Write((byte)1); w.Write(1); w.Write(1f); }));
            Reject(Packet(2, w => { w.Write((byte)2); w.Write(7); w.Write(1f); w.Write(7); w.Write(2f); }));
            Reject(Packet(2, w => { One(w, 1); w.Write(float.NaN); }));
            Reject(Packet(4096, w => { w.Write(float.PositiveInfinity); w.Write(0f); w.Write(0f); }));
            Reject(Packet(64, w => { One(w, 1); w.Write((byte)2); w.Write(new byte[] { 0xc0, 0xaf }); }));
            Reject(Packet(128, w => { One(w, 1); w.Write(-1); }));
            Reject(Packet(128, w => { One(w, 1); w.Write(99); w.Write((byte)0); }));
            Reject(Packet(1, w => { w.Write((byte)0); w.Write(0L); w.Write(0U); }));
            Require(!BlueprintForeignZdo.TryReadNative(Convert.ToBase64String(minimal), Prefab + 1, out _) &&
                !BlueprintForeignZdo.TryReadNative(" " + Convert.ToBase64String(minimal), Prefab, out _) &&
                !BlueprintForeignZdo.TryReadNative("!", Prefab, out _) &&
                !BlueprintForeignZdo.TryReadNative(new string('A', BlueprintForeignPartData.MaximumContainerBase64 + 1), Prefab, out _), "Identity/Base64/budget boundary");
            // Same key in distinct typed maps is legitimate and must not collapse.
            var typed = Read(Packet(18, w => { One(w, 5); w.Write(1f); One(w, 5); w.Write(2); }));
            Require(typed.Count == 2 && typed.TryGet<float>(5, out _) && typed.TryGet<int>(5, out _), "Typed-key collision");
            var emptyBytes = Read(Packet(128, w => { One(w, 7); w.Write(0); }));
            Require(emptyBytes.TryGet<byte[]>(7, out var zero) && zero.Length == 0, "Empty byte value");
            var connectionOnly = Read(Packet(1, w => { w.Write((byte)17); w.Write(0L); w.Write(0U); }));
            Require(connectionOnly.Count == 0 && connectionOnly.ConnectionType == 17 &&
                connectionOnly.NativeConnectionUserId == 0 && connectionOnly.NativeConnectionId == 0,
                "Connection without data maps/None target");
            bool kgRejected = false; try { data.WriteKg(); } catch (InvalidOperationException) { kgRejected = true; }
            Require(kgRejected, "Native projection mislabelled as KG writer input");
            Console.WriteLine("PASS native ZDO.Serialize projection:literal prefix/all7maps/IDs/Euler/count1..32767/immutability;" + refused + " malformed controls rejected. Synthetic wire controls only;no game or producer output proof.");
        }
        private static BlueprintForeignZdo Read(byte[] bytes)
        { Require(BlueprintForeignZdo.TryReadNative(Convert.ToBase64String(bytes), Prefab, out var value), "Valid projection rejected"); return value; }
        private static byte[] Packet(ushort flags, Action<BinaryWriter> body = null)
        { using (var stream = new MemoryStream()) { using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true)) { writer.Write(flags); writer.Write(Prefab); body?.Invoke(writer); } return stream.ToArray(); } }
        private static void One(BinaryWriter writer, int key) { writer.Write((byte)1); writer.Write(key); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
