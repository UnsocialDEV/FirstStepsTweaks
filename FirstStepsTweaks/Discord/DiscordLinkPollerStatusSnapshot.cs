using System;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkPollerStatusSnapshot
    {
        public DateTime? LastSuccessfulPollUtc { get; set; }

        public string LastFailureSummary { get; set; } = string.Empty;

        public int LastProcessedPageCount { get; set; }

        public int LastProcessedMessageCount { get; set; }

        public bool LastPollReachedProcessingCap { get; set; }
    }
}
