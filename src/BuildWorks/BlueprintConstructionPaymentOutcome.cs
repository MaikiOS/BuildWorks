using System;
using System.Runtime.Serialization;

namespace OstrixMods.BuildWorks
{
    // Recipe observations only: tokens are not source authority, save proof or refund permission.
    [DataContract] internal sealed class BlueprintConstructionPaymentOutcome
    {
        [DataMember(Order = 0)] internal bool ExplicitSources;
        [DataMember(Order = 1)] internal Source[] Sources = Array.Empty<Source>();
        [DataContract] internal sealed class Source
        {
            [DataMember(Order = 0)] internal Guid Token;
            [DataMember(Order = 1)] internal bool Exact;
            [DataMember(Order = 2)] internal Item[] Items = Array.Empty<Item>();
        }
        [DataContract] internal sealed class Item
        {
            [DataMember(Order = 0)] internal string Properties;
            [DataMember(Order = 1)] internal int Before;
            // -1 means unknown; it must never be interpreted as an empty stack.
            [DataMember(Order = 2)] internal int After = -1;
        }
    }
}
