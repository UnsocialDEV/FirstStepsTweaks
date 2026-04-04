using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordDebugStateReader
    {
        private readonly IDiscordLinkedAccountStore linkedAccountStore;
        private readonly IPendingDiscordLinkCodeStore pendingCodeStore;
        private readonly IDiscordLinkRewardStateStore rewardStateStore;
        private readonly IDiscordLastMessageStore relayCursorStore;
        private readonly IDiscordLinkLastMessageStore linkCursorStore;
        private readonly DiscordLinkPollerStatusTracker linkPollerStatusTracker;

        public DiscordDebugStateReader(
            IDiscordLinkedAccountStore linkedAccountStore,
            IPendingDiscordLinkCodeStore pendingCodeStore,
            IDiscordLinkRewardStateStore rewardStateStore,
            IDiscordLastMessageStore relayCursorStore,
            IDiscordLinkLastMessageStore linkCursorStore,
            DiscordLinkPollerStatusTracker linkPollerStatusTracker)
        {
            this.linkedAccountStore = linkedAccountStore;
            this.pendingCodeStore = pendingCodeStore;
            this.rewardStateStore = rewardStateStore;
            this.relayCursorStore = relayCursorStore;
            this.linkCursorStore = linkCursorStore;
            this.linkPollerStatusTracker = linkPollerStatusTracker;
        }

        public DiscordDebugStateSnapshot Capture(DateTime nowUtc)
        {
            DiscordLinkPollerStatusSnapshot linkPollerStatus = linkPollerStatusTracker.Capture();
            return new DiscordDebugStateSnapshot
            {
                LinkedAccounts = new Dictionary<string, string>(linkedAccountStore.GetAllLinkedDiscordUserIds(), StringComparer.OrdinalIgnoreCase),
                PendingCodes = new Dictionary<string, PendingDiscordLinkCodeRecord>(pendingCodeStore.GetPendingCodeRecords(nowUtc), StringComparer.OrdinalIgnoreCase),
                ClaimedRewardPlayerUids = rewardStateStore.GetClaimedPlayerUids(),
                PendingRewardPlayerUids = rewardStateStore.GetPendingRewardPlayerUids(),
                RelayCursorMessageId = relayCursorStore.Load() ?? string.Empty,
                LinkCursorMessageId = linkCursorStore.Load() ?? string.Empty,
                LinkPollLastSuccessfulUtc = linkPollerStatus.LastSuccessfulPollUtc,
                LinkPollLastFailureSummary = linkPollerStatus.LastFailureSummary,
                LinkPollLastProcessedPageCount = linkPollerStatus.LastProcessedPageCount,
                LinkPollLastProcessedMessageCount = linkPollerStatus.LastProcessedMessageCount,
                LinkPollLastPollReachedProcessingCap = linkPollerStatus.LastPollReachedProcessingCap
            };
        }

        public string Format(DiscordDebugStateSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Discord debug state");
            builder.AppendLine($"- linkedAccounts: {snapshot.LinkedAccounts.Count}");
            foreach (KeyValuePair<string, string> pair in snapshot.LinkedAccounts.OrderBy(pair => pair.Key))
            {
                builder.AppendLine($"  - {pair.Key} => {pair.Value}");
            }

            builder.AppendLine($"- pendingCodes: {snapshot.PendingCodes.Count}");
            foreach (KeyValuePair<string, PendingDiscordLinkCodeRecord> pair in snapshot.PendingCodes.OrderBy(pair => pair.Key))
            {
                builder.AppendLine($"  - {pair.Key}: playerUid={pair.Value.PlayerUid}, expiresUtcTicks={pair.Value.ExpiresAtUtcTicks}");
            }

            builder.AppendLine($"- claimedRewards: {snapshot.ClaimedRewardPlayerUids.Count}");
            foreach (string playerUid in snapshot.ClaimedRewardPlayerUids.OrderBy(value => value))
            {
                builder.AppendLine($"  - {playerUid}");
            }

            builder.AppendLine($"- pendingRewards: {snapshot.PendingRewardPlayerUids.Count}");
            foreach (string playerUid in snapshot.PendingRewardPlayerUids.OrderBy(value => value))
            {
                builder.AppendLine($"  - {playerUid}");
            }

            builder.AppendLine($"- relayCursorMessageId: {FormatNullable(snapshot.RelayCursorMessageId)}");
            builder.AppendLine($"- linkCursorMessageId: {FormatNullable(snapshot.LinkCursorMessageId)}");
            builder.AppendLine($"- linkPollLastSuccessfulUtc: {FormatNullable(snapshot.LinkPollLastSuccessfulUtc)}");
            builder.AppendLine($"- linkPollLastFailureSummary: {FormatNullable(snapshot.LinkPollLastFailureSummary)}");
            builder.AppendLine($"- linkPollLastProcessedPageCount: {snapshot.LinkPollLastProcessedPageCount}");
            builder.AppendLine($"- linkPollLastProcessedMessageCount: {snapshot.LinkPollLastProcessedMessageCount}");
            builder.AppendLine($"- linkPollLastPollReachedProcessingCap: {snapshot.LinkPollLastPollReachedProcessingCap}");
            return builder.ToString().TrimEnd();
        }

        private static string FormatNullable(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unset" : value;
        }

        private static string FormatNullable(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("u") : "unset";
        }
    }
}
