using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal static class PaidSupplySelectionChecks
    {
        private static int checks;
        private static void Require(bool value, string message)
        { ++checks; if (!value) throw new InvalidOperationException(message); }
        private static Player Wood(params int[] amounts)
        {
            var player = new Player();
            for (int i=0; i<amounts.Length; ++i)
            {
                var item=Program.Item("Wood",amounts[i],"provided-"+i);
                item.m_gridPos=new Vector2i(i%8,i/8);
                player.Inventory.Items.Add(item);
            }
            return player;
        }
        private static BlueprintForeignPartData Demand(int amount=3, int cells=5) =>
            Program.Container(cells,Program.Item("Wood",amount,"archived"));
        private static List<BlueprintItemStandPlacement.Supply> Plan() => new List<BlueprintItemStandPlacement.Supply>();
        private static bool Select(Player player, Piece cost, List<BlueprintItemStandPlacement.Supply> plan,
            params BlueprintForeignPartData[] demand) => BlueprintItemStandPlacement.SelectSupplies(player,demand,plan,cost);

        internal static void Run()
        {
            var originalZone=ZoneSystem.instance; int originalLevel=Game.m_worldLevel;
            var originalKeys=originalZone.Keys.ToArray();
            try
            {
                originalZone.Keys.Clear(); Game.m_worldLevel=0;
                var player=Wood(new[] {2,37,4}.Concat(Enumerable.Repeat(2,12)).ToArray());
                var own=player.Inventory.Items.ToArray(); var counts=own.Select(x=>x.m_stack).ToArray();
                var properties=own.Select(x=>BlueprintItemStandPlacement.SnapshotItem(x)).ToArray();
                var demand=Demand(); string archive=demand.Info+"\n"+demand.Zdo;
                var cost=Program.Cost(own[0],10); var plan=Plan();
                Require(BlueprintItemStandPlacement.SelectSupplies(player,new[] {demand},plan) &&
                    !BlueprintItemStandPlacement.SuppliesAvailable(player,plan,cost),"Legacy contents-only call no longer reproduces351 prefix refusal");
                var prior=plan[0];
                Require(Select(player,cost,plan,demand) && BlueprintItemStandPlacement.SuppliesAvailable(player,plan,cost),
                    "REPRO351:67 own Wood cannot cover shell10 plus contents3 after payment-aware selection");
                Require(plan[0].Items.Count==1 && ReferenceEquals(plan[0].Items[0].Item,own[1]) && plan[0].Items[0].Amount==3,
                    "Payment seed leaked into transfer entries or consumed first stack was reserved");
                Require(prior.Items.Count==2 && ReferenceEquals(prior.Items[0].Item,own[0]) && prior.Items[0].Amount==2,
                    "Planner mutated an already-held immutable Supply");
                Require(own.SequenceEqual(player.Inventory.Items) && counts.SequenceEqual(own.Select(x=>x.m_stack)) &&
                    properties.SequenceEqual(own.Select(x=>BlueprintItemStandPlacement.SnapshotItem(x))) && archive==demand.Info+"\n"+demand.Zdo,
                    "Planning changed inventory order, counts, properties or archived demand");
                player.Inventory.Items.Remove(own[0]); own[1].m_stack-=8;
                Require(BlueprintItemStandPlacement.SuppliesAvailable(player,plan),"Held contents invalidated by legitimate native debit");
                own[1].Custom="changed";
                Require(!BlueprintItemStandPlacement.SuppliesAvailable(player,plan),"Snapshot change silently rebound after payment");

                foreach (var amounts in new[] {new[] {2,11},new[] {2,10},new[] {13},new[] {12}})
                {
                    player=Wood(amounts); plan=Plan(); cost=Program.Cost(player.Inventory.Items[0],10);
                    bool enough=amounts.Sum()>=13;
                    Require(Select(player,cost,plan,Demand(3,1))==enough,"Exact budget/one-cell boundary wrong: "+string.Join(",",amounts));
                    if (enough) Require(plan[0].Items.Count==1 && plan[0].Items[0].Amount==3 &&
                        BlueprintItemStandPlacement.SuppliesAvailable(player,plan,cost),"One-cell reservation counted native debit as contents");
                }
                player=Wood(2,30); plan=Plan(); cost=Program.Cost(player.Inventory.Items[0],10);
                var mount=new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,"Wood:0:1:0",null,null);
                Require(Select(player,cost,plan,Demand(),mount,Demand()) && plan.Sum(x=>x.Items.Sum(y=>y.Amount))==7 &&
                    plan.All(x=>x.Items.All(y=>ReferenceEquals(y.Item,player.Inventory.Items[1]))) &&
                    BlueprintItemStandPlacement.SuppliesAvailable(player,plan,cost),"Payment applied twice or shared contents reused across demands");
                player=Wood(2,20); plan=Plan(); cost=Program.Cost(player.Inventory.Items[0],6);
                cost.m_resources=cost.m_resources.Concat(Program.Cost(player.Inventory.Items[0],4).m_resources).ToArray();
                Require(Select(player,cost,plan,Demand()) && plan[0].Items.Single().Amount==3 &&
                    ReferenceEquals(plan[0].Items[0].Item,player.Inventory.Items[1]),"Repeated requirements reset the native debit budget");

                player=Wood(2,8,3); var alias=player.Inventory.Items[1]; alias.m_dropPrefab.name="AliasWood";
                plan=Plan(); cost=Program.Cost(player.Inventory.Items[0],10);
                Require(Select(player,cost,plan,Demand()) && ReferenceEquals(plan[0].Items.Single().Item,player.Inventory.Items[2]),
                    "Native shared-name alias was not debited before exact-prefab contents selection");
                player=Wood(2,10); player.Inventory.Items[1].m_worldLevel=1; Game.m_worldLevel=1;
                plan=Plan(); cost=Program.Cost(player.Inventory.Items[0],10);
                Require(Select(player,cost,plan,Demand(2)) && ReferenceEquals(plan[0].Items.Single().Item,player.Inventory.Items[0]),
                    "Native cost world-level filter incorrectly excluded provided contents");
                Game.m_worldLevel=0;

                foreach (bool station in new[] {false,true})
                {
                    player=Wood(2,11); if (station) player.Station=new CraftingStation {m_upgrader=true};
                    cost=Program.Cost(player.Inventory.Items[0],10); cost.m_resources[0].m_upgraderResource=true; plan=Plan();
                    Require(Select(player,cost,plan,Demand()) && plan[0].Items.Count==(station?1:2) &&
                        BlueprintItemStandPlacement.SuppliesAvailable(player,plan,cost),"Station/upgrader filtering diverged between selection and guard");
                }
                originalZone.Keys.Add("build"); player=Wood(2); plan=Plan(); cost=Program.Cost(player.Inventory.Items[0],10);
                Require(!Select(player,cost,plan,Demand()),"Free build key waived provided contents");
                player=Wood(2,1); plan=Plan(); cost=Program.Cost(player.Inventory.Items[0],10);
                Require(Select(player,cost,plan,Demand()) && plan[0].Items.Sum(x=>x.Amount)==3 &&
                    BlueprintItemStandPlacement.SuppliesAvailable(player,plan,cost),"Free key did not waive recipe only");
                player.Inventory.Items[0].Custom="changed";
                Require(!BlueprintItemStandPlacement.SuppliesAvailable(player,plan,cost),"Free key bypassed exact contents snapshots");
                originalZone.Keys.Clear();

                player=Wood(1,2,2); plan=Plan(); cost=Program.Cost(player.Inventory.Items[0],1);
                Require(!Select(player,cost,plan,Demand(3,1)),"Different source properties merged to bypass one-cell capacity");
                player=Wood(2,20); plan=Plan(); cost=Program.Cost(player.Inventory.Items[0],10);
                Require(Select(player,cost,plan,Demand(50),mount) == false && plan[0]==null &&
                    plan[1].Items.Single().Amount==1 && ReferenceEquals(plan[1].Items[0].Item,player.Inventory.Items[1]),
                    "Failed container lost payment seed or leaked partial contents reservations");
                Require(!Select(null,cost,plan,Demand()) && plan.Count==0,"Failed prediction left stale successful supplies");
                Require(Select(player,cost,plan,Demand()),"Valid replan after failure rejected");
                player.Inventory.Items[1].m_stack=1;
                Require(!Select(player,cost,plan,Demand()) && plan.Count==0,"Insufficient native cost fell back to contents-only selection");
                player.Inventory.Items[1].m_stack=20;
                Require(Select(player,cost,plan,Demand()),"Valid replan after recipe shortfall rejected");
                player.Inventory.Items.Add(null);
                Require(!Select(player,cost,plan,Demand()) && plan.Count==0,"Invalid native inventory retained stale supplies");
                player.Inventory.Items.Remove(null);
                Require(Select(player,cost,plan,Demand()),"Valid replan after invalid inventory rejected");
                ZoneSystem.instance=null;
                Require(!Select(player,cost,plan,Demand()) && plan.Count==0,"Missing native context fell back to contents-only selection");
                ZoneSystem.instance=originalZone;
                Require(Select(player,cost,plan,Demand()),"Context restoration fixture failed");
                cost.m_resources[0]=null;
                Require(!Select(player,cost,plan,Demand()) && plan.Count==0,"Malformed recipe retained stale reservations");
                Console.WriteLine("PASS "+checks+" payment-aware selection assertions:actual source with host substitutes;no native/provider/reload proof.");
            }
            finally
            {
                ZoneSystem.instance=originalZone; originalZone.Keys.Clear();
                foreach (string key in originalKeys) originalZone.Keys.Add(key);
                Game.m_worldLevel=originalLevel;
            }
        }
    }
}
