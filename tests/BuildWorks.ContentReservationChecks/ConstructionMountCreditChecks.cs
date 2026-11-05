using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal static class BuildWorksLocalization { internal static string Text(string key)=>key; }
    internal sealed partial class ItemDrop
    {
        internal float GetHoverOffset() => 0;
        internal T[] GetComponentsInChildren<T>() where T:class => gameObject.GetComponentsInChildren<T>();
    }
    internal sealed partial class Inventory
    {
        internal int CreditCalls;
        internal string CreditMode;
        internal Action BeforeCellCredit;
        internal bool AddItem(ItemDrop.ItemData item,int amount,int x,int y,bool effect)
        {
            CreditCalls++;BeforeCellCredit?.Invoke();
            if(effect||amount!=1||GetItemAt(x,y)!=null)throw new InvalidOperationException("wrong explicit native credit arguments");
            if(CreditMode=="throw-before")throw new InvalidOperationException("before insertion");
            if(CreditMode=="none")return false;
            if(CreditMode=="merge"){Items.Single(old=>old.m_dropPrefab.name==item.m_dropPrefab.name).m_stack++;return true;}
            Items.Add(item);AfterCredit?.Invoke();
            if(CreditMode=="throw-after")throw new InvalidOperationException("after insertion");
            return true;
        }
    }
    internal sealed partial class BlueprintConstructionPartJournal
    {
        internal const int MountCreditIntent=1,MountProfileCredited=2;
        internal sealed class MountReturnCredit
        {
            internal int Phase,X,Y; internal bool LoadedSource;
            internal string BeforeInfo,BeforeBytes,AfterInfo,AfterBytes,ItemBytes,BeforeProfileHash,SavedProfileHash;
        }
        internal bool CreditBeginFails,CreditConfirmFails;
        internal MountRecord BeginMountReturnCredit(Guid project,Guid part,Guid operation,BlueprintForeignPartData before,string item,int x,int y,string profile,bool loadedSource=false)
        {
            if(Shared.Disk!=null)return Shared.Refresh(Shared.Disk.BeginMountReturnCredit(project,part,operation,before,item,x,y,profile,loadedSource));
            if(Shared.CreditBeginFails)throw new InvalidOperationException("credit intent write failed");
            var row=Shared.Mount;
            if(row.Return.Phase!=MountReturnSaved||row.Return.Credit!=null)throw new InvalidOperationException("credit replay");
            var after=MountCreditInventory(row,before,item,x,y,loadedSource);
            row.Return.Credit=new MountReturnCredit {Phase=MountCreditIntent,X=x,Y=y,BeforeInfo=before.Info,BeforeBytes=before.Zdo,
                AfterInfo=after.Info,AfterBytes=after.Zdo,ItemBytes=item,BeforeProfileHash=profile,LoadedSource=loadedSource};
            Shared.Events.Add("credit-intent");return row;
        }
        internal void ConfirmMountReturnCredit(Guid project,Guid part,Guid operation,NativePlayerProfileReadback.Evidence proof)
        {
            if(Shared.Disk!=null && Shared.CreditConfirmFails)throw new InvalidOperationException("simulated durable credit intent crash");
            if(Shared.Disk!=null){Shared.Refresh(Shared.Disk.ConfirmMountReturnCredit(project,part,operation,proof));return;}
            if(Shared.CreditConfirmFails)throw new InvalidOperationException("credit confirm write failed");
            Shared.Mount.Return.Credit.Phase=MountProfileCredited;Shared.Mount.Return.Credit.SavedProfileHash=proof.ProfileHash;
            Shared.Events.Add("credit-saved");
        }
    }
    internal static partial class ConstructionMountDetachChecks
    {
        private static void CreditChecks()
        {
            foreach(string mode in new[]{"disk-only","loaded-only","ok","near-cent","existing-stack","merge","throw-after","none","throw-before","changed-source","intent-fail","confirm-fail","profile-fail","replace-then-throw","occupied","owner","unpicked"})
            using(var r=new Rig(mode=="near-cent"?(float?)94.12f:null)){
                if(mode=="existing-stack"||mode=="merge"){
                    var existing=r.Forward.Sword.Clone();existing.m_stack=1;existing.m_gridPos=new Vector2i(2,0);existing.m_equipped=false;r.Player.Inventory.Items.Add(existing);
                }
                foreach(var item in r.Player.Inventory.Items)item.m_pickedUp=true;
                Check(BlueprintConstructionMountDetachExecution.TryBegin(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out r.Actor,out string setupError),"credit setup refused:"+mode+":"+setupError);r.Actor.Tick();
                Check(!r.Actor.TryCredit(out _)&&r.Player.Inventory.CreditCalls==0,"unsaved actual source credited");
                ZNet.instance.SaveAction=()=>{r.Forward.WorldSaves++;SaveSystem.Generation++;WriteReturnWorld(r,"ok");};
                Check(r.Actor.TryBeginPersistence(out var pair,out string error),error??"credit pair refused");
                var deadline=DateTime.UtcNow.AddSeconds(5);
                while(pair.State!=BlueprintConstructionProjectPersistence.Result.Verified&&pair.State!=BlueprintConstructionProjectPersistence.Result.Refused&&DateTime.UtcNow<deadline){pair.Tick();Thread.Sleep(1);}
                Check(pair.State==BlueprintConstructionProjectPersistence.Result.Verified,"credit pair fixture failed");
                var source=r.Stand.Returned.Single();source.gameObject.Colliders=new[]{new Collider{Owner=source.gameObject}};
                byte[] raw=BlueprintConstructionPayoutSeal.Package(source.m_itemData);
                r.Player.Inventory.BeforeCellCredit=()=>{
                    Check(r.Journal.Mount.Return.Credit?.Phase==1,"native insertion preceded durable intent");
                    try{Game.instance.SavePlayerProfile(false,false);throw new InvalidOperationException("unprotected callback save");}
                    catch(InvalidOperationException failure){Check(failure.Message!="unprotected callback save","callback save was not blocked");}
                };
                if(mode=="none"||mode=="throw-before"||mode=="throw-after"||mode=="merge")r.Player.Inventory.CreditMode=mode;
                if(mode=="changed-source")r.Player.Inventory.AfterCredit=()=>source.m_itemData.Custom="changed";
                if(mode=="intent-fail")r.Journal.CreditBeginFails=true;
                if(mode=="confirm-fail")r.Journal.CreditConfirmFails=true;
                if(mode=="occupied"){r.Player.Inventory.Width=1;r.Player.Inventory.Height=1;}
                if(mode=="owner")source.GetComponent<ZNetView>().Owner=false;
                if(mode=="unpicked")r.Player.Inventory.Items[0].m_pickedUp=false;
                string receiptKey=BlueprintConstructionPartJournal.MountCreditKey(r.Journal.Mount.Return.Operation);
                if(mode=="disk-only"||mode=="loaded-only"){
                    r.Player.m_customData[receiptKey]="previous-credit";
                    if(mode=="disk-only"){
                        File.WriteAllBytes(r.Forward.Profile.Path,ProfileChecks.FileFor(r.Player));r.Player.m_customData.Remove(receiptKey);
                    }
                }
                byte[] diskBefore=File.ReadAllBytes(r.Forward.Profile.Path);
                int saves=0;var save=Game.instance.SaveAction;
                Game.instance.SaveAction=()=>{saves++;if(mode=="profile-fail"&&saves>1)throw new InvalidOperationException("profile write refused");save();if(mode=="replace-then-throw"&&saves>1)throw new InvalidOperationException("after replacement");};
                bool result=r.Actor.TryCredit(out error);
                bool expected=mode=="ok"||mode=="near-cent"||mode=="existing-stack"||mode=="throw-after"||mode=="replace-then-throw";
                Check(result==expected,"unexpected credit outcome:"+mode+":"+error);
                bool inserted=mode!="disk-only"&&mode!="loaded-only"&&mode!="merge"&&mode!="none"&&mode!="throw-before"&&mode!="intent-fail"&&mode!="occupied"&&mode!="owner"&&mode!="unpicked";
                Check(r.Player.Inventory.CreditCalls==(mode=="disk-only"||mode=="loaded-only"||mode=="intent-fail"||mode=="occupied"||mode=="owner"||mode=="unpicked"?0:1),"wrong native insertion count:"+mode);
                if(mode=="disk-only"||mode=="loaded-only")Check(saves==0&&diskBefore.SequenceEqual(File.ReadAllBytes(r.Forward.Profile.Path)),"contradictory receipt overwritten or saved");
                int oldCount=mode=="existing-stack"||mode=="merge"?1:0;
                Check(r.Player.Inventory.Items.Count(i=>i.m_dropPrefab.name=="SwordIron")== oldCount+(inserted?1:0),"credit item count differs:"+mode);
                Check(BlueprintConstructionPayoutSeal.IsHeld(source),"credit released the world source:"+mode);
                if(expected){
                    var credit=r.Journal.Mount.Return.Credit;var copy=r.Player.Inventory.GetItemAt(credit.X,credit.Y);
                    Check(credit.Phase==2&&!ReferenceEquals(copy,source.m_itemData)&&!ReferenceEquals(copy.m_customData,source.m_itemData.m_customData),"source clone/credit confirmation differs");
                    Check(raw.SequenceEqual(BlueprintConstructionPayoutSeal.Package(source.m_itemData))&&copy.Custom==source.m_itemData.Custom&&copy.m_quality==source.m_itemData.m_quality,
                        "credit altered source or properties");
                    Check(copy.m_durability==source.m_itemData.m_durability,"credit reconstructed and rounded the live source float");
                    if(mode=="near-cent")Check(BitConverter.ToInt32(raw,1)==9411&&BitConverter.ToInt32(Convert.FromBase64String(credit.ItemBytes),1)==9411,
                        "native load/save model lost another cent during source-derived clone");
                    Check(BlueprintStoragePlayerProfile.TryReadSavedCredit(r.Player,new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                        credit.AfterInfo,credit.AfterBytes,null),()=>true,out var proof,out _)&&proof.CustomData[BlueprintConstructionPartJournal.MountCreditKey(r.Journal.Mount.Return.Operation)]==BlueprintConstructionPartJournal.MountCreditValue(r.Journal.Mount),
                        "saved credited inventory/receipt pair absent");
                }
                int calls=r.Player.Inventory.CreditCalls;
                if(r.Journal.Mount.Return.Credit!=null)Check(!r.Actor.TryCredit(out _)&&r.Player.Inventory.CreditCalls==calls,"earlier credit attempt inserted again");
            }
        }
    }
}
