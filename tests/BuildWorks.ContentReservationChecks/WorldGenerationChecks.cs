using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal static class WorldGenerationChecks
    {
        private static int assertions;
        private static void Check(bool value, string error) { assertions++; if (!value) throw new InvalidOperationException("World generation: " + error); }
        private static byte[] Bytes(Action<BinaryWriter> write)
        {
            using (var memory = new MemoryStream())
            { using (var writer = new BinaryWriter(memory, Encoding.UTF8, true)) write(writer); return memory.ToArray(); }
        }
        private static void WriteWorld(TransferChecks.Frame frame, string root)
        {
            Guid marker = frame.Operation;
            byte[] payload = Bytes(w => { w.Write(41); w.Write("QA"); w.Write("seed"); w.Write(10); w.Write(1L); w.Write(1); w.Write(true); w.Write(0); w.Write(0); });
            File.WriteAllBytes(Path.Combine(root, "_main.7.fwl2"), Bytes(w => { w.Write(payload.Length); w.Write(payload); }));
            File.WriteAllBytes(Path.Combine(root, "_main.7.chunks"), Bytes(w => { w.Write((short)41); w.Write(1); w.Write(1); w.Write((ushort)258); w.Write((byte)3); w.Write(7u); w.Write(1); }));
            File.WriteAllBytes(Path.Combine(root, "_main.7.ok"), BitConverter.GetBytes(41));
            File.WriteAllBytes(Path.Combine(root, "_main.7.db2"), Bytes(w => { w.Write(41); w.Write(123d); }));
            byte[] inventory = frame.Placed.Container.View.Data.Raw;
            File.WriteAllBytes(Path.Combine(root, "01_02__3_7.chunk"), Bytes(w => {
                w.Write((short)41); w.Write(1); w.Write((ushort)(256 | 32 | 64 | 128));
                w.Write(0f); w.Write(0f); w.Write(0f); w.Write(123);
                w.Write((byte)1); w.Write(ZDOVars.s_creator); w.Write(100L);
                w.Write((byte)1); w.Write(BlueprintStorageCapture.TransferMarkerKey); w.Write(marker.ToString("N"));
                w.Write((byte)1); w.Write(ZDOVars.s_items); w.Write(inventory.Length); w.Write(inventory);
            }));
        }
        private static BlueprintStorageWorldGeneration Read(TransferChecks.Frame frame)
        {
            Check(frame.Transfer.TryPrepareWorldReadback(out var binding, out string error), error);
            Check(binding.TryStart(CancellationToken.None, out error), error);
            Wait(binding); return binding;
        }
        private static void Wait(BlueprintStorageWorldGeneration binding)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!binding.IsCompleted && DateTime.UtcNow < deadline) Thread.Sleep(1);
            Check(binding.IsCompleted, "Worker did not complete within fixture deadline");
        }
        internal static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "BuildWorks-world-generation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var frame = new TransferChecks.Frame();
            try
            {
                ZNet.instance.World.Directory = root; SaveSystem.Generation = 7; SaveSystem.Suffix = "";
                Check(frame.Begin(), frame.Error);
                Check(!frame.Transfer.TryPrepareWorldReadback(out _, out _), "Pending transfer readback accepted");
                Check(frame.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "Fixture transfer failed");
                WriteWorld(frame, root);
                var binding = Read(frame);
                Check(binding.Operation == frame.Operation && binding.Generation == 7 && binding.TryFinish(out var evidence, out _) &&
                    evidence.Operation == frame.Operation && evidence.World == 1, "Exact completed world evidence missing");
                Check(!binding.TryStart(CancellationToken.None, out _), "Worker started twice");
                Check(BlueprintStorageTransferJournal.Read(frame.Transfer.JournalPath).RequiresReconciliation, "Readback settled journal");
                foreach (var source in new[] { FileHelpers.FileSource.Auto, FileHelpers.FileSource.Cloud, FileHelpers.FileSource.Legacy })
                {
                    ZNet.instance.World.m_fileSource = source;
                    Check(!frame.Transfer.TryPrepareWorldReadback(out _, out _), "Nonlocal world accepted");
                }
                ZNet.instance.World.m_fileSource = FileHelpers.FileSource.Local;
                ZNet.instance.Server = false; Check(!frame.Transfer.TryPrepareWorldReadback(out _, out _), "Client accepted server files"); ZNet.instance.Server = true;
                ZNet.instance.Saving = true; Check(!frame.Transfer.TryPrepareWorldReadback(out _, out _), "In-progress generation captured"); ZNet.instance.Saving = false;
                SaveSystem.Generation = 0; Check(!frame.Transfer.TryPrepareWorldReadback(out _, out _), "Generation zero accepted"); SaveSystem.Generation = 7;
                SaveSystem.Suffix = ".old"; Check(!frame.Transfer.TryPrepareWorldReadback(out _, out _), "Noncanonical native file names accepted"); SaveSystem.Suffix = "";
                ZNet.instance.World.Directory = "relative"; Check(!frame.Transfer.TryPrepareWorldReadback(out _, out _), "Relative native root accepted"); ZNet.instance.World.Directory = root;
                Check(frame.Transfer.TryPrepareWorldReadback(out binding, out _), "Capture before generation change failed");
                SaveSystem.Generation++; Check(!binding.TryStart(CancellationToken.None, out _), "Changed generation started"); SaveSystem.Generation--;
                binding = Read(frame); SaveSystem.Generation++; Check(!binding.TryFinish(out _, out _), "Changed generation accepted"); SaveSystem.Generation--;
                Check(!binding.TryFinish(out _, out _), "Refused receipt resurrected");
                binding = Read(frame); ZNet.instance.Saving = true; Check(!binding.TryFinish(out _, out _), "Concurrent save accepted"); ZNet.instance.Saving = false;
                binding = Read(frame); frame.Item.m_durability--; Check(!binding.TryFinish(out _, out _), "Changed current custody accepted"); frame.Item.m_durability++;
                binding = Read(frame); var world = ZNet.instance.World; ZNet.instance.World = new World { Directory = root }; Check(!binding.TryFinish(out _, out _), "Different world instance accepted"); ZNet.instance.World = world;
                binding = Read(frame); var net = ZNet.instance; ZNet.instance = new ZNet { World = world }; Check(!binding.TryFinish(out _, out _), "Different host instance accepted"); ZNet.instance = net;
                binding = Read(frame); Check(!Task.Run(() => binding.TryFinish(out _, out _)).Result, "Worker accepted game evidence"); Check(binding.TryFinish(out _, out _), "Correct-thread finish refused after off-thread refusal");
                using (var cancel = new CancellationTokenSource())
                {
                    Check(frame.Transfer.TryPrepareWorldReadback(out binding, out _), "Cancellation capture failed");
                    cancel.Cancel(); Check(!binding.TryStart(cancel.Token, out _), "Pre-cancelled worker started");
                }
                using (var cancel = new CancellationTokenSource())
                {
                    Check(frame.Transfer.TryPrepareWorldReadback(out binding, out _), "Cancellation capture failed");
                    Check(binding.TryStart(cancel.Token, out _), "Cancellable worker refused"); Wait(binding);
                    cancel.Cancel(); Check(!binding.TryFinish(out _, out _), "Cancellation after worker ignored");
                }
                foreach (string extension in new[] { ".fwl2", ".chunks", ".ok", ".db2" })
                {
                    string path = Path.Combine(root, "_main.7" + extension); File.Move(path, path + ".new");
                    binding = Read(frame); Check(!binding.TryFinish(out _, out _), "Missing canonical component replaced with .new " + extension);
                    File.Move(path + ".new", path);
                }
                string database = Path.Combine(root, "_main.7.db2"); File.WriteAllBytes(database, Bytes(w => { w.Write(42); w.Write(123d); }));
                binding = Read(frame); Check(!binding.TryFinish(out _, out _), "Future DB2 accepted");
                File.WriteAllBytes(database, Bytes(w => { w.Write(41); w.Write(double.NaN); })); binding = Read(frame); Check(!binding.TryFinish(out _, out _), "Nonfinite DB2 accepted"); WriteWorld(frame, root);
                string chunk = Path.Combine(root, "01_02__3_7.chunk"); File.Move(chunk, chunk + ".old"); binding = Read(frame); Check(!binding.TryFinish(out _, out _), "Missing mapped chunk accepted"); File.Move(chunk + ".old", chunk);
                var open = typeof(NativeWorldStorageFiles).GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic);
                using (var handle = (FileStream)open.Invoke(null, new object[] { root, "_main.7.fwl2" }))
                {
                    bool writerRefused = false;
                    try { using (File.Open(Path.Combine(root, "_main.7.fwl2"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { } }
                    catch (IOException) { writerRefused = true; }
                    Check(writerRefused, "Generation handle allowed concurrent writes");
                    File.Move(Path.Combine(root, "_main.7.fwl2"), Path.Combine(root, "retired.fwl2"));
                    Check(handle.ReadByte() >= 0, "Native cleanup invalidated pinned generation handle");
                }
                File.Move(Path.Combine(root, "retired.fwl2"), Path.Combine(root, "_main.7.fwl2"));
                int main = Thread.CurrentThread.ManagedThreadId; bool touchedWorker = false;
                Check(BlueprintStorageWorldGeneration.TryCapture(Guid.NewGuid(), frame.Operation, 123, 100, frame.Placed.Container.View.Data.Raw,
                    () => { if (Thread.CurrentThread.ManagedThreadId != main) touchedWorker = true; return true; }, out binding, out _), "Thread fixture capture failed");
                Check(binding.TryStart(CancellationToken.None, out _), "Thread fixture start failed"); Wait(binding);
                Check(binding.TryFinish(out _, out _) && !touchedWorker, "Unity custody callback ran on worker");
                Check(frame.Transfer.TryBeginReturn(out var returned, out string error), error);
                Check(returned.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "Return fixture failed"); WriteWorld(frame, root);
                Check(returned.TryPrepareWorldReadback(out binding, out error) && binding.Operation != frame.Operation, error ?? "Return operation identity lost");
                Check(binding.TryStart(CancellationToken.None, out error), error); Wait(binding);
                Check(binding.TryFinish(out evidence, out error) && evidence.Operation == frame.Operation, error ?? "Return marker must remain parent while transfer operation is child");
                returned.Dispose();
                Check(BlueprintStorageTransferJournal.Read(frame.Transfer.JournalPath).RequiresReconciliation, "Return readback released parent hold");
            }
            finally { frame.Transfer?.Dispose(); SaveSystem.Generation = 7; SaveSystem.Suffix = ""; Directory.Delete(root, true); }
            Console.WriteLine("PASS " + assertions + " generation/worker/custody/file-handle assertions; native host substituted, temporary fixtures only; no game save or settlement.");
        }
    }
}
