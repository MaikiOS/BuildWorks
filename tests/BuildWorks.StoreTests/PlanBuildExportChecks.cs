using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PlanBuildExportChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            var source = new CompositeBlueprintStore.Blueprint
            {
                id = Guid.NewGuid().ToString("N"), name = "Plan test", prefabName = "test", category = "OTHER",
                primaryPartId = "wall", placementSupportId = "support",
                supports = new System.Collections.Generic.List<CompositeBlueprintStore.Support> {
                    new CompositeBlueprintStore.Support { stableId = "support", name = "Entry",
                        position = new CompositeBlueprintStore.VectorData { x = 4, y = 2, z = 3 },
                        rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } } },
                customSnapPoints = new System.Collections.Generic.List<CompositeBlueprintStore.VectorData> {
                    new CompositeBlueprintStore.VectorData { x = 7, y = -3, z = 9 } }
            };
            var wall = Part("woodwall", "wall");
            wall.baseScale = new CompositeBlueprintStore.VectorData { x = 2, y = .5f, z = 3 };
            wall.scale = new CompositeBlueprintStore.VectorData { x = .5f, y = 2, z = 1 };
            wall.position = new CompositeBlueprintStore.VectorData { x = 3.25f, y = -2.5f, z = .75f };
            wall.rotation = new CompositeBlueprintStore.QuaternionData { y = .70710677f, w = .70710677f };
            wall.materialSeed = 7;
            var sign = Part("sign", "sign"); sign.signText = "Привет\n\"Ostrix\"\\путь";
            var unsafeSign = Part("sign", "comma"); unsafeSign.signText = "One, two; three";
            var emptySign = Part("sign", "empty"); emptySign.signText = "";
            var chest = Part("piece_chest", "chest");
            chest.foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = Geometry.BlueprintForeignPartData.NativeContainerFormat, info = "5:2:0:0", zdo = "bQAAAAAA" };
            source.parts.AddRange(new[] { wall, sign, unsafeSign, emptySign, chest });
            source.groups.Add(new CompositeBlueprintStore.Group { stableId = "group", name = "Walls", pivotPartId = "wall" });
            wall.parentGroupId = "group";
            byte[] original = BlueprintExchangeFormat.Encode(source);
            var culture = CultureInfo.CurrentCulture;
            PlanBuildExport review;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                review = store.ReviewPlanBuildExport(source).GetAwaiter().GetResult();
                Require(!Directory.Exists(store.ExportDirectory), "Review wrote files");
                Require(review.Parts == 5 && review.Groups == 1 && review.Supports == 1 && review.CustomSnaps == 1 &&
                    review.MaterialSeeds == 1 && review.OmittedState["piece_chest / " + Geometry.BlueprintForeignPartData.NativeContainerFormat] == 1 &&
                    review.OmittedSigns.Count == 2, "Loss review omitted a supported category");
                Require(store.TryExportPlanBuild(review, source, directory, "test", out string result, out string error), error);
                string foreign = File.ReadAllText(Path.Combine(result, "test.blueprint"));
                Require(foreign.StartsWith("#Name:Plan test\n#Creator:Unspecified (BuildWorks export)\n#Description:\"") &&
                    foreign.Contains("#Category:OTHER\n#Pieces\n") && foreign.EndsWith("#SnapPoints\n7;-3;9\n"), "PlanBuild section contract changed");
                string[] rows = foreign.Split('\n').Where(line => line.Contains(";Building;")).ToArray();
                Require(rows.Length == 5 && rows.All(row => row.Split(';').Length == 13), "Not exactly thirteen fields");
                Require(rows[0].EndsWith(";\"\";1;1;3") && rows[0].Contains(";3.25;-2.5;0.75;"), "Relative scale or culture leaked into export");
                var read = ExternalBlueprintLibrary.Parse(foreign, "test.blueprint");
                Require(read.importError == null && read.parts.Count == 5 && read.parts[1].signText == sign.signText &&
                    read.parts[2].signText == null && read.parts[3].signText == null &&
                    read.parts[0].scale.x == 1 && read.parts[0].scale.y == 1 && read.parts[0].scale.z == 3 &&
                    read.parts[0].rotation.y == wall.rotation.y, read.importError ?? "Foreign fields or safe text changed");
                byte[] backup = File.ReadAllBytes(Path.Combine(result, "test.buildworks.json"));
                Require(backup.SequenceEqual(original), "Full backup lost or changed original data");
                var reopened = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(backup));
                Require(reopened.parts[4].foreignData.zdo == chest.foreignData.zdo && reopened.parts[2].signText == unsafeSign.signText &&
                    reopened.parts[3].signText == "" && reopened.supports.Count == 1 && reopened.groups.Count == 1 && reopened.parts[0].materialSeed == 7,
                    "Full backup cannot reopen omitted data");
                Require(!store.TryExportPlanBuild(review, source, directory, "test", out _, out error) && error.Contains("exists") &&
                    File.ReadAllText(Path.Combine(result, "test.blueprint")) == foreign, "Collision overwrote bundle");
            }
            finally { CultureInfo.CurrentCulture = culture; }
            Require(BlueprintExchangeFormat.Encode(source).SequenceEqual(original), "Export mutated source");
            string asynchronous = store.ExportPlanBuild(review, source, directory, "async").GetAwaiter().GetResult();
            Require(File.ReadAllBytes(Path.Combine(asynchronous, "async.buildworks.json")).SequenceEqual(original),
                "Background export lost the full document");
            wall.position.x += 1;
            Require(!store.TryExportPlanBuild(review, source, directory, "stale", out _, out string stale) && stale.Contains("plan_changed") &&
                !Directory.Exists(Path.Combine(directory, "stale")), "Stale review published old data");
            bool staleAsync = false;
            try { store.ExportPlanBuild(review, source, directory, "async-stale").GetAwaiter().GetResult(); }
            catch (IOException error) { staleAsync = error.Message.Contains("plan_changed"); }
            Require(staleAsync && !Directory.Exists(Path.Combine(directory, "async-stale")), "Background export accepted a stale review");
            wall.position.x -= 1;
            foreach (string invalid in new[] { "../escape", "bad:name", "bad.", " " })
                Require(!store.TryExportPlanBuild(review, source, directory, invalid, out _, out _), "Unsafe name accepted");
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                string longName = new string('x', 120);
                Require(!store.TryExportPlanBuild(review, source, directory, longName, out _, out string pathError) &&
                    pathError.Contains("plan_path") && !Directory.Exists(Path.Combine(directory, longName)), "Long path was not refused before creating a bundle");
            }
            string blocker = Path.Combine(directory, "blocker"); File.WriteAllText(blocker, "keep");
            Require(!store.TryExportPlanBuild(review, source, blocker, "failure", out _, out _) && File.ReadAllText(blocker) == "keep",
                "Failed directory write damaged existing data");
            Require(!Directory.GetDirectories(directory).Any(path => path.EndsWith(".tmp")), "Failed export leaked transaction directory");

            wall.baseScale = null;
            bool refused = false;
            try { store.ReviewPlanBuildExport(source).GetAwaiter().GetResult(); }
            catch (InvalidDataException error) { refused = error.Message.Contains("scale_pending"); }
            Require(refused, "Unknown prefab scale invented a unit basis");
            float basisX = 2;
            int callerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            store.ConfigureImportScale(_ => {
                Require(System.Threading.Thread.CurrentThread.ManagedThreadId == callerThread, "Prefab resolver entered file worker");
                return new CompositeBlueprintStore.VectorData { x = basisX, y = .5f, z = 3 };
            });
            var hostReview = store.ReviewPlanBuildExport(source).GetAwaiter().GetResult();
            store.ExportPlanBuild(hostReview, source, directory, "async-host").GetAwaiter().GetResult();
            var cancelledReview = store.ReviewPlanBuildExport(source).GetAwaiter().GetResult();
            bool cancelled = false;
            try
            {
                cancelledReview.TryWrite(source, _ => {
                    cancelledReview.CancelWrite();
                    return new CompositeBlueprintStore.VectorData { x = basisX, y = .5f, z = 3 };
                }, directory, "cancelled", out _, out _);
            }
            catch (OperationCanceledException) { cancelled = true; }
            Require(cancelled && !Directory.Exists(Path.Combine(directory, "cancelled")) &&
                !Directory.GetDirectories(directory).Any(path => path.EndsWith(".tmp")), "Cancelled export published or leaked staging files");
            bool cancelledAsync = false;
            try { store.ExportPlanBuild(cancelledReview, source, directory, "cancelled-async").GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { cancelledAsync = true; }
            Require(cancelledAsync && !Directory.Exists(Path.Combine(directory, "cancelled-async")), "Closed review restarted in background");
            basisX = 3;
            Require(!store.TryExportPlanBuild(hostReview, source, directory, "host-stale", out _, out stale) && stale.Contains("plan_changed"),
                "Changed host basis used old scale");
            wall.scaleIsAbsolute = true;
            store.ConfigureImportScale(_ => null);
            var absoluteReview = store.ReviewPlanBuildExport(source).GetAwaiter().GetResult();
            Require(store.TryExportPlanBuild(absoluteReview, source, directory, "absolute", out string absolutePath, out string absoluteError), absoluteError);
            Require(ExternalBlueprintLibrary.LoadFile(Path.Combine(absolutePath, "absolute.blueprint"))[0].parts[0].scale.x == .5f,
                "Known source absolute scale required missing prefab");

            var raw = ExternalBlueprintLibrary.Parse("#Name:Original\n#Creator:Original Author\n#Pieces\nwoodwall;Building;1;2;3;0;0;0;1;\"\";1;1;1\n", "original.blueprint");
            var rawReview = store.ReviewPlanBuildExport(raw).GetAwaiter().GetResult();
            Require(store.TryExportPlanBuild(rawReview, raw, directory, "source", out string rawPath, out string rawError), rawError);
            Require(File.ReadAllText(Path.Combine(rawPath, "source.blueprint")).Contains("#Creator:Original Author\n"), "Original creator attribution was overwritten");
            Require(BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(rawPath, "source.buildworks.json"))).sourceDocuments[0].text ==
                raw.sourceDocuments[0].text, "Original foreign source archive lost");
            foreach (string unsafeId in new[] { "unsafe;id", "unsafe,id", "unsafe(id", "#unsafe" })
            {
                source.parts[0].prefabName = unsafeId;
                refused = false;
                try { store.ReviewPlanBuildExport(source).GetAwaiter().GetResult(); }
                catch (InvalidDataException error) { refused = error.Message.Contains("plan_identifier"); }
                Require(refused, "Prefab ID changed in foreign reader: " + unsafeId);
            }
            Console.WriteLine("PASS: PlanBuild thirteen fields, nonunit basis, invariant/sign losses, full backup, stale/collision/failure and source preservation.");
        }

        private static CompositeBlueprintStore.Part Part(string prefab, string id) => new CompositeBlueprintStore.Part {
            prefabName = prefab, stableId = id, displayName = id, rotation = new CompositeBlueprintStore.QuaternionData { w = 1 },
            baseScale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 } };
        private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    }
}
