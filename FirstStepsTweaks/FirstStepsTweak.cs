using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks
{
    public class FirstStepsTweaks : ModSystem
    {
        private ICoreServerAPI api;

        private DiscordBridge discord;
        private JoinService joinService;
        private CorpseService corpseService;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            // Services
            discord = new DiscordBridge(api);
            joinService = new JoinService(api);
            corpseService = new CorpseService(api);

            // Events
            api.Event.OnEntityDeath += corpseService.OnEntityDeath;
            api.Event.OnEntityDeath += BackCommands.OnEntityDeath;
            api.Event.DidBreakBlock += corpseService.OnBlockBroken;
            api.Event.PlayerJoin += joinService.OnPlayerJoin;
            api.Event.PlayerChat += discord.OnPlayerChat;

            // Commands
            DebugCommands.Register(api);
            DiscordCommands.Register(api);
            SpawnCommands.Register(api);
            BackCommands.Register(api);
            HomeCommands.Register(api);
            KitCommands.Register(api);
            TpaCommands.Register(api);
            UtilityCommands.Register(api);
            CorpseAdminCommands.Register(api, corpseService);
        }
    }
}
