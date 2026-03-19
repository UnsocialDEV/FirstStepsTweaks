using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Features;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks
{
    public class FirstStepsTweaks : ModSystem
    {
        private const string ConfigFileName = "firststepstweaks.json";
        private const string LegacyConfigFileName = "FirstStepsTweaks.json";
        private static readonly AssetLocation SupporterSpearCode = new AssetLocation("firststepstweaks", "supporter-spear");

        public override void StartServerSide(ICoreServerAPI api)
        {
            var config = LoadConfig(api);
            var runtime = new FeatureRuntime(api);

            registerPrivileges(api);
            api.Event.MatchesGridRecipe += OnMatchesGridRecipe;

            new JoinFeature(api, config, runtime).Register();
            new TeleportFeature(api, config, runtime).Register();
            new DiscordFeature(api, config, runtime).Register();
            new UtilityFeature(api, config, runtime).Register();

            if (config.Features.EnableCorpseService)
            {
                new GravestoneFeature(api, config, runtime).Register();
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
                "firststepstweaks.supporterkit",
                "Allows the player to use the /supporterkit command to receive a special donator kit.",
                true
            );

            api.Permissions.RegisterPrivilege(
                "firststepstweaks.supporter",
                "Allows access to supporter tier features.",
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


        // this handles the supporter spear only being craftable by donators
        private bool OnMatchesGridRecipe(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth)
        {
            if (recipe?.Output?.Code == null || !recipe.Output.Code.Equals(SupporterSpearCode))
            {
                return true;
            }

            return player?.HasPrivilege("firststepstweaks.supporter") == true;
        }
    }
}
