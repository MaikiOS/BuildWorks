using System;
using System.Linq;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Removal = OstrixMods.BuildWorks.BlueprintConstructionCancellationJournal;
using Payout = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks
{
    internal enum ConstructionCancellationAction { Unpaid, Remove, ResumeRemoval, IssueRefund, AwaitCollection, Review }

    // Read-only routing; native operations still own preflight, durable intents and saved evidence.
    internal static class BlueprintConstructionCancellationAction
    {
        internal static ConstructionCancellationAction Read(Plans plans, Guid id, long revision, long player)
        {
            var plan = plans.Load(id);
            if (player == 0 || plan.Cancelled || plan.Revision != revision)
                throw new InvalidOperationException("Construction project revision or player changed.");
            var removal = new Removal(plans).Read(id);
            if (removal == null)
            {
                var parts = new BlueprintConstructionPartJournal(plans);
                if (plan.Parts.Select(part => parts.Read(id, part.Id)).Any(part => part != null &&
                    (part.RequiresReconciliation || part.Phase == BlueprintConstructionPartJournal.Committed && part.Player != player)))
                    return ConstructionCancellationAction.Review;
                return BlueprintConstructionPartJournal.HasExecutionEvidence(plans, plan)
                    ? ConstructionCancellationAction.Remove : ConstructionCancellationAction.Unpaid;
            }
            if (removal.Player != player || removal.PlanRevision != revision)
                throw new InvalidOperationException("Only the original payer can continue cancellation.");
            if (removal.Phase == Removal.Prepared) return ConstructionCancellationAction.ResumeRemoval;
            if (removal.Phase != Removal.RemovedSaved) return ConstructionCancellationAction.Review;
            var payout = new Payout(plans).Read(id);
            if (payout == null || payout.Phase == Payout.Prepared && payout.Items.All(item => item.State == Payout.Ready))
                return ConstructionCancellationAction.IssueRefund;
            return payout.Phase == Payout.IssuedSaved ? ConstructionCancellationAction.AwaitCollection : ConstructionCancellationAction.Review;
        }
    }
}
