using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordConfigStore
    {
        private const string DiscordConfigFileName = "firststepstweaks.discord.json";
        private const string LegacyDiscordConfigFileName = "FirstStepsTweaks.Discord.json";

        private readonly ICoreServerAPI api;

        public DiscordConfigStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public DiscordBridgeConfig Load()
        {
            DiscordBridgeConfig config = api.LoadModConfig<DiscordBridgeConfig>(DiscordConfigFileName);
            if (config != null)
            {
                return config;
            }

            config = api.LoadModConfig<DiscordBridgeConfig>(LegacyDiscordConfigFileName);
            if (config != null)
            {
                api.StoreModConfig(config, DiscordConfigFileName);
                api.Logger.Notification($"[FirstStepsTweaks] Migrated Discord config file '{LegacyDiscordConfigFileName}' to '{DiscordConfigFileName}' for cross-platform compatibility.");
                return config;
            }

            config = new DiscordBridgeConfig();
            api.StoreModConfig(config, DiscordConfigFileName);
            api.Logger.Warning($"[FirstStepsTweaks] Created Discord config file {DiscordConfigFileName}. Fill it in and restart.");
            return config;
        }
    }
}
