using System;
using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Features;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks
{
    #nullable enable
    public class FirstStepsTweaks : ModSystem
    {
        private const string ConfigFileName = "firststepstweaks.json";
        private const string LegacyConfigFileName = "FirstStepsTweaks.json";
        private AgentBridgeFeature? agentBridgeFeature;

        public override void StartServerSide(ICoreServerAPI api)
        {
            var config = LoadConfig(api);
            var runtime = new FeatureRuntime(api, config);

            registerPrivileges(api);

            new JoinFeature(api, config, runtime).Register();
            new TeleportFeature(api, config, runtime).Register();
            new ChatFeature(api, config).Register();
            new DiscordFeature(api, config, runtime).Register();
            new UtilityFeature(api, config, runtime).Register();

            if (config.Features.EnableCorpseService)
            {
                new GravestoneFeature(api, config, runtime).Register();
            }

            // The bridge code stays wired into startup so future re-enable work only needs
            // to change the availability policy instead of rebuilding the registration path.
            agentBridgeFeature = new AgentBridgeFeature(api, config);
            agentBridgeFeature.Register();
        }

        public override void Dispose()
        {
            agentBridgeFeature?.Dispose();
            agentBridgeFeature = null;
            base.Dispose();
        }

        private FirstStepsTweaksConfig LoadConfig(ICoreServerAPI api)
        {
            var config = api.LoadModConfig<FirstStepsTweaksConfig>(ConfigFileName);
            var legacyConfig = api.LoadModConfig<LegacyFirstStepsTweaksConfig>(ConfigFileName);
            if (config != null)
            {
                return ApplyConfigUpgrades(api, config, legacyConfig);
            }

            config = api.LoadModConfig<FirstStepsTweaksConfig>(LegacyConfigFileName);
            legacyConfig = api.LoadModConfig<LegacyFirstStepsTweaksConfig>(LegacyConfigFileName);
            if (config != null)
            {
                api.StoreModConfig(config, ConfigFileName);
                api.Logger.Notification($"[FirstStepsTweaks] Migrated config file '{LegacyConfigFileName}' to '{ConfigFileName}' for cross-platform compatibility.");
                return ApplyConfigUpgrades(api, config, legacyConfig);
            }

            config = new FirstStepsTweaksConfig();
            config = ApplyConfigUpgrades(api, config, null);
            api.StoreModConfig(config, ConfigFileName);
            api.Logger.Warning($"[FirstStepsTweaks] Created config file {ConfigFileName}. Review and adjust as needed.");
            return config;
        }

        private FirstStepsTweaksConfig ApplyConfigUpgrades(ICoreServerAPI api, FirstStepsTweaksConfig config, LegacyFirstStepsTweaksConfig? legacyConfig)
        {
            bool changed = false;
            var agentBridgeConfigUpgrader = new AgentBridgeConfigUpgrader();
            var joinConfigUpgrader = new JoinConfigUpgrader();
            var teleportConfigUpgrader = new TeleportConfigUpgrader();
            var rtpConfigUpgrader = new RtpConfigUpgrader();
            var staffConfigUpgrader = new StaffConfigUpgrader(new StaffAssignmentStore(api));

            if (agentBridgeConfigUpgrader.TryUpgradeLegacyLoopbackPort(config))
            {
                changed = true;
            }

            if (joinConfigUpgrader.TryUpgradeReturningJoinMessage(config))
            {
                changed = true;
            }

            if (teleportConfigUpgrader.TryUpgradeDonatorWarmupSeconds(config))
            {
                changed = true;
            }

            if (rtpConfigUpgrader.TryUpgradeLegacyDefaults(config))
            {
                changed = true;
            }

            if (staffConfigUpgrader.TryUpgradeLegacyAdminPlayerNames(legacyConfig?.Utility?.AdminPlayerNames))
            {
                changed = true;
            }

            if (!changed)
            {
                return config;
            }

            api.StoreModConfig(config, ConfigFileName);
            api.Logger.Notification($"[FirstStepsTweaks] Updated '{ConfigFileName}' config defaults.");
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
                "firststepstweaks.supporter",
                "Allows access to supporter tier features.",
                false
            );

            api.Permissions.RegisterPrivilege(
                "firststepstweaks.contributor",
                "Allows access to contributor tier donator chat prefixes.",
                false
            );

            api.Permissions.RegisterPrivilege(
                "firststepstweaks.sponsor",
                "Allows access to sponsor tier donator chat prefixes.",
                false
            );

            api.Permissions.RegisterPrivilege(
                "firststepstweaks.patron",
                "Allows access to patron tier donator chat prefixes.",
                false
            );

            api.Permissions.RegisterPrivilege(
                "firststepstweaks.founder",
                "Allows access to founder tier donator chat prefixes.",
                false
            );

            api.Permissions.RegisterPrivilege(
                "firststepstweaks.graveadmin",
                "Allows the player to use admin commands for managing gravestones, such as listing, removing, and giving grave items.",
                true
            );
            api.Permissions.RegisterPrivilege(
                StaffPrivilegeCatalog.AdminPrivilege,
                "Allows access to FirstStepsTweaks admin staff commands and managed admin privileges.",
                false
            );
            api.Permissions.RegisterPrivilege(
                StaffPrivilegeCatalog.ModeratorPrivilege,
                "Allows access to FirstStepsTweaks moderator staff commands and managed moderator privileges.",
                false
            );
            api.Permissions.RegisterPrivilege(
                TeleportBypass.Privilege,
                "Allows the player to bypass /rtp cooldown timers.",
                true
            );
        }
    }
}
