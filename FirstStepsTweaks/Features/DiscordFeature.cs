using System;
using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Discord.Transport;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    public sealed class DiscordFeature : IFeatureModule
    {
        private readonly ICoreServerAPI api;
        private readonly PlayerDonatorRoleSyncService roleSyncService;
        private readonly Action registerDiscordBridge;
        private readonly Action registerLinkPoller;
        private readonly Action registerLinkCommands;
        private readonly Action registerDiscordCommands;
        private readonly Action registerPlayerNowPlayingHandlers;
        private readonly DiscordStartupCoordinator startupCoordinator;
        private bool runtimeHooksRegistered;

        public DiscordFeature(ICoreServerAPI api, FirstStepsTweaksConfig config, FeatureRuntime runtime)
        {
            this.api = api;
            DiscordBridgeConfig discordConfig = new DiscordConfigStore(api).Load();
            var linkedAccountStore = new DiscordLinkedAccountStore(api);
            var pendingCodeStore = new PendingDiscordLinkCodeStore(api);
            var linkCodeParser = new DiscordLinkCodeMessageParser();
            var linkService = new DiscordLinkService(
                linkedAccountStore,
                pendingCodeStore,
                linkCodeParser,
                discordConfig.LinkCodeExpiryMinutes);
            var webhookClient = new DiscordWebhookClient();
            var tierCatalog = new DonatorTierCatalog();
            var privilegeMutator = new PlayerPrivilegeMutator(api);
            var privilegeReader = new PlayerPrivilegeReader();
            var roleCodeReader = new PlayerRoleCodeReader();
            var roleAssigner = new PlayerRoleAssigner(api);
            var avatarService = new DiscordPlayerAvatarService(
                discordConfig,
                linkedAccountStore,
                new DiscordUserProfileClient(webhookClient),
                new DiscordAvatarUrlResolver());
            var discordBridge = new DiscordBridge(api, avatarService, new DiscordRelayMessageNormalizer());

            roleSyncService = new PlayerDonatorRoleSyncService(
                api,
                discordConfig,
                linkedAccountStore,
                new DiscordMemberRoleClient(webhookClient),
                new DiscordRoleNameResolver(),
                new DiscordDonatorRolePlanner(tierCatalog),
                new DonatorRoleTransitionApplier(
                    api,
                    roleCodeReader,
                    roleAssigner,
                    new PlayerDefaultRoleResetter(api, roleAssigner)),
                new LegacyDonatorPrivilegeCleaner(privilegeReader, privilegeMutator, tierCatalog),
                new AdminModePriorRoleUpdater(api, runtime.AdminModeStore),
                runtime.Messenger);

            var linkPoller = new DiscordLinkPoller(
                api,
                discordConfig,
                webhookClient,
                new DiscordLinkLastMessageStore(api),
                linkService,
                runtime.DiscordLinkRewardService,
                linkCodeParser,
                runtime.PlayerLookup,
                roleSyncService,
                runtime.Messenger,
                runtime.DiscordLinkPollerStatusTracker);

            var linkCommands = new DiscordLinkCommands(
                api,
                discordConfig,
                linkService,
                roleSyncService,
                runtime.Messenger);

            registerDiscordBridge = discordBridge.Register;
            registerLinkPoller = linkPoller.Register;
            registerLinkCommands = linkCommands.Register;
            registerDiscordCommands = config.Features.EnableDiscordCommand
                ? new DiscordCommands(api, config, runtime.Messenger).Register
                : Noop;
            registerPlayerNowPlayingHandlers = () => api.Event.PlayerNowPlaying += roleSyncService.OnPlayerNowPlaying;
            startupCoordinator = new DiscordStartupCoordinator(api);
        }

        internal DiscordFeature(
            ICoreServerAPI api,
            Action registerDiscordBridge,
            Action registerLinkPoller,
            Action registerLinkCommands,
            Action registerDiscordCommands,
            Action registerPlayerNowPlayingHandlers,
            DiscordStartupCoordinator startupCoordinator)
        {
            this.api = api;
            this.registerDiscordBridge = registerDiscordBridge;
            this.registerLinkPoller = registerLinkPoller;
            this.registerLinkCommands = registerLinkCommands;
            this.registerDiscordCommands = registerDiscordCommands;
            this.registerPlayerNowPlayingHandlers = registerPlayerNowPlayingHandlers;
            this.startupCoordinator = startupCoordinator;
        }

        public void Register()
        {
            registerDiscordCommands();
            registerLinkCommands();
            startupCoordinator.RunWhenWorldReady(RegisterRuntimeHooks);
        }

        private void RegisterRuntimeHooks()
        {
            if (runtimeHooksRegistered)
            {
                return;
            }

            runtimeHooksRegistered = true;
            registerDiscordBridge();
            registerLinkPoller();
            registerPlayerNowPlayingHandlers();
        }

        private static void Noop()
        {
        }
    }
}
