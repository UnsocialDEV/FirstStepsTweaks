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
        private const string ConfigFileName = "firststepstweaks.json";
        private const string LegacyConfigFileName = "FirstStepsTweaks.json";
        private static readonly AssetLocation DonatorSpearCode = new AssetLocation("firststepstweaks", "donator-spear");

        private DiscordBridge discord;
        private JoinService joinService;
        private JoinInvulnerabilityService joinInvulnerabilityService;
        private LandClaimNotificationService landClaimNotificationService;
        private GravestoneService gravestoneService;
        public override void StartServerSide(ICoreServerAPI api)
        {
            var config = LoadConfig(api);

            registerPrivileges(api);

            discord = new DiscordBridge(api);
            joinService = new JoinService(api, config);
            joinInvulnerabilityService = new JoinInvulnerabilityService(api);

            if (config.Features.EnableBackCommand)
            {
                api.Event.OnEntityDeath += BackCommands.OnEntityDeath;
                BackCommands.Register(api, config);
            }

            if (config.Features.EnableCorpseService)
            {
                gravestoneService = new GravestoneService(api, config);
                WhereIsMyGraveCommand.Register(api, gravestoneService);

                if (config.Features.EnableCorpseAdminCommands)
                {
                    GravestoneCommands.Register(api, gravestoneService);
                }
            }

            api.Event.PlayerJoin += joinInvulnerabilityService.OnPlayerJoin;
            api.Event.PlayerNowPlaying += joinInvulnerabilityService.OnPlayerNowPlaying;
            api.Event.PlayerNowPlaying += joinService.OnPlayerNowPlaying;
            api.Event.PlayerLeave += joinInvulnerabilityService.OnPlayerLeave;
            api.Event.PlayerLeave += joinService.OnPlayerLeave;

            if (config.Features.EnableLandClaimNotifications)
            {
                landClaimNotificationService = new LandClaimNotificationService(api, config);
            }

            api.Event.PlayerChat += discord.OnPlayerChat;
            api.Event.MatchesGridRecipe += OnMatchesGridRecipe;

            if (config.Features.EnableDebugCommand) DebugCommands.Register(api);
            if (config.Features.EnableDiscordCommand) DiscordCommands.Register(api, config);
            if (config.Features.EnableSpawnCommands) SpawnCommands.Register(api, config);
            if (config.Features.EnableHomeCommands) HomeCommands.Register(api, config);
            if (config.Features.EnableKitCommands) KitCommands.Register(api, config);
            if (config.Features.EnableTpaCommands) TpaCommands.Register(api, config);
            if (config.Features.EnableWarpCommands) WarpCommands.Register(api, config);
            if (config.Features.EnableRtpCommand) RtpCommands.Register(api, config);
            if (config.Features.EnableUtilityCommands)
            {
                WhosOnlineCommand.Register(api, config);
                WindCommand.Register(api, config);
                AdminVitalsCommands.Register(api);
            }
        }

        private FirstStepsTweaksConfig LoadConfig(ICoreServerAPI api)
        {
            var config = api.LoadModConfig<FirstStepsTweaksConfig>(ConfigFileName);
            if (config != null)
            {
                return config;
            }

            config = api.LoadModConfig<FirstStepsTweaksConfig>(LegacyConfigFileName);
            if (config != null)
            {
                api.StoreModConfig(config, ConfigFileName);
                api.Logger.Notification($"[FirstStepsTweaks] Migrated config file '{LegacyConfigFileName}' to '{ConfigFileName}' for cross-platform compatibility.");
                return config;
            }

            config = new FirstStepsTweaksConfig();
            api.StoreModConfig(config, ConfigFileName);
            api.Logger.Warning($"[FirstStepsTweaks] Created config file {ConfigFileName}. Review and adjust as needed.");
            return config;
        }

        private void registerPrivileges(ICoreServerAPI api)
        {
            api.Permissions.RegisterPrivilege(
                "firststepstweaks.back",
                "Allows the player to use the /back command to return to their last location after death.",
                true
            );

            api.Permissions.RegisterPrivilege(
                "firststepstweaks.donatorkit",
                "Allows the player to use the /kit donor command to receive a special donator kit.",
                true
            );

            api.Permissions.RegisterPrivilege(
                "firststepstweaks.donator",
                "Allows the player to craft donator-only items like the donator spear special recipe.",
                true
            );

            api.Permissions.RegisterPrivilege(
                "firststepstweaks.graveadmin",
                "Allows the player to use admin commands for managing gravestones, such as listing, removing, and giving grave items.",
                true
            );
            api.Permissions.RegisterPrivilege(
                TeleportBypass.Privilege,
                "Allows the player to bypass teleport cooldown timers (for example, /rtp cooldown).",
                true
            );
        }


        // this handles the donator spear only being craftable by donators
        private bool OnMatchesGridRecipe(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth)
        {
            if (recipe?.Output?.Code == null || !recipe.Output.Code.Equals(DonatorSpearCode))
            {
                return true;
            }

            return player?.HasPrivilege("firststepstweaks.donator") == true;
        }
    }
}
