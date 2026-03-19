using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Discord;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    public sealed class DiscordFeature : IFeatureModule
    {
        private readonly ICoreServerAPI api;
        private readonly FirstStepsTweaksConfig config;
        private readonly FeatureRuntime runtime;
        private readonly DiscordBridge discordBridge;

        public DiscordFeature(ICoreServerAPI api, FirstStepsTweaksConfig config, FeatureRuntime runtime)
        {
            this.api = api;
            this.config = config;
            this.runtime = runtime;
            discordBridge = new DiscordBridge(api);
        }

        public void Register()
        {
            api.Event.PlayerChat += discordBridge.OnPlayerChat;

            if (config.Features.EnableDiscordCommand)
            {
                new DiscordCommands(api, config, runtime.Messenger).Register();
            }
        }
    }
}
