using System.Collections.Generic;

namespace FirstStepsTweaks.Config
{
    public class FirstStepsTweaksConfig
    {
        public FeatureToggles Features { get; set; } = new FeatureToggles();
        public AgentBridgeConfig AgentBridge { get; set; } = new AgentBridgeConfig();
        public ChatConfig Chat { get; set; } = new ChatConfig();
        public TeleportConfig Teleport { get; set; } = new TeleportConfig();
        public RtpConfig Rtp { get; set; } = new RtpConfig();
        public JoinConfig Join { get; set; } = new JoinConfig();
        public DiscordCommandConfig DiscordCommand { get; set; } = new DiscordCommandConfig();
        public KitConfig Kits { get; set; } = new KitConfig();
        public UtilityConfig Utility { get; set; } = new UtilityConfig();
        public CorpseConfig Corpse { get; set; } = new CorpseConfig();
        public LandClaimNotificationConfig LandClaims { get; set; } = new LandClaimNotificationConfig();
    }

    public class FeatureToggles
    {
        public bool EnableAgentBridge { get; set; } = false;
        public bool EnableDebugCommand { get; set; } = true;
        public bool EnableDiscordCommand { get; set; } = true;
        public bool EnableSpawnCommands { get; set; } = true;
        public bool EnableStormShelterCommands { get; set; } = true;
        public bool EnableStuckCommand { get; set; } = true;
        public bool EnableBackCommand { get; set; } = true;
        public bool EnableHomeCommands { get; set; } = true;
        public bool EnableKitCommands { get; set; } = true;
        public bool EnableTpaCommands { get; set; } = true;
        public bool EnableWarpCommands { get; set; } = true;
        public bool EnableRtpCommand { get; set; } = true;
        public bool EnableUtilityCommands { get; set; } = true;
        public bool EnableCorpseService { get; set; } = true;
        public bool EnableCorpseAdminCommands { get; set; } = true;
        public bool EnableJoinBroadcasts { get; set; } = true;
        public bool EnableLandClaimNotifications { get; set; } = true;
    }

    public class AgentBridgeConfig
    {
        public const string DefaultHost = "127.0.0.1";
        public const int LegacyDefaultPort = 8765;
        public const int DefaultPort = 28765;

        public string Host { get; set; } = DefaultHost;
        public int Port { get; set; } = DefaultPort;
        public string SharedToken { get; set; } = string.Empty;
    }

    public class ChatConfig
    {
        public bool EnableDonatorPrefixes { get; set; } = true;
        public string DonatorPrefixFormat { get; set; } = "{tier}";
        public string SupporterPrefix { get; set; } = "•S";
        public string ContributorPrefix { get; set; } = "•C";
        public string SponsorPrefix { get; set; } = "•SP";
        public string PatronPrefix { get; set; } = "•P";
        public string FounderPrefix { get; set; } = "•F";
    }

    public class TeleportConfig
    {
        public const int DefaultDonatorWarmupSeconds = 3;

        public int WarmupSeconds { get; set; } = 10;
        public int? DonatorWarmupSeconds { get; set; }
        public double CancelMoveThreshold { get; set; } = 0.1;
        public int TickIntervalMs { get; set; } = 1000;
        public int TpaExpireMs { get; set; } = 180000;
        public HomeLimitConfig HomeLimits { get; set; } = new HomeLimitConfig();
    }

    public class HomeLimitConfig
    {
        public int Default { get; set; } = 1;
        public int Supporter { get; set; } = 2;
        public int Contributor { get; set; } = 3;
        public int Sponsor { get; set; } = 4;
        public int Patron { get; set; } = 5;
        public int Founder { get; set; } = 6;
    }

    public class RtpConfig
    {
        public const int LegacyMinRadius = 256;
        public const int LegacyMaxRadius = 2048;
        public const bool LegacyUsePlayerPositionAsCenter = true;
        public const int DefaultMinRadius = 2500;
        public const int DefaultMaxRadius = 5000;
        public const bool DefaultUsePlayerPositionAsCenter = false;

        public int MinRadius { get; set; } = DefaultMinRadius;
        public int MaxRadius { get; set; } = DefaultMaxRadius;
        public int MaxAttempts { get; set; } = 24;
        public int CooldownSeconds { get; set; } = 300;
        public bool UsePlayerPositionAsCenter { get; set; } = DefaultUsePlayerPositionAsCenter;
        public bool UseWarmup { get; set; } = true;
    }

    public class JoinConfig
    {
        public const string DefaultFirstJoinMessage = "Welcome {player} to the server, this is their first time joining!";
        public const string LegacyReturningJoinMessage = "Welcome back {player}! It's been {days} in-game day(s) since your last visit.";
        public const string DefaultReturningJoinMessage = "Welcome back {player}! It's been {days} in-game day(s) since your last visit, you have played for {playtime} hours.";

        public string FirstJoinMessage { get; set; } = DefaultFirstJoinMessage;
        public string ReturningJoinMessage { get; set; } = DefaultReturningJoinMessage;
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
        public string GraveBlockCode { get; set; } = "firststepstweaks:gravestone";
        public int DropCleanupTickMs { get; set; } = 50;
        public int EnforceGraveTickMs { get; set; } = 200;
        public long GraveExpireMs { get; set; } = 3600000;
        public int GraveCleanupTickMs { get; set; } = 60000;
        public double GraveCleanupInGameDays { get; set; } = 30;
    }

    public class LandClaimNotificationConfig
    {
        public int TickIntervalMs { get; set; } = 1000;
        public string EnterMessage { get; set; } = "You entered {owner} land claim. ({claim})";
        public string ExitMessage { get; set; } = "You left {owner} land claim. ({claim})";
    }
}

