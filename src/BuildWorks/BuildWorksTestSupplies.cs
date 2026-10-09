#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Isolated QA preparation only; never a survival placement or administrator restore path.
    internal static class BuildWorksTestSupplies
    {
        internal const string SupportedSnapFixture = "16-supported-snap.blueprint|0581781B12DE25323C3F85E7F9747B9CD2AE9D5F6C5482CC65A4998B0CB0D427";
        internal const string SupportedSnapAFixture = "17-supported-snap-a.blueprint|6C2C666C6934638732E8270533F3B9EBB1BDA63180B3E503B1834D6BB21A226C";
        internal const string SupportedSnapBFixture = "18-supported-snap-b.blueprint|C5C28366DC4A367C15B53204F5721C3A4D61AE7921D3B9138BBC26DEDE8222EF";
        private static readonly string[] Fixtures =
        {
            "01-walls.blueprint|E65CB24CB573FE4C8E320918049EB0A1EBC05AF66A8D944E60AC618A9731AFBB",
            "02-sign.blueprint|848D85C55E522566BE9531BCEC587C101AFC52A27DCBFA942B563C5CD62C23EC",
            "03-opaque.blueprint|7AD4B8CD1E92AEC9C97E672927B8FF67D68CEAF9063C943AD35EFCF4491BD570",
            "04-snap.blueprint|82064C4E06F80F2B2B6C2229330454AF3B524F604D12F1015999ADF984D553A0",
            "06-lamp.blueprint|093292811B0858CC4C6FFF1FEB5E9C32B4FA0D52501CDFB9676E94136172F4D7",
            "hs_ashlands_madzobuild_coffeetable.blueprint|0DE7C847036390D74CEA27242FB47B9375668B1775BA9A7228EADDF7ECC4DD53",
            "hs_blackforest_hut.blueprint|492E78E16FEEFCCF3B58C1503342732AEFC68A9F9B891401EB3B9BC06E588F49"
        };

        internal static int[] StackBatches(int amount, int maximum)
        {
            if (amount < 1 || amount > 10000 || maximum < 1 || maximum > 10000)
                throw new InvalidDataException("Invalid or excessive QA supply stack.");
            var batches = new int[(amount + maximum - 1) / maximum];
            for (int index = 0; index < batches.Length; ++index)
            { batches[index] = Math.Min(amount, maximum); amount -= batches[index]; }
            return batches;
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }

        internal static Dictionary<GameObject, int> Plan(out List<Piece> pieces, bool supportedSnap = false)
        {
            if (!ZNetScene.instance || !ObjectDB.instance || !MessageHud.instance)
                throw new InvalidOperationException("QA supplies require loaded prefab/item databases and MessageHud.");
            string directory = CompositeBlueprintStore.ValidateExchangeDirectory(BuildWorksTestIsolation.FixtureDirectory);
            pieces = new List<Piece>();
            int resourceBudget = 0;
            foreach (string manifest in supportedSnap ? new[] { SupportedSnapFixture, SupportedSnapAFixture, SupportedSnapBFixture } : Fixtures)
            {
                string[] entry = manifest.Split('|');
                string path = Path.Combine(directory, entry[0]);
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 256 * 1024 || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Missing or invalid QA fixture: " + entry[0]);
                var review = CompositeBlueprintStore.ReviewImportFile(path, name => name.GetStableHashCode(), 0);
                if (Hash(review.Bytes) != entry[1] || review.Documents.Count != 1 || review.Documents[0].importError != null)
                    throw new InvalidDataException("QA fixture changed or failed import: " + entry[0]);
                foreach (var part in review.Documents[0].parts)
                {
                    GameObject prefab = ZNetScene.instance.GetPrefab(part.prefabName);
                    Piece piece = prefab ? prefab.GetComponent<Piece>() : null;
                    if (!piece || ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))
                        throw new InvalidDataException("Missing or free-cost QA piece: " + part.prefabName);
                    pieces.Add(piece);
                    if (pieces.Count > 512) throw new InvalidDataException("Unexpected QA fixture size.");
                    foreach (var requirement in piece.m_resources ?? Array.Empty<Piece.Requirement>())
                    {
                        if (requirement == null || !requirement.m_resItem || requirement.GetAmount(1) < 0 || requirement.GetAmount(1) > 10000)
                            throw new InvalidDataException("Invalid native QA resource requirement.");
                        resourceBudget = checked(resourceBudget + requirement.GetAmount(1));
                        if (resourceBudget > 10000) throw new InvalidDataException("Excessive native QA resource plan.");
                    }
                }
            }
            var result = new Dictionary<GameObject, int>();
            // Each input is bounded, so the existing catalogue accumulator cannot overflow here.
            foreach (var resource in HammerBlueprintPieceRegistry.ResourceCosts(pieces))
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(resource.Item.gameObject.name);
                if (!prefab || prefab.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_name != resource.Item.m_itemData.m_shared.m_name ||
                    resource.Amount < 1 || resource.Amount > 10000)
                    throw new InvalidDataException("Invalid native QA resource prefab/amount.");
                result.Add(prefab, resource.Amount);
            }
            if (!supportedSnap)
            {
                GameObject hammer = ObjectDB.instance.GetItemPrefab("Hammer");
                if (!hammer || result.ContainsKey(hammer)) throw new InvalidDataException("QA Hammer is missing or conflicts with resources.");
                result.Add(hammer, 1);
            }
            return result;
        }

        private static int Count(Inventory inventory, GameObject prefab)
        {
            int count = 0;
            foreach (var item in inventory.GetAllItems().ToArray())
                if (item.m_dropPrefab == prefab) count = checked(count + item.m_stack);
            return count;
        }

        internal static bool Prepare(bool supportedSnap = false)
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            Dictionary<GameObject, int> plan = Plan(out _, supportedSnap);
            string description = string.Join("\n", supportedSnap ? new[] { SupportedSnapFixture, SupportedSnapAFixture, SupportedSnapBFixture } : Fixtures) + "\n" + string.Join("\n", plan.OrderBy(pair => pair.Key.name, StringComparer.Ordinal)
                .Select(pair => pair.Key.name + "=" + pair.Value.ToString(CultureInfo.InvariantCulture)));
            string stamp = BuildWorksTestIsolation.SupplyStamp(description);
            string receipt = supportedSnap ? BuildWorksTestIsolation.SupportedSnapSupplyReceiptPath : BuildWorksTestIsolation.SupplyReceiptPath;
            if (BuildWorksTestIsolation.OnceComplete(receipt, stamp)) return false;
            Inventory inventory = player.GetInventory();
            int slots = 0;
            double weight = inventory.GetTotalWeight();
            var sharedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in plan)
            {
                ItemDrop.ItemData item = pair.Key.GetComponent<ItemDrop>().m_itemData;
                int[] batches = StackBatches(pair.Value, item.m_shared.m_maxStackSize);
                slots = checked(slots + batches.Length);
                weight += (double)item.m_shared.m_weight * pair.Value;
                if (item.m_quality != 1 || item.m_shared.m_weight < 0 || !sharedNames.Add(item.m_shared.m_name) ||
                    inventory.GetAllItems().Any(existing => existing.m_shared.m_name == item.m_shared.m_name && existing.m_dropPrefab != pair.Key))
                    throw new InvalidDataException("Ambiguous native stack identity in QA supplies.");
            }
            // Conservative empty-slot budget ignores merges; neither encumbrance nor capacity is bypassed.
            float capacity = player.GetMaxCarryWeight();
            if (slots > inventory.GetEmptySlots() || weight < 0 || double.IsNaN(weight) || double.IsInfinity(weight) ||
                capacity <= 0 || float.IsNaN(capacity) || float.IsInfinity(capacity) || weight > capacity)
                throw new InvalidOperationException("QA supply plan exceeds the fresh backpack; a prepared supply chest is required.");
            BuildWorksTestIsolation.RequireLoadedSession();
            if (!BuildWorksTestIsolation.BeginOnce(receipt, stamp)) return false;
            foreach (var pair in plan.OrderBy(pair => pair.Key.name == "Hammer" ? 0 : 1).ThenBy(pair => pair.Key.name, StringComparer.Ordinal))
            {
                foreach (int batch in StackBatches(pair.Value, pair.Key.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize))
                {
                    if (BuildWorksTestIsolation.RequireLoadedSession() != player)
                        throw new InvalidOperationException("QA player changed during preparation.");
                    int before = Count(inventory, pair.Key);
                    bool added = inventory.AddItem(pair.Key, batch);
                    if (!added || Count(inventory, pair.Key) - before != batch)
                        throw new InvalidOperationException("Partial QA supply grant; receipt stays incomplete and will not retry.");
                }
                player.AddKnownItem(inventory.GetAllItems().First(item => item.m_dropPrefab == pair.Key));
            }
            float actualWeight = inventory.GetTotalWeight();
            if (actualWeight < 0 || float.IsNaN(actualWeight) || float.IsInfinity(actualWeight) || actualWeight > player.GetMaxCarryWeight())
                throw new InvalidOperationException("Native QA supply weight changed; refusing completion.");
            BuildWorksTestIsolation.RequireLoadedSession();
            PlayerProfile profile = Game.instance.GetPlayerProfile();
            profile.SavePlayerData(player);
            if (!profile.Save()) throw new IOException("Native QA character save failed; no automatic reissue.");
            BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksTestIsolation.CompleteOnce(receipt, stamp);
            return true;
        }
    }
}
#endif
