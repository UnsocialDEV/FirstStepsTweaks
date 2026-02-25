using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public class DiscordBridge
    {
        private readonly ICoreServerAPI api;
        private DiscordBridgeConfig config;

        private static readonly HttpClient http = new HttpClient();
        private readonly SemaphoreSlim pollLock = new SemaphoreSlim(1, 1);

        private const string DiscordLastIdKey = "fst_discord_lastmsgid";
        private string lastMessageId;

        public DiscordBridge(ICoreServerAPI api)
        {
            this.api = api;

            LoadConfig();
            if (IsConfigured() && config.RelayGameToDiscord)
            {
                api.Event.PlayerJoin += OnPlayerJoin;
                api.Event.PlayerDisconnect += OnPlayerDisconnect;
                api.Event.PlayerDeath += OnPlayerDeath;
            }
            RestoreLastMessageId();

            if (IsConfigured() && config.RelayDiscordToGame)
            {
                api.Event.RegisterGameTickListener(OnDiscordPollTick, config.PollMs);
            }
        }

        // =========================================================
        // CONFIG
        // =========================================================

        private void LoadConfig()
        {
            config = api.LoadModConfig<DiscordBridgeConfig>("FirstStepsTweaks.Discord.json");

            if (config == null)
            {
                config = new DiscordBridgeConfig();
                api.StoreModConfig(config, "FirstStepsTweaks.Discord.json");
                api.Logger.Warning("[FirstStepsTweaks] Created Discord config. Fill it in and restart.");
            }
        }

        private void RestoreLastMessageId()
        {
            string saved = api.WorldManager.SaveGame.GetData<string>(DiscordLastIdKey);
            if (!string.IsNullOrWhiteSpace(saved))
                lastMessageId = saved;
        }

        private bool IsConfigured()
        {
            return config != null
                && !string.IsNullOrWhiteSpace(config.BotToken)
                && !string.IsNullOrWhiteSpace(config.ChannelId)
                && !string.IsNullOrWhiteSpace(config.WebhookUrl);
        }

        // =========================================================
        // GAME → DISCORD
        // =========================================================

        public void OnPlayerChat(IServerPlayer player, int channelId, ref string message, ref string data, BoolRef consumed)
        {
            if (!IsConfigured()) return;
            if (!config.RelayGameToDiscord) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            // Ignore command prefixes (like /)
            foreach (var prefix in config.IgnoreGamePrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix) && message.StartsWith(prefix))
                    return;
            }

            string clean = StripVsFormatting(message);

            // Remove "PlayerName: " prefix if present
            string prefix2 = player.PlayerName + ": ";
            if (clean.StartsWith(prefix2))
            {
                clean = clean.Substring(prefix2.Length);
            }

            _ = SendPlainToDiscord(
                player.PlayerName,
                clean
            );
        }

        private string StripVsFormatting(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        private async Task SendPlainToDiscord(string username, string message)
        {
            if (!IsConfigured()) return;

            var payload = new
            {
                username = username,
                content = message
            };

            string json = JsonSerializer.Serialize(payload);

            using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            await http.PostAsync(config.WebhookUrl, httpContent);
        }

        private async Task SendEmbedToDiscord(string username, string description, int color)
        {
            if (!IsConfigured()) return;

            var payload = new
            {
                username = username,   // ← sets webhook display name
                embeds = new[]
                {
            new
            {
                description,
                color
            }
        }
            };

            string json = JsonSerializer.Serialize(payload);

            using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            await http.PostAsync(config.WebhookUrl, httpContent);
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            if (!IsConfigured() || !config.RelayGameToDiscord) return;
            if (!config.RelayJoinLeave) return;

            string message = $"🟢 **{player.PlayerName}** joined the server.";
            _ = SendEmbedToDiscord(
                "Player Joined",
                $"{player.PlayerName} joined the server.",
                0x57F287 // green
            );
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            if (!IsConfigured() || !config.RelayGameToDiscord) return;
            if (!config.RelayJoinLeave) return;

            string name = player?.PlayerName;

            if (string.IsNullOrWhiteSpace(name))
                name = "Unknown Player";

            string message = $"🔴 **{name}** left the server.";
            _ = SendEmbedToDiscord(
                "Player Left",
                $"{name} left the server.",
                0xED4245 // red
            );
        }
        private void OnPlayerDeath(IServerPlayer player, DamageSource damageSource)
        {
            if (!IsConfigured() || !config.RelayGameToDiscord) return;
            if (player == null) return;

            string name = player.PlayerName;
            string deathMessage;

            if (damageSource == null)
            {
                deathMessage = $"{name} died.";
            }
            else if (damageSource.SourceEntity is EntityPlayer killerPlayer)
            {
                deathMessage = $"{name} was slain by {killerPlayer.Player.PlayerName}.";
            }
            else if (damageSource.SourceEntity != null)
            {
                string killerName = damageSource.SourceEntity.GetName();
                deathMessage = $"{name} was killed by {killerName}.";
            }
            else
            {
                switch (damageSource.Type)
                {
                    case EnumDamageType.Gravity:
                        deathMessage = $"{name} fell from a high place.";
                        break;

                    case EnumDamageType.Fire:
                    case EnumDamageType.Heat:
                        deathMessage = $"{name} burned to death.";
                        break;
                    case EnumDamageType.Suffocation:
                        deathMessage = $"{name} suffocated.";
                        break;

                    default:
                        deathMessage = $"{name} died.";
                        break;
                }
            }

            _ = SendPlainToDiscord("Death", $"💀 {deathMessage}");
        }

        // =========================================================
        // DISCORD → GAME
        // =========================================================

        private async void OnDiscordPollTick(float dt)
        {
            if (!await pollLock.WaitAsync(0)) return;

            try
            {
                await CheckDiscordMessagesOnce();
            }
            catch (Exception e)
            {
                api.Logger.Error($"[FirstStepsTweaks] Discord poll error: {e}");
            }
            finally
            {
                pollLock.Release();
            }
        }
        private async Task CheckDiscordMessagesOnce()
        {
            if (!IsConfigured()) return;
            if (!config.RelayDiscordToGame) return;

            try
            {
                string url = string.IsNullOrWhiteSpace(lastMessageId)
                    ? $"https://discord.com/api/v10/channels/{config.ChannelId}/messages?limit=100"
                    : $"https://discord.com/api/v10/channels/{config.ChannelId}/messages?after={lastMessageId}&limit=100";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bot", config.BotToken);

                using var resp = await http.SendAsync(req);

                if ((int)resp.StatusCode == 429)
                {
                    api.Logger.Warning("[FirstStepsTweaks] Discord rate limited.");
                    return;
                }

                resp.EnsureSuccessStatusCode();

                string json = await resp.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json)) return;

                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                    return;

                // Process oldest → newest so chat order is correct
                foreach (JsonElement msg in root.EnumerateArray().Reverse())
                {
                    if (!msg.TryGetProperty("id", out var idProp))
                        continue;

                    string id = idProp.GetString();
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    // Ignore bot messages
                    if (msg.TryGetProperty("author", out var author) &&
                        author.TryGetProperty("bot", out var botProp) &&
                        botProp.ValueKind == JsonValueKind.True)
                    {
                        lastMessageId = id;
                        continue;
                    }

                    string content = msg.TryGetProperty("content", out var contentProp)
                        ? contentProp.GetString()
                        : "";

                    content = SanitizeDiscordContentForGame(msg, content);

                    if (config.IgnoreEmptyDiscordMessages && string.IsNullOrWhiteSpace(content))
                    {
                        lastMessageId = id;
                        continue;
                    }

                    foreach (var prefix in config.IgnoreDiscordPrefixes)
                    {
                        if (!string.IsNullOrWhiteSpace(prefix) && content.StartsWith(prefix))
                        {
                            lastMessageId = id;
                            continue;
                        }
                    }

                    string username = "Discord";
                    if (msg.TryGetProperty("author", out var authorObj) &&
                        authorObj.TryGetProperty("username", out var userProp))
                    {
                        username = userProp.GetString() ?? "Discord";
                    }

                    string prefixLabel = string.IsNullOrWhiteSpace(config.DiscordPrefix)
                        ? "[Discord]"
                        : config.DiscordPrefix;

                    api.SendMessageToGroup(
                        GlobalConstants.GeneralChatGroup,
                        $"{prefixLabel} {username}: {content}",
                        EnumChatType.AllGroups
                    );

                    lastMessageId = id;
                }

                // Save last processed message ID once
                api.WorldManager.SaveGame.StoreData(DiscordLastIdKey, lastMessageId);
            }
            catch (Exception e)
            {
                api.Logger.Error($"[FirstStepsTweaks] Discord polling exception: {e.Message}");
            }
        }

        private string SanitizeDiscordContentForGame(JsonElement msg, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return content;

            // Convert user mentions like <@123...> into @Username so they don't leak VS rich text markers.
            if (msg.TryGetProperty("mentions", out var mentions) && mentions.ValueKind == JsonValueKind.Array)
            {
                foreach (var mention in mentions.EnumerateArray())
                {
                    if (!mention.TryGetProperty("id", out var idProp))
                        continue;

                    string mentionId = idProp.GetString();
                    if (string.IsNullOrWhiteSpace(mentionId))
                        continue;

                    string mentionName = null;

                    if (mention.TryGetProperty("global_name", out var globalNameProp))
                    {
                        mentionName = globalNameProp.GetString();
                    }

                    if (string.IsNullOrWhiteSpace(mentionName) && mention.TryGetProperty("username", out var usernameProp))
                    {
                        mentionName = usernameProp.GetString();
                    }

                    mentionName ??= "user";

                    content = content.Replace($"<@{mentionId}>", $"@{mentionName}");
                    content = content.Replace($"<@!{mentionId}>", $"@{mentionName}");
                }
            }

            // Remove any remaining Discord tag syntax (<#...>, <@&...>, custom emojis, etc.)
            // so Vintage Story does not interpret angle-bracket content as formatting tags.
            content = Regex.Replace(content, "<[^>]+>", string.Empty);

            return content;
        }
    }
}
