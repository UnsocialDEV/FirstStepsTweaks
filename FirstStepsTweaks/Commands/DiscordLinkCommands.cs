using System;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class DiscordLinkCommands
    {
        private readonly ICoreServerAPI api;
        private readonly DiscordBridgeConfig discordConfig;
        private readonly DiscordLinkService linkService;
        private readonly PlayerDonatorPrivilegeSyncService privilegeSyncService;
        private readonly IPlayerMessenger messenger;

        public DiscordLinkCommands(
            ICoreServerAPI api,
            DiscordBridgeConfig discordConfig,
            DiscordLinkService linkService,
            PlayerDonatorPrivilegeSyncService privilegeSyncService,
            IPlayerMessenger messenger)
        {
            this.api = api;
            this.discordConfig = discordConfig;
            this.linkService = linkService;
            this.privilegeSyncService = privilegeSyncService;
            this.messenger = messenger;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("discordlink")
                .WithDescription("Creates a one-time code to link your Discord account")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(DiscordLink);

            api.ChatCommands
                .Create("discordunlink")
                .WithDescription("Removes your linked Discord account")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(DiscordUnlink);
        }

        private TextCommandResult DiscordLink(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            if (!IsLinkConfigured())
            {
                messenger.SendIngameError(player, "discordlink-not-configured", "Discord linking is not configured on this server.");
                return TextCommandResult.Error("Discord linking is not configured.");
            }

            DiscordLinkCodeIssue issue = linkService.CreateLinkCode(player.PlayerUID, DateTime.UtcNow);
            string message = $"Post code {issue.Code} in the Discord link channel to link your account. Code expires at {issue.ExpiresAtUtc:u}.";

            messenger.SendDual(
                player,
                message,
                GlobalConstants.InfoLogChatGroup,
                (int)EnumChatType.Notification,
                GlobalConstants.GeneralChatGroup,
                (int)EnumChatType.CommandSuccess);

            return TextCommandResult.Success();
        }

        private TextCommandResult DiscordUnlink(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            linkService.UnlinkPlayer(player.PlayerUID);
            privilegeSyncService.ClearDonatorPrivileges(player);

            messenger.SendDual(
                player,
                "Discord account unlinked and synced donator roles removed.",
                (int)EnumChatType.Notification,
                (int)EnumChatType.CommandSuccess);

            return TextCommandResult.Success();
        }

        private bool IsLinkConfigured()
        {
            return discordConfig != null
                && !string.IsNullOrWhiteSpace(discordConfig.BotToken)
                && !string.IsNullOrWhiteSpace(discordConfig.LinkChannelId);
        }
    }
}
