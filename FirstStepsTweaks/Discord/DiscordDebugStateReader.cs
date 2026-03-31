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

        public DiscordDebugStateReader(
            IDiscordLinkedAccountStore linkedAccountStore,
            IPendingDiscordLinkCodeStore pendingCodeStore,
            IDiscordLinkRewardStateStore rewardStateStore,
            IDiscordLastMessageStore relayCursorStore,
            IDiscordLinkLastMessageStore linkCursorStore)
        {
            this.linkedAccountStore = linkedAccountStore;
            this.pendingCodeStore = pendingCodeStore;
            this.rewardStateStore = rewardStateStore;
            this.relayCursorStore = relayCursorStore;
            this.linkCursorStore = linkCursorStore;
        }

        public DiscordDebugStateSnapshot Capture(DateTime nowUtc)
        {
            return new DiscordDebugStateSnapshot
            {
                LinkedAccounts = new Dictionary<string, string>(linkedAccountStore.GetAllLinkedDiscordUserIds(), StringComparer.OrdinalIgnoreCase),
                PendingCodes = new Dictionary<string, PendingDiscordLinkCodeRecord>(pendingCodeStore.GetPendingCodeRecords(nowUtc), StringComparer.OrdinalIgnoreCase),
                ClaimedRewardPlayerUids = rewardStateStore.GetClaimedPlayerUids(),
                PendingRewardPlayerUids = rewardStateStore.GetPendingRewardPlayerUids(),
                RelayCursorMessageId = relayCursorStore.Load() ?? string.Empty,
                LinkCursorMessageId = linkCursorStore.Load() ?? string.Empty
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
            return builder.ToString().TrimEnd();
        }

        private static string FormatNullable(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unset" : value;
        }
    }
}
