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
        private readonly AdminTeleportCommands adminTeleportCommands;

        public TeleportFeature(ICoreServerAPI api, FirstStepsTweaksConfig config, FeatureRuntime runtime)
        {
            this.api = api;
            this.config = config;
            var homeStore = new HomeStore(new HomeDataSerializer(), new HomeNameNormalizer(), new DefaultHomeResolver(), runtime.CoordinateReader);
            var homeLimitResolver = new PlayerHomeLimitResolver();
            var warmupResolver = new PlayerTeleportWarmupResolver();
            var homeSlotPolicy = new HomeSlotPolicy();
            var homeAccessPolicy = new HomeAccessPolicy();
            var homeDeletionTargetResolver = new HomeDeletionTargetResolver();
            var playerTeleporter = new PlayerTeleporter();
            var tpaPreferenceStore = new TpaPreferenceStore();
            var tpaRequestStore = new TpaRequestStore();
            var tpaMessageFormatter = new TpaRequestMessageFormatter();
            var tpaTeleportService = new TpaTeleportService(
                config,
                runtime.BackLocationStore,
                runtime.TeleportWarmupService,
                playerTeleporter,
                warmupResolver,
                runtime.CoordinateReader);

            backCommands = new BackCommands(api, config, runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, warmupResolver, runtime.CoordinateReader);
            homeCommands = new HomeCommands(api, config, homeStore, runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, homeLimitResolver, warmupResolver, homeSlotPolicy, homeAccessPolicy, homeDeletionTargetResolver);
            spawnCommands = new SpawnCommands(api, config, new SpawnStore(api, runtime.CoordinateReader), runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, warmupResolver);
            var stormShelterStore = new StormShelterStore(api, runtime.CoordinateReader);
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
                new LandClaimEscapeService(runtime.LandClaimAccessor, new TeleportColumnSafetyScanner(api), new LandClaimEscapePlanner(), runtime.CoordinateReader));
            warpCommands = new WarpCommands(api, config, new WarpStore(api), runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, warmupResolver, runtime.CoordinateReader);
            var rtpConfig = config?.Rtp ?? new RtpConfig();
            var rtpPlanner = new RtpColumnPlanner(rtpConfig);
            var rtpResolver = new RtpDestinationResolver(
                rtpConfig,
                rtpPlanner,
                new RtpColumnSafetyScanner(api),
                runtime.LandClaimAccessor,
                runtime.CoordinateReader);
            rtpCommands = new RtpCommands(
                api,
                new RtpTeleportService(
                    config,
                    runtime.Messenger,
                    runtime.BackLocationStore,
                    runtime.TeleportWarmupService,
                    playerTeleporter,
                    warmupResolver,
                    new RtpCooldownStore(),
                    rtpResolver));
            adminTeleportCommands = new AdminTeleportCommands(
                api,
                new AdminTeleportService(runtime.PlayerLookup, runtime.BackLocationStore, playerTeleporter, runtime.Messenger, runtime.CoordinateReader));
            tpaCommands = new TpaCommands(
                api,
                new TpaRequestCreator(api, config, runtime.Messenger, runtime.PlayerLookup, tpaPreferenceStore, tpaRequestStore, tpaMessageFormatter),
                new TpaRequestAccepter(api, runtime.Messenger, runtime.PlayerLookup, tpaRequestStore, tpaTeleportService, tpaMessageFormatter),
                new TpaRequestDenier(api, runtime.Messenger, runtime.PlayerLookup, tpaRequestStore, tpaMessageFormatter),
                new TpaRequestCanceller(api, runtime.Messenger, runtime.PlayerLookup, tpaRequestStore, tpaMessageFormatter),
                new TpaToggleService(api, runtime.Messenger, runtime.PlayerLookup, tpaPreferenceStore, tpaRequestStore, tpaMessageFormatter));
        }

        public void Register()
        {
            adminTeleportCommands.Register();

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
