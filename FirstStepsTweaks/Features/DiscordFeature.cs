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
        private readonly FirstStepsTweaksConfig config;
        private readonly FeatureRuntime runtime;
        private readonly DiscordBridge discordBridge;
        private readonly DiscordLinkPoller linkPoller;
        private readonly PlayerDonatorPrivilegeSyncService privilegeSyncService;
        private readonly DiscordLinkCommands linkCommands;

        public DiscordFeature(ICoreServerAPI api, FirstStepsTweaksConfig config, FeatureRuntime runtime)
        {
            this.api = api;
            this.config = config;
            this.runtime = runtime;
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
            var privilegeCatalog = new DonatorPrivilegeCatalog();
            var avatarService = new DiscordPlayerAvatarService(
                discordConfig,
                linkedAccountStore,
                new DiscordUserProfileClient(webhookClient),
                new DiscordAvatarUrlResolver());
            discordBridge = new DiscordBridge(api, avatarService, new DiscordRelayMessageNormalizer());

            privilegeSyncService = new PlayerDonatorPrivilegeSyncService(
                api,
                discordConfig,
                linkedAccountStore,
                new DiscordMemberRoleClient(webhookClient),
                new DiscordRoleNameResolver(),
                new DiscordDonatorPrivilegePlanner(privilegeCatalog),
                new PlayerPrivilegeMutator(api),
                privilegeCatalog,
                runtime.Messenger);

            linkPoller = new DiscordLinkPoller(
                api,
                discordConfig,
                webhookClient,
                new DiscordLinkLastMessageStore(api),
                linkService,
                linkCodeParser,
                runtime.PlayerLookup,
                runtime.Messenger);

            linkCommands = new DiscordLinkCommands(
                api,
                discordConfig,
                linkService,
                privilegeSyncService,
                runtime.Messenger);
        }

        public void Register()
        {
            api.Event.PlayerChat += discordBridge.OnPlayerChat;
            api.Event.PlayerNowPlaying += privilegeSyncService.OnPlayerNowPlaying;
            linkPoller.Register();

            if (config.Features.EnableDiscordCommand)
            {
                new DiscordCommands(api, config, runtime.Messenger).Register();
            }

            linkCommands.Register();
        }
    }
}
