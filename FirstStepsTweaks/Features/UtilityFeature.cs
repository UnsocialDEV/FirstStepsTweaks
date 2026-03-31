using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    public sealed class UtilityFeature : IFeatureModule
    {
        private readonly ICoreServerAPI api;
        private readonly FirstStepsTweaksConfig config;
        private readonly FeatureRuntime runtime;

        public UtilityFeature(ICoreServerAPI api, FirstStepsTweaksConfig config, FeatureRuntime runtime)
        {
            this.api = api;
            this.config = config;
            this.runtime = runtime;
        }

        public void Register()
        {
            if (config.Features.EnableDebugCommand)
            {
                var joinHistoryStore = new JoinHistoryStore();
                var kitClaimStore = new KitClaimStore();
                var playtimeStore = new PlayerPlaytimeStore();
                var homeStore = new HomeStore();
                var tpaPreferenceStore = new TpaPreferenceStore();
                var spawnStore = new SpawnStore(api);
                var warpStore = new WarpStore(api);
                var linkedAccountStore = new DiscordLinkedAccountStore(api);
                var pendingCodeStore = new PendingDiscordLinkCodeStore(api);
                var rewardStateStore = new DiscordLinkRewardStateStore(api);
                var relayCursorStore = new DiscordLastMessageStore(api);
                var linkCursorStore = new DiscordLinkLastMessageStore(api);

                new DebugCommands(
                    api,
                    runtime.PlayerLookup,
                    runtime.Messenger,
                    joinHistoryStore,
                    kitClaimStore,
                    playtimeStore,
                    homeStore,
                    tpaPreferenceStore,
                    spawnStore,
                    warpStore,
                    runtime.GravestoneService,
                    linkedAccountStore,
                    pendingCodeStore,
                    rewardStateStore,
                    relayCursorStore,
                    linkCursorStore).Register();
            }

            if (config.Features.EnableKitCommands)
            {
                new KitCommands(api, config, new KitClaimStore(), new KitItemConsolidator(), runtime.Messenger).Register();
            }

            if (config.Features.EnableUtilityCommands)
            {
                new WhosOnlineCommand(api, config).Register();
                new WindCommand(api, config).Register();
                new AdminVitalsCommands(api).Register();
            }
        }
    }
}
