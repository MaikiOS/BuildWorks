using System;
using System.IO;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Ephemeral inventory evidence only. Does not request a save, release holds or authorize a refund.
    internal sealed class BlueprintStoragePlayerProfile
    {
        private readonly Player player;
        private readonly Game game;
        private readonly PlayerProfile profile;
        private readonly Inventory inventory;
        private readonly BlueprintForeignPartData expected;
        private readonly Func<bool> custodyUnchanged;
        private readonly string path, filename, name;
        private readonly long playerId;
        internal readonly Guid Operation;

        private BlueprintStoragePlayerProfile(Player player, Guid operation, Func<bool> custodyUnchanged,
            BlueprintForeignPartData expected)
        {
            this.player = player; Operation = operation; this.custodyUnchanged = custodyUnchanged; this.expected = expected;
            game = Game.instance; profile = game.GetPlayerProfile(); inventory = player.GetInventory();
            playerId = player.GetPlayerID(); filename = profile.GetFilename(); name = profile.GetName(); path = profile.GetPath();
        }

        internal static bool TryCapture(Player player, Guid operation, Func<bool> custodyUnchanged,
            out BlueprintStoragePlayerProfile expectation, out string reason)
        {
            expectation = null; reason = null;
            try
            {
                if (!player || !Game.instance || operation == Guid.Empty || custodyUnchanged == null || !custodyUnchanged() ||
                    !BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var expected))
                    throw new InvalidOperationException("Exact completed transfer is required before profile readback.");
                var candidate = new BlueprintStoragePlayerProfile(player, operation, custodyUnchanged, expected);
                if (!candidate.Valid()) throw new InvalidOperationException("Readback requires the same current Local character and transfer.");
                expectation = candidate; return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        private bool Valid() => player && player == Player.m_localPlayer && Player.m_localPlayerExists &&
            Game.instance == game && !game.IsShuttingDown() && ReferenceEquals(game.GetPlayerProfile(), profile) &&
            profile != null && profile.m_fileSource == FileHelpers.FileSource.Local && playerId != 0 &&
            player.GetPlayerID() == playerId && profile.GetPlayerID() == playerId &&
            profile.GetFilename() == filename && profile.GetName() == name && profile.GetPath() == path &&
            !string.IsNullOrWhiteSpace(filename) && !string.IsNullOrWhiteSpace(name) &&
            Path.IsPathRooted(path) && string.Equals(Path.GetFileName(path), filename + ".fch", StringComparison.Ordinal) &&
            ReferenceEquals(player.GetInventory(), inventory) && custodyUnchanged() &&
            BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) && current.Info == expected.Info && current.Zdo == expected.Zdo;

        internal bool TryVerify(out NativePlayerProfileReadback.Evidence evidence, out string reason)
        {
            evidence = null; reason = null;
            try
            {
                if (!Valid()) throw new InvalidOperationException("Player, Local source or transfer custody changed before readback.");
                // No write/delete sharing: on Windows the canonical name cannot be replaced during this read.
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!NativePlayerProfileReadback.TryVerify(file, playerId, name, expected,
                        prefab => prefab.GetStableHashCode(), out var result, out reason)) return false;
                    if (!Valid()) throw new InvalidOperationException("Player or transfer changed during profile readback.");
                    evidence = result; return true;
                }
            }
            catch (Exception error) { reason = error.Message; return false; }
        }
    }
}
