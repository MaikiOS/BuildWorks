using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using OstrixMods.BuildWorks.Geometry;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    // Shared paid-target identity/permission/pose guards for attachment and detachment.
    internal abstract partial class BlueprintConstructionMountContext
    {
        protected readonly Plans store;
        protected readonly Plans.Record plan;
        protected readonly Plans.Part part;
        protected readonly Journal journal;
        protected readonly Journal.Record parent;
        protected readonly string parentFingerprint;
        protected readonly Player player;
        protected readonly Inventory inventory;
        protected readonly GameObject target, prefab;
        protected readonly Piece piece;
        protected readonly ItemStand stand;
        protected readonly ZNetView view;
        protected readonly ZDO zdo;
        protected readonly ZDOID nativeId;
        private readonly Guid loadedWorldEpoch;
        protected readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly ZNet network;
        private readonly World world;
        private readonly Game game;
        private readonly PlayerProfile profile;
        private readonly string profilePath, profileName, profileFilename;

        protected BlueprintConstructionMountContext(Plans store, Guid project, Guid partId)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            player = Player.m_localPlayer; inventory = player ? player.GetInventory() : null;
            plan = store.Load(project); part = plan.Parts.First(p => p.Id == partId); journal = new Journal(store);
            parent = journal.RequireSavedPayment(project, partId, player ? player.GetPlayerID() : 0);
            parentFingerprint = Journal.Fingerprint(parent);
            var source = plan.Blueprint.parts[part.SourceIndex];
            var demand = source.foreignData?.ToGeometry();
            if (demand?.Format != BlueprintForeignPartData.NativeItemStandFormat || demand.ContentsCleared)
                throw new InvalidOperationException("A paid stand with a nonempty item demand is required.");
            prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(source.prefabName) : null;
            target = BlueprintConstructionPartExecution.FindMarked(parent.Operation, source.prefabName);
            piece = target ? target.GetComponent<Piece>() : null; stand = target ? target.GetComponent<ItemStand>() : null;
            view = target ? target.GetComponent<ZNetView>() : null; zdo = view ? view.GetZDO() : null;
            nativeId = zdo?.m_uid ?? ZDOID.None;
            network = ZNet.instance; world = network ? network.GetWorld() : null;
            game = Game.instance; profile = game ? game.GetPlayerProfile() : null;
            profilePath = profile?.GetPath(); profileName = profile?.GetName(); profileFilename = profile?.GetFilename();
            loadedWorldEpoch = CurrentReturnEpoch();
            if (!Context()) throw new InvalidOperationException("The current paid stand, payer, Local profile or world context differs.");
        }
        protected bool Context()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread || loadedWorldEpoch != CurrentReturnEpoch() || !Player.m_localPlayerExists || !player || player != Player.m_localPlayer || player.IsDead() ||
                !ReferenceEquals(player.GetInventory(), inventory) || player.GetPlayerID() != parent.Player ||
                !BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement || !network || !ReferenceEquals(ZNet.instance, network) ||
                !ReferenceEquals(network.GetWorld(), world) || world == null || world.m_uid != plan.World ||
                !game || !ReferenceEquals(Game.instance, game) || !ReferenceEquals(game.GetPlayerProfile(), profile) ||
                profile == null || profile.m_fileSource != FileHelpers.FileSource.Local || profile.GetPlayerID() != parent.Player ||
                profile.GetPath() != profilePath || profile.GetName() != profileName || profile.GetFilename() != profileFilename ||
                !prefab || !piece || !stand || target.GetComponent<ArmorStand>() || target.GetComponent<Container>() ||
                !BlueprintItemStandPlacement.OwnsView(target, view) || !ReferenceEquals(view.GetZDO(), zdo) ||
                zdo.m_uid != nativeId || nativeId.IsNone() || zdo.GetPrefab() != parent.PrefabHash ||
                piece.GetCreator() != parent.Creator || zdo.GetString(BlueprintConstructionPartExecution.MarkerKey, "") != parent.Operation.ToString("N") ||
                zdo.GetByteArray(BlueprintConstructionPartExecution.ReceiptKey)?.SequenceEqual(parent.Receipt) != true ||
                !PrivateArea.CheckAccess(target.transform.position, 0f, false, false)) return false;
            var currentPlan = store.Load(plan.Id);
            if (currentPlan.Cancelled || currentPlan.Revision != plan.Revision ||
                Journal.Fingerprint(journal.RequireSavedPayment(plan.Id, part.Id, parent.Player)) != parentFingerprint) return false;
            float[] pose = part.WorldPose.Values;
            return Vector3.Distance(target.transform.position, new Vector3(pose[0], pose[1], pose[2])) <= .001f &&
                Quaternion.Angle(target.transform.rotation, new Quaternion(pose[3], pose[4], pose[5], pose[6])) <= .01f &&
                Vector3.Distance(target.transform.localScale, Vector3.Scale(prefab.transform.localScale, new Vector3(pose[7], pose[8], pose[9]))) <= .001f;
        }

    }
}