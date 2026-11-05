#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    internal static partial class BuildWorksTestConstructionProject
    {
        private static Guid ContentsProjectId() => Guid.ParseExact(BuildWorksTestIsolation.SupplyStamp(
            "paid-contents-project-v1\n" + BuildWorksTestSupplies.ContentsPopulatedFixture + "\n" + ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture)).Substring(0, 32), "N");

        private static CompositeBlueprintStore.Blueprint ReadContentsBlueprint(Guid id)
        {
            string[] entry = BuildWorksTestSupplies.ContentsPopulatedFixture.Split('|');
            string directory = CompositeBlueprintStore.ValidateExchangeDirectory(BuildWorksTestIsolation.FixtureDirectory);
            var review = CompositeBlueprintStore.ReviewImportFile(Path.Combine(directory, entry[0]), name => name.GetStableHashCode(), 0);
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(review.Bytes)).Replace("-", "") != entry[1])
                    throw new InvalidDataException("Pinned populated fixture changed.");
            return ContentsBlueprint(review, id);
        }

        internal static string CreatePaidContentsProject(Action requireIdle)
        {
            requireIdle(); var player = BuildWorksTestIsolation.RequireLoadedSession(); var network = ZNet.instance;
            if (!BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement || network.IsSaving() ||
                !BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var before))
                throw new InvalidOperationException("Paid contents QA needs idle private Local inventory.");
            BlueprintConstructionPartExecution.RequireStoragePreflight(network.GetWorldUID());
            Guid id = ContentsProjectId();
            try { Runtime.Read(id); throw new InvalidOperationException("This contents project already exists; adopt it instead of creating another."); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var blueprint = ReadContentsBlueprint(id);
            var prefab = ZNetScene.instance.GetPrefab("piece_chest_wood"); var piece = prefab ? prefab.GetComponent<Piece>() : null;
            if (!piece || !PrecisionPlacementSession.SupportsStorageProjectPart(piece) ||
                !player.HaveRequirements(piece, Player.RequirementMode.CanBuild) || piece.m_resources == null || piece.m_resources.Length == 0 ||
                piece.m_resources.Any(row => row?.m_resItem == null || row.m_resItem.gameObject.name != "Wood" || row.m_upgraderResource || row.GetAmount(0) <= 0))
                throw new InvalidOperationException("Contents QA requires the supported native chest and its real Wood recipe/station.");
            int cost = piece.m_resources.Sum(row => checked(row.GetAmount(0)));
            var supplies = new List<BlueprintItemStandPlacement.Supply>();
            if (cost < 1 || cost > 100 || !BlueprintItemStandPlacement.SelectSupplies(player,
                    new[] { blueprint.parts[0].foreignData.ToGeometry() }, supplies, piece) ||
                !BlueprintItemStandPlacement.SuppliesAvailable(player, supplies, piece))
                throw new InvalidOperationException("Own current supplies must cover the recipe and three contents Wood together; nothing is granted.");
            Vector3 site = BuildWorksTestStations.FindStorageSite(player, piece);
            if (!PrivateArea.CheckAccess(site, 0f, false, false) || piece.m_craftingStation &&
                !CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, site))
                throw new InvalidOperationException("Contents project site lacks native access/station range.");
            requireIdle();
            if (BuildWorksTestIsolation.RequireLoadedSession() != player || ZNet.instance != network || network.IsSaving() ||
                !BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var current) || current.Info != before.Info || current.Zdo != before.Zdo)
                throw new InvalidOperationException("Contents project context or inventory changed before its unpaid plan write.");
            var record = Runtime.Create(id, blueprint, Pose(site, Quaternion.identity), new[] { Pose(site, Quaternion.identity) });
            RememberContentsProject(record);
            return JsonUtility.ToJson(new ContentsProjectReceipt { project = id.ToString("N"), part = record.Parts[0].Id.ToString("N"),
                stage = "unpaid-plan-created", shellCost = cost, contentsWood = 3, woodBefore = player.GetInventory().CountItems("$item_wood", -1, true), site = site });
        }

        internal static string AdoptPaidContentsProject(Action requireIdle)
        {
            requireIdle(); BuildWorksTestIsolation.RequireLoadedSession();
            Guid id = ContentsProjectId(); var record = Runtime.Read(id);
            if (record.Parts.Count != 1 || !record.BlueprintBytes.SequenceEqual(BlueprintExchangeFormat.Encode(ReadContentsBlueprint(id))))
                throw new InvalidOperationException("Saved project differs from the pinned single-chest fixture.");
            RememberContentsProject(record); return Inspect();
        }

        private static void RememberContentsProject(Store.Record record)
        { lastId = record.Id; cancelOperation = Guid.NewGuid(); snapshot = record.BlueprintBytes; partIds = record.Parts.Select(part => part.Id).ToArray(); poses = record.Parts.Select(part => part.WorldPose).ToArray(); }

        [Serializable] private sealed class ContentsProjectReceipt
        { public string project, part, stage; public int shellCost, contentsWood, woodBefore; public Vector3 site; }
    }
}
#endif
