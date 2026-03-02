using FirstStepsTweaks.ChiselTransfer;
using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks
{
    public class FirstStepsTweaks : ModSystem
    {
        private DiscordBridge discord;
        private JoinService joinService;
        private CorpseService corpseService;

        public override void StartServerSide(ICoreServerAPI api)
        {
            var config = LoadConfig(api);

            discord = new DiscordBridge(api);
            joinService = new JoinService(api, config);

            if (config.Features.EnableCorpseService)
            {
                corpseService = new CorpseService(api, config);
                api.Event.OnEntityDeath += corpseService.OnEntityDeath;
                api.Event.DidBreakBlock += corpseService.OnBlockBroken;
            }

            if (config.Features.EnableBackCommand)
            {
                api.Event.OnEntityDeath += BackCommands.OnEntityDeath;
                BackCommands.Register(api, config);
            }

            if (config.Features.EnableJoinBroadcasts)
            {
                api.Event.PlayerJoin += joinService.OnPlayerJoin;
            }

            api.Event.PlayerChat += discord.OnPlayerChat;

            if (config.Features.EnableDebugCommand) DebugCommands.Register(api);
            if (config.Features.EnableDiscordCommand) DiscordCommands.Register(api, config);
            if (config.Features.EnableSpawnCommands) SpawnCommands.Register(api, config);
            if (config.Features.EnableHomeCommands) HomeCommands.Register(api, config);
            if (config.Features.EnableKitCommands) KitCommands.Register(api, config);
            if (config.Features.EnableTpaCommands) TpaCommands.Register(api, config);
            if (config.Features.EnableWarpCommands) WarpCommands.Register(api, config);
            if (config.Features.EnableUtilityCommands) UtilityCommands.Register(api, config);
            if (config.Features.EnableCorpseAdminCommands && corpseService != null)
            {
                CorpseAdminCommands.Register(api, corpseService);
            }

            if (config.Features.EnableChiselTransferCommands)
            {
                ChiselCommandHandlers.Register(api, config);
            }
        }

        private FirstStepsTweaksConfig LoadConfig(ICoreServerAPI api)
        {
            const string fileName = "FirstStepsTweaks.json";
            var config = api.LoadModConfig<FirstStepsTweaksConfig>(fileName);
            if (config == null)
            {
                config = new FirstStepsTweaksConfig();
                api.StoreModConfig(config, fileName);
                api.Logger.Warning("[FirstStepsTweaks] Created config file FirstStepsTweaks.json. Review and adjust as needed.");
            }

            return config;
        }
    }
}
