using System;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class PlayerDonatorPrivilegeSyncService
    {
        private readonly ICoreServerAPI api;
        private readonly DiscordBridgeConfig config;
        private readonly IDiscordLinkedAccountStore linkedAccountStore;
        private readonly DiscordMemberRoleClient memberRoleClient;
        private readonly DiscordRoleNameResolver roleNameResolver;
        private readonly DiscordDonatorPrivilegePlanner planner;
        private readonly PlayerPrivilegeMutator privilegeMutator;
        private readonly DonatorPrivilegeCatalog privilegeCatalog;
        private readonly IPlayerMessenger messenger;

        public PlayerDonatorPrivilegeSyncService(
            ICoreServerAPI api,
            DiscordBridgeConfig config,
            IDiscordLinkedAccountStore linkedAccountStore,
            DiscordMemberRoleClient memberRoleClient,
            DiscordRoleNameResolver roleNameResolver,
            DiscordDonatorPrivilegePlanner planner,
            PlayerPrivilegeMutator privilegeMutator,
            DonatorPrivilegeCatalog privilegeCatalog,
            IPlayerMessenger messenger)
        {
            this.api = api;
            this.config = config;
            this.linkedAccountStore = linkedAccountStore;
            this.memberRoleClient = memberRoleClient;
            this.roleNameResolver = roleNameResolver;
            this.planner = planner;
            this.privilegeMutator = privilegeMutator;
            this.privilegeCatalog = privilegeCatalog;
            this.messenger = messenger;
        }

        public async void OnPlayerNowPlaying(IServerPlayer player)
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

            try
            {
                DiscordMemberRoles memberRoles = await memberRoleClient.GetMemberRolesAsync(config, discordUserId);
                DiscordDonatorPrivilegePlan plan = planner.Plan(
                    roleNameResolver.ResolveRoleNames(memberRoles.MemberRoleIds, memberRoles.GuildRoles),
                    player.HasPrivilege);

                ApplyPlan(player, plan);
            }
            catch (Exception exception)
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to sync Discord donator privileges for {player.PlayerName}: {exception}");
            }
        }

        public void ClearDonatorPrivileges(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            foreach (string privilege in privilegeCatalog.GetAllPrivileges())
            {
                if (!player.HasPrivilege(privilege))
                {
                    continue;
                }

                privilegeMutator.Revoke(player, privilege);
            }
        }

        private bool IsConfiguredForRoleSync()
        {
            return config != null
                && config.EnableRoleSync
                && !string.IsNullOrWhiteSpace(config.BotToken)
                && !string.IsNullOrWhiteSpace(config.GuildId);
        }

        private void ApplyPlan(IServerPlayer player, DiscordDonatorPrivilegePlan plan)
        {
            foreach (string privilege in plan.PrivilegesToGrant)
            {
                privilegeMutator.Grant(player, privilege);
            }

            foreach (string privilege in plan.PrivilegesToRevoke)
            {
                privilegeMutator.Revoke(player, privilege);
            }

            if (plan.PrivilegesToGrant.Count == 0 && plan.PrivilegesToRevoke.Count == 0)
            {
                return;
            }

            messenger.SendInfo(
                player,
                "Discord donator roles synced.",
                GlobalConstants.InfoLogChatGroup,
                (int)EnumChatType.Notification);
        }
    }
}
