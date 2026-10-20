using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ReservationChecks
{
    internal sealed partial class PlayerProfile
    {
        internal int Captures, Writes;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void SavePlayerData(Player player) { player.Save(new ZPackage()); Captures++; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool SavePlayerToDisk() { Writes++; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal bool Save() => SavePlayerToDisk();
    }

    internal static class PlayerSaveGuardChecks
    {
        internal static void Run()
        {
            var oldGame = Game.instance; var oldPlayer = Player.m_localPlayer;
            int checks = 0;
            void Check(bool valid) { checks++; if (!valid) throw new InvalidOperationException("Player save guard failed."); }
            void Refused(Action call)
            {
                bool refused = false;
                try { call(); }
                catch (Exception error) { refused = (error is TargetInvocationException ? error.InnerException : error) is InvalidOperationException; }
                Check(refused);
            }
            try
            {
                var player = Player.m_localPlayer = new Player();
                var profile = new PlayerProfile { Id = player.GetPlayerID() };
                Game.instance = new Game { Profile = profile };
                Game.instance.SaveAction = () => { profile.SavePlayerData(player); profile.Save(); };
                var writer = typeof(PlayerProfile).GetMethod("SavePlayerToDisk", BindingFlags.Instance | BindingFlags.NonPublic);
                using (var guard = BlueprintPlayerSaveGuard.Begin(player))
                {
                    Refused(() => player.Save(new ZPackage()));
                    Refused(() => profile.SavePlayerData(player));
                    Refused(() => profile.Save());
                    Refused(() => writer.Invoke(profile, null));
                    Refused(() => Game.instance.SavePlayerProfile(false, false));
                    Refused(() => BlueprintPlayerSaveGuard.Begin(player));
                    Check(guard.BlockedAttempts == 5 && profile.Captures == 0 && profile.Writes == 0);
                    var other = new PlayerProfile(); Check(other.Save() && other.Writes == 1);
                    new Player().Save(new ZPackage()); Check(guard.BlockedAttempts == 5);
                }
                Game.instance.SavePlayerProfile(false, false);
                Check(profile.Captures == 1 && profile.Writes == 1);
                // A callback can abort after inventory mutation; disposing must still release serialization.
                try { using (BlueprintPlayerSaveGuard.Begin(player)) throw new ApplicationException("callback failure"); }
                catch (ApplicationException) { }
                Check(profile.Save() && profile.Writes == 2);
                profile.m_fileSource = FileHelpers.FileSource.Cloud;
                Refused(() => BlueprintPlayerSaveGuard.Begin(player));
                Console.WriteLine("PASS player save staging guard: " + checks + " assertions; actual Harmony prefixes with host substitutes, no native Valheim proof.");
            }
            finally { Game.instance = oldGame; Player.m_localPlayer = oldPlayer; }
        }
    }
}
