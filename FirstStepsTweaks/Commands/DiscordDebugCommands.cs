using System;
using System.Text;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Infrastructure.Messaging;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class DiscordDebugCommands
    {
        private readonly DiscordDebugStateReader reader;
        private readonly IDiscordLinkedAccountStore linkedAccountStore;
        private readonly IPendingDiscordLinkCodeStore pendingCodeStore;
        private readonly IDiscordLinkRewardStateStore rewardStateStore;
        private readonly IDiscordLastMessageStore relayCursorStore;
        private readonly IDiscordLinkLastMessageStore linkCursorStore;
        private readonly IPlayerMessenger messenger;

        public DiscordDebugCommands(
            DiscordDebugStateReader reader,
            IDiscordLinkedAccountStore linkedAccountStore,
            IPendingDiscordLinkCodeStore pendingCodeStore,
            IDiscordLinkRewardStateStore rewardStateStore,
            IDiscordLastMessageStore relayCursorStore,
            IDiscordLinkLastMessageStore linkCursorStore,
            IPlayerMessenger messenger)
        {
            this.reader = reader;
            this.linkedAccountStore = linkedAccountStore;
            this.pendingCodeStore = pendingCodeStore;
            this.rewardStateStore = rewardStateStore;
            this.relayCursorStore = relayCursorStore;
            this.linkCursorStore = linkCursorStore;
            this.messenger = messenger;
        }

        public TextCommandResult Inspect(TextCommandCallingArgs args)
        {
            SendInfo(args.Caller.Player, reader.Format(reader.Capture(DateTime.UtcNow)));
            return TextCommandResult.Success();
        }

        public TextCommandResult SetLink(TextCommandCallingArgs args)
        {
            string playerUid = args[0] as string;
            string discordUserId = args[1] as string;
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(discordUserId))
            {
                SendDual(args.Caller.Player, "playerUid and discordUserId are required.");
                return TextCommandResult.Success();
            }

            linkedAccountStore.SetLinkedDiscordUserId(playerUid, discordUserId);
            SendDual(args.Caller.Player, $"Linked playerUid {playerUid} to Discord user {discordUserId}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult ClearLink(TextCommandCallingArgs args)
        {
            string playerUid = args[0] as string;
            linkedAccountStore.ClearLinkedDiscordUserId(playerUid);
            SendDual(args.Caller.Player, $"Cleared Discord link for playerUid {playerUid}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult SetClaimed(TextCommandCallingArgs args)
        {
            return SetRewardState(args, claimedState: true);
        }

        public TextCommandResult SetPending(TextCommandCallingArgs args)
        {
            return SetRewardState(args, claimedState: false);
        }

        public TextCommandResult ListCodes(TextCommandCallingArgs args)
        {
            var pendingCodes = pendingCodeStore.GetPendingCodeRecords(DateTime.UtcNow);
            if (pendingCodes.Count == 0)
            {
                SendDual(args.Caller.Player, "No pending Discord link codes found.");
                return TextCommandResult.Success();
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Pending Discord link codes ({pendingCodes.Count}):");
            foreach (var pair in pendingCodes)
            {
                builder.AppendLine($"- {pair.Key}: playerUid={pair.Value.PlayerUid}, expiresUtcTicks={pair.Value.ExpiresAtUtcTicks}");
            }

            SendInfo(args.Caller.Player, builder.ToString().TrimEnd());
            return TextCommandResult.Success();
        }

        public TextCommandResult SetCode(TextCommandCallingArgs args)
        {
            string code = args[0] as string;
            string playerUid = args[1] as string;
            if (!DebugCommandSupport.TryParseLong(args[2] as string, out long expiresUtcTicks))
            {
                SendDual(args.Caller.Player, "expiresUtcTicks must be a whole number.");
                return TextCommandResult.Success();
            }

            pendingCodeStore.SaveCode(code, new PendingDiscordLinkCodeRecord(playerUid, expiresUtcTicks));
            SendDual(args.Caller.Player, $"Saved Discord link code {code} for playerUid {playerUid}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult RemoveCode(TextCommandCallingArgs args)
        {
            string code = args[0] as string;
            pendingCodeStore.RemoveCode(code);
            SendDual(args.Caller.Player, $"Removed Discord link code {code}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult ClearCodesForPlayer(TextCommandCallingArgs args)
        {
            string playerUid = args[0] as string;
            pendingCodeStore.RemoveCodesForPlayer(playerUid);
            SendDual(args.Caller.Player, $"Cleared pending link codes for playerUid {playerUid}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult SetCursor(TextCommandCallingArgs args)
        {
            string cursor = (args[0] as string ?? string.Empty).Trim().ToLowerInvariant();
            string messageId = args[1] as string;
            switch (cursor)
            {
                case "relay":
                    relayCursorStore.Save(messageId);
                    break;
                case "link":
                    linkCursorStore.Save(messageId);
                    break;
                default:
                    SendDual(args.Caller.Player, "Cursor must be relay or link.");
                    return TextCommandResult.Success();
            }

            SendDual(args.Caller.Player, $"Set {cursor} cursor to {messageId}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult ClearCursor(TextCommandCallingArgs args)
        {
            string cursor = (args[0] as string ?? string.Empty).Trim().ToLowerInvariant();
            switch (cursor)
            {
                case "relay":
                    relayCursorStore.Clear();
                    break;
                case "link":
                    linkCursorStore.Clear();
                    break;
                default:
                    SendDual(args.Caller.Player, "Cursor must be relay or link.");
                    return TextCommandResult.Success();
            }

            SendDual(args.Caller.Player, $"Cleared {cursor} cursor.");
            return TextCommandResult.Success();
        }

        private TextCommandResult SetRewardState(TextCommandCallingArgs args, bool claimedState)
        {
            string playerUid = args[0] as string;
            if (!DebugCommandSupport.TryParseBoolean(args[1] as string, out bool value))
            {
                SendDual(args.Caller.Player, "Value must be true or false.");
                return TextCommandResult.Success();
            }

            if (claimedState)
            {
                if (value)
                {
                    rewardStateStore.MarkClaimed(playerUid);
                }
                else
                {
                    rewardStateStore.ClearClaimed(playerUid);
                }

                SendDual(args.Caller.Player, $"Set claimed reward state for {playerUid} to {value}.");
                return TextCommandResult.Success();
            }

            if (value)
            {
                rewardStateStore.MarkPendingReward(playerUid);
            }
            else
            {
                rewardStateStore.ClearPendingReward(playerUid);
            }

            SendDual(args.Caller.Player, $"Set pending reward state for {playerUid} to {value}.");
            return TextCommandResult.Success();
        }

        private void SendDual(IPlayer caller, string message)
        {
            if (caller is IServerPlayer serverPlayer)
            {
                messenger.SendDual(serverPlayer, message, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            }
        }

        private void SendInfo(IPlayer caller, string message)
        {
            if (caller is IServerPlayer serverPlayer)
            {
                messenger.SendInfo(serverPlayer, message, GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            }
        }
    }
}
