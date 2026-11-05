using System;
using System.IO;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class UnboundSemanticChecks
    {
        internal static void Run()
        {
            var basis = new Point3(2, 3, 4);
            UnboundImportContext Context(string name = "wood_floor", bool sync = true, bool ordinary = true, bool sign = false,
                bool material = false, (int Width, int Height)? grid = null) => new UnboundImportContext(
                    new BlueprintPrefabMetadata(1, name, name, "Piece", "synthetic-host", basis), sync, ordinary, sign, material, grid);
            var context = Context();
            var plain = Piece();
            Check(UnboundStateProjection.TryProject(plain, context, null, out var simple), "Plain native state");
            Scale(simple.RootScale, basis);
            Check(UnboundStateProjection.Matches(plain, context.Metadata, basis, false, null, null, null), "Plain projection validation");
            Check(!UnboundStateProjection.Matches(plain, null, basis, false, null, null, null) &&
                !UnboundStateProjection.Matches(plain, context.Metadata, null, false, null, null, null) &&
                !UnboundStateProjection.Matches(plain, context.Metadata, new Point3(1, 1, 1), false, null, null, null) &&
                !UnboundStateProjection.Matches(plain, context.Metadata, basis, true, null, null, null), "Missing or invented basis accepted");
            foreach (float scalar in new[] { 2f, 1.25f })
            {
                var source = Piece(flags: 258, body: w => { One(w, -1726348180); w.Write(scalar); });
                Check(UnboundStateProjection.TryProject(source, context, null, out var projected), "Scalar projection");
                Scale(projected.RootScale, scalar == 2 ? basis : new Point3(scalar, scalar, scalar));
                Check(!UnboundStateProjection.TryProject(source, Context(sync: false), null, out _), "Sync-disabled scale interpreted");
            }
            foreach (float value in new[] { 1e-6f, -1e-6f })
            {
                var source = Scaled(new Point3(value, value, value), 5f);
                Check(UnboundStateProjection.TryProject(source, context, null, out var projected), "Tiny vector fallback");
                Scale(projected.RootScale, new Point3(5, 5, 5));
            }
            // Native strict Single boundary: y/z contributions round away at this magnitude.
            float edge = 1e-5f, below = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(edge) - 1);
            foreach (float x in new[] { edge, below })
            {
                var vector = new Point3(x, 1e-9f, 1e-9f);
                Check(UnboundStateProjection.TryProject(Scaled(vector, 5), context, null, out var projected), "Boundary vector");
                Scale(projected.RootScale, x == edge ? vector : new Point3(5, 5, 5));
            }
            Check(UnboundStateProjection.TryProject(Scaled(new Point3(.5, 2, 3), 8), context, null, out var vectorResult), "Vector precedence");
            Scale(vectorResult.RootScale, new Point3(.5, 2, 3));
            Check(!UnboundStateProjection.TryProject(Scaled(new Point3(-2, 3, 4), 8), context, null, out _), "Negative effective scale accepted");
            var textSeed = Piece("sign", 1084866395, 336, w => {
                One(w, -1049702188); w.Write(17);
                w.Write((byte)2); w.Write(-871206003); w.Write("original"); w.Write(-734892015); w.Write("old author"); });
            var signContext = Context("sign", sign: true, material: true);
            Check(UnboundStateProjection.TryProject(textSeed, signContext, null, out var signProjection) &&
                signProjection.SignText == "original" && signProjection.MaterialSeed == 17 && signProjection.Contents == null, "Known text/seed projection");
            Check(!UnboundStateProjection.TryProject(textSeed, Context("sign", sign: false, material: true), null, out _) &&
                !UnboundStateProjection.TryProject(textSeed, Context("sign", sign: true, material: false), null, out _), "Missing capability accepted");
            Check(UnboundStateProjection.Matches(textSeed, signContext.Metadata, basis, false, "edited", 18, null) &&
                UnboundStateProjection.Matches(textSeed, signContext.Metadata, basis, false, "", 0, null) &&
                !UnboundStateProjection.Matches(textSeed, signContext.Metadata, basis, false, null, 17, null) &&
                !UnboundStateProjection.Matches(textSeed, signContext.Metadata, basis, false, "text", null, null), "Edited known fields vs dropped fields");
            foreach (var unknown in new[] {
                Piece(flags:258, body:w=>{One(w,12345);w.Write(1f);}),
                Piece(flags:272, body:w=>{One(w,-871206003);w.Write(1);}),
                Piece(flags:257, body:w=>{w.Write((byte)1);w.Write(123L);w.Write(1U);}),
                Piece(flags:272, body:w=>{One(w,-1969502066);w.Write(2);}) })
                Check(!UnboundStateProjection.TryProject(unknown, context, null, out _) &&
                    !UnboundStateProjection.Matches(unknown, context.Metadata, basis, false, "edited", 1, null), "Unknown/wrong-type/connection bypass");
            var bypass = Piece(flags:272, body:w=>{One(w,-1969502066);w.Write(1);});
            Check(UnboundStateProjection.TryProject(bypass, context, null, out var archived) && archived.Contents == null,
                "Archive-only no-support flag not classified");
            byte[] emptyInventory = { 110, 0, 0, 0, 0, 0 };
            var storage = Piece(flags:384, body:w=>{One(w,-938864442);w.Write(emptyInventory.Length);w.Write(emptyInventory);});
            var storageContext = Context(ordinary:false, grid:(5,2));
            Check(UnboundStateProjection.TryProject(storage, storageContext, null, out var contents) &&
                contents.Contents.Info == "5:2:0:0" && contents.Contents.Zdo == Convert.ToBase64String(emptyInventory) &&
                UnboundStateProjection.Matches(storage, storageContext.Metadata, basis, false, null, null, contents.Contents), "Exact container demand");
            Check(!UnboundStateProjection.TryProject(storage, context, null, out _) &&
                !UnboundStateProjection.TryProject(plain, storageContext, null, out _) &&
                !UnboundStateProjection.Matches(storage, storageContext.Metadata, basis, false, null, null, null), "Container invented empty or dropped contents");
            Console.WriteLine("PASS Unbound known-state geometry: exact source projection/basis/Single scale gates and boundary/text/seed/typed unknown/refusal/contents;synthetic only,no game or holder restoration.");
        }
        private static UnboundBlueprint.Piece Scaled(Point3 vector, float scalar) => Piece(flags:262, body:w=>{
            One(w,-1726348180);w.Write(scalar);One(w,-528916476);w.Write((float)vector.X);w.Write((float)vector.Y);w.Write((float)vector.Z); });
        private static UnboundBlueprint.Piece Piece(string name = "wood_floor", int hash = -1045925142, ushort flags = 256, Action<BinaryWriter> body = null)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, new UTF8Encoding(false,true), true)) { writer.Write(flags);writer.Write(hash);body?.Invoke(writer); }
                return UnboundBlueprint.Parse("vtbp 2\n" + name + "|0|0|0|0|0|0|1|" + Convert.ToBase64String(stream.ToArray())).Pieces[0];
            }
        }
        private static void One(BinaryWriter writer, int key) { writer.Write((byte)1); writer.Write(key); }
        private static void Scale(Point3 actual, Point3 expected) => Check(actual.X == expected.X && actual.Y == expected.Y && actual.Z == expected.Z, "Scale values differ");
        private static void Check(bool ok,string why) { if(!ok)throw new InvalidOperationException(why); }
    }
}
