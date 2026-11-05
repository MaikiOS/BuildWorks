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
            UnboundItemStandChecks.Run();
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
            ExportChecks();
            Console.WriteLine("PASS Unbound known-state geometry: exact source projection/basis/Single scale gates and boundary/text/seed/typed unknown/refusal/contents;synthetic only,no game or holder restoration.");
        }
        private static void ExportChecks()
        {
            var basis = new Point3(2, 3, 4);
            int Hash(string name) => name == "wood_floor" ? -1045925142 : name == "sign" ? 1084866395 :
                name == "Wood" ? -151837501 : name == "AxeIron" ? 1790496580 : throw new ArgumentException("Unknown fixture prefab");
            UnboundImportContext Context(string name = "wood_floor", bool sync = true, bool ordinary = true,
                bool sign = false, bool material = false, (int Width, int Height)? grid = null, bool persistent = true) =>
                new UnboundImportContext(new BlueprintPrefabMetadata(1, name, name, "Piece", "synthetic", basis),
                    sync, ordinary, sign, material, grid, (Hash(name), persistent, false, 0));
            string Encode(UnboundBlueprint.Piece source, UnboundImportContext context, Point3 root,
                string text = null, int? seed = null, BlueprintForeignPartData data = null)
            {
                Check(UnboundStateProjection.TryEncode(source, context, root, text, seed, data, out var packet), "Safe state encode refused");
                return packet;
            }
            BlueprintForeignZdo Read(string packet) { Check(BlueprintForeignZdo.TryReadNative(packet, Hash("wood_floor"), out var state), "Output parse"); return state; }
            var context = Context();
            Check(Convert.FromBase64String(Encode(null, context, basis)).Length == 6, "Source-free defaults use actual header");
            foreach (ushort flags in new ushort[] { 2304, 0, 256, 512, 768, 1024, 1280, 1536,
                1792, 2048, 2560, 2816, 3072, 3328, 3584, 3840 })
            {
                var archived = Piece(flags: flags);
                var output = Read(Encode(archived, context, basis));
                Check(output.Persistent == true && output.Distant == false && output.NativeObjectType == 0,
                    "Archived instance header was not replaced with current prefab facts");
                Check(archived.State.Persistent == ((flags & 256) != 0) && archived.State.Distant == ((flags & 512) != 0) &&
                    archived.State.NativeObjectType == ((flags >> 10) & 3), "Source header changed during export");
            }
            var header = Convert.FromBase64String(BlueprintForeignZdo.CreateNative(0x10203040, true, true, 3).WriteNative());
            Check(Convert.ToBase64String(header) == Convert.ToBase64String(new byte[] {0,15,64,48,32,16}), "Independent native header golden bytes");
            var noSupport = Piece(flags:272, body:w=>{One(w,-1969502066);w.Write(1);});
            Check(!Read(Encode(noSupport, context, basis)).HasHash(-1969502066) && noSupport.State.HasHash(-1969502066),
                "Authority retained in source only, never reusable output");
            Scale(Read(Encode(null, context, new Point3(2,2,2))).TryGet<Point3>(-528916476, out var vector) ? vector : default,
                new Point3(2,2,2));
            float tiny = 1e-6f;
            Check(Read(Encode(null, context, new Point3(tiny,tiny,tiny))).TryGet<float>(-1726348180, out var scalar) && scalar == tiny,
                "Tiny uniform native scalar fallback");
            var signContext = Context("sign", sign:true, material:true);
            string signPacket = Encode(null, signContext, basis, "edited", 18);
            Check(BlueprintForeignZdo.TryReadNative(signPacket, Hash("sign"), out var signState) &&
                signState.TryGet<string>(-871206003, out var text) && text == "edited" &&
                signState.TryGet<int>(-1049702188, out var seed) && seed == 18 && !signState.HasHash(-734892015), "Working text/seed without old author");
            byte[] inventory = ContainerInventoryTests.Packet(firstHash:1790496580, secondHash:-151837501);
            string Resolve(int key) => key == Hash("Wood") ? "Wood" : key == Hash("AxeIron") ? "AxeIron" : null;
            Check(BlueprintContainerInventory.TryConvertNative(Convert.ToBase64String(inventory),5,2,Resolve,out var contents), "Nonempty inventory fixture");
            var storage = Piece(flags:384, body:w=>{One(w,-938864442);w.Write(inventory.Length);w.Write(inventory);});
            var storageContext = Context(ordinary:false,grid:(5,2));
            var kept = Read(Encode(storage,storageContext,basis,data:contents));
            Check(kept.TryGet<byte[]>(-938864442,out var retained) && Convert.ToBase64String(retained)==contents.Zdo, "Retained contents exact");
            var cleared = contents.WithoutContents(Hash);
            var empty = Read(Encode(storage,storageContext,basis,data:cleared));
            Check(empty.TryGet<byte[]>(-938864442,out var emptied) && Convert.ToBase64String(emptied)=="bgAAAAAA" &&
                contents.Zdo==cleared.Zdo && storage.State.TryGet<byte[]>(-938864442,out var original) &&
                Convert.ToBase64String(original)==contents.Zdo, "CRITICAL: cleared contents emit v110 empty instead of archived nonempty bytes");
            Check(Read(Encode(null,storageContext,basis)).TryGet<byte[]>(-938864442,out var freshEmpty) && freshEmpty.Length==6,
                "New container explicit empty package");
            void Refuse(UnboundBlueprint.Piece source,UnboundImportContext ctx,Point3 root,string label,
                string value=null,int? materialSeed=null,BlueprintForeignPartData data=null)
            { Check(!UnboundStateProjection.TryEncode(source,ctx,root,value,materialSeed,data,out var packet) && packet==null,label); }
            Refuse(null,new UnboundImportContext(context.Metadata,true,true,false,false),basis,"Missing fresh header refused");
            Check(Read(Encode(Piece(), Context(persistent:false), basis)).Persistent == false,
                "Export did not use the trusted current persistence flag");
            Refuse(Piece(flags:257,body:w=>{w.Write((byte)1);w.Write(123L);w.Write(1U);}),context,basis,"References blocked");
            Refuse(Piece(flags:272,body:w=>{One(w,123);w.Write(1);}),context,basis,"Unknown state blocked");
            Refuse(null,Context(ordinary:false),basis,"Holder/custom profile blocked");
            Refuse(null,Context(sync:false),new Point3(1,1,1),"Scale without sync refused");
            Refuse(null,context,new Point3(tiny,2*tiny,3*tiny),"Unrepresentable tiny anisotropy refused");
            Refuse(null,context,new Point3(.1,.1,.1),"Single rounding loss refused");
            Refuse(null,context,basis,"Unsupported text",value:"text");
            Refuse(null,context,basis,"Unsupported seed",materialSeed:1);
            Refuse(storage,storageContext,basis,"Missing working inventory refused");
            Refuse(storage,Context(ordinary:false,grid:(4,2)),basis,"Wrong grid refused",data:cleared);
            Refuse(null,context,basis,"Unexpected contents refused",data:contents);
            bool badHeader=false;try { new UnboundImportContext(context.Metadata,true,true,false,false,nativeHeader:(1,true,false,0)); }
            catch(ArgumentException){badHeader=true;} Check(badHeader,"Header prefab identity");
            Console.WriteLine("PASS Unbound safe state encoding: fresh header/golden bytes/scale/native Single gates/text/seed/nonempty retain/cleared empty/authority stripped/source immutable; unknown/holder/reference/header/capability/grid refusals. Pure synthetic data, no file/UI/native execution.");
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
