using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
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

        private readonly SemaphoreSlim worldUpdateLock = new SemaphoreSlim(1, 1);
        private bool worldStateInitialized;
        private int lastDayNumber;
        private string lastSeasonName;

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

            if (IsConfigured() && config.RelayGameToDiscord && config.RelayWorldUpdates)
            {
                int pollMs = Math.Max(1000, config.WorldUpdatePollMs);
                api.Event.RegisterGameTickListener(OnWorldUpdateTick, pollMs);
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

            string mappedMessage = ReplaceGameMentionsWithDiscordMentions(message);

            var payload = new
            {
                username = username,
                content = mappedMessage,
                allowed_mentions = new
                {
                    parse = new[] { "users" }
                }
            };

            string json = JsonSerializer.Serialize(payload);

            using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            await http.PostAsync(config.WebhookUrl, httpContent);
        }


        private string ReplaceGameMentionsWithDiscordMentions(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return message;
            if (config?.GameMentionMap == null || config.GameMentionMap.Count == 0) return message;

            return Regex.Replace(
                message,
                @"(?<!\w)@([A-Za-z0-9_.-]+)",
                match =>
                {
                    string key = match.Groups[1].Value;

                    if (!config.GameMentionMap.TryGetValue(key, out string discordId) &&
                        !config.GameMentionMap.TryGetValue(key.ToLowerInvariant(), out discordId))
                    {
                        return match.Value;
                    }

                    if (string.IsNullOrWhiteSpace(discordId))
                    {
                        return match.Value;
                    }

                    return $"<@{discordId.Trim()}>";
                }
            );
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

        private async void OnWorldUpdateTick(float dt)
        {
            if (!await worldUpdateLock.WaitAsync(0)) return;

            try
            {
                CheckWorldUpdatesOnce();
            }
            catch (Exception e)
            {
                api.Logger.Error($"[FirstStepsTweaks] World update relay error: {e}");
            }
            finally
            {
                worldUpdateLock.Release();
            }
        }

        private void CheckWorldUpdatesOnce()
        {
            if (!IsConfigured() || !config.RelayGameToDiscord || !config.RelayWorldUpdates) return;
            if (api.World?.Calendar == null) return;

            int dayNumber = (int)Math.Floor(api.World.Calendar.TotalDays);
            string seasonName = ResolveSeasonName();

            if (!worldStateInitialized)
            {
                lastDayNumber = dayNumber;
                lastSeasonName = seasonName;
                worldStateInitialized = true;
                return;
            }

            if (dayNumber != lastDayNumber)
            {
                _ = SendEmbedToDiscord(
                    "World Update",
                    $"🌅 A new day has begun (Day {dayNumber}).",
                    0xFEE75C
                );
                lastDayNumber = dayNumber;
            }

            if (!string.IsNullOrWhiteSpace(seasonName) && !string.Equals(seasonName, lastSeasonName, StringComparison.Ordinal))
            {
                _ = SendEmbedToDiscord(
                    "World Update",
                    $"🍂 The season has changed to **{seasonName}**.",
                    0x5865F2
                );
                lastSeasonName = seasonName;
            }
        }

        private string ResolveSeasonName()
        {
            if (TryGetStringProperty(api.World.Calendar, "SeasonName", out string seasonName) && !string.IsNullOrWhiteSpace(seasonName))
            {
                return seasonName;
            }

            if (TryGetStringProperty(api.World.Calendar, "Season", out seasonName) && !string.IsNullOrWhiteSpace(seasonName))
            {
                return seasonName;
            }

            if (TryGetNumericProperty(api.World.Calendar, "Month", out double monthRaw))
            {
                int month = (int)Math.Floor(monthRaw);
                int monthsPerYear = 12;
                if (TryGetNumericProperty(api.World.Calendar, "MonthsPerYear", out double monthsPerYearRaw) && monthsPerYearRaw > 0)
                {
                    monthsPerYear = Math.Max(1, (int)Math.Round(monthsPerYearRaw));
                }

                return SeasonNameFromMonth(month, monthsPerYear);
            }

            if (TryInvokeCalendarDoubleNoArgs("GetSeasonRel", out double seasonRel) ||
                TryInvokeCalendarDouble("GetSeasonRel", api.World.Calendar.TotalDays, out seasonRel) ||
                TryInvokeCalendarDouble("GetSeasonRel", (float)api.World.Calendar.TotalDays, out seasonRel) ||
                TryGetNumericProperty(api.World.Calendar, "SeasonRel", out seasonRel) ||
                TryGetNumericProperty(api.World.Calendar, "Season", out seasonRel))
            {
                return SeasonNameFromRelative(seasonRel);
            }

            return null;
        }

        private string SeasonNameFromMonth(int month, int monthsPerYear)
        {
            if (monthsPerYear <= 0) monthsPerYear = 12;

            int normalizedMonth = month % monthsPerYear;
            if (normalizedMonth < 0) normalizedMonth += monthsPerYear;

            int seasonIndex = (normalizedMonth * 4) / monthsPerYear;
            switch (seasonIndex)
            {
                case 0: return "Spring";
                case 1: return "Summer";
                case 2: return "Autumn";
                default: return "Winter";
            }
        }

        private string SeasonNameFromRelative(double seasonRel)
        {
            if (double.IsNaN(seasonRel) || double.IsInfinity(seasonRel)) return null;

            // Some API surfaces return [0..1), others [0..4).
            double normalized = seasonRel <= 1d
                ? seasonRel
                : seasonRel / 4d;

            normalized %= 1d;
            if (normalized < 0) normalized += 1d;

            if (normalized < 0.25d) return "Spring";
            if (normalized < 0.5d) return "Summer";
            if (normalized < 0.75d) return "Autumn";
            return "Winter";
        }

        private bool TryGetNumericProperty(object instance, string propertyName, out double value)
        {
            value = default;
            if (instance == null) return false;

            PropertyInfo prop = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return false;

            object raw = prop.GetValue(instance);
            if (raw == null) return false;

            try
            {
                value = Convert.ToDouble(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryGetStringProperty(object instance, string propertyName, out string value)
        {
            value = null;
            if (instance == null) return false;

            PropertyInfo prop = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || prop.PropertyType != typeof(string)) return false;

            value = prop.GetValue(instance) as string;
            return value != null;
        }

        private bool TryInvokeCalendarDouble(string methodName, object arg, out double value)
        {
            value = default;
            if (api.World?.Calendar == null) return false;

            MethodInfo method = api.World.Calendar.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, new[] { arg.GetType() }, null);
            if (method == null) return false;

            object result = method.Invoke(api.World.Calendar, new[] { arg });
            if (result == null) return false;

            try
            {
                value = Convert.ToDouble(result);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryInvokeCalendarDoubleNoArgs(string methodName, out double value)
        {
            value = default;
            if (api.World?.Calendar == null) return false;

            MethodInfo method = api.World.Calendar.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null) return false;

            object result = method.Invoke(api.World.Calendar, Array.Empty<object>());
            if (result == null) return false;

            try
            {
                value = Convert.ToDouble(result);
                return true;
            }
            catch
            {
                return false;
            }
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
