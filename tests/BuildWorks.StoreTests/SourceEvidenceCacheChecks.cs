using System;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class SourceEvidenceCacheChecks
    {
        internal static void Run()
        {
            var evidence = new BlueprintSourceDocument("blueprint", "source.blueprint",
                "#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\"\n");
            var source = CompositeBlueprintStore.SourceDocument.FromGeometry(evidence);
            var validated = source.ToGeometry();
            var copies = CompositeBlueprintStore.CloneSources(new[] { source, null });
            Require(copies.Count == 2 && copies[1] == null && !ReferenceEquals(source, copies[0]) &&
                ReferenceEquals(validated, copies[0].ToGeometry()), "Source clone rebuilt evidence or shared mutable envelope");
            foreach (var mutate in new Action<CompositeBlueprintStore.SourceDocument>[] {
                value => value.text += "changed", value => value.id += "changed",
                value => value.name = "renamed.blueprint", value => value.format = "vbuild" })
            {
                var changed = CompositeBlueprintStore.CloneSources(new[] { source })[0];
                mutate(changed);
                bool refused = false;
                try { changed.ToGeometry(); } catch (ArgumentException) { refused = true; }
                Require(refused && ReferenceEquals(validated, source.ToGeometry()),
                    "Copied evidence bypassed identity validation or changed the original");
            }
            var next = new BlueprintSourceDocument(evidence.Format, evidence.Name, evidence.Text + "\n");
            copies[0].id = next.Id;
            copies[0].text = next.Text;
            Require(copies[0].ToGeometry().Id == next.Id && !ReferenceEquals(validated, copies[0].ToGeometry()) &&
                ReferenceEquals(validated, source.ToGeometry()), "Valid changed source retained stale evidence");
            Console.WriteLine("PASS warm immutable source evidence reuse: clone identity, independent envelopes, changed text/ID/name/format refusal and valid rebind");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
