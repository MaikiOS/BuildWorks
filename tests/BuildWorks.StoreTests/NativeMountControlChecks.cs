using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class NativeMountControlChecks
    {
#pragma warning disable CS0649
        [DataContract] private sealed class Page { [DataMember] public Holder[] profiles; }
        [DataContract] private sealed class Holder {
            [DataMember] public string name,host,kind;
            [DataMember] public bool persistent,distant;
            [DataMember] public byte objectType;
            [DataMember] public int prefabHash;
            [DataMember] public Item[] items;
        }
        [DataContract] private sealed class Item {
            [DataMember] public string name;
            [DataMember] public int variants,maximumQuality;
            [DataMember] public int[] orientations;
        }
#pragma warning restore CS0649
        internal static void Run(string directory,string profiles)
        {
            if(Directory.Exists(directory)||File.Exists(directory))throw new InvalidOperationException("CONTROL requires a new isolated directory.");
            Page page;using(var file=File.OpenRead(profiles))page=(Page)new DataContractJsonSerializer(typeof(Page)).ReadObject(file);
            var native=page.profiles.Single(value=>value.name=="itemstand");var item=native.items.Single(value=>value.name=="SwordIron");
            if(item.variants!=1||item.maximumQuality<1||!item.orientations.Contains(0))throw new InvalidOperationException("Pinned capability does not support CONTROL.");
            byte[] packet;using(var buffer=new MemoryStream())using(var w=new BinaryWriter(buffer,Encoding.UTF8,true)) {
                w.Write((byte)110);w.Write(8123);w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);w.Write((byte)193);
                w.Write(ForeignZdoChecks.Hash("SwordIron"));w.Write((byte)1);w.Write("control.kind");w.Write("SYNTHETIC");w.Write((byte)0);w.Flush();packet=buffer.ToArray();
            }
            var fields=new Dictionary<string,object>{{"item",ForeignZdoChecks.Hash("SwordIron")},{"variant",0},{"quality",1},{"type",0},{"itemData",packet},{"creator",111L}};
            string zdo=BlueprintForeignZdo.CreateNative(native.prefabHash,native.persistent,native.distant,native.objectType).WriteNative(fields.Select(pair=>new KeyValuePair<int,object>(ForeignZdoChecks.Hash(pair.Key),pair.Value)));
            Directory.CreateDirectory(directory);string input=Path.Combine(directory,"CONTROL-SYNTHETIC-SwordIron-v0.vtbp");
            string text="vtbp 2\nitemstand|0|0|0|0|0|0|1|"+zdo+"\n";
            File.WriteAllText(input,text,new UTF8Encoding(false));
            var store=new CompositeBlueprintStore(Path.Combine(directory,"library.json"),backgroundImports:true);
            void Ready() { if(!SpinWait.SpinUntil(()=>{store.CatalogAll();return !store.CatalogLoading;},10000))throw new InvalidOperationException("CONTROL import/catalog timeout."); }
            UnboundImportContext Context(string name)=>new UnboundImportContext(new BlueprintPrefabMetadata(1,name,name,native.kind,native.host,new Point3(1,1,1)),true,false,false,false,
                nativeHeader:(native.prefabHash,native.persistent,native.distant,native.objectType),itemStandItems:new[]{new UnboundItemStandItem(item.name,item.variants,item.maximumQuality,item.orientations)});
            store.ConfigureImportScale(_=>new CompositeBlueprintStore.VectorData{x=1,y=1,z=1},prefabHash:ForeignZdoChecks.Hash,
                resolvePrefabHash:hash=>hash==ForeignZdoChecks.Hash("SwordIron")?"SwordIron":null,unboundContext:Context);
            Ready();var review=store.PrepareImportReview(CompositeBlueprintStore.ReviewImportFile(input,ForeignZdoChecks.Hash),ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            if(review.Contents.Mounts!=1||review.Contents.Unknown!=0)throw new InvalidOperationException("Ordinary CONTROL demand review failed.");
            if(!store.TryImportReviewed(review,false,false,ForeignZdoChecks.Hash,out var error))throw new InvalidOperationException(error);
            string operation=store.ImportOperationPath;
            Ready();if(store.ImportError!=null)throw new InvalidOperationException(store.ImportError);
            if(operation==null||store.ImportResultPath!=operation||store.ImportOperationPath!=operation||
                store.ImportProgress!=CompositeBlueprintStore.ImportStage.Complete||store.CatalogLoading||
                Directory.GetFiles(store.ImportDirectory).Single()!=operation)throw new InvalidOperationException("CONTROL import did not complete as one exact archive.");
            var catalog=store.CatalogAll().Single();
            if(catalog.importError!=null||catalog.importSource!=Path.GetFileName(operation)+"/document_0.buildworks.json")throw new InvalidOperationException("CONTROL catalog is not the completed operation result.");
            void Exact(CompositeBlueprintStore.Blueprint document) {
                var part=document.parts.Single();if(part.prefabName!="itemstand"||part.foreignData?.format!=BlueprintForeignPartData.NativeItemStandFormat||
                    part.foreignData.info!="SwordIron:0:1:0"||part.foreignData.zdo!=Convert.ToBase64String(packet)||
                    !document.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(File.ReadAllBytes(input)))throw new InvalidOperationException("CONTROL changed during ordinary import.");
            }
            Exact(catalog);
            using(var zip=ZipFile.OpenRead(operation)) {
                string[] names={"original/source.bin","original/name.txt","document_0.buildworks.json"};
                if(zip.Entries.Count!=names.Length||!zip.Entries.Select(entry=>entry.FullName).OrderBy(name=>name).SequenceEqual(names.OrderBy(name=>name)))throw new InvalidOperationException("Unexpected CONTROL archive entries.");
                foreach(var entry in zip.Entries)using(var bytes=new MemoryStream()) {
                    using(var stream=entry.Open())stream.CopyTo(bytes);
                    if(bytes.Length!=entry.Length)throw new InvalidOperationException("Truncated CONTROL archive entry.");
                    if(entry.FullName==names[0]&&!bytes.ToArray().SequenceEqual(File.ReadAllBytes(input)))throw new InvalidOperationException("CONTROL archive original differs.");
                    if(entry.FullName==names[1]&&Encoding.UTF8.GetString(bytes.ToArray())!=Path.GetFileName(input))throw new InvalidOperationException("CONTROL archive filename differs.");
                    if(entry.FullName==names[2])Exact(BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(bytes.ToArray())));
                }
            }
            File.WriteAllText(Path.Combine(directory,"IMPORT_RECEIPT.txt"),"operation="+operation+"\nresult="+store.ImportResultPath+"\nstage="+store.ImportProgress+"\nerror=<null>\narchiveEntries=3\ncatalogCount=1\nparts=1\nsource="+catalog.importSource+"\nid="+catalog.id+"\npayload="+Convert.ToBase64String(packet)+"\n",new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory,"CONTROL_README.txt"),"CONTROL/SYNTHETIC; existing fixture framing + ordinary import/demand with saved native profiles. SwordIron variant0 quality1 orientation0; no Hammer supply. Not genuine author output, current inventory, recipe or game proof. Original invalid corpus unchanged.",new UTF8Encoding(false));
            Console.WriteLine("PASS one CONTROL/SYNTHETIC via ordinary import/demand and saved native capability; no game/current stock/genuine corpus proof.");
        }
    }
}
