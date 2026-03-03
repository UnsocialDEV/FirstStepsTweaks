using System.Collections.Generic;

namespace FirstStepsTweaks.Discord
{
    public class DiscordBridgeConfig
    {
        // REQUIRED
        public string BotToken { get; set; } = "";
        public string ChannelId { get; set; } = "";
        public string WebhookUrl { get; set; } = "";

        // Optional behavior
        public int PollMs { get; set; } = 5000;                 // 5000–10000 recommended
        public string DiscordPrefix { get; set; } = "[Discord]"; // shown in-game
        public bool RelayGameToDiscord { get; set; } = true;
        public bool RelayDiscordToGame { get; set; } = true;
        public bool RelayJoinLeave = true;
        public bool RelayWorldUpdates { get; set; } = true;
        public int WorldUpdatePollMs { get; set; } = 5000;

        // Optional filters
        public bool IgnoreEmptyDiscordMessages { get; set; } = true;
        public List<string> IgnoreDiscordPrefixes { get; set; } = new List<string> { "!" }; // ignore bot commands
        public List<string> IgnoreGamePrefixes { get; set; } = new List<string> { "/" };    // ignore commands typed in-game

        // Optional mention mapping for game -> Discord relay.
        // Example: { "alice": "123456789012345678" } allows typing "@alice" in-game to ping that Discord user.
        public Dictionary<string, string> GameMentionMap { get; set; } = new Dictionary<string, string>();
    }
}
