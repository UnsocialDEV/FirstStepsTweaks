using System;
using System.Collections.Generic;
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
        private readonly DonatorPrivilegeCatalog privilegeCatalog;
        private readonly DonatorFeaturePrivilegeResolver featurePrivilegeResolver;
        private readonly IPlayerPrivilegeMutator privilegeMutator;
        private readonly IPlayerMessenger messenger;

        public PlayerDonatorRoleSyncService(
            ICoreServerAPI api,
            DiscordBridgeConfig config,
            IDiscordLinkedAccountStore linkedAccountStore,
            IDiscordMemberRoleClient memberRoleClient,
            DiscordRoleNameResolver roleNameResolver,
            DiscordDonatorRolePlanner planner,
            DonatorPrivilegeCatalog privilegeCatalog,
            DonatorFeaturePrivilegeResolver featurePrivilegeResolver,
            IPlayerPrivilegeMutator privilegeMutator,
            IPlayerMessenger messenger)
        {
            this.api = api;
            this.config = config;
            this.linkedAccountStore = linkedAccountStore;
            this.memberRoleClient = memberRoleClient;
            this.roleNameResolver = roleNameResolver;
            this.planner = planner;
            this.privilegeCatalog = privilegeCatalog;
            this.featurePrivilegeResolver = featurePrivilegeResolver;
            this.privilegeMutator = privilegeMutator;
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

            RevokeNonTargetPrivileges(player, targetPrivilege: null);
            SyncFeaturePrivileges(player, targetPrivilege: null);
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
            bool changed = RevokeNonTargetPrivileges(player, plan.TargetPrivilege);
            changed |= SyncFeaturePrivileges(player, plan.TargetPrivilege);

            if (!string.IsNullOrWhiteSpace(plan.TargetPrivilege) && !player.HasPrivilege(plan.TargetPrivilege))
            {
                privilegeMutator.Grant(player, plan.TargetPrivilege);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            messenger.SendInfo(
                player,
                "Discord donator role synced.",
                GlobalConstants.InfoLogChatGroup,
                (int)EnumChatType.Notification);
        }

        private bool RevokeNonTargetPrivileges(IServerPlayer player, string targetPrivilege)
        {
            bool changed = false;

            foreach (string privilege in privilegeCatalog.GetAllPrivileges())
            {
                if (string.Equals(privilege, targetPrivilege, StringComparison.OrdinalIgnoreCase) || !player.HasPrivilege(privilege))
                {
                    continue;
                }

                privilegeMutator.Revoke(player, privilege);
                changed = true;
            }

            return changed;
        }

        private bool SyncFeaturePrivileges(IServerPlayer player, string targetPrivilege)
        {
            bool changed = false;
            var grantedPrivileges = new HashSet<string>(
                featurePrivilegeResolver.ResolveGrantedPrivileges(targetPrivilege),
                StringComparer.OrdinalIgnoreCase);

            foreach (string privilege in featurePrivilegeResolver.GetManagedPrivileges())
            {
                bool shouldHavePrivilege = grantedPrivileges.Contains(privilege);
                bool hasPrivilege = player.HasPrivilege(privilege);

                if (shouldHavePrivilege && !hasPrivilege)
                {
                    privilegeMutator.Grant(player, privilege);
                    changed = true;
                    continue;
                }

                if (!shouldHavePrivilege && hasPrivilege)
                {
                    privilegeMutator.Revoke(player, privilege);
                    changed = true;
                }
            }

            return changed;
        }
    }
}
