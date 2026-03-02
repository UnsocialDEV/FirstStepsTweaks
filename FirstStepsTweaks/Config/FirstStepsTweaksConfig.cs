using System.Collections.Generic;

namespace FirstStepsTweaks.Config
{
    public class FirstStepsTweaksConfig
    {
        public FeatureToggles Features { get; set; } = new FeatureToggles();
        public TeleportConfig Teleport { get; set; } = new TeleportConfig();
        public JoinConfig Join { get; set; } = new JoinConfig();
        public DiscordCommandConfig DiscordCommand { get; set; } = new DiscordCommandConfig();
        public KitConfig Kits { get; set; } = new KitConfig();
        public UtilityConfig Utility { get; set; } = new UtilityConfig();
        public CorpseConfig Corpse { get; set; } = new CorpseConfig();
    }

    public class FeatureToggles
    {
        public bool EnableDebugCommand { get; set; } = true;
        public bool EnableDiscordCommand { get; set; } = true;
        public bool EnableSpawnCommands { get; set; } = true;
        public bool EnableBackCommand { get; set; } = true;
        public bool EnableHomeCommands { get; set; } = true;
        public bool EnableKitCommands { get; set; } = true;
        public bool EnableTpaCommands { get; set; } = true;
        public bool EnableWarpCommands { get; set; } = true;
        public bool EnableUtilityCommands { get; set; } = true;
        public bool EnableCorpseService { get; set; } = true;
        public bool EnableCorpseAdminCommands { get; set; } = true;
        public bool EnableJoinBroadcasts { get; set; } = true;
    }

    public class TeleportConfig
    {
        public int WarmupSeconds { get; set; } = 10;
        public double CancelMoveThreshold { get; set; } = 0.1;
        public int TickIntervalMs { get; set; } = 1000;
        public int TpaExpireMs { get; set; } = 180000;
    }

    public class JoinConfig
    {
        public string FirstJoinMessage { get; set; } = "Welcome {player} to the server, this is their first time joining!";
        public string ReturningJoinMessage { get; set; } = "Welcome back {player}!";
    }

    public class DiscordCommandConfig
    {
        public string InviteMessage { get; set; } = "Discord: discord.gg/8SqKaERD6m";
    }

    public class KitConfig
    {
        public bool EnableStarterKit { get; set; } = true;
        public bool EnableWinterKit { get; set; } = true;
        public List<KitItemConfig> StarterItems { get; set; } = new List<KitItemConfig>
        {
            new KitItemConfig("game:flint", 6),
            new KitItemConfig("game:stick", 6),
            new KitItemConfig("game:drygrass", 1),
            new KitItemConfig("game:firewood", 4),
            new KitItemConfig("game:torch-basic-lit-up", 4),
            new KitItemConfig("game:bread-rye-perfect", 8)
        };

        public List<KitItemConfig> WinterItems { get; set; } = new List<KitItemConfig>
        {
            new KitItemConfig("game:clothes-upperbodyover-fur-coat", 1),
            new KitItemConfig("game:clothes-foot-knee-high-fur-boots", 1),
            new KitItemConfig("game:clothes-hand-fur-gloves", 1),
            new KitItemConfig("game:redmeat-cooked", 12)
        };
    }

    public class KitItemConfig
    {
        public KitItemConfig() { }

        public KitItemConfig(string code, int quantity)
        {
            Code = code;
            Quantity = quantity;
        }

        public string Code { get; set; } = "";
        public int Quantity { get; set; } = 1;
    }

    public class UtilityConfig
    {
        public float HurricaneThreshold { get; set; } = 0.85f;
        public float StormThreshold { get; set; } = 0.65f;
        public float StrongWindThreshold { get; set; } = 0.45f;
        public float BreezyThreshold { get; set; } = 0.25f;
        public List<string> AdminPlayerNames { get; set; } = new List<string>();
    }

    public class CorpseConfig
    {
        public string GraveBlockCode { get; set; } = "game:figurehead-skull";
        public int DropCleanupTickMs { get; set; } = 50;
        public int EnforceGraveTickMs { get; set; } = 200;
    }
}
