using System;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkPollerStatusTracker
    {
        private readonly object syncRoot = new object();
        private DateTime? lastSuccessfulPollUtc;
        private string lastFailureSummary = string.Empty;
        private int lastProcessedPageCount;
        private int lastProcessedMessageCount;
        private bool lastPollReachedProcessingCap;

        public void RecordSuccess(DateTime occurredAtUtc, int processedPageCount, int processedMessageCount, bool reachedProcessingCap)
        {
            lock (syncRoot)
            {
                lastSuccessfulPollUtc = occurredAtUtc;
                lastFailureSummary = string.Empty;
                lastProcessedPageCount = Math.Max(0, processedPageCount);
                lastProcessedMessageCount = Math.Max(0, processedMessageCount);
                lastPollReachedProcessingCap = reachedProcessingCap;
            }
        }

        public void RecordFailure(string failureSummary)
        {
            lock (syncRoot)
            {
                lastFailureSummary = failureSummary ?? string.Empty;
                lastProcessedPageCount = 0;
                lastProcessedMessageCount = 0;
                lastPollReachedProcessingCap = false;
            }
        }

        public DiscordLinkPollerStatusSnapshot Capture()
        {
            lock (syncRoot)
            {
                return new DiscordLinkPollerStatusSnapshot
                {
                    LastSuccessfulPollUtc = lastSuccessfulPollUtc,
                    LastFailureSummary = lastFailureSummary,
                    LastProcessedPageCount = lastProcessedPageCount,
                    LastProcessedMessageCount = lastProcessedMessageCount,
                    LastPollReachedProcessingCap = lastPollReachedProcessingCap
                };
            }
        }
    }
}
