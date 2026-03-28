using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FirstStepsTweaks.Discord.Transport;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkPoller
    {
        private readonly ICoreServerAPI api;
        private readonly DiscordBridgeConfig config;
        private readonly IDiscordWebhookClient webhookClient;
        private readonly IDiscordLinkLastMessageStore lastMessageStore;
        private readonly DiscordLinkService linkService;
        private readonly DiscordLinkCodeMessageParser parser;
        private readonly IPlayerLookup playerLookup;
        private readonly PlayerDonatorRoleSyncService roleSyncService;
        private readonly IPlayerMessenger messenger;
        private readonly SemaphoreSlim pollLock = new SemaphoreSlim(1, 1);
        private string lastMessageId;

        public DiscordLinkPoller(
            ICoreServerAPI api,
            DiscordBridgeConfig config,
            IDiscordWebhookClient webhookClient,
            IDiscordLinkLastMessageStore lastMessageStore,
            DiscordLinkService linkService,
            DiscordLinkCodeMessageParser parser,
            IPlayerLookup playerLookup,
            PlayerDonatorRoleSyncService roleSyncService,
            IPlayerMessenger messenger)
        {
            this.api = api;
            this.config = config;
            this.webhookClient = webhookClient;
            this.lastMessageStore = lastMessageStore;
            this.linkService = linkService;
            this.parser = parser;
            this.playerLookup = playerLookup;
            this.roleSyncService = roleSyncService;
            this.messenger = messenger;
            lastMessageId = lastMessageStore.Load();
        }

        public void Register()
        {
            if (!IsConfigured())
            {
                return;
            }

            api.Event.RegisterGameTickListener(OnPollTick, config.PollMs);
        }

        private bool IsConfigured()
        {
            return config != null
                && !string.IsNullOrWhiteSpace(config.BotToken)
                && !string.IsNullOrWhiteSpace(config.LinkChannelId);
        }

        private async void OnPollTick(float dt)
        {
            if (!await pollLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                await PollOnceAsync();
            }
            catch (Exception exception)
            {
                api.Logger.Error($"[FirstStepsTweaks] Discord link poll error: {exception}");
            }
            finally
            {
                pollLock.Release();
            }
        }

        private async Task PollOnceAsync()
        {
            bool shouldPrimeLastMessageId = string.IsNullOrWhiteSpace(lastMessageId);
            string url = shouldPrimeLastMessageId
                ? $"https://discord.com/api/v10/channels/{config.LinkChannelId}/messages?limit=100"
                : $"https://discord.com/api/v10/channels/{config.LinkChannelId}/messages?after={lastMessageId}&limit=100";

            DiscordHttpResponse response = await webhookClient.GetAsync(url, config.BotToken);
            if (response.StatusCode == 429)
            {
                api.Logger.Warning("[FirstStepsTweaks] Discord link poll was rate limited.");
                return;
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300 || string.IsNullOrWhiteSpace(response.Body))
            {
                return;
            }

            using JsonDocument document = JsonDocument.Parse(response.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return;
            }

            if (shouldPrimeLastMessageId)
            {
                lastMessageId = TryGetNewestMessageId(document.RootElement);
                if (!string.IsNullOrWhiteSpace(lastMessageId))
                {
                    lastMessageStore.Save(lastMessageId);
                }

                return;
            }

            foreach (JsonElement message in document.RootElement.EnumerateArray().Reverse())
            {
                if (!message.TryGetProperty("id", out JsonElement idElement))
                {
                    continue;
                }

                string messageId = idElement.GetString();
                if (string.IsNullOrWhiteSpace(messageId))
                {
                    continue;
                }

                if (IsBotMessage(message))
                {
                    lastMessageId = messageId;
                    continue;
                }

                string content = message.TryGetProperty("content", out JsonElement contentElement)
                    ? contentElement.GetString() ?? string.Empty
                    : string.Empty;

                string discordUserId = TryGetAuthorId(message);
                if (linkService.TryCompleteLink(discordUserId, content, DateTime.UtcNow, out string playerUid))
                {
                    await SendChannelMessageAsync(
                        $"<@{discordUserId}> link successful. Your donator role will sync in game now, or on your next join.");
                    await SyncLinkedPlayerAsync(playerUid);
                }
                else if (parser.TryParseCandidateCode(content, out _))
                {
                    await SendChannelMessageAsync(
                        $"<@{discordUserId}> link failed. Run `/discordlink` in game to get a fresh code, then post that new code here.");
                }

                lastMessageId = messageId;
            }

            lastMessageStore.Save(lastMessageId);
        }

        private static string TryGetNewestMessageId(JsonElement messages)
        {
            foreach (JsonElement message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("id", out JsonElement idElement))
                {
                    continue;
                }

                string messageId = idElement.GetString();
                if (!string.IsNullOrWhiteSpace(messageId))
                {
                    return messageId;
                }
            }

            return null;
        }

        private async Task SyncLinkedPlayerAsync(string playerUid)
        {
            IServerPlayer player = playerLookup.FindOnlinePlayerByUid(playerUid);
            if (player == null)
            {
                return;
            }

            await roleSyncService.SyncAsync(player);

            messenger.SendDual(
                player,
                "Discord account linked. Donator role sync requested.",
                GlobalConstants.InfoLogChatGroup,
                (int)EnumChatType.Notification,
                GlobalConstants.GeneralChatGroup,
                (int)EnumChatType.Notification);
        }

        private static bool IsBotMessage(JsonElement message)
        {
            return message.TryGetProperty("author", out JsonElement author)
                && author.TryGetProperty("bot", out JsonElement botElement)
                && botElement.ValueKind == JsonValueKind.True;
        }

        private static string TryGetAuthorId(JsonElement message)
        {
            if (!message.TryGetProperty("author", out JsonElement author)
                || !author.TryGetProperty("id", out JsonElement authorIdElement))
            {
                return null;
            }

            return authorIdElement.GetString();
        }

        private async Task SendChannelMessageAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            string payload = JsonSerializer.Serialize(new
            {
                content
            });

            DiscordHttpResponse response = await webhookClient.PostBotJsonAsync(
                $"https://discord.com/api/v10/channels/{config.LinkChannelId}/messages",
                config.BotToken,
                payload);

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                api.Logger.Warning($"[FirstStepsTweaks] Discord link reply failed with status code {response.StatusCode}.");
            }
        }
    }
}
