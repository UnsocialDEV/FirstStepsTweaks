using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
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
                new DebugCommands(api).Register();
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
