#if BUILDWORKS_TEST_STAND
using System;
using System.Collections;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;

namespace OstrixMods.BuildWorks
{
    // Opt-in QA binary only. Keep the process pinned until exit, even if the plugin is disabled.
    internal static class BuildWorksTestIsolation
    {
        internal const string MarkerName = "qa-isolation.token";
        private const string OwnerName = "buildworks-qa-root.txt";
        private static string root, token, fault;

        internal static bool Activate(string configDirectory)
        {
            string marker = Path.Combine(configDirectory, "BuildWorks", MarkerName);
            if (!File.Exists(marker)) return false;
            token = ReadToken(marker);
            string selectedRoot = ResolveRoot(configDirectory, token);
            RequireColdStorage();
            RequireHost(typeof(World), "25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE");
            RequireHost(typeof(Utils), "6BE2AFEB97B5B26FD3176E1B8584C07E57AD48DDCD6E8E5E79E32BEE943827C1");
            EnsureOwnedRoot(selectedRoot, token);

            // Validate all targets before any patch or native root change.
            MethodInfo initialize = Required(AccessTools.Method(typeof(PlatformInitializer), "InitializeSaveDataStorage"));
            MethodInfo supported = Required(AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupported"));
            MethodInfo enabled = Required(AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupportedAndEnabled"));
            MethodInfo set = Required(AccessTools.Method(typeof(Utils), nameof(Utils.SetSaveDataPath)));
            MethodInfo reset = Required(AccessTools.Method(typeof(Utils), nameof(Utils.ResetSaveDataPath)));
            var harmony = new Harmony(BuildWorksPlugin.PluginGuid + ".qa-isolation");
            fault = "QA isolation did not finish. Restart after fixing the test configuration.";
            harmony.Patch(initialize, prefix: Prefix(nameof(BeforeStorage)));
            harmony.Patch(supported, prefix: Prefix(nameof(CloudOff)));
            harmony.Patch(enabled, prefix: Prefix(nameof(CloudOff)));
            root = selectedRoot;
            Utils.SetSaveDataPath(root);
            harmony.Patch(set, prefix: Prefix(nameof(BeforeSetRoot)));
            harmony.Patch(reset, prefix: Prefix(nameof(BeforeResetRoot)));
            foreach (MethodInfo target in new[] { initialize, supported, enabled, set, reset })
                if (Harmony.GetPatchInfo(target)?.Prefixes.Any(patch => patch.owner == harmony.Id) != true)
                    throw new InvalidOperationException("QA isolation prefix was not installed: " + target.Name);
            AssertPinned();
            fault = null;
            return true;
        }

        internal static string ResolveRoot(string configDirectory, string sessionToken)
        {
            if (!Guid.TryParseExact(sessionToken, "N", out Guid id) || id == Guid.Empty)
                throw new InvalidDataException("QA isolation needs a nonempty GUID in N format.");
            string config = CompositeBlueprintStore.ValidateExchangeDirectory(configDirectory);
            var directory = new DirectoryInfo(config);
            if (!string.Equals(directory.Name, "config", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(directory.Parent?.Name, "BepInEx", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(directory.Parent?.Parent?.Name, "TerrainRamp-1.0-Test", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("QA isolation is restricted to TerrainRamp-1.0-Test/BepInEx/config.");
            return CompositeBlueprintStore.ValidateExchangeDirectory(Path.Combine(config, "BuildWorks", "qa-saves", id.ToString("N")));
        }

        internal static void EnsureOwnedRoot(string directory, string sessionToken)
        {
            directory = CompositeBlueprintStore.ValidateExchangeDirectory(directory);
            string owner = Path.Combine(directory, OwnerName);
            if (Directory.Exists(directory))
            {
                if (File.Exists(owner))
                {
                    if (ReadToken(owner) != sessionToken) throw new InvalidDataException("QA save root belongs to another session.");
                    return;
                }
                if (Directory.EnumerateFileSystemEntries(directory).Any())
                    throw new InvalidDataException("Refusing to adopt an existing nonempty QA save root.");
            }
            Directory.CreateDirectory(directory);
            CompositeBlueprintStore.ValidateExchangeDirectory(directory);
            using (var stream = new FileStream(owner, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream)) writer.Write(sessionToken);
        }

        private static string ReadToken(string path)
        {
            CompositeBlueprintStore.ValidateExchangeDirectory(Path.GetDirectoryName(path));
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 64 || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Invalid QA isolation token file.");
            string value = File.ReadAllText(path).Trim();
            if (!Guid.TryParseExact(value, "N", out Guid id) || id == Guid.Empty)
                throw new InvalidDataException("Invalid QA isolation token.");
            return id.ToString("N");
        }

        private static object StaticField(Type type, string name)
            => (AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name)).GetValue(null);

        private static void RequireColdStorage()
        {
            if (PlatformInitializer.StartedSaveDataInitialization ||
                StaticField(typeof(SaveSystem), "s_saveCollections") != null ||
                ((ICollection)StaticField(typeof(SaveSystem), "s_cachedWorlds")).Count != 0 ||
                (bool)StaticField(typeof(SaveSystem), "s_saving") || StaticField(typeof(ZNet), "m_world") != null ||
                Game.instance || Player.m_localPlayer)
                throw new InvalidOperationException("QA isolation refused: save storage, world or player is already active.");
            FejdStartup startup = FejdStartup.instance;
            if (startup)
                foreach (string name in new[] { "m_profiles", "m_worlds", "m_world" })
                {
                    object value = (AccessTools.Field(typeof(FejdStartup), name) ??
                        throw new MissingFieldException(typeof(FejdStartup).FullName, name)).GetValue(startup);
                    if (value is ICollection collection ? collection.Count != 0 : value != null)
                        throw new InvalidOperationException("QA isolation refused: startup save selection is already populated.");
                }
        }

        internal static void AssertPinned()
        {
            if (root == null || FileHelpers.CloudStorageSupported || FileHelpers.CloudStorageSupportedAndEnabled ||
                !string.Equals(Path.GetFullPath(Utils.GetSaveDataPath(FileHelpers.FileSource.Local)), root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("QA save root/cloud isolation is no longer valid.");
            CompositeBlueprintStore.ValidateExchangeDirectory(root);
            if (ReadToken(Path.Combine(root, OwnerName)) != token)
                throw new InvalidDataException("QA save root ownership changed.");
        }

        internal static string PrepareFreshSaves()
        {
            AssertPinned();
            if (!PlatformInitializer.SaveDataInitialized || Game.instance || Player.m_localPlayer || StaticField(typeof(ZNet), "m_world") != null)
                throw new InvalidOperationException("QA identities require initialized storage and the main menu.");
            string name = "BW_QA_" + token.Substring(0, 8);
            string receipt = Path.Combine(root, "qa-identities.txt");
            var world = new World(name, "BuildWorksQA") { m_fileSource = FileHelpers.FileSource.Local };
            var profile = new PlayerProfile(name, FileHelpers.FileSource.Local);
            profile.SetName(name);
            string worldPath = world.GetSaveFWLPath(), profilePath = profile.GetPath();
            if (world.m_uid == 0 || profile.GetPlayerID() == 0)
                throw new InvalidDataException("Valheim did not assign fresh QA identities.");
            if (File.Exists(receipt))
            {
                ValidateSavePath(receipt);
                ValidateSavePath(worldPath); ValidateSavePath(profilePath);
                ReadIdentityReceipt();
                if (!File.Exists(worldPath) || !File.Exists(profilePath))
                    throw new InvalidDataException("Previous QA identity preparation is incomplete; it will not be retried automatically.");
                return name;
            }
            foreach (string path in new[] { worldPath, profilePath })
                foreach (string suffix in new[] { "", ".new", ".old" })
                {
                    ValidateSavePath(path + suffix);
                    if (File.Exists(path + suffix)) throw new InvalidDataException("Refusing to overwrite an existing QA identity.");
                }
            using (var stream = new FileStream(receipt, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                foreach (string value in new[] { "BuildWorks-QA-1", token, name, world.m_uid.ToString(CultureInfo.InvariantCulture),
                    name, profile.GetPlayerID().ToString(CultureInfo.InvariantCulture), "started" }) writer.WriteLine(value);
            }
            AssertPinned(); ValidateSavePath(worldPath);
            world.SaveWorldFWLData(DateTime.Now);
            if (world.m_fileSource != FileHelpers.FileSource.Local || !File.Exists(worldPath))
                throw new IOException("Valheim did not create the isolated Local world metadata.");
            AssertPinned(); ValidateSavePath(profilePath);
            if (!profile.Save() || profile.m_fileSource != FileHelpers.FileSource.Local || !File.Exists(profilePath))
                throw new IOException("Valheim did not create the isolated Local character.");
            AssertPinned(); ValidateSavePath(receipt);
            File.AppendAllText(receipt, "complete" + Environment.NewLine);
            SaveSystem.InvalidateCache(SaveDataType.World);
            SaveSystem.InvalidateCache(SaveDataType.Character);
            return name;
        }

        private static string[] ReadIdentityReceipt()
        {
            string path = Path.Combine(root, "qa-identities.txt");
            ValidateSavePath(path);
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 512) throw new InvalidDataException("Invalid QA identity receipt.");
            string[] lines = File.ReadAllLines(path);
            ValidateIdentityReceipt(lines, token);
            return lines;
        }

        internal static long[] ValidateIdentityReceipt(string[] lines, string sessionToken)
        {
            if (!Guid.TryParseExact(sessionToken, "N", out Guid id) || id == Guid.Empty)
                throw new InvalidDataException("Invalid QA identity token.");
            string name = "BW_QA_" + id.ToString("N").Substring(0, 8);
            if (lines == null || lines.Length != 8 || lines[0] != "BuildWorks-QA-1" || lines[1] != id.ToString("N") ||
                lines[2] != name || lines[4] != name || lines[6] != "started" || lines[7] != "complete" ||
                !long.TryParse(lines[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out long worldId) || worldId == 0 ||
                !long.TryParse(lines[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out long playerId) || playerId == 0)
                throw new InvalidDataException("Previous QA identity preparation is incomplete or changed; it will not be retried automatically.");
            return new[] { worldId, playerId };
        }

        internal static void RequireIdentityMatch(string[] receipt, string sessionToken, string worldName, long worldId,
            string profileFilename, long profileId, long localPlayerId)
        {
            long[] ids = ValidateIdentityReceipt(receipt, sessionToken);
            if (worldName != receipt[2] || worldId != ids[0] || profileFilename != receipt[4] ||
                profileId != ids[1] || localPlayerId != ids[1])
                throw new InvalidDataException("Loaded QA world/profile/player does not match its original identity receipt.");
        }

        // Call immediately before each future provisioning operation; an earlier PASS is not a grant capability.
        internal static Player RequireLoadedSession()
        {
            AssertPinned();
            ZNet network = ZNet.instance;
            Game game = Game.instance;
            Player player = Player.m_localPlayer;
            if (!network || !game || !player || !ZoneSystem.instance || !PlatformInitializer.SaveDataInitialized ||
                !network.IsServer() || network.IsDedicated() || ZNet.IsOpenServer() || network.GetPeers().Count != 0 ||
                game.IsShuttingDown() || player.IsDead() || player.IsTeleporting() || player.GetInventory() == null ||
                Player.m_debugMode || player.NoCostCheat() || Game.m_worldLevel != 0 ||
                ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoBuildCost) || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))
                throw new InvalidOperationException("QA preparation requires a loaded living player on a closed Local survival server with no peers or cheats.");
            World world = network.GetWorld();
            PlayerProfile profile = game.GetPlayerProfile();
            if (world == null || profile == null || world.m_fileSource != FileHelpers.FileSource.Local ||
                profile.m_fileSource != FileHelpers.FileSource.Local)
                throw new InvalidOperationException("QA preparation requires Local world and character saves.");
            ValidateSavePath(world.GetSaveFWLPath());
            ValidateSavePath(profile.GetPath());
            if (!File.Exists(world.GetSaveFWLPath()) || !File.Exists(profile.GetPath()))
                throw new InvalidDataException("Loaded QA identity save files are missing.");
            RequireIdentityMatch(ReadIdentityReceipt(), token, world.m_worldName, world.m_uid,
                profile.GetFilename(), profile.GetPlayerID(), player.GetPlayerID());
            return player;
        }

        internal static void ValidateSavePath(string path)
        {
            string full = Path.GetFullPath(path);
            if (root == null || !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("QA save destination escaped its isolated root.");
            CompositeBlueprintStore.ValidateExchangeDirectory(Path.GetDirectoryName(full));
            if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("QA save destination is a symbolic link.");
        }

        private static MethodInfo Required(MethodInfo method) => method ?? throw new MissingMethodException("QA host API changed.");
        private static void RequireHost(Type type, string expectedHash)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(type.Assembly.Location))
                if (BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "") != expectedHash)
                    throw new InvalidOperationException("QA native save contract needs review after a host update: " + type.Assembly.GetName().Name);
        }
        private static HarmonyMethod Prefix(string name) => new HarmonyMethod(AccessTools.Method(typeof(BuildWorksTestIsolation), name));
        private static bool CloudOff(ref bool __result) { __result = false; return false; }
        private static void BeforeStorage()
        {
            if (fault != null) throw new InvalidOperationException(fault);
            AssertPinned();
        }
        private static void BeforeSetRoot(string __0)
        {
            if (!string.Equals(Path.GetFullPath(__0 ?? ""), root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("QA save root is pinned until process exit.");
        }
        private static bool BeforeResetRoot() => false;
    }
}
#endif
