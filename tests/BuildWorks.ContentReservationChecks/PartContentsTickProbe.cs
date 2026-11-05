using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
namespace ReservationChecks
{
    // Production BeginContents/Complete/Tick/Refuse are extracted verbatim; only the parent/host lookup is substituted.
    internal sealed partial class PartContentsTickProbe
    {
        private BlueprintStorageTransfer storageTransfer;
        private BlueprintStoragePersistence storagePersistence;
        private BlueprintConstructionProjectPersistence persistence=null;
        private BlueprintConstructionMountExecution mountExecution;
        private readonly BlueprintConstructionPartJournal journal;
        private readonly BlueprintConstructionProjectStore store;
        private readonly BlueprintConstructionProjectStore.Record plan;
        private readonly BlueprintConstructionProjectStore.Part part;
        private readonly long playerId;
        private readonly BlueprintForeignPartData contents;
        private readonly Player player;
        internal bool Finished { get; private set; }
        internal bool Saving => !Finished&&(storageTransfer!=null||persistence!=null||mountExecution!=null);
        internal string Status { get; private set; }
        internal string Reason { get; private set; }
        internal PartContentsTickProbe(BlueprintStorageTransfer transfer) { storageTransfer = transfer; }
        internal PartContentsTickProbe(ConstructionMountExecutionChecks.Rig rig,Guid partId=default)
        {
            store=rig.Store;plan=store.Plan;part=partId==Guid.Empty?plan.Parts[0]:plan.Parts.Find(value=>value.Id==partId);player=rig.Frame.Player;playerId=player.GetPlayerID();
            journal=new BlueprintConstructionPartJournal(store);contents=ContentsDemand(plan.Blueprint.parts[part.SourceIndex]);BeginContents();
        }
        internal static void RequireStoragePreflight(long world) => BlueprintConstructionPartExecution.RequireStoragePreflight(world);
    }
}
