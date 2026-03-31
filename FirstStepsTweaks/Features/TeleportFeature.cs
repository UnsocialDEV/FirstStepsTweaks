using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Teleport;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    public sealed class TeleportFeature : IFeatureModule
    {
        private readonly ICoreServerAPI api;
        private readonly FirstStepsTweaksConfig config;
        private readonly BackCommands backCommands;
        private readonly HomeCommands homeCommands;
        private readonly SpawnCommands spawnCommands;
        private readonly StormShelterCommands stormShelterCommands;
        private readonly StuckCommand stuckCommand;
        private readonly WarpCommands warpCommands;
        private readonly RtpCommands rtpCommands;
        private readonly TpaCommands tpaCommands;

        public TeleportFeature(ICoreServerAPI api, FirstStepsTweaksConfig config, FeatureRuntime runtime)
        {
            this.api = api;
            this.config = config;
            var homeStore = new HomeStore();
            var homeLimitResolver = new PlayerHomeLimitResolver();
            var warmupResolver = new PlayerTeleportWarmupResolver();
            var homeSlotPolicy = new HomeSlotPolicy();
            var homeAccessPolicy = new HomeAccessPolicy();
            var homeDeletionTargetResolver = new HomeDeletionTargetResolver();

            backCommands = new BackCommands(api, config, runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, warmupResolver);
            homeCommands = new HomeCommands(api, config, homeStore, runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, homeLimitResolver, warmupResolver, homeSlotPolicy, homeAccessPolicy, homeDeletionTargetResolver);
            spawnCommands = new SpawnCommands(api, config, new SpawnStore(api), runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, warmupResolver);
            var stormShelterStore = new StormShelterStore(api);
            stormShelterCommands = new StormShelterCommands(
                api,
                stormShelterStore,
                runtime.Messenger,
                new StormShelterTeleportService(stormShelterStore, runtime.BackLocationStore));
            stuckCommand = new StuckCommand(
                api,
                config,
                runtime.Messenger,
                runtime.BackLocationStore,
                runtime.TeleportWarmupService,
                warmupResolver,
                new LandClaimEscapeService(runtime.LandClaimAccessor, new TeleportColumnSafetyScanner(api)));
            warpCommands = new WarpCommands(api, config, new WarpStore(api), runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, warmupResolver);
            rtpCommands = new RtpCommands(api, config, runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, warmupResolver, new RtpCooldownStore());
            tpaCommands = new TpaCommands(
                api,
                config,
                runtime.Messenger,
                runtime.PlayerLookup,
                runtime.TeleportWarmupService,
                runtime.BackLocationStore,
                warmupResolver,
                new TpaPreferenceStore(),
                new TpaRequestStore());
        }

        public void Register()
        {
            if (config.Features.EnableBackCommand)
            {
                api.Event.OnEntityDeath += backCommands.OnEntityDeath;
                backCommands.Register();
            }

            if (config.Features.EnableHomeCommands)
            {
                homeCommands.Register();
            }

            if (config.Features.EnableSpawnCommands)
            {
                spawnCommands.Register();
            }

            if (config.Features.EnableStormShelterCommands)
            {
                stormShelterCommands.Register();
            }

            if (config.Features.EnableStuckCommand)
            {
                stuckCommand.Register();
            }

            if (config.Features.EnableWarpCommands)
            {
                warpCommands.Register();
            }

            if (config.Features.EnableRtpCommand)
            {
                rtpCommands.Register();
            }

            if (config.Features.EnableTpaCommands)
            {
                tpaCommands.Register();
            }
        }
    }
}
