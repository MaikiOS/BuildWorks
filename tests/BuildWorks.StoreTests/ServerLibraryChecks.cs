using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ServerLibraryChecks
    {
        internal static void Run(string folder)
        {
            bool administrator = true;
            var library = new ServerBlueprintLibrary(folder, principal => principal == "admin" && administrator);
            byte[] source = Document("Public"), privateSource = Document("Private server restoration");
            byte[] original = (byte[])source.Clone();
            Require(library.List("player").Count == 0 && !Directory.Exists(folder), "Empty read created server files");
            Denied(() => library.Publish("player", source, ServerBlueprintLibrary.Access.Players));
            Require(!Directory.Exists(folder), "Unauthorized publish created a directory");
            var shared = library.Publish("admin", source, ServerBlueprintLibrary.Access.Players);
            var hidden = library.Publish("admin", privateSource, ServerBlueprintLibrary.Access.Administrators);
            Require(library.List("player").Single().Id == shared.Id && library.List("admin").Count == 2,
                "Restricted metadata appeared in public list");
            string forbidden = Error(() => library.Read("player", hidden.Id, hidden.Revision));
            Require(forbidden == Error(() => library.Read("player", Guid.NewGuid().ToString("N"), 1)) &&
                forbidden == Error(() => library.Read("player", "../outside", 1)), "Read leaks restricted entry existence or permits a path");
            Denied(() => library.Delete("player", shared.Id, shared.Revision));
            Denied(() => library.Publish("player", source, ServerBlueprintLibrary.Access.Administrators, shared.Id, shared.Revision));
            Require(library.Read("player", shared.Id, 1).SequenceEqual(original), "Public bytes changed");
            source[0] ^= 1;
            byte[] read = library.Read("admin", shared.Id, 1); read[0] ^= 1;
            Require(library.Read("player", shared.Id, 1).SequenceEqual(original), "Input/output byte arrays alias server state");
            administrator = false;
            Require(library.List("admin").Count == 1, "Cached administrator list survived revocation");
            Denied(() => library.Publish("admin", original, ServerBlueprintLibrary.Access.Players));
            Require(Error(() => library.Read("admin", hidden.Id, 1)) == forbidden, "Revoked admin read restricted bytes");
            administrator = true;
            byte[] updated = Document("Updated");
            var replacement = library.Publish("admin", updated, ServerBlueprintLibrary.Access.Administrators, shared.Id, 1);
            Require(replacement.Id == shared.Id && replacement.Revision == 2 && replacement.Publisher == "admin" &&
                library.List("player").Count == 0, "Update did not preserve ID/revision/publisher or revoke public visibility");
            Error(() => library.Read("admin", shared.Id, 1));
            Error(() => library.Publish("admin", original, ServerBlueprintLibrary.Access.Players, shared.Id, 1));
            Error(() => library.Delete("admin", shared.Id, 1));
            Require(library.Read("admin", shared.Id, 2).SequenceEqual(updated), "Stale operation changed current data");
            string lockedPath = Path.Combine(folder, shared.Id + ".bwserver.zip");
            using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Error(() => library.Publish("admin", original, ServerBlueprintLibrary.Access.Players, shared.Id, 2));
                Error(() => library.Delete("admin", shared.Id, 2));
                Require(library.Read("admin", shared.Id, 2).SequenceEqual(updated) && Directory.GetFiles(folder, "*.tmp.zip").Length == 0,
                    "Failed replacement/deletion changed old data or leaked staging");
            }
            var reloaded = new ServerBlueprintLibrary(folder, principal => principal == "admin");
            Require(reloaded.List("admin").Count == 2 && reloaded.Read("admin", shared.Id, 2).SequenceEqual(updated),
                "Server restart lost data/ACL/revision");
            library.Delete("admin", shared.Id, 2);
            Require(new ServerBlueprintLibrary(folder, _ => true).List("admin").Single().Id == hidden.Id &&
                Directory.GetFiles(folder, "*.tmp.zip").Length == 0, "Delete/restart or staging cleanup failed");

            string boundedFolder = folder + "-bounded";
            var bounded = new ServerBlueprintLibrary(boundedFolder, _ => true, 1);
            Error(() => bounded.Publish("admin", original, ServerBlueprintLibrary.Access.Players));
            Require(!Directory.Exists(boundedFolder), "Capacity rejection created files");
            Error(() => new ServerBlueprintLibrary(folder, _ => true, 1).List("admin"));
            Error(() => library.Publish("admin", original, (ServerBlueprintLibrary.Access)2));
            Error(() => library.Publish("admin", Encoding.UTF8.GetBytes("{}"), ServerBlueprintLibrary.Access.Players));
            Denied(() => library.List("\nforged"));
            Error(() => library.Publish("admin", original, ServerBlueprintLibrary.Access.Players, null, 1));

            // Revocation after staging must refuse publication and remove the exact temporary file.
            string revokedFolder = folder + "-revoked"; int checks = 0;
            var revoked = new ServerBlueprintLibrary(revokedFolder, _ => ++checks == 1);
            Denied(() => revoked.Publish("admin", original, ServerBlueprintLibrary.Access.Players));
            Require(Directory.Exists(revokedFolder) && Directory.GetFiles(revokedFolder).Length == 0,
                "Revocation at commit left a publication or temporary archive");

            string file = Directory.GetFiles(folder, "*.bwserver.zip").Single();
            byte[] intact = File.ReadAllBytes(file);
            using (var append = new FileStream(file, FileMode.Append)) append.WriteByte(1);
            Error(() => library.Read("admin", hidden.Id, 1));
            Error(() => library.Publish("admin", original, ServerBlueprintLibrary.Access.Players, hidden.Id, 1));
            Error(() => library.Delete("admin", hidden.Id, 1));
            Require(File.ReadAllBytes(file).Length == intact.Length + 1, "Changed server file was overwritten or removed");
            File.WriteAllBytes(file, intact);

            // Access metadata and embedded document are bound by strict IDs/version/SHA and exact archive members.
            foreach (string fault in new[] { "version", "id", "sha", "extra", "duplicate" })
            {
                string invalidFolder = folder + "-" + fault; Directory.CreateDirectory(invalidFolder);
                string invalid = Path.Combine(invalidFolder, Path.GetFileName(file)); File.WriteAllBytes(invalid, intact);
                using (var zip = ZipFile.Open(invalid, ZipArchiveMode.Update))
                {
                    var meta = zip.GetEntry("access.json");
                    string text; using (var reader = new StreamReader(meta.Open())) text = reader.ReadToEnd();
                    if (fault == "extra" || fault == "duplicate")
                    { using (var writer = new StreamWriter(zip.CreateEntry(fault == "extra" ? "unknown.bin" : "access.json").Open())) writer.Write("{}"); }
                    else
                    {
                        meta.Delete();
                        if (fault == "version") text = text.Replace("\"version\":1", "\"version\":2");
                        if (fault == "id") text = text.Replace(hidden.Id, Guid.NewGuid().ToString("N"));
                        if (fault == "sha") text = text.Replace(hidden.DocumentSha256, new string('0', 64));
                        using (var writer = new StreamWriter(zip.CreateEntry("access.json").Open())) writer.Write(text);
                    }
                }
                var corrupt = new ServerBlueprintLibrary(invalidFolder, _ => true);
                Error(() => corrupt.List("admin")); Error(() => corrupt.List("player"));
            }
            Console.WriteLine("PASS server library storage: current ACL/list+file denial, admin-only mutation, revisions/revocation/atomic cleanup, reload, defensive copies, shared capacity, changed/corrupt/future/duplicate archive refusal. No authentication/RPC/UI/game proof.");
        }

        private static byte[] Document(string name)
        {
            var parsed = ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.Parse(
                "#Name:" + name + "\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1", "server-source.blueprint"),
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            var document = ExternalBlueprintLibrary.DetachedCopy(parsed);
            // Keep deterministic bytes for the exact read/reload assertions.
            document.id = "ca042ed8b76b4f0d8270ace4bd4d05bc";
            return BlueprintExchangeFormat.Encode(document);
        }
        private static string Error(Action action)
        {
            try { action(); }
            catch (Exception exception) when (exception is InvalidDataException || exception is IOException || exception is System.Runtime.Serialization.SerializationException)
            { return exception.Message; }
            throw new Exception("Expected invalid server-library operation");
        }
        private static void Denied(Action action)
        {
            try { action(); }
            catch (UnauthorizedAccessException) { return; }
            throw new Exception("Expected server permission denial");
        }
        private static void Require(bool condition, string error) { if (!condition) throw new Exception(error); }
    }
}
