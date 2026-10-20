using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace OstrixMods.BuildWorks
{
    // Only staging: dispose after a final coherent pair, a proven no-effect, or a retained pending marker.
    internal sealed class BlueprintPlayerSaveGuard : IDisposable
    {
        private static BlueprintPlayerSaveGuard active;
        private readonly Player player;
        private readonly PlayerProfile profile;
        private int blocked;
        internal int BlockedAttempts => blocked;
        private BlueprintPlayerSaveGuard(Player player, PlayerProfile profile) { this.player = player; this.profile = profile; }

        internal static BlueprintPlayerSaveGuard Begin(Player player)
        {
            var game = Game.instance; var profile = game ? game.GetPlayerProfile() : null;
            if (active != null || !player || player != Player.m_localPlayer || profile == null ||
                profile.m_fileSource != FileHelpers.FileSource.Local || profile.GetPlayerID() != player.GetPlayerID())
                throw new InvalidOperationException("Receipt staging requires the same idle Local player/profile.");
            var harmony = new Harmony("ostrmod.buildworks");
            Install(harmony, AccessTools.DeclaredMethod(typeof(Player), nameof(Player.Save), new[] { typeof(ZPackage) }), nameof(BeforePlayer));
            Install(harmony, AccessTools.DeclaredMethod(typeof(PlayerProfile), nameof(PlayerProfile.SavePlayerData), new[] { typeof(Player) }), nameof(BeforeProfile));
            Install(harmony, AccessTools.DeclaredMethod(typeof(PlayerProfile), "SavePlayerToDisk", Type.EmptyTypes), nameof(BeforeProfile));
            return active = new BlueprintPlayerSaveGuard(player, profile);
        }
        private static void Install(Harmony harmony, MethodInfo target, string prefix)
        {
            if (target == null) throw new MissingMethodException("Native receipt staging/save contract changed.");
            var method = AccessTools.DeclaredMethod(typeof(BlueprintPlayerSaveGuard), prefix);
            if (Harmony.GetPatchInfo(target)?.Prefixes.Any(p => p.PatchMethod == method) != true)
                harmony.Patch(target, prefix: new HarmonyMethod(method));
        }
        private static void BeforePlayer(Player __instance)
        { if (active != null && ReferenceEquals(active.player, __instance)) active.Refuse(); }
        private static void BeforeProfile(PlayerProfile __instance)
        { if (active != null && ReferenceEquals(active.profile, __instance)) active.Refuse(); }
        private void Refuse()
        {
            Interlocked.Increment(ref blocked);
            throw new InvalidOperationException("Player/profile serialization is blocked while receipt and inventory are being staged.");
        }
        public void Dispose() { if (ReferenceEquals(active, this)) active = null; }
    }
}
