using System;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using FirstStepsTweaks.Discord.Messaging;
using FirstStepsTweaks.Discord.Transport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public class DiscordBridge
    {
        private readonly ICoreServerAPI api;
        private readonly DiscordConfigStore configStore;
        private readonly IDiscordLastMessageStore lastMessageStore;
        private readonly IDiscordMessageTranslator messageTranslator;
        private readonly IDiscordWebhookClient webhookClient;
        private readonly DiscordPlayerAvatarService avatarService;
        private readonly DiscordRelayMessageNormalizer relayMessageNormalizer;
        private readonly DiscordRelayConfigurationValidator configurationValidator;
        private const int DiscordMessagePageSize = 100;
        private DiscordBridgeConfig config;
        private readonly SemaphoreSlim pollLock = new SemaphoreSlim(1, 1);
        private string lastMessageId;

        private readonly SemaphoreSlim worldUpdateLock = new SemaphoreSlim(1, 1);
        private bool worldStateInitialized;
        private int lastDayNumber;
        private string lastSeasonName;
        private bool isRegistered;

        public DiscordBridge(ICoreServerAPI api, DiscordPlayerAvatarService avatarService, DiscordRelayMessageNormalizer relayMessageNormalizer)
        {
            this.api = api;
            this.avatarService = avatarService;
            this.relayMessageNormalizer = relayMessageNormalizer;
            configStore = new DiscordConfigStore(api);
            lastMessageStore = new DiscordLastMessageStore(api);
            messageTranslator = new DiscordMessageTranslator();
            webhookClient = new DiscordWebhookClient();
            configurationValidator = new DiscordRelayConfigurationValidator();

            LoadConfig();
            RestoreLastMessageId();
        }

        internal DiscordBridge(
            ICoreServerAPI api,
            DiscordBridgeConfig config,
            IDiscordLastMessageStore lastMessageStore,
            IDiscordMessageTranslator messageTranslator,
            IDiscordWebhookClient webhookClient,
            DiscordPlayerAvatarService avatarService,
            DiscordRelayMessageNormalizer relayMessageNormalizer,
            DiscordRelayConfigurationValidator configurationValidator)
        {
            this.api = api;
            configStore = null!;
            this.lastMessageStore = lastMessageStore;
            this.messageTranslator = messageTranslator;
            this.webhookClient = webhookClient;
            this.avatarService = avatarService;
            this.relayMessageNormalizer = relayMessageNormalizer;
            this.configurationValidator = configurationValidator;
            this.config = config;

            RestoreLastMessageId();
        }

        public void Register()
        {
            if (isRegistered)
            {
                return;
            }

            isRegistered = true;

            if (IsConfiguredForGameToDiscordRelay() && config.RelayGameToDiscord)
            {
                api.Event.PlayerChat += OnPlayerChat;
                api.Event.PlayerJoin += OnPlayerJoin;
                api.Event.PlayerDisconnect += OnPlayerDisconnect;
                api.Event.PlayerDeath += OnPlayerDeath;
            }

            if (IsConfiguredForDiscordToGameRelay() && config.RelayDiscordToGame)
            {
                api.Event.RegisterGameTickListener(OnDiscordPollTick, config.PollMs);
            }

            if (IsConfiguredForGameToDiscordRelay() && config.RelayGameToDiscord && config.RelayWorldUpdates)
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
            config = configStore.Load();
        }

        private void RestoreLastMessageId()
        {
            string saved = lastMessageStore.Load();
            if (!string.IsNullOrWhiteSpace(saved))
                lastMessageId = saved;
        }

        private bool IsConfiguredForDiscordToGameRelay()
        {
            return configurationValidator.IsReadyForDiscordToGame(config);
        }

        private bool IsConfiguredForGameToDiscordRelay()
        {
            return configurationValidator.IsReadyForGameToDiscord(config);
        }

        // =========================================================
        // GAME → DISCORD
        // =========================================================

        public void OnPlayerChat(IServerPlayer player, int channelId, ref string message, ref string data, BoolRef consumed)
        {
            if (!IsConfiguredForGameToDiscordRelay()) return;
            if (!config.RelayGameToDiscord) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            // Ignore command prefixes (like /)
            foreach (var prefix in config.IgnoreGamePrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix) && message.StartsWith(prefix))
                    return;
            }

            string clean = StripVsFormatting(message);

            clean = relayMessageNormalizer.NormalizePlayerChat(player.PlayerName, clean);

            _ = SendPlainToDiscord(
                player.PlayerUID,
                player.PlayerName,
                clean
            );
        }

        private string StripVsFormatting(string input)
        {
            return messageTranslator.StripVsFormatting(input);
        }

        private async Task SendPlainToDiscord(string playerUid, string username, string message)
        {
            if (!IsConfiguredForGameToDiscordRelay()) return;

            string mappedMessage = messageTranslator.ReplaceGameMentionsWithDiscordMentions(message, config.GameMentionMap);
            string avatarUrl = avatarService == null
                ? null
                : await avatarService.TryGetAvatarUrlAsync(playerUid);

            var payload = new
            {
                username = username,
                content = mappedMessage,
                avatar_url = avatarUrl,
                allowed_mentions = new
                {
                    parse = new[] { "users" }
                }
            };

            string json = JsonSerializer.Serialize(payload);

            await webhookClient.PostJsonAsync(config.WebhookUrl, json);
        }


        private string ReplaceGameMentionsWithDiscordMentions(string message)
        {
            return messageTranslator.ReplaceGameMentionsWithDiscordMentions(message, config?.GameMentionMap);
        }

        private async Task SendEmbedToDiscord(string username, string description, int color)
        {
            if (!IsConfiguredForGameToDiscordRelay()) return;

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

            await webhookClient.PostJsonAsync(config.WebhookUrl, json);
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            if (!IsConfiguredForGameToDiscordRelay() || !config.RelayGameToDiscord) return;
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
            if (!IsConfiguredForGameToDiscordRelay() || !config.RelayGameToDiscord) return;
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
            if (!IsConfiguredForGameToDiscordRelay() || !config.RelayGameToDiscord) return;
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

            _ = SendPlainToDiscord(null, "Death", $"💀 {deathMessage}");
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
            if (!IsConfiguredForGameToDiscordRelay() || !config.RelayGameToDiscord || !config.RelayWorldUpdates) return;
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
            if (!IsConfiguredForDiscordToGameRelay()) return;
            if (!config.RelayDiscordToGame) return;

            try
            {
                List<JsonElement> pendingMessages = await LoadPendingDiscordMessagesAsync();
                if (pendingMessages.Count == 0) return;

                foreach (JsonElement msg in pendingMessages.AsEnumerable().Reverse())
                {
                    RelayDiscordMessageToGame(msg);
                }

                lastMessageStore.Save(lastMessageId);
            }
            catch (Exception e)
            {
                api.Logger.Error($"[FirstStepsTweaks] Discord polling exception: {e.Message}");
            }
        }

        private async Task<List<JsonElement>> LoadPendingDiscordMessagesAsync()
        {
            var pendingMessages = new List<JsonElement>();

            if (string.IsNullOrWhiteSpace(lastMessageId))
            {
                pendingMessages.AddRange(await LoadDiscordMessagePageAsync(null));
                return pendingMessages;
            }

            string beforeMessageId = null;
            bool reachedLastProcessedMessage = false;

            while (true)
            {
                JsonElement[] page = await LoadDiscordMessagePageAsync(beforeMessageId);
                if (page.Length == 0)
                {
                    break;
                }

                foreach (JsonElement message in page)
                {
                    if (IsLastProcessedMessage(message))
                    {
                        reachedLastProcessedMessage = true;
                        break;
                    }

                    pendingMessages.Add(message);
                }

                if (reachedLastProcessedMessage || page.Length < DiscordMessagePageSize)
                {
                    break;
                }

                string oldestMessageId = TryGetMessageId(page[^1]);
                if (string.IsNullOrWhiteSpace(oldestMessageId))
                {
                    break;
                }

                beforeMessageId = oldestMessageId;
            }

            return pendingMessages;
        }

        private async Task<JsonElement[]> LoadDiscordMessagePageAsync(string beforeMessageId)
        {
            string url = BuildDiscordMessagesUrl(beforeMessageId);

            DiscordHttpResponse response = await webhookClient.GetAsync(url, config.BotToken);
            if (response.StatusCode == 429)
            {
                api.Logger.Warning("[FirstStepsTweaks] Discord rate limited.");
                return Array.Empty<JsonElement>();
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                throw new InvalidOperationException($"Discord returned status code {response.StatusCode}.");
            }

            string json = response.Body;
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<JsonElement>();
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                return Array.Empty<JsonElement>();
            }

            return root.EnumerateArray()
                .Select(message => message.Clone())
                .ToArray();
        }

        private string BuildDiscordMessagesUrl(string beforeMessageId)
        {
            if (string.IsNullOrWhiteSpace(beforeMessageId))
            {
                return $"https://discord.com/api/v10/channels/{config.ChannelId}/messages?limit={DiscordMessagePageSize}";
            }

            return $"https://discord.com/api/v10/channels/{config.ChannelId}/messages?before={beforeMessageId}&limit={DiscordMessagePageSize}";
        }

        private bool IsLastProcessedMessage(JsonElement message)
        {
            return string.Equals(TryGetMessageId(message), lastMessageId, StringComparison.Ordinal);
        }

        private string TryGetMessageId(JsonElement message)
        {
            if (!message.TryGetProperty("id", out JsonElement idProp))
            {
                return null;
            }

            return idProp.GetString();
        }

        private void RelayDiscordMessageToGame(JsonElement msg)
        {
            string id = TryGetMessageId(msg);
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (msg.TryGetProperty("author", out JsonElement author) &&
                author.TryGetProperty("bot", out JsonElement botProp) &&
                botProp.ValueKind == JsonValueKind.True)
            {
                lastMessageId = id;
                return;
            }

            string content = msg.TryGetProperty("content", out JsonElement contentProp)
                ? contentProp.GetString()
                : "";

            content = SanitizeDiscordContentForGame(msg, content);

            if (config.IgnoreEmptyDiscordMessages && string.IsNullOrWhiteSpace(content))
            {
                lastMessageId = id;
                return;
            }

            foreach (string prefix in config.IgnoreDiscordPrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix) && content.StartsWith(prefix))
                {
                    lastMessageId = id;
                    return;
                }
            }

            string username = "Discord";
            if (msg.TryGetProperty("author", out JsonElement authorObj) &&
                authorObj.TryGetProperty("username", out JsonElement userProp))
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

        private string SanitizeDiscordContentForGame(JsonElement msg, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            var mentions = new System.Collections.Generic.List<DiscordMention>();
            if (msg.TryGetProperty("mentions", out JsonElement mentionElements) && mentionElements.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement mention in mentionElements.EnumerateArray())
                {
                    mentions.Add(new DiscordMention
                    {
                        Id = mention.TryGetProperty("id", out JsonElement idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
                        GlobalName = mention.TryGetProperty("global_name", out JsonElement globalNameProp) ? globalNameProp.GetString() ?? string.Empty : string.Empty,
                        Username = mention.TryGetProperty("username", out JsonElement usernameProp) ? usernameProp.GetString() ?? string.Empty : string.Empty
                    });
                }
            }

            return messageTranslator.SanitizeDiscordContentForGame(content, mentions);
        }
    }
}
