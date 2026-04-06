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
                var homeStore = new HomeStore(new HomeDataSerializer(), new HomeNameNormalizer(), new DefaultHomeResolver(), runtime.CoordinateReader);
                var tpaPreferenceStore = new TpaPreferenceStore();
                var spawnStore = new SpawnStore(api, runtime.CoordinateReader);
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
                    runtime.CoordinateDisplayFormatter,
                    linkedAccountStore,
                    pendingCodeStore,
                    rewardStateStore,
                    relayCursorStore,
                    linkCursorStore,
                    runtime.DiscordLinkPollerStatusTracker).Register();
            }

            if (config.Features.EnableKitCommands)
            {
                new KitCommands(api, config, new KitClaimStore(), new KitItemConsolidator(), runtime.Messenger).Register();
            }

            if (config.Features.EnableUtilityCommands)
            {
                new StaffCommands(api, runtime.StaffAssignmentStore, runtime.StaffStatusReader, runtime.StaffPrivilegeSyncService, runtime.PlayerLookup).Register();
                new WhosOnlineCommand(api, runtime.StaffStatusReader).Register();
                new WindCommand(api, config, runtime.CoordinateReader).Register();
                new AdminVitalsCommands(api).Register();
                new AdminModeCommand(api, runtime.AdminModeService).Register();
            }
        }
    }
}
