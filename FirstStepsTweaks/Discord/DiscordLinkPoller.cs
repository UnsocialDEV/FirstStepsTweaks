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
        private const int DiscordMessagePageSize = 100;
        private const int MaxPagesPerPoll = 10;
        private readonly ICoreServerAPI api;
        private readonly DiscordBridgeConfig config;
        private readonly IDiscordWebhookClient webhookClient;
        private readonly IDiscordLinkLastMessageStore lastMessageStore;
        private readonly DiscordLinkService linkService;
        private readonly DiscordLinkRewardService rewardService;
        private readonly DiscordLinkCodeMessageParser parser;
        private readonly IPlayerLookup playerLookup;
        private readonly PlayerDonatorRoleSyncService roleSyncService;
        private readonly IPlayerMessenger messenger;
        private readonly DiscordLinkPollerStatusTracker statusTracker;
        private readonly int maxPagesPerPoll;
        private readonly SemaphoreSlim pollLock = new SemaphoreSlim(1, 1);
        private string lastMessageId;
        private string activeFailureSummary = string.Empty;
        private bool activeProcessingCapWarning;
        private bool isRegistered;

        public DiscordLinkPoller(
            ICoreServerAPI api,
            DiscordBridgeConfig config,
            IDiscordWebhookClient webhookClient,
            IDiscordLinkLastMessageStore lastMessageStore,
            DiscordLinkService linkService,
            DiscordLinkRewardService rewardService,
            DiscordLinkCodeMessageParser parser,
            IPlayerLookup playerLookup,
            PlayerDonatorRoleSyncService roleSyncService,
            IPlayerMessenger messenger,
            DiscordLinkPollerStatusTracker statusTracker,
            int maxPagesPerPoll = MaxPagesPerPoll)
        {
            this.api = api;
            this.config = config;
            this.webhookClient = webhookClient;
            this.lastMessageStore = lastMessageStore;
            this.linkService = linkService;
            this.rewardService = rewardService;
            this.parser = parser;
            this.playerLookup = playerLookup;
            this.roleSyncService = roleSyncService;
            this.messenger = messenger;
            this.statusTracker = statusTracker;
            this.maxPagesPerPoll = Math.Max(1, maxPagesPerPoll);
            lastMessageId = lastMessageStore.Load();
        }

        public void Register()
        {
            if (isRegistered)
            {
                return;
            }

            if (!IsConfigured())
            {
                return;
            }

            isRegistered = true;
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
            DateTime startedAtUtc = DateTime.UtcNow;
            bool shouldPrimeLastMessageId = string.IsNullOrWhiteSpace(lastMessageId);
            string url = BuildMessagesUrl(shouldPrimeLastMessageId, lastMessageId);

            DiscordHttpResponse response;
            try
            {
                response = await webhookClient.GetAsync(url, config.BotToken);
            }
            catch (TaskCanceledException)
            {
                HandlePollFailure($"Discord link poll request timed out after {DiscordWebhookClient.RequestTimeoutSeconds} seconds.");
                return;
            }
            catch (Exception exception)
            {
                HandlePollFailure($"Discord link poll request failed: {exception.GetType().Name}: {exception.Message}");
                return;
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                HandlePollFailure(BuildPollFailureSummary(response.StatusCode));
                return;
            }

            if (string.IsNullOrWhiteSpace(response.Body))
            {
                HandlePollFailure($"Discord link poll returned an empty response body with status code {response.StatusCode}.");
                return;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(response.Body);
            }
            catch (JsonException exception)
            {
                HandlePollFailure($"Discord link poll returned invalid JSON: {exception.Message}");
                return;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
                {
                    RecordSuccessfulPoll(startedAtUtc, 0, 0, false);
                    return;
                }

                if (shouldPrimeLastMessageId)
                {
                    lastMessageId = TryGetNewestMessageId(document.RootElement);
                    if (!string.IsNullOrWhiteSpace(lastMessageId))
                    {
                        SaveLastMessageId(lastMessageId);
                    }

                    RecordSuccessfulPoll(startedAtUtc, 0, 0, false);
                    return;
                }

                int processedPageCount = 0;
                int processedMessageCount = 0;
                bool reachedProcessingCap = false;
                JsonElement[] currentPage = CloneMessages(document.RootElement);

                while (currentPage.Length > 0)
                {
                    processedPageCount++;
                    processedMessageCount += await ProcessPageAsync(currentPage);
                    SaveLastMessageId(lastMessageId);

                    if (processedPageCount >= maxPagesPerPoll)
                    {
                        reachedProcessingCap = currentPage.Length == DiscordMessagePageSize;
                        break;
                    }

                    if (currentPage.Length < DiscordMessagePageSize)
                    {
                        break;
                    }

                    try
                    {
                        currentPage = await LoadMessagePageAsync(BuildMessagesUrl(false, lastMessageId));
                    }
                    catch (TaskCanceledException)
                    {
                        HandlePollFailure($"Discord link poll request timed out after {DiscordWebhookClient.RequestTimeoutSeconds} seconds.");
                        return;
                    }
                    catch (JsonException exception)
                    {
                        HandlePollFailure($"Discord link poll returned invalid JSON: {exception.Message}");
                        return;
                    }
                    catch (Exception exception)
                    {
                        HandlePollFailure(exception.Message);
                        return;
                    }
                }

                RecordSuccessfulPoll(startedAtUtc, processedPageCount, processedMessageCount, reachedProcessingCap);
            }
        }

        private string BuildMessagesUrl(bool shouldPrimeLastMessageId, string currentLastMessageId)
        {
            return shouldPrimeLastMessageId
                ? $"https://discord.com/api/v10/channels/{config.LinkChannelId}/messages?limit={DiscordMessagePageSize}"
                : $"https://discord.com/api/v10/channels/{config.LinkChannelId}/messages?after={currentLastMessageId}&limit={DiscordMessagePageSize}";
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

        private async Task<JsonElement[]> LoadMessagePageAsync(string url)
        {
            DiscordHttpResponse response = await webhookClient.GetAsync(url, config.BotToken);

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                throw new InvalidOperationException(BuildPollFailureSummary(response.StatusCode));
            }

            if (string.IsNullOrWhiteSpace(response.Body))
            {
                throw new InvalidOperationException($"Discord link poll returned an empty response body with status code {response.StatusCode}.");
            }

            using JsonDocument document = JsonDocument.Parse(response.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return Array.Empty<JsonElement>();
            }

            return CloneMessages(document.RootElement);
        }

        private static JsonElement[] CloneMessages(JsonElement messages)
        {
            return messages.EnumerateArray()
                .Select(message => message.Clone())
                .ToArray();
        }

        private async Task<int> ProcessPageAsync(JsonElement[] messages)
        {
            int processedMessageCount = 0;

            foreach (JsonElement message in messages.Reverse())
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

                processedMessageCount++;

                string content = message.TryGetProperty("content", out JsonElement contentElement)
                    ? contentElement.GetString() ?? string.Empty
                    : string.Empty;

                string discordUserId = TryGetAuthorId(message);
                LinkCompletionResult completion = TryCompleteLink(discordUserId, content);
                if (completion.LinkSucceeded)
                {
                    await SendChannelMessageAsync(
                        $"<@{discordUserId}> link successful. Your donator role will sync in game now, or on your next join.");
                    await SyncLinkedPlayerAsync(completion.Player, completion.RewardOutcome);
                }
                else if (parser.TryParseCandidateCode(content, out _))
                {
                    await SendChannelMessageAsync(
                        $"<@{discordUserId}> link failed. Run `/discordlink` in game to get a fresh code, then post that new code here.");
                }

                lastMessageId = messageId;
            }

            return processedMessageCount;
        }

        private LinkCompletionResult TryCompleteLink(string discordUserId, string content)
        {
            if (string.IsNullOrWhiteSpace(discordUserId))
            {
                return LinkCompletionResult.None;
            }

            if (!linkService.TryCompleteLink(discordUserId, content, DateTime.UtcNow, out string playerUid))
            {
                return LinkCompletionResult.None;
            }

            IServerPlayer player = playerLookup.FindOnlinePlayerByUid(playerUid);
            DiscordLinkRewardOutcome rewardOutcome = rewardService.HandleSuccessfulLink(playerUid, player);
            return new LinkCompletionResult(true, player, rewardOutcome);
        }

        private async Task SyncLinkedPlayerAsync(IServerPlayer player, DiscordLinkRewardOutcome rewardOutcome)
        {
            if (player == null)
            {
                return;
            }

            await roleSyncService.SyncAsync(player);

            messenger.SendDual(
                player,
                BuildLinkedMessage(rewardOutcome),
                GlobalConstants.InfoLogChatGroup,
                (int)EnumChatType.Notification,
                GlobalConstants.GeneralChatGroup,
                (int)EnumChatType.Notification);
        }

        private void SaveLastMessageId(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return;
            }

            lastMessageStore.Save(messageId);
        }

        private void RecordSuccessfulPoll(DateTime occurredAtUtc, int processedPageCount, int processedMessageCount, bool reachedProcessingCap)
        {
            activeFailureSummary = string.Empty;
            if (reachedProcessingCap)
            {
                    if (!activeProcessingCapWarning)
                    {
                        activeProcessingCapWarning = true;
                        LogWarning($"[FirstStepsTweaks] Discord link poll reached the per-poll processing cap of {maxPagesPerPoll} pages. Remaining backlog will continue draining on the next poll.");
                    }
            }
            else
            {
                activeProcessingCapWarning = false;
            }

            statusTracker.RecordSuccess(occurredAtUtc, processedPageCount, processedMessageCount, reachedProcessingCap);
        }

        private void HandlePollFailure(string failureSummary)
        {
            statusTracker.RecordFailure(failureSummary);
            activeProcessingCapWarning = false;
            if (string.Equals(activeFailureSummary, failureSummary, StringComparison.Ordinal))
            {
                return;
            }

            activeFailureSummary = failureSummary;
            LogWarning($"[FirstStepsTweaks] {failureSummary}");
        }

        private static string BuildPollFailureSummary(int statusCode)
        {
            switch (statusCode)
            {
                case 401:
                    return "Discord link poll returned 401 Unauthorized. Check the configured bot token.";
                case 403:
                    return "Discord link poll returned 403 Forbidden. Check the bot permissions for the link channel.";
                case 404:
                    return "Discord link poll returned 404 Not Found. Check the configured link channel ID.";
                case 429:
                    return "Discord link poll returned 429 Too Many Requests. The bot is currently rate limited.";
                default:
                    return $"Discord link poll returned status code {statusCode}.";
            }
        }

        private void LogWarning(string message)
        {
            if (api == null)
            {
                return;
            }

            api.Logger.Warning(message);
        }

        private static string FormatCursor(string cursor)
        {
            return string.IsNullOrWhiteSpace(cursor) ? "unset" : cursor;
        }

        private static string BuildLinkedMessage(DiscordLinkRewardOutcome rewardOutcome)
        {
            switch (rewardOutcome)
            {
                case DiscordLinkRewardOutcome.GrantedImmediately:
                    return "Discord account linked. Donator role sync requested. You received 10 rusty gears.";
                case DiscordLinkRewardOutcome.QueuedForNextJoin:
                    return "Discord account linked. Donator role sync requested. Your 10 rusty gears will be delivered on your next join.";
                default:
                    return "Discord account linked. Donator role sync requested.";
            }
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

        private readonly struct LinkCompletionResult
        {
            public static LinkCompletionResult None => new(false, null, DiscordLinkRewardOutcome.None);

            public LinkCompletionResult(bool linkSucceeded, IServerPlayer player, DiscordLinkRewardOutcome rewardOutcome)
            {
                LinkSucceeded = linkSucceeded;
                Player = player;
                RewardOutcome = rewardOutcome;
            }

            public bool LinkSucceeded { get; }

            public IServerPlayer Player { get; }

            public DiscordLinkRewardOutcome RewardOutcome { get; }
        }
    }
}
