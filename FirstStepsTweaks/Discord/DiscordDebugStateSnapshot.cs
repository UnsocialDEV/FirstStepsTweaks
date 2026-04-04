using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordDebugStateSnapshot
    {
        public IReadOnlyDictionary<string, string> LinkedAccounts { get; set; } = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, PendingDiscordLinkCodeRecord> PendingCodes { get; set; } = new Dictionary<string, PendingDiscordLinkCodeRecord>();

        public IReadOnlyCollection<string> ClaimedRewardPlayerUids { get; set; } = new string[0];

        public IReadOnlyCollection<string> PendingRewardPlayerUids { get; set; } = new string[0];

        public string RelayCursorMessageId { get; set; } = string.Empty;

        public string LinkCursorMessageId { get; set; } = string.Empty;

        public DateTime? LinkPollLastSuccessfulUtc { get; set; }

        public string LinkPollLastFailureSummary { get; set; } = string.Empty;

        public int LinkPollLastProcessedPageCount { get; set; }

        public int LinkPollLastProcessedMessageCount { get; set; }

        public bool LinkPollLastPollReachedProcessingCap { get; set; }
    }
}
