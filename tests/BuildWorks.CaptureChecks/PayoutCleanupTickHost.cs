using System;
using System.Collections.Generic;

namespace CleanupTickChecks
{
    // Inert external boundaries; PayoutCleanupGuard.ps1 inserts the actual production Tick method.
    internal sealed class Proof
    {
        internal long World = 17;
        internal Guid Operation;
        internal uint Generation = 101;
        internal int MarkerKey = Journal.ItemMarkerKey;
        internal bool CompleteMappedScan = true;
        internal List<Guid> Operations;
        internal string ProfileHash = "same";
    }
    internal sealed class Cleanup { }
    internal sealed class Credit { internal Cleanup Removal; }
    internal sealed class Item { internal Guid Id = Guid.NewGuid(); }
    internal sealed class Record
    {
        internal Guid Project = Guid.NewGuid(), Operation = Guid.NewGuid();
        internal long World = 17;
        internal Credit[] Collections = { new Credit() };
        internal Item[] Items = { new Item() };
    }
    internal sealed class Journal
    {
        internal const int ItemMarkerKey = 123;
        internal int Begins, Confirms;
        internal Record Record;
        internal Profile Profile;
        internal Record BeginCleanup(Guid project, Guid operation, int index, uint generation, Proof proof)
        { if (Profile.Saves != 1) throw new Exception("Intent preceded baseline profile save"); Begins++; Record.Collections[index].Removal = new Cleanup(); return Record; }
        internal Record ConfirmCleanup(Guid project, Guid operation, int index, Proof absence, Proof proof) { Confirms++; return Record; }
    }
    internal static class SaveSystem { internal static uint Generation = 100; internal static uint GetSaveNumber() => Generation; }
    internal sealed class Network
    {
        internal bool Saving, StartSave = true;
        internal int Saves;
        internal bool IsSaving() => Saving;
        internal void Save(bool sync, bool saveOtherPlayerProfiles, bool waitForNextFrame)
        {
            if (sync || saveOtherPlayerProfiles || waitForNextFrame) throw new Exception("Native save arguments changed");
            Saves++; if (StartSave) SaveSystem.Generation++;
        }
    }
    internal sealed class Manager { internal object Object; internal object GetZDO(int id) => Object; }
    internal sealed class Scene { internal bool Present; internal bool FindInstance(int id) => Present; }
    internal sealed class Scan
    {
        internal bool IsCompleted = true, Accept = true;
        internal Proof Proof;
        internal bool TryFinish(out Proof proof, out string error) { proof = Proof; error = null; return Accept; }
        internal bool TryFinishAbsence(out Proof proof, out string error) { proof = Proof; error = null; return Accept; }
    }
    internal sealed class Profile
    {
        internal bool Accept = true;
        internal int Saves;
        internal bool TrySaveAndVerify(out Proof proof, out string error) { Saves++; proof = new Proof(); error = null; return Accept; }
    }
    internal sealed partial class Probe
    {
        internal string Status = "checking-source", Reason;
        internal bool Finished => Status == "cleanup-saved" || Status == "reconciliation";
        internal readonly int index = 0, removedId = 42;
        internal readonly uint beforeGeneration = 100;
        internal uint absenceGeneration = 101;
        internal DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        internal bool replay, Valid = true, SourceValid = true, NoLoadedSources = true, ProfileValid = true, ReceiptValid = true;
        internal int Deletes, AbsenceStarts, ProfileSaveRequests, WorldSaveRequests;
        internal Record record = new Record();
        internal Journal journal = new Journal();
        internal Network network = new Network();
        internal Manager manager = new Manager();
        internal Scene scene = new Scene();
        internal Scan scan = new Scan();
        internal Profile profile = new Profile();
        internal Probe()
        {
            SaveSystem.Generation = 100; journal.Record = record; journal.Profile = profile;
            scan.Proof = new Proof { World = record.World, Operation = record.Items[0].Id, Operations = new List<Guid> { record.Items[0].Id } };
        }
        private bool Custody() => Valid;
        private bool BeforeCustody() => SourceValid;
        private bool NoSources() => NoLoadedSources;
        private bool ReadReceipt(out Proof proof, out string error) { proof = new Proof(); error = null; return ReceiptValid; }
        private bool ExactProfile(out Proof proof, out string error) { proof = new Proof(); error = null; return ProfileValid; }
        private void Delete() { if (record.Collections[index].Removal == null) throw new Exception("Deletion preceded intent"); Deletes++; Status = "queued-removal"; }
        private void StartAbsence(uint generation) { AbsenceStarts++; absenceGeneration = generation; Status = "reading-absence"; }
        private void Refuse(string error) { Reason = error; Status = "reconciliation"; }
        __ACTUAL_TICK__
    }
    public static class Runner
    {
        private static int count;
        private static void Check(bool valid, string message) { count++; if (!valid) throw new Exception(message); }
        public static int Run()
        {
            var p = new Probe(); p.scan.IsCompleted = false; p.Tick(); Check(p.Deletes == 0 && p.journal.Begins == 0, "Incomplete source scan caused effect");
            p = new Probe(); p.SourceValid = false; p.Tick(); Check(p.Status == "reconciliation" && p.Deletes == 0, "Changed live source deleted");
            p = new Probe(); p.scan.Accept = false; p.Tick(); Check(p.Deletes == 0 && p.Status == "reconciliation", "Rejected saved source deleted");
            p = new Probe(); p.scan.Proof.World++; p.Tick(); Check(p.Deletes == 0 && p.journal.Begins == 0, "Another saved world admitted intent");
            p = new Probe(); p.scan.Proof.Operation = Guid.NewGuid(); p.Tick(); Check(p.Deletes == 0 && p.journal.Begins == 0, "Another saved source admitted intent");
            p = new Probe(); p.profile.Accept = false; p.Tick(); Check(p.Deletes == 0 && p.journal.Begins == 0, "Failed baseline profile allowed intent");
            p = new Probe(); p.ReceiptValid = false; p.Tick(); Check(p.Deletes == 0 && p.journal.Begins == 0, "Missing saved receipt allowed intent");
            p = new Probe(); p.Tick(); Check(p.profile.Saves == 1 && p.journal.Begins == 1 && p.Deletes == 1 && p.Status == "queued-removal", "Fresh path order not completed");
            p = new Probe(); p.record.Collections[0].Removal = new Cleanup(); p.Tick(); Check(p.profile.Saves == 0 && p.journal.Begins == 0 && p.Deletes == 1, "Existing intent repeated baseline/intent");
            p = new Probe(); p.Status = "queued-removal"; p.manager.Object = new object(); p.Tick(); Check(p.network.Saves == 0, "World saved with live ZDO");
            p.manager.Object = null; p.scene.Present = true; p.Tick(); Check(p.network.Saves == 0, "World saved with live scene instance");
            p.scene.Present = false; p.Tick(); Check(p.network.Saves == 1 && p.Status == "saving", "Both registries absent did not request exact save");
            p.Tick(); Check(p.AbsenceStarts == 1 && p.journal.Confirms == 1 && p.Status == "cleanup-saved", "Next generation exact absence not confirmed");
            p.Tick(); Check(p.network.Saves == 1 && p.journal.Confirms == 1, "Finished operation repeated effect");
            p = new Probe(); p.Status = "queued-removal"; p.NoLoadedSources = false; p.Tick(); Check(p.network.Saves == 0 && p.Status == "reconciliation", "Extra loaded source allowed save");
            p = new Probe(); p.Status = "queued-removal"; p.ProfileValid = false; p.Tick(); Check(p.network.Saves == 0, "Changed profile allowed world save");
            p = new Probe(); p.Status = "queued-removal"; p.network.Saving = true; p.Tick(); Check(p.Status == "reconciliation" && p.network.Saves == 0, "Overlapping world save accepted before deletion drain");
            p = new Probe(); p.Status = "queued-removal"; SaveSystem.Generation++; p.Tick(); Check(p.Status == "reconciliation" && p.network.Saves == 0, "Generation drift accepted before deletion drain");
            p = new Probe(); p.Status = "queued-removal"; p.network.StartSave = false; p.Tick(); Check(p.Status == "reconciliation", "Missing next save accepted");
            p = new Probe(); p.Status = "saving"; p.network.Saving = true; p.Tick(); Check(p.AbsenceStarts == 0 && p.journal.Confirms == 0, "Readback started while saving");
            p = new Probe(); p.Status = "saving"; SaveSystem.Generation = 102; p.Tick(); Check(p.Status == "reconciliation" && p.AbsenceStarts == 0, "Wrong saved generation accepted");
            foreach (Action<Proof> damage in new Action<Proof>[] { x => x.World++, x => x.Generation++, x => x.MarkerKey++,
                x => x.CompleteMappedScan = false, x => x.Operations.Clear(), x => x.Operations[0] = Guid.NewGuid() })
            { p = new Probe(); p.Status = "reading-absence"; damage(p.scan.Proof); p.Tick(); Check(p.Status == "reconciliation" && p.journal.Confirms == 0, "Wrong absence evidence confirmed"); }
            p = new Probe(); p.Status = "reading-absence"; p.ReceiptValid = false; p.Tick(); Check(p.Status == "reconciliation" && p.journal.Confirms == 0, "Missing receipt confirmed cleanup");
            p = new Probe(); p.Status = "reading-absence"; p.replay = true; p.Tick(); Check(p.Status == "cleanup-saved" && p.journal.Confirms == 0 && p.Deletes == 0 && p.network.Saves == 0, "Settled replay changed durable state");
            p = new Probe(); p.Valid = false; p.Tick(); Check(p.Status == "reconciliation" && p.Deletes == 0, "Lost context allowed deletion");
            p = new Probe(); p.deadline = DateTime.UtcNow.AddMinutes(-1); p.Tick(); Check(p.Status == "reconciliation" && p.Deletes == 0, "Expired cleanup allowed deletion");
            return count;
        }
    }
}
