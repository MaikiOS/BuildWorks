using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OstrixMods.BuildWorks.Geometry;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks.StoreTests
{
    // Offline evidence only: validate copies, never confirm credit or clean a world source.
    internal static class SavedCreditEvidence
    {
        internal static void Run(string[] args, string temporary)
        {
            string source = Path.GetFullPath(args[1]), profile = Path.GetFullPath(args[4]);
            long world = long.Parse(args[2]); Guid project = Guid.ParseExact(args[3], "N");
            int index = int.Parse(args[6]);
            int phase = args[7] == "intent" ? Journal.CreditIntent : args[7] == "credited" ? Journal.ProfileCredited :
                throw new ArgumentException("Expected stage: intent or credited.");
            string hash = args[8];
            if (project == Guid.Empty || hash.Length != 128 || hash.Any(c => !(c >= '0' && c <= '9' || c >= 'A' && c <= 'F')))
                throw new ArgumentException("A project and the captured uppercase SHA512 profile payload hash are required.");
            var inputs = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            string snapshot = Path.Combine(temporary, "credit-evidence"); Directory.CreateDirectory(snapshot);
            foreach (string path in Directory.EnumerateFiles(source, project.ToString("N") + ".*"))
            {
                string extension = Path.GetExtension(path);
                if (extension != ".bwproject" && extension != ".bwpart" && extension != ".bwremoval" && extension != ".bwpayout") continue;
                byte[] bytes = Read(path, 16 * 1024 * 1024 + 44);
                inputs.Add(path, bytes); File.WriteAllBytes(Path.Combine(snapshot, Path.GetFileName(path)), bytes);
            }
            byte[] saved = Read(profile, NativePlayerProfileReadback.MaximumProfileBytes); inputs.Add(profile, saved);
            var plans = new BlueprintConstructionProjectStore(snapshot, world);
            var record = new Journal(plans).Read(project) ?? throw new InvalidDataException("Missing payout journal.");
            if (plans.Load(project).World != world || record.World != world || record.Phase != Journal.IssuedSaved || record.Terminal == null ||
                index < 0 || record.Collections == null || index >= record.Collections.Length || record.Collections[index]?.Phase != phase)
                throw new InvalidDataException("Expected terminal payout and selected credit stage do not match.");
            var row = record.Collections[index];
            var expected = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, row.AfterInfo, row.AfterBytes, null);
            using (var stream = new MemoryStream(saved, false))
            {
                if (!NativePlayerProfileReadback.TryVerifyInventoryAndCustomData(stream, record.Player, args[5], expected,
                    name => name.GetStableHashCode(), out var proof, out string reason)) throw new InvalidDataException(reason);
                if (proof.ProfileHash != hash || proof.CustomData == null ||
                    !proof.CustomData.TryGetValue(Journal.ReceiptKey(record.Items[index].Id), out string receipt) || receipt != Journal.ReceiptValue(record, index))
                    throw new InvalidDataException("Captured profile hash or canonical saved receipt differs.");
            }
            foreach (var input in inputs)
                if (!Read(input.Key, input.Value.Length).SequenceEqual(input.Value)) throw new InvalidDataException("Input changed during evidence verification.");
            using (var sha = SHA256.Create())
                Console.WriteLine("PASS saved credit pair: stage=" + args[7] + " project=" + project.ToString("N") + " index=" + index +
                    " profileSHA256=" + BitConverter.ToString(sha.ComputeHash(saved)).Replace("-", "") +
                    "; actual journal validation, exact saved inventory and canonical receipt; inputs unchanged. No game, confirmation, cleanup or restart proof.");
        }

        private static byte[] Read(string path, int maximum)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Evidence file cannot be a reparse point.");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length > maximum) throw new InvalidDataException("Evidence file exceeds its budget.");
                using (var buffer = new MemoryStream()) { file.CopyTo(buffer); return buffer.ToArray(); }
            }
        }
    }
}
