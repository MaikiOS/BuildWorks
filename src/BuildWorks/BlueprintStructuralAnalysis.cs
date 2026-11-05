using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using Evaluation = OstrixMods.BuildWorks.Geometry.StructuralSupportEvaluation;

namespace OstrixMods.BuildWorks
{
    internal enum BlueprintStructuralState { Pending, Supported, Unsupported, Unknown, NotApplicable }

    internal sealed class BlueprintStructuralAnalysis : IDisposable
    {
        internal sealed class Node
        {
            internal BlueprintStructuralSnapshot.Part Part;
            internal BlueprintStructuralState State;
            internal string Reason, PermanentUnknown, PrimaryId, SecondaryId, LostSupportId;
            internal bool Foundation, Removed;
            internal Evaluation.Result Result;
            internal readonly List<BlueprintStructuralSnapshot.Contact> Contacts = new List<BlueprintStructuralSnapshot.Contact>();
            internal float? ColorValue => State == BlueprintStructuralState.Supported || State == BlueprintStructuralState.Unsupported
                ? (float?)Result.ColorValue : null;
            internal bool WouldFall => State == BlueprintStructuralState.Unsupported && Part.NoSupportWear;
        }

        private readonly BlueprintStructuralSnapshot snapshot;
        private readonly List<Node> nodes;
        private readonly Dictionary<BlueprintStructuralSnapshot.Part, Node> byPart;
        private readonly IEnumerator<int> work;
        private bool disposed;
        internal IReadOnlyList<Node> Nodes { get; }
        internal bool Complete { get; private set; }
        internal string Stage { get; private set; } = "contacts";
        internal int Processed { get; private set; }

        internal BlueprintStructuralAnalysis(BlueprintStructuralSnapshot snapshot, float groundY)
        {
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (float.IsNaN(groundY) || float.IsInfinity(groundY)) throw new ArgumentOutOfRangeException(nameof(groundY));
            nodes = snapshot.Parts.OrderBy(part => part.Id, StringComparer.Ordinal).Select(part => new Node { Part = part }).ToList();
            Nodes = nodes.AsReadOnly();
            byPart = nodes.ToDictionary(node => node.Part);
            work = Calculate(groundY).GetEnumerator();
        }

        internal bool Step(int maximumSteps = 16, double milliseconds = 8)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintStructuralAnalysis));
            if (maximumSteps < 1 || milliseconds <= 0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))
                throw new ArgumentOutOfRangeException(nameof(maximumSteps));
            var watch = Stopwatch.StartNew();
            for (int index = 0; !Complete && index < maximumSteps && (index == 0 || watch.Elapsed.TotalMilliseconds < milliseconds); ++index)
                if (!work.MoveNext()) { Complete = true; Stage = "complete"; work.Dispose(); }
            return Complete;
        }

        private IEnumerable<int> Calculate(float groundY)
        {
            long updates = 0, maximumUpdates = Math.Max(4096L, nodes.Count * 4096L);
            bool incompleteGeometry = nodes.Any(node => !node.Part.CompleteGeometry);
            while (true)
            {
                Stage = "contacts"; Processed = 0;
                foreach (Node node in nodes)
                {
                    if (node.Removed) continue;
                    node.Reason = node.PermanentUnknown ?? node.Part.UnknownReason;
                    node.PrimaryId = node.SecondaryId = null;
                    if (node.Reason != null) node.State = BlueprintStructuralState.Unknown;
                    else if (node.Part.StaticAnchor) node.State = BlueprintStructuralState.NotApplicable;
                    else if (!snapshot.TryContacts(node.Part, groundY, node.Contacts, out node.Foundation, out node.Reason))
                        node.State = BlueprintStructuralState.Unknown;
                    else if (incompleteGeometry && !node.Foundation)
                    { node.State = BlueprintStructuralState.Unknown; node.Reason = "incomplete-blueprint-geometry"; }
                    else node.State = BlueprintStructuralState.Pending;
                    ++Processed; yield return 0;
                }
                foreach (int tick in PropagateUnknown()) yield return tick;
                var values = new Dictionary<Node, float>();
                foreach (Node node in nodes)
                {
                    values[node] = node.State == BlueprintStructuralState.Pending && node.Foundation ? node.Part.Profile.Value.Maximum : 0f;
                    if (node.State == BlueprintStructuralState.Pending && node.Foundation)
                    {
                        try
                        {
                            node.Result = Evaluation.EvaluateReceiver(node.Part.Profile.Value, Point(node.Part.Com),
                                node.Part.ForceCorrectCom, true, Array.Empty<Evaluation.Contact>());
                        }
                        catch (ArgumentException)
                        {
                            node.State = BlueprintStructuralState.Unknown;
                            node.Reason = node.PermanentUnknown = "invalid-support-math"; values[node] = 0;
                        }
                    }
                }
                foreach (int tick in PropagateUnknown()) yield return tick;
                bool restart = false;
                Stage = "support";
                while (true)
                {
                    bool changed = false;
                    var next = new Dictionary<Node, Evaluation.Result>();
                    foreach (Node node in nodes)
                    {
                        if (node.State != BlueprintStructuralState.Pending || node.Foundation) continue;
                        if (++updates > maximumUpdates)
                        {
                            foreach (Node unfinished in nodes.Where(value => value.State == BlueprintStructuralState.Pending && !value.Foundation))
                            { unfinished.State = BlueprintStructuralState.Unknown; unfinished.Reason = "analysis-budget"; }
                            FinishKnown(); yield break;
                        }
                        Evaluation.Result result;
                        try
                        {
                            var contacts = node.Contacts.Select(contact => new Evaluation.Contact(Point(contact.Source.Origin),
                                Point(contact.Source.Com), Point(contact.Point), values[byPart[contact.Source]], contact.Source.Relays)).ToArray();
                            result = Evaluation.EvaluateReceiver(node.Part.Profile.Value, Point(node.Part.Com),
                                node.Part.ForceCorrectCom, false, contacts);
                        }
                        catch (ArgumentException)
                        { node.PermanentUnknown = "invalid-support-math"; restart = true; break; }
                        next[node] = result;
                        if (!result.Support.Equals(values[node])) changed = true;
                        yield return 0;
                    }
                    if (restart) break;
                    foreach (var pair in next) { values[pair.Key] = pair.Value.Support; pair.Key.Result = pair.Value; }
                    if (!changed) break;
                }
                if (restart) continue;
                Stage = "cascade";
                bool removed = false;
                foreach (Node node in nodes)
                {
                    if (node.State != BlueprintStructuralState.Pending) continue;
                    if (!node.Result.HasSupport && node.Part.NoSupportWear)
                    {
                        node.Removed = true; removed = true;
                        node.State = BlueprintStructuralState.Unsupported;
                        node.Reason = node.LostSupportId == null ? "insufficient-support" : "support-cascade";
                        SetWitness(node);
                        // Only the analysis copy disappears; the blueprint and world stay untouched.
                        node.Part.Root.SetActive(false);
                    }
                    yield return 0;
                }
                if (!removed) { FinishKnown(); yield break; }
                foreach (Node node in nodes.Where(value => value.State == BlueprintStructuralState.Pending &&
                    value.Result.HasSupport && value.LostSupportId == null))
                {
                    foreach (int index in new[] { node.Result.FirstContact, node.Result.SecondContact })
                        if (index >= 0 && byPart[node.Contacts[index].Source].Removed)
                        { node.LostSupportId = node.Contacts[index].Source.Id; break; }
                }
            }
        }

        private IEnumerable<int> PropagateUnknown()
        {
            bool changed;
            do
            {
                changed = false;
                foreach (Node node in nodes)
                {
                    if (node.State == BlueprintStructuralState.Pending && !node.Foundation &&
                        node.Contacts.Any(contact => byPart[contact.Source].State == BlueprintStructuralState.Unknown))
                    { node.State = BlueprintStructuralState.Unknown; node.Reason = "unknown-support-chain"; changed = true; }
                    yield return 0;
                }
            } while (changed);
        }

        private void FinishKnown()
        {
            foreach (Node node in nodes)
            {
                if (node.State != BlueprintStructuralState.Pending) continue;
                node.State = node.Result.HasSupport ? BlueprintStructuralState.Supported : BlueprintStructuralState.Unsupported;
                node.Reason = node.Result.HasSupport ? null : node.LostSupportId == null ? "insufficient-support" : "support-cascade";
                SetWitness(node);
            }
        }

        private static void SetWitness(Node node)
        {
            if (node.Foundation)
                node.PrimaryId = node.Part.GroundContact ? "@ground" : node.Part.StaticSupport?.Id;
            else
            {
                node.PrimaryId = node.Result.FirstContact >= 0 ? node.Contacts[node.Result.FirstContact].Source.Id : null;
                node.SecondaryId = node.Result.SecondContact >= 0 ? node.Contacts[node.Result.SecondContact].Source.Id : null;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; work.Dispose(); snapshot.Dispose();
        }

        private static Point3 Point(UnityEngine.Vector3 value) => new Point3(value.x, value.y, value.z);
    }
}
