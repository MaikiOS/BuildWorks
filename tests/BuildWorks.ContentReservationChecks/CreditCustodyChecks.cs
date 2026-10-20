using System;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    // Actual production predicates are extracted by Run.ps1; these are inert external boundaries.
    internal sealed partial class CreditCustodyProbe
    {
        internal readonly uint generation = SaveSystem.GetSaveNumber();
        internal readonly ZNet network = new ZNet();
        internal readonly Player player = new Player();
        internal readonly string key = "receipt", value = "final";
        internal readonly BlueprintForeignPartData before = new BlueprintForeignPartData("blueprint", "before", null, null);
        internal readonly BlueprintForeignPartData after = new BlueprintForeignPartData("blueprint", "after", null, null);
        internal BlueprintForeignPartData Current;
        internal bool CurrentContext = true, SourceValid = true;
        private bool Context() => CurrentContext;
        private bool SourceMatches() => SourceValid;
        private bool Matches(BlueprintForeignPartData expected) => ReferenceEquals(Current, expected);
        internal CreditCustodyProbe() { Current = before; }
    }
    internal static class CreditCustodyChecks
    {
        internal static void Run()
        {
            uint savedGeneration = SaveSystem.Generation; int checks = 0;
            void Check(bool value) { checks++; if (!value) throw new InvalidOperationException("Credit custody predicate failed."); }
            try
            {
                var baseline = new CreditCustodyProbe();
                Check(baseline.BeforeCustody() && !baseline.CreditedCustody(baseline.after));
                baseline.Current = baseline.after; baseline.player.m_customData[baseline.key] = baseline.value;
                Check(!baseline.BeforeCustody() && baseline.CreditedCustody(baseline.after));
                Action<CreditCustodyProbe>[] callbacks = {
                    p => SaveSystem.Generation++, p => p.network.Saving = true, p => p.SourceValid = false,
                    p => p.Current = p.before, p => p.player.m_customData[p.key] = "pending:" + p.value,
                    p => p.CurrentContext = false, p => p.player.m_customData.Remove(p.key)
                };
                foreach (var callback in callbacks)
                {
                    SaveSystem.Generation = savedGeneration;
                    var probe = new CreditCustodyProbe { Current = null };
                    probe.Current = probe.after; probe.player.m_customData[probe.key] = probe.value;
                    Check(probe.CreditedCustody(probe.after));
                    callback(probe); // The boundary change an Inventory.Changed/save callback may cause.
                    Check(!probe.CreditedCustody(probe.after) && !probe.BeforeCustody());
                }
                Console.WriteLine("PASS credit custody: " + checks + " assertions; actual predicates/wiring, inert callback boundaries; no native collection proof.");
            }
            finally { SaveSystem.Generation = savedGeneration; }
        }
    }
}
