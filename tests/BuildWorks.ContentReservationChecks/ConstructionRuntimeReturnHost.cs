using System;
using System.Collections.Generic;
namespace ReservationChecks
{
    internal sealed class BlueprintConstructionProjectBoard : Obj
    {
        internal Guid Id;internal readonly Transform transform=new Transform();
    }
    internal sealed class BlueprintConstructionProjectView
    {
        internal bool IsOpen;
        internal void Tick(BlueprintConstructionProjectRuntime runtime,BlueprintConstructionProjectBoard board) { IsOpen=board; }
        internal void Close(){IsOpen=false;}
    }
    internal sealed class TestFinishedWorker
    { internal bool Finished;internal void Refuse(string reason){Finished=true;} }
    internal interface IRemoved { }
    internal static class PrecisionPlacementSession {
        internal static bool HasUsableHammer(Player player)=>player&&BlueprintConstructionProjectRuntime.Hammer;
        internal static bool SupportsProjectPart(Piece piece)=>piece&&!piece.GetComponent<ItemStand>()&&!piece.GetComponent<Container>();
        internal static bool SupportsStorageProjectPart(Piece piece)=>piece&&BlueprintStorageCapture.PrefabGrid(piece.gameObject).HasValue;
        internal static bool SupportsMountProjectPart(Piece piece,BlueprintConstructionProjectStore.Source source)=>piece&&piece.GetComponent<ItemStand>();
    }
    internal static partial class BuildWorksPlugin { internal static bool CanOpenConstructionProject=true; internal static BlueprintConstructionProjectRuntime ConstructionProjects; }
    internal sealed partial class ZNet { internal bool LocalPlayerIsAdminOrHost()=>BlueprintConstructionProjectRuntime.Admin; }
    internal sealed partial class BlueprintConstructionProjectRuntime
    {
        internal static bool Hammer=true;
        private bool disposed=false,failed=false;
        private BlueprintConstructionProjectStore store;
        private ZNet network;
        private long world;
        private Guid queued=Guid.Empty;
        private readonly Dictionary<Guid,BlueprintConstructionProjectBoard> boards=new Dictionary<Guid,BlueprintConstructionProjectBoard>();
        private readonly HashSet<Guid> cleanupProjects=new HashSet<Guid>(),cancellationProjects=new HashSet<Guid>();
        private readonly BlueprintConstructionProjectView view=new BlueprintConstructionProjectView();
        private readonly Action<Exception> log=null;
        private PartContentsTickProbe execution=null;
        private BlueprintConstructionCancellationRemoval removal=null;
        private BlueprintConstructionContentsReturn contentsReturn;
        private BlueprintConstructionPayoutIssuance payout=null;
        private TestFinishedWorker collection=null,cleanup=null,paidCancellation=null;
        internal string Failure {get;private set;}
        internal BlueprintConstructionProjectBoard OpenBoard {get;private set;}
        internal BlueprintConstructionProjectRuntime(ConstructionMountExecutionChecks.Rig rig)
        {
            store=rig.Store;network=ZNet.instance;world=store.Plan.World;
            boards.Add(store.Plan.Id,new BlueprintConstructionProjectBoard{Id=store.Plan.Id});
            BlueprintConstructionCancellationRemoval.Started=0;Hammer=true;
            BuildWorksPlugin.ConstructionProjects=this;
        }
        internal BlueprintConstructionProjectBoard Board(Guid id)=>boards.TryGetValue(id,out var board)?board:null;
        private void ClearBoards(){Close();boards.Clear();}
        internal void TestTick(){Time.frameCount++;TickContentsReturn();removal?.Tick();payout?.Tick();}
        internal bool TestBusy=>PayoutBusy;
        internal BlueprintConstructionContentsReturn TestReturn=>contentsReturn;
        internal BlueprintConstructionCancellationRemoval TestRemoval=>removal;
        internal BlueprintConstructionPayoutIssuance TestPayout=>payout;
        internal int BoardCount=>boards.Count;
    }
}
