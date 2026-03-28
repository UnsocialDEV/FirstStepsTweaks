using System;
using System.Threading.Tasks;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class PlayerDonatorRoleSyncService
    {
        private readonly ICoreServerAPI api;
        private readonly DiscordBridgeConfig config;
        private readonly IDiscordLinkedAccountStore linkedAccountStore;
        private readonly IDiscordMemberRoleClient memberRoleClient;
        private readonly DiscordRoleNameResolver roleNameResolver;
        private readonly DiscordDonatorRolePlanner planner;
        private readonly IPlayerRoleCodeReader roleCodeReader;
        private readonly IPlayerRoleAssigner roleAssigner;
        private readonly IPlayerDefaultRoleResetter defaultRoleResetter;
        private readonly IPlayerMessenger messenger;

        public PlayerDonatorRoleSyncService(
            ICoreServerAPI api,
            DiscordBridgeConfig config,
            IDiscordLinkedAccountStore linkedAccountStore,
            IDiscordMemberRoleClient memberRoleClient,
            DiscordRoleNameResolver roleNameResolver,
            DiscordDonatorRolePlanner planner,
            IPlayerRoleCodeReader roleCodeReader,
            IPlayerRoleAssigner roleAssigner,
            IPlayerDefaultRoleResetter defaultRoleResetter,
            IPlayerMessenger messenger)
        {
            this.api = api;
            this.config = config;
            this.linkedAccountStore = linkedAccountStore;
            this.memberRoleClient = memberRoleClient;
            this.roleNameResolver = roleNameResolver;
            this.planner = planner;
            this.roleCodeReader = roleCodeReader;
            this.roleAssigner = roleAssigner;
            this.defaultRoleResetter = defaultRoleResetter;
            this.messenger = messenger;
        }

        public async void OnPlayerNowPlaying(IServerPlayer player)
        {
            try
            {
                await SyncAsync(player);
            }
            catch (Exception exception)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to sync Discord donator role for {player?.PlayerName}: {exception}");
            }
        }

        public async Task SyncAsync(IServerPlayer player)
        {
            if (!IsConfiguredForRoleSync() || player == null)
            {
                return;
            }

            string discordUserId = linkedAccountStore.GetLinkedDiscordUserId(player.PlayerUID);
            if (string.IsNullOrWhiteSpace(discordUserId))
            {
                return;
            }

            DiscordMemberRoles memberRoles = await memberRoleClient.GetMemberRolesAsync(config, discordUserId);
            DiscordDonatorRolePlan plan = planner.Plan(
                roleNameResolver.ResolveRoleNames(memberRoles.MemberRoleIds, memberRoles.GuildRoles));

            ApplyPlan(player, plan);
        }

        public void ClearDonatorRole(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            string currentRoleCode = roleCodeReader.Read(player);
            string defaultRoleCode = defaultRoleResetter.GetDefaultRoleCode();

            if (string.Equals(currentRoleCode, defaultRoleCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            defaultRoleResetter.Reset(player);
        }

        private bool IsConfiguredForRoleSync()
        {
            return config != null
                && config.EnableRoleSync
                && !string.IsNullOrWhiteSpace(config.BotToken)
                && !string.IsNullOrWhiteSpace(config.GuildId);
        }

        private void ApplyPlan(IServerPlayer player, DiscordDonatorRolePlan plan)
        {
            string currentRoleCode = roleCodeReader.Read(player);

            if (string.IsNullOrWhiteSpace(plan.TargetRoleCode))
            {
                string defaultRoleCode = defaultRoleResetter.GetDefaultRoleCode();
                if (string.Equals(currentRoleCode, defaultRoleCode, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                defaultRoleResetter.Reset(player);
            }
            else
            {
                if (string.Equals(currentRoleCode, plan.TargetRoleCode, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                roleAssigner.Assign(player, plan.TargetRoleCode);
            }

            messenger.SendInfo(
                player,
                "Discord donator role synced.",
                GlobalConstants.InfoLogChatGroup,
                (int)EnumChatType.Notification);
        }
    }
}
