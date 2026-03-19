using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Teleport;
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
        private readonly WarpCommands warpCommands;
        private readonly RtpCommands rtpCommands;
        private readonly TpaCommands tpaCommands;

        public TeleportFeature(ICoreServerAPI api, FirstStepsTweaksConfig config, FeatureRuntime runtime)
        {
            this.api = api;
            this.config = config;

            backCommands = new BackCommands(api, config, runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService);
            homeCommands = new HomeCommands(api, config, new HomeStore(), runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService);
            spawnCommands = new SpawnCommands(api, config, new SpawnStore(api), runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService);
            warpCommands = new WarpCommands(api, config, new WarpStore(api), runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService);
            rtpCommands = new RtpCommands(api, config, runtime.Messenger, runtime.BackLocationStore, runtime.TeleportWarmupService, new RtpCooldownStore());
            tpaCommands = new TpaCommands(
                api,
                config,
                runtime.Messenger,
                runtime.PlayerLookup,
                runtime.TeleportWarmupService,
                runtime.BackLocationStore,
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
