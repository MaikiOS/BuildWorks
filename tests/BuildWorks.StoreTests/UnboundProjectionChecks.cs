using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class UnboundProjectionChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string source = Source();
            var raw = ExternalBlueprintLibrary.Parse(source, "projection.vtbp");
            Check(raw.importError == null && raw.parts.Count == 3, "Projection fixture parse");
            string Resolve(int id) => id == ForeignZdoChecks.Hash("Wood") ? "Wood" : id == ForeignZdoChecks.Hash("Stone") ? "Stone" : null;
            var prepared = Prepare(raw, Context, Resolve);
            Check(prepared.importError == null && prepared.parts[0].baseScale.Value.x == 2 && prepared.parts[0].baseScale.Value.y == 3 &&
                prepared.parts[0].baseScale.Value.z == 4 && prepared.parts[0].foreignData == null &&
                prepared.parts[1].signText == "source text" && prepared.parts[1].materialSeed == 17 && prepared.parts[1].foreignData == null &&
                prepared.parts[2].foreignData.format == BlueprintForeignPartData.NativeContainerFormat &&
                prepared.parts[2].foreignData.info == "5:2:2:5|Stone|Wood" && CompositeBlueprintStore.ValidSources(prepared.parts, prepared.sourceDocuments),
                "Actual host preparation lost known fields: " + prepared.importError);
            Check(raw.parts.All(p => p.foreignData.format == UnboundBlueprint.Format && !p.baseScale.HasValue && p.signText == null && !p.materialSeed.HasValue),
                "Preparation mutated original raw data");
            var missingContext = Prepare(prepared, null, Resolve);
            Check(missingContext.importError == "$buildworks_placement.contents_stale" && missingContext.parts.Count == 0,
                "Missing editor context no longer fails atomically");
            Check(BlueprintContainerInventory.TryRead(prepared.parts[2].foreignData.ToGeometry(), ForeignZdoChecks.Hash, out var items) &&
                items.Count == 2 && items[0].Prefab == "Wood" && items[0].Stack == 3 && items[1].Prefab == "Stone" && items[1].Stack == 2 &&
                CompositeBlueprintStore.InspectContents(prepared, ForeignZdoChecks.Hash, name => name == "sign").Unknown == 0, "Known inventory remains unknown or was changed");
            var edited = prepared.ImportCopy(); edited.parts[1].signText = "edited"; edited.parts[1].materialSeed = 18; edited.parts[0].scale.x = 1.5f;
            Check(CompositeBlueprintStore.ValidSources(edited.parts, edited.sourceDocuments), "Working edits break source proof");
            UnboundSourceChecks.Editor(edited);
            var dropped = prepared.ImportCopy(); dropped.parts[1].signText = null;
            Check(!CompositeBlueprintStore.ValidSources(dropped.parts, dropped.sourceDocuments), "Dropped text became applied");
            dropped = prepared.ImportCopy(); dropped.parts[1].materialSeed = null;
            Check(!CompositeBlueprintStore.ValidSources(dropped.parts, dropped.sourceDocuments), "Dropped seed became applied");
            dropped = prepared.ImportCopy(); dropped.parts[0].baseScale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
            Check(!CompositeBlueprintStore.ValidSources(dropped.parts, dropped.sourceDocuments), "Invented scale basis accepted");
            dropped = prepared.ImportCopy(); dropped.parts[2].foreignData = null;
            Check(!CompositeBlueprintStore.ValidSources(dropped.parts, dropped.sourceDocuments), "Dropped items became applied");
            var unresolved = Prepare(raw, Context, _ => null);
            Check(unresolved.parts[2].foreignData.format == UnboundBlueprint.Format && !unresolved.parts[2].baseScale.HasValue,
                "Unresolved item name caused partial conversion");
            var noSync = Prepare(raw, name => Context(name, sync: false), Resolve);
            Check(noSync.parts[0].foreignData.format == UnboundBlueprint.Format && !noSync.parts[0].baseScale.HasValue, "Scale sync capability guessed");
            var stale = Prepare(prepared, name => Context(name, sync: false), Resolve);
            Check(stale.importError != null && stale.parts.Count == 0, "Stale projected capability accepted");
            var unknownRaw = ExternalBlueprintLibrary.Parse(Source(unknown: true), "unknown.vtbp");
            var unknown = Prepare(unknownRaw, Context, Resolve);
            Check(unknown.parts[1].foreignData.format == UnboundBlueprint.Format && unknown.parts[1].signText == null &&
                !unknown.parts[1].materialSeed.HasValue && !unknown.parts[1].baseScale.HasValue, "Unknown key partially applied");
            var forged = unknown.ImportCopy(); forged.parts[1].foreignData = null; forged.parts[1].metadata = prepared.parts[1].metadata;
            forged.parts[1].baseScale = prepared.parts[1].baseScale; forged.parts[1].signText = "forged"; forged.parts[1].materialSeed = 17;
            Check(!CompositeBlueprintStore.ValidSources(forged.parts, forged.sourceDocuments), "Unknown source bypassed by declaring projection fields");
            bool editorRejected = false; try { UnboundSourceChecks.Editor(forged); } catch (ArgumentException) { editorRejected = true; }
            Check(editorRejected, "Editor accepted unknown state without its raw payload");
            string path = Path.Combine(directory, "source.vtbp"); File.WriteAllText(path, source, new UTF8Encoding(false,true));
            int thread = Thread.CurrentThread.ManagedThreadId, profileCalls = 0, itemCalls = 0;
            void OnHost() => Check(Thread.CurrentThread.ManagedThreadId == thread, "Native callback executed on worker");
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"), backgroundImports: true);
            store.ConfigureImportScale(name => { OnHost(); return new CompositeBlueprintStore.VectorData { x = name == "wood_floor" ? 2 : 1,
                y = name == "wood_floor" ? 3 : 1, z = name == "wood_floor" ? 4 : 1 }; }, prefabHash: ForeignZdoChecks.Hash,
                supportsMaterialSeed: name => { OnHost(); return name == "sign"; },
                resolvePrefabHash: id => { OnHost(); ++itemCalls; return Resolve(id); },
                unboundContext: name => { OnHost(); ++profileCalls; return Context(name); });
            Ready();
            var review = store.PrepareImportReview(CompositeBlueprintStore.ReviewImportFile(path, ForeignZdoChecks.Hash), ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Check(review.Contents.Unknown == 0 && profileCalls == 3 && itemCalls == 2, "Host profile/name snapshot incomplete");
            Check(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error), error); Ready();
            Check(store.ImportError == null && store.CatalogAll().Single().parts[1].signText == "source text", "Catalog prepared state lost: " + store.ImportError);
            Check(store.TryGetOrCreatePlacementCopy(edited, out var saved, out error), error);
            var reopened = new CompositeBlueprintStore(Path.Combine(directory, "library.json")).All().Single();
            byte[] portable = BlueprintExchangeFormat.Encode(saved);
            Check(portable.SequenceEqual(BlueprintExchangeFormat.Encode(reopened)) &&
                portable.SequenceEqual(BlueprintExchangeFormat.Encode(BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(portable)))) &&
                reopened.sourceDocuments.Single().text == source, "Native/portable edited state or raw bytes changed");
            Console.WriteLine("PASS Unbound actual atomic host projection: ordinary/scale/text/seed/container bytes,source-derived drop/unknown/stale guards,host-only snapshots,review/archive/catalog/native36portable24;synthetic capabilities only,no game/holder restore.");
            void Ready() => Check(SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000), "Projection catalog timeout");
        }
        private static CompositeBlueprintStore.Blueprint Prepare(CompositeBlueprintStore.Blueprint source, Func<string,UnboundImportContext> context, Func<int,string> names) =>
            ExternalBlueprintLibrary.PrepareForHost(source, _ => null, ForeignZdoChecks.Hash, resolvePrefabHash:names, unboundContext:context);
        private static UnboundImportContext Context(string name) => Context(name,true);
        private static UnboundImportContext Context(string name,bool sync)
        {
            var basis = name == "wood_floor" ? new Point3(2,3,4) : new Point3(1,1,1);
            return new UnboundImportContext(new BlueprintPrefabMetadata(1,name,name,"Piece","synthetic-host",basis),sync,
                name != "piece_chest_wood",name == "sign",name == "sign",name == "piece_chest_wood" ? (5,2) : ((int,int)?)null);
        }
        private static string Source(bool unknown = false)
        {
            string Packet(string prefab,ushort flags,Action<BinaryWriter> body)
            { using(var stream=new MemoryStream()) { using(var w=new BinaryWriter(stream,new UTF8Encoding(false,true),true)){w.Write(flags);w.Write(ForeignZdoChecks.Hash(prefab));body(w);}return Convert.ToBase64String(stream.ToArray()); } }
            string floor=Packet("wood_floor",258,w=>{One(w,-1726348180);w.Write(2f);});
            string sign=Packet("sign",336,w=>{w.Write((byte)(unknown?2:1));w.Write(-1049702188);w.Write(17);if(unknown){w.Write(777);w.Write(1);}One(w,-871206003);w.Write("source text");});
            byte[] inventory;
            using(var stream=new MemoryStream())
            { using(var w=new BinaryWriter(stream,Encoding.UTF8,true)){w.Write(110);w.Write((ushort)2);Item(w,"Wood",0,3);Item(w,"Stone",1,2);}inventory=stream.ToArray(); }
            string chest=Packet("piece_chest_wood",384,w=>{One(w,-938864442);w.Write(inventory.Length);w.Write(inventory);});
            return "\uFEFFvtbp 2\r\nwood_floor|0|0|0|0|0|0|1|"+floor+"\nsign|1|0|0|0|0|0|1|"+sign+"\npiece_chest_wood|2|0|0|0|0|0|1|"+chest;
        }
        private static void Item(BinaryWriter w,string name,byte x,ushort stack)
        {w.Write(0);w.Write(x);w.Write((byte)0);w.Write((byte)0);w.Write((byte)72);w.Write(stack);w.Write(ForeignZdoChecks.Hash(name));w.Write((byte)0);}
        private static void One(BinaryWriter w,int key){w.Write((byte)1);w.Write(key);}
        private static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    }
}
