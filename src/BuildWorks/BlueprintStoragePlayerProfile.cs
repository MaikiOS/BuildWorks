using System;
using System.IO;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Local native save/readback evidence only. Never releases holds or authorizes a refund.
    internal sealed class BlueprintStoragePlayerProfile
    {
        internal static readonly Guid ProcessSession = Guid.NewGuid();
        private readonly Player player;
        private readonly Game game;
        private readonly PlayerProfile profile;
        private readonly Inventory inventory;
        private readonly BlueprintForeignPartData expected;
        private readonly Func<bool> custodyUnchanged;
        private readonly string path, filename, name;
        private readonly long playerId;
        internal readonly Guid Operation;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private bool saveRequested;
        private byte[] freshPlayerData;

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

        private bool Valid() => Thread.CurrentThread.ManagedThreadId == thread && player && player == Player.m_localPlayer && Player.m_localPlayerExists &&
            Game.instance == game && !game.IsShuttingDown() && ReferenceEquals(game.GetPlayerProfile(), profile) &&
            profile != null && profile.m_fileSource == FileHelpers.FileSource.Local && playerId != 0 &&
            player.GetPlayerID() == playerId && profile.GetPlayerID() == playerId &&
            profile.GetFilename() == filename && profile.GetName() == name && profile.GetPath() == path &&
            !string.IsNullOrWhiteSpace(filename) && !string.IsNullOrWhiteSpace(name) &&
            Path.IsPathRooted(path) && string.Equals(Path.GetFileName(path), filename + ".fch", StringComparison.Ordinal) &&
            ReferenceEquals(player.GetInventory(), inventory) && custodyUnchanged() &&
            BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) && current.Info == expected.Info && current.Zdo == expected.Zdo;

        internal bool TrySaveAndVerify(out NativePlayerProfileReadback.Evidence evidence, out string reason)
        {
            evidence = null; reason = null;
            int finished = 0, wrongThread = 0;
            Action signal = () => { Interlocked.Increment(ref finished); if (Thread.CurrentThread.ManagedThreadId != thread) Interlocked.Increment(ref wrongThread); };
            try
            {
                if (saveRequested || !Valid() || SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter))
                    throw new InvalidOperationException("Fresh Local profile save requires unchanged custody and may only be requested once.");
                saveRequested = true;
                var package = new ZPackage(); player.Save(package); freshPlayerData = package.GetArray();
                if (freshPlayerData.Length > NativePlayerProfileReadback.MaximumProfileBytes - 72)
                    throw new InvalidOperationException("Fresh player data exceeds the profile readback budget.");
                using (var witness = BlueprintStorageProfileWrite.Begin(path, temporary =>
                {
                    if (!TryRead(temporary, out var written, out string failure)) throw new InvalidDataException(failure);
                    return written.ProfileHash;
                }))
                {
                    PlayerProfile.SavingFinished += signal;
                    try { game.SavePlayerProfile(setLogoutPoint: false, isFromRpc: false); }
                    finally { PlayerProfile.SavingFinished -= signal; }
                    if (finished != 1 || wrongThread != 0 || !Valid())
                        throw new InvalidOperationException("The requested Local profile save did not complete in its captured context.");
                    if (!witness.Completed || !TryVerify(out evidence, out reason) || evidence.ProfileHash != witness.ProfileHash)
                        throw new InvalidOperationException(reason ?? witness.Reason ?? "The current native profile write/replacement was not verified.");
                    return true;
                }
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        internal bool TryVerify(out NativePlayerProfileReadback.Evidence evidence, out string reason)
            => TryRead(path, out evidence, out reason);

        internal static bool TryVerifyLoaded(Player player, BlueprintStorageTransferJournal.Persistence saved,
            Func<bool> context, out NativePlayerProfileReadback.Evidence evidence, out string reason)
        {
            evidence = null; reason = null;
            try
            {
                var game = Game.instance;
                var profile = game ? game.GetPlayerProfile() : null;
                var inventory = player ? player.GetInventory() : null;
                string path = profile?.GetPath(), filename = profile?.GetFilename(), name = profile?.GetName();
                bool Valid() => player && player == Player.m_localPlayer && Player.m_localPlayerExists && game &&
                    Game.instance == game && !game.IsShuttingDown() && ReferenceEquals(game.GetPlayerProfile(), profile) &&
                    profile != null && profile.m_fileSource == FileHelpers.FileSource.Local &&
                    player.GetPlayerID() == saved.Player && profile.GetPlayerID() == saved.Player &&
                    profile.GetPath() == path && profile.GetFilename() == filename && profile.GetName() == name &&
                    !string.IsNullOrWhiteSpace(filename) && !string.IsNullOrWhiteSpace(name) && Path.IsPathRooted(path) &&
                    Path.GetFileName(path) == filename + ".fch" && ReferenceEquals(player.GetInventory(), inventory) &&
                    context() && BlueprintStorageLoadedInventory.Matches(inventory, saved.PlayerInventory);
                if (saved?.PlayerInventory == null || context == null ||
                    saved.SourceSession == ProcessSession && saved.SourcePlayerInstance == player.GetInstanceID() || !Valid())
                    throw new InvalidOperationException("Loaded inventory is not the exact native projection of the paired save.");
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!NativePlayerProfileReadback.TryVerify(file, saved.Player, name, saved.PlayerInventory,
                        prefab => prefab.GetStableHashCode(), out var current, out reason) ||
                        current.InventoryHash != saved.PlayerInventoryHash || !Valid())
                        throw new InvalidOperationException(reason ?? "Canonical profile inventory or loaded custody changed.");
                    evidence = current;
                }
                return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        private bool TryRead(string filename, out NativePlayerProfileReadback.Evidence evidence, out string reason)
        {
            evidence = null; reason = null;
            try
            {
                if (!Valid()) throw new InvalidOperationException("Player, Local source or transfer custody changed before readback.");
                // No write/delete sharing: on Windows the canonical name cannot be replaced during this read.
                using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    NativePlayerProfileReadback.Evidence result;
                    bool passed = freshPlayerData == null
                        ? NativePlayerProfileReadback.TryVerify(file, playerId, name, expected, prefab => prefab.GetStableHashCode(), out result, out reason)
                        : NativePlayerProfileReadback.TryVerifyPlayerData(file, playerId, name, expected, prefab => prefab.GetStableHashCode(), freshPlayerData, out result, out reason);
                    if (!passed) return false;
                    if (!Valid()) throw new InvalidOperationException("Player or transfer changed during profile readback.");
                    evidence = result; return true;
                }
            }
            catch (Exception error) { reason = error.Message; return false; }
        }
    }
}
