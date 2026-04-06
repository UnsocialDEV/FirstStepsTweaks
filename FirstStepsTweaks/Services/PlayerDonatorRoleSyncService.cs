using System;
using System.Threading.Tasks;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Infrastructure.Messaging;
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
        private readonly DonatorRoleTransitionApplier roleTransitionApplier;
        private readonly LegacyDonatorPrivilegeCleaner legacyPrivilegeCleaner;
        private readonly AdminModePriorRoleUpdater adminModePriorRoleUpdater;
        private readonly IPlayerMessenger messenger;

        public PlayerDonatorRoleSyncService(
            ICoreServerAPI api,
            DiscordBridgeConfig config,
            IDiscordLinkedAccountStore linkedAccountStore,
            IDiscordMemberRoleClient memberRoleClient,
            DiscordRoleNameResolver roleNameResolver,
            DiscordDonatorRolePlanner planner,
            DonatorRoleTransitionApplier roleTransitionApplier,
            LegacyDonatorPrivilegeCleaner legacyPrivilegeCleaner,
            AdminModePriorRoleUpdater adminModePriorRoleUpdater,
            IPlayerMessenger messenger)
        {
            this.api = api;
            this.config = config;
            this.linkedAccountStore = linkedAccountStore;
            this.memberRoleClient = memberRoleClient;
            this.roleNameResolver = roleNameResolver;
            this.planner = planner;
            this.roleTransitionApplier = roleTransitionApplier;
            this.legacyPrivilegeCleaner = legacyPrivilegeCleaner;
            this.adminModePriorRoleUpdater = adminModePriorRoleUpdater;
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

            DonatorRoleTransitionResult transition = roleTransitionApplier.Apply(player, targetRoleCode: null);
            if (!transition.Succeeded)
            {
                return;
            }

            if (transition.Changed)
            {
                adminModePriorRoleUpdater.UpdateIfActive(player, transition.EffectiveRoleCode);
            }

            legacyPrivilegeCleaner.ClearManagedPrivileges(player);
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
            DonatorRoleTransitionResult transition = roleTransitionApplier.Apply(player, plan?.TargetRoleCode);
            if (!transition.Succeeded)
            {
                return;
            }

            if (transition.Changed)
            {
                adminModePriorRoleUpdater.UpdateIfActive(player, transition.EffectiveRoleCode);
            }

            bool cleanedLegacyPrivileges = legacyPrivilegeCleaner.ClearManagedPrivileges(player);

            if (!transition.Changed && !cleanedLegacyPrivileges)
            {
                return;
            }

            messenger.SendInfo(player, "Discord donator role synced.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
        }
    }
}
