using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;
using Projects = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ConstructionProjectChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder); var library = new B(Path.Combine(folder, "library.json"));
            Require(library.TrySaveDocument("Unpaid fixture", "QA", new[] { Part("a", 0), Part("b", 2) }, Array.Empty<B.Group>(), Array.Empty<B.VectorData>(),
                "a", null, out var blueprint, out string error), error);
            var values = new float[] { 2500.125f, 45.5f, -1900.75f, 0, .38268343f, 0, .9238795f, 1, 1.5f, 2 };
            var pose = new Projects.Pose(values); var poses = new[] { pose, new Projects.Pose(new float[] { 2502, 46, -1901, 0, 0, 0, 1, 2, 1, 1 }) };
            var directory = Path.Combine(folder, "projects"); var store = new Projects(directory, 42); Guid id = Guid.NewGuid();
            byte[] snapshot = BlueprintExchangeFormat.Encode(blueprint); var saved = store.Create(id, blueprint, pose, poses);
            Require(new Projects(directory, 42).Ids().SequenceEqual(new[] { id }), "Reload enumeration lost the project ID.");
            values[0] = 0; blueprint.name = "Changed original";
            byte[] exposed = saved.BlueprintBytes; exposed[0] ^= 1; saved.Blueprint.parts[0].prefabName = "Changed clone";
            var reloaded = new Projects(directory, 42).Load(id);
            Require(reloaded.Id == id && reloaded.World == 42 && reloaded.Revision == 1 && !reloaded.Cancelled && reloaded.BlueprintBytes.SequenceEqual(snapshot), "Project snapshot/identity/revision did not survive reload.");
            Require(reloaded.RootPose.Values[0] == 2500.125f && reloaded.RootPose.Values.SequenceEqual(pose.Values), "Root pose changed or aliases caller storage.");
            Require(reloaded.Parts.Select(p => p.Id).SequenceEqual(saved.Parts.Select(p => p.Id)) && reloaded.Parts.Select(p => p.SourceIndex).SequenceEqual(new[] { 0, 1 }) &&
                reloaded.Parts.Select(p => p.WorldPose.Values).Zip(poses, (a, b) => a.SequenceEqual(b.Values)).All(v => v), "Part IDs/source mapping/poses changed.");
            var repeated = store.Create(id, reloaded.Blueprint, pose, poses);
            Require(repeated.Parts.Select(p => p.Id).SequenceEqual(saved.Parts.Select(p => p.Id)), "Repeated create generated new part identities.");
            Refuse<InvalidOperationException>(() => store.Create(id, blueprint, pose, poses));
            Refuse<InvalidDataException>(() => new Projects(directory, 43).Load(id));
            Refuse<InvalidOperationException>(() => store.CancelUnpaid(id, 2, Guid.NewGuid()));
            string file = Path.Combine(directory, id.ToString("N") + ".bwproject"); byte[] pending = File.ReadAllBytes(file);
            using (var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                Refuse<IOException>(() => store.CancelUnpaid(id, 1, Guid.NewGuid()));
            Require(File.ReadAllBytes(file).SequenceEqual(pending) && !Directory.GetFiles(directory, "*.preparing").Any(), "Failed atomic replacement changed the prior project or leaked a temporary file.");
            Guid operation = Guid.NewGuid(); var cancelled = store.CancelUnpaid(id, 1, operation);
            Require(cancelled.Cancelled && cancelled.Revision == 2 && cancelled.CancelOperation == operation && cancelled.BlueprintBytes.SequenceEqual(snapshot), "Unpaid cancellation lost its snapshot or operation identity.");
            Require(new Projects(directory, 42).CancelUnpaid(id, 1, operation).CancelOperation == operation, "Cancellation replay did not return its durable result.");
            Refuse<InvalidOperationException>(() => store.CancelUnpaid(id, 1, Guid.NewGuid()));
            Refuse<InvalidOperationException>(() => store.Create(id, reloaded.Blueprint, pose, poses));
            byte[] terminal = File.ReadAllBytes(file), corrupt = (byte[])terminal.Clone(); corrupt[50] ^= 1; File.WriteAllBytes(file, corrupt);
            Refuse<InvalidDataException>(() => store.Load(id));
            File.WriteAllBytes(file, terminal.Take(31).ToArray()); Refuse<InvalidDataException>(() => store.Load(id));
            var payload = terminal.Skip(8).Take(terminal.Length - 40).Concat(new byte[] { 1 }).ToArray();
            using (var sha = SHA256.Create()) File.WriteAllBytes(file, terminal.Take(8).Concat(payload).Concat(sha.ComputeHash(payload)).ToArray());
            Refuse<InvalidDataException>(() => store.Load(id)); // Valid hash cannot turn extra paid/refund data into an unpaid plan.
            File.WriteAllBytes(file, terminal);
            File.WriteAllText(Path.Combine(directory, "incomplete.preparing"), "partial");
            Require(store.Load(id).Cancelled, "Unrelated partial staging file replaced the confirmed record.");
            Require(store.Ids().SequenceEqual(new[] { id }), "Staging/lock files became restorable projects.");
            string malformedName = Path.Combine(directory, "not-a-project-id.bwproject"); File.WriteAllText(malformedName, "invalid");
            Refuse<InvalidDataException>(() => store.Ids().ToArray()); File.Delete(malformedName);
            Refuse<ArgumentException>(() => store.Create(Guid.Empty, reloaded.Blueprint, pose, poses));
            Refuse<InvalidDataException>(() => store.Create(Guid.NewGuid(), reloaded.Blueprint, pose, poses.Take(1).ToArray()));
            Refuse<InvalidDataException>(() => new Projects.Pose(new float[10]));
            var invalid = pose.Values; invalid[0] = float.NaN; Refuse<InvalidDataException>(() => new Projects.Pose(invalid));
            invalid = pose.Values; invalid[7] = 0; Refuse<InvalidDataException>(() => new Projects.Pose(invalid));
            Require(store.Load(id).Blueprint.parts.Select(p => p.stableId).SequenceEqual(new[] { "a", "b" }), "Source stable IDs were lost.");
            Console.WriteLine("PASS unpaid project store: immutable portable snapshot/TRS/part IDs, atomic reload/cancel replay, world/revision/conflict/corruption/extra-data/IO failures. No native construction, payment, refunds, board or server authorization executed.");
            ConstructionPartJournalChecks.Run(Path.Combine(folder, "part-journal"));
        }
        private static B.Part Part(string id, float x) => new B.Part {
            stableId = id, prefabName = "woodwall", displayName = "Wall", position = new B.VectorData { x = x },
            rotation = new B.QuaternionData { w = 1 }, scale = new B.VectorData { x = 1, y = 1, z = 1 },
            baseScale = new B.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true
        };
        private static void Require(bool value, string error) { if (!value) throw new Exception(error); }
        private static void Refuse<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected refusal: " + typeof(T).Name); }
    }
}
