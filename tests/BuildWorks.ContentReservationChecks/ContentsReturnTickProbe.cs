using System;
namespace ReservationChecks
{
    // Legacy container scenarios now invoke the complete production coordinator.
    internal sealed class ContentsReturnTickProbe
    {
        private readonly BlueprintConstructionContentsReturn actual;
        internal bool Finished => actual.Finished;
        internal string Status => actual.Status;
        internal string Reason => actual.Reason;
        internal ContentsReturnTickProbe(Action current, int count = 1) { actual = new BlueprintConstructionContentsReturn(current, count); }
        internal void Tick() => actual.Tick();
        internal void Refuse(string reason) => actual.Refuse(reason);
    }
    internal sealed partial class BlueprintConstructionContentsReturn
    {
        internal BlueprintConstructionContentsReturn(Action current, int count)
        {
            plans = new BlueprintConstructionProjectStore { LegacyReturnProducer=true }; Project = plans.Plan.Id; Revision = plans.Plan.Revision; Operation = Guid.NewGuid();
            context = () => { current(); return true; }; player = Player.m_localPlayer; inventory = player.GetInventory(); payer = player.GetPlayerID();
            parts = new Guid[count];
            for(int i=0; i<count; i++) {
                var part = new BlueprintConstructionProjectStore.Part(); parts[i]=part.Id; plans.Plan.Parts.Add(part);
            }
            plans.Plan.Blueprint.parts.Add(new BlueprintConstructionProjectStore.Source());
            ZNetScene.instance = new ZNetScene { Prefab = new GameObject { name="itemstand",Container=new Container(4) } };
            new BlueprintConstructionPartJournal(new BlueprintConstructionPartJournal.Record { Project=Project, Part=count==0?Guid.Empty:parts[0] });
            Advance();
        }
    }
    internal sealed partial class BlueprintStorageTransfer
    {
        internal static Func<BlueprintStorageTransfer> ReturnProducer;
        internal static bool TryBeginProjectReturn(BlueprintConstructionProjectStore plans, Guid project, Guid part, Player player,
            out BlueprintStorageTransfer transfer, out string reason) => plans.LegacyReturnProducer
                ? TryBeginProjectReturn((object)plans,project,part,player,out transfer,out reason)
                : TryBeginProjectReturnActual(plans,project,part,player,out transfer,out reason);
        internal static bool TryBeginProjectReturn(object plans, Guid project, Guid part, Player player,
            out BlueprintStorageTransfer transfer, out string reason)
        {
            transfer = null; reason = null;
            try { transfer = ReturnProducer(); return true; }
            catch (Exception error) { reason = error.Message; return false; }
        }
    }
}
