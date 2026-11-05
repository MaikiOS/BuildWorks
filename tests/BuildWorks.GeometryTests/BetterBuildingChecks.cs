using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OstrixMods.BuildWorks.Geometry;
using Bbp = OstrixMods.BuildWorks.Geometry.BetterBuildingBlueprint;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class BetterBuildingChecks
    {
        private static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        private static void Reject(Action action, string reason)
        {
            try { action(); } catch (FormatException) { return; }
            throw new Exception("Accepted " + reason);
        }
        internal static void Run()
        {
            byte[] raw = Minimal(), file = Pack(raw);
            var model = Bbp.ParseBinary(file);
            Check(model.PayloadVersion == 1 && model.Name == "one" && model.Pieces.Count == 1 && model.Pieces[0].Prefab == "wood_floor", "Minimal independent wire fixture");
            for (int i = 0; i < raw.Length; ++i) { int length=i; Reject(()=>Bbp.ParseBinary(Pack(raw.Take(length).ToArray())),"truncated raw " + i); }
            for (int i = 0; i < file.Length; ++i) { int length=i; Reject(()=>Bbp.ParseBinary(file.Take(length).ToArray()),"truncated file " + i); }
            byte[] changed=(byte[])file.Clone(); changed[16]^=1; Reject(()=>Bbp.ParseBinary(changed),"checksum");
            void BadHeader(int offset,int value)
            { var bytes=(byte[])file.Clone(); Array.Copy(BitConverter.GetBytes(value),0,bytes,offset,4); Reject(()=>Bbp.ParseBinary(bytes),"header " + offset); }
            BadHeader(0,0); BadHeader(4,2); BadHeader(8,0); BadHeader(8,Bbp.MaximumRawBytes+1); BadHeader(12,0); BadHeader(12,int.MaxValue);
            Reject(()=>Bbp.ParseBinary(Pack(raw.Concat(new byte[]{0}).ToArray())),"trailing raw");
            void BadRaw(int offset,int value)
            { var bytes=(byte[])raw.Clone(); Array.Copy(BitConverter.GetBytes(value),0,bytes,offset,4); Reject(()=>Bbp.ParseBinary(Pack(bytes)),"raw " + offset); }
            BadRaw(0,5); BadRaw(8,int.MaxValue); BadRaw(23,50001); BadRaw(27,-1);
            BadRaw(31,0x7fc00000); BadRaw(43,0x7f800000); BadRaw(55,0); // xyz, quaternion finite/norm
            var flag=(byte[])raw.Clone(); flag[59]=2; Reject(()=>Bbp.ParseBinary(Pack(flag)),"terrain boolean");
            var utf=(byte[])raw.Clone(); utf[5]=0xff; Reject(()=>Bbp.ParseBinary(Pack(utf)),"strict UTF8");
            var prefix=(byte[])raw.Clone(); prefix[4]=0x80; prefix[5]=0x80; prefix[6]=0x80; Reject(()=>Bbp.ParseBinary(Pack(prefix)),"four-byte string prefix");
            var row=new Bbp.Piece(0,"sign",new Point3(1,2,3),new Rotation3(0,0,0,1),"",new Bbp.Attachment("SwordIron",255,63),new Point3(.1,10,1));
            for (int v=1;v<4;++v) { int version=v; Reject(()=>Bbp.WriteBinary(version,"version",new[]{row}),"version discards fields"); }
            var terrainHeights=new[]{-0f,.25f,-.5f,1f}; terrainHeights[0]=BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
            var terrain=new Bbp.TerrainProfile(1,2,.5f,1,-1,-1,terrainHeights);
            terrainHeights[0]=7;
            byte[] all=Bbp.WriteBinary(4,"all 😀",new[]{row},terrain);
            byte[] duplicateRaw=Expand(Bbp.WriteBinary(4,"duplicates",new[]{row,row}));
            foreach(int offset in SecondOptionalIndices(duplicateRaw))
            {
                byte[] duplicate=(byte[])duplicateRaw.Clone();Array.Copy(BitConverter.GetBytes(0),0,duplicate,offset,4);
                Reject(()=>Bbp.ParseBinary(Pack(duplicate)),"duplicate optional table index");
            }
            var complete=Bbp.ParseBinary(all);
            Check(complete.Pieces[0].SignText=="" && complete.Pieces[0].DisplayItem.Variant==255 && complete.Pieces[0].Scale.Value.Y==10 &&
                BitConverter.SingleToInt32Bits(complete.Terrain.Heights[0])==unchecked((int)0x80000000),"All fields/defensive terrain copy/signed zero");
            byte[] kept=complete.BinaryBytes(); all[0]=0; kept[0]=0;
            Check(complete.BinaryBytes()[0]==0x42 && Bbp.ParseBinaryText(complete.Text).Name==complete.Name,"Input/output byte ownership");
            var source=new BlueprintSourceDocument(Bbp.BinaryFormat,"sample.bbp",complete.Text);
            Check(source.BetterBuilding!=null && source.ByteCount==complete.BinaryByteCount && source.OriginalBytes().SequenceEqual(complete.BinaryBytes()) &&
                source.HasPartAddress(0,1,"sign") && !source.HasPartAddress(1,1,"sign") && !source.HasPartAddress(0,2,"sign") && source.HasUnsupportedSections(true),"BBP source/archive/address/retained sections");
            var plainSource=new BlueprintSourceDocument(Bbp.BinaryFormat,"plain.bbp",model.Text);
            Check(plainSource.HasUnsupportedSections(false) && !plainSource.HasUnsupportedSections(true),"BBP frame application");
            Reject(()=>Bbp.ParseBinaryText(complete.Text+"\n"),"noncanonical Base64");
            Reject(()=>new Bbp.Piece(0,"bad/name",new Point3(),new Rotation3(0,0,0,1)),"prefab path");
            Reject(()=>new Bbp.Piece(0,"sign",new Point3(),new Rotation3(0,0,0,1),"bad\0text"),"sign NUL");
            Reject(()=>new Bbp.Attachment("item",0,64),"orientation range");
            Reject(()=>new Bbp.Piece(0,"wood_floor",new Point3(),new Rotation3(.002,0,0,1.0024948877675137)),"quaternion valid as double but invalid on float wire");
            Reject(()=>new Bbp.TerrainProfile(2,2,1,1,0,0,new float[4]),"terrain v2 geometry");
            Reject(()=>new Bbp.TerrainProfile(1,449,1,1,0,0,new float[449*449]),"terrain v1 width");
            // The declared cap and the streaming cap both reject compression bombs before allocating their declared content.
            byte[] bomb=new byte[Bbp.MaximumRawBytes+1];
            Reject(()=>Bbp.ParseBinary(Pack(bomb)),"expanded header limit");
            Reject(()=>Bbp.ParseBinary(Pack(bomb,Bbp.MaximumRawBytes)),"actual expanded limit");
            var large=new Bbp.Piece(0,"sign",new Point3(),new Rotation3(0,0,0,1),new string('x',4096));
            Reject(()=>Bbp.WriteBinary(3,"too large",Enumerable.Repeat(large,2050)),"writer raw limit");
            var compressedSource=Bbp.ParseBinary(Bbp.WriteBinary(3,"aggregate",Enumerable.Repeat(large,2000)));
            var originals=Enumerable.Range(0,4).Select(i=>new BlueprintSourceDocument(Bbp.BinaryFormat,"aggregate"+i+".bbp",compressedSource.Text)).ToArray();
            Check(originals.Sum(p=>p.ByteCount)<BlueprintSourceDocument.MaximumTotalBytes,"Fixture compressed bytes must fit");
            BlueprintSourceDocument.Index(originals.Take(3));
            try { BlueprintSourceDocument.Index(originals); throw new Exception("Expanded aggregate budget bypassed"); }
            catch(ArgumentException) { }
            Type bounded=typeof(Bbp).GetNestedType("BoundedOutput",BindingFlags.NonPublic);
            using(var stream=(Stream)Activator.CreateInstance(bounded,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{4},null))
            { Reject(()=>stream.Write(new ReadOnlySpan<byte>(new byte[5])),"span cap"); Check(stream.Length==0,"Rejected span must not write"); }
        }

        internal static int Corpus(string directory,string output)
        {
            if(Directory.Exists(output))throw new IOException("Refusing to replace previous BBP exports.");
            string[] files=Directory.GetFiles(directory,"*.bbp"); Check(files.Length==5,"Expected five pinned original-writer samples");
            Directory.CreateDirectory(output);
            foreach(string path in files)
            {
                var model=Bbp.ParseBinary(File.ReadAllBytes(path));
                using(var dto=JsonDocument.Parse(File.ReadAllBytes(Path.ChangeExtension(path,"dto.json")))) Compare(model,dto.RootElement);
                byte[] bytes=Bbp.WriteBinary(model.PayloadVersion,model.Name,model.Pieces,model.Terrain);
                var read=Bbp.ParseBinary(bytes);
                using(var dto=JsonDocument.Parse(File.ReadAllBytes(Path.ChangeExtension(path,"dto.json")))) Compare(read,dto.RootElement);
                Check(model.BinaryBytes().SequenceEqual(File.ReadAllBytes(path)),"Original bytes changed");
                File.WriteAllBytes(Path.Combine(output,Path.GetFileName(path)),bytes);
                Console.WriteLine("PASS BBP original sample "+Path.GetFileName(path)+" v"+model.PayloadVersion+" parts="+model.Pieces.Count+" raw="+model.RawByteCount+" file="+model.BinaryByteCount);
            }
            return 0;
        }
        internal static void Compare(Bbp model,JsonElement root)
        {
            Check(model.Name==root.GetProperty("name").GetString() && model.PayloadVersion==root.GetProperty("formatVersion").GetInt32(),"Root fields");
            JsonElement[] rows=root.GetProperty("pieces").EnumerateArray().ToArray(); Check(rows.Length==model.Pieces.Count,"Piece count");
            void Float(double value,JsonElement json) => Check(BitConverter.SingleToInt32Bits((float)value)==BitConverter.SingleToInt32Bits(json.GetSingle()),"Float bits");
            void Vector(Point3 value,JsonElement json) { Float(value.X,json.GetProperty("x"));Float(value.Y,json.GetProperty("y"));Float(value.Z,json.GetProperty("z")); }
            for(int i=0;i<rows.Length;++i)
            {
                var p=model.Pieces[i];var row=rows[i];Check(p.Record==i+1 && p.Prefab==row.GetProperty("prefab").GetString(),"Prefab/record"); Vector(p.Position,row);
                Float(p.Rotation.X,row.GetProperty("qx"));Float(p.Rotation.Y,row.GetProperty("qy"));Float(p.Rotation.Z,row.GetProperty("qz"));Float(p.Rotation.W,row.GetProperty("qw"));
                Check(p.SignText==row.GetProperty("signText").GetString(),"Sign null/empty/content");
                var item=row.GetProperty("attachment"); Check((p.DisplayItem==null)==(item.ValueKind==JsonValueKind.Null),"Attachment presence");
                if(p.DisplayItem!=null)Check(p.DisplayItem.Prefab==item.GetProperty("prefab").GetString() && p.DisplayItem.Variant==item.GetProperty("variant").GetInt32() && p.DisplayItem.Orientation==item.GetProperty("orientation").GetInt32(),"Attachment fields");
                var scale=row.GetProperty("scale"); Check(p.Scale.HasValue!=(scale.ValueKind==JsonValueKind.Null),"Scale presence"); if(p.Scale.HasValue)Vector(p.Scale.Value,scale);
            }
            var terrain=root.GetProperty("terrain");Check((model.Terrain==null)==(terrain.ValueKind==JsonValueKind.Null),"Terrain presence");
            if(model.Terrain!=null)
            {
                var t=model.Terrain;Check(t.Version==terrain.GetProperty("version").GetInt32() && t.Width==terrain.GetProperty("width").GetInt32(),"Terrain version/width");
                Float(t.Spacing,terrain.GetProperty("spacing"));Float(t.Radius,terrain.GetProperty("radius"));Float(t.OriginX,terrain.GetProperty("originX"));Float(t.OriginZ,terrain.GetProperty("originZ"));
                var heights=terrain.GetProperty("heights").EnumerateArray().ToArray();Check(heights.Length==t.Heights.Count,"Terrain height count");for(int i=0;i<heights.Length;++i)Float(t.Heights[i],heights[i]);
            }
        }
        private static byte[] Minimal()
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true),true))
            {
                writer.Write(1);writer.Write("one");writer.Write(1);writer.Write("wood_floor");writer.Write(1);writer.Write(0);
                for(int i=0;i<6;++i)writer.Write(0f);writer.Write(1f);writer.Write(false);writer.Flush();return stream.ToArray();
            }
        }
        private static byte[] Pack(byte[] raw,int? declared=null)
        {
            byte[] packed;using(var buffer=new MemoryStream()) {using(var deflate=new DeflateStream(buffer,CompressionLevel.Optimal,true))deflate.Write(raw,0,raw.Length);packed=buffer.ToArray();}
            using(var output=new MemoryStream())using(var writer=new BinaryWriter(output))
            { writer.Write(1179664962);writer.Write(1);writer.Write(declared??raw.Length);writer.Write(packed.Length);writer.Write(SHA256.HashData(packed));writer.Write(packed);return output.ToArray(); }
        }
        private static byte[] Expand(byte[] file)
        {
            using(var input=new MemoryStream(file,48,file.Length-48,false))using(var deflate=new DeflateStream(input,CompressionMode.Decompress))using(var raw=new MemoryStream())
            { deflate.CopyTo(raw);return raw.ToArray(); }
        }
        private static int[] SecondOptionalIndices(byte[] raw)
        {
            using(var input=new MemoryStream(raw))using(var reader=new BinaryReader(input))
            {
                Check(reader.ReadInt32()==4,"Duplicate fixture version");reader.ReadString();int prefabs=reader.ReadInt32();for(int i=0;i<prefabs;++i)reader.ReadString();
                int count=reader.ReadInt32();input.Position+=count*32;Check(!reader.ReadBoolean(),"Duplicate fixture terrain");
                var offsets=new int[3];
                for(int section=0;section<3;++section)
                {
                    Check(reader.ReadInt32()==2,"Duplicate fixture table count");
                    for(int i=0;i<2;++i)
                    {
                        if(i==1)offsets[section]=(int)input.Position;reader.ReadInt32();
                        if(section==0){reader.ReadString();reader.ReadInt32();reader.ReadInt32();}
                        else if(section==1)reader.ReadString();else input.Position+=12;
                    }
                }
                return offsets;
            }
        }
    }
}
