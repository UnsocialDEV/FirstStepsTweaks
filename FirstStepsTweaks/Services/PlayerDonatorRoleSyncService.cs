using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly DiscordDonatorPrivilegePlanner planner;
        private readonly IPlayerPrivilegeReader privilegeReader;
        private readonly IPlayerPrivilegeMutator privilegeMutator;
        private readonly DonatorPrivilegeCatalog privilegeCatalog;
        private readonly IPlayerMessenger messenger;

        public PlayerDonatorRoleSyncService(
            ICoreServerAPI api,
            DiscordBridgeConfig config,
            IDiscordLinkedAccountStore linkedAccountStore,
            IDiscordMemberRoleClient memberRoleClient,
            DiscordRoleNameResolver roleNameResolver,
            DiscordDonatorPrivilegePlanner planner,
            IPlayerPrivilegeReader privilegeReader,
            IPlayerPrivilegeMutator privilegeMutator,
            DonatorPrivilegeCatalog privilegeCatalog,
            IPlayerMessenger messenger)
        {
            this.api = api;
            this.config = config;
            this.linkedAccountStore = linkedAccountStore;
            this.memberRoleClient = memberRoleClient;
            this.roleNameResolver = roleNameResolver;
            this.planner = planner;
            this.privilegeReader = privilegeReader;
            this.privilegeMutator = privilegeMutator;
            this.privilegeCatalog = privilegeCatalog;
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
            DiscordDonatorPrivilegePlan plan = planner.Plan(
                roleNameResolver.ResolveRoleNames(memberRoles.MemberRoleIds, memberRoles.GuildRoles));

            ApplyPlan(player, plan);
        }

        public void ClearDonatorRole(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            IReadOnlyCollection<string> currentPrivileges = GetCurrentManagedPrivileges(player);
            if (currentPrivileges.Count == 0)
            {
                return;
            }

            foreach (string privilege in currentPrivileges)
            {
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
            HashSet<string> currentPrivileges = new HashSet<string>(GetCurrentManagedPrivileges(player), StringComparer.OrdinalIgnoreCase);
            HashSet<string> targetPrivileges = new HashSet<string>(plan.TargetPrivileges ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            bool changed = false;

            foreach (string privilege in currentPrivileges)
            {
                if (targetPrivileges.Contains(privilege))
                {
                    continue;
                }

                privilegeMutator.Revoke(player, privilege);
                changed = true;
            }

            foreach (string privilege in targetPrivileges)
            {
                if (currentPrivileges.Contains(privilege))
                {
                    continue;
                }

                privilegeMutator.Grant(player, privilege);
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

        private IReadOnlyCollection<string> GetCurrentManagedPrivileges(IServerPlayer player)
        {
            return privilegeCatalog.GetAllPrivileges()
                .Where(privilege => privilegeReader.HasPrivilege(player, privilege))
                .ToArray();
        }
    }
}
